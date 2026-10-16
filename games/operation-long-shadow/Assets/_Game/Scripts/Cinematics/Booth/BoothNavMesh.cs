using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.AI;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// A tiny NavMesh for the booth floor, built at runtime (no NavMeshSurface package in this assembly) so the
    /// interrogator's HumanoidBody can <c>MoveTo</c> its marks (outro shot 7: he walks past the chair to the scrambler,
    /// shot 8 to the phone). Furniture is carved out as not-walkable boxes. Removed when the booth unloads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoothNavMesh : MonoBehaviour
    {
        /// <summary>Unity's built-in "Not Walkable" area.</summary>
        private const int NotWalkable = 1;

        [SerializeField, Tooltip("Furniture footprints in booth-local space (centre xz, size xz).")]
        private Vector4[] _obstacles =
        {
            new Vector4(0f, 0.05f, 0.75f, 0.80f),      // chair
            new Vector4(0f, 1.25f, 1.75f, 0.60f),      // CRT cart
            new Vector4(-1.42f, 0.98f, 1.18f, 0.62f),  // scrambler trolley
            new Vector4(1.42f, 0.98f, 1.18f, 0.62f),   // phone trolley
            new Vector4(-0.8f, -0.7f, 0.95f, 0.65f),   // steel table
            new Vector4(-1.6f, 2.2f, 1.0f, 1.0f),      // film camera tripod
        };

        private NavMeshDataInstance _instance;
        private NavMeshData _data;

        private void OnEnable() => Build();

        private void OnDisable()
        {
            if (_instance.valid) _instance.Remove();
            if (_data != null) Destroy(_data);
            _data = null;
        }

        private void Build()
        {
            var settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource>(_obstacles.Length + 1);
            var floorSize = new Vector3(BoothLayout.MaxX - BoothLayout.MinX, 0.1f, BoothLayout.MaxZ - BoothLayout.MinZ);
            var floorCentre = new Vector3((BoothLayout.MaxX + BoothLayout.MinX) * 0.5f, -0.05f, (BoothLayout.MaxZ + BoothLayout.MinZ) * 0.5f);
            var m = transform.localToWorldMatrix;
            sources.Add(Box(m, floorCentre, floorSize, 0));
            foreach (var o in _obstacles)
                sources.Add(Box(m, new Vector3(o.x, 0.5f, o.y), new Vector3(o.z, 1.0f, o.w), NotWalkable));

            // Sources are in world space; build and add the data in the world frame.
            var bounds = new Bounds(transform.TransformPoint(floorCentre), floorSize + new Vector3(1f, 4f, 1f));
            _data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (_data == null)
            {
                LSLog.Warn(LogCategory.Cinematics, "Booth NavMesh build failed; the interrogator moves on rails.", this);
                return;
            }
            _instance = NavMesh.AddNavMeshData(_data);
            // Agents enabled before the data existed are not on the mesh yet.
            foreach (var agent in GetComponentsInChildren<NavMeshAgent>())
                if (agent.isActiveAndEnabled && !agent.isOnNavMesh) agent.Warp(agent.transform.position);
        }

        private static NavMeshBuildSource Box(Matrix4x4 root, Vector3 centre, Vector3 size, int area) => new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box,
            transform = root * Matrix4x4.Translate(centre),
            size = size,
            area = area,
        };
    }
}
