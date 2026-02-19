
#ifndef CUSTOM_LIGHTING
#define CUSTOM_LIGHTING 

void GetMainLightData_float(half4 ShadowCoord, float3 WorldPosition, half4 ShadowMask,
out half3 direction, out half3 color, out float distanceAttenuation, out half shadowAttenuation)
{
    #ifdef SHADERGRAPH_PREVIEW
	direction = float3(0.5, 0.5, 0);
    color = half3(1, 0.6, 0.3);
    distanceAttenuation = length(WorldPosition + float3(1, 1, 1));
    shadowAttenuation = length(WorldPosition + float3(1, 1, 1));
    #else
    Light mainLightInfo = GetMainLight(ShadowCoord, WorldPosition, ShadowMask);
    direction = mainLightInfo.direction;
    color = mainLightInfo.color;
    distanceAttenuation = mainLightInfo.distanceAttenuation;
    shadowAttenuation = mainLightInfo.shadowAttenuation;
    #endif
}


void StepLightIntensity_float(float Intensity_in, float BaseIntensity, float4 CelIntensities, float4 CelThresholds, out
float Intensity_out)
{
    if (Intensity_in <= CelThresholds.x)
    {
        Intensity_out = BaseIntensity;
    }
    else if (Intensity_in <= CelThresholds.y)
    {
        Intensity_out = CelIntensities.x;
    }
    else if (Intensity_in <= CelThresholds.z)
    {
        Intensity_out = CelIntensities.y;
    }
    else if (Intensity_in <= CelThresholds.w)
    {
        Intensity_out = CelIntensities.z;
    }
    else
    {
        Intensity_out = CelIntensities.w;
    }
}

void GetShadowCoord_float(float3 WorldPosition, out float4 ShadowCoord)
{
#ifdef SHADERGRAPH_PREVIEW
	ShadowCoord = 0;
#else
#if SHADOWS_SCREEN
	float4 positionCS = TransformWorldToHClip(WorldPosition);
	ShadowCoord = ComputeScreenPos(positionCS);
#else
    ShadowCoord = TransformWorldToShadowCoord(WorldPosition);

     half cascadeIndex = ComputeCascadeIndex(WorldPosition);
    float4 shadowCoord = mul(_MainLightWorldToShadow[cascadeIndex], float4(WorldPosition, 1.0));

    ShadowCoord = float4(shadowCoord.xyz, 0);
#endif
#endif
}

void GetShadowMask_half(out half4 ShadowMask)
{
	// Grab the shadomaks if it exists
#if !defined (LIGHTMAP_ON)
    ShadowMask = unity_ProbesOcclusion;
#else
    ShadowMask = half4(1, 1, 1, 1);
#endif
}

#endif