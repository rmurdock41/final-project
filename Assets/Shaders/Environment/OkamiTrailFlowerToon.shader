Shader "Okami/TrailFlowerToon"
{
    Properties
    {
        [MainTexture] _BaseMap("Flower Atlas", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _ShadeColor("Shade Color", Color) = (0.55,0.62,0.48,1)
        _ShadeStep("Shade Step", Range(0,1)) = 0.52
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.15
        _OutlineColor("Outline Color", Color) = (0.028,0.021,0.015,1)
        _OutlineWidth("Outline Width", Range(0,0.0005)) = 0.00021
        _InnerInkWidth("Screen-space Ink Rim", Range(0.5,4)) = 2.2
        _WatercolorStrength("Watercolor Posterize", Range(0,1)) = 0.38
        _EdgeBreakup("Dry Edge Breakup", Range(0,0.2)) = 0.045
        _ColorSteps("Pigment Steps", Range(2,8)) = 4
        _PaperColor("Watercolor Paper", Color) = (0.96,0.94,0.86,1)
        _PigmentDensity("Pigment Density", Range(0,1)) = 0.68
        _Granulation("Paper Granulation", Range(0,1)) = 0.55
        _EdgePooling("Wet Edge Pooling", Range(0,1)) = 0.75
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadeColor;
                half4 _OutlineColor;
                half _ShadeStep;
                half _Cutoff;
                float _OutlineWidth;
                float _InnerInkWidth;
                float _WatercolorStrength;
                float _EdgeBreakup;
                float _ColorSteps;
                half4 _PaperColor;
                float _PigmentDensity;
                float _Granulation;
                float _EdgePooling;
            CBUFFER_END

            float Hash13(float3 value)
            {
                return frac(sin(dot(value, float3(12.9898, 78.233, 37.719))) * 43758.5453);
            }

            Varyings OutlineVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 sourcePositionWS = TransformObjectToWorld(input.positionOS.xyz);
                float outlineNoise = Hash13(floor(sourcePositionWS * 19.0));
                float3 expandedPosition = input.positionOS.xyz +
                                          input.normalOS * _OutlineWidth * lerp(0.72, 1.28, outlineNoise);
                output.positionCS = TransformObjectToHClip(expandedPosition);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionWS = sourcePositionWS;
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                half edgeNoise = Hash13(floor(input.positionWS * 37.0));
                clip(alpha - _Cutoff - (edgeNoise - 0.5h) * _EdgeBreakup);
                return half4(_OutlineColor.rgb, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex FlowerVertex
            #pragma fragment FlowerFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadeColor;
                half4 _OutlineColor;
                half _ShadeStep;
                half _Cutoff;
                float _OutlineWidth;
                float _InnerInkWidth;
                float _WatercolorStrength;
                float _EdgeBreakup;
                float _ColorSteps;
                half4 _PaperColor;
                float _PigmentDensity;
                float _Granulation;
                float _EdgePooling;
            CBUFFER_END

            float Hash13(float3 value)
            {
                return frac(sin(dot(value, float3(12.9898, 78.233, 37.719))) * 43758.5453);
            }

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 local = frac(value);
                local = local * local * (3.0 - 2.0 * local);

                float bottom = lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), local.x);
                float top = lerp(Hash21(cell + float2(0.0, 1.0)), Hash21(cell + 1.0), local.x);
                return lerp(bottom, top, local.y);
            }

            Varyings FlowerVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 FlowerFragment(Varyings input) : SV_Target
            {
                half4 atlas = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half edgeNoise = Hash13(floor(input.positionWS * 37.0));
                half effectiveCutoff = _Cutoff + (edgeNoise - 0.5h) * _EdgeBreakup;
                clip(atlas.a - effectiveCutoff);

                // The atlas only supplies hue and silhouette. Flatten its baked,
                // plastic-looking gradient into a translucent pigment wash over
                // warm paper, while retaining a little of the original petal detail.
                half sourceValue = max(atlas.r, max(atlas.g, atlas.b));
                half3 pigmentHue = saturate(atlas.rgb / max(sourceValue, 0.035h));
                half pigmentSteps = max(2.0h, (half)_ColorSteps);
                half steppedValue = floor(sourceValue * pigmentSteps + 0.5h) / pigmentSteps;
                half retainedDetail = lerp(sourceValue, steppedValue, 0.22h * _WatercolorStrength);

                float2 instanceOffset = floor(input.positionWS.xz * 2.7) * float2(0.73, 1.17);
                half broadWash = ValueNoise(input.uv * 17.0 + instanceOffset);
                half fineGrain = ValueNoise(input.uv * 71.0 + instanceOffset * 4.3);
                half pigmentVariation = lerp(
                    1.0h - _Granulation * 0.20h,
                    1.0h + _Granulation * 0.09h,
                    fineGrain);

                half densityVariation = lerp(0.88h, 1.05h, broadWash);
                half density = saturate(_PigmentDensity * densityVariation);
                half3 washColor = lerp(_PaperColor.rgb, pigmentHue, density);
                washColor *= lerp(0.88h, 1.035h, retainedDetail) * pigmentVariation;

                // Sparse paper-coloured gaps break the perfectly smooth computer
                // gradient and read as pigment settling into textured fibres.
                half paperLift = smoothstep(0.76h, 0.96h, fineGrain + broadWash * 0.12h);
                washColor = lerp(washColor, _PaperColor.rgb, paperLift * _Granulation * 0.34h);

                // Pigment accumulates just inside the alpha-cut petal silhouette,
                // producing the darker wet edge typical of a watercolour wash.
                half alphaWidth = max(fwidth(atlas.a) * 5.0h, 0.04h);
                half insideEdge = saturate((atlas.a - _Cutoff) / alphaWidth);
                half pooledEdge = 1.0h - smoothstep(0.08h, 1.0h, insideEdge);
                pooledEdge *= _EdgePooling * lerp(0.72h, 1.18h, broadWash);
                half3 pooledPigment = pigmentHue * lerp(0.43h, 0.62h, fineGrain);
                washColor = lerp(washColor, pooledPigment, saturate(pooledEdge));

                // A second ink rim is drawn just inside the alpha silhouette. Its
                // width is measured through screen-space derivatives, so the line
                // remains visible after the small trail flowers recede from camera.
                half alphaDerivative = max(fwidth(atlas.a), 0.001h);
                half alphaDistance = (atlas.a - effectiveCutoff) / alphaDerivative;
                half innerInk = 1.0h - smoothstep(0.18h, max(0.5h, (half)_InnerInkWidth), alphaDistance);
                innerInk *= lerp(0.76h, 1.0h, broadWash);
                washColor = lerp(washColor, _OutlineColor.rgb, saturate(innerInk * 0.94h));

                Light mainLight = GetMainLight();
                half halfLambert = dot(normalize(input.normalWS), mainLight.direction) * 0.5h + 0.5h;
                half litWash = smoothstep(_ShadeStep - 0.18h, _ShadeStep + 0.20h, halfLambert);
                half shadeValue = lerp(0.82h, 1.0h, litWash);
                half3 shadowTint = lerp(half3(1.0h, 1.0h, 1.0h), _ShadeColor.rgb, (1.0h - litWash) * 0.18h);
                half3 lightColor = lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color, 0.22h);
                return half4(washColor * shadeValue * shadowTint * lightColor, 1.0h);
            }
            ENDHLSL
        }
    }
}
