using UnityEngine;

namespace LongShadow.Characters.Animation
{
    /// <summary>Name and type of one animator parameter of the WP05 controllers (<see cref="CharacterAnimIds.AllParameters"/>).</summary>
    public readonly struct ParameterSpec
    {
        /// <summary>Parameter name.</summary>
        public readonly string Name;
        /// <summary>Parameter type.</summary>
        public readonly AnimatorControllerParameterType Type;

        public ParameterSpec(string name, AnimatorControllerParameterType type)
        {
            Name = name;
            Type = type;
        }

        public override string ToString() => Name + ":" + Type;
    }
}
