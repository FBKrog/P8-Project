#ifndef CUSTOM_LIGHTING
#define CUSTOM_LIGHTING 

struct CustomLightingData {
	// view propetries
	float3 normalWS; // normal of the fragment in world space
	float3 viewDirection; // Direction from the camera to the fragment
    float3 worldPosition; // Fragment world position
	float4 shadowCoord; // coordnate used to look up in shadow map?
	
	// baked properties
    half4 shadowMask; // Coordinates for baked shadows?

	// object properties
	float3 albedo; // Base RGB color with no light influence, eg. Sampled color from texture. 
	float smoothness; // stength of specular highlight

	// stylization
	float4 celThresholds;
	float4 celIntensities;
    float baseIntensity;
};

// translate 0-1 smoothness to an exponent, reducing the highlight as smothness grows
float GetSmoothnessPower(float rawSmoothness) {
	return exp2(10 * rawSmoothness + 1); 
}


// Light class is not avalible in preview window
#ifndef SHADERGRAPH_PREVIEW
	// compute the diffuse light on the fragment
float3 CustomLightHandling(CustomLightingData d, Light light) {

	// Get the light strength based on the angle between the normal and the light direction. Clamped between 0-1
	float diffuseStrength = saturate(dot(d.normalWS, light.direction));
	float specularBase = saturate(dot(d.normalWS, normalize(light.direction + d.viewDirection)));
	float specularStrength = diffuseStrength * pow(specularBase, GetSmoothnessPower(d.smoothness));
	
	// DistanceAttenuation: distance from light (0 outside range shape)
	// Intensity: Stengthens light.color.
	// shadow Attenuation: 'strengt' of the shadow, where 0 is complete shadow and 1 is no shadow.
	// the light has an RGB color. Darken that based on the shadow of other objects and distance from source.
    float lightIntensity = light.distanceAttenuation * light.shadowAttenuation * (diffuseStrength + specularStrength);

	// Clamp the light intensity into different buckets to create the cel shaded look.
	// TODO: the ifs can be removed by some smart math. And enabled/disabled by multiplying with an activation bool (1/0).

	if (lightIntensity <= d.celThresholds.x) {
		lightIntensity = d.baseIntensity;
	}
	else if (lightIntensity <= d.celThresholds.y) {
		lightIntensity = d.celIntensities.x;
	}
	else if (lightIntensity <= d.celThresholds.z) {
		lightIntensity = d.celIntensities.y;
	}
	else if (lightIntensity <= d.celThresholds.w) {
		lightIntensity = d.celIntensities.z;
	}
	else {
		lightIntensity = d.celIntensities.w;
	}
	
	
	// combine the color and strength of the light hitting the fragment.
	float3 lightStrength = light.color * lightIntensity;

	// Use the strength of the light to light up the base texture color sample
	float3 color = d.albedo * lightStrength;

	return color;
	}
#endif

float3 CalculateCustomLighting(CustomLightingData d) {
// Light class is not avalible in preview window
#ifdef SHADERGRAPH_PREVIEW
	// assume light direction and calculate based on that.
	float3 lightDir = float3(0.5, 0.5, 0);
	float intensity = saturate(dot(d.normalWS, lightDir)) +
		pow(saturate(dot(d.normalWS, normalize(d.viewDirection + lightDir))), GetSmoothnessPower(d.smoothness)); 
	return d.albedo* intensity;
#else
	// Returns the single main light in the scene. Must be a directional light?
    Light mainLight = GetMainLight(d.shadowCoord, d.worldPosition, d.shadowMask);

	// start with a black pixel because no light has hit it yet.
	float3 color = 0;

	// Add the light from the main light to the pixel
	color += CustomLightHandling(d, mainLight);

	#ifdef _ADDITIONAL_LIGHTS
	// For each additional light, also add them to the fragment color.
	uint additionalLightCount = GetAdditionalLightsCount();
	for (uint light_i = 0; light_i < additionalLightCount; light_i++) {
		Light light = GetAdditionalLight(light_i, d.worldPosition, d.shadowMask);
		color += CustomLightHandling(d, light);
	}
	#endif

	return color;
#endif
}


// Wrapper function called by shader graph. Output is provided through out variables
// _float suffix specifies precision level on GPU
void CalculateCustomLighting_float(float3 WorldPosition, float3 ViewDirection, float3 Albedo, float3 Normal, float Smoothness, float4 CelThresholds, float4 CelIntensities, float BaseIntensity, 
	out float3 Color) {
	CustomLightingData d;
	d.albedo = Albedo;
	d.normalWS = Normal;
	d.viewDirection = ViewDirection;
	d.smoothness = Smoothness;
    d.worldPosition = WorldPosition;
	d.celThresholds = CelThresholds;
	d.celIntensities = CelIntensities;
    d.baseIntensity = BaseIntensity;

	// Calculate the shadow coord based on the fragment position.
	#ifdef SHADERGRAPH_PREVIEW
	d.shadowCoord = 0;
	#else
	#if SHADOWS_SCREEN
	float4 positionCS = TransformWorldToHClip(d.worldPosition);
	d.shadowCoord = ComputeScreenPos(positionCS);
	#else
    d.shadowCoord = TransformWorldToShadowCoord(d.worldPosition);
	#endif
	#endif
	
	// Grab the shadomaks if it exists
	#if !defined (LIGHTMAP_ON)
    d.shadowMask = unity_ProbesOcclusion; 
    #else
    d.shadowMask = half4(1, 1, 1, 1);
    #endif

	Color = CalculateCustomLighting(d);
}

#endif
