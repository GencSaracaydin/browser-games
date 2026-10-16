using LongShadow.Combat.Data;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Viewmodel
{
    /// <summary>
    /// The procedural first-person weapon motion (TECH_PLAN §7.5 ViewmodelController; CINEMATIC_DIRECTION §4): hip and ADS
    /// poses (the rear and front sights land exactly on the view axis at the weapon's eye relief), sprint/crouch/slide
    /// poses, raise/lower, mouse-lag sway with spring inertia, a figure-eight walk bob synced to the head bob, idle
    /// breathing, strafe roll, jump lift and landing dip, the per-shot kick (GDD §13.1: 2.2 cm back, spring return) and the
    /// reload and hand-action offsets. Pure C# (springs from Core), so EditMode tests can step it.
    /// Output: the weapon pivot's local pose in view-model space (camera at the origin looking +Z).
    /// </summary>
    public sealed class ViewmodelMotion
    {
        private SpringVector3 _swayRot;
        private SpringVector3 _swayPos;
        private SpringVector3 _kickPos;
        private SpringVector3 _kickRot;
        private SpringVector3 _movePos;
        private SpringVector3 _moveRot;
        private float _sprint01;
        private float _crouch01;
        private float _slide01;
        private float _air01;
        private float _heft = -1f;

        /// <summary>Creates the motion with its tuning.</summary>
        public ViewmodelMotion(ViewmodelTuning tuning)
        {
            Tuning = tuning ?? new ViewmodelTuning();
            Reset();
        }

        /// <summary>Feel constants.</summary>
        public ViewmodelTuning Tuning { get; }
        /// <summary>Current sprint pose weight 0..1.</summary>
        public float Sprint01 => _sprint01;
        /// <summary>Kick position offset (metres), for tests and debugging.</summary>
        public Vector3 KickPosition => _kickPos.Value;
        /// <summary>Kick rotation offset (degrees).</summary>
        public Vector3 KickEuler => _kickRot.Value;

        /// <summary>Snaps every spring to rest (equip, teleport).</summary>
        public void Reset()
        {
            var t = Tuning;
            _swayRot = new SpringVector3(t.SwayFrequency, t.SwayDamping);
            _swayPos = new SpringVector3(t.SwayFrequency, t.SwayDamping);
            _kickPos = new SpringVector3(t.KickPositionFrequency, t.KickPositionDamping);
            _kickRot = new SpringVector3(t.KickRotationFrequency, t.KickRotationDamping);
            _movePos = new SpringVector3(t.MoveFrequency, t.MoveDamping);
            _moveRot = new SpringVector3(t.MoveFrequency, t.MoveDamping);
            _sprint01 = _crouch01 = _slide01 = _air01 = 0f;
            _heft = -1f;
        }

        /// <summary>
        /// ADS pose of a weapon: the rotation that brings the front sight to the rear sight's height (sights zeroed, bore
        /// slightly up) and the position that puts the rear sight on the view axis at <paramref name="eyeRelief"/>.
        /// </summary>
        public static void AdsPose(Vector3 sightRear, Vector3 sightFront, float eyeRelief, Vector3 adsOffset, out Vector3 position, out Quaternion rotation)
        {
            var dz = sightFront.z - sightRear.z;
            var up = dz > 1e-4f ? Mathf.Atan2(sightRear.y - sightFront.y, dz) * Mathf.Rad2Deg : 0f;
            var side = dz > 1e-4f ? Mathf.Atan2(sightFront.x - sightRear.x, dz) * Mathf.Rad2Deg : 0f;
            rotation = Quaternion.Euler(-up, -side, 0f);
            position = new Vector3(0f, 0f, eyeRelief) - rotation * sightRear + adsOffset;
        }

        /// <summary>Adds the kick of one shot (GDD §13.1 visual kick).</summary>
        public void AddKick(RecoilSpec r, Vector2 aimKick, float adsBlend, float rollRandom, float yawRandom)
        {
            if (r == null) return;
            var t = Tuning;
            var ads = CombatUtil.Smooth01(adsBlend);
            var posScale = Mathf.Lerp(1f, t.KickAdsPositionScale, ads);
            var rotScale = Mathf.Lerp(1f, t.KickAdsRotationScale, ads);
            var back = r.ModelKickBack * posScale; // GDD §13.1: back 2.2 cm in Z
            _kickPos.Value += new Vector3(0f, back * t.KickRise, -back);
            var pitch = (r.ModelKickPitch + aimKick.x * 0.6f) * rotScale;
            var yaw = (aimKick.y * 0.8f + (yawRandom * 2f - 1f) * 0.35f) * rotScale;
            var roll = (rollRandom * 2f - 1f) * r.ModelKickRoll * rotScale;
            _kickRot.Value += new Vector3(-pitch, yaw, roll);
        }

        /// <summary>Adds a landing dip for a landing at <paramref name="speed"/> m/s downward.</summary>
        public void AddLanding(float speed)
        {
            var t = Tuning;
            var dip = Mathf.Min(t.LandingDip.y, Mathf.Abs(speed) * t.LandingDip.x);
            var pitch = Mathf.Min(t.LandingPitch.y, Mathf.Abs(speed) * t.LandingPitch.x);
            _movePos.Value += new Vector3(0f, -dip, 0f);
            _moveRot.Value += new Vector3(pitch, 0f, 0f);
        }

        /// <summary>
        /// Advances one frame and returns the weapon pivot pose for <paramref name="vm"/> (pose values from the prefab's
        /// ViewmodelRig) and the weapon's sights.
        /// </summary>
        public void Step(in ViewmodelInput input, in PoseSet vm, float heft, out Vector3 position, out Quaternion rotation)
        {
            var t = Tuning;
            var dt = Mathf.Max(0f, input.Dt);
            if (!Mathf.Approximately(heft, _heft))
            {
                _heft = heft;
                _swayRot.Frequency = _swayPos.Frequency = t.SwayFrequencyFor(heft);
                _kickPos.Frequency = t.KickFrequencyFor(heft);
            }
            var ads = CombatUtil.Smooth01(input.AdsBlend);
            var hip = 1f - ads;

            // Pose weights (eased).
            _sprint01 = Mathf.MoveTowards(_sprint01, input.Sprinting ? 1f : 0f, dt / Mathf.Max(0.01f, input.Sprinting ? t.SprintInTime : vm.SprintOutTime));
            _crouch01 = Mathf.MoveTowards(_crouch01, input.Crouched && !input.Sliding ? 1f : 0f, dt / t.StancePoseTime);
            _slide01 = Mathf.MoveTowards(_slide01, input.Sliding ? 1f : 0f, dt / t.StancePoseTime);
            _air01 = Mathf.MoveTowards(_air01, input.Airborne ? 1f : 0f, dt / 0.2f);
            var sprint = CombatUtil.Smooth01(_sprint01) * hip;
            var lowered = Mathf.Clamp01(input.Lowered01);

            // Base pose.
            AdsPose(vm.SightRear, vm.SightFront, vm.EyeRelief, vm.AdsOffset, out var adsPos, out var adsRot);
            var pos = Vector3.Lerp(vm.HipPosition, adsPos, ads);
            var rot = Quaternion.Slerp(Quaternion.Euler(vm.HipEuler), adsRot, ads);

            // Sway: the gun lags the look (CINEMATIC_DIRECTION §4: about 1.5°), springs back with a little overshoot.
            var lookVel = dt > 1e-5f ? input.LookDelta / dt : Vector2.zero;
            var swayScale = vm.SwayScale * Mathf.Lerp(1f, t.SwayAdsScale, ads);
            var swayTarget = new Vector3(
                Mathf.Clamp(lookVel.x * t.SwayPerDegPerSec, -t.SwayMaxDegrees, t.SwayMaxDegrees),
                Mathf.Clamp(-lookVel.y * t.SwayPerDegPerSec, -t.SwayMaxDegrees, t.SwayMaxDegrees),
                Mathf.Clamp(-lookVel.y * t.SwayPerDegPerSec * 0.6f, -t.SwayMaxDegrees, t.SwayMaxDegrees)) * swayScale; // banks into the turn
            _swayRot.Target = swayTarget;
            _swayRot.Step(dt);
            _swayPos.Target = new Vector3(
                Mathf.Clamp(-lookVel.y * t.SwayPositionPerDegPerSec, -t.SwayMaxPosition, t.SwayMaxPosition),
                Mathf.Clamp(-lookVel.x * t.SwayPositionPerDegPerSec, -t.SwayMaxPosition, t.SwayMaxPosition), 0f) * swayScale;
            _swayPos.Step(dt);

            // Bob (head bob: vertical -cos 2φ, lateral sin φ; GDD §12 1.2 / 0.8 cm at walk).
            var amount = t.WalkBobAmount > 0f ? input.BobAmount01 / t.WalkBobAmount : 0f;
            amount *= Mathf.Lerp(1f, t.SprintBobScale, sprint) * vm.BobScale * Mathf.Lerp(1f, t.BobAdsScale, ads) * (1f - _air01);
            var phase = input.BobPhase;
            var bobPos = new Vector3(Mathf.Sin(phase) * t.BobAmplitude.x, -Mathf.Cos(2f * phase) * t.BobAmplitude.y, 0f) * amount;
            var bobRot = new Vector3(Mathf.Cos(2f * phase) * t.BobRotation.y * 0.5f, Mathf.Sin(phase) * t.BobRotation.y, -Mathf.Sin(phase) * t.BobRotation.x) * amount;

            // Idle breathing.
            var breathScale = Mathf.Lerp(1f, t.BreathAdsScale, ads) * (1f - _sprint01);
            var breath = new Vector3(Mathf.Sin(input.Time * t.BreathFrequency.x * 2f * Mathf.PI) * t.BreathAmplitude.x,
                Mathf.Sin(input.Time * t.BreathFrequency.y * 2f * Mathf.PI) * t.BreathAmplitude.y, 0f) * breathScale;
            var breathRot = new Vector3(Mathf.Sin(input.Time * t.BreathFrequency.y * 2f * Mathf.PI + 0.7f) * t.BreathRotation,
                Mathf.Sin(input.Time * t.BreathFrequency.x * 2f * Mathf.PI + 1.3f) * t.BreathRotation, 0f) * breathScale;

            // Movement: strafe roll, forward pull, air lift.
            var walk = Mathf.Max(0.1f, input.WalkSpeed);
            var strafe = Mathf.Clamp(input.LocalVelocity.x / walk, -1.5f, 1.5f);
            var fwd = Mathf.Clamp(input.LocalVelocity.z / walk, -1f, 1.5f);
            var moveScale = Mathf.Lerp(1f, 0.3f, ads);
            _movePos.Target = new Vector3(0f, t.AirLift * _air01, -t.ForwardPull * Mathf.Max(0f, fwd)) * moveScale;
            _moveRot.Target = new Vector3(0f, 0f, -strafe * t.StrafeRollDegrees) * moveScale;
            _movePos.Step(dt);
            _moveRot.Step(dt);

            // Kick springs back to rest.
            _kickPos.Target = Vector3.zero;
            _kickRot.Target = Vector3.zero;
            _kickPos.Step(dt);
            _kickRot.Step(dt);

            // Stance poses (hip only).
            var crouch = CombatUtil.Smooth01(_crouch01) * hip;
            var slide = CombatUtil.Smooth01(_slide01);
            pos += vm.SprintPosition * sprint + vm.CrouchPosition * crouch + vm.SlidePosition * slide + vm.LoweredPosition * lowered;
            pos += bobPos + breath + _swayPos.Value + _kickPos.Value + _movePos.Value + input.Reload.WeaponOffset + input.Action.WeaponOffset;

            var euler = vm.SprintEuler * sprint + vm.CrouchEuler * crouch + vm.SlideEuler * slide + vm.LoweredEuler * lowered +
                        bobRot + breathRot + _swayRot.Value + _kickRot.Value + _moveRot.Value + input.Reload.WeaponEuler + input.Action.WeaponEuler;
            rotation = rot * Quaternion.Euler(euler);
            position = pos;
        }

        /// <summary>The pose values of one view-model prefab (ViewmodelRig, defaults from <see cref="ViewmodelSpec"/>).</summary>
        public struct PoseSet
        {
            /// <summary>Hip grip position.</summary>
            public Vector3 HipPosition;
            /// <summary>Hip rotation.</summary>
            public Vector3 HipEuler;
            /// <summary>Eye to rear sight distance at full ADS.</summary>
            public float EyeRelief;
            /// <summary>Fine ADS offset.</summary>
            public Vector3 AdsOffset;
            /// <summary>Rear sight in weapon space.</summary>
            public Vector3 SightRear;
            /// <summary>Front sight in weapon space.</summary>
            public Vector3 SightFront;
            /// <summary>Sprint offset.</summary>
            public Vector3 SprintPosition;
            /// <summary>Sprint rotation.</summary>
            public Vector3 SprintEuler;
            /// <summary>Seconds out of the sprint pose (the weapon's sprint-out time).</summary>
            public float SprintOutTime;
            /// <summary>Crouch offset.</summary>
            public Vector3 CrouchPosition;
            /// <summary>Crouch rotation.</summary>
            public Vector3 CrouchEuler;
            /// <summary>Slide offset.</summary>
            public Vector3 SlidePosition;
            /// <summary>Slide rotation.</summary>
            public Vector3 SlideEuler;
            /// <summary>Lowered offset.</summary>
            public Vector3 LoweredPosition;
            /// <summary>Lowered rotation.</summary>
            public Vector3 LoweredEuler;
            /// <summary>Sway multiplier.</summary>
            public float SwayScale;
            /// <summary>Bob multiplier.</summary>
            public float BobScale;

            /// <summary>Pose values from a weapon spec (used when a prefab has no ViewmodelRig overrides).</summary>
            public static PoseSet From(WeaponSpec w)
            {
                var v = w?.Viewmodel ?? new ViewmodelSpec();
                return new PoseSet
                {
                    HipPosition = v.HipPosition, HipEuler = v.HipEuler, EyeRelief = v.EyeRelief, AdsOffset = v.AdsOffset,
                    SightRear = v.Sockets.SightRear, SightFront = v.Sockets.SightFront,
                    SprintPosition = v.SprintPosition, SprintEuler = v.SprintEuler, SprintOutTime = w != null && w.SprintOutTime > 0f ? w.SprintOutTime : 0.2f,
                    CrouchPosition = v.CrouchPosition, CrouchEuler = v.CrouchEuler, SlidePosition = v.SlidePosition, SlideEuler = v.SlideEuler,
                    LoweredPosition = v.LoweredPosition, LoweredEuler = v.LoweredEuler, SwayScale = v.SwayScale, BobScale = v.BobScale,
                };
            }
        }
    }
}
