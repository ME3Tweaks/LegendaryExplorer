// This has to match the data in the vertex buffer.
struct VS_IN {
	float3 pos : POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD0;
};

struct VS_OUT {
	float4 pos : SV_POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD0;
};

struct PS_IN {
	float4 pos : SV_POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD0;
};

struct PS_OUT {
	float4 color : SV_TARGET;
};

cbuffer constants {
	float4x4 projection;
	float4x4 view;
	float4x4 model;
	int Flags; // 196 len
	int4 padding1; // Constant buffers must be a multiple of 16 bytes long
	int4 padding2; // Constant buffers must be a multiple of 16 bytes long
	int4 padding3; // Constant buffers must be a multiple of 16 bytes long
};

Texture2D tex : register(t0);
SamplerState samstate : register(s0);

VS_OUT VSMain(VS_IN input) {
	VS_OUT result = (VS_OUT)0;

	// Transform the input object-space position into a screen-space position
	result.pos = mul(float4(input.pos, 1), model);
	result.pos = mul(result.pos, view);
	result.pos = mul(result.pos, projection);

	// Pass through the normal
	result.normal = input.normal;

	// Pass through the uv coordinate
	result.uv = input.uv;

	return result;
}

// Render flags
#define FLAG_ENABLEREDCHANNEL (1 << 2)
#define FLAG_ENABLEGREENCHANNEL (1 << 3)
#define FLAG_ENABLEBLUECHANNEL (1 << 4)
#define FLAG_ENABLEALPHACHANNEL (1 << 5)

PS_OUT PSMain(PS_IN input) {
	PS_OUT result = (PS_OUT)0;

	// just color everything white
	//result.color = float4(1.0, 1.0, 1.0, 1.0);

	// use the texture
	//result.color = tex2D(sam, input.uv);
	
	// use the texture with some primitive lambert shading
	float4 textureValue = tex.Sample(samstate, input.uv);

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
	
	float3 toLight = normalize(float3(0.6, 1, 0.3)); // the direction to the fake directional light
	float lambert = saturate(dot(toLight, input.normal));
	lambert = lambert * 0.5 + 0.5; // a super simple way to fake some ambient lighting in. wildly inaccurate though.
	result.color = float4(textureValue.xyz * lambert, 1.0);
	
	// use the input normal (negative values are clamped to zero (black))
	//result.color = float4(input.normal, 1.0);

	return result;
}
