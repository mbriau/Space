// Camera-facing glow for engine nozzles and impact flashes: a soft radial falloff, a brighter ring
// and a white-hot core, added on top of the scene. The quad mesh is a unit square centred on the
// object's pivot; the object's X scale is the halo diameter. Dimmer when seen from the front of
// the nozzle (the object's +Z) so the hull hides the glow naturally.
Shader "Space/EngineHalo"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.3, 0.85, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1.6
        _Core ("White core", Range(0, 2)) = 0.8
        _DepthPull ("Pull toward the camera (diameters)", Range(0, 1)) = 0.25
        _FrontFade ("Fade seen from the front", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Halo"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Core;
                float _DepthPull;
                half _FrontFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fade : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 axisX = float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20);
                float3 axisZ = float3(UNITY_MATRIX_M._m02, UNITY_MATRIX_M._m12, UNITY_MATRIX_M._m22);
                float size = length(axisX);

                float3 centerVS = TransformWorldToView(centerWS);
                float3 toCamera = normalize(GetCameraPositionWS() - centerWS);
                // Pull the quad toward the camera so it is not cut by the nozzle it sits in.
                centerVS += normalize(-centerVS) * size * _DepthPull;
                float3 positionVS = centerVS + float3(input.positionOS.xy * size, 0);
                output.positionCS = TransformWViewToHClip(positionVS);
                output.uv = input.uv;
                half facing = dot(-normalize(axisZ), toCamera); // 1 = looking into the exhaust
                output.fade = lerp(1.0h - _FrontFade, 1.0h, saturate(facing * 0.5h + 0.5h));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half r = length(input.uv * 2.0 - 1.0);
                half soft = pow(saturate(1.0h - r), 2.2h);
                half ring = smoothstep(0.44h, 0.38h, r);
                half core = smoothstep(0.2h, 0.15h, r);
                half3 colour = _Color.rgb * _Intensity * (soft * 1.2h + ring * 0.5h) + core * _Core;
                return half4(colour * input.fade * _Color.a, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
