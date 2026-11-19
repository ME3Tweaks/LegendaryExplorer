using System.Numerics;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Stock game lighting constants (BioEngine.ini [WrapLighting]) and the features the viewport implements
/// </summary>
/// <param name="SupportsLightEnvironments">Whether any of the game's environment strategies are implemented (see <see cref="GetEnvironmentSupport"/>)</param>
/// <param name="CompositeDynamicLights">Installed [SystemSettings] CompositeDynamicLights: whether LE2/LE3 composite bypass lights</param>
/// <param name="AllowBetterModulatedShadows">Installed [SystemSettings] bAllowBetterModulatedShadows (true in all three games): whether LightShadow_ModulateBetter
/// shadows are masked by their receivers</param>
internal readonly record struct GameLightingProfile(Vector4 WrapLightingParameters, bool SupportsLightEnvironments, bool CompositeDynamicLights = false,
    bool AllowBetterModulatedShadows = true)
{
    public static GameLightingProfile ForGame(MEGame game) => game switch
    {
        MEGame.LE1 => new(new Vector4(0.35f, 0.20f, 0, 0), true, AllowBetterModulatedShadows: true),
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
                }
                return null;
            default:
                return null;
        }
    }
}
