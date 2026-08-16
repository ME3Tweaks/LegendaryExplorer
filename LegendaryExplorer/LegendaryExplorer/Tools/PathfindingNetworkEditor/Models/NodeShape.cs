namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;

/// <summary>
/// Defines the available shapes for graph nodes.
/// </summary>
public enum NodeShape
{
    Circle,
    Rectangle,
    Diamond,
    Hexagon,
    /// <summary>An up-then-right arrow (L-shaped arrow).</summary>
    Mantle,
    /// <summary>A diagonal right-up then diagonal right-down arrow (chevron/lightning bolt).</summary>
    Jump,
    /// <summary>A boot shape with a thruster nozzle and an upward arrow.</summary>
    BoostUp,
    /// <summary>A boot shape with a thruster nozzle and a downward arrow.</summary>
    BoostDown,
    /// <summary>A T-shaped tetromino with the stem pointing up and the bar horizontal at the bottom.</summary>
    CoverLink,
    /// <summary>A 4-point shield: diamond whose left/right vertices are 3/4 up the shape instead of at the midpoint.</summary>
    CoverSlotMarker,
    /// <summary>A ladder with 3 rungs and an upward-pointing triangle at the top.</summary>
    LadderUp,
    /// <summary>A ladder with 3 rungs and a downward-pointing triangle at the bottom.</summary>
    LadderDown,
    /// <summary>A rectangle with a small inward notch on the left side representing a door handle.</summary>
    SFXDoor,
    /// <summary>A question mark glyph.</summary>
    Unknown,
    /// <summary>A triangle pointing in the direction of focus.</summary>
    Player,
    /// <summary>A camera with lines indicating the field of view.</summary>
    Camera
}
