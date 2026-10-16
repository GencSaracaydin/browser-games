using System.Collections.Generic;
using LongShadow.AI.Encounters;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.AI.Editor
{
    /// <summary>WP04 menu items that are not builders of their own: build everything, and id validation.</summary>
    public static class AIMenu
    {
        /// <summary>Builds the enemy prefabs, LIB_Enemies, PF_AIServices and the squad prefabs in one go.</summary>
        [MenuItem("LongShadow/AI/Build All AI Prefabs", priority = 99)]
        public static void BuildAll()
        {
            EnemyPrefabBuilder.Build();
            EnemyPrefabBuilder.BuildServices();
            SquadPrefabBuilder.Build();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Checks every id the AI uses (EncounterTable markers, closets, perches, routes, vehicles, triggers, behaviour
        /// lookups) against Assets/_Game/Data/markup_ids.csv and against the markers in the open scenes.
        /// </summary>
        [MenuItem("LongShadow/AI/Validate Encounter Ids", priority = 120)]
        public static void Validate()
        {
            var csv = MarkupIds.Load();
            var problems = EncounterValidation.Check(csv.Contains);
            foreach (var p in problems) LSLog.Warn(LogCategory.AI, "Encounter id missing from markup_ids.csv: " + p);

            var inScenes = new HashSet<string>();
            foreach (var m in Object.FindObjectsByType<SceneMarker>(FindObjectsInactive.Include))
                if (!string.IsNullOrEmpty(m.Id)) inScenes.Add(m.Id);
            var present = 0;
            var refs = EncounterValidation.ReferencedIds();
            foreach (var kv in refs)
                if (inScenes.Contains(kv.Key)) present++;
            LSLog.Info(LogCategory.AI, "Encounter id check: " + problems.Count + " missing from the CSV; " + present + "/" + refs.Count +
                                       " referenced ids present in the open scenes (" + inScenes.Count + " markers loaded).");
        }
    }
}
