Shader "DriftPaint/Rampa de pista"
{
    // Rampa con losetas y bandas de seguridad en los bordes, dibujadas en el espacio del objeto para que
    // acompañen el tamaño de cada rampa. Recibe la luz y la tinta de las demás superficies del escenario.
    Properties
    {
        _PrintScale("Puntos por metro", Range(1,48)) = 8
        _PrintStrength("Trama en sombra", Range(0,1)) = .85
        _StripeLight("Banda clara", Color) = (1,.75,.12,1)
        _StripeDark("Banda oscura", Color) = (.22,.17,.28,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "InkLighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half _PrintScale, _PrintStrength;
            half4 _StripeLight, _StripeDark;
            CBUFFER_END

            struct Link
            {
                float4 clip : SV_POSITION;
                float3 local : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 world : TEXCOORD2;
                half3 normalWS : TEXCOORD3;
            };

            Link Vert(float4 vertex : POSITION, float3 normal : NORMAL)
            {
                Link o;
                o.local = vertex.xyz;
                o.normal = normal;
                o.world = TransformObjectToWorld(vertex.xyz);
                o.normalWS = TransformObjectToWorldNormal(normal);
                o.clip = TransformWorldToHClip(o.world);
                return o;
            }

            half4 Frag(Link i) : SV_Target
            {
                float2 uv = i.local.xz + .5;
                half3 base = lerp(half3(.30, .29, .36), half3(.50, .47, .55), saturate(i.normal.y));
                float2 seam = abs(frac(uv * float2(6, 8)) - .5);
                base *= 1 - .18 * smoothstep(.47, .5, max(seam.x, seam.y));
                float outside = abs(i.local.x);
                float stripes = step(.40, frac(uv.y * 5 - outside * 5));
                float edge = step(.34, outside);
                half3 hazard = lerp(_StripeDark.rgb, _StripeLight.rgb, stripes);
                half3 albedo = lerp(base, hazard, edge);
                // Filete continuo sobre la cara superior para leer la rampa desde el piso.
                albedo = lerp(albedo, _StripeLight.rgb, step(.474, outside) * saturate(i.normal.y));
                // Las bandas de seguridad llevan menos tinta para que se lean desde lejos.
                half ink = lerp(1, .45, edge);
                half3 shaded = InkShade(albedo, i.world, NormalizeNormalPerPixel(i.normalWS), i.clip, ink, _PrintScale, _PrintStrength);
                return half4(shaded + _StripeLight.rgb * edge * stripes * .33, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex InkShadowVert
            #pragma fragment InkShadowFrag
            #define INK_SHADOW_CASTER
            #include "InkPasses.hlsl"
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
            #define INK_OUTLINE_GROUP .50
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "InkPasses.hlsl"
            ENDHLSL
        }
    }
}
