// Stylised pond water for URP.
//
// Flat translucent discs read as plastic. This adds the four things that make water look
// like water in a stylised game: a surface that actually moves, a depth fade so the bank
// shallows out instead of ending on a hard line, a foam rim that traces wherever the
// ground crosses the surface, and a wide soft glitter rather than one hard highlight.
//
// The mesh is a flat fan cut wider than the pond, so its outer ring is buried in the
// hillside and the visible waterline is the intersection - which is why the depth fade
// below is doing structural work, not just shading.
Shader "Follow/StylizedWater"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.52, 0.80, 0.75, 0.55)
        _DeepColor("Deep Color", Color) = (0.13, 0.36, 0.44, 0.86)
        _FoamColor("Foam Color", Color) = (0.95, 1.0, 0.99, 1)

        _DepthRange("Depth Range", Range(0.1, 8)) = 1.8
        _FoamWidth("Foam Width", Range(0, 1.5)) = 0.30
        _FoamCutoff("Foam Cutoff", Range(0, 1)) = 0.35

        _RippleScale("Ripple Scale", Range(0.5, 20)) = 3.2
        _RippleSpeed("Ripple Speed", Range(0, 3)) = 0.55
        _RippleStrength("Ripple Strength", Range(0, 1)) = 0.22

        _WaveHeight("Wave Height", Range(0, 0.6)) = 0.075
        _WaveScale("Wave Scale", Range(0.05, 3)) = 0.65
        _WaveSpeed("Wave Speed", Range(0, 4)) = 1.1

        _SpecColor2("Sparkle Color", Color) = (1, 1, 0.94, 1)
        _SpecPower("Sparkle Power", Range(1, 200)) = 22
        _SpecStrength("Sparkle Strength", Range(0, 2)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half  _DepthRange;
                half  _FoamWidth;
                half  _FoamCutoff;
                half  _RippleScale;
                half  _RippleSpeed;
                half  _RippleStrength;
                half  _WaveHeight;
                half  _WaveScale;
                half  _WaveSpeed;
                half4 _SpecColor2;
                half  _SpecPower;
                half  _SpecStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  wave       : TEXCOORD3;
            };

            // Two crossing swells. Slow and long, so the surface breathes rather than
            // chops - this is a hill pond, not an ocean.
            float Wave(float2 p, float t)
            {
                float a = sin(p.x * _WaveScale + t * _WaveSpeed);
                float b = sin((p.x * 0.6 + p.y * 1.1) * _WaveScale * 0.8 - t * _WaveSpeed * 0.72);
                return (a + b) * 0.5;
            }

            // Cheap two-wave ripple, used both for shading and to wobble the foam edge.
            float Ripple(float2 p)
            {
                float t = _Time.y * _RippleSpeed;
                float a = sin(p.x * _RippleScale + t * 1.3);
                float b = sin((p.y * _RippleScale * 0.85) - t * 0.9 + a * 0.6);
                return (a + b) * 0.25 + 0.5;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                // Displace in world space so neighbouring ponds never share a seam and the
                // swell keeps its scale however large the disc is stretched.
                float w = Wave(positionWS.xz, _Time.y);
                positionWS.y += w * _WaveHeight;
                OUT.wave = w;

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.screenPos.xy / max(IN.screenPos.w, 0.0001);

                // How much water sits between this pixel and whatever is behind it.
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float surfaceDepth = IN.screenPos.w;
                float waterDepth = saturate((sceneDepth - surfaceDepth) / _DepthRange);

                float ripple = Ripple(IN.positionWS.xz);

                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, waterDepth);
                col += (ripple - 0.5) * _RippleStrength * 0.35;

                // Foam where the water is shallow, wobbled by the ripple and by the swell
                // so the rim breathes in and out of the bank instead of sitting still.
                float foamEdge = _FoamWidth * (0.7 + ripple * 0.45 + IN.wave * 0.25);
                float foam = 1.0 - smoothstep(0.0, foamEdge, waterDepth);
                foam = smoothstep(_FoamCutoff, 1.0, foam);
                col = lerp(col, _FoamColor.rgb, foam * 0.85);

                // A wide, soft glitter. A tight highlight on a low-poly disc reads as a
                // plastic sheen; broadening it turns the same maths into sun on water.
                Light mainLight = GetMainLight();
                float3 n = normalize(IN.normalWS + float3((ripple - 0.5) * 0.5, 0, (ripple - 0.5) * 0.5));
                float3 v = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 h = normalize(mainLight.direction + v);
                half spec = pow(saturate(dot(n, h)), _SpecPower);
                col += _SpecColor2.rgb * spec * _SpecStrength * mainLight.color;

                // Daylight tints the whole surface, so a pond at dusk is not a bright
                // turquoise hole in an otherwise blue forest.
                col *= lerp(half3(0.55, 0.6, 0.72), half3(1, 1, 1),
                            saturate(dot(mainLight.color, half3(0.33, 0.33, 0.33)) * 1.4));

                half alpha = lerp(_ShallowColor.a, _DeepColor.a, waterDepth);
                alpha = max(alpha, foam * 0.9);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
