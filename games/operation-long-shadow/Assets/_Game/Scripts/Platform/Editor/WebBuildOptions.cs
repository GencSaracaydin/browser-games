using System;

namespace LongShadow.Platform.Editor
{
    /// <summary>
    /// Options of <see cref="WebBuild.Build"/> (TECH_PLAN §9.1). The two menus use <see cref="Release"/> and
    /// <see cref="QaBuild"/>; an editor agent can pass its own through <c>Unity_RunCommand</c>, e.g.
    /// <c>LongShadow.Platform.Editor.WebBuild.Build(new LongShadow.Platform.Editor.WebBuildOptions { FailOnDuplicates = false })</c>.
    /// </summary>
    [Serializable]
    public sealed class WebBuildOptions
    {
        /// <summary>QA variant: diagnostics overlay, external debug symbols, dev console on (LS_VARIANT "qa").</summary>
        public bool Qa;

        /// <summary>Output folder, project-relative or absolute (TECH_PLAN §9.1: Builds/Web).</summary>
        public string OutputFolder = WebBuild.OutputFolder;

        /// <summary>Clean and rebuild the Addressables content before the player (TECH_PLAN §9.1 step 2).</summary>
        public bool BuildContent = true;

        /// <summary>Apply <see cref="AddressablesGroupRules"/> before the content build.</summary>
        public bool ApplyGroupRules = true;

        /// <summary>Create dev placeholders for S_Persistent and the sections that do not exist yet (WP01 DoD).</summary>
        public bool EnsurePlaceholders = true;

        /// <summary>Rebuild S_Boot from code before building (a missing S_Boot is always built).</summary>
        public bool RebuildBootScene;

        /// <summary>Fail when Addressables "Check Duplicate Bundle Dependencies" reports anything (TECH_PLAN §9.1 step 2).</summary>
        public bool FailOnDuplicates = true;

        /// <summary>Run ProjectSetup when its verify pass reports drift from the TECH_PLAN §3 recipe (step 1).</summary>
        public bool EnforceProjectSettings = true;

        /// <summary>Fail when the CovertOps template is not the active WebGL template (it carries the errorHandler, DPR cap and gate).</summary>
        public bool RequireCovertOpsTemplate = true;

        /// <summary>Run <c>python3 Tools/build/publish_plan.py</c> afterwards (publish mirror + Builds/Web_publish_plan.json).</summary>
        public bool RunPublishPlan = true;

        /// <summary>Options of the "Web Release" menu: hashed file names, no diagnostics.</summary>
        public static WebBuildOptions Release() => new WebBuildOptions();

        /// <summary>Options of the "Web QA (diagnostics)" menu.</summary>
        public static WebBuildOptions QaBuild() => new WebBuildOptions { Qa = true };

        /// <summary>One line for logs.</summary>
        public override string ToString() =>
            (Qa ? "QA" : "Release") + " -> " + OutputFolder + (BuildContent ? "" : ", no content build") +
            (ApplyGroupRules ? "" : ", no group rules") + (EnsurePlaceholders ? ", placeholders" : "") +
            (FailOnDuplicates ? "" : ", duplicates allowed");
    }
}
