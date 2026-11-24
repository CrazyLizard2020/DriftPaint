Shader "DriftPaint/Tribunas"
{
    // Tribunas, torres y ciudad horneadas en una sola malla con color por vértice. Es fondo: no recibe
    // sombras y se resuelve con una luz fija, pigmento de tinta y bruma hacia el color del cielo con la
    // distancia. Los vértices blancos son los focos de las torres y brillan para el Bloom.
    Properties
    {
        _Haze("Bruma del horizonte", Color) = (0.66, 0.50, 0.68, 1)
        _HazeDistance("Bruma: inicio y fin (m)", Vector) = (60, 190, 0, 0)
        _LampGlow("Brillo de los focos", Float) = 4.5
        _PrintScale("Puntos por metro", Range(.5,12)) = 2.7
        _PrintStrength("Trama de tribunas", Range(0,1)) = .76
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _Haze;
        float4 _HazeDistance;
        half _LampGlow, _PrintScale, _PrintStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "InkCommon.hlsl"

            struct Link
            {
                float4 clip : SV_POSITION;
                half4 color : COLOR;
                float3 world : TEXCOORD0;
                half3 normal : TEXCOORD1;
            };

            Link Vert(float4 vertex : POSITION, float3 n : NORMAL, half4 color : COLOR)
            {
                Link o;
                o.world = TransformObjectToWorld(vertex.xyz);
                o.clip = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(n);
                o.color = color;
                return o;
            }

            half4 Frag(Link i) : SV_Target
            {
                half3 n = normalize(i.normal);
                half lamp = step(.96, i.color.r) * step(.93, i.color.g) * step(.58, i.color.b);
                half light = .62 + .38 * saturate(dot(n, normalize(half3(-.4, .8, .3))));
                float2 p = InkPlanar(i.world, n);
                half pigment = InkFbm(p * .12);
                half3 color = i.color.rgb * light * (.9 + .2 * pigment);
                float distance = length(GetCameraPositionWS() - i.world);
                half haze = smoothstep(_HazeDistance.x, _HazeDistance.y, distance) * .35;
                color = lerp(color, _Haze.rgb, haze * (1 - lamp));
                half shade = saturate(1 - light + .65 - dot(color, half3(.21,.72,.07)));
                color *= 1 - InkDots(p, _PrintScale, shade) * _PrintStrength * shade;
                half3 emission = lerp(half3(1,.38,.045), half3(1,.86,.62), step(.96,i.color.b));
                return half4(lerp(color, emission * _LampGlow, lamp), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex InkDepthVert
            #pragma fragment InkDepthFrag
            #include "InkPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex InkDepthVert
            #pragma fragment InkNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "InkPasses.hlsl"
            ENDHLSL
        }
    }
}
