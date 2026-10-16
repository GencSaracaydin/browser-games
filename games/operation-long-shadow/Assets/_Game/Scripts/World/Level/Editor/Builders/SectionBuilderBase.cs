using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Editor.Common;
using Unity.AI.Navigation;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// One section scene builder (TECH_PLAN §7.15 S1–S9). <see cref="SectionBuilders.Build"/> runs the fixed pipeline:
    /// <see cref="Geometry"/> → flush batches → markup (<c>MarkupPlacer</c>) → zone volumes → <see cref="Wire"/> (components
    /// on markers: awnings, crates, gates' visuals, vehicles) → flush → NavMesh bake → <see cref="AfterNav"/> → validate →
    /// save. Builders are deterministic: the same data gives the same scene.
    /// </summary>
    public abstract class SectionBuilderBase
    {
        /// <summary>The section this builder makes.</summary>
        public abstract SectionId Section { get; }

        /// <summary>Ground, buildings, landmarks, dressing and props.</summary>
        public abstract void Geometry(SectionBuildContext ctx);

        /// <summary>Section-specific components on the placed markers.</summary>
        public virtual void Wire(SectionBuildContext ctx)
        {
        }

        /// <summary>Hook after the NavMesh bake (links by name).</summary>
        public virtual void AfterNav(SectionBuildContext ctx, Dictionary<string, NavMeshLink> links)
        {
        }
    }
}
