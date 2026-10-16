using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Level.Editor.Markup
{
    /// <summary>
    /// Places a section's <see cref="ZoneTag"/>s and <see cref="EyeAdaptationZone"/>s from <see cref="ZoneVolumes"/>:
    /// anonymous trigger boxes on layer Trigger. Eye-adaptation profiles are WP10 Look's (<c>Settings/Volumes/VP_Interior_*</c>);
    /// a missing profile is logged and the zone keeps its EV offset.
    /// </summary>
    public static class VolumePlacer
    {
        /// <summary>Places the zones of the context's section; returns the count.</summary>
        public static int Place(SectionBuildContext ctx)
        {
            var n = 0;
            foreach (var z in ZoneVolumes.For(ctx.Section))
            {
                Place(ctx, z);
                n++;
            }
            ctx.Info("Volumes: " + n + " zone boxes.");
            return n;
        }

        private static void Place(SectionBuildContext ctx, ZoneVolumeDef z)
        {
            var go = new GameObject(z.Name);
            go.transform.SetParent(ctx.Group(SectionBuildContext.GVolumes), false);
            var c = z.Rect.Center;
            go.transform.position = new Vector3(c.x, (z.Y0 + z.Y1) * 0.5f, c.z);
            go.layer = LayerIds.Trigger;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(z.Rect.Width, z.Y1 - z.Y0, z.Rect.Depth);
            if (z.EyeAdaptation)
            {
                var eye = go.AddComponent<EyeAdaptationZone>();
                var path = LevelPaths.Volumes + "/" + z.Profile + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile == null) ctx.Info("Eye adaptation profile missing (WP10 Look builds it): " + path);
                eye.Configure(profile, z.Ev);
                eye.Configure(true, false, true);
            }
            else
            {
                var tag = go.AddComponent<ZoneTag>();
                tag.SetZone(z.Zone, z.Priority);
                tag.Configure(true, false, true);
            }
        }
    }
}
