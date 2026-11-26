using System.Collections.Generic;
using Space.Core.Combat;
using UnityEngine;

namespace Space.Presentation.Ships
{
    /// <summary>
    /// Firing range of the hangar: an invincible practice target alongside the ship, as the enemy
    /// sits in the tactical combat, and the ship's turrets shooting at it with the real numbers of
    /// their weapon (reload time, projectile speed, spread). Purely visual: nothing is simulated.
    /// </summary>
    public class FiringRange : MonoBehaviour
    {
        [Tooltip("Distance from our ship to the target, on the side.")]
        public float TargetDistance = 230f;

        [Tooltip("The target sits a little ahead, like an enemy on a parallel course.")]
        public float TargetAhead = 30f;

        public bool Firing = true;

        [Tooltip("Width of tracers per metre of distance to the camera, so they stay visible from afar.")]
        public float WidthPerMetre = 0.004f;

        public ShipModel Ship;
        public Transform Target { get; private set; }
        public float TargetRadius { get; private set; }

        /// <summary>Shots fired since the start, per kind (read by the tests).</summary>
        public int ShotsFired { get; private set; }

        sealed class Shot
        {
            public LineRenderer Line;
            public Transform Head;
            public Vector3 From, To;
            public float Travelled, Length, Speed, TrailLength, Width, FlashSize;
            public Color Color;
            public bool Active;
        }

        sealed class Flash
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float Age, Duration, Size;
            public Color Color;
            public bool Active;
        }

        readonly List<Shot> _shots = new();
        readonly List<Flash> _flashes = new();
        MaterialPropertyBlock _block;
        Mesh _targetMesh;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            BuildTarget();
        }

        void OnDestroy()
        {
            if (_targetMesh != null)
                Destroy(_targetMesh);
        }

        /// <summary>A striped practice barge with bullseyes on the flank that faces our ship.</summary>
        void BuildTarget()
        {
            var red = ShipPalette.Hex(0xE8453C);
            var white = ShipPalette.Hex(0xF2EEE6);
            var dark = ShipPalette.Hex(0x30343F);
            var m = new ShipMeshData();
            const float length = 40f, w = 7f, h = 6f;
            var rings = new List<Vector3[]>();
            var bands = new List<Color>();
            for (var i = 0; i <= 8; i++)
            {
                var t = i / 8f;
                var taper = i == 0 || i == 8 ? 0.75f : 1f;
                rings.Add(ShipMeshData.Octagon(w * taper, h * taper, h * 0.35f, h * 0.35f, (t - 0.5f) * length));
                bands.Add(i % 2 == 0 ? red : white);
            }
            m.AddLoft(rings, bands);
            // Bullseyes facing -X (toward the ship), and a mast with a beacon.
            var facing = Quaternion.LookRotation(Vector3.left);
            foreach (var z in new[] { -10f, 10f })
            {
                var profile = new List<Vector2>
                {
                    new(0f, 0f), new(5f, 0f), new(5f, 0.5f), new(3.6f, 0.5f), new(3.6f, 0.7f), new(2.2f, 0.7f), new(2.2f, 0.9f), new(0f, 0.9f),
                };
                m.AddLathe(new Vector3(-w + 0.2f, 0f, z), facing, profile, new[] { dark, dark, red, red, white, white, red }, 24);
            }
            m.AddLathe(new Vector3(0f, h - 0.5f, 0f), Quaternion.Euler(-90f, 0f, 0f),
                new List<Vector2> { new(0f, 0f), new(0.3f, 0f), new(0.3f, 8f), new(0f, 8f) }, new[] { dark }, 6);
            m.AddChamferBox(new Vector3(0f, h + 7.6f, 0f), Quaternion.identity, Vector3.one * 1.2f, 0.25f, new Color(1f, 0.85f, 0.3f, 1f));
            m.ComputeOutlineNormals();

            var target = new GameObject("Cible d'entraînement");
            target.transform.SetParent(transform, false);
            target.transform.localPosition = new Vector3(TargetDistance, 0f, TargetAhead);
            _targetMesh = ShipMeshes.ToMesh(m, "PracticeTarget");
            target.AddComponent<MeshFilter>().sharedMesh = _targetMesh;
            target.AddComponent<MeshRenderer>().sharedMaterial = ShipMeshes.Toon;
            Target = target.transform;
            TargetRadius = length * 0.5f;
        }

        void Update()
        {
            var dt = Time.deltaTime;
            if (Ship != null && Target != null)
                foreach (var turret in Ship.Turrets)
                {
                    if (turret == null)
                        continue;
                    var aimed = turret.Aim(Target.position, dt);
                    turret.Cooldown -= dt;
                    if (Firing && aimed && turret.Cooldown <= 0f)
                    {
                        Fire(turret);
                        turret.Cooldown = Mathf.Max(0.3f, turret.Weapon.Weapon.Cooldown);
                    }
                }

            UpdateShots(dt);
            UpdateFlashes(dt);
        }

        static Color ShotColor(DamageType type) => type switch
        {
            DamageType.Laser => new Color(1f, 0.35f, 0.55f),
            DamageType.Ion => new Color(0.45f, 0.6f, 1f),
            DamageType.Missile => new Color(1f, 0.6f, 0.15f),
            _ => new Color(1f, 0.85f, 0.45f),
        };

        static Color ImpactColor(DamageType type) => type switch
        {
            DamageType.Ion => new Color(0.45f, 0.7f, 1f),
            DamageType.Laser => new Color(1f, 0.45f, 0.6f),
            _ => new Color(1f, 0.55f, 0.15f),
        };

        void Fire(ShipTurret turret)
        {
            var stats = turret.Weapon.Weapon;
            var from = turret.Fire();
            // Aim at the flank facing us, scattered by the weapon's spread.
            var toTarget = Target.position - from;
            var distance = toTarget.magnitude;
            var spread = Mathf.Tan(stats.SpreadDegrees * Mathf.Deg2Rad) * distance;
            var surface = Target.position - toTarget.normalized * 6f;
            var to = surface + new Vector3(0f, Random.Range(-2.5f, 2.5f), Random.Range(-TargetRadius * 0.6f, TargetRadius * 0.6f))
                     + Random.insideUnitSphere * spread;

            var shot = GetShot();
            shot.From = from;
            shot.To = to;
            shot.Length = Vector3.Distance(from, to);
            shot.Travelled = 0f;
            shot.Speed = Mathf.Max(50f, stats.ProjectileSpeed);
            shot.Color = ShotColor(stats.Type);
            shot.TrailLength = stats.Type switch
            {
                DamageType.Laser => 45f,
                DamageType.Ion => 35f,
                DamageType.Missile => 14f,
                _ => 10f,
            };
            shot.Width = stats.Type == DamageType.Kinetic ? 0.35f : 0.5f;
            shot.FlashSize = 4f + (stats.Damage + stats.ShieldDamage) * 0.05f;
            shot.Head.gameObject.SetActive(stats.Type == DamageType.Missile);
            shot.Active = true;
            shot.Line.enabled = true;
            ShotsFired++;

            SpawnFlash(from, turret.Size * 1.2f, 0.15f, ShotColor(stats.Type));
        }

        Shot GetShot()
        {
            foreach (var s in _shots)
                if (!s.Active)
                    return s;
            var go = new GameObject("Shot");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = ShipMeshes.Line;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            var head = CreateHalo("Missile");
            head.transform.SetParent(go.transform, false);
            var shot = new Shot { Line = line, Head = head.transform };
            _shots.Add(shot);
            return shot;
        }

        GameObject CreateHalo(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = ShipMeshes.Quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ShipMeshes.Halo;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        void UpdateShots(float dt)
        {
            var camera = Camera.main;
            foreach (var s in _shots)
            {
                if (!s.Active)
                    continue;
                s.Travelled += s.Speed * dt;
                if (s.Travelled >= s.Length)
                {
                    s.Active = false;
                    s.Line.enabled = false;
                    s.Head.gameObject.SetActive(false);
                    SpawnFlash(s.To, s.FlashSize, 0.7f, s.Color);
                    continue;
                }

                var direction = (s.To - s.From) / s.Length;
                var head = s.From + direction * s.Travelled;
                var tail = s.From + direction * Mathf.Max(0f, s.Travelled - s.TrailLength);
                s.Line.SetPosition(0, tail);
                s.Line.SetPosition(1, head);
                var widthScale = camera != null ? Mathf.Max(1f, Vector3.Distance(camera.transform.position, head) * WidthPerMetre / s.Width) : 1f;
                s.Line.widthMultiplier = s.Width * widthScale;
                s.Line.startColor = new Color(s.Color.r, s.Color.g, s.Color.b, 0f);
                s.Line.endColor = s.Color;
                if (s.Head.gameObject.activeSelf)
                {
                    s.Head.position = head;
                    s.Head.localScale = Vector3.one * 2.5f * widthScale;
                    SetHaloColor(s.Head.GetComponent<MeshRenderer>(), s.Color, 1f);
                }
            }
        }

        void SpawnFlash(Vector3 position, float size, float duration, Color color)
        {
            Flash flash = null;
            foreach (var f in _flashes)
                if (!f.Active) { flash = f; break; }
            if (flash == null)
            {
                var go = CreateHalo("Flash");
                flash = new Flash { Transform = go.transform, Renderer = go.GetComponent<MeshRenderer>() };
                _flashes.Add(flash);
            }
            flash.Transform.position = position;
            flash.Age = 0f;
            flash.Duration = duration;
            flash.Size = size;
            flash.Color = color;
            flash.Active = true;
            flash.Transform.gameObject.SetActive(true);
        }

        void UpdateFlashes(float dt)
        {
            foreach (var f in _flashes)
            {
                if (!f.Active)
                    continue;
                f.Age += dt;
                var t = f.Age / f.Duration;
                if (t >= 1f)
                {
                    f.Active = false;
                    f.Transform.gameObject.SetActive(false);
                    continue;
                }
                // Pops out fast, then fades while it keeps growing a little.
                var scale = f.Size * (0.6f + 0.8f * Mathf.Sqrt(t));
                f.Transform.localScale = Vector3.one * scale;
                SetHaloColor(f.Renderer, f.Color, 1f - t * t);
            }
        }

        void SetHaloColor(MeshRenderer renderer, Color color, float alpha)
        {
            _block.SetColor("_Color", new Color(color.r, color.g, color.b, alpha));
            // Seen from any side: no front fade for shots and impacts.
            _block.SetFloat("_FrontFade", 0f);
            renderer.SetPropertyBlock(_block);
        }
    }
}
