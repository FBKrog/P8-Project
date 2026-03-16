Shader "Felix/Cel Trans"
{
    Properties
    { 
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        _ShadowCutoff("Shadow Cutoff", Range(0.0, 1.0)) = 0.5
        _ShadowBlur("Shadow Edge Blur", Range(0.0, 0.01)) = 0.002
        
        _SpecularCutoff("Specular Cutoff", Range(0.001, 1.0)) = 0.5
        
        [ToggleOff] _UseSpecular("Use Specular", Float) = 0.0
        
        [ToggleUI] _UseEmission("Use Emission", Float) = 0.0
        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // For transparency blending
        Blend SrcAlpha OneMinusSrcAlpha
        
        // Cull Off // Turns off backculling
        // ZWrite On // Allows writing to the Z-buffer
        Pass
        {
            // The LightMode tag matches the ShaderPassName set in UniversalRenderPipeline.cs.
            // The SRPDefaultUnlit pass and passes without the LightMode tag are also rendered by URP
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            
            HLSLPROGRAM
                        
            #pragma vertex vert
            #pragma fragment frag

            // This multi_compile declaration is required for the Forward rendering path
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            
            // This multi_compile declaration is required for the Forward+ rendering path
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            // This multi_compile declaration is required for accessing the shadow map for the main light
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            
            // Makes floats into toggles for conditional compilation
            #pragma shader_feature_local_fragment _USESPECULAR_OFF
            #pragma shader_feature_local_fragment _USEEMISSION_OFF

            // This multi_compile declaration is required for accessing the shadow maps for additional lights
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            // This multi_compile declaration is required for accessing baked lightmaps
            // Included as per https://discussions.unity.com/t/how-do-i-sample-baked-gi-in-urp-hlsl/1653774
            #pragma multi_compile _ LIGHTMAP_ON
            
            // #include "Packages/com.unity.render-pipelines.universal/Shaders/LitGBufferPass.hlsl"
            
            // Included as per https://docs.unity3d.com/6000.3/Documentation/Manual/urp/use-built-in-shader-methods-additional-lights-fplus.html
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

            // Included as per https://docs.unity3d.com/6000.3/Documentation/Manual/urp/use-built-in-shader-methods-shadows.html
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                // Lightmap declarations from https://github.com/Unity-Technologies/Graphics/blob/b81f05bd21ab1bf7a240dd30fb4ecee4cff2d4e5/Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitGBufferPass.hlsl#L113-L127
                float2 staticLightmapUV   : TEXCOORD1;
                float2 dynamicLightmapUV  : TEXCOORD2;
            };
            
            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float2 uv           : TEXCOORD0;
                
                // Lightmaps and probeOcllusion from https://github.com/Unity-Technologies/Graphics/blob/b81f05bd21ab1bf7a240dd30fb4ecee4cff2d4e5/Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitGBufferPass.hlsl#L113-L127
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
                #ifdef DYNAMICLIGHTMAP_ON
                    float2  dynamicLightmapUV : TEXCOORD8; // Dynamic lightmap UVs
                #endif
                
                #ifdef USE_APV_PROBE_OCCLUSION
                    float4 probeOcclusion : TEXCOORD9;
                #endif
            };

            struct CustomLightingData
            {
                half3 litColor;
                half3 shadowColor;
                float4 shadowCoord;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _ShadowCutoff;
                float _ShadowBlur;
                float _SpecularCutoff;
                half4 _EmissionColor;
                half4 _EmissionMap;
            CBUFFER_END
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Get object position in world and object space
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                // Get object normals
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                // Get object UV map
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                
                return OUT;
            }

            half SpecularCelStep(half specular)
            {
                return step(_SpecularCutoff, specular);
            }

            half3 MyLightingSpecular(half3 lightColor, half3 lightDir, half3 normal, half3 viewDir, half smoothness)
            {
                float3 halfVec = SafeNormalize(float3(lightDir) + float3(viewDir));
                half NdotH = half(saturate(dot(normal, halfVec)));
                half modifier = pow(float(NdotH), float(smoothness)); // Half produces banding, need full precision
                // NOTE: In order to fix internal compiler error on mobile platforms, this needs to be float3
                float3 specularReflection = float3(1.0 ,1.0 ,1.0) * SpecularCelStep(modifier);
                return lightColor * specularReflection;
            }

            // Modified
            half3 MyBlinnPhong(Light light, InputData inputData)
            {
                // Original
                //half3 attenuatedLightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
                //half3 lightDiffuseColor = LightingLambert(attenuatedLightColor, light.direction, inputData.normalWS);
    
                half3 attenuatedLightColor = light.color * pow(light.distanceAttenuation, 0.1) * light.shadowAttenuation;

                half3 lightSpecularColor = half3(0, 0, 0);
                
#ifndef _USESPECULAR_OFF
                lightSpecularColor += MyLightingSpecular(attenuatedLightColor, light.direction, inputData.normalWS, inputData.viewDirectionWS, 36);
#endif

                return lightSpecularColor;
            }
            
            // Calculations to be done per light source
            half3 MyLightingFunction(float3 normalWS, Light light, half shadowValue)
            {

                // Half lambert diffuse
                float NdotL = dot(normalWS, normalize(light.direction));
                NdotL = (NdotL + 1) * 0.5 * pow(light.distanceAttenuation, 0.1);
                NdotL = saturate(NdotL);
                NdotL = smoothstep(_ShadowCutoff, _ShadowCutoff + _ShadowBlur, NdotL) * light.shadowAttenuation;

                return saturate(NdotL) * light.color;
            }
            
            // This function loops through the lights in the scene
            half3 MyLightLoop(CustomLightingData d, InputData inputData)
            {
                half3 lighting = 0;
                
                // Get the main light
                Light mainLight = GetMainLight();
                half shadowValue = MainLightRealtimeShadow(d.shadowCoord);
                lighting += MyLightingFunction(inputData.normalWS, mainLight, shadowValue);
                lighting += MyBlinnPhong(mainLight, inputData);
                
                // Get additional lights
                #if defined(_ADDITIONAL_LIGHTS)

                // Additional light loop for non-main directional lights. This block is specific to Forward+.
                #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light additionalLight = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1,1,1,1));
                    shadowValue = AdditionalLightRealtimeShadow(lightIndex, inputData.positionWS);
                    lighting += MyLightingFunction(inputData.normalWS, additionalLight, shadowValue);
                    lighting += MyBlinnPhong(additionalLight, inputData);
                }
                #endif
                
                // Additional light loop.
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light additionalLight = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1,1,1,1));
                    shadowValue = AdditionalLightRealtimeShadow(lightIndex, inputData.positionWS);
                    lighting += MyLightingFunction(inputData.normalWS, additionalLight, shadowValue);
                    lighting += MyBlinnPhong(additionalLight, inputData);
                LIGHT_LOOP_END
                
                #endif

                return lerp(d.shadowColor, d.litColor, lighting);
            }
            
            half4 frag(Varyings input) : SV_Target0
            {
                // The Forward+ light loop (LIGHT_LOOP_BEGIN) requires the InputData struct to be in its scope.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = input.normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // Lightmap extra info 
                // From https://github.com/Unity-Technologies/Graphics/blob/b81f05bd21ab1bf7a240dd30fb4ecee4cff2d4e5/Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitGBufferPass.hlsl#L113-L127
                #if defined(DEBUG_DISPLAY)
                #if defined(DYNAMICLIGHTMAP_ON)
                inputData.dynamicLightmapUV = input.dynamicLightmapUV;
                #endif
                #if defined(LIGHTMAP_ON)
                inputData.staticLightmapUV = input.staticLightmapUV;
                #else
                inputData.vertexSH = input.vertexSH;
                #endif
                #if defined(USE_APV_PROBE_OCCLUSION)
                inputData.probeOcclusion = input.probeOcclusion;
                #endif
                #endif

                // Extra data I wanna transfer
                CustomLightingData d;

                #ifdef SHADERGRAPH_PREVIEW
                    // In preview, there's no shadows or bakedGI
                    d.shadowCoord = 0;
                #else
                    // Calculate the main light shadow coord
                    // There are two types depending on if cascades are enabled
                    #if SHADOWS_SCREEN
                        d.shadowCoord = ComputeScreenPos(input.positionCS);
                    #else
                        d.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    #endif
                #endif

                // Sampled color from base map
                half4 sampledTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                d.litColor = sampledTex.rgb * _BaseColor.rgb;
                // Gets ambient lighting for shadow colour
                d.shadowColor = sampledTex.rgb * half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);

                half3 lighting = MyLightLoop(d, inputData);
                
#ifndef _USEEMISSION_OFF
                lighting += _EmissionColor.rgb;
#endif

                half4 finalColor = half4(lighting, sampledTex.a * _BaseColor.a);
                
                return finalColor;
            }
            
            ENDHLSL
        }

        // GBuffer Pass, for trying to get some lightmap baking
        // From URP Lit Shader
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite[_ZWrite]
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            // Deferred Rendering Path does not support the OpenGL-based graphics API:
            // Desktop OpenGL, OpenGL ES 3.0, WebGL 2.0.
            #pragma exclude_renderers gles3 glcore

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitGBufferPassVertex
            #pragma fragment LitGBufferPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            //#pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED

            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            //#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitGBufferPass.hlsl"
            ENDHLSL
        }
        
        // shadow caster rendering pass, implemented manually
        // using macros from UnityCG.cginc
        // From URP Lit Shader
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        
        // This pass it not used during regular rendering, only for lightmap baking.
        // From URP Lit shader
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            // -------------------------------------
            // Render State Commands
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SPECGLOSSMAP
            #pragma shader_feature EDITOR_VISUALIZATION

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"

            ENDHLSL
        }
    }
}
