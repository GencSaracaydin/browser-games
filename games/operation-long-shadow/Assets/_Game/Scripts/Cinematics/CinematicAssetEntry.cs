using System;
using LongShadow.Core;
using Object = UnityEngine.Object;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// One entry of <see cref="CinematicLibrary"/> (LIB_Cinematics): an asset reference by id. Ids are timeline names
    /// ("TL_Intro"), manifest ids ("IMG_MAP_SARDAHN", "VEH_HELICOPTER"), body prefab names ("PFB_Hollis") or the material
    /// and profile ids in <see cref="CinematicAssetIds"/>.
    /// </summary>
    [Serializable]
    public sealed class CinematicAssetEntry : ILibraryEntry
    {
        [UnityEngine.SerializeField] private string _id;
        [UnityEngine.SerializeField] private Object _asset;
        [UnityEngine.SerializeField] private string _note;

        /// <summary>Empty entry (serialization).</summary>
        public CinematicAssetEntry()
        {
        }

        /// <summary>Creates an entry (builders).</summary>
        public CinematicAssetEntry(string id, Object asset, string note = null)
        {
            _id = id;
            _asset = asset;
            _note = note;
        }

        /// <inheritdoc/>
        public string Id => _id;

        /// <summary>The referenced asset (may be null when the content has not landed).</summary>
        public Object Asset => _asset;

        /// <summary>Builder note (source path, fallback used).</summary>
        public string Note => _note;
    }
}
