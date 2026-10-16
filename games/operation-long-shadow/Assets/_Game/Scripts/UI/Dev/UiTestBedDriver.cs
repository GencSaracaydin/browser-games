using System;
using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LongShadow.UI.Dev
{
    /// <summary>
    /// Drives every HUD element in the UI test bed (WP08 definition of done: "a builder-made test scene with fake services
    /// shows every HUD element"). Keys trigger single features (F1 lists them); F2 runs a scripted showcase of the whole
    /// HUD, the title card, the pause/options flow and the debrief. Uses only Core interfaces and the fakes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiTestBedDriver : MonoBehaviour
    {
        [SerializeField] private UiFakeServices _fakes;
        [SerializeField] private Transform[] _markerTargets;
        [SerializeField] private Transform _ringTarget;
        [SerializeField] private Transform _player;
        [SerializeField] private bool _showLegend = true;
        [SerializeField] private bool _autoShowcase;

        private ServiceRef<IHud> _hud;
        private ServiceRef<IClickGate> _gate;
        private int _subtitleIndex;
        private int _handle = 100;
        private float _drill;
        private bool _letterbox;
        private HudOverlay _overlay;
        private float _holdProgress = -1f;
        private int _tutorialCount;
        private int _objectiveStep;
        private float _gateProgress = -1f;
        private string _log = "F1: keys   F2: showcase";
        private readonly List<(float time, Action action)> _showcase = new List<(float, Action)>();
        private float _showcaseTime = -1f;
        private int _showcaseIndex;
        private GUIStyle _style;

        private static readonly SubtitleSample[] s_samples =
        {
            new SubtitleSample("D007", SpeakerIds.Hollis, "SUNDIAL-ACTUAL", "Sun's at our backs. Only gift we get today. Glass the town.", true, false),
            new SubtitleSample("D008", SpeakerIds.Sparrow, "SUNDIAL-2", "Orange armbands are Harrou's Legion. Grey ones at the station gate are advisors. No insignia. Nobody's advisor.", true, false),
            new SubtitleSample("D009", SpeakerIds.Tinker, "SUNDIAL-4", "Clock tower's stuck at six-forty. Been stuck for years, they say.", true, false),
            new SubtitleSample("D069", SpeakerIds.Magpie, "MAGPIE", "Sundial, Magpie. Ninety seconds out. How hot?", true, false),
            new SubtitleSample("D001", SpeakerIds.Interrogator, "UNKNOWN", "Sergeant Calder. Eyes on the screens, not on me. October twenty-first. Zaroud.", false, false),
            new SubtitleSample("D039", SpeakerIds.Clockmaker, "CLOCKMAKER", "Attention. Attention. Seven-four-four-one-zero. Seven-four-four-one-zero. Zero. Six. Four...", true, false),
            new SubtitleSample("D012", SpeakerIds.Harrou, "PA", "People of Zaroud, stay inside. The curfew is for your protection.", false, false),
            new SubtitleSample("BK_LEG_02", SpeakerIds.Legion, "LEGION", "(shouting) Push! Push!", false, true),
        };

        private readonly struct SubtitleSample
        {
            public readonly string Id, Speaker, Callsign, Text;
            public readonly bool Radio, Bark;

            public SubtitleSample(string id, string speaker, string callsign, string text, bool radio, bool bark)
            {
                Id = id;
                Speaker = speaker;
                Callsign = callsign;
                Text = text;
                Radio = radio;
                Bark = bark;
            }
        }

        private static readonly ObjectiveDef[] s_objectives =
        {
            new ObjectiveDef { Id = "B3.Push", Text = "PUSH THROUGH THE LONG MARKET", Index = 3, Total = 11 },
            new ObjectiveDef { Id = "B4.Flank", Text = "FLANK THROUGH THE CINEMA", Index = 4, Total = 11 },
            new ObjectiveDef { Id = "B4.Technical", Text = "NEUTRALIZE THE TECHNICAL", Index = 4, Total = 11 },
            new ObjectiveDef { Id = "B9.Transmitter", Text = "DESTROY THE TRANSMITTER 0/4", Index = 9, Total = 11 },
        };

        /// <summary>Assigns scene references (builder).</summary>
        public void Configure(UiFakeServices fakes, Transform[] markerTargets, Transform ringTarget, Transform player)
        {
            _fakes = fakes;
            _markerTargets = markerTargets;
            _ringTarget = ringTarget;
            _player = player;
        }

        private void Start()
        {
            BuildShowcase();
            SetObjectives(0);
            if (_gate.Value != null) ShowGate(); // start like the game: the click gate fills, then waits for a click
            if (_autoShowcase) StartShowcase();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _fakes == null) return;
            var hud = _hud.Value;

            if (kb.f1Key.wasPressedThisFrame) _showLegend = !_showLegend;
            if (kb.f2Key.wasPressedThisFrame) StartShowcase();
            if (kb.digit1Key.wasPressedThisFrame) TogglePalette();
            if (kb.digit2Key.wasPressedThisFrame) _fakes.Weapon.Fire();
            if (kb.digit3Key.wasPressedThisFrame) _fakes.Weapon.Reload();
            if (kb.digit4Key.wasPressedThisFrame) GameEvents.RaiseHitConfirmed(false, false);
            if (kb.digit5Key.wasPressedThisFrame) GameEvents.RaiseHitConfirmed(true, true);
            if (kb.digit6Key.wasPressedThisFrame) Damage(UnityEngine.Random.Range(0f, 360f));
            if (kb.digit7Key.wasPressedThisFrame) Grenade();
            if (kb.digit8Key.wasPressedThisFrame) Subtitle(false);
            if (kb.digit9Key.wasPressedThisFrame) Subtitle(true);
            if (kb.digit0Key.wasPressedThisFrame) _holdProgress = _holdProgress < 0f ? 0f : -1f;
            if (kb.qKey.wasPressedThisFrame) hud?.IntelCard("LEDGERS", null, "INTEL 1/3");
            if (kb.bKey.wasPressedThisFrame) Banner();
            if (kb.fKey.wasPressedThisFrame) hud?.FailCard("YOU ABANDONED YOUR TEAM");
            if (kb.oKey.wasPressedThisFrame) CycleOverlay();
            if (kb.tKey.wasPressedThisFrame) hud?.TitleCard(8f);
            if (kb.lKey.wasPressedThisFrame) ToggleLetterbox();
            if (kb.gKey.wasPressedThisFrame) Drill();
            if (kb.nKey.wasPressedThisFrame) Tutorial();
            if (kb.kKey.wasPressedThisFrame) NextObjective();
            if (kb.hKey.wasPressedThisFrame) _fakes.Health.Current = Mathf.Max(5f, _fakes.Health.Current - 15f);
            if (kb.jKey.wasPressedThisFrame) _fakes.Health.ResetFull();
            if (kb.zKey.wasPressedThisFrame) RigAction(true);
            if (kb.xKey.wasPressedThisFrame) RigAction(false);
            if (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) _fakes.Pause.Pause();
            if (kb.uKey.wasPressedThisFrame) hud?.ShowDebrief(_fakes.Director.Stats);
            if (kb.cKey.wasPressedThisFrame) ShowGate();
            if (kb.vKey.wasPressedThisFrame) Awareness();
            if (kb.rKey.wasPressedThisFrame) ResetAll();

            TickHold(hud);
            TickGate();
            TickShowcase();
            _fakes.Weapon.CrosshairSpread01 = Mathf.MoveTowards(_fakes.Weapon.CrosshairSpread01, 0.12f, Time.unscaledDeltaTime * 0.8f);
        }

        // ---------------------------------------------------------------- actions

        private void TogglePalette()
        {
            var s = _fakes.Light.S >= 2f ? 0.4f : 2.4f;
            _fakes.Light.SetImmediate(s);
            Log("LightStage s = " + s.ToString("0.0") + (s >= 2f ? " (pale green markers)" : " (amber markers)"));
        }

        private void Damage(float relativeBearing)
        {
            if (_player == null) return;
            var dir = Quaternion.Euler(0f, _player.eulerAngles.y + relativeBearing, 0f) * Vector3.forward;
            var source = _player.position + dir * 15f;
            var d = new DamageInfo(12f, DamageType.Bullet, HitZone.Torso, _player.position + Vector3.up * 1.4f, -dir, null, Team.Legion, "AK");
            _fakes.Health.ApplyDamage(d);
            Log("Damage from " + Mathf.RoundToInt(relativeBearing) + " deg (source " + source + ")");
        }

        private void Grenade()
        {
            if (_player == null) return;
            var pos = _player.position + _player.rotation * new Vector3(-2.5f, 0f, 3f);
            GameEvents.RaiseGrenadeLanded(pos, Team.Legion);
            Log("Grenade landed 4 m front-left (explodes in 4 s)");
            _pendingExplosion = pos;
            _explodeAt = Time.unscaledTime + 4f;
        }

        private Vector3 _pendingExplosion;
        private float _explodeAt = -1f;

        private void Subtitle(bool bark)
        {
            SubtitleSample s;
            if (bark) s = s_samples[s_samples.Length - 1];
            else
            {
                s = s_samples[_subtitleIndex % (s_samples.Length - 1)];
                _subtitleIndex++;
            }
            var style = SpeakerIds.StyleOf(s.Speaker);
            var data = new SubtitleData
            {
                Handle = ++_handle,
                LineId = s.Id,
                Speaker = style.DisplayName,
                Callsign = s.Callsign,
                Text = s.Text,
                Color = style.Color,
                Radio = s.Radio,
                Italic = style.ItalicInBooth,
                Monospace = style.Monospace,
                IsBark = s.Bark,
                Duration = 2.5f + s.Text.Length * 0.045f,
            };
            GameEvents.RaiseSubtitleShow(data);
            Log("Subtitle " + s.Id + " (" + s.Speaker + ")");
        }

        private int _bannerCount = -1;
        private float _bannerNext;

        private void Banner()
        {
            _bannerCount = 6;
            _bannerNext = 0f;
            GameEvents.RaiseSubtitleShow(new SubtitleData
            {
                Handle = ++_handle, LineId = "D093", Speaker = "HOLLIS", Callsign = "SUNDIAL-ACTUAL", Text = "Calder, where are you going? Back on me.",
                Color = SpeakerIds.StyleOf(SpeakerIds.Hollis).Color, Radio = true, Duration = 3f,
            });
        }

        private void CycleOverlay()
        {
            _overlay = (HudOverlay)(((int)_overlay + 1) % 3);
            _hud.Value?.SetOverlay(_overlay);
            if (_overlay == HudOverlay.DoorGun) _fakes.Weapon.Heat01 = 0.2f;
            Log("Overlay " + _overlay);
        }

        private void ToggleLetterbox()
        {
            _letterbox = !_letterbox;
            _hud.Value?.Letterbox(_letterbox, 0.5f);
        }

        private void Drill()
        {
            _drill = _drill >= 1f ? 0f : Mathf.Min(1f, _drill + 0.17f);
            if (_ringTarget != null) _hud.Value?.SetRing("B9.Drill", _ringTarget, _drill, "DRILL " + Mathf.RoundToInt(_drill * 100f) + " %");
        }

        private void Tutorial()
        {
            _tutorialCount++;
            var texts = new[]
            {
                "Hold RIGHT MOUSE to raise the binoculars. Keep a building in the centre to mark it.",
                "Hold G to cook a grenade. Release to throw.",
                "Press C while sprinting to slide into cover.",
            };
            _hud.Value?.Tutorial("testbed." + _tutorialCount, texts[_tutorialCount % texts.Length]);
        }

        private void NextObjective()
        {
            var o = _fakes.Objectives;
            if (o.Active.Count > 0) o.Complete(o.Active[0].Id);
            _objectiveStep = (_objectiveStep + 1) % s_objectives.Length;
            o.Set(s_objectives[_objectiveStep]);
            if (_objectiveStep == 2 && _markerTargets != null && _markerTargets.Length > 1)
                o.AddMarker(new MarkerDef { Id = "B4.Technical", Target = _markerTargets[1], Kind = MarkerKind.HighPriority, Label = "TECHNICAL", ShowDistance = true, ThroughWalls = true });
        }

        private void SetObjectives(int step)
        {
            var o = _fakes.Objectives;
            o.Clear();
            o.Set(s_objectives[step]);
            o.IntelCount = 1;
            o.OptionalCount = 0;
            o.Set(new ObjectiveDef { Id = "B7.O1", Text = "—", Index = 7, Total = 11, Optional = true });
            if (_markerTargets != null)
            {
                for (var i = 0; i < _markerTargets.Length; i++)
                {
                    var t = _markerTargets[i];
                    if (t == null) continue;
                    var kind = i == 0 ? MarkerKind.Diamond : i == 1 ? MarkerKind.HighPriority : i == 2 ? MarkerKind.DiamondHollow : MarkerKind.Intel;
                    o.AddMarker(new MarkerDef { Id = "TB.Marker" + i, Target = t, Kind = kind, Label = i == 0 ? "ARCH" : i == 1 ? "TECHNICAL" : i == 2 ? "POI" : null, ShowDistance = true, ThroughWalls = kind == MarkerKind.HighPriority });
                }
            }
            var squad = _fakes.Squad.Member(SquadIds.Hollis);
            if (squad != null) o.AddMarker(new MarkerDef { Id = "B1.Follow", Target = squad, Kind = MarkerKind.Follow, Label = "FOLLOW" });
        }

        private void RigAction(bool ads)
        {
            var rig = GameServices.Get<IPlayerRig>() as FakePlayerRig;
            if (rig == null) return;
            if (ads) rig.ToggleAds();
            else rig.ToggleSprint();
        }

        private void ShowGate()
        {
            var gate = _gate.Value;
            if (gate == null)
            {
                Log("No IClickGate in this scene");
                return;
            }
            gate.SetVisible(true);
            gate.SetReady(false);
            _gateProgress = 0f;
        }

        private void Awareness()
        {
            var e = _fakes.Encounters;
            var v = e.Awareness01("E2-01") >= 1f ? 0f : Mathf.Min(1f, e.Awareness01("E2-01") + 0.25f);
            e.SetAwareness("E2-01", v);
            e.SetAwareness("E2-02", v * 0.5f);
            Log("Sentry awareness " + v.ToString("0.00"));
        }

        private void ResetAll()
        {
            _fakes.Health.ResetFull();
            _fakes.Weapon.Magazine = 30;
            _fakes.Weapon.Reserve = 120;
            _fakes.Weapon.Heat01 = 0f;
            _overlay = HudOverlay.None;
            _hud.Value?.SetOverlay(HudOverlay.None);
            _hud.Value?.ClearRing("B9.Drill");
            _hud.Value?.Banner(string.Empty, 0f);
            SetObjectives(0);
            _fakes.Checkpoints.Reload(false);
        }

        private void TickHold(IHud hud)
        {
            if (hud == null) return;
            if (_holdProgress < 0f)
            {
                hud.HidePrompt("Interact");
                return;
            }
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.isPressed) _holdProgress = Mathf.Min(1f, _holdProgress + Time.unscaledDeltaTime / 1.2f);
            else _holdProgress = Mathf.Max(0f, _holdProgress - Time.unscaledDeltaTime * 2f);
            hud.ShowPrompt(new PromptData { Id = "Interact", Text = "HOLD [E] Take the ledgers", Hold = true, Progress01 = _holdProgress });
            if (_holdProgress >= 1f)
            {
                _holdProgress = -1f;
                hud.IntelCard("LEDGERS", null, "INTEL 1/3");
            }
        }

        private void TickGate()
        {
            if (_explodeAt > 0f && Time.unscaledTime >= _explodeAt)
            {
                _explodeAt = -1f;
                GameEvents.RaiseGrenadeExploded(_pendingExplosion, 6f);
            }
            if (_bannerCount > 0 && Time.unscaledTime >= _bannerNext)
            {
                _hud.Value?.Banner("RETURN TO THE MISSION AREA " + _bannerCount + "…", 1.15f);
                _bannerCount--;
                _bannerNext = Time.unscaledTime + 1f;
            }
            if (_gateProgress < 0f) return;
            var gate = _gate.Value;
            if (gate == null) return;
            _gateProgress += Time.unscaledDeltaTime * 0.25f;
            gate.SetProgress(Mathf.Clamp01(_gateProgress));
            if (_gateProgress >= 1f)
            {
                _gateProgress = -1f;
                gate.SetReady(true);
                gate.Clicked -= OnGateClicked;
                gate.Clicked += OnGateClicked;
            }
        }

        private void OnGateClicked()
        {
            var gate = _gate.Value;
            if (gate == null) return;
            gate.Clicked -= OnGateClicked;
            gate.SetReady(false);
            gate.SetVisible(false);
            Log("Gate clicked");
        }

        // ---------------------------------------------------------------- showcase

        private void BuildShowcase()
        {
            _showcase.Clear();
            var t = 0f;
            void At(float dt, Action a)
            {
                t += dt;
                _showcase.Add((t, a));
            }
            At(0.5f, () => Subtitle(false));
            At(1.5f, () => GameEvents.RaiseHitConfirmed(false, false));
            At(0.4f, () => GameEvents.RaiseHitConfirmed(true, true));
            At(0.6f, () => Damage(-60f));
            At(0.5f, () => Damage(150f));
            At(0.8f, Grenade);
            At(1.0f, () => { _fakes.Weapon.Magazine = 6; });
            At(1.5f, () => { _fakes.Weapon.Magazine = 0; });
            At(1.5f, () => { _fakes.Weapon.Reload(); });
            At(0.5f, () => _holdProgress = 0f);
            At(2.0f, () => _hud.Value?.IntelCard("REELS", null, "INTEL 2/3"));
            At(1.0f, Tutorial);
            At(2.5f, NextObjective);
            At(2.0f, () => Subtitle(false));
            At(2.5f, () => Subtitle(true));
            At(1.5f, TogglePalette);
            At(2.0f, Banner);
            At(6.5f, () => { _fakes.Health.Current = 20f; });
            At(2.5f, () => _fakes.Health.ResetFull());
            At(0.5f, Drill);
            At(0.5f, Drill);
            At(0.5f, Awareness);
            At(0.5f, Awareness);
            At(1.5f, () => { _overlay = HudOverlay.None; CycleOverlay(); });
            At(2.5f, () => { CycleOverlay(); _fakes.Weapon.Heat01 = 0.95f; _fakes.Weapon.Overheated = true; });
            At(2.5f, () => { _fakes.Weapon.Overheated = false; _fakes.Weapon.Heat01 = 0f; CycleOverlay(); });
            At(0.5f, () => _hud.Value?.FailCard("YOU ABANDONED YOUR TEAM"));
            At(3.0f, () => _fakes.Checkpoints.Reload(false));
            At(1.0f, () => _hud.Value?.TitleCard(8f));
            At(9.0f, () => _fakes.Pause.Pause());
            At(4.0f, () => _fakes.Pause.Resume());
            At(1.0f, () => _hud.Value?.ShowDebrief(_fakes.Director.Stats));
        }

        private void StartShowcase()
        {
            _showcaseTime = 0f;
            _showcaseIndex = 0;
            ResetAll();
            Log("Showcase running (" + _showcase.Count + " steps)");
        }

        private void TickShowcase()
        {
            if (_showcaseTime < 0f) return;
            _showcaseTime += Time.unscaledDeltaTime;
            while (_showcaseIndex < _showcase.Count && _showcase[_showcaseIndex].time <= _showcaseTime)
            {
                try
                {
                    _showcase[_showcaseIndex].action();
                }
                catch (Exception e)
                {
                    LSLog.Exception(LogCategory.UI, e, this);
                }
                _showcaseIndex++;
            }
            if (_showcaseIndex >= _showcase.Count) _showcaseTime = -1f;
        }

        private void Log(string message)
        {
            _log = message;
            LSLog.Info(LogCategory.UI, "[TestBed] " + message, this);
        }

        private void OnGUI()
        {
            if (!_showLegend) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = false };
                _style.normal.textColor = new Color(0.7f, 1f, 0.7f, 0.9f);
            }
            const string legend =
                "UI TEST BED  F1 legend  F2 showcase  RMB+mouse look  WASD move\n" +
                "1 palette  2 fire  3 reload  4 hit  5 kill+headshot  6 damage  7 grenade\n" +
                "8 subtitle  9 bark  0 hold prompt (E)  Q intel  B OOB banner  F fail card\n" +
                "O overlay  T title card  L letterbox  G drill ring  N tutorial  K next objective  Tab list\n" +
                "H hurt  J heal  Z ADS  X sprint  P/Esc pause  U debrief  C click gate  V sentry meter  R reset";
            GUI.Label(new Rect(12, Screen.height - 110, 900, 100), legend + "\n> " + _log, _style);
        }
    }
}
