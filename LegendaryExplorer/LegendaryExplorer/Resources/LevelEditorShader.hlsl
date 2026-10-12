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

//LEVertex layout, used by meshes rendered with the game's shaders. Used when a material can't be rendered with game shaders
struct VS_IN_LEVERTEX {
    float4 pos : POSITION0;
    float3 tangent : TANGENT0;
    float4 normal : NORMAL0; //packed into [0,1], like the game's vertex data
    float4 color : COLOR1;
    float4 uv : TEXCOORD0;
};

VS_OUT VSMainLEVertex(VS_IN_LEVERTEX input) {
	VS_OUT result = (VS_OUT)0;

	result.pos = mul(float4(input.pos.xyz, 1), model);
	result.pos = mul(result.pos, view);
	result.pos = mul(result.pos, projection);

    result.normal = input.normal.xyz * 2 - 1;
	result.uv = input.uv.xy;
    result.color = input.color;

	return result;
}

//UE3's default display gamma
#define GAMMA 2.2

// Render flags
#define FLAG_ENABLEREDCHANNEL (1 << 2)
#define FLAG_ENABLEGREENCHANNEL (1 << 3)
#define FLAG_ENABLEBLUECHANNEL (1 << 4)
#define FLAG_ENABLEALPHACHANNEL (1 << 5)

//level editor flags
#define FLAG_UNLIT (1 << 28)
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
	if ((Flags & FLAG_UNLIT) != 0) lambert = 1.0;
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

	//The scene is rendered in linear space and gamma-encoded when resolved to the screen (see PSMainResolve).
	//This shader's colors are already gamma-encoded, so decode them
    result.color.rgb = pow(abs(result.color.rgb), GAMMA);

	return result;
}

//Fullscreen triangle, for copying the linear HDR scene color to the backbuffer
float4 VSMainResolve(uint id : SV_VertexID) : SV_POSITION {
    float2 uv = float2((id << 1) & 2, id & 2);
    return float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
}

//John Hable's filmic curve, with LE3's parameters (BioEngine.ini [FilmicTonemappingLUT])
float3 FilmicCurve(float3 x) {
    const float A = 0.6;   //ShoulderStrength
    const float B = 0.1;   //LinearStrength
    const float C = 0.3;   //LinearAngle
    const float D = 0.2;   //ToeStrength
    const float E = 0.025; //ToeNumerator
    const float F = 0.16;  //ToeDenominator
    return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

//Scene color alpha marks pixels rendered with the game's shaders (see PSMainHitProxy). Only those are tonemapped,
//so that LEX's shader, primitives, and the background look the same as they always have.
#define GAME_SHADER_PIXEL_ALPHA 2.0

//The scene targets are multisampled. MeshRenderContext compiles PSMainResolve with MSAA_SAMPLES defined as their sample count
#ifndef MSAA_SAMPLES
#define MSAA_SAMPLES 1
#endif
#if MSAA_SAMPLES > 1
Texture2DMS<float4, MSAA_SAMPLES> ResolveSceneColor : register(t0);
Texture2DMS<float4, MSAA_SAMPLES> ResolveHitTest : register(t1);
#define LOAD_SAMPLE(texture, pixel, sampleIndex) texture.Load(pixel, sampleIndex)
#else
Texture2D<float4> ResolveSceneColor : register(t0);
Texture2D<float4> ResolveHitTest : register(t1);
#define LOAD_SAMPLE(texture, pixel, sampleIndex) texture.Load(int3(pixel, 0))
#endif

struct PS_OUT_RESOLVE {
    float4 color : SV_TARGET0;
    float4 hitTestID : SV_TARGET1;
};

PS_OUT_RESOLVE PSMainResolve(float4 pos : SV_POSITION) {
    PS_OUT_RESOLVE result;
    int2 pixel = int2(pos.xy);
    //Each sample is tonemapped before averaging. Averaging HDR values first would let one very bright sample dominate the pixel, leaving aliased edges
    float3 sum = 0;
    [unroll]
    for (int i = 0; i < MSAA_SAMPLES; i++) {
        float4 sceneColor = LOAD_SAMPLE(ResolveSceneColor, pixel, i);
        float3 color = sceneColor.rgb;
        if (sceneColor.a > GAME_SHADER_PIXEL_ALPHA - 0.5) {
            //Approximates LE3's FSFXUberPostProcessBlendPixelShader with filmic tonemapping on (the default), and default color grading.
            //LE3 looks up sceneColor * 0.0616082214 in a LUT generated from the filmic curve, so the LUT spans [0, 16.23].
            //Normalizing so that the top of that range maps to white is an assumption; the LUT's generation hasn't been reverse engineered.
            //(LE3 also multiplies by (1.01036298, 1.00000572, 1.16309249) after gamma correction. That's left out: in game it's combined with
            //per-level color grading, and on its own it gives everything a purple cast)
            const float WhitePoint = 1 / 0.0616082214;
            color = FilmicCurve(clamp(color, 0, WhitePoint)) / FilmicCurve(WhitePoint);
        }
        sum += saturate(color);
    }
    result.color = float4(pow(sum / MSAA_SAMPLES, 1 / GAMMA), 1);
    //IDs can't be averaged, so one sample is copied
    result.hitTestID = LOAD_SAMPLE(ResolveHitTest, pixel, 0);
    return result;
}

//Game shaders don't write to the hit test render target, so meshes drawn with them are drawn a second time with this pixel shader,
//using the same vertex shader. It has no inputs, so that it is compatible with any vertex shader's outputs.
cbuffer HitProxyConstants : register(b3) {
    float3 HitProxyID;
    int HitProxyFlags;
};

struct PS_OUT_HITPROXY {
	float4 color : SV_TARGET0; //added to the scene color
    float4 hitTestID : SV_Target1;
};

PS_OUT_HITPROXY PSMainHitProxy() {
	PS_OUT_HITPROXY result;
    //rgb multiplies the scene color (selection highlight). alpha replaces it, marking the pixel as game-shaded for PSMainResolve
    result.color = (HitProxyFlags & FLAG_SELECTED) == FLAG_SELECTED ? float4(0.8, 0.8, 1.6, 2.0) : float4(1, 1, 1, 2.0);
    result.hitTestID = float4(HitProxyID, 1.0f);
	return result;
}
