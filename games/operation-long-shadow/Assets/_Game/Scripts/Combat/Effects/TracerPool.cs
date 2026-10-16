using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// Pooled tracer streaks (TECH_PLAN §7.5: 400 m/s visual; red for Player/Squad, green for Legion/Greycoat; GDD §13.4).
    /// Each tracer is a two-point LineRenderer whose head flies at <see cref="Speed"/> from the muzzle to the impact and whose
    /// tail trails <see cref="Length"/> behind; width grows with camera distance so distant streams stay readable.
    /// One Update drives all of them (no per-object behaviours).
    /// </summary>
    public sealed class TracerPool
    {
        /// <summary>Visual speed in m/s (TECH_PLAN §7.5).</summary>
        public const float Speed = 400f;
        /// <summary>Streak length in metres.</summary>
        public const float Length = 7f;

        private readonly LineRenderer[] _lines;
        private readonly Vector3[] _from;
        private readonly Vector3[] _dir;
        private readonly float[] _dist;
        private readonly float[] _travel;
        private readonly Vector3[] _two = new Vector3[2];
        private int _next;

        /// <summary>Red (SUNDIAL, door gun) material.</summary>
        public Material RedMaterial { get; }
        /// <summary>Green (Legion, Greycoat) material.</summary>
        public Material GreenMaterial { get; }

        /// <summary>Creates <paramref name="count"/> tracers. <paramref name="baseMaterial"/>: v 0.5-1 red, 0-0.5 green.</summary>
        public TracerPool(Transform root, int count, Material baseMaterial)
        {
            RedMaterial = baseMaterial != null ? new Material(baseMaterial) { name = "M_FX_Tracer_Red" } : null;
            GreenMaterial = baseMaterial != null ? new Material(baseMaterial) { name = "M_FX_Tracer_Green" } : null;
            FxMaterials.SetBaseMapRect(RedMaterial, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f));
            FxMaterials.SetBaseMapRect(GreenMaterial, new Vector2(1f, 0.5f), Vector2.zero);
            _lines = new LineRenderer[count];
            _from = new Vector3[count];
            _dir = new Vector3[count];
            _dist = new float[count];
            _travel = new float[count];
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("Tracer_" + i);
                go.transform.SetParent(root, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.textureMode = LineTextureMode.Stretch;
                lr.alignment = LineAlignment.View;
                lr.numCapVertices = 0;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                lr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                lr.startWidth = lr.endWidth = 0.03f;
                lr.enabled = false;
                _lines[i] = lr;
            }
        }

        /// <summary>Material for a team (red for the player's side).</summary>
        public Material For(Team team) => Teams.IsEnemySide(team) ? GreenMaterial : RedMaterial;

        /// <summary>Launches a tracer from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public void Fire(Vector3 from, Vector3 to, Team team)
        {
            if (_lines.Length == 0) return;
            var d = to - from;
            var dist = d.magnitude;
            if (dist < 1f) return;
            var i = _next;
            _next = (_next + 1) % _lines.Length;
            _from[i] = from;
            _dir[i] = d / dist;
            _dist[i] = dist;
            _travel[i] = 0f;
            var lr = _lines[i];
            lr.sharedMaterial = For(team);
            lr.enabled = lr.sharedMaterial != null;
        }

        /// <summary>Moves every live tracer; <paramref name="cameraPos"/> scales widths.</summary>
        public void Tick(float dt, Vector3 cameraPos)
        {
            for (var i = 0; i < _lines.Length; i++)
            {
                var lr = _lines[i];
                if (!lr.enabled) continue;
                _travel[i] += Speed * dt;
                var head = Mathf.Min(_travel[i], _dist[i]);
                var tail = Mathf.Max(0f, _travel[i] - Length);
                if (tail >= _dist[i])
                {
                    lr.enabled = false;
                    continue;
                }
                _two[0] = _from[i] + _dir[i] * tail;
                _two[1] = _from[i] + _dir[i] * head;
                lr.SetPositions(_two);
                var camDist = Vector3.Distance(cameraPos, _two[1]);
                var w = Mathf.Clamp(camDist * 0.0011f, 0.014f, 0.3f);
                lr.startWidth = w * 0.6f;
                lr.endWidth = w;
            }
        }

        /// <summary>Hides every tracer.</summary>
        public void Clear()
        {
            for (var i = 0; i < _lines.Length; i++) _lines[i].enabled = false;
        }
    }
}
