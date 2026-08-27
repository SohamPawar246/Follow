// The forest floor.
//
// One flat green across the whole map is the loudest tell that a world was generated
// rather than made. The streamer bakes damp, dry and bare-rock colour into the mesh's
// vertex colours as it builds each chunk, and this shader does the rest: a fine grain
// so the low-poly facets are not perfectly uniform, and the same half-lambert wrap the
// foliage uses so shadowed ground stays readable instead of crushing to black.
Shader "Follow/StylizedGround"
{
    Properties
    {
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)

        [Header(Grain)]
        _GrainColor("Grain Color", Color) = (0.20, 0.28, 0.14, 1)
        _GrainStrength("Grain Strength", Range(0, 1)) = 0.16
        _GrainScale("Grain Scale", Range(0.05, 8)) = 1.6

        [Header(Stylised Light)]
        _AmbientBoost("Ambient Boost", Range(0, 1)) = 0.30
        _Wrap("Light Wrap", Range(0, 1)) = 0.60
        _SpecTint("Sheen Color", Color) = (1, 0.97, 0.86, 1)
        _SpecStrength("Sheen Strength", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _GrainColor;
                half  _GrainStrength;
                half  _GrainScale;
                half  _AmbientBoost;
                half  _Wrap;
                half4 _SpecTint;
                half  _SpecStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 color      : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
            };

            // Cheap value noise. Enough to break up a facet, not enough to read as texture.
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // The chunk builder wrote the local ground story into the vertices.
                half3 albedo = IN.color.rgb * _BaseColor.rgb;

                float grain = ValueNoise(IN.positionWS.xz * _GrainScale);
                float coarse = ValueNoise(IN.positionWS.xz * _GrainScale * 0.17);
                grain = lerp(coarse, grain, 0.6);
                albedo = lerp(albedo, _GrainColor.rgb, (grain - 0.5) * _GrainStrength + _GrainStrength * 0.5);

                float3 normalWS = normalize(IN.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half ndl = saturate(dot(normalWS, mainLight.direction));
                half wrapped = ndl * 0.5 + 0.5;
                half3 lighting = mainLight.color * lerp(ndl, wrapped, _Wrap) * mainLight.shadowAttenuation;

                half3 ambient = SampleSH(normalWS) * (1.0 + _AmbientBoost);
                half3 color = albedo * (lighting + ambient);

                // A faint sheen along the grazing angle: dew, and it gives the hills form.
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half3 halfDir = normalize(viewDir + mainLight.direction);
                half sheen = pow(saturate(dot(normalWS, halfDir)), 24.0);
                color += _SpecTint.rgb * sheen * _SpecStrength * mainLight.shadowAttenuation;

                // The fire and any other point lights have to reach the ground, or camp
                // at night is a glowing object standing on a black floor.
                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; ++i)
                {
                    Light light = GetAdditionalLight(i, IN.positionWS);
                    half atten = light.distanceAttenuation * light.shadowAttenuation;
                    color += albedo * light.color * saturate(dot(normalWS, light.direction)) * atten;
                }
                #endif

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

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings shadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 shadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings depthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 depthFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
