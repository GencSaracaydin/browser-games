using LongShadow.Core;

namespace LongShadow.UI.Dev
{
    /// <summary>Settable <see cref="IWeaponHudState"/> for the UI test bed and tests (a Commando by default).</summary>
    public sealed class FakeWeaponHudState : IWeaponHudState
    {
        public float CrosshairSpread01 { get; set; } = 0.15f;
        public bool CrosshairVisible { get; set; } = true;
        public float Heat01 { get; set; }
        public bool Overheated { get; set; }
        public float CookTime01 { get; set; }
        public string DisplayName { get; set; } = "COMMANDO";
        public int Magazine { get; set; } = 30;
        public int MagazineSize { get; set; } = 30;
        public int Reserve { get; set; } = 120;
        public int Frags { get; set; } = 3;
        public FireMode FireMode { get; set; } = FireMode.Auto;
        public bool IsReloading { get; set; }

        /// <summary>Fires one round (spread kick).</summary>
        public void Fire()
        {
            if (Magazine <= 0) return;
            Magazine--;
            CrosshairSpread01 = UnityEngine.Mathf.Min(1f, CrosshairSpread01 + 0.12f);
        }

        /// <summary>Instant reload from the reserve.</summary>
        public void Reload()
        {
            var need = MagazineSize - Magazine;
            var take = UnityEngine.Mathf.Min(need, Reserve);
            Magazine += take;
            Reserve -= take;
        }
    }
}
