Shader "Okami/TrailInkBloom"
{
    Properties
    {
        _InkColor("Ink Color", Color) = (0.08, 0.10, 0.055, 0.46)
        _LifeColor("Life Color", Color) = (0.35, 0.55, 0.18, 0.26)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-20"
            "RenderPipeline" = "UniversalRenderPipeline"
        }

        Pass
        {
            Name "InkBloom"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor;
                half4 _LifeColor;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(BloomProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BloomData)
            UNITY_INSTANCING_BUFFER_END(BloomProps)

            float Hash11(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 bloomData = UNITY_ACCESS_INSTANCED_PROP(BloomProps, _BloomData);
                float2 radialPoint = input.uv * 2.0 - 1.0;
                float radius = length(radialPoint);
                float angle = atan2(radialPoint.y, radialPoint.x);
                float normalizedAngle = frac(angle / 6.2831853 + 0.5);

                float wobble = sin(angle * 5.0 + bloomData.y * 11.0) * 0.035;
                wobble += sin(angle * 11.0 - bloomData.y * 19.0) * 0.016;
                float ringDistance = abs(radius - (0.78 + wobble));
                float brushWidth = lerp(0.045, 0.065, bloomData.z);
                float ring = 1.0 - smoothstep(brushWidth, brushWidth + 0.028, ringDistance);

                float segment = floor(normalizedAngle * 19.0);
                float coarseBreakup = Hash11(segment + bloomData.y * 37.0);
                float bristle = smoothstep(0.25, 0.68, coarseBreakup);
                float fineBreakup = 0.68 + 0.32 * sin(angle * 31.0 + bloomData.y * 53.0);
                float breakup = saturate(bristle * fineBreakup);

                float innerWash = (1.0 - smoothstep(0.0, 0.64, radius)) * 0.018;
                innerWash *= 1.0 - bloomData.w;
                float shape = saturate(ring * breakup + innerWash);
                clip(shape - 0.018);

                half4 color = lerp(_InkColor, _LifeColor, 0.16 + bloomData.z * 0.16);
                color.a *= shape * bloomData.x;
                return color;
            }
            ENDHLSL
        }
    }
}
