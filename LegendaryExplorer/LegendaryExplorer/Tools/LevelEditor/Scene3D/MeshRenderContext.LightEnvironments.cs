using LegendaryExplorer.Misc;
using LegendaryExplorer.Resources;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using Color = System.Windows.Media.Color;
using D2D = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;
using LECTexture2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public partial class MeshRenderContext
{
    //light environments that have primitives, by their component (and the actor that owns them, since actors can share an archetype's)
    private readonly Dictionary<(ExportEntry owner, ExportEntry lightEnvironment), DynamicLightEnvironment> LightEnvironments = [];

    private readonly record struct WorldLightEnvironmentSettings(float CharacterLightingContrastFactor, bool AllowSphericalHarmonicLights,
        bool AllowModulateBetterShadows);

    //each level's persistent WorldInfo settings that light environments use. Guarded by LightEnvironments
    private readonly Dictionary<IMEPackage, WorldLightEnvironmentSettings> CachedWorldLightEnvironmentSettings = [];
    //Each game's installed environment defaults; guarded by LightEnvironments
    private readonly Dictionary<MEGame, LightEnvironmentSettings.InstalledDefaults> InstalledEnvironmentDefaults = [];

    /// <summary>
    /// Whether any of the game's light environment strategies are implemented; each environment's mode is checked separately
    /// </summary>
    internal static bool SupportsLightEnvironments(MEGame game) => GameLightingProfile.ForGame(game).SupportsLightEnvironments;

    /// <summary>
    /// Finds the light environment a primitive component is lit by: its LightEnvironment, unless that's disabled.
    /// Characters whose light environment can't be found (since it's set by a class's defaults that can't be read) get one with the class's defaults
    /// </summary>
    /// <param name="condensedProps">The component's properties, including inherited ones</param>
    /// <param name="owner">The actor that owns the component</param>
    /// <param name="lightEnvironment">The light environment, if it's used and its game and mode are supported.
    /// The component joins it with <see cref="AddLightEnvironmentPrimitive"/></param>
    /// <returns>Whether the component is lit by a light environment. False if its light environment can't be read</returns>
    internal bool ResolveLightEnvironment(ExportEntry componentExport, PropertyCollection condensedProps, ExportEntry owner, out DynamicLightEnvironment lightEnvironment) =>
        ResolveLightEnvironment(componentExport, condensedProps, owner, out lightEnvironment, out _);

    /// <param name="unsupportedReason">Why the component's light environment isn't rendered, when it uses one and <paramref name="lightEnvironment"/> is null</param>
    internal bool ResolveLightEnvironment(ExportEntry componentExport, PropertyCollection condensedProps, ExportEntry owner, out DynamicLightEnvironment lightEnvironment,
        out string unsupportedReason)
    {
        lightEnvironment = null;
        unsupportedReason = null;
        if (!EffectiveLightingProperties.UsesLightEnvironment(componentExport, condensedProps, owner, PackageCache, out ExportEntry lightEnvironmentExport))
        {
            return false;
        }
        if (!SupportsLightEnvironments(componentExport.Game))
        {
            unsupportedReason = "light environments aren't implemented for this game";
            return true;
        }
        try
        {
            //a shared light environment is its actor's. An inherited one with no instance of its own is still this actor's alone
            ExportEntry lightEnvironmentOwner = lightEnvironmentExport?.Parent is ExportEntry outer && outer != owner
                && EffectiveLightingProperties.IsPlacedActor(outer) ? outer : owner;
            lightEnvironment = GetLightEnvironment(lightEnvironmentOwner, lightEnvironmentExport, out unsupportedReason);
            return true;
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Could not read the light environment of {componentExport.InstancedFullPath}, so it's lit like other meshes: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// The light environment whose LightEnvironment component is <paramref name="lightEnvironmentExport"/>: the existing one, or a new one, which is only kept once
    /// a primitive joins it (<see cref="AddLightEnvironmentPrimitive"/>). Null if the game or the environment's mode isn't supported
    /// </summary>
    /// <param name="owner">The actor that owns the light environment</param>
    /// <param name="lightEnvironmentExport">Null for a character with the class's default light environment</param>
    private DynamicLightEnvironment GetLightEnvironment(ExportEntry owner, ExportEntry lightEnvironmentExport, out string unsupportedReason)
    {
        unsupportedReason = null;
        if ((owner ?? lightEnvironmentExport) is not { } entry || !SupportsLightEnvironments(entry.Game))
        {
            unsupportedReason = "light environments aren't implemented for this game";
            return null;
        }
        IMEPackage level = entry.FileRef;
        WorldLightEnvironmentSettings worldSettings;
        bool hasWorldSettings;
        //the lock is held briefly, since the render thread takes it every frame: the settings are read (from packages, on the loading thread) outside it
        lock (LightEnvironments)
        {
            if (LightEnvironments.TryGetValue((owner, lightEnvironmentExport), out DynamicLightEnvironment lightEnvironment))
            {
                return lightEnvironment;
            }
            hasWorldSettings = CachedWorldLightEnvironmentSettings.TryGetValue(level, out worldSettings);
        }
        if (!hasWorldSettings)
        {
            worldSettings = ReadWorldLightEnvironmentSettings(level);
            lock (LightEnvironments)
            {
                CachedWorldLightEnvironmentSettings[level] = worldSettings;
            }
        }
        LightEnvironmentSettings settings = lightEnvironmentExport is null ? LightEnvironmentSettings.CreateDefaults(entry.Game, isBio: true)
            : LightEnvironmentSettings.Read(lightEnvironmentExport, PackageCache);
        GetInstalledEnvironmentDefaults(entry.Game).ApplyTo(settings);
        settings.AllowModulateBetterShadows = worldSettings.AllowModulateBetterShadows;
        //Unimplemented modes fall back with their reason rather than borrowing another game's strategy
        if (GameLightingProfile.GetEnvironmentSupport(settings) is { } reason)
        {
            unsupportedReason = reason;
            return null;
        }
        //(if another thread made one for the same component meanwhile, AddLightEnvironmentPrimitive keeps whichever is added first)
        return new DynamicLightEnvironment(this, owner, lightEnvironmentExport, settings, worldSettings.CharacterLightingContrastFactor,
            worldSettings.AllowSphericalHarmonicLights);
    }

    private LightEnvironmentSettings.InstalledDefaults GetInstalledEnvironmentDefaults(MEGame game)
    {
        lock (LightEnvironments)
        {
            if (InstalledEnvironmentDefaults.TryGetValue(game, out var cached)) return cached;
        }
        //(read outside the lock; the first stored wins)
        var defaults = LightEnvironmentSettings.InstalledDefaults.Load(game, PackageCache);
        lock (LightEnvironments) return InstalledEnvironmentDefaults.TryAdd(game, defaults) ? defaults : InstalledEnvironmentDefaults[game];
    }

    /// <summary>
    /// The WorldInfo settings that light environments in a level use: CharacterLightingContrastFactor, bAllowLightEnvSphericalHarmonicLights,
    /// and bAllowModulateBetterShadows.
    /// The game reads them from the persistent level's WorldInfo (GWorld->GetWorldInfo(TRUE)), not the streamed-in level's own
    /// </summary>
    private WorldLightEnvironmentSettings ReadWorldLightEnvironmentSettings(IMEPackage level)
    {
        //Default__WorldInfo's values
        WorldLightEnvironmentSettings settings = new(1.5f, true, true);
        try
        {
            if (FindWorldInfo(FindPersistentLevel(level) ?? level) is { } worldInfo)
            {
                PropertyCollection worldProps = EffectiveLightingProperties.Read(worldInfo, PackageCache);
                settings = new WorldLightEnvironmentSettings(worldProps.GetProp<FloatProperty>("CharacterLightingContrastFactor")?.Value ?? 1.5f,
                    worldProps.GetProp<BoolProperty>("bAllowLightEnvSphericalHarmonicLights")?.Value ?? true,
                    worldProps.GetProp<BoolProperty>("bAllowModulateBetterShadows")?.Value ?? true);
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Could not read the WorldInfo of {level.FileNameNoExtension}'s persistent level, so the defaults are used: {e.Message}");
        }
        return settings;

        //LE3's levels have a BioWorldInfo
        static ExportEntry FindWorldInfo(IMEPackage pcc) =>
            pcc.Exports.FirstOrDefault(export => !export.IsDefaultObject && GlobalUnrealObjectInfo.IsA(export.ClassName, "WorldInfo", export.Game));
    }

    /// <summary>
    /// The persistent level that streams in <paramref name="level"/>: BioWare names it BioP_{map}, for the levels named Bio?_{map}_..., and its WorldInfo's
    /// StreamingLevels lists them. It's looked for beside <paramref name="level"/> first (see <see cref="RelatedLevelFiles"/>).
    /// Null if it can't be found, or <paramref name="level"/> is a persistent level
    /// </summary>
    private IMEPackage FindPersistentLevel(IMEPackage level)
    {
        string levelName = level.FileNameNoExtension;
        string[] nameParts = levelName.Split('_');
        if (levelName.StartsWith("BioP_", StringComparison.OrdinalIgnoreCase) || nameParts.Length < 2
            || RelatedLevelFiles.Find($"BioP_{nameParts[1]}.pcc", Path.GetDirectoryName(level.FilePath), level.Game) is not { } persistentPath
            || PackageCache.GetCachedPackage(persistentPath) is not { } persistent)
        {
            return null;
        }
        //it's only the persistent level if it streams this one in
        ExportEntry worldInfo = persistent.Exports.FirstOrDefault(export => !export.IsDefaultObject && GlobalUnrealObjectInfo.IsA(export.ClassName, "WorldInfo", export.Game));
        if (worldInfo?.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels") is not { } streamingLevels)
        {
            return null;
        }
        foreach (ObjectProperty streamingLevelProp in streamingLevels)
        {
            if (streamingLevelProp.ResolveToEntry(persistent) is ExportEntry streamingLevel
                && streamingLevel.GetProperty<NameProperty>("PackageName")?.Value.Instanced.Equals(levelName, StringComparison.OrdinalIgnoreCase) is true)
            {
                return persistent;
            }
        }
        return null;
    }

    /// <summary>
    /// Adds a primitive to a light environment from <see cref="ResolveLightEnvironment"/>, keeping the light environment
    /// </summary>
    /// <returns>The light environment the primitive joined: <paramref name="lightEnvironment"/>, unless another primitive's has been kept for the same component meanwhile</returns>
    internal DynamicLightEnvironment AddLightEnvironmentPrimitive(DynamicLightEnvironment lightEnvironment, LightEnvironmentPrimitive primitive)
    {
        lock (LightEnvironments)
        {
            if (!LightEnvironments.TryGetValue((lightEnvironment.Owner, lightEnvironment.Export), out DynamicLightEnvironment kept))
            {
                LightEnvironments.Add((lightEnvironment.Owner, lightEnvironment.Export), kept = lightEnvironment);
            }
            kept.AddPrimitive(primitive);
            return kept;
        }
    }

    internal DynamicLightEnvironment[] GetLightEnvironments()
    {
        lock (LightEnvironments) return [.. LightEnvironments.Values];
    }

    /// <summary>
    /// Removes a primitive from its light environment, which is forgotten once it has none
    /// </summary>
    internal void RemoveLightEnvironmentPrimitive(DynamicLightEnvironment lightEnvironment, LightEnvironmentPrimitive primitive)
    {
        //under the same lock as adding, so a primitive can't join a light environment that's being forgotten
        lock (LightEnvironments)
        {
            if (lightEnvironment.RemovePrimitive(primitive) == 0
                && LightEnvironments.TryGetValue((lightEnvironment.Owner, lightEnvironment.Export), out DynamicLightEnvironment kept) && kept == lightEnvironment)
            {
                LightEnvironments.Remove((lightEnvironment.Owner, lightEnvironment.Export));
            }
        }
    }
}
