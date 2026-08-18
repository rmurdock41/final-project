Shader "Okami/Environment/GroundWatercolor"
{
    Properties
    {
        [MainColor] _BaseColor("Earth Pigment", Color) = (0.39, 0.43, 0.22, 1)
        _WashColor("Fresh Wash", Color) = (0.58, 0.57, 0.30, 1)
        _PaperColor("Paper Tint", Color) = (0.82, 0.78, 0.57, 1)
        _ShadowColor("Original Shadow Tint", Color) = (0.14, 0.17, 0.095, 1)
        _InkWashColor("Ink Wash Interior", Color) = (0.07, 0.105, 0.055, 1)
        _InkPoolColor("Pooled Ink Edge", Color) = (0.006, 0.012, 0.005, 1)
        _WashScale("Wash Scale", Range(0.03, 0.5)) = 0.105
        _WashStrength("Wash Strength", Range(0, 1)) = 0.58
        _PaperScale("Paper Grain Scale", Range(1, 30)) = 9
        _PaperStrength("Paper Grain Strength", Range(0, 0.3)) = 0.075
        _PigmentSteps("Pigment Steps", Range(2, 8)) = 5
        _ShadowStrength("Original Shadow Strength", Range(0, 1)) = 0
        _InkWashOpacity("Ink Wash Opacity", Range(0, 1)) = 0.82
        _InkEdgeWidth("Shadow Blur Radius (Shadow Texels)", Range(1, 128)) = 10
        _InkEdgeWobble("Brush Edge Warp (Shadow Texels)", Range(0, 24)) = 9
        _InkBreakup("Dry Brush Breakup", Range(0, 1)) = 0.34
        _InkGranulation("Shadow Granulation", Range(0, 1)) = 0.42
        _InkEdgePooling("Wet Edge Pooling", Range(0, 1)) = 0.72
        _InkEdgeSoftness("Shadow Edge Softness", Range(0, 1)) = 0.55
        _InkCoveragePower("Thin Shadow Ink Boost", Range(0.2, 1.5)) = 0.38
        [NoScaleOffset] _BrushTexture("Shadow Brush Texture", 2D) = "white" {}
        [NoScaleOffset] _BrushOverlay("Shadow Brush Flying White", 2D) = "black" {}
        _ShadowBrushScale("Shadow Brush World Scale", Range(0.02, 0.5)) = 0.115
        _ShadowBrushStrength("Shadow Brush Strength", Range(0, 1)) = 0.88
        _ShadowBrushCutout("Shadow Brush Flying White", Range(0, 1)) = 0.72
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 3.0
            #pragma vertex GroundVertex
            #pragma fragment GroundFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _WashColor;
                half4 _PaperColor;
                half4 _ShadowColor;
                half4 _InkWashColor;
                half4 _InkPoolColor;
                float _WashScale;
                float _WashStrength;
                float _PaperScale;
                float _PaperStrength;
                float _PigmentSteps;
                float _ShadowStrength;
                float _InkWashOpacity;
                float _InkEdgeWidth;
                float _InkEdgeWobble;
                float _InkBreakup;
                float _InkGranulation;
                float _InkEdgePooling;
                float _InkEdgeSoftness;
                float _InkCoveragePower;
                float _ShadowBrushScale;
                float _ShadowBrushStrength;
                float _ShadowBrushCutout;
            CBUFFER_END

            TEXTURE2D(_BrushTexture);
            SAMPLER(sampler_BrushTexture);
            TEXTURE2D(_BrushOverlay);
            SAMPLER(sampler_BrushOverlay);

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

            half2 ShadowBrushPattern(float2 worldUV)
            {
                // This is the same texture pair and subtraction idea used by the
                // game's drawing Brush Shader. World-space projection keeps the
                // dry-brush marks attached to the ground instead of the camera.
                float2 brushUV = worldUV * _ShadowBrushScale;
                float2 brushUVRotated = float2(-brushUV.y, brushUV.x) * 0.73 + float2(0.31, 0.67);

                half mainAlphaA = SAMPLE_TEXTURE2D(_BrushTexture, sampler_BrushTexture, brushUV).a;
                half mainAlphaB = SAMPLE_TEXTURE2D(_BrushTexture, sampler_BrushTexture, brushUVRotated).a;
                half mainStroke = saturate(max(mainAlphaA, mainAlphaB * 0.82h));

                half overlayA = SAMPLE_TEXTURE2D(_BrushOverlay, sampler_BrushOverlay, brushUV * 1.37 + float2(0.17, 0.43)).a;
                half overlayB = SAMPLE_TEXTURE2D(_BrushOverlay, sampler_BrushOverlay, brushUVRotated * 1.61 + float2(0.59, 0.11)).a;
                half overlayStroke = saturate(overlayA * 0.68h + overlayB * 0.46h);

                // Brush Shader uses smoothstep(Simple Noise) to reveal the light
                // overlay. The extra low-frequency term groups the gaps into
                // deliberate flying-white streaks instead of television static.
                half revealNoise = smoothstep(0.45h, 0.58h, ValueNoise(worldUV * 0.74 + float2(31.7, 12.9)));
                half groupedNoise = smoothstep(0.32h, 0.74h, ValueNoise(worldUV * 2.9 + float2(7.4, 55.2)));
                half flyingWhite = saturate(overlayStroke * revealNoise * lerp(0.55h, 1.0h, groupedNoise));
                half retainedInk = saturate(mainStroke - flyingWhite * _ShadowBrushCutout);
                // Match the original material's alpha-clip character, but soften
                // the threshold just enough for a painted shadow to keep tonal
                // variation instead of becoming a hard checkerboard.
                half clippedBrushInk = smoothstep(0.38h, 0.74h, retainedInk);

                // x controls pigment inside the silhouette; y supplies a broken
                // brush rim. Keep a little base coverage so shadows stay legible.
                half body = lerp(1.0h, lerp(0.08h, 1.0h, clippedBrushInk), _ShadowBrushStrength);
                half rim = lerp(0.24h, 1.0h, clippedBrushInk);
                return half2(body, rim);
            }

            half SampleInkShadowCoverage(float4 shadowCoord)
            {
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                // Sample the comparison texture directly. Calling
                // MainLightRealtimeShadow here would run URP's own 9-tap filter
                // for every one of our 25 Gaussian taps (225 reads per pixel).
                // The direct read gives the ink filter an unmodified silhouette.
                if (BEYOND_SHADOW_FAR(shadowCoord))
                    return 0.0h;
                half rawAttenuation = SAMPLE_TEXTURE2D_SHADOW(
                    _MainLightShadowmapTexture,
                    sampler_MainLightShadowmapTexture,
                    shadowCoord.xyz);
                return saturate(1.0h - rawAttenuation);
            #else
                return 0.0h;
            #endif
            }

            half SampleSuperSoftShadow(float4 centerCoord, float2 sampleStep)
            {
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                // Separable 1-4-6-4-1 Gaussian kernel evaluated as a 5x5 grid.
                // This intentionally favours a very visible, cinematic feather;
                // after the visual direction is approved the radius/tap count can
                // be reduced for the final performance budget.
                half weightedCoverage = 0.0h;
                [unroll]
                for (int y = -2; y <= 2; y++)
                {
                    half wy = (abs(y) == 2) ? 1.0h : ((abs(y) == 1) ? 4.0h : 6.0h);
                    [unroll]
                    for (int x = -2; x <= 2; x++)
                    {
                        half wx = (abs(x) == 2) ? 1.0h : ((abs(x) == 1) ? 4.0h : 6.0h);
                        float4 sampleCoord = centerCoord;
                        sampleCoord.xy += float2(x, y) * sampleStep;
                        weightedCoverage += SampleInkShadowCoverage(sampleCoord) * wx * wy;
                    }
                }
                return weightedCoverage * (1.0h / 256.0h);
            #else
                return 0.0h;
            #endif
            }

            half2 InkShadowData(float3 positionWS, float2 worldUV, half paperGrain)
            {
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                // Distort the lookup in light-space rather than moving the ground.
                // The low-frequency world-space noise keeps the brush edge stable
                // while the camera moves.
                half warpX = ValueNoise(worldUV * 0.72 + float2(17.31, 4.73));
                half warpY = ValueNoise(worldUV * 0.72 + float2(53.17, 29.41));
                half breakupNoise = ValueNoise(worldUV * 3.35 + float2(8.13, 61.73));
                float2 shadowTexel = _MainLightShadowmapSize.xy;
                float2 warpedOffset = (float2(warpX, warpY) - 0.5) * 2.0 * shadowTexel * _InkEdgeWobble;

                float4 centerCoord = TransformWorldToShadowCoord(positionWS);
                centerCoord.xy += warpedOffset;

                // Sample an eight-point ring around the warped silhouette. The
                // maximum is a dilation, the minimum is an erosion, and their
                // difference is a real, controllable ink boundary.
                float2 radius = shadowTexel * _InkEdgeWidth;
                float2 diagonalRadius = radius * 0.70710678;
                half center = SampleInkShadowCoverage(centerCoord);
                half s0 = SampleInkShadowCoverage(centerCoord + float4( radius.x, 0.0, 0.0, 0.0));
                half s1 = SampleInkShadowCoverage(centerCoord + float4(-radius.x, 0.0, 0.0, 0.0));
                half s2 = SampleInkShadowCoverage(centerCoord + float4(0.0,  radius.y, 0.0, 0.0));
                half s3 = SampleInkShadowCoverage(centerCoord + float4(0.0, -radius.y, 0.0, 0.0));
                half s4 = SampleInkShadowCoverage(centerCoord + float4( diagonalRadius.x,  diagonalRadius.y, 0.0, 0.0));
                half s5 = SampleInkShadowCoverage(centerCoord + float4(-diagonalRadius.x,  diagonalRadius.y, 0.0, 0.0));
                half s6 = SampleInkShadowCoverage(centerCoord + float4( diagonalRadius.x, -diagonalRadius.y, 0.0, 0.0));
                half s7 = SampleInkShadowCoverage(centerCoord + float4(-diagonalRadius.x, -diagonalRadius.y, 0.0, 0.0));

                half ringMaximum = max(max(max(s0, s1), max(s2, s3)), max(max(s4, s5), max(s6, s7)));
                half ringMinimum = min(min(min(s0, s1), min(s2, s3)), min(min(s4, s5), min(s6, s7)));
                half dilatedCoverage = max(center, ringMaximum);
                half erodedCoverage = min(center, ringMinimum);
                half edgeBand = saturate(dilatedCoverage - erodedCoverage);
                // Five samples span the full configured radius on each axis.
                // At the maximum-soft test setting this creates an unmistakably
                // broad transition rather than only antialiasing the silhouette.
                half featheredCoverage = SampleSuperSoftShadow(centerCoord, radius * 0.5);

                // The body deliberately expands into the irregular wet edge.
                // Dry-brush noise only removes pigment from that edge; the centre
                // of the cast shadow remains solid and readable.
                half dryBrush = smoothstep(0.28h, 0.72h, breakupNoise + (paperGrain - 0.5h) * 0.35h);
                half2 brushPattern = ShadowBrushPattern(worldUV);
                half edgeRetention = lerp(1.0h, lerp(0.18h, 1.0h, dryBrush), _InkBreakup) * brushPattern.y;
                half shadowCoverage = saturate(erodedCoverage + edgeBand * edgeRetention);
                shadowCoverage = lerp(shadowCoverage, featheredCoverage * brushPattern.y, _InkEdgeSoftness);
                shadowCoverage = pow(shadowCoverage, _InkCoveragePower);

                // Broad pigment blooms and fine paper grain produce obvious tonal
                // variation inside the shadow without changing its identity.
                half broadInk = ValueNoise(worldUV * 0.38 + float2(73.21, 14.37));
                half fineInk = ValueNoise(worldUV * 5.1 + float2(12.83, 91.07));
                half inkPattern = saturate(broadInk * 0.55h + fineInk * 0.30h + paperGrain * 0.15h);
                half steppedInk = floor(inkPattern * 4.0h) / 3.0h;
                half inkDensity = lerp(1.0h, lerp(0.46h, 1.0h, saturate(steppedInk)), _InkGranulation);
                shadowCoverage *= inkDensity * brushPattern.x;

                // Pool ink primarily on the retained portions of the wet edge.
                // The low exponent deliberately turns even a partially covered
                // boundary into a readable brush stroke instead of a faint halo.
                half pooledEdge = pow(saturate(edgeBand), 0.24h);
                pooledEdge *= lerp(0.46h, 1.0h, dryBrush);
                pooledEdge *= lerp(0.78h, 1.0h, broadInk);
                pooledEdge *= brushPattern.y;
                return half2(saturate(shadowCoverage), saturate(pooledEdge));
            #else
                return half2(0.0h, 0.0h);
            #endif
            }

            Varyings GroundVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 GroundFragment(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 absoluteNormal = abs(normalWS);
                float2 worldUV = input.positionWS.xz;
                if (absoluteNormal.x > absoluteNormal.y && absoluteNormal.x > absoluteNormal.z)
                    worldUV = input.positionWS.zy;
                else if (absoluteNormal.z > absoluteNormal.y)
                    worldUV = input.positionWS.xy;

                // Stable world-space pigment washes: no ground UVs or extra textures required.
                half broadWash = ValueNoise(worldUV * _WashScale);
                half mediumWash = ValueNoise(worldUV * (_WashScale * 3.17) + 19.37);
                half paperGrain = ValueNoise(worldUV * _PaperScale + 43.13);

                half wash = saturate(broadWash * 0.72h + mediumWash * 0.28h);
                half steps = max(2.0h, (half)_PigmentSteps);
                half steppedWash = floor(wash * steps + paperGrain * 0.28h) / max(1.0h, steps - 1.0h);
                wash = saturate(lerp(wash, steppedWash, 0.42h) * _WashStrength + 0.20h);

                half3 pigment = lerp(_BaseColor.rgb, _WashColor.rgb, wash);
                half paperAmount = saturate((paperGrain - 0.5h) * 2.0h) * _PaperStrength;
                pigment = lerp(pigment, _PaperColor.rgb, paperAmount);
                pigment *= lerp(1.0h - _PaperStrength, 1.0h + _PaperStrength * 0.45h, paperGrain);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half diffuse = smoothstep(0.12h, 0.58h, saturate(dot(normalWS, mainLight.direction)));
                half3 lightTint = lerp(half3(0.82h, 0.84h, 0.75h), mainLight.color, 0.42h);
                half3 litColor = pigment * lerp(0.86h, 1.08h, diffuse) * lightTint;
                litColor += SampleSH(normalWS) * pigment * 0.12h;

                half rawShadowCoverage = 1.0h - mainLight.shadowAttenuation;
                half2 paintedShadow = InkShadowData(input.positionWS, worldUV, paperGrain);
                half paintedShadowCoverage = paintedShadow.x;
                // Do not combine this with the unwarped Unity sample: retaining
                // both silhouettes is what caused the visible double/triple shadow.
                half shadowCoverage = paintedShadowCoverage;
                // The ordinary Unity shadow and the painted replacement are
                // deliberately independent so either can be inspected alone.
                half originalShadowAmount = saturate(rawShadowCoverage * _ShadowStrength);
                half3 finalColor = lerp(litColor, _ShadowColor.rgb, originalShadowAmount);
                // Convert the deposited pigment into a strong, stepped wash. A
                // smooth linear multiply made the old result indistinguishable
                // from Unity's ordinary grey shadow.
                // Keep the full coverage range here. Clamping everything above
                // 0.48 to solid ink erased the brush texture inside dark shadows.
                half inkWashAmount = smoothstep(0.025h, 0.96h, shadowCoverage) * _InkWashOpacity;
                half3 granularInkColor = lerp(_InkWashColor.rgb * 0.62h, _InkWashColor.rgb * 1.32h, paperGrain);
                finalColor = lerp(finalColor, granularInkColor, saturate(inkWashAmount));
                half pooledEdgeAmount = smoothstep(0.07h, 0.46h, paintedShadow.y) * _InkEdgePooling;
                finalColor = lerp(finalColor, _InkPoolColor.rgb, pooledEdgeAmount);
                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
