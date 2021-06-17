
struct VS_OUT {
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

cbuffer Constants {
    float4x4 Projection;
    float4x4 View;
    int4 Mip;
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
    float4 textureValue = Texture.SampleLevel(Sampler, input.TexCoord, Mip.x);
    return textureValue;
}