#ifndef FELIX_LIT_META_PASS_INCLUDED
#define FELIX_LIT_META_PASS_INCLUDED

#include "Assets/Shaders/CelShadedLighting/BasedOnLit/FUniversalMetaPassBased.hlsl"

half4 UniversalFragmentMetaLit(Varyings input) : SV_Target
{
    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

    BRDFData brdfData;
    InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, brdfData);

    MetaInput metaInput;
    metaInput.Albedo = brdfData.diffuse + brdfData.specular * brdfData.roughness * 0.5;
    metaInput.Emission = surfaceData.emission;
    return UniversalFragmentMeta(input, metaInput);
}
#endif
