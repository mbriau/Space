// Shield aura of a ship drawn from a ready-made model: a faint glow on the silhouette's edges
// (fresnel), additive, that breathes a little. _Intensity follows the shield's charge and flashes on
// hits. See ShipArtModel.
Shader "Space/ShieldAura"
{
    Properties
    {
        _Color ("Color", Color) = (0.4, 0.7, 1, 1)
        _Intensity ("Intensity", Float) = 0.25
        _Power ("Rim power", Float) = 3
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Power;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                output.positionWS = positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewWS);
                float rim = pow(1.0 - saturate(dot(n, v)), _Power);
                // Slow bands of energy running over the surface.
                float bands = 0.75 + 0.25 * sin(input.positionWS.y * 0.6 + input.positionWS.z * 0.25 + _Time.y * 1.7);
                return half4(_Color.rgb * rim * bands * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
