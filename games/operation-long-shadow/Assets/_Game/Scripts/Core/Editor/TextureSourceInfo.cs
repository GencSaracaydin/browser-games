using System;

namespace LongShadow.Core.Editor
{
    /// <summary>What the import rules need to know about a source image, read from its file header (and, for PNGs with
    /// an alpha channel, from the pixels). Produced by <see cref="TextureSourceProbe"/>.</summary>
    public struct TextureSourceInfo
    {
        /// <summary>Header was parsed (PNG, JPEG, TGA, EXR, HDR). False for other formats.</summary>
        public bool Known;
        /// <summary>Pixel size of the source.</summary>
        public int Width, Height;
        /// <summary>The file stores an alpha channel (PNG colour type 4/6 or tRNS, 32-bit TGA, ...).</summary>
        public bool HasAlphaChannel;
        /// <summary>Some pixel has alpha below 250/255. Scanned for PNG; equals <see cref="HasAlphaChannel"/> otherwise.</summary>
        public bool AlphaUsed;
        /// <summary>Bits per channel (8 or 16 for PNG; 0 when unknown).</summary>
        public int BitsPerChannel;
        /// <summary>High dynamic range source (.exr/.hdr).</summary>
        public bool IsHdr;

        /// <summary>Largest side in pixels.</summary>
        public int MaxSide => Math.Max(Width, Height);

        /// <summary>True when both sides are multiples of 4 (block compression without padding).</summary>
        public bool BlockAligned => Width % 4 == 0 && Height % 4 == 0;

        /// <inheritdoc />
        public override string ToString() =>
            Known ? $"{Width}x{Height} {(BitsPerChannel > 0 ? BitsPerChannel + "bit" : "")}{(IsHdr ? " HDR" : "")}{(HasAlphaChannel ? (AlphaUsed ? " alpha(used)" : " alpha(opaque)") : "")}"
                  : "unknown";
    }
}
