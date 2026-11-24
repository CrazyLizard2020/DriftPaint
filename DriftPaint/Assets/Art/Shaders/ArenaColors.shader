Shader "DriftPaint/Tribunas"
{
    // Tribunas, torres y ciudad horneadas en una sola malla con color por vértice. Es fondo: no recibe
    // sombras y se resuelve con una luz fija, pigmento de tinta y bruma hacia el color del cielo con la
    // distancia. Los vértices blancos son los focos de las torres y brillan para el Bloom.
    Properties
    {
        _Haze("Bruma del horizonte", Color) = (0.66, 0.50, 0.68, 1)
        _HazeDistance("Bruma: inicio y fin (m)", Vector) = (60, 190, 0, 0)
        _LampGlow("Brillo de los focos", Float) = 2.4
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _Haze;
        float4 _HazeDistance;
        half _LampGlow;
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
                half lamp = step(.97, min(i.color.r, min(i.color.g, i.color.b)));
                half light = .62 + .38 * saturate(dot(n, normalize(half3(-.4, .8, .3))));
                float2 p = InkPlanar(i.world, n);
                half pigment = InkFbm(p * .12);
                half3 color = i.color.rgb * light * (.9 + .2 * pigment);
                float distance = length(GetCameraPositionWS() - i.world);
                half haze = smoothstep(_HazeDistance.x, _HazeDistance.y, distance) * .35;
                color = lerp(color, _Haze.rgb, haze * (1 - lamp));
                return half4(lerp(color, i.color.rgb * _LampGlow, lamp), 1);
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
