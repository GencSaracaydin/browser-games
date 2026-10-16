using LongShadow.Core;
using UnityEngine;

namespace LongShadow.UI.Dev
{
    /// <summary>Settable <see cref="IPlayerHealth"/> for the UI test bed (100 HP, GDD §16 Regular).</summary>
    public sealed class FakePlayerHealth : IPlayerHealth
    {
        public float Current { get; set; } = 100f;
        public float Max { get; set; } = 100f;
        public float TimeSinceDamage { get; set; } = 99f;
        public bool GodMode { get; set; }
        public Team Team => Team.Player;
        public bool IsAlive => Current > 0f;
        public Transform AimPoint { get; set; }

        public void ApplyDamage(in DamageInfo d)
        {
            Current = Mathf.Max(0f, Current - d.Amount);
            TimeSinceDamage = 0f;
            GameEvents.RaisePlayerDamaged(d);
        }

        public void ResetFull()
        {
            Current = Max;
            TimeSinceDamage = 99f;
        }
    }
}
