struct VS_IN {
    float4 pos : POSITION0;
    float3 hitTestID : TANGENT0;
    float4 normal : NORMAL0;
    float4 color : COLOR1;
    float2 uv : TEXCOORD0;
};

struct VS_OUT {
    float4 pos : SV_POSITION;
    float4 color : COLOR1;
	float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float3 hitTestID : COLOR2;
};

struct PS_IN {
    float4 pos : SV_POSITION;
    float4 color : COLOR1;
	float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float3 hitTestID : COLOR2;
};

struct PS_OUT {
	float4 color : SV_TARGET0;
    float4 hitTestID : SV_Target1;
};

//reminder: Constant buffers must be a multiple of 16 bytes long
cbuffer constants {
	float4x4 projection;
	float4x4 view;
	float4x4 model;
    float3 HitTestID;
	int Flags;
};

Texture2D tex : register(t0);
SamplerState samstate : register(s0);

VS_OUT VSMain(VS_IN input) {
	VS_OUT result = (VS_OUT)0;

	// Transform the input object-space position into a screen-space position
	result.pos = mul(float4(input.pos.xyz, 1), model);
	result.pos = mul(result.pos, view);
	result.pos = mul(result.pos, projection);

	// Pass through
    result.normal = input.normal.xyz;
	result.uv = input.uv;
    result.color = input.color;
    result.hitTestID = input.hitTestID;
	
	return result;
}

// Render flags
#define FLAG_ENABLEREDCHANNEL (1 << 2)
#define FLAG_ENABLEGREENCHANNEL (1 << 3)
#define FLAG_ENABLEBLUECHANNEL (1 << 4)
#define FLAG_ENABLEALPHACHANNEL (1 << 5)

//level editor flags
#define FLAG_WIREFRAME (1 << 29)
#define FLAG_SELECTED (1 << 30)
#define FLAG_PRIMITIVE (1 << 31)

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
	
    if ((Flags & FLAG_SELECTED) == FLAG_SELECTED)
    {
        result.color.b *= 2;
        if ((Flags & FLAG_WIREFRAME) == FLAG_WIREFRAME)
        {
            result.color.rgba = float4(1.0, 1.0, 0, 1.0);
        }
    }
	
	//the second render target is used for hit testing (clicking)
    result.hitTestID = float4(HitTestID, 1.0f);
	
	//ignore all that, and use vertex info
    if ((Flags & FLAG_PRIMITIVE) == FLAG_PRIMITIVE)
    {
        result.color = input.color;
        result.hitTestID = float4(input.hitTestID, 1.0f);
    }
	
	return result;
}
