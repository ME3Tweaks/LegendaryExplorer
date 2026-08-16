using System;
using System.Windows;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;

/// <summary>
/// Defines a single tile in a tiled background image layer.
/// </summary>
/// <param name="SourceFactory">
/// Called on the UI thread the first time (or after an unload) the tile scrolls into view.
/// Should return a frozen <see cref="ImageSource"/> for best rendering performance, or
/// <see langword="null"/> if the tile cannot be loaded.
/// The factory may be called again if the tile is unloaded after scrolling out of view
/// and then scrolls back into view.
/// </param>
/// <param name="X">Left edge of the tile in graph space.</param>
/// <param name="Y">Top edge of the tile in graph space.</param>
/// <param name="Width">Width of the tile in graph space.</param>
/// <param name="Height">Height of the tile in graph space.</param>
public sealed record BackgroundTile(
    Func<ImageSource?> SourceFactory,
    double X,
    double Y,
    double Width,
    double Height)
{
    /// <summary>
    /// Creates a <see cref="BackgroundTile"/> from two corner points in graph space.
    /// The corners do not need to be in any particular order; the bounding box is
    /// computed automatically.
    /// </summary>
    /// <param name="sourceFactory">Factory called when the tile first enters the viewport.</param>
    /// <param name="topLeft">Top-left corner of the tile region in graph space.</param>
    /// <param name="bottomRight">Bottom-right corner of the tile region in graph space.</param>
    public static BackgroundTile FromCorners(
        Func<ImageSource?> sourceFactory,
        Point topLeft,
        Point bottomRight)
    {
        var minX = Math.Min(topLeft.X, bottomRight.X);
        var minY = Math.Min(topLeft.Y, bottomRight.Y);
        var maxX = Math.Max(topLeft.X, bottomRight.X);
        var maxY = Math.Max(topLeft.Y, bottomRight.Y);
        return new BackgroundTile(sourceFactory, minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Creates a <see cref="BackgroundTile"/> from explicit edge coordinates in graph space.
    /// The values do not need to be ordered; the bounding box is computed automatically.
    /// </summary>
    /// <param name="sourceFactory">Factory called when the tile first enters the viewport.</param>
    /// <param name="left">Left edge of the tile in graph space.</param>
    /// <param name="top">Top edge of the tile in graph space.</param>
    /// <param name="right">Right edge of the tile in graph space.</param>
    /// <param name="bottom">Bottom edge of the tile in graph space.</param>
    public static BackgroundTile FromCorners(
        Func<ImageSource?> sourceFactory,
        double left, double top,
        double right, double bottom)
        => FromCorners(sourceFactory, new Point(left, top), new Point(right, bottom));
}
