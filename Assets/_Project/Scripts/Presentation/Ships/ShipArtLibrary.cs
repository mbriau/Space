using System;
using System.Collections.Generic;
using UnityEngine;

namespace Space.Presentation.Ships
{
    /// <summary>
    /// Finished art for some hangar designs: a textured hull and the turret heads cut out of the same
    /// model, so the weapons can still be fitted one by one. Built by the editor tool
    /// (Space &gt; Build Ship Art) and loaded from Resources; a design without art keeps its generated model.
    /// </summary>
    [CreateAssetMenu(menuName = "Space/Ship Art Library", fileName = "ShipArtLibrary")]
    public class ShipArtLibrary : ScriptableObject
    {
        public const string ResourceName = "ShipArtLibrary";

        public List<ShipArt> Ships = new();

        static ShipArtLibrary _instance;
        static bool _loaded;

        /// <summary>The library in Resources, or null when there is none.</summary>
        public static ShipArtLibrary Instance
        {
            get
            {
                if (!_loaded)
                {
                    _instance = Resources.Load<ShipArtLibrary>(ResourceName);
                    _loaded = true;
                }
                return _instance;
            }
        }

        /// <summary>The art of a design, or null.</summary>
        public ShipArt Find(string designId)
        {
            foreach (var ship in Ships)
                if (ship != null && ship.DesignId == designId && (ship.Hull != null || ship.Model != null))
                    return ship;
            return null;
        }

        /// <summary>The art of a design in the library of the project, or null.</summary>
        public static ShipArt For(string designId) => Instance != null ? Instance.Find(designId) : null;
    }

    /// <summary>One ship's art, in ship space (+Z forward, +Y up, metres).</summary>
    [Serializable]
    public class ShipArt
    {
        public string DesignId;
        public Mesh Hull;
        public Material Material;

        /// <summary>
        /// A ready-made model (an asset pack's prefab) drawn instead of <see cref="Hull"/>, scaled by
        /// <see cref="ModelScale"/>, nose along +Z. Its weapons are part of the model: no turret heads.
        /// </summary>
        public GameObject Model;
        public float ModelScale = 1f;

        /// <summary>Size in metres of the generated turrets put on a model's slots (it has no turret heads of its own).</summary>
        public float TurretSize = 2f;

        /// <summary>
        /// Shield aura of a model, in ship space: ellipsoids taken from the prefab's colliders, a little
        /// larger than them. Empty = no aura.
        /// </summary>
        public List<AuraShape> Aura = new();

        /// <summary>Turret pivots on the deck, one per weapon slot, front to back.</summary>
        public List<Vector3> Slots = new();

        /// <summary>The turret heads cut out of the model; a fitted weapon gets the head of its kind.</summary>
        public List<TurretArt> Turrets = new();

        /// <summary>
        /// The head for a weapon's turret kind: the same kind, else one of the same family (energy
        /// emitters, cannons, launchers), else the first head.
        /// </summary>
        public TurretArt BestTurret(TurretKind kind)
        {
            TurretArt family = null;
            foreach (var turret in Turrets)
            {
                if (turret == null || turret.Head == null)
                    continue;
                if (turret.Kind == kind)
                    return turret;
                if (family == null && Family(turret.Kind) == Family(kind))
                    family = turret;
            }
            if (family != null)
                return family;
            foreach (var turret in Turrets)
                if (turret != null && turret.Head != null)
                    return turret;
            return null;
        }

        static int Family(TurretKind kind) => kind switch
        {
            TurretKind.Laser or TurretKind.Ion => 0,
            TurretKind.Missile => 2,
            _ => 1,
        };
    }

    /// <summary>One piece of a shield aura: an ellipsoid (centre, size), turned to follow the collider it comes from.</summary>
    [Serializable]
    public struct AuraShape
    {
        public Vector3 Center;
        public Vector3 Size;
        public Vector3 Euler;
    }

    /// <summary>A turret head: the mesh turns around its pivot (origin, +Y), barrels along +Z.</summary>
    [Serializable]
    public class TurretArt
    {
        public TurretKind Kind;
        public Mesh Head;

        /// <summary>Muzzle positions in head space; shots leave from there, in turn.</summary>
        public List<Vector3> Muzzles = new();

        /// <summary>Rough size of the head in metres (recoil distance).</summary>
        public float Size = 3f;
    }
}
