using LongShadow.AI.Enemy;
using UnityEngine;

namespace LongShadow.AI.Encounters
{
    /// <summary>
    /// One enemy of an encounter (a row of a GDD §7 encounter table): id, archetype, position and facing, how it spawns,
    /// its wave and delay, and the behaviour notes turned into data (flank route, pair role, sentry, hold rules, perch,
    /// vehicle, B11 windows). Built fluently in <see cref="EncounterTable"/>.
    /// </summary>
    public sealed class EnemySpawnDef
    {
        /// <summary>Creates a definition.</summary>
        public EnemySpawnDef(string id, ArchetypeId archetype, Vector3 position, float heading)
        {
            Id = id;
            Archetype = archetype;
            Position = position;
            Heading = heading;
            MarkerId = id;
        }

        /// <summary>Enemy id ("E3-05"; also the ITargetable ActorId).</summary>
        public string Id { get; }
        /// <summary>Archetype.</summary>
        public ArchetypeId Archetype { get; }
        /// <summary>Table position (world metres, GDD §7).</summary>
        public Vector3 Position { get; }
        /// <summary>Compass facing in degrees (GDD §7 "Facing").</summary>
        public float Heading { get; }
        /// <summary>Appendix A marker that overrides the position when registered (defaults to the id: "E2-01").</summary>
        public string MarkerId { get; private set; }
        /// <summary>Prefab variant id (null = archetype default, see <see cref="EnemyVariants"/>).</summary>
        public string Variant { get; private set; }
        /// <summary>Spawn kind.</summary>
        public SpawnKind Kind { get; private set; } = SpawnKind.Placed;
        /// <summary>Spawn closet id ("SC_B3_N").</summary>
        public string ClosetId { get; private set; }
        /// <summary>Perch node id ("B3.Perch.Balcony"); perched enemies never roam.</summary>
        public string PerchId { get; private set; }
        /// <summary>Spline vehicle id ("B10.Truck1").</summary>
        public string VehicleId { get; private set; }
        /// <summary>Seat index on the vehicle.</summary>
        public int Seat { get; private set; }
        /// <summary>Stays on the vehicle (RPG on the truck bed).</summary>
        public bool StaysOnVehicle { get; private set; }
        /// <summary>Wave id.</summary>
        public string Wave { get; private set; } = "Main";
        /// <summary>Seconds after the wave starts.</summary>
        public float Delay { get; private set; }
        /// <summary>Queue order inside a wave when the cap holds spawns back (lower first; default 1).</summary>
        public int QueuePriority { get; private set; } = 1;
        /// <summary>Flank route marker id ("B3.Route.WaveB").</summary>
        public string FlankRouteId { get; private set; }
        /// <summary>Inline route when there is no marker (Beat 10 truck 1 push along z 412).</summary>
        public Vector3[] Route { get; private set; }
        /// <summary>Greycoat pair id.</summary>
        public string PairId { get; private set; }
        /// <summary>Pair role.</summary>
        public PairRole Role { get; private set; }
        /// <summary>Marker the suppressor fires from ("B4.SuppressPos").</summary>
        public string SuppressMarkerId { get; private set; }
        /// <summary>Beat 2 sentry: smoking idle, chatter barks, 30 HP while unaware, suspicious state.</summary>
        public bool Sentry { get; private set; }
        /// <summary>Events that release a hold (ambush): the enemy stays idle and blind until one happens.</summary>
        public string[] HoldUntil { get; private set; }
        /// <summary>Event after which the enemy targets the player instead of the squad (B4 marquee).</summary>
        public string PreferSquadUntil { get; private set; }
        /// <summary>Throws its grenade on arrival (E9 wave 2).</summary>
        public bool FragOnArrival { get; private set; }
        /// <summary>Night-vision variant (green lens glow tell, E10-04).</summary>
        public bool Nvg { get; private set; }
        /// <summary>Weapon override (B11 T3 carries an RPG).</summary>
        public string WeaponOverride { get; private set; }
        /// <summary>Drops its weapon (null = archetype default).</summary>
        public bool? DropWeapon { get; private set; }
        /// <summary>Beat 11 window start (exfil seconds).</summary>
        public float WindowStart { get; private set; } = -1f;
        /// <summary>Beat 11 window end (exfil seconds).</summary>
        public float WindowEnd { get; private set; } = -1f;
        /// <summary>Beat 11: hidden behind a parapet until this time (exfil seconds; -1 = never hidden).</summary>
        public float HiddenUntil { get; private set; } = -1f;
        /// <summary>Beat 11: fires its rocket at this time whatever happens (-1 = none).</summary>
        public float FireAt { get; private set; } = -1f;
        /// <summary>Holds this long after spawning before pushing (E6-08).</summary>
        public float HoldSeconds { get; private set; }
        /// <summary>Where to push after the hold (E6-08 cable drums).</summary>
        public Vector3? PushTo { get; private set; }
        /// <summary>Bark played when the hold starts (E6-08 BK_GRY_03).</summary>
        public string HoldBark { get; private set; }
        /// <summary>Preferred cover group.</summary>
        public string CoverGroup { get; private set; }
        /// <summary>Design notes (GDD text).</summary>
        public string Notes { get; private set; }

        /// <summary>True for enemies that never use the NavMesh (perch, vehicle bed, static archetypes).</summary>
        public bool IsStatic => Kind == SpawnKind.Perch || PerchId != null || StaysOnVehicle || !ArchetypeTable.Get(Archetype).UsesNavMesh;

        // ---- fluent setup (EncounterTable)
        /// <summary>Placed (default).</summary>
        public EnemySpawnDef Placed() { Kind = SpawnKind.Placed; return this; }
        /// <summary>Closet spawn.</summary>
        public EnemySpawnDef Closet(string closetId) { Kind = SpawnKind.Closet; ClosetId = closetId; return this; }
        /// <summary>Perched (keeps a closet spawn if one was set, then moves onto the perch).</summary>
        public EnemySpawnDef Perch(string perchId = null)
        {
            if (Kind != SpawnKind.Closet) Kind = SpawnKind.Perch;
            PerchId = perchId;
            return this;
        }
        /// <summary>Rides a vehicle seat.</summary>
        public EnemySpawnDef Vehicle(string vehicleId, int seat, bool staysOnVehicle = false)
        {
            Kind = SpawnKind.Vehicle;
            VehicleId = vehicleId;
            Seat = seat;
            StaysOnVehicle = staysOnVehicle;
            return this;
        }
        /// <summary>Wave and delay.</summary>
        public EnemySpawnDef InWave(string wave, float delay = 0f) { Wave = wave; Delay = delay; return this; }
        /// <summary>Queue priority.</summary>
        public EnemySpawnDef Priority(int p) { QueuePriority = p; return this; }
        /// <summary>Marker id override.</summary>
        public EnemySpawnDef AtMarker(string markerId) { MarkerId = markerId; return this; }
        /// <summary>Prefab variant.</summary>
        public EnemySpawnDef AsVariant(string variant) { Variant = variant; return this; }
        /// <summary>Flank route marker.</summary>
        public EnemySpawnDef Flank(string routeId) { FlankRouteId = routeId; return this; }
        /// <summary>Inline route.</summary>
        public EnemySpawnDef Along(params Vector3[] points) { Route = points; return this; }
        /// <summary>Greycoat pair.</summary>
        public EnemySpawnDef Pair(string pairId, PairRole role, string suppressMarkerId = null)
        {
            PairId = pairId;
            Role = role;
            SuppressMarkerId = suppressMarkerId;
            return this;
        }
        /// <summary>Beat 2 sentry.</summary>
        public EnemySpawnDef AsSentry() { Sentry = true; return this; }
        /// <summary>Ambush hold.</summary>
        public EnemySpawnDef HoldUntilEvent(params string[] events) { HoldUntil = events; return this; }
        /// <summary>Targets the squad until the event.</summary>
        public EnemySpawnDef SquadFirstUntil(string eventId) { PreferSquadUntil = eventId; return this; }
        /// <summary>Frag on arrival.</summary>
        public EnemySpawnDef FragFirst() { FragOnArrival = true; return this; }
        /// <summary>NVG variant.</summary>
        public EnemySpawnDef WithNvg() { Nvg = true; return this; }
        /// <summary>Weapon override.</summary>
        public EnemySpawnDef Weapon(string weaponId) { WeaponOverride = weaponId; return this; }
        /// <summary>Weapon drop override.</summary>
        public EnemySpawnDef Drops(bool drops) { DropWeapon = drops; return this; }
        /// <summary>Beat 11 active window.</summary>
        public EnemySpawnDef Window(float start, float end) { WindowStart = start; WindowEnd = end; return this; }
        /// <summary>Beat 11 hidden-until and fire-at times.</summary>
        public EnemySpawnDef HiddenUntilThenFire(float hiddenUntil, float fireAt) { HiddenUntil = hiddenUntil; FireAt = fireAt; return this; }
        /// <summary>Hold then push.</summary>
        public EnemySpawnDef HoldThenPush(float seconds, Vector3 pushTo, string bark = null)
        {
            HoldSeconds = seconds;
            PushTo = pushTo;
            HoldBark = bark;
            return this;
        }
        /// <summary>Preferred cover group.</summary>
        public EnemySpawnDef CoverIn(string group) { CoverGroup = group; return this; }
        /// <summary>Notes.</summary>
        public EnemySpawnDef Note(string notes) { Notes = notes; return this; }
    }
}
