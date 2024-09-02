using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;

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
            throw new NotImplementedException();
        }
    }
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderParameter SpotAngles;
        public FShaderParameter SpotDirection;
        public FShaderParameter LightColorAndFalloffExponent;
        public void Serialize(SerializingContainer sc)
        {
            throw new NotImplementedException();
        }
    }
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightPositionAndInvRadius;
        public void Serialize(SerializingContainer sc)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
    public struct ModShadowPixelParamsType : IModShadowPixelParamsType
    {
        public FShaderParameter LightPositionParam;
        public FShaderParameter FalloffParameters;
        public void Serialize(SerializingContainer sc)
        {
            throw new NotImplementedException();
        }
    }
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightPositionAndInvRadius;
        public void Serialize(SerializingContainer sc)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightDirection;
        public void Serialize(SerializingContainer sc)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightmapCoordinateScaleBias;
        public void Serialize(SerializingContainer sc)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderResourceParameter ShadowTexture;
        public void Serialize(SerializingContainer sc)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
    public struct VertexParametersType : IVertexParametersType
    {
        public FShaderParameter LightPositionAndInvRadius;
        public FShaderParameter ShadowViewProjection;
        public void Serialize(SerializingContainer sc)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
    public struct PixelParametersType : IPixelParametersType
    {
        public FShaderParameter LightColorAndFalloffExponent;
        public FShaderParameter bReceiveDynamicShadows;
        public void Serialize(SerializingContainer sc)
        {
            throw new NotImplementedException();
        }
    }
}