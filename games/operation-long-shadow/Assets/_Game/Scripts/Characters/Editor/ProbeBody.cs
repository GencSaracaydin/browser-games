using LongShadow.Core;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// A temporary Humanoid body used by the builders to sample clips (<see cref="LocomotionSpeedProbe"/>): an instance of
    /// the reference model SK_Calder, or the placeholder mannequin before it lands. Hidden and never saved.
    /// </summary>
    public static class ProbeBody
    {
        /// <summary>Creates the probe (null only if no Humanoid can be made).</summary>
        public static GameObject Create()
        {
            GameObject go = null;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPaths.ReferenceModel);
            if (model != null)
            {
                go = Object.Instantiate(model);
                var a = go.GetComponentInChildren<Animator>();
                if (a == null || a.avatar == null || !a.avatar.isHuman)
                {
                    Object.DestroyImmediate(go);
                    go = null;
                }
            }
            if (go == null) go = PlaceholderMannequin.CreateInstance("Probe_Mannequin", Placeholders.Greybox);
            go.name = "__WP05_Probe";
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.position = new Vector3(0f, -5000f, 0f);
            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
            }
            return go;
        }

        /// <summary>Destroys the probe.</summary>
        public static void Destroy(GameObject probe)
        {
            if (probe != null) Object.DestroyImmediate(probe);
        }
    }
}
