using System;
using LongShadow.Audio.Core;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Editor
{
    /// <summary>
    /// The mix design of every non-VO cue (TECH_PLAN §7.9, D9; GDD §10.2; WP06_SFX import notes): category, 2D/3D,
    /// rolloff distances, level, randomisation, stealing priority, the speed-of-sound delay and the near/far crossfade.
    /// Distances follow the physical size of the source (a 7.62 mm rifle carries ~250 m before the baked far take takes
    /// over, a grenade ~400 m, the curfew siren across the whole town, booth foley ~8 m). Folder defaults first, then
    /// per-prefix overrides; the AudioLibraryBuilder writes the result into LIB_Audio. Pure (unit-tested).
    /// </summary>
    public static class CueDefaults
    {
        /// <summary>Settings of a cue id found in <paramref name="group"/> (<c>SFX/Weapons</c>, <c>Music</c>, <c>Ambience</c>…).</summary>
        public static CueSettings Resolve(string cueId, string group)
        {
            var id = cueId ?? "";
            var s = GroupDefaults(group ?? "");
            Override(id, ref s);
            return s;
        }

        private static CueSettings GroupDefaults(string group)
        {
            if (group == "Ambience")
            {
                var a = CueSettings.Effect3D(1f, 50f, 10);
                a.Category = AudioCategories.Ambience;
                a.SpatialBlend = 0f;
                a.Volume = 0.8f;
                a.VolumeJitterDb = 0f;
                a.PitchJitter = 0f;
                return a;
            }
            if (group == "Music")
            {
                var m = CueSettings.Effect3D(1f, 50f, 100);
                m.Category = AudioCategories.Music;
                m.SpatialBlend = 0f;
                m.VolumeJitterDb = 0f;
                m.PitchJitter = 0f;
                return m;
            }
            switch (CueNaming.SfxGroup(group))
            {
                case "Weapons": return CueSettings.Effect3D(2f, 40f, 60);
                case "Impacts":
                {
                    var s = CueSettings.Effect3D(1.5f, 45f, 40);
                    s.VolumeJitterDb = 2f;
                    s.PitchJitter = 0.06f;
                    s.MaxInstances = 12;
                    return s;
                }
                case "Foley": return CueSettings.Effect3D(1f, 18f, 45);
                case "Footsteps":
                {
                    // WP06_SFX: "walk at volume 1.0, sprint +2 dB" (the caller passes the level).
                    var s = CueSettings.Effect3D(1f, 25f, 30);
                    s.VolumeJitterDb = 1.5f;
                    s.PitchJitter = 0.04f;
                    s.MaxInstances = 10;
                    return s;
                }
                case "Explosions":
                {
                    // Manifest note: "3D, big min distance ~10 m, max ~400 m"; baked _FAR takes beyond 120 m.
                    var s = CueSettings.Effect3D(10f, 400f, 95);
                    s.SpeedOfSound = true;
                    s.PitchJitter = 0.04f;
                    s.FarMinDistance = 40f;
                    s.FarMaxDistance = 1800f;
                    return s;
                }
                case "Whizz": return CueSettings.Effect3D(2f, 40f, 90);
                case "Synth": return CueSettings.Effect3D(1f, 20f, 70);
                case "Vehicles": return CueSettings.Effect3D(4f, 150f, 60);
                case "UI":
                {
                    var s = CueSettings.Effect3D(1f, 10f, 70);
                    s.Category = AudioCategories.UI;
                    s.SpatialBlend = 0f;
                    s.VolumeJitterDb = 0.5f;
                    s.PitchJitter = 0.01f;
                    s.MaxInstances = 4;
                    return s;
                }
                default: // SetPieces and unknown groups
                {
                    var s = CueSettings.Effect3D(2f, 40f, 60);
                    s.PitchJitter = 0.02f;
                    return s;
                }
            }
        }

        private static void Override(string id, ref CueSettings s)
        {
            // ---- Music folder: diegetic radio sounds are voices, not score.
            if (id == "INTERVAL_SIGNAL") { Voice3D(ref s, 0.8f, 25f); return; }          // from Tinker's PRC-77 (G9)
            if (id == "INTERVAL_SIGNAL_ROOM_SPEAKER" || id == "NUM_BASEMENT_LOOP") { Voice3D(ref s, 2f, 35f); return; } // basement speaker
            if (id == "INTERVAL_SIGNAL_HF_AM") { Voice3D(ref s, 1f, 10f); s.SpatialBlend = 0f; return; } // on the net, 2D

            // ---- weapons
            // Weapon layers stay at volume 1.0: WP06_SFX balanced transient + body + mech + tail for that (worst case
            // ≤ −1.2 dBTP; Docs/REQUESTS.md 13:00). Scale the stack with the mix, never per layer.
            if (Starts(id, "SFX_WPN_TAIL_")) { Set(ref s, 10f, 400f, 55); s.PitchJitter = 0.02f; s.VolumeJitterDb = 0.5f; return; }
            if (Starts(id, "SFX_WPN_BODY_")) { Set(ref s, 6f, 150f, 70); s.VolumeJitterDb = 0.5f; return; }
            if (Starts(id, "SFX_WPN_SHELLS")) { Set(ref s, 0.5f, 12f, 20); s.Volume = 0.6f; s.PitchJitter = 0.06f; s.MaxInstances = 6; return; }
            if (Starts(id, "SFX_WPN_HANDLING")) { Set(ref s, 1f, 12f, 75); s.VolumeJitterDb = 0.5f; return; }
            if (Starts(id, "SFX_WPN_") && id.IndexOf("_RELOAD", StringComparison.Ordinal) >= 0)
            {
                Set(ref s, 1f, 15f, 75);
                s.VolumeJitterDb = 0.5f;
                s.PitchJitter = 0.015f;
                return;
            }
            if (Starts(id, "SFX_WPN_") && Ends(id, "_MECH")) { Set(ref s, 1f, 15f, 85); s.VolumeJitterDb = 0.5f; return; }
            if (id == "SFX_WPN_M60D_OVERHEAT") { Set(ref s, 1f, 15f, 80); s.PitchJitter = 0.01f; return; }
            if (Starts(id, "SFX_WPN_") && Ends(id, "_SHOT"))
            {
                // AI near layer carries ~250 m; the baked _FAR take crossfades in between 40 and 120 m (TECH_PLAN §7.9).
                Set(ref s, 6f, 250f, 80);
                s.SpeedOfSound = true;
                s.VolumeJitterDb = 1f; // ±1 dB keeps the stack's −1.2 dBTP headroom below full scale
                s.PitchJitter = 0.03f;
                s.FarCrossfade = new Vector2(AudioConstants.WeaponNearDistance, AudioConstants.WeaponFarDistance);
                s.FarMinDistance = 30f;
                s.FarMaxDistance = 1500f;
                return;
            }
            if (id == "SFX_RPG_LAUNCH") { Set(ref s, 8f, 400f, 90); s.SpeedOfSound = true; return; }
            if (id == "SFX_RPG_FLYBY") { Set(ref s, 5f, 150f, 85); return; }
            if (Starts(id, "SFX_GREN_")) { Set(ref s, 1.5f, 30f, 70); return; }
            if (Starts(id, "SFX_KNIFE_")) { Set(ref s, 1f, 12f, 75); return; }

            // ---- bullets, impacts, explosions
            if (id == "SFX_BULLET_CRACK") { Set(ref s, 2f, 40f, 92); s.VolumeJitterDb = 2f; s.PitchJitter = 0.05f; s.MaxInstances = 6; return; }
            if (id == "SFX_BULLET_WHIZ") { Set(ref s, 1f, 15f, 88); s.MaxInstances = 4; return; }
            if (id == "SFX_IMPACT_FLESH") { s.MaxDistance = 25f; return; }
            if (id == "SFX_EXP_DEBRIS") { Set(ref s, 4f, 80f, 70); s.SpeedOfSound = true; return; }
            if (id == "SFX_EXP_BREACH") { s.MinDistance = 12f; s.MaxDistance = 600f; return; }

            // ---- movement foley
            if (id == "SFX_BODYFALL") { Set(ref s, 2f, 30f, 45); return; }
            if (Starts(id, "SFX_MOVE_")) { Set(ref s, 1f, 15f, 50); return; }
            if (id == "SFX_FOLEY_GEAR") { Set(ref s, 0.5f, 8f, 40); s.Volume = 0.5f; return; }

            // ---- radio and synth
            if (Starts(id, "SFX_RADIO_CLICKS")) { Set(ref s, 1f, 25f, 75); return; }
            if (Starts(id, "SFX_RADIO_KEYUP") || Starts(id, "SFX_RADIO_RELEASE")) { Set(ref s, 0.5f, 10f, 70); return; }
            if (id == "SFX_RADIO_STATIC_LOOP") { Set(ref s, 1.5f, 25f, 70); return; }
            if (id == "SFX_TINNITUS") { Set(ref s, 1f, 10f, 100); s.SpatialBlend = 0f; s.VolumeJitterDb = 0f; s.PitchJitter = 0f; return; }
            if (id == "SFX_HEARTBEAT") { Set(ref s, 1f, 10f, 90); s.SpatialBlend = 0f; s.Volume = 0.9f; s.VolumeJitterDb = 0f; s.PitchJitter = 0f; return; }

            // ---- set pieces with a big footprint
            if (id == "SFX_CURFEW_SIREN") { Set(ref s, 60f, 2500f, 75); s.SpeedOfSound = true; s.VolumeJitterDb = 0f; s.PitchJitter = 0.01f; return; }
            if (Starts(id, "SFX_ROTOR_")) { Set(ref s, 15f, 900f, 85); s.PitchJitter = 0.01f; return; }
            if (id == "SFX_DOWNWASH_SAND") { Set(ref s, 5f, 80f, 80); return; }
            if (id == "SFX_CINE_RAMP") { Set(ref s, 1f, 10f, 85); s.SpatialBlend = 0f; s.VolumeJitterDb = 0f; s.PitchJitter = 0f; return; }
            if (id == "SFX_HELI_CABIN_LOOP") { Set(ref s, 2f, 20f, 90); s.SpatialBlend = 0.3f; s.PitchJitter = 0f; return; }
            if (id == "SFX_HELI_ALARM") { Set(ref s, 0.5f, 8f, 85); s.PitchJitter = 0f; return; }
            if (Starts(id, "SFX_TRUCK_")) { Set(ref s, 5f, 160f, 65); return; }
            if (id == "SFX_FLOOD_SLAM_ON") { Set(ref s, 12f, 250f, 75); s.SpeedOfSound = true; return; }
            if (id == "SFX_FLOOD_DIEOFF") { Set(ref s, 6f, 150f, 70); return; }
            if (id == "SFX_FLARE") { Set(ref s, 12f, 700f, 70); s.SpeedOfSound = true; return; }
            if (id == "SFX_TUBE_RACK_SHATTER") { Set(ref s, 3f, 60f, 80); return; }

            // ---- set pieces close to the player
            if (Starts(id, "SFX_SODIUM_")) { Set(ref s, 2.5f, 40f, 40); return; }
            if (Starts(id, "SFX_FLUORESCENT_")) { Set(ref s, 0.5f, 10f, 40); return; }
            if (id == "SFX_TRANSMITTER_HUM") { Set(ref s, 2f, 30f, 50); return; }
            if (Starts(id, "SFX_DRILL_")) { Set(ref s, 1.5f, 40f, 75); return; }
            if (id == "SFX_FIRE_CRACKLE_LOOP") { Set(ref s, 2f, 40f, 55); return; }
            if (Starts(id, "SFX_DOOR_") || Starts(id, "SFX_GATE_")) { Set(ref s, 2f, 45f, 65); return; }
            if (id == "SFX_SMOKE_GRENADE") { Set(ref s, 2f, 35f, 55); return; }
            if (id == "SFX_SPARKS") { Set(ref s, 1.5f, 30f, 55); return; }
            if (id == "SFX_FRUIT_SPILL" || Starts(id, "SFX_AWNING_")) { Set(ref s, 2f, 40f, 55); return; }
            if (id == "SFX_SATCHEL_PLANT" || id == "SFX_BOLTCUTTER_CHAIN" || id == "SFX_CHEMLIGHT_CRACK" || id == "SFX_CAMERA_SHUTTER" ||
                Starts(id, "SFX_FILM_") || Starts(id, "SFX_PROJECTOR_") || Starts(id, "SFX_TELETYPE"))
            {
                Set(ref s, 1f, 20f, 65);
                return;
            }

            // ---- G5 civilians behind walls (GDD §0.3)
            if (id == "SFX_TV_MUFFLED" || id == "SFX_RADIO_TURNED_DOWN" || id == "SFX_KETTLE" || id == "AMB_CASSETTE_TUNE")
            {
                Set(ref s, 1.5f, 18f, 35);
                return;
            }
            if (id == "SFX_SHUTTER_LATCH") { Set(ref s, 2f, 35f, 35); return; }
            if (id == "SFX_PIGEONS") { Set(ref s, 3f, 70f, 40); return; }

            // ---- the booth: a small concrete room (GDD §8.0), everything within a few metres
            if (id == "SFX_PEN_TAP" || id == "SFX_LIGHTER" || id == "SFX_LEATHER_CREAK" || id == "SFX_CHAIR_CREAK" ||
                id == "SFX_GLOVE_STUBBLE" || id == "SFX_HAND_SLAP" || id == "SFX_BREATH_BOOTH" || id == "SFX_PHONE" ||
                id == "SFX_TOGGLE_CLICK" || id == "SFX_TAPE_TRANSPORT" || id == "SFX_REEL_TO_REEL_LOOP" || Starts(id, "SFX_CRT_") ||
                id == "SFX_LEADER_BEEP" || id == "SFX_CLOCK_TICK_STUCK" || Starts(id, "STG_LAYERS_"))
            {
                Set(ref s, 0.4f, 8f, 60);
                s.PitchJitter = 0.015f;
            }
        }

        private static void Voice3D(ref CueSettings s, float min, float max)
        {
            s.Category = AudioCategories.Voice;
            s.SpatialBlend = 1f;
            s.MinDistance = min;
            s.MaxDistance = max;
            s.Rolloff = AudioRolloffMode.Logarithmic;
            s.Priority = 100;
            s.VolumeJitterDb = 0f;
            s.PitchJitter = 0f;
        }

        private static void Set(ref CueSettings s, float min, float max, int priority)
        {
            s.MinDistance = min;
            s.MaxDistance = max;
            s.Priority = priority;
        }

        private static bool Starts(string id, string prefix) => id.StartsWith(prefix, StringComparison.Ordinal);

        private static bool Ends(string id, string suffix) => id.EndsWith(suffix, StringComparison.Ordinal);
    }
}
