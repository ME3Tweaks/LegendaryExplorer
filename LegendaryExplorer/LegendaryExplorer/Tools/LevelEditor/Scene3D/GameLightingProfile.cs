using System.Numerics;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Stock game lighting constants (BioEngine.ini [WrapLighting]) and the features the viewport implements
/// </summary>
/// <param name="SupportsLightEnvironments">Whether any of the game's environment strategies are implemented (see <see cref="GetEnvironmentSupport"/>)</param>
/// <param name="CompositeDynamicLights">Installed [SystemSettings] CompositeDynamicLights: whether LE2/LE3 composite bypass lights</param>
internal readonly record struct GameLightingProfile(Vector4 WrapLightingParameters, bool SupportsLightEnvironments, bool CompositeDynamicLights = false)
{
    public static GameLightingProfile ForGame(MEGame game) => game switch
    {
        MEGame.LE1 => new(new Vector4(0.35f, 0.20f, 0, 0), true),
        MEGame.LE2 => new(new Vector4(0.15f, 0.025f, 0, 0), true, CompositeDynamicLights: false),
        MEGame.LE3 => new(new Vector4(0.33f, 0.05f, 0, 0), true, CompositeDynamicLights: false),
        // Original-trilogy game shaders and light environments aren't supported by this renderer.
        _ => default,
    };

    /// <summary>
    /// Null if the viewport implements the strategy these settings select; otherwise why it falls back
    /// </summary>
    public static string GetEnvironmentSupport(LightEnvironmentSettings settings)
    {
        if (!ForGame(settings.Game).SupportsLightEnvironments) return "light environments aren't implemented for this game";
        switch (settings.Game)
        {
            case MEGame.LE1:
                switch (settings.EffectiveLE1Quality)
                {
                    case LE1LightEnvironmentQuality.Unknown:
                        return "its LE1 quality (eQuality/eKismetOverrideQuality) isn't a known ELightEnvironmentQuality value";
                    case LE1LightEnvironmentQuality.CinematicPlusMod:
                        //The subject-culled modulated shadow's render path isn't traced
                        return "ELEQ_CinematicPlusMod culls its modulated shadow on the subject (bCullModulatedShadowOnSubject), which the viewport doesn't render";
                }
                //The native shadow helper only computes a modulation color for LightShadow_Modulate.
                if (settings.CastShadows && settings.LightShadowMode is LightEnvironmentShadowMode.ModulateBetter
                    && settings.EffectiveLE1Quality is not LE1LightEnvironmentQuality.Cinematic)
                    return "LightShadow_ModulateBetter environment shadows aren't supported";
                return null;
            case MEGame.LE2:
                if (settings.IsBio && settings.IsCinematicQuality)
                    return "LE2 cinematic Bio environments (key/fill/rim with pass limits and transitions) aren't implemented";
                return null;
            default:
                return null;
        }
    }
}
