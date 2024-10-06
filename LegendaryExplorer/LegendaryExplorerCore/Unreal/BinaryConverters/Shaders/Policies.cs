// ReSharper disable InconsistentNaming

namespace LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;

//These are seperate interfaces to ensure correct usage as Type params
public interface IModShadowPixelParamsType
{
    void Serialize(SerializingContainer sc);
}
public interface IPixelParametersType
{
    void Serialize(SerializingContainer sc);
}
public interface IVertexParametersType
{
    void Serialize(SerializingContainer sc);
}
public interface IVertexShaderParametersType
{
    void Serialize(SerializingContainer sc);
}

//Policies aren't serialized with SerializeUnmanaged because FNullPolicy needs to serialize as 0 bytes,
//but structs have a minimum size of 1
public struct FNullPolicy : IModShadowPixelParamsType, IPixelParametersType, IVertexParametersType, IVertexShaderParametersType
{
    public readonly void Serialize(SerializingContainer sc){}
}

public static class FSpotLightPolicy
{
    public struct ModShadowPixelParamsType : IModShadowPixelParamsType
    {
        public FShaderParameter LightPositionParam;
        public FShaderParameter FalloffParameters;
        public FShaderParameter SpotDirectionParam;
        public FShaderParameter SpotAnglesParam;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightPositionParam);
            sc.SerializeUnmanaged(ref FalloffParameters);
            sc.SerializeUnmanaged(ref SpotDirectionParam);
            sc.SerializeUnmanaged(ref SpotAnglesParam);
        }
    }
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderParameter SpotAngles;
        public FShaderParameter SpotDirection;
        public FShaderParameter LightColorAndFalloffExponent;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref SpotAngles);
            sc.SerializeUnmanaged(ref SpotDirection);
            sc.SerializeUnmanaged(ref LightColorAndFalloffExponent);
        }
    }
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightPositionAndInvRadius;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightPositionAndInvRadius);
        }
    }
}

public static class FPointLightPolicy
{
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderParameter WorldIncidentLighting;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref WorldIncidentLighting);
        }
    }
    public struct ModShadowPixelParamsType : IModShadowPixelParamsType
    {
        public FShaderParameter LightPositionParam;
        public FShaderParameter FalloffParameters;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightPositionParam);
            sc.SerializeUnmanaged(ref FalloffParameters);
        }
    }
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightPositionAndInvRadius;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightPositionAndInvRadius);
        }
    }
}

public static class FSHLightLightMapPolicy
{
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderParameter LightColorAndFalloffExponent;
        public FShaderParameter bReceiveDynamicShadows;
        public FShaderParameter WorldIncidentLighting;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightColorAndFalloffExponent);
            sc.SerializeUnmanaged(ref bReceiveDynamicShadows);
            sc.SerializeUnmanaged(ref WorldIncidentLighting);
        }
    }
}

public static class FSignedDistanceFieldShadowTexturePolicy
{
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderParameter DistanceFieldParameters;
        public FShaderResourceParameter ShadowTexture;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref DistanceFieldParameters);
            sc.SerializeUnmanaged(ref ShadowTexture);
        }
    }
}

public static class FSphericalHarmonicLightPolicy
{
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderParameter WorldIncidentLighting;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref WorldIncidentLighting);
        }
    }
}

public static class FCustomLightMapTexturePolicy
{
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderResourceParameter LightMapTextures;
        public FShaderParameter LightMapScale;
        public FShaderParameter LightMapBias;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightMapTextures);
            sc.SerializeUnmanaged(ref LightMapScale);
            sc.SerializeUnmanaged(ref LightMapBias);
        }
    }
}

public static class FDirectionalLightPolicy
{
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderParameter LightColor;
        public FShaderParameter bReceiveDynamicShadows;
        public FShaderParameter bEnableDistanceShadowFading;
        public FShaderParameter DistanceFadeParameters;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightColor);
            sc.SerializeUnmanaged(ref bReceiveDynamicShadows);
            sc.SerializeUnmanaged(ref bEnableDistanceShadowFading);
            sc.SerializeUnmanaged(ref DistanceFadeParameters);
        }
    }
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightDirection;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightDirection);
        }
    }
}

public static class FLightMapTexturePolicy
{
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderResourceParameter LightMapTextures;
        public FShaderParameter LightMapScale;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightMapTextures);
            sc.SerializeUnmanaged(ref LightMapScale);
        }
    }
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightmapCoordinateScaleBias;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightmapCoordinateScaleBias);
        }
    }
}

public static class FShadowTexturePolicy
{
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightmapCoordinateScaleBias;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightmapCoordinateScaleBias);
        }
    }
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderResourceParameter ShadowTexture;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref ShadowTexture);
        }
    }
}

public static class FSFXPointLightPolicy
{
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderResourceParameter LightSpaceShadowMap;
        public FShaderParameter LightColorAndFalloffExponent;
        public FShaderParameter ShadowFilter;
        public FShaderParameter ShadowTextureRegion;
        public FShaderParameter MaxVarianceShadowAttenuation;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightSpaceShadowMap);
            sc.SerializeUnmanaged(ref LightColorAndFalloffExponent);
            sc.SerializeUnmanaged(ref ShadowFilter);
            sc.SerializeUnmanaged(ref ShadowTextureRegion);
            sc.SerializeUnmanaged(ref MaxVarianceShadowAttenuation);
        }
    }
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightPositionAndInvRadius;
        public FShaderParameter ShadowViewProjection;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightPositionAndInvRadius);
            sc.SerializeUnmanaged(ref ShadowViewProjection);
        }
    }
}

public static class FVertexLightMapPolicy
{
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightMapScale;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightMapScale);
        }
    }
}

public static class FConstantDensityPolicy
{
    public struct VertexShaderParametersType : IVertexShaderParametersType
    {
        public FShaderParameter FirstDensityFunction;
        public FShaderParameter SecondDensityFunction;
        public FShaderParameter StartDistance;
        public FShaderParameter FogVolumeBoxMin;
        public FShaderParameter FogVolumeBoxMax;
        public FShaderParameter ApproxFogColor;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref FirstDensityFunction);
            sc.SerializeUnmanaged(ref SecondDensityFunction);
            sc.SerializeUnmanaged(ref StartDistance);
            sc.SerializeUnmanaged(ref FogVolumeBoxMin);
            sc.SerializeUnmanaged(ref FogVolumeBoxMax);
            sc.SerializeUnmanaged(ref ApproxFogColor);
        }
    }
}

public static class FDirectionalLightLightMapPolicy
{
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightDirectionAndbDirectional;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightDirectionAndbDirectional);
        }
    }
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderParameter LightColorAndFalloffExponent;
        public FShaderParameter bReceiveDynamicShadows;
        public void Serialize(SerializingContainer sc)
        {
            sc.SerializeUnmanaged(ref LightColorAndFalloffExponent);
            sc.SerializeUnmanaged(ref bReceiveDynamicShadows);
        }
    }
}