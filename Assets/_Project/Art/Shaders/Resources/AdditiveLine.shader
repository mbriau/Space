// Unlit additive colour from the vertices: tracers and beams of the firing range (LineRenderer).
Shader "Space/AdditiveLine"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 8)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Line"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Bright in the middle of the strip, soft at its edges.
                half across = 1.0h - abs(input.uv.y * 2.0h - 1.0h);
                half3 colour = input.color.rgb * input.color.a * _Intensity * (across * across + 0.6h * smoothstep(0.6h, 0.9h, across));
                return half4(colour, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
