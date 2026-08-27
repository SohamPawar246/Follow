// Vertex-animated foliage for URP.
//
// A static forest reads as a diorama; the moment the canopy moves it reads as a place.
// The sway is masked by height in object space so trunks stay planted while leaves move,
// and it is phase-offset by world position so neighbouring trees never move in lockstep.
Shader "Follow/FoliageWind"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1,1,1,1)

        [Header(Wind)]
        _WindStrength("Wind Strength", Range(0, 1)) = 0.13
        _WindSpeed("Wind Speed", Range(0, 6)) = 1.15
        _WindScale("Wind Scale", Range(0.01, 2)) = 0.22
        _WindHeightMask("Height Mask Power", Range(0.1, 6)) = 1.6
        _WindPivotY("Pivot Height", Float) = 0.0

        [Header(Stylised Light)]
        _AmbientBoost("Ambient Boost", Range(0, 1)) = 0.35
        _RimColor("Rim Color", Color) = (1, 0.94, 0.78, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 3.2
        _RimStrength("Rim Strength", Range(0, 2)) = 0.45
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Occlusion)]
        // Dropped toward zero while this plant stands between the camera and the player.
        _Fade("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _WindStrength;
            half   _WindSpeed;
            half   _WindScale;
            half   _WindHeightMask;
            float  _WindPivotY;
            half   _AmbientBoost;
            half4  _RimColor;
            half   _RimPower;
            half   _RimStrength;
            half   _Cutoff;
            half   _Fade;
        CBUFFER_END

        // A 4x4 ordered dither. Screen-door transparency rather than real alpha, because
        // a tree fading out has to keep writing depth and sorting like an opaque object -
        // the moment it goes translucent it sorts against every other leaf in the canopy
        // and the whole forest starts flickering through itself.
        float DitherThreshold(float2 positionSS)
        {
            const float pattern[16] =
            {
                0.0625, 0.5625, 0.1875, 0.6875,
                0.8125, 0.3125, 0.9375, 0.4375,
                0.2500, 0.7500, 0.1250, 0.6250,
                1.0000, 0.5000, 0.8750, 0.3750
            };
            int2 p = int2(fmod(positionSS, 4.0));
            return pattern[p.y * 4 + p.x];
        }

        // Two out-of-phase waves give a gust that never reads as a loop.
        float3 ApplyWind(float3 positionOS, float3 positionWS)
        {
            float mask = saturate((positionOS.y - _WindPivotY));
            mask = pow(abs(mask), _WindHeightMask);

            float t = _Time.y * _WindSpeed;
            float phase = (positionWS.x + positionWS.z) * _WindScale;

            float swayX = sin(t + phase) + 0.4 * sin(t * 2.3 + phase * 1.7);
            float swayZ = cos(t * 0.85 + phase * 1.2) + 0.4 * cos(t * 1.9 + phase * 2.1);

            positionOS.x += swayX * _WindStrength * mask;
            positionOS.z += swayZ * _WindStrength * mask * 0.7;
            return positionOS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 swayed = ApplyWind(IN.positionOS.xyz, pivotWS);

                OUT.positionWS = TransformObjectToWorld(swayed);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);
                clip(_Fade - DitherThreshold(IN.positionCS.xy) + 0.0001);

                float3 normalWS = normalize(IN.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Half-lambert keeps shadowed foliage readable instead of crushing to black,
                // which is what the cozy register needs.
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half wrapped = ndl * 0.5 + 0.5;
                half3 lighting = mainLight.color * lerp(ndl, wrapped, 0.65) * mainLight.shadowAttenuation;

                half3 ambient = SampleSH(normalWS) * (1.0 + _AmbientBoost);
                half3 color = albedo.rgb * (lighting + ambient);

                // Warm rim, so silhouettes lift off the background at a top-down angle.
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half rim = pow(saturate(1.0 - saturate(dot(normalWS, viewDir))), _RimPower);
                color += _RimColor.rgb * rim * _RimStrength * mainLight.shadowAttenuation;

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings shadowVert(Attributes IN)
            {
                Varyings OUT;

                // The shadow must sway with the mesh or it detaches and reads as a bug.
                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 swayed = ApplyWind(IN.positionOS.xyz, pivotWS);

                float3 positionWS = TransformObjectToWorld(swayed);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 shadowFrag(Varyings IN) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
