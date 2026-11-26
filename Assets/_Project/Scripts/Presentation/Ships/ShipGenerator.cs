using System.Collections.Generic;
using Space.Core.Equipment;
using UnityEngine;
using Random = System.Random;

namespace Space.Presentation.Ships
{
    /// <summary>
    /// Builds a medium ship from its <see cref="ShipDesign"/>: a lofted hull, a deck, cargo modules,
    /// a bridge, engine nacelles, wings and a few lights, all as chunky flat-shaded blocks painted
    /// with vertex colours. The silhouette comes first: detail is left to the toon shading.
    /// Ship space: +Z forward (nose at +Length/2), +Y up, X to starboard. Deterministic.
    /// </summary>
    public static class ShipGenerator
    {
        public static GeneratedShip Generate(in ShipDesign design) => new Builder(design).Build();

        /// <summary>Emission mask in the vertex alpha: windows glow a little, nozzles and lights fully.</summary>
        public const float WindowGlow = 0.55f;
        public const float FullGlow = 1f;

        static Color Glowing(Color c, float amount) => new(c.r, c.g, c.b, amount);
        static Color Shade(Color c, float factor) => new(c.r * factor, c.g * factor, c.b * factor, c.a);

        /// <summary>A cross-section of a lofted body at a fraction of the hull length.</summary>
        struct Section
        {
            public float T, W, H, Y, ChamferTop, ChamferBottom, TopScale;
            /// <summary>Colour of the span from this section to the next one.</summary>
            public Color Band;
        }

        sealed class Body
        {
            public float X;
            public readonly List<Section> Sections = new();

            public Section Sample(float t)
            {
                var s = Sections;
                if (t <= s[0].T) return s[0];
                for (var i = 0; i < s.Count - 1; i++)
                {
                    if (t > s[i + 1].T) continue;
                    var k = Mathf.InverseLerp(s[i].T, s[i + 1].T, t);
                    return new Section
                    {
                        T = t,
                        W = Mathf.Lerp(s[i].W, s[i + 1].W, k),
                        H = Mathf.Lerp(s[i].H, s[i + 1].H, k),
                        Y = Mathf.Lerp(s[i].Y, s[i + 1].Y, k),
                        ChamferTop = Mathf.Lerp(s[i].ChamferTop, s[i + 1].ChamferTop, k),
                        ChamferBottom = Mathf.Lerp(s[i].ChamferBottom, s[i + 1].ChamferBottom, k),
                        TopScale = Mathf.Lerp(s[i].TopScale, s[i + 1].TopScale, k),
                        Band = s[i].Band,
                    };
                }
                return s[^1];
            }
        }

        /// <summary>Height of the top surface along one lane (a line along Z), sampled every <see cref="Step"/> metres.</summary>
        sealed class Lane
        {
            public const float Step = 0.25f;
            public readonly float X;
            public readonly float ZMin;
            public readonly float[] Top;
            public readonly bool[] Blocked;

            public Lane(float x, float zMin, float zMax)
            {
                X = x;
                ZMin = zMin;
                var n = Mathf.CeilToInt((zMax - zMin) / Step) + 1;
                Top = new float[n];
                Blocked = new bool[n];
                for (var i = 0; i < n; i++) Top[i] = float.NegativeInfinity;
            }

            public float ZAt(int i) => ZMin + i * Step;

            public void Raise(float z0, float z1, float y, bool block = false)
            {
                var i0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(z0, z1) - ZMin) / Step), 0, Top.Length - 1);
                var i1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(z0, z1) - ZMin) / Step), 0, Top.Length - 1);
                for (var i = i0; i <= i1; i++)
                {
                    Top[i] = Mathf.Max(Top[i], y);
                    Blocked[i] |= block;
                }
            }
        }

        sealed class Builder
        {
            readonly ShipDesign _d;
            readonly ShipPalette _p;
            readonly Random _rng;
            readonly ShipMeshData _m = new();
            readonly GeneratedShip _ship = new();

            readonly float _l, _w, _h, _zTail, _zNose, _overhang;
            readonly Body _main = new();
            Body _side;            // twin hulls (mirrored), null otherwise
            Lane _center;
            Lane _pair;            // top of the twin hulls
            float _cargo;

            public Builder(in ShipDesign design)
            {
                _d = design;
                _p = design.Palette;
                _rng = new Random(design.Seed);
                _l = design.Length;
                _w = _l * Range(0.12f, 0.145f);
                _h = _l * Range(0.085f, 0.1f);
                _overhang = _l * 0.06f;
                _zNose = _l * 0.5f;
                _zTail = -_l * 0.5f + _overhang;
            }

            float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
            bool Chance(float p) => _rng.NextDouble() < p;
            float Z(float t) => Mathf.Lerp(_zTail, _zNose, t);

            public GeneratedShip Build()
            {
                _center = new Lane(0f, -_l * 0.5f, _l * 0.5f);
                BuildHull();
                BuildCargo();
                BuildDeck();
                BuildBridge();
                BuildEngines();
                BuildWings();
                BuildLights();
                PlaceHardpoints();

                _m.ComputeOutlineNormals();
                _ship.Design = _d;
                _ship.Hull = _m;
                _ship.Bounds = _m.ComputeBounds();
                _ship.CargoVolume = _cargo;
                return _ship;
            }

            // ------------------------------------------------------------------ Hull

            void AddSection(Body body, float t, float w, float h, float y, float chamferTop, float chamferBottom, float topScale, Color band)
            {
                body.Sections.Add(new Section
                {
                    T = t, W = w, H = h, Y = y, ChamferTop = chamferTop, ChamferBottom = chamferBottom,
                    TopScale = topScale, Band = band,
                });
            }

            void BuildHull()
            {
                var w = _w;
                var h = _h;
                var stripe = _p.Accent;
                var main = _p.Primary;
                var tail = _p.Secondary;
                switch (_d.Hull)
                {
                    case HullForm.Brick:
                    {
                        var ct = h * 0.32f; var cb = h * 0.2f; const float ts = 0.92f;
                        AddSection(_main, 0f, w * 0.86f, h * 0.84f, 0f, ct, cb, ts, tail);
                        AddSection(_main, 0.05f, w, h, 0f, ct, cb, ts, main);
                        AddSection(_main, 0.6f, w, h, 0f, ct, cb, ts, stripe);
                        AddSection(_main, 0.64f, w, h, 0f, ct, cb, ts, main);
                        AddSection(_main, 0.84f, w, h, 0f, ct, cb, ts, tail);
                        AddSection(_main, 0.94f, w * 0.92f, h * 0.84f, h * 0.08f, ct, cb, ts, tail);
                        AddSection(_main, 1f, w * 0.68f, h * 0.55f, h * 0.16f, ct * 0.8f, cb * 0.8f, ts, tail);
                        break;
                    }
                    case HullForm.Wedge:
                    {
                        var ct = h * 0.4f; var cb = h * 0.24f; const float ts = 0.78f;
                        AddSection(_main, 0f, w * 0.9f, h * 0.9f, 0f, ct, cb, ts, tail);
                        AddSection(_main, 0.06f, w, h, 0f, ct, cb, ts, main);
                        AddSection(_main, 0.46f, w, h, 0f, ct, cb, ts, stripe);
                        AddSection(_main, 0.5f, w, h, 0f, ct, cb, ts, main);
                        AddSection(_main, 0.72f, w * 0.9f, h * 0.82f, -h * 0.1f, ct * 0.8f, cb, ts, main);
                        AddSection(_main, 1f, w * 0.3f, h * 0.22f, -h * 0.42f, ct * 0.2f, cb * 0.2f, ts, main);
                        break;
                    }
                    case HullForm.Rounded:
                    {
                        var c = Mathf.Min(w, h) * 0.55f; const float ts = 0.95f;
                        AddSection(_main, 0f, w * 0.8f, h * 0.8f, 0f, c * 0.8f, c * 0.8f, ts, tail);
                        AddSection(_main, 0.07f, w, h, 0f, c, c, ts, main);
                        AddSection(_main, 0.55f, w * 1.05f, h * 1.06f, h * 0.02f, c, c, ts, stripe);
                        AddSection(_main, 0.59f, w * 1.05f, h * 1.06f, h * 0.02f, c, c, ts, main);
                        AddSection(_main, 0.84f, w * 0.96f, h * 0.96f, h * 0.06f, c, c, ts, main);
                        AddSection(_main, 0.95f, w * 0.72f, h * 0.7f, h * 0.08f, c * 0.75f, c * 0.75f, ts, main);
                        AddSection(_main, 1f, w * 0.42f, h * 0.4f, h * 0.08f, c * 0.4f, c * 0.4f, ts, main);
                        break;
                    }
                    case HullForm.Spine:
                    {
                        // Engine module at the back, a narrow keel, a big head with the bridge.
                        var ct = h * 0.3f; var cb = h * 0.2f;
                        AddSection(_main, 0f, w * 0.82f, h * 0.85f, 0f, ct, cb, 0.9f, tail);
                        AddSection(_main, 0.12f, w * 0.82f, h * 0.85f, 0f, ct, cb, 0.9f, tail);
                        AddSection(_main, 0.14f, w * 0.36f, h * 0.4f, 0f, h * 0.12f, h * 0.12f, 1f, _p.Metal);
                        AddSection(_main, 0.74f, w * 0.36f, h * 0.4f, 0f, h * 0.12f, h * 0.12f, 1f, _p.Metal);
                        AddSection(_main, 0.76f, w * 0.78f, h * 0.95f, 0f, ct, cb, 0.88f, main);
                        AddSection(_main, 0.86f, w * 0.78f, h * 0.95f, 0f, ct, cb, 0.88f, stripe);
                        AddSection(_main, 0.89f, w * 0.78f, h * 0.95f, 0f, ct, cb, 0.88f, main);
                        AddSection(_main, 0.95f, w * 0.7f, h * 0.8f, -h * 0.05f, ct, cb, 0.88f, main);
                        AddSection(_main, 1f, w * 0.48f, h * 0.5f, -h * 0.12f, ct * 0.7f, cb * 0.7f, 0.88f, main);
                        break;
                    }
                    case HullForm.Twin:
                    {
                        // Central pod (cockpit and cargo bridge) plus two slim hulls carrying the engines.
                        var c = h * 0.3f;
                        AddSection(_main, 0.18f, w * 0.3f, h * 0.55f, h * 0.15f, c, c, 0.9f, _p.Metal);
                        AddSection(_main, 0.22f, w * 0.4f, h * 0.65f, h * 0.15f, c, c, 0.9f, main);
                        AddSection(_main, 0.8f, w * 0.4f, h * 0.65f, h * 0.15f, c, c, 0.9f, tail);
                        AddSection(_main, 1f, w * 0.22f, h * 0.32f, h * 0.05f, c * 0.6f, c * 0.6f, 0.9f, tail);

                        _side = new Body { X = w * 0.95f };
                        var sw = w * 0.42f; var sh = h * 0.8f;
                        AddSection(_side, 0f, sw * 0.9f, sh * 0.9f, 0f, sh * 0.35f, sh * 0.25f, 0.85f, tail);
                        AddSection(_side, 0.05f, sw, sh, 0f, sh * 0.35f, sh * 0.25f, 0.85f, main);
                        AddSection(_side, 0.42f, sw, sh, 0f, sh * 0.35f, sh * 0.25f, 0.85f, stripe);
                        AddSection(_side, 0.46f, sw, sh, 0f, sh * 0.35f, sh * 0.25f, 0.85f, main);
                        AddSection(_side, 0.7f, sw * 0.95f, sh * 0.9f, -sh * 0.05f, sh * 0.35f, sh * 0.25f, 0.85f, main);
                        AddSection(_side, 0.86f, sw * 0.45f, sh * 0.4f, -sh * 0.2f, sh * 0.15f, sh * 0.1f, 0.85f, main);
                        break;
                    }
                }

                LoftBody(_main);
                RaiseLaneOver(_center, _main);
                if (_side != null)
                {
                    var mark = _m.Mark();
                    LoftBody(_side);
                    _m.MirrorSince(mark);
                    _pair = new Lane(_side.X, -_l * 0.5f, _l * 0.5f);
                    RaiseLaneOver(_pair, _side);

                    // Two thick struts tie the hulls to the pod.
                    foreach (var t in new[] { 0.3f, 0.62f })
                    {
                        var s = _main.Sample(t);
                        var strut = _m.Mark();
                        var span = _side.X;
                        _m.AddChamferBox(new Vector3(span * 0.5f, s.Y + _h * 0.1f, Z(t)), Quaternion.identity,
                            new Vector3(span, _h * 0.5f, _l * 0.1f), _h * 0.12f, _p.Secondary);
                        _m.MirrorSince(strut);
                    }
                }
            }

            void LoftBody(Body body)
            {
                var rings = new List<Vector3[]>();
                var bands = new List<Color>();
                foreach (var s in body.Sections)
                {
                    var ring = ShipMeshData.Octagon(s.W, s.H, s.ChamferTop, s.ChamferBottom, Z(s.T), s.Y, s.TopScale);
                    for (var i = 0; i < ring.Length; i++) ring[i].x += body.X;
                    rings.Add(ring);
                    bands.Add(s.Band);
                }
                _m.AddLoft(rings, bands);
            }

            void RaiseLaneOver(Lane lane, Body body)
            {
                var t0 = body.Sections[0].T;
                var t1 = body.Sections[^1].T;
                for (var i = 0; i < lane.Top.Length; i++)
                {
                    var t = Mathf.InverseLerp(_zTail, _zNose, lane.ZAt(i));
                    if (t < t0 || t > t1) continue;
                    var s = body.Sample(t);
                    lane.Raise(lane.ZAt(i), lane.ZAt(i), s.Y + s.H);
                }
            }

            float TopOf(Body body, float t)
            {
                var s = body.Sample(t);
                return s.Y + s.H;
            }

            float BottomOf(Body body, float t)
            {
                var s = body.Sample(t);
                return s.Y - s.H;
            }

            // ------------------------------------------------------------------ Deck

            /// <summary>Raised superstructure on the back half: layers the silhouette like stacked plates.</summary>
            void BuildDeck()
            {
                if (_d.Hull == HullForm.Spine || _d.Hull == HullForm.Twin)
                    return;
                var t0 = 0.1f;
                var t1 = _d.Bridge == BridgeLayout.Nose ? Range(0.46f, 0.54f) : Range(0.62f, 0.7f);
                var top = Mathf.Max(TopOf(_main, t0 + 0.02f), TopOf(_main, t1 - 0.02f));
                var height = _h * Range(0.22f, 0.3f);
                var width = _w * Range(0.45f, 0.58f);
                var z0 = Z(t0);
                var z1 = Z(t1);
                var color = Chance(0.5f) ? _p.Panel : _p.Secondary;
                // Sunk into the hull so it sits on the chamfered top without a gap.
                var center = new Vector3(0f, top + height * 0.5f - _h * 0.15f, (z0 + z1) * 0.5f);
                var size = new Vector3(width * 2f, height + _h * 0.3f, z1 - z0);
                _m.AddChamferBox(center, Quaternion.identity, size, height * 0.45f, color);
                _center.Raise(z0 + height * 0.5f, z1 - height * 0.5f, center.y + size.y * 0.5f);

                // An accent stripe across the deck.
                var stripeZ = Mathf.Lerp(z0, z1, 0.72f);
                _m.AddChamferBox(new Vector3(0f, center.y, stripeZ), Quaternion.identity,
                    new Vector3(size.x + 0.1f, size.y + 0.1f, _l * 0.025f), height * 0.45f, _p.Accent);
            }

            // ------------------------------------------------------------------ Cargo

            Color ContainerColor(int i)
            {
                var colors = new[] { _p.Secondary, _p.Accent, _p.Panel, _p.Secondary, _p.Primary };
                return colors[(i + _d.Seed) % colors.Length];
            }

            void AddContainer(Vector3 center, Vector3 size, Color color)
            {
                // Doors at both ends are a shade darker: reads as a container without any texture.
                _m.AddChamferBox(center, Quaternion.identity, size, Mathf.Min(size.x, size.y) * 0.12f, color, Shade(color, 0.78f));
                _cargo += size.x * size.y * size.z;
                // Two ribs around it.
                foreach (var k in new[] { -0.3f, 0.3f })
                    _m.AddChamferBox(center + new Vector3(0f, 0f, size.z * k), Quaternion.identity,
                        new Vector3(size.x * 1.04f, size.y * 1.04f, size.z * 0.06f), Mathf.Min(size.x, size.y) * 0.1f, Shade(color, 0.85f));
            }

            void BuildCargo()
            {
                switch (_d.Cargo)
                {
                    case CargoLayout.BellyContainers: BuildBellyContainers(); break;
                    case CargoLayout.SideContainers: BuildSideContainers(); break;
                    case CargoLayout.SidePods: BuildSidePods(); break;
                    case CargoLayout.SpineRack: BuildSpineRack(); break;
                    case CargoLayout.Bay: BuildBay(); break;
                }
            }

            void BuildBellyContainers()
            {
                var twin = _d.Hull == HullForm.Twin;
                var t0 = twin ? 0.24f : 0.2f;
                var t1 = twin ? 0.74f : Range(0.66f, 0.74f);
                var count = Mathf.Clamp(Mathf.RoundToInt((Z(t1) - Z(t0)) / (_l * 0.11f)), 3, 5);
                var length = (Z(t1) - Z(t0)) / count;
                var columns = twin ? 2 : (Chance(0.5f) ? 2 : 1);
                var cw = twin ? _w * 0.62f : (columns == 2 ? _w * 0.9f : _w * 1.5f);
                var ch = _h * Range(1.0f, 1.25f);
                var bottom = twin ? BottomOf(_main, 0.5f) + _h * 0.35f : Mathf.Min(BottomOf(_main, t0), BottomOf(_main, t1));
                var top = bottom + _h * 0.15f;
                var y = top - _h * 0.12f - ch * 0.5f;

                // Cradle: a plate and rails holding the containers under the hull.
                _m.AddChamferBox(new Vector3(0f, top - _h * 0.06f, (Z(t0) + Z(t1)) * 0.5f), Quaternion.identity,
                    new Vector3(cw * columns + _w * 0.2f, _h * 0.22f, Z(t1) - Z(t0) + _l * 0.02f), _h * 0.06f, _p.Metal);
                for (var i = 0; i < count; i++)
                {
                    var z = Z(t0) + length * (i + 0.5f);
                    for (var c = 0; c < columns; c++)
                    {
                        var x = columns == 1 ? 0f : (c == 0 ? -1f : 1f) * (cw * 0.5f + _w * 0.03f);
                        AddContainer(new Vector3(x, y, z), new Vector3(cw * 0.94f, ch, length * 0.9f), ContainerColor(i * 2 + c));
                    }
                }
                // Keel rail under the containers.
                _m.AddChamferBox(new Vector3(0f, y - ch * 0.5f - _h * 0.05f, (Z(t0) + Z(t1)) * 0.5f), Quaternion.identity,
                    new Vector3(_w * 0.25f, _h * 0.12f, Z(t1) - Z(t0) + _l * 0.03f), _h * 0.04f, _p.Metal);
            }

            void BuildSideContainers()
            {
                var t0 = Range(0.22f, 0.28f);
                var t1 = Range(0.66f, 0.72f);
                var count = Mathf.Clamp(Mathf.RoundToInt((Z(t1) - Z(t0)) / (_l * 0.1f)), 3, 5);
                var length = (Z(t1) - Z(t0)) / count;
                var cw = _w * 0.6f;
                var ch = _h * 0.85f;
                var s = _main.Sample((t0 + t1) * 0.5f);
                var x = s.W + cw * 0.5f + _w * 0.08f;
                var mark = _m.Mark();
                // A rack: one rail between the two rows, one on top.
                _m.AddChamferBox(new Vector3(s.W + _w * 0.04f, s.Y, (Z(t0) + Z(t1)) * 0.5f), Quaternion.identity,
                    new Vector3(_w * 0.2f, _h * 0.3f, Z(t1) - Z(t0) + _l * 0.04f), _h * 0.06f, _p.Metal);
                for (var i = 0; i < count; i++)
                {
                    var z = Z(t0) + length * (i + 0.5f);
                    for (var row = 0; row < 2; row++)
                    {
                        if (i == count - 1 && row == 0 && Chance(0.4f))
                            continue; // a missing container: it is a working ship
                        var y = s.Y + (row == 0 ? 1f : -1f) * (ch * 0.5f + _h * 0.03f);
                        AddContainer(new Vector3(x, y, z), new Vector3(cw, ch, length * 0.9f), ContainerColor(i + row * 3));
                    }
                }
                _m.AddChamferBox(new Vector3(x, s.Y + ch + _h * 0.1f, (Z(t0) + Z(t1)) * 0.5f), Quaternion.identity,
                    new Vector3(cw * 0.5f, _h * 0.12f, Z(t1) - Z(t0) + _l * 0.02f), _h * 0.04f, _p.Metal);
                _m.MirrorSince(mark);
            }

            void BuildSidePods()
            {
                var t0 = Range(0.18f, 0.26f);
                var t1 = Range(0.62f, 0.7f);
                var s = _main.Sample((t0 + t1) * 0.5f);
                var r = _h * Range(0.7f, 0.85f);
                var x = s.W + r * 1.25f;
                var y = s.Y - _h * 0.15f;
                var z0 = Z(t0);
                var z1 = Z(t1);
                var mark = _m.Mark();
                var profile = new List<Vector2>
                {
                    new(0f, z0), new(r * 0.55f, z0), new(r, z0 + r * 0.45f),
                    new(r, z0 + r * 0.9f), new(r, z0 + r * 1.25f),
                    new(r, z1 - r * 1.25f), new(r, z1 - r * 0.9f),
                    new(r, z1 - r * 0.45f), new(r * 0.55f, z1), new(0f, z1),
                };
                var colors = new[]
                {
                    _p.Metal, _p.Secondary, _p.Secondary, _p.Accent, _p.Primary, _p.Accent, _p.Secondary, _p.Secondary, _p.Metal,
                };
                // Profile z values are absolute: the lathe origin only moves the pod sideways.
                _m.AddLathe(new Vector3(x, y, 0f), Quaternion.identity, profile, colors, 18);
                _cargo += Mathf.PI * r * r * (z1 - z0);
                // Pylons.
                foreach (var k in new[] { 0.25f, 0.75f })
                {
                    var z = Mathf.Lerp(z0, z1, k);
                    _m.AddChamferBox(new Vector3((s.W + x) * 0.5f, y + r * 0.1f, z), Quaternion.identity,
                        new Vector3(x - s.W + _w * 0.2f, r * 0.45f, _l * 0.05f), r * 0.1f, _p.Metal);
                }
                _m.MirrorSince(mark);
            }

            void BuildSpineRack()
            {
                // Containers on top, below and on both sides of the keel, in slices.
                var t0 = 0.16f;
                var t1 = 0.72f;
                var count = 4;
                var length = (Z(t1) - Z(t0)) / count;
                var keel = _main.Sample(0.4f);
                var cw = _w * 0.62f;
                var ch = _h * 0.8f;
                for (var i = 0; i < count; i++)
                {
                    var z = Z(t0) + length * (i + 0.5f);
                    var size = new Vector3(cw, ch, length * 0.88f);
                    // Clamp ring around the keel.
                    _m.AddChamferBox(new Vector3(0f, keel.Y, z - length * 0.47f), Quaternion.identity,
                        new Vector3(keel.W * 2f + cw * 0.3f, keel.H * 2f + ch * 0.3f, length * 0.06f), _h * 0.05f, _p.Metal);
                    // Top and bottom.
                    var top = new Vector3(0f, keel.Y + keel.H + ch * 0.5f, z);
                    AddContainer(top, new Vector3(keel.W * 2f + cw * 0.3f, ch, length * 0.88f), ContainerColor(i));
                    _center.Raise(z - length * 0.44f, z + length * 0.44f, top.y + ch * 0.5f);
                    if (i != 1 || Chance(0.5f))
                        AddContainer(new Vector3(0f, keel.Y - keel.H - ch * 0.5f, z), new Vector3(keel.W * 2f + cw * 0.3f, ch, length * 0.88f), ContainerColor(i + 2));
                    // Flanks.
                    var mark = _m.Mark();
                    AddContainer(new Vector3(keel.W + cw * 0.5f + _w * 0.03f, keel.Y, z), size, ContainerColor(i + 1));
                    _m.MirrorSince(mark);
                    _cargo += size.x * size.y * size.z; // the mirrored one
                }
            }

            void BuildBay()
            {
                // A bulging hold around the midsection with big doors on the flanks.
                var t0 = Range(0.24f, 0.3f);
                var t1 = Range(0.62f, 0.68f);
                var z0 = Z(t0);
                var z1 = Z(t1);
                var s = _main.Sample((t0 + t1) * 0.5f);
                var size = new Vector3(s.W * 2.3f, s.H * 2.3f, z1 - z0);
                var center = new Vector3(0f, s.Y - s.H * 0.1f, (z0 + z1) * 0.5f);
                _m.AddChamferBox(center, Quaternion.identity, size, s.H * 0.4f, _p.Secondary);
                _cargo += size.x * size.y * size.z * 0.7f;
                _center.Raise(z0 + s.H * 0.4f, z1 - s.H * 0.4f, center.y + size.y * 0.5f);

                var mark = _m.Mark();
                for (var i = 0; i < 2; i++)
                {
                    var z = Mathf.Lerp(z0, z1, 0.28f + i * 0.44f);
                    _m.AddChamferBox(new Vector3(size.x * 0.5f, center.y, z), Quaternion.identity,
                        new Vector3(_w * 0.1f, size.y * 0.62f, (z1 - z0) * 0.36f), _w * 0.03f, _p.Panel);
                    // Door hinge in accent.
                    _m.AddChamferBox(new Vector3(size.x * 0.5f + _w * 0.02f, center.y + size.y * 0.33f, z), Quaternion.identity,
                        new Vector3(_w * 0.12f, _h * 0.1f, (z1 - z0) * 0.38f), _w * 0.02f, _p.Accent);
                }
                _m.MirrorSince(mark);
            }

            // ------------------------------------------------------------------ Bridge

            /// <summary>
            /// Block with a sloped front: the slope is the window strip. Blocks the lane so no turret
            /// lands on it.
            /// </summary>
            void AddBridgeBlock(float z0, float z1, float yBottom, float yTop, float halfWidth, Color body)
            {
                var hh = (yTop - yBottom) * 0.5f;
                var yc = (yTop + yBottom) * 0.5f;
                var c = Mathf.Min(halfWidth, hh) * 0.35f;
                var slope = Mathf.Min((z1 - z0) * 0.4f, hh * 1.4f);
                var rings = new List<Vector3[]>
                {
                    ShipMeshData.Octagon(halfWidth * 0.94f, hh * 0.94f, c, c, z0, yc),
                    ShipMeshData.Octagon(halfWidth, hh, c, c, z0 + c, yc),
                    ShipMeshData.Octagon(halfWidth, hh, c, c, z1 - slope, yc),
                    ShipMeshData.Octagon(halfWidth * 0.92f, hh * 0.42f, c * 0.5f, c * 0.5f, z1, yc - hh * 0.5f),
                };
                _m.AddLoft(rings, new[] { body, body, Glowing(_p.Window, WindowGlow) });
                // Roof plate in accent.
                _m.AddChamferBox(new Vector3(0f, yTop + hh * 0.06f, (z0 + z1 - slope) * 0.5f), Quaternion.identity,
                    new Vector3(halfWidth * 1.3f, hh * 0.18f, (z1 - slope - z0) * 0.8f), hh * 0.06f, _p.Accent);
                _center.Raise(z0, z1, yTop + hh * 0.12f, block: true);

                // Antenna mast with a red tip light.
                var mastHeight = hh * Range(1.2f, 2.2f);
                var mastZ = z0 + (z1 - z0) * 0.25f;
                var mastX = halfWidth * 0.5f;
                var profile = new List<Vector2>
                {
                    new(0f, 0f), new(_l * 0.004f, 0f), new(_l * 0.004f, mastHeight), new(0f, mastHeight),
                };
                _m.AddLathe(new Vector3(mastX, yTop, mastZ), Quaternion.Euler(-90f, 0f, 0f), profile, new[] { _p.Metal }, 6);
                _m.AddChamferBox(new Vector3(mastX, yTop + mastHeight, mastZ), Quaternion.identity,
                    Vector3.one * _l * 0.014f, _l * 0.003f, Glowing(new Color(1f, 0.25f, 0.2f), FullGlow));
            }

            void BuildBridge()
            {
                switch (_d.Bridge)
                {
                    case BridgeLayout.Nose:
                    {
                        var t0 = _d.Hull == HullForm.Spine ? 0.8f : (_d.Hull == HullForm.Wedge ? 0.6f : 0.72f);
                        var t1 = _d.Hull == HullForm.Wedge ? 0.84f : 0.95f;
                        var body = _main;
                        var low = Mathf.Min(TopOf(body, t0), TopOf(body, t1));
                        var high = Mathf.Max(TopOf(body, t0), TopOf(body, t1));
                        AddBridgeBlock(Z(t0), Z(t1), low - _h * 0.4f, high + _h * Range(0.35f, 0.5f), _w * Range(0.42f, 0.55f), _p.Secondary);
                        break;
                    }
                    case BridgeLayout.MidTower:
                    case BridgeLayout.RearTower:
                    {
                        var rear = _d.Bridge == BridgeLayout.RearTower;
                        var t0 = rear ? 0.12f : 0.48f;
                        var t1 = rear ? 0.26f : 0.6f;
                        var baseY = Mathf.Max(SampleLane(_center, Z(t0)), SampleLane(_center, Z(t1)));
                        var stemTop = baseY + _h * (rear ? 1.1f : 0.7f);
                        // Stem.
                        _m.AddChamferBox(new Vector3(0f, (baseY + stemTop) * 0.5f - _h * 0.1f, (Z(t0) + Z(t1)) * 0.5f - _l * 0.01f),
                            Quaternion.identity, new Vector3(_w * 0.6f, stemTop - baseY + _h * 0.2f, (Z(t1) - Z(t0)) * 0.6f), _h * 0.1f, _p.Metal);
                        // Wide cab overhanging the stem.
                        AddBridgeBlock(Z(t0), Z(t1) + _l * 0.02f, stemTop - _h * 0.1f, stemTop + _h * 0.55f, _w * 0.62f, _p.Secondary);
                        _center.Raise(Z(t0), Z(t1), stemTop, block: true);
                        break;
                    }
                }
            }

            float SampleLane(Lane lane, float z)
            {
                var i = Mathf.Clamp(Mathf.RoundToInt((z - lane.ZMin) / Lane.Step), 0, lane.Top.Length - 1);
                return lane.Top[i];
            }

            // ------------------------------------------------------------------ Engines

            /// <summary>Nacelle along -Z whose nozzle exit is at zExit. Registers its glow.</summary>
            void AddNacelle(Vector3 exitCenter, float r, float length)
            {
                const float zb = 0f; // nozzle exit at the origin of the lathe
                var profile = new List<Vector2>
                {
                    new(0f, zb + 0.3f * r), new(0.72f * r, zb + 0.3f * r),     // glowing throat
                    new(0.84f * r, zb), new(r, zb),                            // inner wall, lip
                    new(0.86f * r, zb + 0.55f * r),                            // outer bell
                    new(0.93f * r, zb + 0.55f * r), new(0.93f * r, zb + 0.85f * r), // accent ring
                    new(0.84f * r, zb + 0.85f * r),
                    new(0.84f * r, zb + length - 0.25f * r),                   // body
                    new(0.6f * r, zb + length), new(0f, zb + length),          // front
                };
                var colors = new[]
                {
                    Glowing(_p.Glow, FullGlow), Shade(_p.Metal, 0.7f), _p.Metal, _p.Metal,
                    _p.Accent, _p.Accent, _p.Accent, _p.Secondary, _p.Secondary, _p.Secondary,
                };
                _m.AddLathe(exitCenter, Quaternion.identity, profile, colors, 20);
            }

            void Engine(float x, float y, float r, float length, bool mirrored)
            {
                var exit = new Vector3(x, y, _zTail - _overhang);
                AddNacelle(exit, r, length);
                _ship.Engines.Add(new EngineMount(exit + new Vector3(0f, 0f, 0.3f * r), 0.72f * r));
                if (mirrored)
                    _ship.Engines.Add(new EngineMount(new Vector3(-exit.x, exit.y, exit.z + 0.3f * r), 0.72f * r));
            }

            void BuildEngines()
            {
                var rear = _main.Sample(0.02f);
                var length = _overhang + _l * 0.16f;
                switch (_d.Hull == HullForm.Twin ? EngineLayout.Pair : _d.Engines)
                {
                    case EngineLayout.Pair when _d.Hull == HullForm.Twin:
                    {
                        var side = _side.Sample(0.02f);
                        var r = Mathf.Min(side.W, side.H) * 0.95f;
                        var mark = _m.Mark();
                        Engine(_side.X, side.Y, r, length, true);
                        _m.MirrorSince(mark);
                        break;
                    }
                    case EngineLayout.Pair:
                    {
                        var r = Mathf.Min(rear.H * 0.62f, rear.W * 0.46f);
                        var mark = _m.Mark();
                        Engine(rear.W * 0.5f, rear.Y, r, length, true);
                        _m.MirrorSince(mark);
                        break;
                    }
                    case EngineLayout.Trio:
                    {
                        var r = rear.H * 0.66f;
                        Engine(0f, rear.Y + rear.H * 0.12f, r, length * 1.1f, false);
                        var mark = _m.Mark();
                        Engine(rear.W * 0.72f, rear.Y - rear.H * 0.35f, r * 0.62f, length * 0.9f, true);
                        _m.MirrorSince(mark);
                        break;
                    }
                    case EngineLayout.Quad:
                    {
                        var r = Mathf.Min(rear.H, rear.W) * 0.42f;
                        var mark = _m.Mark();
                        Engine(rear.W * 0.48f, rear.Y + rear.H * 0.46f, r, length, true);
                        Engine(rear.W * 0.48f, rear.Y - rear.H * 0.46f, r, length, true);
                        _m.MirrorSince(mark);
                        break;
                    }
                    case EngineLayout.Pylons:
                    {
                        var r = _h * 0.62f;
                        var mid = _main.Sample(0.1f);
                        var x = mid.W + r * 1.5f;
                        var mark = _m.Mark();
                        Engine(x, mid.Y, r, _overhang + _l * 0.26f, true);
                        // Swept pylon from the flank to the nacelle.
                        _m.AddChamferBox(new Vector3((mid.W * 0.7f + x) * 0.5f, mid.Y, Z(0.08f)), Quaternion.identity,
                            new Vector3(x - mid.W * 0.7f, r * 0.5f, _l * 0.12f), r * 0.12f, _p.Primary);
                        _m.AddChamferBox(new Vector3(x, mid.Y + r * 0.9f, Z(0.02f)), Quaternion.identity,
                            new Vector3(r * 0.3f, r * 0.7f, _l * 0.12f), r * 0.1f, _p.Accent);
                        _m.MirrorSince(mark);
                        // A small central booster between them.
                        Engine(0f, rear.Y, rear.H * 0.45f, length * 0.8f, false);
                        break;
                    }
                    case EngineLayout.Block:
                    {
                        var height = rear.H * 1.5f;
                        var width = rear.W * 2.3f;
                        var depth = _l * 0.12f;
                        _m.AddChamferBox(new Vector3(0f, rear.Y, _zTail + depth * 0.3f), Quaternion.identity,
                            new Vector3(width, height, depth), height * 0.2f, _p.Metal, _p.Metal);
                        _m.AddChamferBox(new Vector3(0f, rear.Y + height * 0.42f, _zTail + depth * 0.3f), Quaternion.identity,
                            new Vector3(width * 1.02f, height * 0.18f, depth * 1.02f), height * 0.08f, _p.Accent);
                        var r = Mathf.Min(height * 0.36f, width / 7f);
                        Engine(0f, rear.Y - height * 0.08f, r, length, false);
                        var mark = _m.Mark();
                        Engine(width * 0.32f, rear.Y - height * 0.08f, r, length, true);
                        _m.MirrorSince(mark);
                        break;
                    }
                }
            }

            // ------------------------------------------------------------------ Wings

            /// <summary>
            /// Tapered slab whose span runs along the local X axis of <paramref name="frame"/>, chord
            /// along local Z, thickness along local Y. Root at the origin of the frame.
            /// </summary>
            void AddFin(Vector3 root, Quaternion frame, float span, float rootChord, float tipChord, float sweep, float thickness, Color body, Color tip)
            {
                Vector3[] Ring(float x, float chord, float zShift, float th)
                {
                    var hz = chord * 0.5f;
                    var ht = th * 0.5f;
                    return new[]
                    {
                        new Vector3(x, 0f, hz + zShift),
                        new Vector3(x, ht, hz * 0.6f + zShift),
                        new Vector3(x, ht, -hz * 0.8f + zShift),
                        new Vector3(x, 0f, -hz + zShift),
                        new Vector3(x, -ht, -hz * 0.8f + zShift),
                        new Vector3(x, -ht, hz * 0.6f + zShift),
                    };
                }
                var midChord = Mathf.Lerp(rootChord, tipChord, 0.86f);
                var rings = new List<Vector3[]>
                {
                    Ring(0f, rootChord, 0f, thickness),
                    Ring(span * 0.86f, midChord, -sweep * 0.86f, thickness * 0.8f),
                    Ring(span, tipChord, -sweep, thickness * 0.75f),
                };
                ShipMeshData.Transform(rings, root, frame);
                _m.AddLoft(rings, new[] { body, tip });
            }

            void BuildWings()
            {
                switch (_d.Wings)
                {
                    case WingLayout.Stubs:
                    {
                        var t = Range(0.24f, 0.36f);
                        var s = _main.Sample(t);
                        var mark = _m.Mark();
                        AddFin(new Vector3(s.W * 0.8f, s.Y - s.H * 0.3f, Z(t)), Quaternion.Euler(0f, 0f, Range(8f, 16f)),
                            _w * Range(0.9f, 1.2f), _l * 0.2f, _l * 0.09f, _l * 0.08f, _h * 0.28f, _p.Primary, _p.Accent);
                        _m.MirrorSince(mark);
                        break;
                    }
                    case WingLayout.Radiators:
                    {
                        var t = Range(0.36f, 0.44f);
                        var s = _main.Sample(t);
                        var mark = _m.Mark();
                        AddFin(new Vector3(s.W * 0.85f, s.Y + s.H * 0.2f, Z(t)), Quaternion.identity,
                            _w * Range(1.2f, 1.5f), _l * 0.17f, _l * 0.15f, _l * 0.01f, _h * 0.1f, _p.Panel, _p.Accent);
                        // Root block.
                        _m.AddChamferBox(new Vector3(s.W * 0.95f, s.Y + s.H * 0.2f, Z(t)), Quaternion.identity,
                            new Vector3(_w * 0.25f, _h * 0.35f, _l * 0.19f), _h * 0.08f, _p.Metal);
                        _m.MirrorSince(mark);
                        break;
                    }
                    case WingLayout.Fins:
                    {
                        var t = 0.1f;
                        var s = _main.Sample(t);
                        var top = Mathf.Max(s.Y + s.H, SampleLane(_center, Z(t)));
                        AddFin(new Vector3(0f, top - _h * 0.1f, Z(t)), Quaternion.Euler(0f, 0f, 90f),
                            _h * Range(1.1f, 1.4f), _l * 0.2f, _l * 0.08f, _l * 0.09f, _w * 0.12f, _p.Primary, _p.Accent);
                        _center.Raise(Z(t) - _l * 0.12f, Z(t) + _l * 0.1f, top + _h, block: true);
                        var mark = _m.Mark();
                        AddFin(new Vector3(s.W * 0.5f, s.Y - s.H * 0.8f, Z(t)), Quaternion.Euler(0f, 0f, -40f),
                            _h * 0.9f, _l * 0.16f, _l * 0.07f, _l * 0.06f, _w * 0.1f, _p.Primary, _p.Accent);
                        _m.MirrorSince(mark);
                        break;
                    }
                }
            }

            // ------------------------------------------------------------------ Lights

            void BuildLights()
            {
                // Headlights on the nose, nav lights on the flanks (red to port, green to starboard).
                var nose = _main.Sample(0.995f);
                var lamp = Glowing(new Color(1f, 0.95f, 0.78f), FullGlow);
                var size = Vector3.one * Mathf.Max(0.3f, _l * 0.014f);
                var mark = _m.Mark();
                _m.AddChamferBox(new Vector3(nose.W * 0.45f, nose.Y - nose.H * 0.25f, _zNose), Quaternion.identity, size, size.x * 0.2f, lamp);
                _m.MirrorSince(mark);

                var mid = _main.Sample(0.5f);
                var outer = _d.Hull == HullForm.Twin ? _side.X + _side.Sample(0.5f).W : mid.W;
                _m.AddChamferBox(new Vector3(outer + size.x * 0.3f, mid.Y + mid.H * 0.4f, Z(0.5f)), Quaternion.identity,
                    size * 0.8f, size.x * 0.15f, Glowing(new Color(0.3f, 1f, 0.45f), FullGlow));
                _m.AddChamferBox(new Vector3(-outer - size.x * 0.3f, mid.Y + mid.H * 0.4f, Z(0.5f)), Quaternion.identity,
                    size * 0.8f, size.x * 0.15f, Glowing(new Color(1f, 0.25f, 0.2f), FullGlow));
            }

            // ------------------------------------------------------------------ Hardpoints

            /// <summary>Turret size for this hull: about 8.5 % of the length (4 m on a 46 m ship).</summary>
            float TurretSize => _l * 0.085f;

            void PlaceHardpoints()
            {
                var slots = ShipLoadout.WeaponSlots;
                var size = TurretSize;
                if (_pair != null)
                {
                    // Twin hulls: a pair on the hulls, the rest on the central pod.
                    var pairZ = BestSpots(_pair, 1, size, -0.1f)[0];
                    var y = FootprintTop(_pair, pairZ, size);
                    _ship.Hardpoints.Add(new Hardpoint(new Vector3(_pair.X, y, pairZ), size));
                    _ship.Hardpoints.Add(new Hardpoint(new Vector3(-_pair.X, y, pairZ), size));
                    slots -= 2;
                }
                foreach (var z in BestSpots(_center, slots, size, 0f))
                    _ship.Hardpoints.Add(new Hardpoint(new Vector3(0f, FootprintTop(_center, z, size), z), size));
                _ship.Hardpoints.Sort((a, b) => b.Position.z.CompareTo(a.Position.z));

                // Hand adjustments of the design: slide a hardpoint along the ship, back onto the deck.
                var shifts = _d.HardpointShifts;
                if (shifts == null)
                    return;
                for (var i = 0; i < _ship.Hardpoints.Count && i < shifts.Length; i++)
                {
                    if (shifts[i] == 0f)
                        continue;
                    var p = _ship.Hardpoints[i].Position;
                    var lane = p.x == 0f || _pair == null ? _center : _pair;
                    p.z = Mathf.Clamp(p.z + shifts[i] * _l, _zTail + size, _zNose - size);
                    p.y = FootprintTop(lane, p.z, size);
                    _ship.Hardpoints[i] = new Hardpoint(p, size);
                }
            }

            float FootprintTop(Lane lane, float z, float size)
            {
                var top = float.NegativeInfinity;
                for (var dz = -size * 0.5f; dz <= size * 0.5f; dz += Lane.Step)
                    top = Mathf.Max(top, SampleLane(lane, z + dz));
                return top;
            }

            /// <summary>
            /// Spreads <paramref name="count"/> turrets along a lane: only on free, nearly flat stretches
            /// of the top, as far from each other as possible, slightly favouring the front.
            /// </summary>
            List<float> BestSpots(Lane lane, int count, float size, float bias)
            {
                var candidates = new List<(float z, float top)>();
                var reach = Mathf.CeilToInt(size * 0.6f / Lane.Step);
                for (var i = reach; i < lane.Top.Length - reach; i++)
                {
                    var ok = true;
                    var min = float.PositiveInfinity;
                    var max = float.NegativeInfinity;
                    for (var k = i - reach; k <= i + reach && ok; k++)
                    {
                        if (lane.Blocked[k] || float.IsNegativeInfinity(lane.Top[k])) ok = false;
                        min = Mathf.Min(min, lane.Top[k]);
                        max = Mathf.Max(max, lane.Top[k]);
                    }
                    var z = lane.ZAt(i);
                    if (ok && max - min < size * 0.35f && z > _zTail + size && z < _zNose - size)
                        candidates.Add((z, max));
                }

                var picked = new List<float>();
                if (candidates.Count == 0)
                {
                    // Nowhere flat: fall back on the middle of the hull.
                    for (var i = 0; i < count; i++)
                        picked.Add(Mathf.Lerp(Z(0.3f), Z(0.7f), count == 1 ? 0.5f : i / (float)(count - 1)));
                    return picked;
                }

                for (var n = 0; n < count; n++)
                {
                    var best = candidates[0].z;
                    var bestScore = float.NegativeInfinity;
                    foreach (var (z, top) in candidates)
                    {
                        var spacing = float.PositiveInfinity;
                        foreach (var p in picked)
                            spacing = Mathf.Min(spacing, Mathf.Abs(p - z));
                        if (picked.Count == 0)
                            spacing = _l;
                        // Higher spots see more of the sky; the front is preferred a little.
                        var score = Mathf.Min(spacing, size * 4f) * 10f + top * 2f + (z - _zTail) * (0.05f + bias);
                        if (spacing < size * 1.15f)
                            score -= 1000f;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = z;
                        }
                    }
                    picked.Add(best);
                }
                return picked;
            }
        }
    }
}
