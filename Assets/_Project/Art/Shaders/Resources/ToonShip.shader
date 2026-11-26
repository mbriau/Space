// Cartoon shading for the procedural ships: paint from the vertex colour (rgb, sRGB) and emission
// mask in the vertex alpha. Two-tone lighting with a cool shadow tint, a lighter band, a hard
// specular spot and a rim light, plus an inverted-hull outline that uses the smoothed normals stored
// in TEXCOORD3 so the line has no gaps at the hard edges. Main light only; SRP Batcher compatible.
Shader "Space/ToonShip"
{
    Properties
    {
        _ShadowTint ("Shadow tint", Color) = (0.38, 0.42, 0.62, 1)
        _ShadeThreshold ("Shade threshold", Range(-1, 1)) = 0
        _ShadeSoftness ("Shade softness", Range(0.001, 0.3)) = 0.04
        _HighlightThreshold ("Highlight threshold", Range(0, 1)) = 0.55
        _HighlightBoost ("Highlight boost", Range(0, 0.5)) = 0.15
        _Ambient ("Ambient", Range(0, 1)) = 0.12
        _RimStrength ("Rim strength", Range(0, 1)) = 0.45
        _RimThreshold ("Rim threshold", Range(0, 1)) = 0.45
        _SpecStrength ("Specular strength", Range(0, 1)) = 0.3
        _EmissionStrength ("Emission strength", Range(0, 8)) = 2.5
        _OutlineColor ("Outline colour", Color) = (0.06, 0.05, 0.1, 1)
        _OutlineTint ("Outline takes paint", Range(0, 1)) = 0.35
        _OutlineWidth ("Outline width (pixels)", Range(0, 6)) = 2.2
        _OutlineMaxWidth ("Outline max width (metres)", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ShadowTint;
            half _ShadeThreshold;
            half _ShadeSoftness;
            half _HighlightThreshold;
            half _HighlightBoost;
            half _Ambient;
            half _RimStrength;
            half _RimThreshold;
            half _SpecStrength;
            half _EmissionStrength;
            half4 _OutlineColor;
            half _OutlineTint;
            float _OutlineWidth;
            float _OutlineMaxWidth;
        CBUFFER_END

        half3 PaintToLinear(half3 c)
        {
            #if defined(UNITY_COLORSPACE_GAMMA)
                return c;
            #else
                return SRGBToLinear(c);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light light = GetMainLight();
                half3 paint = PaintToLinear(input.color.rgb);

                half ndl = dot(n, light.direction);
                half lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl);
                half high = smoothstep(_HighlightThreshold - 0.03h, _HighlightThreshold + 0.03h, ndl);
                half3 colour = lerp(paint * _ShadowTint.rgb, paint, lit) * (1.0h + _HighlightBoost * high) * light.color;
                colour += paint * SampleSH(n) * _Ambient;

                half fresnel = pow(1.0h - saturate(dot(n, v)), 3.0h);
                colour += paint * _RimStrength * smoothstep(_RimThreshold, _RimThreshold + 0.06h, fresnel * (0.35h + 0.65h * lit));

                float3 h = normalize(light.direction + v);
                colour += _SpecStrength * smoothstep(0.965h, 0.975h, dot(n, h)) * lit * light.color;

                colour = lerp(colour, paint * _EmissionStrength, input.color.a);
                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 outlineNormalOS : TEXCOORD3;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalOS = dot(input.outlineNormalOS, input.outlineNormalOS) > 0.01 ? input.outlineNormalOS : input.normalOS;
                float3 normalWS = normalize(TransformObjectToWorldNormal(normalOS));
                // Constant width in pixels, capped in metres so a distant ship does not turn into a blob.
                float w = TransformWorldToHClip(positionWS).w;
                float width = min(_OutlineWidth * 2.0 * w / (_ScreenParams.y * UNITY_MATRIX_P._m11), _OutlineMaxWidth);
                output.positionCS = TransformWorldToHClip(positionWS + normalWS * width);
                output.color = lerp(_OutlineColor.rgb, PaintToLinear(input.color.rgb) * 0.25h, _OutlineTint);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(input.color, 1.0h);
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(normalize(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
