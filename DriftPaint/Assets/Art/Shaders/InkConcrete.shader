Shader "DriftPaint/Concreto entintado"
{
    // Piso, paredes, bloques y jardines: color plano con pigmento, manchas y grano de tinta anclados al
    // mundo, y sombra del sol con borde de pincel. Recibe sombras y SSAO, y proyecta sombra.
    Properties
    {
        _BaseMap("Textura", 2D) = "white" {}
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Ink("Tinta", Range(0, 1)) = 1
        _PrintScale("Puntos por metro", Range(1, 80)) = 21
        _PrintStrength("Trama en sombra", Range(0,1)) = .76
        _Rim("Luz de borde del esmalte", Range(0,.6)) = .16
        _FloorSheen("Reflejo rasante del atardecer", Range(0,1)) = 0
        [Enum(Concreto,0,Esmalte,1,Vidrio,2)] _Enamel("Acabado", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half _Ink, _PrintScale, _PrintStrength, _Enamel, _Rim, _FloorSheen;
        CBUFFER_END
        ENDHLSL

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

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                if (_Enamel > .5)
                {
                    Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                    half facing = saturate(dot(normalWS, sun.direction)) * sun.shadowAttenuation;
                    // Orientación local y escala en metros para que los puntos viajen con cada modelo.
                    float3x3 basis = (float3x3)GetObjectToWorldMatrix();
                    float3 scale = float3(length(basis._m00_m10_m20), length(basis._m01_m11_m21), length(basis._m02_m12_m22));
                    float3 local = TransformWorldToObject(input.positionWS) * scale;
                    float2 p = InkPlanar(local, TransformWorldToObjectNormal(normalWS));
                    half dots = InkDots(p, _PrintScale, 1 - facing) * _PrintStrength * (1 - .85 * facing);
                    half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    if (_Enamel > 1.5)
                    {
                        float3 reflected = reflect(-view, normalWS);
                        float cloud = InkFbm(reflected.xz * float2(5, 8) + reflected.y * 3);
                        float wash = smoothstep(.30,.64,cloud) * .55 + .35 * saturate(reflected.y);
                        half3 glass = lerp(albedo * .70, albedo * 1.30 + half3(.18,.075,.08), wash);
                        float dots = InkDots(p, _PrintScale, .75 - .5 * wash);
                        return half4(glass * (1 - _PrintStrength * dots * .61), 1);
                    }
                    half alignment = saturate(dot(normalWS, SafeNormalize(sun.direction + view)));
                    half shine = (.14 * pow(alignment, 16) + smoothstep(.968, .994, alignment)) * sun.shadowAttenuation;
                    half band = .62 * smoothstep(.12,.44,facing) + .38 * smoothstep(.55,.83,facing);
                    half3 enamel = albedo * lerp(half3(.48, .62, .72), half3(1.28, 1.18, .94), band);
                    half rim = pow(1-saturate(dot(view,normalWS)), 4) * facing * _Rim;
                    return half4(enamel * (1 - .58 * dots) + half3(.90, .66, .38) * (shine + rim), 1);
                }
                half3 concrete = InkShade(albedo, input.positionWS, normalWS, input.positionCS, _Ink, _PrintScale, _PrintStrength);
                if (_FloorSheen > 0)
                {
                    Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                    float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    float axis = saturate(dot(-view.xz, normalize(sun.direction.xz + 1e-5)));
                    float brush = InkFbm(input.positionWS.xz * float2(.42, 2.8));
                    concrete += half3(.54,.13,.032) * (_FloorSheen * pow(axis, 20) * (1 - saturate(view.y)) *
                                sun.shadowAttenuation * (.15 + brush));
                }
                return half4(concrete, 1);
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
            #define INK_OUTLINE_GROUP (_Enamel > .5 ? .25 : .75)
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "InkPasses.hlsl"
            ENDHLSL
        }
    }
}
