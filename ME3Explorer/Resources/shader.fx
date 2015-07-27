struct VertexToPixel
{
	float4 Position		: POSITION;
	float4 Color		: COLOR0;
	float4 Position3D	: TEXCOORD0;
};

struct PixelToFrame
{
	float4 Color 		: COLOR0;
};

float4x4 xViewProjection;

VertexToPixel SimplestVertexShader( float4 inPos : POSITION, float4 inColor : COLOR0)
{	
	VertexToPixel Output = (VertexToPixel)0;    
	Output.Position =mul(inPos, xViewProjection);
	Output.Color.rgb = inPos.yxz;
	Output.Position3D = inPos;    
	return Output;    
}

PixelToFrame OurFirstPixelShader(VertexToPixel PSIn)
{
	PixelToFrame Output = (PixelToFrame)0; 	
	Output.Color.rgb = PSIn.Position3D.yxz; 
	return Output;
}

technique Simplest
{
    pass Pass0
    {        
	VertexShader = compile vs_1_1 SimplestVertexShader();
	PixelShader = compile ps_1_1 OurFirstPixelShader();
    }
}