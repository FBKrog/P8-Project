Shader "Custom/FelixDocsCel"
{
    Properties
    { 
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        _ShadowColor("Shadow Color", Color) = (0.075, 0, 0.15, 0.9)
        _ShadowCutoff("Shadow Cutoff", Range(0.0, 1.0)) = 0.5
        _ShadowBlur("Shadow Edge Blur", Range(0.0, 0.01)) = 0.002
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        
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

            // This multi_compile declaration is required for accessing the shadow maps for additional lights
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            
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
            };
            
            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float2 uv           : TEXCOORD0;
            };

            struct CustomLightingData
            {
                float3 litColor;
                float3 shadowColor;
                float4 shadowCoord;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                float4 _BaseMap_ST;
                float _ShadowCutoff;
                float _ShadowBlur;
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
            
            // Calculations to be done per light source
            float3 MyLightingFunction(float3 normalWS, Light light, half shadowValue)
            {

                // Half lambert diffuse
                float NdotL = dot(normalWS, normalize(light.direction));
                // Remove self-cast shadows at 0.5 or lower (75% of shadows since -1 to 1 range from dot product)
                float alteredShadowValue = lerp(1, shadowValue, step(0.5, NdotL));
                NdotL = (NdotL + 1) * 0.5 * pow(light.distanceAttenuation, 0.1) * light.shadowAttenuation * alteredShadowValue;
                NdotL = saturate(NdotL);
                NdotL = smoothstep(_ShadowCutoff, _ShadowCutoff + _ShadowBlur, NdotL);

                return saturate(NdotL) * light.color;
            }
            
            // This function loops through the lights in the scene
            float3 MyLightLoop(CustomLightingData d, InputData inputData)
            {
                float3 lighting = 0;
                
                // Get the main light
                Light mainLight = GetMainLight();
                half shadowValue = MainLightRealtimeShadow(d.shadowCoord);
                lighting += MyLightingFunction(inputData.normalWS, mainLight, shadowValue);
                
                // Get additional lights
                #if defined(_ADDITIONAL_LIGHTS)

                // Additional light loop for non-main directional lights. This block is specific to Forward+.
                #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light additionalLight = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1,1,1,1));
                    shadowValue = AdditionalLightRealtimeShadow(lightIndex, inputData.positionWS);
                    lighting += MyLightingFunction(inputData.normalWS, additionalLight, shadowValue);
                }
                #endif
                
                // Additional light loop.
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light additionalLight = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1,1,1,1));
                    shadowValue = AdditionalLightRealtimeShadow(lightIndex, inputData.positionWS);
                    lighting += MyLightingFunction(inputData.normalWS, additionalLight, shadowValue);
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
                float3 sampledTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                d.litColor = sampledTex * _BaseColor.rgb;
                d.shadowColor = lerp(sampledTex, _ShadowColor.rgb, _ShadowColor.a);

                float3 lighting = MyLightLoop(d, inputData);
                
                half4 finalColor = half4(lighting, 1);

                return finalColor;
            }
            
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
    }
}
