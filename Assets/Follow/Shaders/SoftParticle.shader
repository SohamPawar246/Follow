// Every soft round particle in the game: pollen, fireflies, embers, flame, smoke, motes.
//
// This exists because assembling a transparent URP particle material from script is a trap.
// Setting _Surface and _Blend on the stock shader looks like it should work and does not -
// those are inspector-side fields, and without also setting the blend factors, the ZWrite
// flag and the surface keyword the material stays opaque. The result is opaque squares
// where the soft dots should be, which is exactly what the smoke over the campfire was.
//
// So: blend factors as real properties, and the round falloff generated in the fragment
// rather than sampled from a texture, which removes the other half of the problem.
Shader "Follow/SoftParticle"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Softness("Edge Softness", Range(0.01, 1)) = 0.55
        _Core("Core Size", Range(0, 1)) = 0.15

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5   // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 1   // One
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Particle"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            Lighting Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _Softness;
                half  _Core;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _BaseColor;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Distance from the middle of the quad, so the sprite is a disc however
                // the particle system happens to have stretched it.
                float2 d = IN.uv * 2.0 - 1.0;
                float r = saturate(length(d));

                // Flat core, then a smooth shoulder out to the rim. Squaring the falloff
                // is what stops it reading as a hard-edged bubble.
                half a = 1.0 - smoothstep(_Core, _Core + _Softness, r);
                a *= a;

                half4 color = IN.color;
                color.a *= a;
                clip(color.a - 0.002);

                // Additive motes should not be fogged out of existence; alpha ones should.
                color.rgb = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
