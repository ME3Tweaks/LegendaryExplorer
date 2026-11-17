using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// UE3's FSHVector: the coefficients of a function on the sphere, projected onto the first three bands of the real spherical harmonics (9 coefficients)
/// </summary>
[InlineArray(NumCoefficients)]
public struct SHVector
{
    public const int NumCoefficients = 9;

    private float _element0;

    //sqrt(1 / (4 * PI)), the constant basis function
    private const float Y00 = 0.2820948f;

    /// <summary>
    /// The integral of the constant basis function over the sphere, divided by <see cref="Y00"/>: 2 * sqrt(PI). (UE3's DAT for CalcIntegral)
    /// </summary>
    private const float TwoSqrtPi = 3.5449078f;

    /// <summary>
    /// The projection of a constant function of 1/(4π) over the sphere (UE3's global ambient function)
    /// </summary>
    public static SHVector AmbientFunction
    {
        get
        {
            var sh = new SHVector();
            sh[0] = Y00;
            return sh;
        }
    }

    /// <summary>
    /// UE3's UpperSkyFunction: the projection of a function that is 1 over the upper hemisphere (world +Z) and 0 below, divided by π
    /// </summary>
    public static SHVector UpperSkyFunction
    {
        get
        {
            var sh = new SHVector();
            sh[0] = 0.5641896f;
            sh[2] = 0.4886025f;
            return sh;
        }
    }

    /// <summary>
    /// UE3's LowerSkyFunction: the projection of a function that is 1 over the lower hemisphere (world -Z) and 0 above, divided by π
    /// </summary>
    public static SHVector LowerSkyFunction
    {
        get
        {
            var sh = new SHVector();
            sh[0] = 0.5641896f;
            sh[2] = -0.4886025f;
            return sh;
        }
    }

    /// <summary>
    /// SHBasisFunction: the value of each basis function in the given direction, which must be normalized
    /// </summary>
    public static SHVector BasisFunction(Vector3 v)
    {
        var sh = new SHVector();
        sh[0] = Y00;
        sh[1] = -0.48860299f * v.Y;
        sh[2] = 0.48860299f * v.Z;
        sh[3] = -0.48860299f * v.X;
        sh[4] = 1.0925480f * v.X * v.Y;
        sh[5] = -1.0925480f * v.Y * v.Z;
        sh[6] = 0.94617593f * v.Z * v.Z - 0.31539199f;
        sh[7] = -1.0925480f * v.X * v.Z;
        sh[8] = 0.54627401f * (v.X * v.X - v.Y * v.Y);
        return sh;
    }

    public readonly float Dot(in SHVector other)
    {
        float result = 0;
        for (int i = 0; i < NumCoefficients; i++)
        {
            result += this[i] * other[i];
        }
        return result;
    }

    /// <summary>
    /// The integral of the function over the sphere, as UE3 scales the constant coefficient to get it
    /// </summary>
    public readonly float CalcIntegral() => this[0] * TwoSqrtPi;

    public static SHVector operator *(in SHVector sh, float scale)
    {
        var result = new SHVector();
        for (int i = 0; i < NumCoefficients; i++)
        {
            result[i] = sh[i] * scale;
        }
        return result;
    }

    public static SHVector operator +(in SHVector a, in SHVector b)
    {
        var result = new SHVector();
        for (int i = 0; i < NumCoefficients; i++)
        {
            result[i] = a[i] + b[i];
        }
        return result;
    }
}

/// <summary>
/// UE3's FSHVectorRGB: an <see cref="SHVector"/> per color channel, of incident radiance
/// </summary>
public struct SHVectorRGB
{
    public SHVector R;
    public SHVector G;
    public SHVector B;

    /// <summary>
    /// The basis function scaled by a color: the SH of light of that color arriving from one direction (or one hemisphere, for the sky functions)
    /// </summary>
    public static SHVectorRGB FromColor(in SHVector basis, Vector3 color) => new()
    {
        R = basis * color.X,
        G = basis * color.Y,
        B = basis * color.Z,
    };

    public static SHVectorRGB operator +(in SHVectorRGB a, in SHVectorRGB b) => new() { R = a.R + b.R, G = a.G + b.G, B = a.B + b.B };

    public static SHVectorRGB operator -(in SHVectorRGB a, in SHVectorRGB b) => a + b * -1;

    public static SHVectorRGB operator *(in SHVectorRGB sh, float scale) => new() { R = sh.R * scale, G = sh.G * scale, B = sh.B * scale };

    /// <summary>
    /// Scales each channel by the corresponding component of <paramref name="scale"/>
    /// </summary>
    public static SHVectorRGB operator *(in SHVectorRGB sh, Vector3 scale) => new() { R = sh.R * scale.X, G = sh.G * scale.Y, B = sh.B * scale.Z };

    /// <summary>
    /// GetLuminance: 0.3 R + 0.59 G + 0.11 B
    /// </summary>
    public readonly SHVector GetLuminance() => R * 0.3f + G * 0.59f + B * 0.11f;

    /// <summary>
    /// The color whose light from the shape of <paramref name="basis"/> best matches this, clamped to be non-negative: Dot(this, basis) / Dot(basis, basis)
    /// </summary>
    public readonly Vector3 Project(in SHVector basis)
    {
        float invBasisDot = 1 / basis.Dot(basis);
        return Vector3.Max(Vector3.Zero, new Vector3(R.Dot(basis), G.Dot(basis), B.Dot(basis)) * invBasisDot);
    }

    /// <summary>
    /// The integral of each channel over the sphere
    /// </summary>
    public readonly Vector3 CalcIntegral() => new(R.CalcIntegral(), G.CalcIntegral(), B.CalcIntegral());

    /// <summary>
    /// The coefficients in the layout of FSHLightLightMapPolicy's WorldIncidentLighting[7]: the constant terms of R, G and B, then the other 8 coefficients
    /// of each channel in turn, 4 per vector
    /// </summary>
    public readonly LegendaryExplorerCore.Gammtek.Fixed7<Vector4> ToWorldIncidentLighting()
    {
        var packed = new LegendaryExplorerCore.Gammtek.Fixed7<Vector4>();
        packed[0] = new Vector4(R[0], G[0], B[0], 0);
        packed[1] = new Vector4(R[1], R[2], R[3], R[4]);
        packed[2] = new Vector4(R[5], R[6], R[7], R[8]);
        packed[3] = new Vector4(G[1], G[2], G[3], G[4]);
        packed[4] = new Vector4(G[5], G[6], G[7], G[8]);
        packed[5] = new Vector4(B[1], B[2], B[3], B[4]);
        packed[6] = new Vector4(B[5], B[6], B[7], B[8]);
        return packed;
    }

    /// <summary>
    /// Removes the light of the shape of <paramref name="basis"/> that best matches this, returning its color. (UE3's pattern of projecting then subtracting)
    /// </summary>
    public Vector3 Extract(in SHVector basis, Vector3? colorOverride = null)
    {
        Vector3 color = colorOverride ?? Project(basis);
        this -= FromColor(basis, color);
        return color;
    }
}

internal static class LinearColorUtils
{
    /// <summary>
    /// FLinearColor::Desaturate: lerps towards the color's luminance (0.3 R + 0.59 G + 0.11 B)
    /// </summary>
    public static Vector3 Desaturate(Vector3 color, float desaturation)
    {
        float luminance = Luminance(color);
        return color + (new Vector3(luminance) - color) * desaturation;
    }

    public static float Luminance(Vector3 color) => color.X * 0.3f + color.Y * 0.59f + color.Z * 0.11f;
}
