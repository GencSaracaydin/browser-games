using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// <c>LIB_Dialogue</c> (TECH_PLAN D12, §7.10): DIALOGUE.json line id → <see cref="DialogueLine"/>. Asset at
    /// <c>Assets/_Game/Data/Libraries/LIB_Dialogue.asset</c>, rebuilt by <b>LongShadow/Audio/Import Dialogue</b>.
    /// </summary>
    [CreateAssetMenu(menuName = "LongShadow/Audio/Dialogue Library", fileName = "LIB_Dialogue")]
    public sealed class DialogueLibrary : AssetLibrary<DialogueLine>
    {
    }
}
