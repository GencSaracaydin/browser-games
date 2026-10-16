using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.UI.Common;
using LongShadow.UI.Hud;
using LongShadow.UI.Menus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LongShadow.UI.Screens
{
    /// <summary>
    /// Debrief (GDD §8.9): a CRT dossier typed line by line with live stats (numbers roll while their line types),
    /// "HOSTILE" on Hollister's line struck through and retyped "UNDER REVIEW", redaction bars wiping across the
    /// personnel names, then the stamp <c>PROGRAM LONG SHADOW — ASSETS ACTIVE: 13</c> and the buttons
    /// <c>[REPLAY SESSION] · [SELECT BEAT] · [END SESSION]</c>. Green phosphor on CRT black; redactions are inverse-video
    /// blocks (black felt-marker art would vanish on a black screen); the stamp uses the IMG_DEBRIEF_DOSSIER stamp art
    /// when imported. REPLAY and END return to the click gate (its click is the browser gesture that re-locks the pointer;
    /// REPLAY then starts from the intro); SELECT BEAT resumes the chosen checkpoint behind the pause menu so the
    /// player's click re-locks the pointer.
    /// </summary>
    public sealed class DebriefScreen : HudView
    {
        private const float Cps = 62f;
        private const float LinePause = 0.14f;
        private const float LinePitch = 44f;
        private const float FontSize = 28f;

        private enum Phase { Closed, Typing, Strike, Retype, Redact, Stamp, Buttons, SelectBeat }

        private readonly TextMeshProUGUI[] _lines = new TextMeshProUGUI[DebriefModel.LineCount];
        private readonly TextBuffer[] _texts = new TextBuffer[DebriefModel.LineCount];
        private readonly Image[] _redactions = new Image[4];
        private readonly List<CrtButton> _beatButtons = new List<CrtButton>();
        private readonly List<string> _beatIds = new List<string>();
        private CanvasGroup _group;
        private CrtScreen _crt;
        private RectTransform _sheet;
        private Image _strike;
        private RectTransform _stamp;
        private Image _cursor;
        private CrtButton _replay, _select, _end, _beatBack;
        private RectTransform _beatPanel;
        private DebriefModel _model;
        private Phase _phase;
        private int _line;
        private float _lineAge;
        private float _phaseAge;
        private int _typed;
        private int _baseLength;
        private float _fade;

        /// <summary>True while open.</summary>
        public bool IsOpen => _phase != Phase.Closed;

        /// <summary>True once the buttons are up (tests).</summary>
        public bool Finished => _phase == Phase.Buttons || _phase == Phase.SelectBeat;

        /// <summary>Plain text of a dossier line (tests; allocates).</summary>
        internal string LineText(int i) => _texts[i].ToString();

        protected override void OnBuild(RectTransform parent)
        {
            Root = UIFactory.Stretch("Debrief", parent);
            _group = UIFactory.Group(Root.gameObject, 0f, true);
            UIFactory.StretchImage("CrtBlack", Root, Ctx.Sprite(UISpriteId.Square), UIColors.CrtBlack);
            UIFactory.StretchImage("Glow", Root, Ctx.Sprite(UISpriteId.SoftDisc), new Color(0.3f, 0.8f, 0.35f, 0.05f));

            _sheet = UIFactory.Rect("Sheet", Root, UIFactory.Center, UIFactory.Center, new Vector2(0f, 90f), new Vector2(1320f, LinePitch * DebriefModel.LineCount));
            var glow = UIColors.WithAlpha(UIColors.Phosphor, 0.38f);
            for (var i = 0; i < DebriefModel.LineCount; i++)
            {
                _texts[i] = new TextBuffer(96);
                _lines[i] = UIFactory.Text("Line" + i, _sheet, Ctx.Fonts, MenuStyle.Font, i == 0 ? FontSize + 2f : FontSize,
                    i == 0 ? UIColors.PhosphorBright : UIColors.Phosphor, TextAlignmentOptions.TopLeft, UIFactory.TopLeft, UIFactory.TopLeft,
                    new Vector2(0f, -i * LinePitch), new Vector2(1320f, LinePitch), false, glow);
            }
            for (var i = 0; i < 4; i++)
            {
                _redactions[i] = UIFactory.Image("Redact" + i, _lines[DebriefModel.PersonnelFirst + i].rectTransform, Ctx.Sprite(UISpriteId.Square),
                    UIColors.WithAlpha(UIColors.Phosphor, 0.9f), UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(-4f, -4f), new Vector2(0f, FontSize + 6f));
                _redactions[i].enabled = false;
            }
            var strikeArt = UISprites.GetOptional(UISpriteId.StrikeThrough, Ctx.Theme);
            _strike = UIFactory.Image("Strike", _lines[DebriefModel.HollisterLine].rectTransform, strikeArt != null ? strikeArt : Ctx.Sprite(UISpriteId.Square),
                strikeArt != null ? UIColors.PhosphorBright : UIColors.PhosphorBright, UIFactory.TopLeft, UIFactory.MiddleLeft, Vector2.zero, new Vector2(0f, 3f));
            _strike.enabled = false;
            _cursor = UIFactory.Image("Cursor", _sheet, Ctx.Sprite(UISpriteId.CrtCursor), Color.white, UIFactory.TopLeft, UIFactory.TopLeft, Vector2.zero, new Vector2(15f, 26f));

            BuildStamp();
            BuildButtons();
            _crt = CrtScreen.Add(Root, Ctx.Theme, Ctx.Theme.ScanlineStrength, Ctx.Theme.CrtFlicker, true, true);
            _crt.BaseAlpha = 0f;
            Root.gameObject.SetActive(false);
        }

        private void BuildStamp()
        {
            _stamp = UIFactory.Rect("Stamp", Root, UIFactory.Center, UIFactory.Center, new Vector2(330f, -150f), new Vector2(620f, 150f));
            _stamp.localEulerAngles = new Vector3(0f, 0f, 8f);
            var art = UISprites.GetOptional(UISpriteId.Stamp, Ctx.Theme);
            if (art != null)
            {
                var img = UIFactory.StretchImage("Art", _stamp, art, Color.white);
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
            }
            else
            {
                var frame = UIFactory.StretchImage("Frame", _stamp, Ctx.Sprite(UISpriteId.Outline), UIColors.WithAlpha(UIColors.StampRed, 0.9f));
                frame.type = Image.Type.Sliced;
                var t = UIFactory.Text("Text", _stamp, Ctx.Fonts, FontRole.Typewriter, 34f, UIColors.WithAlpha(UIColors.StampRed, 0.92f), TextAlignmentOptions.Center,
                    UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(600f, 130f), false);
                t.textWrappingMode = TextWrappingModes.Normal;
                t.SetText("PROGRAM LONG SHADOW\nASSETS ACTIVE: 13");
            }
            _stamp.gameObject.SetActive(false);
        }

        private void BuildButtons()
        {
            _replay = CrtButton.Create("Replay", Root, Ctx.Fonts, Ctx.Theme, "[REPLAY SESSION]", UIFactory.BottomCenter, UIFactory.BottomCenter,
                new Vector2(-440f, 90f), new Vector2(380f, MenuStyle.ItemHeight), Ctx.Audio);
            _select = CrtButton.Create("Select", Root, Ctx.Fonts, Ctx.Theme, "[SELECT BEAT]", UIFactory.BottomCenter, UIFactory.BottomCenter,
                new Vector2(0f, 90f), new Vector2(330f, MenuStyle.ItemHeight), Ctx.Audio);
            _end = CrtButton.Create("End", Root, Ctx.Fonts, Ctx.Theme, "[END SESSION]", UIFactory.BottomCenter, UIFactory.BottomCenter,
                new Vector2(420f, 90f), new Vector2(330f, MenuStyle.ItemHeight), Ctx.Audio);
            _replay.Clicked += OnReplay;
            _select.Clicked += OpenSelectBeat;
            _end.Clicked += OnEnd;
            _replay.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = _select, selectOnLeft = _end };
            _select.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = _end, selectOnLeft = _replay };
            _end.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = _replay, selectOnLeft = _select };
            SetButtons(false);

            _beatPanel = UIFactory.Rect("SelectBeat", Root, UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(760f, 760f));
            UIFactory.Group(_beatPanel.gameObject, 1f, true);
            var glass = UIFactory.StretchImage("Glass", _beatPanel, Ctx.Sprite(UISpriteId.CrtPanel), Color.white);
            glass.type = Image.Type.Sliced;
            var title = UIFactory.Text("Title", _beatPanel, Ctx.Fonts, MenuStyle.Font, 28f, MenuStyle.Text, TextAlignmentOptions.TopLeft,
                UIFactory.TopLeft, UIFactory.TopLeft, new Vector2(48f, -32f), new Vector2(660f, 40f), false, MenuStyle.Glow);
            title.SetText("// SELECT BEAT");
            var cps = SequenceIds.Checkpoints;
            for (var i = 0; i < cps.Length; i++)
            {
                var cp = cps[i];
                var b = CrtButton.Create(cp, _beatPanel, Ctx.Fonts, Ctx.Theme, BeatLabel(cp), UIFactory.TopLeft, UIFactory.TopLeft,
                    new Vector2(40f, -86f - i * 52f), new Vector2(680f, 46f), Ctx.Audio, 22f);
                b.Clicked += () => OnSelectBeat(cp);
                b.Cancelled += CloseSelectBeat;
                _beatButtons.Add(b);
                _beatIds.Add(cp);
            }
            _beatBack = CrtButton.Create("Back", _beatPanel, Ctx.Fonts, Ctx.Theme, "BACK", UIFactory.BottomLeft, UIFactory.BottomLeft,
                new Vector2(40f, 28f), new Vector2(200f, 46f), Ctx.Audio, 22f);
            _beatBack.Clicked += CloseSelectBeat;
            _beatBack.Cancelled += CloseSelectBeat;
            _beatPanel.gameObject.SetActive(false);
        }

        /// <summary>Menu label of a checkpoint (beat names from GDD §7).</summary>
        public static string BeatLabel(string cp)
        {
            switch (cp)
            {
                case SequenceIds.CP1: return "CP1   BEAT 1 — THE SHOULDER";
                case SequenceIds.CP2: return "CP2   BEAT 2 — PUMP HOUSE SIX";
                case SequenceIds.CP3: return "CP3   BEAT 3 — THE LONG MARKET";
                case SequenceIds.CP3b: return "CP3B  BEAT 3 — THE CROSSING";
                case SequenceIds.CP4: return "CP4   BEAT 4 — CLOCK SQUARE";
                case SequenceIds.CP5: return "CP5   BEAT 5 — LAST LIGHT";
                case SequenceIds.CP6: return "CP6   BEAT 6 — THE RELAY YARD";
                case SequenceIds.CP7: return "CP7   BEAT 7 — THE RELAY HALL";
                case SequenceIds.CP8: return "CP8   BEAT 8 — THE REEL";
                case SequenceIds.CP9: return "CP9   BEAT 9 — THE CLOCKMAKER";
                case SequenceIds.CP10: return "CP10  BEAT 10 — THE PITCH";
                default: return cp;
            }
        }

        /// <summary>Opens the debrief for the given stats.</summary>
        public void Open(MissionStats stats)
        {
            var objectives = Ctx.Objectives.Value;
            _model = objectives != null
                ? new DebriefModel(stats, objectives.IsComplete("B7.Ledgers"), objectives.IsComplete("B8.Film"), objectives.IsComplete("B9.Codebook") || (stats != null && stats.Intel >= 3))
                : DebriefModel.FromStats(stats);

            for (var i = 0; i < DebriefModel.LineCount; i++)
            {
                _texts[i].Clear();
                _lines[i].SetText(string.Empty);
                _lines[i].maxVisibleCharacters = 0;
            }
            for (var i = 0; i < 4; i++)
            {
                _redactions[i].enabled = false;
                _redactions[i].rectTransform.sizeDelta = new Vector2(0f, FontSize + 6f);
            }
            _strike.enabled = false;
            _stamp.gameObject.SetActive(false);
            _beatPanel.gameObject.SetActive(false);
            SetButtons(false);
            _phase = Phase.Typing;
            _line = 0;
            _lineAge = 0f;
            _typed = 0;
            _fade = 0f;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();

            // The debrief is the end of the mission: no pause, free cursor, SELECT BEAT unlocked (GDD §17).
            var pause = Ctx.Pause.Value;
            if (pause != null) pause.CanPause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var settings = Ctx.Settings.Value;
            if (settings != null && settings.GetValue(StorageKeys.CompletedOnce) != "1") settings.SetValue(StorageKeys.CompletedOnce, "1");
            StartLine(0);
        }

        /// <summary>Closes the debrief.</summary>
        public void Close()
        {
            _phase = Phase.Closed;
            Root.gameObject.SetActive(false);
            MenuSelection.Clear();
        }

        public override void ResetState()
        {
            if (IsOpen) Close();
        }

        private void StartLine(int line)
        {
            _line = line;
            _lineAge = 0f;
            _typed = 0;
            _model.Write(line, _texts[line], 0f);
            _baseLength = _texts[line].Length;
            _texts[line].ApplyTo(_lines[line]);
            _lines[line].maxVisibleCharacters = 0;
            if (_texts[line].Length > 0) Ctx.Audio.Play(UiCues.TeletypeLine, 0.45f);
        }

        public override void Tick(float dt)
        {
            if (_phase == Phase.Closed) return;
            _fade = Mathf.Min(1f, _fade + dt / 0.6f);
            _crt.BaseAlpha = _fade; // CrtScreen multiplies its flicker onto this base every frame
            _group.alpha = _fade;
            _phaseAge += dt;
            switch (_phase)
            {
                case Phase.Typing: TickTyping(dt); break;
                case Phase.Strike: TickStrike(); break;
                case Phase.Retype: TickRetype(dt); break;
                case Phase.Redact: TickRedact(); break;
                case Phase.Stamp: TickStamp(); break;
            }
            var blink = UIAnim.Blink(Time.unscaledTime, 1.06f) && _phase != Phase.Buttons && _phase != Phase.SelectBeat;
            if (_cursor.enabled != blink) _cursor.enabled = blink;
        }

        private void TickTyping(float dt)
        {
            _lineAge += dt;
            var line = _line;
            var length = _baseLength;
            var target = Mathf.Min(length, Mathf.FloorToInt(_lineAge * Cps));
            var isStats = line == DebriefModel.StatsLineA || line == DebriefModel.StatsLineB;
            if (isStats)
            {
                // Live stats: the numbers roll up while the line types (and 0.4 s after).
                var f = UIAnim.OutCubic(_lineAge / (length / Cps + 0.4f));
                _model.Write(line, _texts[line], f);
                _texts[line].ApplyTo(_lines[line]);
            }
            if (target != _typed)
            {
                _typed = target;
                _lines[line].maxVisibleCharacters = target;
                Ctx.Audio.TypeKey(0.35f, 0.04f);
            }
            PlaceCursor(line, target);
            var done = target >= length && (!isStats || _lineAge >= length / Cps + 0.4f);
            if (!done || _lineAge < length / Cps + LinePause) return;
            if (isStats)
            {
                _model.Write(line, _texts[line], 1f);
                _texts[line].ApplyTo(_lines[line]);
            }
            _lines[line].maxVisibleCharacters = 99999;
            if (line + 1 < DebriefModel.LineCount) StartLine(line + 1);
            else SetPhase(Phase.Strike);
        }

        private void TickStrike()
        {
            if (_phaseAge < 0.5f) return;
            var line = _lines[DebriefModel.HollisterLine];
            if (!_strike.enabled)
            {
                _strike.enabled = true;
                line.ForceMeshUpdate();
            }
            var info = line.textInfo;
            var startX = 0f;
            var endX = 0f;
            var midY = -FontSize * 0.55f;
            if (info != null && info.characterCount > DebriefModel.StrikeStart + DebriefModel.StrikeLength - 1)
            {
                var a = info.characterInfo[DebriefModel.StrikeStart];
                var b = info.characterInfo[DebriefModel.StrikeStart + DebriefModel.StrikeLength - 1];
                startX = a.bottomLeft.x;
                endX = b.topRight.x;
                midY = (a.bottomLeft.y + a.topLeft.y) * 0.5f; // text pivot = top-left = the strike's anchor
            }
            var t = Mathf.Clamp01((_phaseAge - 0.5f) / 0.35f);
            _strike.rectTransform.anchoredPosition = new Vector2(startX - 4f, midY);
            _strike.rectTransform.sizeDelta = new Vector2((endX - startX + 8f) * UIAnim.OutCubic(t), 3f);
            if (t >= 1f) SetPhase(Phase.Retype);
        }

        private void TickRetype(float dt)
        {
            if (_phaseAge < 0.25f) return;
            var line = DebriefModel.HollisterLine;
            if (_texts[line].Length == DebriefModel.PersonnelBaseLength(line))
            {
                _model.Write(line, _texts[line], 1f, true);
                _texts[line].ApplyTo(_lines[line]);
            }
            var baseLen = DebriefModel.PersonnelBaseLength(line);
            var total = _texts[line].Length;
            var typed = Mathf.Min(total, baseLen + Mathf.FloorToInt((_phaseAge - 0.25f) * Cps * 0.5f));
            if (typed != _typed)
            {
                _typed = typed;
                _lines[line].maxVisibleCharacters = typed;
                Ctx.Audio.TypeKey(0.45f, 0.06f);
            }
            PlaceCursor(line, typed);
            if (typed >= total && _phaseAge > 0.25f + (total - baseLen) / (Cps * 0.5f) + 0.6f) SetPhase(Phase.Redact);
        }

        private void TickRedact()
        {
            var allDone = true;
            for (var i = 0; i < 4; i++)
            {
                var start = i * 0.15f;
                var t = Mathf.Clamp01((_phaseAge - start) / 0.25f);
                if (t > 0f && !_redactions[i].enabled)
                {
                    _redactions[i].enabled = true;
                    Ctx.Audio.Play(UiCues.MenuMove, 0.5f);
                }
                var width = CharWidth() * DebriefModel.NameLength(DebriefModel.PersonnelFirst + i) + 8f;
                _redactions[i].rectTransform.sizeDelta = new Vector2(width * UIAnim.OutCubic(t), FontSize + 6f);
                if (t < 1f) allDone = false;
            }
            if (allDone && _phaseAge > 1.2f) SetPhase(Phase.Stamp);
        }

        private void TickStamp()
        {
            if (!_stamp.gameObject.activeSelf)
            {
                _stamp.gameObject.SetActive(true);
                Ctx.Audio.Play(UiCues.Kill, 1f);
            }
            var t = Mathf.Clamp01(_phaseAge / 0.22f);
            var s = Mathf.Lerp(1.9f, 1f, UIAnim.OutCubic(t));
            _stamp.localScale = new Vector3(s, s, 1f);
            if (_phaseAge > 0.9f)
            {
                SetPhase(Phase.Buttons);
                SetButtons(true);
                MenuSelection.Select(_replay.gameObject);
                GameEvents.RaiseMissionEvent(UiEvents.DebriefReady);
            }
        }

        private float CharWidth()
        {
            var l = _lines[DebriefModel.RuleLine];
            var info = l.textInfo;
            if (info != null && info.characterCount > 1)
            {
                var a = info.characterInfo[0];
                var b = info.characterInfo[info.characterCount - 1];
                var w = (b.topRight.x - a.bottomLeft.x) / info.characterCount;
                if (w > 1f) return w;
            }
            return FontSize * 0.6f; // Courier Prime advance
        }

        private void PlaceCursor(int line, int typed)
        {
            _cursor.rectTransform.anchoredPosition = new Vector2(typed * CharWidth() + 4f, -line * LinePitch - 4f);
        }

        private void SetPhase(Phase p)
        {
            _phase = p;
            _phaseAge = 0f;
            _typed = 0;
        }

        private void SetButtons(bool on)
        {
            _replay.gameObject.SetActive(on);
            _select.gameObject.SetActive(on);
            _end.gameObject.SetActive(on);
        }

        private void OpenSelectBeat()
        {
            _phase = Phase.SelectBeat;
            _beatPanel.gameObject.SetActive(true);
            _beatPanel.SetAsLastSibling();
            var settings = Ctx.Settings.Value;
            var completed = settings == null || settings.GetValue(StorageKeys.CompletedOnce) == "1";
            var highest = settings != null ? SequenceIds.CheckpointOrder(settings.GetValue(StorageKeys.HighestCheckpoint)) : -1;
            CrtButton first = null, prev = null;
            for (var i = 0; i < _beatButtons.Count; i++)
            {
                var unlocked = completed || i <= highest;
                var b = _beatButtons[i];
                b.interactable = unlocked;
                if (!unlocked) continue;
                if (first == null) first = b;
                if (prev != null)
                {
                    var n = prev.navigation;
                    n.selectOnDown = b;
                    prev.navigation = n;
                    b.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = prev };
                }
                else b.navigation = new Navigation { mode = Navigation.Mode.Explicit };
                prev = b;
            }
            if (prev != null)
            {
                var n = prev.navigation;
                n.selectOnDown = _beatBack;
                prev.navigation = n;
            }
            _beatBack.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = prev };
            MenuSelection.Select(first != null ? first.gameObject : _beatBack.gameObject);
        }

        private void CloseSelectBeat()
        {
            _beatPanel.gameObject.SetActive(false);
            _phase = Phase.Buttons;
            MenuSelection.Select(_select.gameObject);
        }

        private void OnSelectBeat(string cp)
        {
            GameEvents.RaiseMissionEvent(UiEvents.DebriefSelectPrefix + cp);
            Close();
            var director = Ctx.Director.Value;
            var pause = Ctx.Pause.Value;
            director?.ResumeFrom(cp);
            if (pause != null)
            {
                // The pause menu's RESUME click re-locks the pointer inside a browser gesture.
                pause.CanPause = true;
                pause.Pause();
            }
        }

        private void OnReplay()
        {
            GameEvents.RaiseMissionEvent(UiEvents.DebriefReplay);
            ToGate();
        }

        private void OnEnd()
        {
            GameEvents.RaiseMissionEvent(UiEvents.DebriefEnd);
            ToGate();
        }

        private void ToGate()
        {
            Close();
            Ctx.Director.Value?.QuitToSession();
            var gate = Ctx.Gate.Value;
            if (gate != null)
            {
                gate.SetVisible(true);
                gate.SetReady(true);
            }
        }

        /// <summary>Test hook: jumps to the end of the sequence.</summary>
        internal void SkipToEnd()
        {
            for (var i = 0; i < DebriefModel.LineCount; i++)
            {
                _model.Write(i, _texts[i], 1f, i == DebriefModel.HollisterLine);
                _texts[i].ApplyTo(_lines[i]);
                _lines[i].maxVisibleCharacters = 99999;
            }
            SetPhase(Phase.Buttons);
            SetButtons(true);
        }
    }
}
