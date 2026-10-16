using System;
using LongShadow.Core;
using LongShadow.Player.Movement;
using LongShadow.Player.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LongShadow.Player.Body
{
    /// <summary>
    /// Seats the player (TECH_PLAN §7.3 PlayerMount; Beat 11 helicopter door gun, GDD §13.3): the rig root is parented
    /// to the seat (feet at the seat origin, facing its forward) so it rides the helicopter without a frame of lag, the
    /// motor stops (controller off), the look becomes seat-relative (limits follow the seat), and
    /// <see cref="GameEvents.PlayerMountChanged"/> is raised. The weapon side (<c>IWeaponService.MountDoorGun</c>) is
    /// called by the mission script, not here.
    /// <para>Parenting moves the rig into the seat's scene; <see cref="Dismount"/> puts it back under its previous parent
    /// (or the scene it came from, normally S_Persistent). Dismount before the seat's section unloads.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMount : MonoBehaviour
    {
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private PlayerLook _look;

        private Transform _seat;
        private Transform _previousParent;
        private Scene _previousScene;
        private bool _mounted;

        /// <summary>Raised with true on mount and false on dismount.</summary>
        public event Action<bool> Changed;

        /// <summary>True while seated.</summary>
        public bool IsMounted => _mounted;
        /// <summary>The current seat, or null.</summary>
        public Transform Seat => _mounted ? _seat : null;

        /// <summary>Wires the motor and look (PlayerRigFactory).</summary>
        internal void Bind(PlayerMotor motor, PlayerLook look)
        {
            _motor = motor;
            _look = look;
        }

        private void Awake()
        {
            if (_motor == null) _motor = GetComponent<PlayerMotor>();
            if (_look == null) _look = GetComponent<PlayerLook>();
        }

        /// <summary>Seats the player on <paramref name="seat"/>. Mounting another seat first leaves the current one.</summary>
        public bool Mount(Transform seat)
        {
            if (seat == null)
            {
                LSLog.Warn(LogCategory.Player, "PlayerMount.Mount(null) ignored.", this);
                return false;
            }
            if (_mounted && seat == _seat) return true;
            if (_mounted) Dismount();

            if (_look != null) _look.Mount(seat); // before parenting: converts the yaw to the seat frame
            if (_motor != null) _motor.SetMounted(true);
            _previousParent = transform.parent;
            _previousScene = gameObject.scene;
            transform.SetParent(seat, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (_look != null) _look.Apply();
            _seat = seat;
            _mounted = true;
            LSLog.Info(LogCategory.Player, "Mounted on " + seat.name, this);
            Changed?.Invoke(true);
            GameEvents.RaisePlayerMountChanged(true);
            return true;
        }

        /// <summary>Leaves the seat where it is now and gives control back to the motor.</summary>
        public void Dismount()
        {
            if (!_mounted) return;
            _mounted = false;
            var position = transform.position;
            var parent = _previousParent;
            transform.SetParent(parent, true);
            if (parent == null && _previousScene.IsValid() && _previousScene.isLoaded && gameObject.scene != _previousScene)
                SceneManager.MoveGameObjectToScene(gameObject, _previousScene);
            transform.position = position;
            if (_look != null) _look.Dismount(); // writes the upright root yaw
            if (_motor != null) _motor.SetMounted(false);
            _seat = null;
            _previousParent = null;
            LSLog.Info(LogCategory.Player, "Dismounted", this);
            Changed?.Invoke(false);
            GameEvents.RaisePlayerMountChanged(false);
        }
    }
}
