using System.Collections.Generic;
using Space.Core.Equipment;
using UnityEngine;

namespace Space.Presentation.Ships
{
    /// <summary>
    /// A turret of the hangar: its head turns toward the aim point at the weapon's turn rate and
    /// recoils when it fires. The firing range decides when to fire (<see cref="FiringRange"/>).
    /// </summary>
    public class ShipTurret : MonoBehaviour
    {
        [Tooltip("Turn rate of the head, degrees per second.")]
        public float TurnRate = 70f;

        public Transform Head;
        public TurretKind Kind;
        /// <summary>Turret size in metres.</summary>
        public float Size = 3f;
        public ItemDefinition Weapon;
        public readonly List<Vector3> Muzzles = new();

        /// <summary>Seconds until the weapon may fire again.</summary>
        public float Cooldown;

        int _nextMuzzle;
        float _recoil;
        float _yaw;

        /// <summary>Turns the head toward a world point; true when it points at it.</summary>
        public bool Aim(Vector3 target, float deltaTime)
        {
            var local = transform.InverseTransformPoint(target);
            var wanted = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            _yaw = Mathf.MoveTowardsAngle(_yaw, wanted, TurnRate * deltaTime);
            _recoil = Mathf.MoveTowards(_recoil, 0f, deltaTime * 2.5f);
            if (Head != null)
            {
                Head.localRotation = Quaternion.Euler(0f, _yaw, 0f);
                Head.localPosition = new Vector3(0f, Head.localPosition.y, 0f) - Head.localRotation * Vector3.forward * (_recoil * 0.1f * Size);
            }
            return Mathf.Abs(Mathf.DeltaAngle(_yaw, wanted)) < 2f;
        }

        /// <summary>World position of the next muzzle in turn, and kicks the head back.</summary>
        public Vector3 Fire()
        {
            _recoil = 1f;
            if (Muzzles.Count == 0 || Head == null)
                return transform.position;
            var muzzle = Muzzles[_nextMuzzle % Muzzles.Count];
            _nextMuzzle++;
            return Head.TransformPoint(muzzle);
        }

        public Vector3 Forward => Head != null ? Head.forward : transform.forward;

        /// <summary>World position of the muzzle the next shot will leave from.</summary>
        public Vector3 NextMuzzle => Muzzles.Count == 0 || Head == null
            ? transform.position
            : Head.TransformPoint(Muzzles[_nextMuzzle % Muzzles.Count]);
    }
}
