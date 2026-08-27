// Stylised pond water for URP.
//
// Flat translucent discs read as plastic. This adds the three things that make water
// look like water in a stylised game: a moving normal-ish ripple, a depth fade so the
// bank shallows out instead of ending on a hard line, and a bright foam rim at the edge.
Shader "Follow/StylizedWater"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.45, 0.78, 0.76, 0.75)
        _DeepColor("Deep Color", Color) = (0.12, 0.34, 0.42, 0.94)
        _FoamColor("Foam Color", Color) = (0.92, 0.98, 0.98, 1)

        _DepthRange("Depth Range", Range(0.1, 8)) = 2.2
        _FoamWidth("Foam Width", Range(0, 1.5)) = 0.42
        _FoamCutoff("Foam Cutoff", Range(0, 1)) = 0.55

        _RippleScale("Ripple Scale", Range(0.5, 20)) = 7
        _RippleSpeed("Ripple Speed", Range(0, 3)) = 0.45
        _RippleStrength("Ripple Strength", Range(0, 1)) = 0.3

        _SpecColor2("Sparkle Color", Color) = (1, 1, 0.94, 1)
        _SpecPower("Sparkle Power", Range(1, 200)) = 60
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
            Cull Back

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
                half4 _SpecColor2;
                half  _SpecPower;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
            };

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
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
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

                // Foam where the water is shallow, wobbled by the ripple so the rim is
                // never a clean circle.
                float foamEdge = _FoamWidth * (0.75 + ripple * 0.5);
                float foam = 1.0 - smoothstep(0.0, foamEdge, waterDepth);
                foam = smoothstep(_FoamCutoff, 1.0, foam);
                col = lerp(col, _FoamColor.rgb, foam);

                // A single specular glint so the surface catches the sun.
                Light mainLight = GetMainLight();
                float3 n = normalize(IN.normalWS + float3((ripple - 0.5) * 0.6, 0, (ripple - 0.5) * 0.6));
                float3 v = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 h = normalize(mainLight.direction + v);
                half spec = pow(saturate(dot(n, h)), _SpecPower);
                col += _SpecColor2.rgb * spec * 0.8;

                half alpha = lerp(_ShallowColor.a, _DeepColor.a, waterDepth);
                alpha = max(alpha, foam);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
