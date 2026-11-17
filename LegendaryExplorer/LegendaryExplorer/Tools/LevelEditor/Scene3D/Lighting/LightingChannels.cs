using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// UE3's FLightingChannelContainer: a light only affects primitives it shares a channel with
/// </summary>
public readonly record struct LightingChannels(uint Channels)
{
    /// <summary>
    /// The channels' property names, in bit order
    /// </summary>
    public static IReadOnlyList<string> Names => ChannelNames;

    private static readonly string[] ChannelNames =
    [
        "BSP", "Static", "Dynamic", "CompositeDynamic", "Skybox",
        "Unnamed_1", "Unnamed_2", "Unnamed_3", "Unnamed_4", "Unnamed_5", "Unnamed_6",
        "Cinematic_1", "Cinematic_2", "Cinematic_3", "Cinematic_4", "Cinematic_5", "Cinematic_6", "Cinematic_7", "Cinematic_8", "Cinematic_9", "Cinematic_10",
        "Gameplay_1", "Gameplay_2", "Gameplay_3", "Gameplay_4", "Crowd"
    ];

    /// <summary>
    /// Default__LightComponent's channels
    /// </summary>
    public static readonly LightingChannels LightDefault = FromNames("BSP", "Static", "Dynamic", "CompositeDynamic");

    /// <summary>
    /// UE3 initializes an unset primitive's channels to Static if it has static shadowing (as static meshes in levels do)
    /// </summary>
    public static readonly LightingChannels StaticPrimitiveDefault = FromNames("Static");

    /// <summary>
    /// UE3 initializes an unset primitive's channels to Dynamic if it doesn't have static shadowing, as light environments' primitives don't
    /// </summary>
    public static readonly LightingChannels DynamicPrimitiveDefault = FromNames("Dynamic");

    public static readonly uint DynamicBit = DynamicPrimitiveDefault.Channels;
    public static readonly uint CompositeDynamicBit = FromNames("CompositeDynamic").Channels;

    public bool OverlapsWith(LightingChannels other) => (Channels & other.Channels) != 0;

    private static LightingChannels FromNames(params string[] names)
    {
        uint channels = 0;
        foreach (string name in names)
        {
            channels |= 1u << Array.IndexOf(ChannelNames, name);
        }
        return new LightingChannels(channels);
    }

    /// <summary>
    /// Reads an object's LightingChannels after its inherited struct fields have been condensed
    /// </summary>
    /// <param name="baseChannels">The value at the root of the archetype chain</param>
    /// <param name="isInitialized">Whether <paramref name="baseChannels"/> is initialized (bInitialized)</param>
    /// <param name="uninitializedDefault">What UE3 sets the channels to if they're never initialized</param>
    internal static LightingChannels FromProperty(StructProperty lightingChannels, LightingChannels baseChannels, bool isInitialized, LightingChannels uninitializedDefault)
    {
        uint channels = baseChannels.Channels;
        if (lightingChannels is not null)
        {
            foreach (Property prop in lightingChannels.Properties)
            {
                if (prop is not BoolProperty boolProp)
                {
                    continue;
                }
                //matched by instanced name, since names like Unnamed_3 are stored as a name and a number
                string name = prop.Name.Instanced;
                if (name == "bInitialized")
                {
                    isInitialized = boolProp.Value;
                }
                else if (Array.IndexOf(ChannelNames, name) is int index and >= 0)
                {
                    channels = boolProp.Value ? channels | 1u << index : channels & ~(1u << index);
                }
            }
        }
        return isInitialized ? new LightingChannels(channels) : uninitializedDefault;
    }
}
