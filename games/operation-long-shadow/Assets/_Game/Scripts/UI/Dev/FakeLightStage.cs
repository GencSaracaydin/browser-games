using LongShadow.Core;
using LongShadow.UI.Common;
using UnityEngine;

namespace LongShadow.UI.Dev
{
    /// <summary>
    /// Minimal <see cref="ILightStage"/> for the UI test bed: <see cref="S"/> drives the marker palette (GDD §11: amber
    /// before 2.0, pale green after) and a linear story clock from 17:58 (s 0) to 19:00 (s 4).
    /// </summary>
    public sealed class FakeLightStage : ILightStage
    {
        private float _s;

        public float S => _s;
        public StageId Stage => _s < 1f ? StageId.Golden : _s < 2f ? StageId.Sunset : _s < 3f ? StageId.Afterglow : StageId.BlueHour;
        public bool IsOverridden { get; private set; }
        public float AISightRange => Mathf.Lerp(60f, 35f, _s / 4f);
        public float HearingScale => 1f;
        public bool AfterSunset => _s >= 2f;
        public Color HudColor => UIColors.MarkerPalette(_s);
        public float StoryClockMinutes => 17f * 60f + 58f + _s * 15.5f;

        public void SetTarget(float s, float smoothing = 2f) => SetImmediate(s);

        public void SetImmediate(float s)
        {
            var old = _s;
            _s = Mathf.Clamp(s, 0f, 4f);
            if (Mathf.Abs(old - _s) >= 0.01f) GameEvents.RaiseLightStageChanged(_s);
        }

        public void Override(object owner, bool on) => IsOverridden = on;
    }
}
