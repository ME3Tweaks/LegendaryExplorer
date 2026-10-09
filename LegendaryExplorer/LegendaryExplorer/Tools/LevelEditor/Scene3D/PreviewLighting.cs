using System.Numerics;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// The lighting used when rendering with the game's shaders: one directional light, plus hemispherical sky lighting for ambient.
/// This is the light environment the game uses for dynamically lit primitives such as characters, minus the spherical harmonic term.
/// All colors are linear.
/// </summary>
public class PreviewLighting
{
    /// <summary>
    /// If true, the directional light is positioned relative to the camera (behind, above, and to the left of it), so that whatever is being looked at is lit.
    /// It's kept well away from the view direction, since a light near it puts a highlight on every surface facing the camera.
    /// If false, <see cref="WorldLightDirection"/> is used.
    /// </summary>
    public bool LightFollowsCamera { get; set; } = true;

    /// <summary>
    /// World-space direction pointing towards the directional light. Used when <see cref="LightFollowsCamera"/> is false.
    /// </summary>
    public Vector3 WorldLightDirection { get; set; } = Vector3.Normalize(new Vector3(-0.5f, -0.6f, 0.8f));

    public LinearColor LightColor { get; set; } = new(1.0f, 0.97f, 0.92f, 1);

    /// <summary>
    /// Ambient light from above (world +Z)
    /// </summary>
    public LinearColor UpperSkyColor { get; set; } = new(0.26f, 0.28f, 0.32f, 1);

    /// <summary>
    /// Ambient light from below (world -Z)
    /// </summary>
    public LinearColor LowerSkyColor { get; set; } = new(0.08f, 0.07f, 0.06f, 1);

    //LE3's BioEngine.ini [WrapLighting] DirectScale and IndirectScale.
    //The game multiplies these by per-primitive scales, which are 1 when not otherwise specified.
    public const float WrapLightingDirectScale = 0.33f;
    public const float WrapLightingIndirectScale = 0.05f;

    /// <summary>
    /// Gets the world-space direction pointing towards the directional light
    /// </summary>
    public Vector3 GetLightDirection(SceneCamera camera)
    {
        if (!LightFollowsCamera)
        {
            return WorldLightDirection;
        }
        //a typical key light: about 55 degrees off the view direction, above and to the side
        return Vector3.Normalize(-camera.CameraForward + camera.CameraUp - camera.CameraRight);
    }
}
