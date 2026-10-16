namespace LongShadow.Core.Editor
{
    /// <summary>What the model rules need to know about a source file before import (see <see cref="ModelSourceProbe"/>).</summary>
    public struct ModelSourceInfo
    {
        /// <summary>The file was read.</summary>
        public bool Known;
        /// <summary>The file names objects "*_LOD1" (authored LODs: Unity builds a LODGroup, Mesh LOD generation stays off).</summary>
        public bool HasAuthoredLods;
        /// <summary>File size in bytes.</summary>
        public long Bytes;

        /// <inheritdoc />
        public override string ToString() => Known ? (Bytes / 1024) + " KB" + (HasAuthoredLods ? ", authored LODs" : "") : "unknown";
    }
}
