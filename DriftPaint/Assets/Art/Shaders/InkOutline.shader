Shader "Hidden/DriftPaint/Contorno a tinta"
{
    // Contorno de pantalla completa que corre antes de los transparentes. Busca bordes en la profundidad y
    // en las normales de la escena, así que el piso plano, la pintura, las partículas y la UI no generan
    // líneas: no tienen saltos de geometría o no escriben profundidad.
    Properties
    {
        _InkColor("Color de la línea", Color) = (.08, .07, .14, 1)
        _Strength("Opacidad", Range(0, 1)) = .85
        _DepthThreshold("Umbral de profundidad", Range(.005, .5)) = .06
        _NormalThreshold("Umbral de normales", Range(0, 1)) = .5
        _FadeDistance("Desvanecido: inicio y fin (m)", Vector) = (25, 120, 0, 0)
        _FarStrength("Opacidad lejos", Range(0, 1)) = .3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "Contorno"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _InkColor;
            half _Strength;
            float _DepthThreshold;
            half _NormalThreshold;
            float4 _FadeDistance;
            half _FarStrength;

            // Inversa de la distancia a la cámara: sobre una superficie plana varía en forma lineal en la
            // pantalla, así que su laplaciano solo se aleja de cero en las siluetas y los quiebres.
            float InverseDepth(float2 uv)
            {
                return 1.0 / LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                // Un píxel de grosor a 1080p; a más resolución la línea crece en la misma proporción.
                float2 texel = _ScreenSize.zw * max(1.0, round(_ScreenSize.y / 1080.0));
                float2 offsets[4] = { float2(texel.x, 0), float2(-texel.x, 0), float2(0, texel.y), float2(0, -texel.y) };

                float center = InverseDepth(uv);
                half3 normal = SampleSceneNormals(uv);
                float depthSum = 0;
                half normalEdge = 0;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    float2 neighbourUv = uv + offsets[k];
                    float neighbour = InverseDepth(neighbourUv);
                    depthSum += center - neighbour;
                    // Solo marca el lado más cercano del quiebre, así la línea mide un píxel y no dos.
                    half nearer = step(neighbour, center * 1.002);
                    half3 neighbourNormal = SampleSceneNormals(neighbourUv);
                    normalEdge = max(normalEdge, nearer * (1 - dot(normal, neighbourNormal)));
                }

                // Con el centro más cerca que sus vecinos la suma es positiva: es una silueta.
                half depthEdge = saturate((depthSum / center - _DepthThreshold) / _DepthThreshold);
                normalEdge = saturate((normalEdge - _NormalThreshold) / max(1e-3, 1 - _NormalThreshold) * 3);
                half edge = max(depthEdge, normalEdge);

                float eye = 1.0 / center;
                half fade = lerp(1, _FarStrength, smoothstep(_FadeDistance.x, _FadeDistance.y, eye));
                color.rgb = lerp(color.rgb, _InkColor.rgb, edge * _Strength * fade);
                return color;
            }
            ENDHLSL
        }
    }
}
