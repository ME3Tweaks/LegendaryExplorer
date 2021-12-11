
#define FLAG_RECONSTRUCTNORMALZ (1 << 0)

struct VS_OUT {
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

cbuffer Constants {
    float4x4 Projection;
    float4x4 View;
    int Mip;
    int Flags;
    int2 _Padding;
};

Texture2D Texture : register(t0);
SamplerState Sampler : register(s0);

VS_OUT VSMain(uint vertexID : SV_VertexID) {
    VS_OUT output = (VS_OUT)0;
    
    output.TexCoord = float2(uint2(vertexID << 1, vertexID) & 2) * float2(0.5f, -0.5f);
    output.Position = float4(mul(Projection, mul(View, float4(output.TexCoord + float2(-0.5f, 0.5f), 0.0f, 1.0f))).xyz, 1.0f);
    
    return output;
}

float4 PSMain(VS_OUT input) : SV_Target0 {
    float4 textureValue = Texture.SampleLevel(Sampler, float2(input.TexCoord.x, 1.0f - input.TexCoord.y), Mip);

    if (Flags & FLAG_RECONSTRUCTNORMALZ != 0) {
        float2 normalVector = textureValue.xy * 2.0f - 1.0f; // The texture uses values in 0.0 to 1.0 (0 to 255) to represent floats from -1.0 to 1.0
        textureValue.z = sqrt(1.0f - pow(normalVector.x, 2.0f) - pow(normalVector.y, 2.0f)) * 0.5f + 0.5f; // Pythagorean theorem solved for Z, then rescale from -1.0 to 1.0 to the 0.0 to 1.0 range
    }

    return textureValue;
}