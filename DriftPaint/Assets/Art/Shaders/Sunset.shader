Shader "DriftPaint/Cielo de atardecer"
{
    // Cielo de atardecer: naranja cálido en el horizonte, lavanda arriba y nubes violetas pintadas a pincel,
    // con el borde encendido del lado del sol. El disco del sol supera el umbral del Bloom.
    Properties
    {
        _SunDirection("Dirección del sol", Vector) = (.52, .095, .84, 0)
        _Horizon("Horizonte", Color) = (1, .62, .40, 1)
        _Sky("Cielo", Color) = (.70, .56, .85, 1)
        _Zenith("Cenit", Color) = (.40, .32, .64, 1)
        _Cloud("Nubes", Color) = (.37, .28, .54, 1)
        _CloudLit("Nubes del lado del sol", Color) = (1, .66, .58, 1)
        _SunGlow("Brillo del sol", Float) = 1.7
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "InkCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _SunDirection;
            half4 _Horizon, _Sky, _Zenith, _Cloud, _CloudLit;
            half _SunGlow;
            CBUFFER_END

            struct Link { float4 clip : SV_POSITION; float3 dir : TEXCOORD0; };

            Link Vert(float4 vertex : POSITION)
            {
                Link o;
                o.clip = TransformObjectToHClip(vertex.xyz);
                o.dir = vertex.xyz;
                return o;
            }

            half4 Frag(Link i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 sunDir = normalize(_SunDirection.xyz);
                float sun = distance(dir, sunDir);
                float h = dir.y;

                half3 sky = lerp(_Horizon.rgb, _Sky.rgb, smoothstep(-.02, .32, h));
                sky = lerp(sky, _Zenith.rgb, smoothstep(.32, .9, h));
                sky += half3(.40, .20, .04) * (1 - smoothstep(.02, .5, sun));
                // El sol se dibuja antes que las nubes para que lo puedan tapar.
                sky = lerp(sky, half3(1, .93, .62) * _SunGlow, 1 - smoothstep(.04, .046, sun));

                // Nubes en franjas horizontales: el ruido se toma sobre un cilindro alrededor de la cámara,
                // estirado en el giro y fino en la altura, como pinceladas de lado a lado. Se mezclan dos
                // muestras según hacia dónde mira cada dirección, así no aparece una costura.
                float2 around = normalize(dir.xz + 1e-4);
                float2 weight = abs(around) / (abs(around.x) + abs(around.y));
                float lift = h * 15 + .8 * sin(around.x * 3.1 + around.y * 2.3);
                float body = InkFbm(float2(around.x * 3.2, lift) + 3.7) * weight.x
                           + InkFbm(float2(around.y * 3.2 + 9.4, lift)) * weight.y;
                // Trazos secos que deshilachan el borde de cada nube.
                float brush = InkNoise(float2(around.x * 14 + around.y * 11, h * 70)) - .5;
                float band = smoothstep(.03, .12, h) * (1 - smoothstep(.42, .7, h));
                float cloud = smoothstep(.53, .6, body + brush * .28) * band;

                float towardSun = saturate(dot(normalize(dir.xz + 1e-4), normalize(sunDir.xz)));
                float rim = smoothstep(.55, .82, body + brush * .15) ;
                half3 cloudColor = lerp(_Cloud.rgb, _CloudLit.rgb, towardSun * towardSun * (1 - rim) * .85);
                sky = lerp(sky, cloudColor, cloud * .9);
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
