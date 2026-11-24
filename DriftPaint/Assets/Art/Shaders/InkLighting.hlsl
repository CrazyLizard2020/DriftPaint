#ifndef DRIFTPAINT_INK_LIGHTING_INCLUDED
#define DRIFTPAINT_INK_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "InkCommon.hlsl"

// Distancia a la que el detalle fino termina de apagarse: lejos solo queda el pigmento de baja frecuencia.
#define INK_DETAIL_NEAR 16.0
#define INK_DETAIL_FAR 70.0

// Iluminación de las superficies entintadas: luz del sol con sombras, ambiente del cielo y SSAO, más una capa
// de tinta anclada al mundo. ink va de 0 (superficie limpia) a 1 (tinta completa).
half3 InkShade(half3 albedo, float3 positionWS, half3 normalWS, float4 positionCS, half ink, float frequency, half strength)
{
    float distanceToCamera = length(GetCameraPositionWS() - positionWS);
    half near = 1.0 - smoothstep(INK_DETAIL_NEAR, INK_DETAIL_FAR, distanceToCamera);
    float2 p = InkPlanar(positionWS, normalWS);

    // Pigmento irregular y manchas grandes de tinta, como un lavado sobre papel.
    half pigment = InkFbm(p * 0.09);
    half stain = smoothstep(0.6, 0.8, InkFbm(p * 0.21 + 41.7));
    half grain = InkNoise(p * 2.7) - 0.5;
    half3 color = albedo * (1.0 + (pigment - 0.5) * 0.24 * ink);
    color *= 1.0 - stain * 0.22 * ink;
    color *= 1.0 + grain * 0.10 * ink * near;
    float2 paperUV = p * 28;
    float2 paperFootprint = fwidth(paperUV);
    half paper = (InkNoise(paperUV) - .5) * (1 - smoothstep(.32, 1, max(paperFootprint.x, paperFootprint.y)));
    color *= 1 + paper * .54 * ink;

    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light sun = GetMainLight(shadowCoord);
    half facing = saturate(dot(normalWS, sun.direction));

    // El borde de la sombra se corre con trazos alargados, así parece pintado a pincel.
    half stroke = InkNoise(p * float2(0.45, 2.6));
    half shadow = smoothstep(0.22, 0.78, sun.shadowAttenuation + (stroke - 0.5) * 0.5 * ink);
    // Dentro de la sombra aparecen algunos trazos más oscuros.
    half hatch = (1.0 - shadow * facing) * smoothstep(0.62, 0.92, stroke) * 0.14 * ink * near;

    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(positionCS));
    half3 ambient = SampleSH(normalWS) * ao.indirectAmbientOcclusion;
    half3 direct = sun.color * (facing * shadow * ao.directAmbientOcclusion);
    half shade = saturate(1 - facing * shadow);
    half dots = InkDots(p, frequency, shade) * strength * (.22 + .78 * shade) * near;
    return color * (ambient + direct) * (1.0 - hatch) * lerp(1, half3(.25, .20, .38), dots);
}

#endif
