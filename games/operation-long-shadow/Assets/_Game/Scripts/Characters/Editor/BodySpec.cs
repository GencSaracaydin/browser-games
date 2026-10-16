using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>One body prefab to build (a row of <see cref="BodySpecs"/>).</summary>
    public sealed class BodySpec
    {
        /// <summary>Prefab name (PFB_*), written to Prefabs/Bodies.</summary>
        public string Prefab;
        /// <summary>ASSET_MANIFEST id (CHR_*).</summary>
        public string Manifest;
        /// <summary>Model paths below Art/Models/Characters, first existing wins (fallbacks after the first).</summary>
        public string[] Models;
        public BodyKind Kind;
        /// <summary>Controller for HumanoidBody kinds (AC_Soldier or an override).</summary>
        public ControllerRole Controller = ControllerRole.Soldier;
        public Team Team = Team.None;
        /// <summary>DIALOGUE.json speaker id for the jaw flap (null = no jaw flap).</summary>
        public string Speaker;
        /// <summary>Weapon shown on spawn (Core WeaponIds; null = unarmed).</summary>
        public string Weapon;
        /// <summary>Hit points (GDD §14.1); 0 = no Health component.</summary>
        public float Hp;
        /// <summary>Torso damage factor (Greycoat vest 0.8, GDD §14.1).</summary>
        public float TorsoArmour = 1f;
        /// <summary>Stable actor id (squad ids).</summary>
        public string ActorId;
        /// <summary>Colour of the greybox mannequin when no model has landed.</summary>
        public Color Placeholder = Placeholders.Greybox;
        /// <summary>Renderers also lit by the helicopter cabin lamp (rendering layer Cabin).</summary>
        public bool Cabin;
        /// <summary>Calder in the booth chair (BoothBody isCalder, cuffs).</summary>
        public bool BoothCalder;
        /// <summary>Notes for the report.</summary>
        public string Note;

        public override string ToString() => Prefab + " (" + Kind + ")";
    }
}
