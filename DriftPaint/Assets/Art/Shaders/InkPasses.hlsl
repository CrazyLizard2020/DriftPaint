#ifndef DRIFTPAINT_INK_PASSES_INCLUDED
#define DRIFTPAINT_INK_PASSES_INCLUDED

// Pases de profundidad, normales y sombra para superficies opacas sin recorte. Sin ellos la superficie no
// existe para el SSAO ni para el contorno, y no proyecta sombra. Cada shader declara su CBUFFER antes de
// incluir este archivo, igual en todos sus pases, para que siga siendo compatible con el SRP Batcher.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct InkDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

struct InkDepthVaryings
{
    float4 positionCS : SV_POSITION;
    half3 normalWS : TEXCOORD0;
};

InkDepthVaryings InkDepthVert(InkDepthAttributes input)
{
    InkDepthVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half InkDepthFrag(InkDepthVaryings input) : SV_Target
{
    return input.positionCS.z;
}

half4 InkNormalsFrag(InkDepthVaryings input) : SV_Target
{
    // URP Forward guarda la normal en RGB. Alfa identifica el material para el trazo interior.
    #ifndef INK_OUTLINE_GROUP
        #define INK_OUTLINE_GROUP 0.0
    #endif
    half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 octNormal = PackNormalOctQuadEncode(normalWS);
        return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), INK_OUTLINE_GROUP);
    #else
        return half4(normalWS, INK_OUTLINE_GROUP);
    #endif
}

#if defined(INK_SHADOW_CASTER)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// Dirección de la luz que URP carga antes de dibujar el mapa de sombras.
float3 _LightDirection;

InkDepthVaryings InkShadowVert(InkDepthAttributes input)
{
    InkDepthVaryings output;
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    output.positionCS = positionCS;
    output.normalWS = normalWS;
    return output;
}

half4 InkShadowFrag(InkDepthVaryings input) : SV_Target
{
    return 0;
}
#endif

#endif
