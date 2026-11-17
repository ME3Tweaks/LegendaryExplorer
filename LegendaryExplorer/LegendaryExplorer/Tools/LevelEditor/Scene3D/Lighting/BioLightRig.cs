using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Coalesced.Config;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// LE3's SFXLightRig, named by a cinematic Bio environment's CinematicLightRig: lighting captured for the character, whose SH and shadow replace
/// the level's lights (RVA 0x1a4840), and scales for the key, fill and ambient
/// </summary>
public sealed record BioLightRig(SHVectorRGB Shadowed, SHVectorRGB Unshadowed, Vector3 ShadowDirection, Vector3 TotalShadowIntensity,
    Vector3 KeyLightModifier, Vector3 FillLightModifier, Vector3 AmbientLightModifier)
{
    internal static BioLightRig Read(ExportEntry rig, PackageCache cache)
    {
        PropertyCollection props = EffectiveLightingProperties.Read(rig, cache);
        return new BioLightRig(ReadSH("LightEnvironment"), ReadSH("NonShadowedLightEnvironment"),
            props.GetProp<StructProperty>("ShadowDirection") is { } direction ? CommonStructs.GetVector3(direction) : Vector3.Zero,
            ReadColor("TotalShadowIntensity", Vector3.Zero), ReadColor("KeyLightModifier", Vector3.One), ReadColor("FillLightModifier", Vector3.One),
            ReadColor("AmbientLightModifier", Vector3.One));

        Vector3 ReadColor(string name, Vector3 fallback)
        {
            if (props.GetProp<StructProperty>(name) is not { } prop) return fallback;
            LinearColor color = CommonStructs.GetLinearColor(prop);
            return new Vector3(color.R, color.G, color.B);
        }

        SHVectorRGB ReadSH(string name)
        {
            var sh = new SHVectorRGB();
            if (props.GetProp<StructProperty>(name) is { } prop)
            {
                sh.R = ReadChannel(prop, "R");
                sh.G = ReadChannel(prop, "G");
                sh.B = ReadChannel(prop, "B");
            }
            return sh;
        }

        static SHVector ReadChannel(StructProperty rgb, string name)
        {
            var channel = new SHVector();
            if (rgb.Properties.GetProp<StructProperty>(name) is { } prop)
            {
                foreach (FloatProperty v in prop.Properties.OfType<FloatProperty>())
                {
                    if (v.Name == "V" && v.StaticArrayIndex < SHVector.NumCoefficients) channel[v.StaticArrayIndex] = v.Value;
                }
            }
            return channel;
        }
    }

    /// <summary>The captured SH turned by LightRigOrientation about Z</summary>
    internal static SHVectorRGB Orient(SHVectorRGB sh, float degrees)
    {
        float angle = (float)(degrees * (Math.PI / 180));
        return new SHVectorRGB { R = RotateZ(sh.R, angle), G = RotateZ(sh.G, angle), B = RotateZ(sh.B, angle) };
    }

    /// <summary>
    /// LE3's SH rotation about Z (RVA 0xb6050), as it runs: coefficients 1 and 5 take 2 and 6 where a rotation takes 3 and 7
    /// This seems like a bug, but we should match the game's behavior
    /// </summary>
    internal static SHVector RotateZ(SHVector sh, float angle)
    {
        (float s, float c) = MathF.SinCos(angle);
        (float s2, float c2) = MathF.SinCos(2 * angle);
        var rotated = new SHVector();
        rotated[0] = sh[0];
        rotated[1] = sh[1] * c - sh[2] * s;
        rotated[2] = sh[2];
        rotated[3] = sh[3] * c + sh[1] * s;
        rotated[4] = sh[4] * c2 - sh[8] * s2;
        rotated[5] = sh[5] * c - sh[6] * s;
        rotated[6] = sh[6];
        rotated[7] = sh[7] * c + sh[5] * s;
        rotated[8] = sh[8] * c2 + sh[4] * s2;
        return rotated;
    }
}
