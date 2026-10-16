using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Geometry;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Markup
{
    /// <summary>
    /// Door and gate leaves of a <see cref="GateBlockerDef"/>: one hinge transform per leaf (yaw = closed direction of the
    /// leaf's +X), the leaf mesh (plank door, steel door or iron bar gate, probe-lit, not static) with a box collider that
    /// stops bullets, and a <see cref="GateDoorAnimator"/> on the gate. Leaves are named "DYN_*" so the NavMesh bake ignores
    /// them (doorways stay walkable; the GateMarker's PlayerClip does the blocking).
    /// </summary>
    public static class GateLeaves
    {
        /// <summary>Prefix of objects the NavMesh bake skips.</summary>
        public const string DynamicPrefix = "DYN_";

        /// <summary>Builds the leaves under <paramref name="gate"/> and configures its animator.</summary>
        public static void Build(SectionBuildContext ctx, Transform gate, GateBlockerDef def)
        {
            var hinges = new Transform[def.Hinges.Length];
            for (var i = 0; i < hinges.Length; i++)
            {
                var hinge = new GameObject(DynamicPrefix + "Hinge" + i).transform;
                hinge.SetParent(gate, false);
                hinge.SetPositionAndRotation(def.Hinges[i], Quaternion.Euler(0f, def.ClosedYaw[i], 0f));
                var mb = new MeshBuilder();
                Leaf(mb, def.LeafWidth, def.LeafHeight, def.LeafKind, i);
                var leaf = ctx.Mesh(MeshAssets.SafeName(def.Id) + "_Leaf" + i, hinge, def.Hinges[i], hinge.rotation, mb, 0f, false);
                leaf.name = DynamicPrefix + "Leaf" + i;
                LevelObjects.ClearStatic(leaf);
                var col = leaf.AddComponent<BoxCollider>();
                col.center = new Vector3(def.LeafWidth * 0.5f, def.LeafHeight * 0.5f, 0f);
                col.size = new Vector3(def.LeafWidth - 0.04f, def.LeafHeight, def.LeafKind == 2 ? 0.08f : 0.06f);
                leaf.AddComponent<SurfaceTag>().Surface = def.LeafKind == 0 ? SurfaceType.Wood : SurfaceType.Metal;
                hinges[i] = hinge;
            }
            var anim = gate.gameObject.AddComponent<GateDoorAnimator>();
            var creak = def.LeafKind == 2 ? "SFX_GATE_CREAK" : "SFX_DOOR_CREAK";
            anim.Configure(def.Id, hinges, def.OpenYaw, def.Kick ? 0.35f : 1.6f, def.Kick ? "SFX_DOOR_KICK" : creak, creak, def.Kick);
        }

        /// <summary>One leaf in hinge space: +X along the leaf, +Y up, centred on z = 0.</summary>
        public static void Leaf(MeshBuilder mb, float width, float height, int kind, int seed)
        {
            switch (kind)
            {
                case 2:
                {
                    // Iron bar gate: frame, vertical bars every 0.14 m, two rails, a chain plate on the meeting stile.
                    mb.Slot(KitSlots.Iron);
                    mb.AddBox(new Vector3(0.03f, height * 0.5f, 0f), new Vector3(0.06f, height, 0.06f));
                    mb.AddBox(new Vector3(width - 0.03f, height * 0.5f, 0f), new Vector3(0.06f, height, 0.06f));
                    mb.AddBox(new Vector3(width * 0.5f, 0.12f, 0f), new Vector3(width, 0.08f, 0.05f));
                    mb.AddBox(new Vector3(width * 0.5f, height - 0.1f, 0f), new Vector3(width, 0.08f, 0.05f));
                    mb.AddBox(new Vector3(width * 0.5f, height * 0.55f, 0f), new Vector3(width, 0.06f, 0.04f));
                    var n = Mathf.FloorToInt((width - 0.12f) / 0.14f);
                    for (var i = 1; i <= n; i++)
                    {
                        var x = 0.06f + i * (width - 0.12f) / (n + 1);
                        mb.AddCylinder(new Vector3(x, 0.08f, 0f), 0.014f, height - 0.08f, 5, true, false);
                        mb.AddFrustum(new Vector3(x, height, 0f), 0.025f, 0f, 0.09f, 4, false, false);
                    }
                    if (seed == 0) mb.AddBox(new Vector3(width - 0.08f, height * 0.5f, 0.05f), new Vector3(0.14f, 0.22f, 0.03f));
                    break;
                }
                case 1:
                {
                    mb.Slot(KitSlots.SteelGreen);
                    mb.AddChamferBox(new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(width, height, 0.05f), 0.01f, true, true);
                    mb.Slot(KitSlots.SteelFrame);
                    mb.AddBox(new Vector3(width * 0.5f, height * 0.72f, 0.03f), new Vector3(width - 0.2f, 0.04f, 0.012f));
                    mb.AddBox(new Vector3(width * 0.5f, height * 0.3f, 0.03f), new Vector3(width - 0.2f, 0.04f, 0.012f));
                    mb.AddBox(new Vector3(width - 0.12f, 1.0f, 0.045f), new Vector3(0.14f, 0.03f, 0.03f));
                    mb.AddBox(new Vector3(width - 0.12f, 1.0f, -0.045f), new Vector3(0.14f, 0.03f, 0.03f));
                    break;
                }
                default:
                {
                    mb.Slot(KitSlots.WoodPainted);
                    var planks = Mathf.Max(3, Mathf.RoundToInt(width / 0.15f));
                    var pw = width / planks;
                    for (var i = 0; i < planks; i++)
                    {
                        var h = height - ((i * 7 + seed * 3) % 5 == 0 ? 0.02f : 0f);
                        mb.AddBox(new Vector3(pw * (i + 0.5f), h * 0.5f, 0f), new Vector3(pw - 0.006f, h, 0.04f));
                    }
                    mb.Slot(KitSlots.WoodDark);
                    mb.AddBox(new Vector3(width * 0.5f, height * 0.2f, -0.03f), new Vector3(width - 0.08f, 0.12f, 0.025f));
                    mb.AddBox(new Vector3(width * 0.5f, height * 0.8f, -0.03f), new Vector3(width - 0.08f, 0.12f, 0.025f));
                    mb.Slot(KitSlots.Iron);
                    mb.AddBox(new Vector3(width - 0.1f, 1.0f, 0.04f), new Vector3(0.05f, 0.12f, 0.03f));
                    break;
                }
            }
        }
    }
}
