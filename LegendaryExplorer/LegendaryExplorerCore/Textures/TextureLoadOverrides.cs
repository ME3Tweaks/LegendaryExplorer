using System;

namespace LegendaryExplorerCore.Textures
{
    /// <summary>Explicit import exceptions. Existing texture-loading APIs retain their legacy behavior.</summary>
    [Flags]
    public enum TextureLoadOverrides : uint
    {
        None = 0,
        /// <summary>Keep a TGA's stored all-zero alpha instead of treating it as unused and making it opaque.</summary>
        PreserveAllZeroTgaAlpha = 1
    }
}
