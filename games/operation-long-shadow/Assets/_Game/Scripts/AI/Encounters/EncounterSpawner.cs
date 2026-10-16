using System.Collections.Generic;
using LongShadow.AI.Common;
using LongShadow.AI.Enemy;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Encounters
{
    /// <summary>
    /// <see cref="IEncounterService"/> (TECH_PLAN §7.6 EncounterSpawner): runs the encounters of <see cref="EncounterTable"/>
    /// (E2-E11). It spawns enemies from pools (prewarmed when their section loads) using LIB_Enemies prefabs or runtime
    /// capsule soldiers, positions them from Appendix A markers (falling back to the table coordinates), enforces the
    /// closet rule (out of the player's sight by raycast to the closet door and spawn point, ≥ 15 m away), the per-beat
    /// max alive and the hard cap of 12 (extra spawns queue), the morale break, Greycoat pairs, ally alerts and "man down"
    /// barks, straggler despawn two beats behind, alert/run commands from mission events, and killed-id persistence
    /// (<see cref="ISaveable"/> "Encounters": killed enemies stay dead after a checkpoint reload, open waves reset).
    /// Raises <see cref="GameEvents.EncounterActivated"/>, <see cref="GameEvents.WaveSpawned"/> and
    /// <see cref="GameEvents.EncounterCleared"/>. Lives on PF_AIServices in S_Persistent.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.AI - 10)]
    [DisallowMultipleComponent]
    public sealed class EncounterSpawner : MonoBehaviour, IEncounterService, ISaveable, ISpawnHost
    {
        /// <summary>Encounter evaluation rate (5 Hz).</summary>
        public const float TickInterval = 0.2f;
        /// <summary>
        /// After a closet spawn has waited this long, the distance rule relaxes to <see cref="RelaxedClosetDistance"/>
        /// (never the sight rule) so a player camping next to a closet cannot soft-lock a beat (WP04 decision).
        /// </summary>
        public const float ClosetPatienceSeconds = 20f;
        /// <summary>Relaxed closet distance (m).</summary>
        public const float RelaxedClosetDistance = 10f;
        /// <summary>Dead enemies return to the pool after this long (WP05 fades corpses at 30 s).</summary>
        public const float CorpseSeconds = 40f;
        /// <summary>"Mennik!" from a Legion ally within this distance (DIALOGUE BK_LEG_07).</summary>
        public const float AllyKilledRadius = 15f;
        /// <summary>Wave-mates within this distance hear a contact shout.</summary>
        public const float AllyAlertRadius = 30f;
        /// <summary>Save key.</summary>
        public const string Key = "Encounters";

        [SerializeField, Tooltip("LIB_Enemies (EnemyPrefabBuilder). Empty = Libraries lookup, then runtime capsules.")]
        private EnemyLibrary _library;
        [SerializeField, Tooltip("Prewarm enemy pools when an encounter's section loads (TECH_PLAN §7.16).")]
        private bool _prewarmOnSectionLoad = true;

        private static EncounterSpawner s_instance;

        private readonly Dictionary<string, EncounterRuntime> _runtimes = new Dictionary<string, EncounterRuntime>();
        private readonly List<EncounterRuntime> _list = new List<EncounterRuntime>();
        private readonly Dictionary<string, EncounterDef> _extraDefs = new Dictionary<string, EncounterDef>();
        private readonly Dictionary<EnemyActor, EnemySlot> _byActor = new Dictionary<EnemyActor, EnemySlot>();
        private readonly List<EnemySlot> _alive = new List<EnemySlot>(16);
        private readonly List<EnemySlot> _corpses = new List<EnemySlot>(16);
        private readonly HashSet<string> _killed = new HashSet<string>();
        private readonly HashSet<EnemyActor> _hooked = new HashSet<EnemyActor>();
        private readonly Dictionary<string, GameObject> _templates = new Dictionary<string, GameObject>();
        private readonly List<string> _scratchIds = new List<string>();
        private Transform _templateRoot;
        private ServiceRef<IPlayerRig> _rig;
        private ServiceRef<ICheckpointService> _checkpoints;
        private ServiceRef<ITargetRegistry> _targets;
        private bool _registeredSave;
        private float _nextTick;

        /// <summary>The spawner in the scene, or null.</summary>
        public static EncounterSpawner Instance => s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_instance = null;

        /// <summary>Sets the enemy library (builders, dev arena).</summary>
        public void SetLibrary(EnemyLibrary library) => _library = library;

        /// <summary>Registers an extra encounter definition (dev arena, tests); table ids win on clashes.</summary>
        public void RegisterEncounter(EncounterDef def)
        {
            if (def != null && EncounterTable.Get(def.Id) == null) _extraDefs[def.Id] = def;
        }

        /// <summary>The runtime of an encounter, or null if never activated.</summary>
        public EncounterRuntime Runtime(string encounterId) =>
            encounterId != null && _runtimes.TryGetValue(encounterId, out var r) ? r : null;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                LSLog.Warn(LogCategory.AI, "Second EncounterSpawner destroyed", this);
                Destroy(this);
                return;
            }
            s_instance = this;
            GameServices.Register<IEncounterService>(this);
            AIDirector.Ensure();
        }

        private void OnDestroy()
        {
            if (s_instance != this) return;
            s_instance = null;
            GameServices.Unregister<IEncounterService>(this);
            _checkpoints.Value?.Unregister(this);
        }

        private void OnEnable()
        {
            GameEvents.MissionEvent += OnMissionEvent;
            GameEvents.TriggerEntered += OnTriggerEntered;
            GameEvents.Interacted += OnNamed;
            GameEvents.DestructibleDestroyed += OnNamed;
            GameEvents.VOLineStarted += OnLineStarted;
            GameEvents.SectionLoaded += OnSectionLoaded;
            GameEvents.SectionUnloaded += OnSectionUnloaded;
            GameEvents.BeatStarted += OnBeatStarted;
        }

        private void OnDisable()
        {
            GameEvents.MissionEvent -= OnMissionEvent;
            GameEvents.TriggerEntered -= OnTriggerEntered;
            GameEvents.Interacted -= OnNamed;
            GameEvents.DestructibleDestroyed -= OnNamed;
            GameEvents.VOLineStarted -= OnLineStarted;
            GameEvents.SectionLoaded -= OnSectionLoaded;
            GameEvents.SectionUnloaded -= OnSectionUnloaded;
            GameEvents.BeatStarted -= OnBeatStarted;
        }

        private void Update()
        {
            if (!_registeredSave && _checkpoints.Value != null)
            {
                _checkpoints.Value.Register(this);
                _registeredSave = true;
            }
            var now = Time.time;
            if (now < _nextTick) return;
            _nextTick = now + TickInterval;
            for (var i = 0; i < _list.Count; i++)
                if (_list[i].Active) _list[i].Tick(now);
            for (var i = _corpses.Count - 1; i >= 0; i--)
            {
                var s = _corpses[i];
                if (now - s.DiedAt < CorpseSeconds) continue;
                _corpses.RemoveAt(i);
                ReturnToPool(s);
            }
        }

        // ------------------------------------------------------------------ IEncounterService

        /// <inheritdoc/>
        public void Activate(string encounterId)
        {
            var r = GetOrCreate(encounterId);
            if (r == null || r.Active) return;
            r.Activate(Time.time);
            GameEvents.RaiseEncounterActivated(encounterId);
        }

        /// <inheritdoc/>
        public void TriggerWave(string encounterId, string wave)
        {
            var r = GetOrCreate(encounterId);
            if (r == null) return;
            if (!r.Active) Activate(encounterId);
            r.StartWave(wave, Time.time, true);
            r.Tick(Time.time);
        }

        /// <inheritdoc/>
        public void Deactivate(string encounterId)
        {
            var r = Runtime(encounterId);
            if (r == null) return;
            r.Deactivate();
            for (var i = _corpses.Count - 1; i >= 0; i--)
            {
                if (_corpses[i].Encounter != r) continue;
                ReturnToPool(_corpses[i]);
                _corpses.RemoveAt(i);
            }
        }

        /// <inheritdoc/>
        public int Alive(string encounterId, string wave = null) => Runtime(encounterId)?.Alive(wave) ?? 0;

        /// <inheritdoc/>
        public int Killed(string encounterId) => Runtime(encounterId)?.Killed ?? 0;

        /// <inheritdoc/>
        public bool Cleared(string encounterId)
        {
            var r = Runtime(encounterId);
            return r != null && r.ClearedRaised;
        }

        /// <inheritdoc/>
        public ITargetable Actor(string enemyId)
        {
            for (var i = 0; i < _alive.Count; i++)
                if (_alive[i].Def.Id == enemyId && _alive[i].Instance != null) return _alive[i].Instance;
            return null;
        }

        /// <inheritdoc/>
        public int AliveTotal => _alive.Count;

        /// <summary>
        /// Detection meter 0..1 of a living enemy (the Beat 2 sentry HUD icon, GDD §14.2); 0 when unknown or dead.
        /// Requested as <c>IEncounterService.Awareness01</c> in Docs/CORE_CHANGES.md so WP08 can read it.
        /// </summary>
        public float Awareness01(string enemyId)
        {
            for (var i = 0; i < _alive.Count; i++)
            {
                var s = _alive[i];
                if (s.Def.Id == enemyId && s.Instance != null) return s.Instance.Brain != null ? s.Instance.Brain.Suspicion01 : 0f;
            }
            return 0f;
        }

        /// <summary>Removes one living enemy without killing it (the marksman at s ≥ 2.0).</summary>
        public void DespawnEnemy(string enemyId)
        {
            for (var i = 0; i < _alive.Count; i++)
            {
                var s = _alive[i];
                if (s.Def.Id != enemyId) continue;
                Despawn(s);
                s.Encounter.NotifyDespawned(s);
                return;
            }
        }

        private EncounterRuntime GetOrCreate(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_runtimes.TryGetValue(id, out var r)) return r;
            var def = EncounterTable.Get(id);
            if (def == null) _extraDefs.TryGetValue(id, out def);
            if (def == null)
            {
                LSLog.ErrorOnce("enc-unknown:" + id, LogCategory.AI, "Unknown encounter '" + id + "'.");
                return null;
            }
            r = new EncounterRuntime(def, this, _killed);
            _runtimes[id] = r;
            _list.Add(r);
            return r;
        }

        // ------------------------------------------------------------------ ISpawnHost

        /// <inheritdoc/>
        public SpawnResult TrySpawn(EncounterRuntime encounter, EnemySlot slot, float now)
        {
            var def = slot.Def;
            var variantId = !string.IsNullOrEmpty(def.Variant) ? def.Variant : EnemyVariants.DefaultFor(def.Archetype, def.Id);
            var variant = EnemyVariants.Get(variantId) ?? EnemyVariants.Get(EnemyVariants.DefaultFor(def.Archetype, def.Id));
            var reg = MarkerRegistry.Instance;

            var position = def.Position;
            var rotation = Quaternion.Euler(0f, def.Heading, 0f);
            var perchTag = PerchTag.Ground;
            Vector3? perchPosition = null;
            ISplineVehicle vehicle = null;
            Transform seat = null;

            if (!string.IsNullOrEmpty(def.MarkerId) && reg.TryGet(def.MarkerId, out SceneMarker marker)) position = marker.Position;
            if (!string.IsNullOrEmpty(def.PerchId) && reg.TryGet(def.PerchId, out PerchNode perch))
            {
                perchTag = perch.Tag;
                if (def.Kind == SpawnKind.Closet) perchPosition = perch.Position;
                else position = perch.Position;
            }
            else if (def.Kind == SpawnKind.Vehicle || def.Archetype == ArchetypeId.TechnicalGunner) perchTag = PerchTag.Vehicle;
            else if (def.IsStatic && position.y > 1.5f) perchTag = PerchTag.Rooftop;

            switch (def.Kind)
            {
                case SpawnKind.Closet:
                {
                    var r = ResolveCloset(slot, now, ref position, ref rotation);
                    if (r != SpawnResult.Spawned) return r;
                    if (perchPosition == null && def.IsStatic) perchPosition = def.Position;
                    break;
                }
                case SpawnKind.Vehicle:
                    if (reg.TryGet(def.VehicleId, out SceneMarker vm)) vehicle = vm.GetComponent<ISplineVehicle>();
                    if (vehicle != null)
                    {
                        seat = def.Seat >= 0 && def.Seat < vehicle.SeatCount ? vehicle.GetSeat(def.Seat) : null;
                        var anchor = seat != null ? seat : vehicle.Root;
                        position = anchor.position;
                        rotation = anchor.rotation;
                    }
                    else LSLog.WarnOnce("enc-novehicle:" + def.VehicleId, LogCategory.AI,
                        "Vehicle '" + def.VehicleId + "' not found: " + def.Id + " spawns at its tailgate position instead.");
                    break;
            }

            var instance = Acquire(variant, position, rotation);
            if (instance == null) return SpawnResult.Failed;
            var actor = instance.GetComponent<EnemyActor>();
            var brain = instance.GetComponent<EnemyBrain>();
            if (actor == null || brain == null)
            {
                LSLog.ErrorOnce("enc-badprefab:" + variantId, LogCategory.AI, "Enemy prefab for '" + variantId + "' has no EnemyActor/EnemyBrain.", instance);
                Pools.Despawn(instance);
                return SpawnResult.Failed;
            }
            actor.Setup(variant.Id, def.Archetype);
            // Closet perchers walk out onto their perch on the NavMesh; everything else static keeps its agent off.
            var isStatic = def.Kind != SpawnKind.Closet && (def.IsStatic || def.Kind == SpawnKind.Vehicle);
            actor.Prepare(def.Id, position, rotation, perchTag, isStatic);

            var ctx = new EnemySpawnContext
            {
                Encounter = encounter.Def,
                Wave = slot.Wave,
                Def = def,
                Alerted = slot.Alerted,
                SpawnTime = now,
                Position = position,
                Rotation = rotation,
                PerchPosition = perchPosition,
                Perch = perchTag,
                Vehicle = vehicle,
                Seat = seat,
                Route = ResolveRoute(def),
                SuppressPosition = !string.IsNullOrEmpty(def.SuppressMarkerId) && reg.TryGet(def.SuppressMarkerId, out SceneMarker sm) ? sm.Position : (Vector3?)null,
                EncounterTime = encounter.ActivatedAt,
            };
            slot.Instance = actor;
            _byActor[actor] = slot;
            _alive.Add(slot);
            Hook(actor, brain);
            brain.Configure(ctx);
            Pair(encounter, slot, brain);
            if (def.Kind == SpawnKind.Closet && ArchetypeTable.Get(def.Archetype).LegionBarks)
                brain.Barker.Bark(BarkIds.LegHurry, actor.transform, actor.Team, now, 6f);              // reinforcements running in
            return SpawnResult.Spawned;
        }

        private SpawnResult ResolveCloset(EnemySlot slot, float now, ref Vector3 position, ref Quaternion rotation)
        {
            var def = slot.Def;
            var reg = MarkerRegistry.Instance;
            reg.TryGet(def.ClosetId, out SpawnCloset closet);
            if (closet == null)
                LSLog.WarnOnce("enc-nocloset:" + def.ClosetId, LogCategory.AI, "Spawn closet '" + def.ClosetId + "' not registered: " + def.Id + " uses its table position (sight rule still applies).");
            var eye = PlayerEye(out var hasPlayer);
            if (!hasPlayer)
            {
                if (closet != null)
                {
                    var p0 = closet.SpawnPoint(slot.Order);
                    position = p0.position;
                    rotation = p0.rotation;
                }
                return SpawnResult.Spawned;
            }
            var director = AIDirector.Instance;
            var patience = !float.IsPositiveInfinity(slot.WaitingSince) && now - slot.WaitingSince >= ClosetPatienceSeconds;
            var minDistance = closet != null ? closet.MinPlayerDistance : SpawnRules.ClosetMinDistance;
            if (patience)
            {
                minDistance = Mathf.Min(minDistance, RelaxedClosetDistance);
                LSLog.WarnOnce("enc-closet-relax:" + def.Id, LogCategory.AI, def.Id + ": closet blocked for " + ClosetPatienceSeconds + " s; distance rule relaxed to " + RelaxedClosetDistance + " m (still out of sight).");
            }
            var count = closet != null ? closet.SpawnPointCount : 1;
            var door = closet != null ? closet.Door.position + Vector3.up * 1.2f : def.Position + Vector3.up * 1.2f;
            for (var k = 0; k < count; k++)
            {
                var t = closet != null ? closet.SpawnPoint(slot.Order + k) : null;
                var p = t != null ? t.position : def.Position;
                var distance = Vector3.Distance(eye, p);
                if (distance < minDistance) continue;
                if (director != null && !director.TryUseRays(2)) return SpawnResult.Wait;          // TECH_PLAN §7.6 ray budget
                var doorVisible = !Physics.Linecast(eye, door, LayerIds.AISight, QueryTriggerInteraction.Ignore);
                var pointVisible = !Physics.Linecast(eye, p + Vector3.up * 1.4f, LayerIds.AISight, QueryTriggerInteraction.Ignore);
                if (!SpawnRules.ClosetAllowed(distance, minDistance, doorVisible, pointVisible)) continue;
                position = p;
                rotation = t != null ? t.rotation : rotation;
                return SpawnResult.Spawned;
            }
            return SpawnResult.Wait;
        }

        private Vector3 PlayerEye(out bool hasPlayer)
        {
            var rig = _rig.Value;
            if (rig != null && rig.Eye != null)
            {
                hasPlayer = true;
                return rig.Eye.position;
            }
            var p = _targets.Value?.Player;
            if (p != null && p.Root != null)
            {
                hasPlayer = true;
                return p.AimPoint != null ? p.AimPoint.position + Vector3.up * 0.3f : p.Root.position + Vector3.up * 1.6f;
            }
            hasPlayer = false;
            return Vector3.zero;
        }

        private static Vector3[] ResolveRoute(EnemySpawnDef def)
        {
            if (!string.IsNullOrEmpty(def.FlankRouteId) && MarkerRegistry.Instance.TryGet(def.FlankRouteId, out FlankRoute route) && route.Points.Count > 0)
            {
                var pts = new Vector3[route.Points.Count];
                for (var i = 0; i < pts.Length; i++) pts[i] = route.Points[i];
                return pts;
            }
            return def.Route;
        }

        private void Pair(EncounterRuntime encounter, EnemySlot slot, EnemyBrain brain)
        {
            var pairId = slot.Def.PairId;
            if (string.IsNullOrEmpty(pairId)) return;
            var slots = encounter.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                var other = slots[i];
                if (other == slot || other.Def.PairId != pairId || other.State != SlotState.Alive || other.Instance == null) continue;
                var ob = other.Instance.Brain;
                if (ob == null) continue;
                ob.SetPartner(brain);
                brain.SetPartner(ob);
                return;
            }
        }

        private GameObject Acquire(EnemyVariant variant, Vector3 position, Quaternion rotation)
        {
            var prefab = PrefabFor(variant);
            if (prefab == null) return null;
            var go = Pools.Spawn(prefab, position, rotation);
            if (go != null && !go.activeSelf) go.SetActive(true);
            return go;
        }

        private GameObject PrefabFor(EnemyVariant variant)
        {
            if (variant == null) return null;
            var lib = _library;
            if (lib == null && Libraries.TryGet(out EnemyLibrary found)) lib = _library = found;
            var prefab = lib != null ? lib.PrefabFor(variant.Id) : null;
            if (prefab != null) return prefab;
            if (_templates.TryGetValue(variant.Id, out var template) && template != null) return template;
            LSLog.MissingAsset(LogCategory.AI, "LIB_Enemies/" + variant.Id, EnemyLibrary.AssetPath, this);
            if (_templateRoot == null)
            {
                _templateRoot = new GameObject("[EnemyTemplates]").transform;
                _templateRoot.SetParent(transform, false);
            }
            template = EnemyFactory.BuildRuntimeTemplate(variant, _templateRoot);
            _templates[variant.Id] = template;
            return template;
        }

        private void Hook(EnemyActor actor, EnemyBrain brain)
        {
            if (!_hooked.Add(actor)) return;
            actor.Died += OnEnemyDied;
            brain.CombatEntered += OnCombatEntered;
        }

        /// <inheritdoc/>
        public void Despawn(EnemySlot slot)
        {
            if (slot?.Instance == null) return;
            _alive.Remove(slot);
            ReturnToPool(slot);
        }

        private void ReturnToPool(EnemySlot slot)
        {
            var actor = slot.Instance;
            slot.Instance = null;
            if (actor == null) return;
            _byActor.Remove(actor);
            actor.Retire();
            Pools.Despawn(actor.gameObject);
        }

        /// <inheritdoc/>
        public bool PlayerInside(string triggerId) =>
            MarkerRegistry.Instance.TryGet(triggerId, out TriggerVolume t) && t.PlayerInside;

        /// <inheritdoc/>
        public void OnWaveSpawned(EncounterRuntime encounter, WaveDef wave) => GameEvents.RaiseWaveSpawned(encounter.Def.Id, wave.Id);

        /// <inheritdoc/>
        public void OnAlerted(EncounterRuntime encounter, EnemySlot slot) => slot.Instance?.Brain?.AlertToPlayer(Time.time, null);

        /// <inheritdoc/>
        public void OnMoraleBreak(EncounterRuntime encounter, WaveDef wave)
        {
            var now = Time.time;
            var barked = false;
            for (var i = 0; i < _alive.Count; i++)
            {
                var s = _alive[i];
                if (s.Encounter != encounter || s.Def.Wave != wave.Id || s.Instance == null) continue;
                var brain = s.Instance.Brain;
                if (brain == null || !ArchetypeTable.Get(s.Def.Archetype).MoraleBreaks) continue;
                if (!barked)
                {
                    barked = brain.Barker.Bark(BarkIds.LegFallBack, s.Instance.transform, s.Instance.Team, now, 10f, true);   // "Dovvo!"
                }
                brain.FallBack(now);
            }
            GameEvents.RaiseMissionEvent(AIEvents.MoralePrefix + encounter.Def.Id + "." + wave.Id);
        }

        /// <inheritdoc/>
        public void OnCleared(EncounterRuntime encounter) => GameEvents.RaiseEncounterCleared(encounter.Def.Id);

        private void OnEnemyDied(EnemyActor actor, DamageInfo d)
        {
            if (!_byActor.TryGetValue(actor, out var slot)) return;
            var now = Time.time;
            _alive.Remove(slot);
            _killed.Add(slot.Def.Id);
            slot.Encounter.NotifyDeath(slot, now);
            _corpses.Add(slot);
            // "Mennik!" from the nearest living Legion within 15 m (DIALOGUE BK_LEG_07).
            EnemyActor nearest = null;
            var best = AllyKilledRadius * AllyKilledRadius;
            for (var i = 0; i < _alive.Count; i++)
            {
                var a = _alive[i].Instance;
                if (a == null || a.Team != Team.Legion || !a.IsAlive) continue;
                var dd = (a.transform.position - actor.transform.position).sqrMagnitude;
                if (dd > best) continue;
                best = dd;
                nearest = a;
            }
            if (nearest != null && nearest.Brain != null) nearest.Brain.Barker.Bark(BarkIds.LegManDown, nearest.transform, nearest.Team, now, 6f);
        }

        private void OnCombatEntered(EnemyBrain brain)
        {
            if (!_byActor.TryGetValue(brain.Actor, out var slot)) return;
            var target = brain.Target;
            var lkp = target?.Root != null ? target.Root.position : brain.transform.position + brain.transform.forward * 10f;
            var now = Time.time;
            var r2 = AllyAlertRadius * AllyAlertRadius;
            for (var i = 0; i < _alive.Count; i++)
            {
                var s = _alive[i];
                if (s == slot || s.Encounter != slot.Encounter || s.Def.Wave != slot.Def.Wave || s.Instance == null) continue;
                if ((s.Instance.transform.position - brain.transform.position).sqrMagnitude > r2) continue;
                s.Instance.Brain?.AlertFromAlly(target, lkp, now);
            }
        }

        // ------------------------------------------------------------------ events

        private void OnMissionEvent(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (AIEvents.TryParseAlert(id, out var enc, out var wave))
            {
                var r = Runtime(enc);
                if (r != null) r.Alert(wave);
                return;
            }
            if (AIEvents.TryParseRun(id, out var enemyId))
            {
                RunCommand(enemyId);
                return;
            }
            OnNamed(id);
        }

        private void OnTriggerEntered(string id, Collider c)
        {
            if (TriggerVolume.IsPlayer(c)) OnNamed(id);
        }

        private void OnLineStarted(string lineId, string speaker)
        {
            if (_list.Count == 0 || string.IsNullOrEmpty(lineId)) return;
            OnNamed(AIEvents.Vo(lineId));
        }

        private void OnNamed(string id)
        {
            var now = Time.time;
            for (var i = 0; i < _list.Count; i++) _list[i].RecordEvent(id, now);
        }

        private void RunCommand(string enemyId)
        {
            for (var i = 0; i < _alive.Count; i++)
            {
                var s = _alive[i];
                if (s.Def.Id != enemyId || s.Instance == null) continue;
                var brain = s.Instance.Brain;
                if (brain == null) return;
                var from = s.Instance.transform.position;
                var dest = from + Vector3.forward * 25f;                                               // "runs north" (GDD §7.2)
                var group = s.Encounter.Def.GetWave("Gate")?.CoverGroup;
                var nodes = AIDirector.Instance != null ? AIDirector.Instance.CoverNodes : null;
                var bestD = float.MaxValue;
                if (nodes != null && !string.IsNullOrEmpty(group))
                {
                    for (var k = 0; k < nodes.Count; k++)
                    {
                        var n = nodes[k];
                        if (n == null || n.Group != group) continue;
                        var d = (n.Position - from).sqrMagnitude;
                        if (d >= bestD) continue;
                        bestD = d;
                        dest = n.Position;
                    }
                }
                brain.RunTo(dest, BarkIds.LegContact);
                return;
            }
        }

        private void OnSectionLoaded(SectionId section)
        {
            if (!_prewarmOnSectionLoad) return;
            var all = EncounterTable.All;
            for (var i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def.Section != section) continue;
                _scratchIds.Clear();
                for (var k = 0; k < def.Enemies.Count; k++)
                {
                    var e = def.Enemies[k];
                    _scratchIds.Add(!string.IsNullOrEmpty(e.Variant) ? e.Variant : EnemyVariants.DefaultFor(e.Archetype, e.Id));
                }
                _scratchIds.Sort(System.StringComparer.Ordinal);
                for (var k = 0; k < _scratchIds.Count;)
                {
                    var id = _scratchIds[k];
                    var n = 0;
                    while (k < _scratchIds.Count && _scratchIds[k] == id)
                    {
                        n++;
                        k++;
                    }
                    var prefab = PrefabFor(EnemyVariants.Get(id));
                    if (prefab != null) Pools.Prewarm(prefab, Mathf.Min(n, def.MaxAlive));
                }
            }
        }

        private void OnSectionUnloaded(SectionId section)
        {
            for (var i = 0; i < _list.Count; i++)
                if (_list[i].Active && _list[i].Def.Section == section) Deactivate(_list[i].Def.Id);
        }

        private void OnBeatStarted(int beat)
        {
            // GDD §14.3: stragglers left two beats behind are despawned.
            for (var i = 0; i < _list.Count; i++)
            {
                var r = _list[i];
                if (r.Active && r.Def.Beat <= beat - 2) Deactivate(r.Def.Id);
            }
        }

        // ------------------------------------------------------------------ ISaveable

        /// <inheritdoc/>
        public string SaveKey => Key;

        /// <inheritdoc/>
        public void Capture(SaveWriter w) => w.WriteStrings("killed", _killed);

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            DespawnAll();
            _scratchIds.Clear();
            r.ReadStrings("killed", _scratchIds);
            _killed.Clear();
            for (var i = 0; i < _scratchIds.Count; i++) _killed.Add(_scratchIds[i]);
            _scratchIds.Clear();
        }

        /// <summary>Despawns every enemy and forgets every runtime (checkpoint reload; killed ids are kept).</summary>
        public void DespawnAll()
        {
            for (var i = _alive.Count - 1; i >= 0; i--) ReturnToPool(_alive[i]);
            _alive.Clear();
            for (var i = 0; i < _corpses.Count; i++) ReturnToPool(_corpses[i]);
            _corpses.Clear();
            _runtimes.Clear();
            _list.Clear();
            AIDirector.Instance?.Tokens.Clear();
            AIDirector.Instance?.Claims.Clear();
        }
    }
}
