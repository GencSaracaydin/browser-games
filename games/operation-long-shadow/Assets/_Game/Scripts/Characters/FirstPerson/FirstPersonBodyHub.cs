using System;
using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.FirstPerson
{
    /// <summary>
    /// The one <see cref="IFirstPersonBody"/> registered in <see cref="GameServices"/>. Calder has two "first-person"
    /// bodies: the FP arms on the player rig (watch check, pistol rise) and the seated booth body seen from C_POV
    /// (outro fists, GDD §8.8 rows 2 and 5). Mission scripts and cinematics talk to this hub; it routes the watch check
    /// and pistol rise to <see cref="FPArmsIK"/> and the fists to every attached <see cref="IFistsTarget"/>. Registered
    /// while at least one body is attached. Statics reset on SubsystemRegistration (R6).
    /// </summary>
    public sealed class FirstPersonBodyHub : IFirstPersonBody
    {
        private static FirstPersonBodyHub s_instance;

        private FPArmsIK _arms;
        private readonly List<IFistsTarget> _fists = new List<IFistsTarget>(2);
        private bool _clenched;
        private bool _fistsSet;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_instance = null;

        /// <summary>The hub (created on first use).</summary>
        public static FirstPersonBodyHub Instance => s_instance ?? (s_instance = new FirstPersonBodyHub());

        /// <summary>Attaches the FP arms (registers the service).</summary>
        public void AttachArms(FPArmsIK arms)
        {
            _arms = arms;
            if (arms != null && !_fists.Contains(arms)) _fists.Add(arms);
            Refresh();
        }

        /// <summary>Detaches the FP arms.</summary>
        public void DetachArms(FPArmsIK arms)
        {
            if (_arms == arms) _arms = null;
            _fists.Remove(arms);
            Refresh();
        }

        /// <summary>Attaches a fists target (booth body).</summary>
        public void AttachFists(IFistsTarget target)
        {
            if (target == null || _fists.Contains(target)) return;
            _fists.Add(target);
            if (_fistsSet) target.SetFists(_clenched);
            Refresh();
        }

        /// <summary>Detaches a fists target.</summary>
        public void DetachFists(IFistsTarget target)
        {
            _fists.Remove(target);
            Refresh();
        }

        private void Refresh()
        {
            var any = _arms != null || _fists.Count > 0;
            if (any)
            {
                if (!GameServices.TryGet(out IFirstPersonBody existing) || !ReferenceEquals(existing, this))
                    GameServices.Register<IFirstPersonBody>(this);
            }
            else GameServices.Unregister<IFirstPersonBody>(this);
        }

        /// <inheritdoc/>
        public void PlayWatchCheck(float seconds, Action onDone)
        {
            if (_arms == null)
            {
                LSLog.WarnOnce("fp-nowatch", LogCategory.Characters, "PlayWatchCheck: no FP arms in the scene; skipped.");
                onDone?.Invoke();
                return;
            }
            _arms.PlayWatchCheck(seconds, onDone);
        }

        /// <inheritdoc/>
        public void PlayPistolRise(float seconds, Action onDone)
        {
            if (_arms == null)
            {
                LSLog.WarnOnce("fp-norise", LogCategory.Characters, "PlayPistolRise: no FP arms in the scene; skipped.");
                onDone?.Invoke();
                return;
            }
            _arms.PlayPistolRise(seconds, onDone);
        }

        /// <inheritdoc/>
        public void SetFists(bool clenched)
        {
            _clenched = clenched;
            _fistsSet = true;
            for (var i = _fists.Count - 1; i >= 0; i--)
            {
                var f = _fists[i];
                if (f == null || (f is UnityEngine.Object o && o == null))
                {
                    _fists.RemoveAt(i);
                    continue;
                }
                f.SetFists(clenched);
            }
        }

        /// <inheritdoc/>
        public bool IsPlaying => _arms != null && _arms.IsPlaying;
    }
}
