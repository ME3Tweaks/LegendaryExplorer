using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorerCore.Textures
{
    // MILAN CHANGE - 2026-10-14 - hooks so the caller can cache the (expensive) encoded mip chains Texture2D.Replace() generates.

    /// <summary>
    /// Optional hooks for <see cref="Texture2D.Replace"/>. All of them may be left unset; with none set, the texture is
    /// replaced exactly as without callbacks.
    /// </summary>
    public sealed class TextureReplaceCallbacks
    {
        /// <summary>
        /// Called right before Replace() would generate and encode a mip chain. Return the chain to use instead (every mip
        /// from the top one, the size of <see cref="TextureEncodeRequest.Width"/> x <see cref="TextureEncodeRequest.Height"/>,
        /// down to 1x1, encoded in <see cref="TextureEncodeRequest.DestinationFormat"/>), or null to have it generated as usual.
        /// A chain that does not fit the request is ignored and the mips are generated.
        /// </summary>
        public Func<TextureEncodeRequest, IReadOnlyList<EncodedMip>> TryGetEncodedMips;

        /// <summary>
        /// Called right after Replace() generated and encoded a complete mip chain (not when <see cref="TryGetEncodedMips"/>
        /// supplied one), so a caller can store it.
        /// </summary>
        public Action<TextureEncodeRequest, IReadOnlyList<EncodedMip>> MipsEncoded;
    }

    /// <summary>
    /// Everything that decides the mip chain Replace() is about to generate, taken before generating it.
    /// </summary>
    public sealed class TextureEncodeRequest
    {
        /// <summary>The texture being replaced.</summary>
        public ExportEntry Export { get; init; }

        /// <summary>The top mip the chain is generated from, in <see cref="SourceFormat"/>. Do not modify it.</summary>
        public byte[] SourceData { get; init; }

        /// <summary>The real (unpadded) width of the top mip.</summary>
        public int Width { get; init; }

        /// <summary>The real (unpadded) height of the top mip.</summary>
        public int Height { get; init; }

        /// <summary>The pixel format of <see cref="SourceData"/>.</summary>
        public PixelFormat SourceFormat { get; init; }

        /// <summary>The pixel format the chain is encoded in.</summary>
        public PixelFormat DestinationFormat { get; init; }

        /// <summary>Whether a DXT1 destination keeps 1-bit alpha.</summary>
        public bool Dxt1HasAlpha { get; init; }

        /// <summary>Alpha cut-off for DXT1 1-bit alpha.</summary>
        public byte Dxt1Threshold { get; init; }

        /// <summary>How the lower mips are filtered. Null is the historical box filter.</summary>
        public MipGenerationOptions MipOptions { get; init; }

        /// <summary>Free for the caller, for example to keep a cache key computed in <see cref="TextureReplaceCallbacks.TryGetEncodedMips"/> for <see cref="TextureReplaceCallbacks.MipsEncoded"/>.</summary>
        public object CallerState { get; set; }
    }
}
