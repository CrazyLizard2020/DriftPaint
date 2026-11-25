Shader "Hidden/DriftPaint/Contorno a tinta"
{
    // Contorno de pantalla completa que corre antes de los transparentes. Busca bordes en la profundidad y
    // en las normales de la escena, así que el piso plano, la pintura, las partículas y la UI no generan
    // líneas: no tienen saltos de geometría o no escriben profundidad.
    Properties
    {
        _InkColor("Color de la línea", Color) = (.08, .07, .14, 1)
        _Strength("Opacidad", Range(0, 1)) = .85
        [Enum(Continuo,0,Pincelada,1)] _BrushMode("Estilo de línea", Float) = 1
        _BrushPressure("Variación de presión", Range(0,1)) = .55
        _DryBrush("Pincel seco", Range(0,1)) = .48
        _BrushLength("Longitud de pincelada (px a 1080p)", Range(8,100)) = 40
        _CarWidth("Carrocería y cristales (px a 1080p)", Range(0,12)) = 3.6
        _RampWidth("Rampas (px)", Range(0,12)) = 3
        _ConcreteWidth("Piso, muros y bloques de concreto (px)", Range(0,12)) = 2
        _ArenaWidth("Tribunas y ciudad (px)", Range(0,12)) = 1.8
        _Width("Otros opacos, ruedas y props (px)", Range(0,12)) = 2.3
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
            Stencil { Ref 0 ReadMask 1 Comp Equal }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _InkColor;
            half _Strength;
            float _Width;
            float _CarWidth, _RampWidth, _ConcreteWidth, _ArenaWidth;
            float _BrushMode, _BrushPressure, _DryBrush, _BrushLength;
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

            float GroupWidth(float2 uv)
            {
                float2 normalUv = ClampAndScaleUVForBilinear(UnityStereoTransformScreenSpaceTex(uv), _CameraNormalsTexture_TexelSize.xy);
                float group = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_PointClamp, normalUv).a;
                if (group < .125) return _Width;
                if (group < .375) return _CarWidth;
                if (group < .625) return _RampWidth;
                if (group < .875) return _ConcreteWidth;
                return _ArenaWidth;
            }

            // Papel fijo en pantalla. Los cambios de presión son anchos y las fibras son alargadas.
            // Las frecuencias más finas se atenúan a resoluciones pequeñas.
            float2 Brush(float2 uv)
            {
                float2 paper = uv * _ScreenSize.xy * (1080.0 / _ScreenSize.y);
                float2 axis = float2(dot(paper, float2(.94,.34)), dot(paper, float2(-.34,.94)));
                float phase = axis.x / max(1, _BrushLength);
                float pressure = sin(phase * 3.1 + sin(axis.y * .06)) * cos(phase * 1.3 + axis.y * .034);
                float fibers = sin(axis.y * 1.53 + sin(phase * 2.3)) * cos(phase * 3.7 + axis.y * .23);
                float filter = saturate(_ScreenSize.y / 1080.0);
                return float2(1 + .72 * _BrushPressure * pressure,
                              1 - _DryBrush * smoothstep(-.2, .78, fibers) * filter);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                // Un píxel de grosor a 1080p; a más resolución la línea crece en la misma proporción.
                float center = InverseDepth(uv);
                if (center < 1.001 / _ProjectionParams.z) return color;
                float distanceFade = smoothstep(_FadeDistance.x, _FadeDistance.y, 1.0 / center);
                float width = GroupWidth(uv);
                if (width <= 0) return color;
                float2 brush = float2(1,1);
                if (_BrushMode > .5) brush = Brush(uv);
                float radius = lerp(width, min(width, 1), distanceFade) * brush.x * _ScreenSize.y / 1080.0;
                float2 texel = _ScreenSize.zw * max(1.0, radius);
                float2 offsets[4] = { float2(texel.x, 0), float2(-texel.x, 0), float2(0, texel.y), float2(0, -texel.y) };

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

                half fade = lerp(1, _FarStrength, distanceFade);
                color.rgb = lerp(color.rgb, _InkColor.rgb, edge * _Strength * fade * saturate(radius) * brush.y);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "Excluir del contorno"
            Tags { "LightMode"="InkOutlineMask" }
            ZTest Equal ZWrite Off ColorMask 0 Cull Off
            Stencil { Ref 1 WriteMask 1 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment MaskFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 MaskVert(float4 vertex : POSITION) : SV_POSITION { return TransformObjectToHClip(vertex.xyz); }
            half4 MaskFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
