#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>Shared scene-view drawing for markup components (editor only).</summary>
    internal static class MarkupGizmos
    {
        /// <summary>Labels are drawn only within this distance of the scene camera.</summary>
        public const float LabelDistance = 80f;

        public static bool ShowLabels
        {
            get => EditorPrefs.GetBool("LongShadow.Markup.ShowLabels", true);
            set => EditorPrefs.SetBool("LongShadow.Markup.ShowLabels", value);
        }

        public static void Label(Vector3 position, string text, Color color)
        {
            if (!ShowLabels || string.IsNullOrEmpty(text)) return;
            var cam = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera : null;
            if (cam != null && (cam.transform.position - position).sqrMagnitude > LabelDistance * LabelDistance) return;
            var style = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = color } };
            Handles.Label(position, text, style);
        }

        public static void Diamond(Vector3 p, float size, Color color)
        {
            Gizmos.color = color;
            var u = Vector3.up * size;
            var r = Vector3.right * size;
            var f = Vector3.forward * size;
            Gizmos.DrawLine(p + u, p + r); Gizmos.DrawLine(p + r, p - u); Gizmos.DrawLine(p - u, p - r); Gizmos.DrawLine(p - r, p + u);
            Gizmos.DrawLine(p + u, p + f); Gizmos.DrawLine(p + f, p - u); Gizmos.DrawLine(p - u, p - f); Gizmos.DrawLine(p - f, p + u);
        }

        public static void Arrow(Vector3 from, Vector3 dir, float length, Color color)
        {
            Gizmos.color = color;
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            var to = from + dir * length;
            Gizmos.DrawLine(from, to);
            var side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
            side.Normalize();
            Gizmos.DrawLine(to, to - dir * 0.25f * length + side * 0.12f * length);
            Gizmos.DrawLine(to, to - dir * 0.25f * length - side * 0.12f * length);
        }

        public static void Arc(Vector3 center, Vector3 forward, float arcDegrees, float radius, Color color)
        {
            Handles.color = color;
            var from = Quaternion.AngleAxis(-arcDegrees * 0.5f, Vector3.up) * Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            Handles.DrawWireArc(center, Vector3.up, from, arcDegrees, radius);
        }

        public static void ColliderShape(Collider c, Color color)
        {
            if (c == null) return;
            Gizmos.color = color;
            var m = Gizmos.matrix;
            Gizmos.matrix = c.transform.localToWorldMatrix;
            switch (c)
            {
                case BoxCollider b:
                    Gizmos.DrawWireCube(b.center, b.size);
                    Gizmos.color = new Color(color.r, color.g, color.b, color.a * 0.15f);
                    Gizmos.DrawCube(b.center, b.size);
                    break;
                case SphereCollider s:
                    Gizmos.DrawWireSphere(s.center, s.radius);
                    break;
                case CapsuleCollider cap:
                    Gizmos.DrawWireSphere(cap.center + Vector3.up * (cap.height * 0.5f - cap.radius), cap.radius);
                    Gizmos.DrawWireSphere(cap.center - Vector3.up * (cap.height * 0.5f - cap.radius), cap.radius);
                    break;
                default:
                    Gizmos.matrix = m;
                    Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
                    break;
            }
            Gizmos.matrix = m;
        }
    }
}
#endif
