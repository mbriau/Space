// Electric splash where a shot strikes a shield: jagged arcs crawling out from the centre of a quad,
// flickering, additive. _Seed varies each hit, _Fade (1 to 0) ends it. See ShieldHitEffects.
Shader "Space/ElectricArc"
{
    Properties
    {
        _Color ("Color", Color) = (0.45, 0.8, 1, 1)
        _Fade ("Fade", Range(0, 1)) = 1
        _Seed ("Seed", Float) = 0
        _Reach ("Reach", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        // On the shield's surface, in front of the hull: never hidden by the ship it protects.
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Fade;
                float _Seed;
                float _Reach;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * 2.0 - 1.0;
                return output;
            }

            float Hash(float n) { return frac(sin(n) * 43758.5453); }

            float Noise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                return lerp(Hash(i), Hash(i + 1.0), f * f * (3.0 - 2.0 * f)) * 2.0 - 1.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv);
                float a = atan2(input.uv.y, input.uv.x);
                float flicker = floor(_Time.y * 24.0);
                float glow = 0.0;

                // Six arcs from the centre, each wandering around its own angle, redrawn many times a second.
                [unroll]
                for (int k = 0; k < 6; k++)
                {
                    float baseAngle = Hash(_Seed + k * 7.13) * 6.2831;
                    float wander = Noise(r * 9.0 + flicker * 1.7 + k * 3.1 + _Seed) * 0.35 * r
                                 + Noise(r * 23.0 + flicker * 2.3 + k) * 0.08;
                    float d = abs(sin((a - baseAngle - wander) * 0.5)) * r * 6.2831;
                    float length = (0.55 + 0.45 * Hash(_Seed + k * 3.7 + flicker)) * _Reach;
                    float along = saturate(1.0 - r / max(length, 0.01));
                    glow += (exp(-d * 22.0) + exp(-d * 6.0) * 0.25) * along;
                }

                float core = exp(-r * 6.0) * 2.0;
                float intensity = (glow * 1.8 + core) * _Fade;
                half3 color = lerp(_Color.rgb, half3(1, 1, 1), saturate(core + glow * 0.3));
                return half4(color * intensity, 1);
            }
            ENDHLSL
        }
    }
}
