#ifndef DRIFTPAINT_INK_COMMON_INCLUDED
#define DRIFTPAINT_INK_COMMON_INCLUDED

// Ruido y proyección compartidos por los materiales entintados y el cielo. Todo es procedural y de baja
// frecuencia: no hay texturas que muestrear ni mipmaps que puedan parpadear a distancia.

float InkHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// Ruido de valor con interpolación suave entre los puntos de una grilla de 1 unidad.
float InkNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = InkHash(i);
    float b = InkHash(i + float2(1, 0));
    float c = InkHash(i + float2(0, 1));
    float d = InkHash(i + 1);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Dos octavas: alcanza para manchas de pigmento sin que se vea la grilla del ruido.
float InkFbm(float2 p)
{
    return 0.62 * InkNoise(p) + 0.38 * InkNoise(p * 2.13 + 17.1);
}

// Coordenadas sobre la cara dominante de la superficie, ancladas al mundo. Es una proyección triplanar de
// una sola muestra: alcanza para mallas low poly, donde cada cara mira claramente hacia un eje.
float2 InkPlanar(float3 positionWS, float3 normalWS)
{
    float3 a = abs(normalWS);
    if (a.y >= a.x && a.y >= a.z) return positionWS.xz;
    return a.x >= a.z ? positionWS.zy : positionWS.xy;
}

// Trama rotada como una plancha de impresión. Las derivadas apagan el patrón antes del subpíxel.
float InkDots(float2 plane, float frequency, float shade)
{
    float2 uv = float2(dot(plane, float2(.8944, .4472)), dot(plane, float2(-.4472, .8944))) * frequency;
    float2 footprint = fwidth(uv);
    float width = max(footprint.x, footprint.y);
    float irregular = InkHash(floor(uv)) - .5;
    float radius = lerp(.10, .43, saturate(shade)) * (1 + .45 * irregular);
    float d = length(frac(uv) - .5 - float2(.11,-.14) * irregular);
    float aa = max(.015, .5 * width);
    return (1 - smoothstep(radius - aa, radius + aa, d)) * (1 - smoothstep(.29, .58, width));
}

#endif
