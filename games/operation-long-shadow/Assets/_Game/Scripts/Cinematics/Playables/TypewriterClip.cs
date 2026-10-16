using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LongShadow.Cinematics.Playables
{
    /// <summary>
    /// Typed on-screen text (TECH_PLAN §7.12 TypewriterClip): intro shot 1's slate
    /// "02 NOVEMBER 1984 — 03:12 — LOCATION: [REDACTED]", and the title card when WP08's HUD is missing (GDD §8.3:
    /// number groups corrected into text, 18:40 shown for 6 frames on line 3). Key clicks per character.
    /// </summary>
    [System.Serializable]
    public sealed class TypewriterClip : PlayableAsset, ITimelineClipAsset
    {
        /// <summary>Where the text sits.</summary>
        public enum Slot
        {
            /// <summary>Centre of the screen.</summary>
            Center,
            /// <summary>Lower third.</summary>
            Lower,
            /// <summary>Title-card block (lower left).</summary>
            Title,
        }

        [SerializeField, TextArea(2, 6)] private string _text;
        [SerializeField] private float _charsPerSecond = 26f;
        [SerializeField] private bool _numberGroups;
        [SerializeField] private Slot _slot = Slot.Center;
        [SerializeField] private Color _color = new Color(0.86f, 0.95f, 0.86f);
        [SerializeField, Tooltip("Show only when no IHud is registered (WP08 draws the title card).")] private bool _onlyWithoutHud;
        [SerializeField, Tooltip("Key click every N characters (0 = silent).")] private int _clickEvery = 1;
        [SerializeField] private string _clickCue = CinematicCues.TypeKey;
        [SerializeField] private int _flickerLine = -1;
        [SerializeField] private string _flickerFind;
        [SerializeField] private string _flickerShow;
        [SerializeField] private float _flickerSeconds = 0.1f;

        /// <inheritdoc/>
        public ClipCaps clipCaps => ClipCaps.None;
        /// <summary>Text ('\n' separates lines).</summary>
        public string Text { get => _text; set => _text = value; }
        /// <summary>Characters per second.</summary>
        public float CharsPerSecond { get => _charsPerSecond; set => _charsPerSecond = value; }
        /// <summary>Number groups first.</summary>
        public bool NumberGroups { get => _numberGroups; set => _numberGroups = value; }
        /// <summary>Screen slot.</summary>
        public Slot Position { get => _slot; set => _slot = value; }
        /// <summary>Text colour.</summary>
        public Color Color { get => _color; set => _color = value; }
        /// <summary>Only without WP08's HUD.</summary>
        public bool OnlyWithoutHud { get => _onlyWithoutHud; set => _onlyWithoutHud = value; }
        /// <summary>Click every N characters.</summary>
        public int ClickEvery { get => _clickEvery; set => _clickEvery = value; }
        /// <summary>Click cue.</summary>
        public string ClickCue { get => _clickCue; set => _clickCue = value; }

        /// <summary>Configures the line-3 flicker (find/show must have the same length).</summary>
        public void SetFlicker(int line, string find, string show, float seconds)
        {
            _flickerLine = line;
            _flickerFind = find;
            _flickerShow = show;
            _flickerSeconds = seconds;
        }

        /// <summary>Builds the reveal model.</summary>
        public TypewriterModel CreateModel()
        {
            var m = new TypewriterModel(_text, _charsPerSecond, _numberGroups);
            if (_flickerLine >= 0) m.SetFlicker(_flickerLine, _flickerFind, _flickerShow, 0.05f, _flickerSeconds);
            return m;
        }

        /// <inheritdoc/>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<TypewriterBehaviour>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }
    }

    /// <summary>Per-clip behaviour of typed text.</summary>
    internal sealed class TypewriterBehaviour : PlayableBehaviour
    {
        public TypewriterClip Clip;
        public TypewriterModel Model;
    }
}
