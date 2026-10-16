using LongShadow.Characters.Body;
using LongShadow.Characters.FirstPerson;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Puts Calder's dive watch on the left wrist of the FP body (GDD §4.1 "a Seiko dive watch showing the story clock",
    /// MID-A watch check reads 18:40): on the back of the wrist 3.5 cm above the joint, dial facing out, 3 o'clock toward
    /// the hand. Uses the FAL_DIVE_WATCH model when it has landed (scaled to a 42 mm case) and procedural hands either way
    /// (the manifest says the dial hands are procedural), driven by <see cref="WristWatch"/>.
    /// </summary>
    public static class WatchBuilder
    {
        /// <summary>Manifest id of the watch model (WP10 prop).</summary>
        public const string ManifestId = "FAL_DIVE_WATCH";
        /// <summary>Case diameter (m).</summary>
        public const float CaseDiameter = 0.042f;

        /// <summary>Builds (or rebuilds) the watch under the left forearm. Returns the WristWatch, or null without arm bones.</summary>
        public static WristWatch Attach(BodyRig rig, int layer, uint renderingLayers)
        {
            if (rig == null || rig.LeftLowerArm == null || rig.LeftHand == null) return null;
            var forearm = rig.LeftLowerArm;
            var old = forearm.Find("Watch");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var s = rig.Height / 1.78f;
            var dir = (rig.LeftHand.position - forearm.position).normalized;
            var dorsal = -Vector3.ProjectOnPlane(rig.PalmWorld(true), dir).normalized;
            if (dorsal.sqrMagnitude < 0.5f) dorsal = rig.transform.up;
            var root = new GameObject("Watch").transform;
            root.SetParent(forearm, false);
            root.position = rig.LeftHand.position - dir * (0.035f * s) + dorsal * (0.028f * s);
            root.rotation = Quaternion.LookRotation(Vector3.Cross(dir, dorsal), dorsal); // up = dial normal, right = 3 o'clock

            var dialHeight = 0.006f;
            var model = AssetManifest.FindAll<GameObject>(ManifestId);
            if (model.Count > 0)
            {
                var m = (GameObject)Object.Instantiate(model[0], root, false);
                m.name = "WatchModel";
                FitModel(m.transform, root);
                dialHeight = TopOf(m.transform, root) - 0.0008f;
            }
            else
            {
                LSLog.MissingAsset(LogCategory.Characters, ManifestId, "FAL_DIVE_WATCH (watch case); procedural stand-in used");
                var steel = BuilderUtil.PlaceholderMaterial(new Color(0.42f, 0.43f, 0.44f));
                var black = BuilderUtil.PlaceholderMaterial(new Color(0.03f, 0.03f, 0.035f));
                Part(root, "Case", PrimitiveType.Cylinder, new Vector3(0f, 0.004f, 0f), new Vector3(CaseDiameter, 0.004f, CaseDiameter), steel);
                Part(root, "Dial", PrimitiveType.Cylinder, new Vector3(0f, 0.0085f, 0f), new Vector3(0.034f, 0.0006f, 0.034f), black);
                Part(root, "Bezel", PrimitiveType.Cylinder, new Vector3(0f, 0.0082f, 0f), new Vector3(0.041f, 0.0009f, 0.041f), black);
                Part(root, "Strap_Hand", PrimitiveType.Cube, new Vector3(0.03f, 0.001f, 0f), new Vector3(0.03f, 0.004f, 0.02f), black);
                Part(root, "Strap_Elbow", PrimitiveType.Cube, new Vector3(-0.03f, 0.001f, 0f), new Vector3(0.03f, 0.004f, 0.02f), black);
                dialHeight = 0.0092f;
            }
            var lume = BuilderUtil.PlaceholderMaterial(new Color(0.86f, 0.86f, 0.8f));
            var orange = BuilderUtil.PlaceholderMaterial(new Color(0.85f, 0.35f, 0.1f));
            var hour = HandPivot(root, "Hand_Hour", dialHeight, 0.0105f, 0.0032f, lume);
            var minute = HandPivot(root, "Hand_Minute", dialHeight + 0.0004f, 0.0155f, 0.0024f, lume);
            var second = HandPivot(root, "Hand_Second", dialHeight + 0.0008f, 0.0165f, 0.0008f, orange);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            BodyPrefabBuilder.SetRenderers(root.gameObject, layer, renderingLayers, UnityEngine.Rendering.ShadowCastingMode.Off, true, false);
            var watch = root.gameObject.AddComponent<WristWatch>();
            watch.EditorSetup(hour, minute, second, Vector3.up);
            return watch;
        }

        private static Transform HandPivot(Transform root, string name, float height, float length, float width, Material mat)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(0f, height, 0f);
            // At rest the hand points to 12 o'clock (+Z of the watch).
            Part(pivot, name + "_Mesh", PrimitiveType.Cube, new Vector3(0f, 0f, length * 0.4f), new Vector3(width, 0.0004f, length), mat);
            return pivot;
        }

        private static void Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void FitModel(Transform model, Transform root)
        {
            var b = LocalBounds(model, root);
            var widest = Mathf.Max(b.size.x, b.size.z);
            if (widest > 1e-5f) model.localScale *= CaseDiameter / widest;
            b = LocalBounds(model, root);
            model.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
        }

        private static float TopOf(Transform model, Transform root) => LocalBounds(model, root).max.y;

        private static Bounds LocalBounds(Transform model, Transform root)
        {
            var any = false;
            var b = new Bounds();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var p = root.InverseTransformPoint(r.transform.TransformPoint(v));
                    if (!any)
                    {
                        b = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }
    }
}
