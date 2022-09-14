﻿
#define FLAG_RECONSTRUCTNORMALZ (1 << 0)
#define FLAG_ALPHAASBLACK (1 << 1)
#define FLAG_ENABLEREDCHANNEL (1 << 2)
#define FLAG_ENABLEGREENCHANNEL (1 << 3)
#define FLAG_ENABLEBLUECHANNEL (1 << 4)
#define FLAG_ENABLEALPHACHANNEL (1 << 5)

struct VS_OUT {
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

cbuffer Constants {
    float4x4 Projection;
    float4x4 View;
    int Mip;
    int Flags;
    int TextureWidth;
    int TextureHeight;
};

Texture2D Texture : register(t0);
SamplerState Sampler : register(s0);

VS_OUT VSMain(uint vertexID : SV_VertexID) {
    VS_OUT output = (VS_OUT)0;
    
    output.TexCoord = float2(uint2(vertexID << 1, vertexID) & 2) * float2(0.5f, -0.5f); // (we're too good for vertex buffers)
    output.Position = float4(mul(Projection, mul(View, float4(output.TexCoord + float2(-0.5f, 0.5f), 0.0f, 1.0f))).xyz, 1.0f);

    // Stretch horizontally to match aspect ratio
    output.Position.x *= ((float)TextureWidth / TextureHeight);
    
    return output;
}

float4 PSMain(VS_OUT input) : SV_Target0 {
    float4 textureValue = Texture.SampleLevel(Sampler, float2(input.TexCoord.x, 1.0f - input.TexCoord.y), Mip);

    if ((Flags & FLAG_RECONSTRUCTNORMALZ) != 0) {
        float2 normalVector = textureValue.xy * 2.0f - 1.0f; // The texture uses values in 0.0 to 1.0 (0 to 255) to represent floats from -1.0 to 1.0
        textureValue.z = sqrt(1.0f - pow(normalVector.x, 2.0f) - pow(normalVector.y, 2.0f)) * 0.5f + 0.5f; // Pythagorean theorem solved for Z, then rescaled from [-1.0, 1.0] to the [0.0, 1.0] range
    }

    // If only the alpha flag is enabled, show the alpha as a black-and-white image
    if ((Flags & (FLAG_ENABLEALPHACHANNEL | FLAG_ENABLEREDCHANNEL | FLAG_ENABLEGREENCHANNEL | FLAG_ENABLEBLUECHANNEL)) == FLAG_ENABLEALPHACHANNEL) {
        textureValue = float4(textureValue.a, textureValue.a, textureValue.a, 1.0f);
    }
    else {
        // Mask out channels that don't have flags set for them
        if ((Flags & FLAG_ENABLEALPHACHANNEL) == 0) {
            textureValue.a = 1.0f; // Disabling the alpha channel means making it fully opaque
        }
        if ((Flags & FLAG_ENABLEREDCHANNEL) == 0) {
            textureValue.r = 0.0f;
        }
        if ((Flags & FLAG_ENABLEGREENCHANNEL) == 0) {
            textureValue.g = 0.0f;
        }
        if ((Flags & FLAG_ENABLEBLUECHANNEL) == 0) {
            textureValue.b = 0.0f;
        }
    }

    if ((Flags & FLAG_ALPHAASBLACK) != 0) {
        // Blend from black to the texture color according to the texture's alpha
        textureValue = lerp(float4(0.0f, 0.0f, 0.0f, 1.0f) /* black */, float4(textureValue.rgb, 1.0f) /* make the result opaque */, textureValue.a);
    }

    return textureValue;
}