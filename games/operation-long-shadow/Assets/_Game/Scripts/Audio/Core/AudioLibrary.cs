using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Core
{
    /// <summary>
    /// <c>LIB_Audio</c> (TECH_PLAN D12, §7.9): cue id → <see cref="AudioCue"/>. Asset at
    /// <c>Assets/_Game/Data/Libraries/LIB_Audio.asset</c>, rebuilt by <b>LongShadow/Audio/Build Audio Library</b> from the
    /// clips under <c>Assets/_Game/Audio/{SFX,Music,Ambience}</c>. Looked up at runtime with
    /// <c>Libraries.Get&lt;AudioLibrary&gt;()</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "LongShadow/Audio/Audio Library", fileName = "LIB_Audio")]
    public sealed class AudioLibrary : AssetLibrary<AudioCue>
    {
    }
}
