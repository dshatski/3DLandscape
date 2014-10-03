//----------------------------------------------------
//--                                                --
//--               www.riemers.net                  --
//--         Series 4: Advanced terrain             --
//--                 Shader code                    --
//--                                                --
//----------------------------------------------------

//------- Constants --------
float4x4 xView;
float4x4 xProjection;
float4x4 xWorld;
float3 xLightDirection;
float3 xCamPos;
float xAmbient;
bool xEnableLighting;

//------- Technique: Clipping Plane Fix --------
bool Clipping;
float4 ClipPlane0;
//------- Technique: Clipping Plane Fix --------

Texture xTexture;
Texture xTexture0;

Texture xRefractionMap;

float4x4 xReflectionView;
Texture xReflectionMap;

Texture xWaterBumpMap;
float xWaveLength;
float xWaveHeight;

//- moving water support
float xTime;
float3 xWindDirection; //- direction of ripples
float xWindForce; //- how fast the ripples scroll through water

//------- Texture Samplers --------
sampler TextureSampler = sampler_state { texture = <xTexture>; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = mirror; AddressV = mirror;};

sampler TextureSampler0 = sampler_state { texture = <xTexture0> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = wrap; AddressV = wrap;};Texture xTexture1;
sampler TextureSampler1 = sampler_state { texture = <xTexture1> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = wrap; AddressV = wrap;};Texture xTexture2;
sampler TextureSampler2 = sampler_state { texture = <xTexture2> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = mirror; AddressV = mirror;};Texture xTexture3;
sampler TextureSampler3 = sampler_state { texture = <xTexture3> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = mirror; AddressV = mirror;};

sampler ReflectionSampler = sampler_state { texture = <xReflectionMap> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = mirror; AddressV = mirror;};
sampler RefractionSampler = sampler_state { texture = <xRefractionMap> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = mirror; AddressV = mirror;};

sampler WaterBumpMapSampler = sampler_state { texture = <xWaterBumpMap> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = mirror; AddressV = mirror;};
//------- Technique: Colored --------
struct ColVertexToPixel
{
    float4 Position   	: POSITION;    
    float4 Color		: COLOR0;
    float LightingFactor: TEXCOORD0;
};

struct ColPixelToFrame
{
    float4 Color : COLOR0;
};

ColVertexToPixel ColoredVS( float4 inPos : POSITION, float4 inColor: COLOR, float3 inNormal: NORMAL)
{	
	ColVertexToPixel Output = (ColVertexToPixel)0;
	float4x4 preViewProjection = mul (xView, xProjection);
	float4x4 preWorldViewProjection = mul (xWorld, preViewProjection);
    
	Output.Position = mul(inPos, preWorldViewProjection);
	Output.Color = inColor;
	
	float3 Normal = normalize(mul(normalize(inNormal), xWorld));	
	Output.LightingFactor = 1;
	if (xEnableLighting)
		Output.LightingFactor = saturate(dot(Normal, -xLightDirection));
    
	return Output;    
}

ColPixelToFrame ColoredPS(ColVertexToPixel PSIn) 
{
	ColPixelToFrame Output = (ColPixelToFrame)0;		
    
	Output.Color = PSIn.Color;
	Output.Color.rgb *= saturate(PSIn.LightingFactor + xAmbient);	
	
	return Output;
}

technique Colored
{
	pass Pass0
    {   
    	VertexShader = compile vs_1_1 ColoredVS();
        PixelShader  = compile ps_2_0 ColoredPS();
    }
}

//------- Technique: Textured --------
struct TexVertexToPixel
{
    float4 Position   	: POSITION;    
    float4 Color		: COLOR0;
    float LightingFactor: TEXCOORD0;
    float2 TextureCoords: TEXCOORD1;
};

struct TexPixelToFrame
{
    float4 Color : COLOR0;
};

TexVertexToPixel TexturedVS( float4 inPos : POSITION, float3 inNormal: NORMAL, float2 inTexCoords: TEXCOORD0)
{	
	TexVertexToPixel Output = (TexVertexToPixel)0;
	float4x4 preViewProjection = mul (xView, xProjection);
	float4x4 preWorldViewProjection = mul (xWorld, preViewProjection);
    
	Output.Position = mul(inPos, preWorldViewProjection);	
	Output.TextureCoords = inTexCoords;
	
	float3 Normal = normalize(mul(normalize(inNormal), xWorld));	
	Output.LightingFactor = 1;
	if (xEnableLighting)
		Output.LightingFactor = saturate(dot(Normal, -xLightDirection));
    
	return Output;     
}

TexPixelToFrame TexturedPS(TexVertexToPixel PSIn) 
{
	TexPixelToFrame Output = (TexPixelToFrame)0;		
    
	Output.Color = tex2D(TextureSampler, PSIn.TextureCoords);
	Output.Color.rgb *= saturate(PSIn.LightingFactor + xAmbient);

	return Output;
}

technique Textured
{
	pass Pass0
    {   
    	VertexShader = compile vs_1_1 TexturedVS();
        PixelShader  = compile ps_2_0 TexturedPS();
    }
}

//------- Technique: MultiTextured --------
struct MTVertexToPixel
{
    float4 Position         : POSITION;    
    float4 Color            : COLOR0;
    float3 Normal            : TEXCOORD0;
    float2 TextureCoords    : TEXCOORD1;
    float4 LightDirection    : TEXCOORD2;
    float4 TextureWeights    : TEXCOORD3;
    float Depth            : TEXCOORD4;
    float4 clipDistances     : TEXCOORD5;   //MSS - Water Refactor added
};

MTVertexToPixel MultiTexturedVS( float4 inPos : POSITION, float3 inNormal: NORMAL, float2 inTexCoords: TEXCOORD0, float4 inTexWeights: TEXCOORD1)
{   
    //- inPos are vertices of the water
    MTVertexToPixel Output = (MTVertexToPixel)0;
    float4x4 preViewProjection = mul (xView, xProjection);
    float4x4 preWorldViewProjection = mul (xWorld, preViewProjection);
    
    Output.Position = mul(inPos, preWorldViewProjection);
    Output.Normal = mul(normalize(inNormal), xWorld);
    Output.TextureCoords = inTexCoords;
    Output.LightDirection.xyz = -xLightDirection;
    Output.LightDirection.w = 1;    
    Output.TextureWeights = inTexWeights;
    Output.Depth = Output.Position.z/Output.Position.w;

    Output.clipDistances = dot(inPos, ClipPlane0); //MSS - Water Refactor added    

    return Output;    
}

struct MTPixelToFrame
{
    float4 Color : COLOR0;
};

MTPixelToFrame MultiTexturedPS(MTVertexToPixel PSIn)
{
    MTPixelToFrame Output = (MTPixelToFrame)0;        
    
    float lightingFactor = 1;
    if (xEnableLighting)
        lightingFactor = saturate(saturate(dot(PSIn.Normal, PSIn.LightDirection)) + xAmbient);

	float blendDistance = 0.99f;
	float blendWidth = 0.005f;
	float blendFactor = clamp((PSIn.Depth-blendDistance)/blendWidth, 0, 1);

	float4 farColor;
	farColor = tex2D(TextureSampler0, PSIn.TextureCoords)*PSIn.TextureWeights.x;
	farColor += tex2D(TextureSampler1, PSIn.TextureCoords)*PSIn.TextureWeights.y;
	farColor += tex2D(TextureSampler2, PSIn.TextureCoords)*PSIn.TextureWeights.z;
	farColor += tex2D(TextureSampler3, PSIn.TextureCoords)*PSIn.TextureWeights.w;
    
	float4 nearColor;
	float2 nearTextureCoords = PSIn.TextureCoords*3;
	nearColor = tex2D(TextureSampler0, nearTextureCoords)*PSIn.TextureWeights.x;
	nearColor += tex2D(TextureSampler1, nearTextureCoords)*PSIn.TextureWeights.y;
	nearColor += tex2D(TextureSampler2, nearTextureCoords)*PSIn.TextureWeights.z;
	nearColor += tex2D(TextureSampler3, nearTextureCoords)*PSIn.TextureWeights.w;

	Output.Color = lerp(nearColor, farColor, blendFactor);
	Output.Color *= lightingFactor;

    if (Clipping)
	    clip(PSIn.clipDistances);  //MSS - Water Refactor added

    return Output;
}

technique MultiTextured
{
    pass Pass0
    {
        VertexShader = compile vs_1_1 MultiTexturedVS();
        PixelShader = compile ps_2_0 MultiTexturedPS();
    }
}

//------- Technique: Water --------
//- Since the 2 triangles of the water are completely flat, we’re not passing any normal data;
//- in case we would need it, we know it’s always pointing upward. We only need to calculate
//- the sampling coords for the reflection map.

struct WPixelToFrame
{
    float4 Color : COLOR0;
};

//- Output struct for projective textures with water bump
struct WVertexToPixel
{
    float4 Position                 : POSITION;
    float4 ReflectionMapSamplingPos    : TEXCOORD1;
    float2 BumpMapSamplingPos        : TEXCOORD2;
    float4 RefractionMapSamplingPos : TEXCOORD3;
    float4 Position3D                : TEXCOORD4;
};

//- VERTEX SHADER
//- only calculate 2 output values: the 2D screen position of the current vertex, and the
//- corresponding 2D position as it would be seen by camera B. We will use this second 2D
//- position as sampling coordinate for our reflective texture in the pixel shader.
WVertexToPixel WaterVS(float4 inPos : POSITION, float2 inTex: TEXCOORD)
{    
	WVertexToPixel Output = (WVertexToPixel)0;

    float4x4 preViewProjection = mul (xView, xProjection);
    float4x4 preWorldViewProjection = mul (xWorld, preViewProjection);
    float4x4 preReflectionViewProjection = mul (xReflectionView, xProjection);
    float4x4 preWorldReflectionViewProjection = mul (xWorld, preReflectionViewProjection);

    Output.Position = mul(inPos, preWorldViewProjection);
    Output.ReflectionMapSamplingPos = mul(inPos, preWorldReflectionViewProjection);
	
	//- get 2D screen coords for pixel, as seen by normal camera which created the refraction map
	Output.RefractionMapSamplingPos = mul(inPos, preWorldViewProjection);
	//- get 3D position of each pixel to calculate the eyevector
	Output.Position3D = mul(inPos, xWorld);

	float3 windDir = normalize(xWindDirection);
	float3 perpDir = cross(xWindDirection, float3(0,1,0));

	//- find a fixed texure coordinates, rotated so the waves are perpendicular to our xWindDirection
	float ydot = dot(inTex, xWindDirection.xz);
	float xdot = dot(inTex, perpDir.xz);
	float2 moveVector = float2(xdot, ydot);

	//- make the texture scroll vertically by scrolling bump map along the Y direction
	//- larger values for xWaveLength will make the tex cords smaller, and thus the bump map will be stretched over a larger area
	moveVector.y += xTime*xWindForce;
    Output.BumpMapSamplingPos = moveVector/xWaveLength;

    return Output;
}

//- PIXEL SHADER
WPixelToFrame WaterPS(WVertexToPixel PSIn)
{
    WPixelToFrame Output = (WPixelToFrame)0;        
    
    float2 ProjectedTexCoords;
	
	//- sample the bump map
	float4 bumpColor = tex2D(WaterBumpMapSampler, PSIn.BumpMapSamplingPos);

    ProjectedTexCoords.x = PSIn.ReflectionMapSamplingPos.x/PSIn.ReflectionMapSamplingPos.w/2.0f + 0.5f;
    ProjectedTexCoords.y = -PSIn.ReflectionMapSamplingPos.y/PSIn.ReflectionMapSamplingPos.w/2.0f + 0.5f;    
	//- At this moment, the red and green color values of the bumpColor indicate how much the sampling coordinates of the reflection map should be perturbated.
	//- So you expect it to contain a value between -1 and +1. However, colors can only contain value between 0 and 1! So, as a solution, heightmaps are created
	//- by bringing the values from the [-1,1] region into the [0,1] region before saving them as a color. This can be done by dividing by 2 and adding 0.5.
	//- As an example, if the normal is unadjusted, you would have the X and Z component would be 0 and the Y component would be 1. When this is shifted to colors,
	//- you get (0.5, 0.5, 1), which is the main color you find in most bump maps.

	//- remap the values we find in the bump map from the [0,1] region into the [-1,1] region
	float2 perturbation = xWaveHeight*(bumpColor.rg - 0.5f)*2.0f;

	//- perturbated texture coordinates
	float2 perturbatedTexCoords = ProjectedTexCoords + perturbation;

	//- Fresnel calculations
	//- save the reflective color
	float4 reflectiveColor = tex2D(ReflectionSampler, perturbatedTexCoords);

	float2 ProjectedRefrTexCoords;
	ProjectedRefrTexCoords.x = PSIn.RefractionMapSamplingPos.x/PSIn.RefractionMapSamplingPos.w/2.0f + 0.5f;
	ProjectedRefrTexCoords.y = -PSIn.RefractionMapSamplingPos.y/PSIn.RefractionMapSamplingPos.w/2.0f + 0.5f;
	float2 perturbatedRefrTexCoords = ProjectedRefrTexCoords + perturbation;
	float4 refractiveColor = tex2D(RefractionSampler, perturbatedRefrTexCoords);
	
	//- find the eyevector, to obtain the Fresnel term
	float3 eyeVector = normalize(xCamPos - PSIn.Position3D);

	//- get the normal vector stored in the bump map into the [-1,1] region
	float3 normalVector = (bumpColor.rbg-0.5f)*2.0f;

	//- find Fresnel term
	float fresnelTerm = dot(eyeVector, normalVector);

	//- blend in a dull water color
	float4 combinedColor = lerp(reflectiveColor, refractiveColor, fresnelTerm);
	//- define a blueish gray dull water color
	float4 dullColor = float4(0.3f, 0.3f, 0.5f, 1.0f);

	//- blend reflective, refractive, and dull water colors by interpolation
	Output.Color = lerp(combinedColor, dullColor, 0.2f);

	//-	calculate the direction of the light, reflected over the normal vector
	float3 reflectionVector = -reflect(xLightDirection, normalVector);

	//- find out how much this reflected vector is the same as the eyeVector
	float specular = dot(normalize(reflectionVector), normalize(eyeVector));

	//- only take into account values that are higher than 0.95
	specular = pow(specular, 256);
	//- add this amount as white to final color of the pixel
	Output.Color.rgb += specular;
   
    return Output;
}

//- technique definition
technique Water
{
    pass Pass0
    {
        VertexShader = compile vs_1_1 WaterVS();
        PixelShader = compile ps_2_0 WaterPS();
    }
}