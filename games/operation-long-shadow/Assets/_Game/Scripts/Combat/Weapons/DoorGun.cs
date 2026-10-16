using System.Collections.Generic;
using LongShadow.Combat.Data;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Weapons
{
    /// <summary>
    /// The Beat 11 left-door gun mount (TECH_PLAN §7.5 DoorGun, GDD §13.3, §7.11). <see cref="Mount"/> seats the player
    /// behind the gun at the <c>HELI.DoorGunPivot</c> marker (its forward is the left-door normal), limits the look to yaw
    /// ±70° and pitch +15°/−60°, scales incoming damage ×0.25 with a 1 HP floor, shows the door-gun overlay and hides the
    /// helicopter's own world M60D (renderers under the pivot) so only the view-model gun draws. Firing (550 RPM, 60 per hit,
    /// 0.6° spread, red tracer every 3rd round, heat +1 per shot, overheat at 55) runs through PlayerWeapon with
    /// <see cref="WeaponTable.DoorGun"/> and <see cref="WeaponTable.DoorGunMount"/>.
    /// <para>Assumption about WP02: <c>IPlayerRig.Mount(seat)</c> parents the rig root to the seat with the standing eye
    /// height (1.65 m) above it; the seat is placed so the eye lands at <see cref="DoorGunSpec.EyeOffset"/> from the pivot.</para>
    /// </summary>
    public sealed class DoorGun
    {
        /// <summary>Standing eye height the seat is offset by (GDD §12).</summary>
        public const float EyeHeight = 1.65f;

        private readonly DoorGunSpec _spec;
        private readonly List<Renderer> _hidden = new List<Renderer>(16);
        private Transform _seat;

        /// <summary>Creates the mount controller.</summary>
        public DoorGun(DoorGunSpec spec) => _spec = spec ?? WeaponTable.DoorGunMount;

        /// <summary>True while mounted.</summary>
        public bool IsMounted { get; private set; }
        /// <summary>The pivot mounted on.</summary>
        public Transform Pivot { get; private set; }
        /// <summary>The mount numbers.</summary>
        public DoorGunSpec Spec => _spec;

        /// <summary>Mounts the player (idempotent for the same pivot). Returns false without a pivot or rig.</summary>
        public bool Mount(Transform pivot, IPlayerRig rig, IHud hud)
        {
            if (pivot == null || rig == null) return false;
            if (IsMounted && pivot == Pivot) return true;
            if (IsMounted) Dismount(rig, hud);
            Pivot = pivot;
            _seat = new GameObject("DoorGunSeat").transform;
            _seat.SetParent(pivot, false);
            _seat.localPosition = _spec.EyeOffset - Vector3.up * EyeHeight;
            _seat.localRotation = Quaternion.identity;
            HideWorldGun(pivot);
            rig.Mount(_seat);
            rig.SetLookLimits(pivot.rotation, _spec.YawLimit, _spec.PitchUp, _spec.PitchDown); // GDD §13.3
            rig.SetDamageModifiers(_spec.IncomingDamageScale, _spec.HealthFloor);                  // GDD §7.11
            hud?.SetOverlay(HudOverlay.DoorGun);
            IsMounted = true;
            return true;
        }

        /// <summary>Leaves the gun and undoes everything <see cref="Mount"/> set.</summary>
        public void Dismount(IPlayerRig rig, IHud hud)
        {
            if (!IsMounted) return;
            IsMounted = false;
            if (rig != null)
            {
                rig.ClearLookLimits();
                rig.SetDamageModifiers(1f, 0f);
                rig.Dismount();
            }
            hud?.SetOverlay(HudOverlay.None);
            for (var i = 0; i < _hidden.Count; i++)
                if (_hidden[i] != null) _hidden[i].enabled = true;
            _hidden.Clear();
            if (_seat != null) Object.Destroy(_seat.gameObject);
            _seat = null;
            Pivot = null;
        }

        private void HideWorldGun(Transform pivot)
        {
            _hidden.Clear();
            foreach (var r in pivot.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled || r.gameObject.layer == LayerIds.Viewmodel) continue;
                r.enabled = false;
                _hidden.Add(r);
            }
        }
    }
}
