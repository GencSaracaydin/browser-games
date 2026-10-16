using System.Text;
using LongShadow.AI.Common;
using LongShadow.AI.Encounters;
using LongShadow.AI.Enemy;
using LongShadow.AI.Squad;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Dev
{
    /// <summary>
    /// Runs the AI dev arena (TECH_PLAN §7.6, §11.4 WP04 DoD): registers the dev encounter "DEV" (4 Legion riflemen and a
    /// perched gunner, a Legion flank wave from a closet, a Greycoat pair with suppressor and flanker; 9 enemies, max
    /// alive 7) and optionally two Beat 2-style smoking sentries ("DEV_SENTRY"), puts the squad on a node set, and draws
    /// an overlay with the AI cost (ms average/peak, budget 3 ms), the ray budget peak (≤ 24), attack-token holders
    /// against capacity, alive counts and every enemy's state, mode and token, plus buttons to drive the squad.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AIDevArenaDirector : MonoBehaviour
    {
        /// <summary>Main dev encounter id.</summary>
        public const string EncounterId = "DEV";
        /// <summary>Optional sentry encounter id.</summary>
        public const string SentryEncounterId = "DEV_SENTRY";

        [SerializeField] private bool _activateOnStart = true;
        [SerializeField] private bool _includeSentries;
        [SerializeField] private bool _overlay = true;

        private readonly StringBuilder _sb = new StringBuilder(1024);
        private float _nextText;
        private string _text = string.Empty;
        private ServiceRef<ISquadService> _squad;
        private AIDevTarget _dummy;

        /// <summary>The main dev encounter (also used by tests).</summary>
        public static EncounterDef BuildEncounter()
        {
            const ArchetypeId R = ArchetypeId.LegionRifleman;
            var e = new EncounterDef(EncounterId, 0, SectionId.Approach, 7) { Notes = "AI dev arena" };
            e.AddWave(new WaveDef("A", WaveTrigger.OnActivate()).Note("Riflemen behind the north blocks and a perched gunner."));
            e.AddWave(new WaveDef("B", WaveTrigger.Any(WaveCondition.WaveAliveAtMost("A", 2), WaveCondition.PlayerIn(AIDevArenaLayout.PushTrigger),
                    WaveCondition.After(40f))).Alerted().Note("Legion flank wave from the north closet."));
            e.AddWave(new WaveDef("G", WaveTrigger.Any(WaveCondition.KilledAtLeast(3), WaveCondition.After(30f))).Alerted()
                .Note("Greycoat pair: suppressor + flanker."));
            var a = AIDevArenaLayout.WaveA;
            for (var i = 0; i < a.Length; i++)
                e.Add(new EnemySpawnDef("DEV-A" + (i + 1), R, a[i], 180f).InWave("A").AtMarker(null));
            e.Add(new EnemySpawnDef("DEV-GUN", ArchetypeId.LegionGunner, AIDevArenaLayout.PerchPoint, 180f).Perch(AIDevArenaLayout.Perch).InWave("A"));
            e.Add(new EnemySpawnDef("DEV-B1", R, AIDevArenaLayout.ClosetNorthPos, 160f).Closet(AIDevArenaLayout.ClosetNorth).InWave("B", 0f).Flank(AIDevArenaLayout.RouteWest));
            e.Add(new EnemySpawnDef("DEV-B2", R, AIDevArenaLayout.ClosetNorthPos, 160f).Closet(AIDevArenaLayout.ClosetNorth).InWave("B", 1f).Flank(AIDevArenaLayout.RouteWest));
            e.Add(new EnemySpawnDef("DEV-G1", ArchetypeId.GreycoatAdvisor, AIDevArenaLayout.ClosetEastPos, 220f).Closet(AIDevArenaLayout.ClosetEast)
                .InWave("G", 0f).Pair("DEV.Pair", PairRole.Flanker).Flank(AIDevArenaLayout.RouteEast));
            e.Add(new EnemySpawnDef("DEV-G2", ArchetypeId.GreycoatAdvisor, AIDevArenaLayout.ClosetEastPos, 220f).Closet(AIDevArenaLayout.ClosetEast)
                .InWave("G", 1f).Pair("DEV.Pair", PairRole.Suppressor, AIDevArenaLayout.SuppressPos));
            return e;
        }

        /// <summary>The optional sentry encounter.</summary>
        public static EncounterDef BuildSentryEncounter()
        {
            var e = new EncounterDef(SentryEncounterId, 0, SectionId.Approach, 2);
            e.AddWave(new WaveDef("Sentries", WaveTrigger.OnActivate()));
            var s = AIDevArenaLayout.Sentries;
            e.Add(new EnemySpawnDef("DEV-S1", ArchetypeId.LegionRifleman, s[0], 200f).InWave("Sentries").AsSentry().AtMarker(null));
            e.Add(new EnemySpawnDef("DEV-S2", ArchetypeId.LegionRifleman, s[1], 160f).InWave("Sentries").AsSentry().AtMarker(null));
            return e;
        }

        private void Start()
        {
            _dummy = FindAnyObjectByType<AIDevTarget>();
            var spawner = EncounterSpawner.Instance;
            if (spawner == null)
            {
                LSLog.Error(LogCategory.AI, "AIDevArenaDirector: no EncounterSpawner in the scene (add PF_AIServices).", this);
                return;
            }
            spawner.RegisterEncounter(BuildEncounter());
            spawner.RegisterEncounter(BuildSentryEncounter());
            _squad.Value?.SetNodeSet(AIDevArenaLayout.NodeSetA);
            if (!_activateOnStart) return;
            spawner.Activate(EncounterId);
            if (_includeSentries) spawner.Activate(SentryEncounterId);
        }

        private void OnGUI()
        {
            if (!_overlay) return;
            if (Time.unscaledTime >= _nextText)
            {
                _nextText = Time.unscaledTime + 0.25f;
                _text = BuildText();
            }
            GUI.Box(new Rect(8, 8, 430, 22 + 15 * CountLines(_text)), GUIContent.none);
            GUI.Label(new Rect(14, 12, 420, 15 * CountLines(_text) + 16), _text);
            var y = 30 + 15 * CountLines(_text);
            var spawner = EncounterSpawner.Instance;
            var squad = _squad.Value;
            if (GUI.Button(new Rect(8, y, 100, 22), "Squad: A")) squad?.SetNodeSet(AIDevArenaLayout.NodeSetA);
            if (GUI.Button(new Rect(112, y, 100, 22), "Squad: B")) squad?.SetNodeSet(AIDevArenaLayout.NodeSetB);
            if (GUI.Button(new Rect(216, y, 100, 22), "Squad: follow")) squad?.Follow();
            if (GUI.Button(new Rect(320, y, 118, 22), "Chemlights")) squad?.SetChemlights(!(SquadService.Instance?.ChemlightsOn ?? false));
            y += 26;
            if (GUI.Button(new Rect(8, y, 100, 22), "Alert DEV")) GameEvents.RaiseMissionEvent(AIEvents.AlertPrefix + EncounterId);
            if (GUI.Button(new Rect(112, y, 100, 22), "Wave B")) spawner?.TriggerWave(EncounterId, "B");
            if (GUI.Button(new Rect(216, y, 100, 22), "Wave G")) spawner?.TriggerWave(EncounterId, "G");
            if (GUI.Button(new Rect(320, y, 118, 22), "Restart"))
            {
                spawner?.DespawnAll();
                spawner?.RegisterEncounter(BuildEncounter());
                spawner?.Activate(EncounterId);
                if (_includeSentries) spawner?.Activate(SentryEncounterId);
                AIDirector.Instance?.ResetStats();
            }
        }

        private static int CountLines(string s)
        {
            var n = 1;
            for (var i = 0; i < s.Length; i++)
                if (s[i] == '\n') n++;
            return n;
        }

        private string BuildText()
        {
            var d = AIDirector.Instance;
            var spawner = EncounterSpawner.Instance;
            _sb.Length = 0;
            if (d == null) return "No AIDirector";
            _sb.Append("AI ").Append(d.AverageMs.ToString("0.00")).Append(" ms avg, ").Append(d.PeakMs.ToString("0.00"))
               .Append(" ms peak (budget 3.00)  agents ").Append(d.AgentCount).Append('\n');
            _sb.Append("Rays/frame peak ").Append(d.Budget.PeakPerFrame).Append(" / ").Append(d.Budget.PerFrame)
               .Append("   tokens ").Append(d.Tokens.HolderCount).Append(" / ").Append(d.Tokens.Capacity)
               .Append("   waiting ").Append(d.Tokens.WaiterCount).Append('\n');
            _sb.Append("Alive ").Append(spawner != null ? spawner.AliveTotal : 0).Append(" (cap ").Append(EncounterTable.HardCap)
               .Append(")  DEV alive ").Append(spawner?.Alive(EncounterId) ?? 0).Append(" killed ").Append(spawner?.Killed(EncounterId) ?? 0)
               .Append(spawner != null && spawner.Cleared(EncounterId) ? "  CLEARED" : string.Empty)
               .Append("   combat ").Append(d.Combat.InCombat ? "yes" : "no")
               .Append("   player suppressed ").Append(d.PlayerSuppressed(Time.time) ? "yes" : "no").Append('\n');
            if (_dummy != null) _sb.Append("Dummy hits taken ").Append(_dummy.HitsTaken).Append(_dummy.Crouched ? " (crouched)" : string.Empty).Append('\n');
            var runtime = spawner?.Runtime(EncounterId);
            if (runtime != null)
            {
                var slots = runtime.Slots;
                for (var i = 0; i < slots.Count; i++)
                {
                    var s = slots[i];
                    _sb.Append(s.Id).Append(' ').Append(s.State);
                    var b = s.Instance != null ? s.Instance.Brain : null;
                    if (b != null && s.State == SlotState.Alive)
                    {
                        _sb.Append(" ").Append(b.State).Append('/').Append(b.Mode);
                        if (b.HasToken) _sb.Append(" [TOKEN]");
                        if (b.Cover != null) _sb.Append(" cover");
                        if (s.Instance.IsSuppressed(Time.time)) _sb.Append(" suppressed");
                    }
                    _sb.Append('\n');
                }
            }
            var squad = SquadService.Instance;
            if (squad != null)
            {
                _sb.Append("Squad ").Append(squad.Mode).Append(' ').Append(squad.CurrentSet ?? string.Empty)
                   .Append("  last callout ").Append(squad.Callouts.LastLine ?? "-");
            }
            return _sb.ToString();
        }
    }
}
