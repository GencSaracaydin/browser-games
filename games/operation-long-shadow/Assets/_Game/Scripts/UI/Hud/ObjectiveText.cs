using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;

namespace LongShadow.UI.Hud
{
    /// <summary>
    /// Objective text (GDD §11, §7.12): top-left, revealed teletype-style,
    /// <c>OBJ 4/11 — 47219 — NEUTRALIZE THE TECHNICAL</c>. Up to three primary objectives show; a completed one is struck
    /// through, holds, then fades. Holding Tab shows the full list (optional intel included) with the intel counts.
    /// Driven by <see cref="GameEvents.ObjectivesChanged"/>/<see cref="GameEvents.ObjectiveCompleted"/> plus a 0.5 s
    /// poll; text is rebuilt only when something changes.
    /// </summary>
    public sealed class ObjectiveText : HudView
    {
        private const int Capacity = 10;
        private const float RevealCps = 55f;
        private const float CompleteHold = 1.6f;
        private const float CompleteFade = 0.45f;

        private sealed class Entry
        {
            public ObjectiveDef Def;
            public float Age;
            public bool Completing;
            public float CompleteAge;
            public bool Seen;
            public TextMeshProUGUI Line;
            public readonly TextBuffer Text = new TextBuffer(96);
            public int VisibleChars;
            public float Height;
        }

        private readonly Entry[] _entries = new Entry[Capacity];
        private int _count;
        private readonly TextMeshProUGUI[] _lines = new TextMeshProUGUI[Capacity];
        private TextMeshProUGUI _intel;
        private readonly TextBuffer _intelText = new TextBuffer(48);
        private CanvasGroup _group;
        private bool _dirty = true;
        private bool _layoutDirty = true;
        private float _poll;
        private bool _expanded;
        private int _lastIntel = -1, _lastOptional = -1;
        private float _width;
        private System.Action _onChanged;
        private System.Action<string> _onCompleted;

        /// <summary>Lines currently shown (tests).</summary>
        public int ShownCount { get; private set; }

        /// <summary>Plain text of a shown entry (tests; allocates).</summary>
        internal string EntryText(int i) => i >= 0 && i < _count ? _entries[i].Text.ToString() : null;

        protected override void OnBuild(RectTransform parent)
        {
            _width = HudLayout.ObjectiveWidth(Ctx.CanvasSize);
            Root = UIFactory.Rect("Objectives", parent, UIFactory.TopLeft, UIFactory.TopLeft,
                new Vector2(HudLayout.Margin, -HudLayout.ObjectiveTop), new Vector2(_width, HudLayout.ObjectiveLine * HudLayout.ObjectiveLinesCollapsed));
            _group = UIFactory.Group(Root.gameObject);
            for (var i = 0; i < Capacity; i++)
            {
                var t = UIFactory.Text("Line" + i, Root, Ctx.Fonts, FontRole.TypewriterClean, HudLayout.ObjectiveFont, UIColors.Ink,
                    TextAlignmentOptions.TopLeft, UIFactory.TopLeft, UIFactory.TopLeft, Vector2.zero, new Vector2(_width, HudLayout.ObjectiveLine));
                t.textWrappingMode = TextWrappingModes.Normal;
                t.lineSpacing = -8f;
                t.enabled = false;
                _lines[i] = t;
                _entries[i] = new Entry { Line = t };
            }
            _intel = UIFactory.Text("Intel", Root, Ctx.Fonts, FontRole.Condensed, 19f, UIColors.InkDim, TextAlignmentOptions.TopLeft,
                UIFactory.TopLeft, UIFactory.TopLeft, Vector2.zero, new Vector2(_width, 24f));
            _intel.characterSpacing = 3f;
            _intel.enabled = false;
        }

        public override void Enable()
        {
            _onChanged ??= () => _dirty = true;
            _onCompleted ??= OnCompleted;
            GameEvents.ObjectivesChanged += _onChanged;
            GameEvents.ObjectiveCompleted += _onCompleted;
            _dirty = true;
        }

        public override void Disable()
        {
            GameEvents.ObjectivesChanged -= _onChanged;
            GameEvents.ObjectiveCompleted -= _onCompleted;
        }

        public override void ResetState()
        {
            _count = 0;
            for (var i = 0; i < Capacity; i++) _lines[i].enabled = false;
            _dirty = true;
            _layoutDirty = true;
        }

        public override void Relayout(Vector2 canvasSize)
        {
            _width = HudLayout.ObjectiveWidth(canvasSize);
            Root.sizeDelta = new Vector2(_width, Root.sizeDelta.y);
            for (var i = 0; i < Capacity; i++) _lines[i].rectTransform.sizeDelta = new Vector2(_width, HudLayout.ObjectiveLine);
            _intel.rectTransform.sizeDelta = new Vector2(_width, 24f);
            for (var i = 0; i < _count; i++) Measure(_entries[i]);
            _layoutDirty = true;
        }

        private void OnCompleted(string id)
        {
            for (var i = 0; i < _count; i++)
            {
                var e = _entries[i];
                if (!string.Equals(e.Def.Id, id) || e.Completing) continue;
                StartCompleting(e);
            }
            _dirty = true;
        }

        public override void Tick(float dt)
        {
            var objectives = Ctx.Objectives.Value;
            _poll -= dt;
            if (_poll <= 0f)
            {
                _poll = 0.5f;
                _dirty = true;
            }
            if (_dirty)
            {
                _dirty = false;
                Sync(objectives);
            }

            var input = Ctx.Input.Value;
            var expanded = input != null && input.ObjectivesHeld;
            if (expanded != _expanded)
            {
                _expanded = expanded;
                _layoutDirty = true;
            }

            // Reveal and completion animation.
            for (var i = 0; i < _count; i++)
            {
                var e = _entries[i];
                e.Age += dt;
                if (e.VisibleChars < e.Text.Length)
                {
                    var target = Mathf.Min(e.Text.Length, Mathf.FloorToInt(e.Age * RevealCps));
                    if (target != e.VisibleChars)
                    {
                        e.VisibleChars = target;
                        e.Line.maxVisibleCharacters = target >= e.Text.Length ? 99999 : target;
                    }
                }
                if (e.Completing)
                {
                    e.CompleteAge += dt;
                    var a = 1f - Mathf.Clamp01((e.CompleteAge - CompleteHold) / CompleteFade);
                    e.Line.alpha = a;
                    if (e.CompleteAge > CompleteHold + CompleteFade)
                    {
                        RemoveAt(i);
                        i--;
                        _layoutDirty = true;
                    }
                }
            }

            UpdateIntel(objectives);
            if (_layoutDirty) Layout();
            _group.alpha = Ctx.Overlay == HudOverlay.DoorGun && !_expanded ? 0.6f : 1f;
        }

        private void Sync(IObjectiveService objectives)
        {
            for (var i = 0; i < _count; i++) _entries[i].Seen = false;
            if (objectives != null)
            {
                IReadOnlyList<ObjectiveDef> active = objectives.Active;
                var n = active != null ? active.Count : 0;
                for (var a = 0; a < n; a++)
                {
                    var def = active[a];
                    var e = Find(def.Id);
                    if (e == null)
                    {
                        if (_count >= Capacity) break;
                        e = _entries[_count++];
                        e.Def = def;
                        e.Age = 0f;
                        e.Completing = false;
                        e.CompleteAge = 0f;
                        e.VisibleChars = 0;
                        e.Line.maxVisibleCharacters = 0;
                        e.Line.alpha = 1f;
                        Rebuild(e);
                        if (!def.Optional)
                        {
                            Ctx.Audio.Play(UiCues.Objective, 0.8f);
                            Ctx.Audio.Play(UiCues.TeletypeLine, 0.6f);
                        }
                        _layoutDirty = true;
                    }
                    else if (!e.Completing && (!string.Equals(e.Def.Text, def.Text) || e.Def.Index != def.Index || e.Def.Total != def.Total))
                    {
                        // Counter text changed ("OBSERVE ZAROUD 1/3"): retype without the full reveal.
                        e.Def = def;
                        Rebuild(e);
                        e.VisibleChars = e.Text.Length;
                        e.Line.maxVisibleCharacters = 99999;
                        _layoutDirty = true;
                    }
                    e.Seen = true;
                }
            }
            for (var i = 0; i < _count; i++)
            {
                var e = _entries[i];
                if (e.Seen || e.Completing) continue;
                var completed = objectives != null && objectives.IsComplete(e.Def.Id);
                if (completed) StartCompleting(e);
                else
                {
                    RemoveAt(i);
                    i--;
                }
                _layoutDirty = true;
            }
        }

        private void StartCompleting(Entry e)
        {
            e.Completing = true;
            e.CompleteAge = 0f;
            Rebuild(e, true);
            e.VisibleChars = e.Text.Length;
            e.Line.maxVisibleCharacters = 99999;
        }

        private void Rebuild(Entry e, bool struck = false)
        {
            var prefix = UIColors.WithAlpha(UIColors.KeyAmber, 0.85f);
            var text = struck ? UIColors.InkDim : UIColors.Ink;
            if (e.Def.Optional && (string.IsNullOrEmpty(e.Def.Text) || e.Def.Text == "—"))
            {
                var def = e.Def;
                def.Text = "OPTIONAL INTEL";
                ObjectiveFormatter.Write(e.Text, def, prefix, text, struck);
            }
            else
            {
                ObjectiveFormatter.Write(e.Text, e.Def, prefix, text, struck);
            }
            e.Text.ApplyTo(e.Line);
            Measure(e);
        }

        private void Measure(Entry e)
        {
            var pref = e.Line.GetPreferredValues(_width, 0f);
            e.Height = Mathf.Max(HudLayout.ObjectiveLine, pref.y + 6f);
        }

        private void Layout()
        {
            _layoutDirty = false;
            var y = 0f;
            var shown = 0;
            for (var i = 0; i < _count; i++)
            {
                var e = _entries[i];
                var show = _expanded || (!e.Def.Optional && shown < HudLayout.ObjectiveLinesCollapsed) || e.Completing;
                if (e.Def.Optional && !_expanded) show = false;
                if (e.Line.enabled != show) e.Line.enabled = show;
                if (!show) continue;
                e.Line.rectTransform.anchoredPosition = new Vector2(0f, -y);
                e.Line.rectTransform.sizeDelta = new Vector2(_width, e.Height);
                y += e.Height;
                shown++;
            }
            if (_intel.enabled != _expanded) _intel.enabled = _expanded;
            if (_expanded) _intel.rectTransform.anchoredPosition = new Vector2(0f, -y - 4f);
            ShownCount = shown;
        }

        private void UpdateIntel(IObjectiveService objectives)
        {
            if (objectives == null) return;
            var intel = objectives.IntelCount;
            var optional = objectives.OptionalCount;
            if (intel == _lastIntel && optional == _lastOptional) return;
            _lastIntel = intel;
            _lastOptional = optional;
            ObjectiveFormatter.WriteIntel(_intelText, intel, optional);
            _intelText.ApplyTo(_intel);
        }

        private Entry Find(string id)
        {
            for (var i = 0; i < _count; i++)
                if (string.Equals(_entries[i].Def.Id, id)) return _entries[i];
            return null;
        }

        private void RemoveAt(int index)
        {
            var e = _entries[index];
            e.Line.enabled = false;
            for (var i = index; i < _count - 1; i++) _entries[i] = _entries[i + 1];
            _entries[_count - 1] = e;
            _count--;
        }

        public override void CollectLayout(List<HudLayoutRegion> regions)
        {
            // Audit the collapsed block at its maximum: three lines, each possibly wrapped once.
            var saved = Root.sizeDelta;
            Root.sizeDelta = new Vector2(_width, HudLayout.ObjectiveLine * HudLayout.ObjectiveLinesCollapsed + 6f);
            regions.Add(HudLayoutRegion.Of("Objectives", Root, Ctx.CanvasRect));
            Root.sizeDelta = saved;
        }
    }
}
