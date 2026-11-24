Shader "DriftPaint/Cielo de atardecer"
{
    // Cielo de atardecer: naranja cálido en el horizonte, lavanda arriba y nubes violetas pintadas a pincel,
    // con el borde encendido del lado del sol. El disco del sol supera el umbral del Bloom.
    Properties
    {
        _SunDirection("Dirección del sol", Vector) = (.599, .122, .8, 0)
        _Horizon("Horizonte", Color) = (1, .61, .18, 1)
        _Sky("Cielo", Color) = (.99, .38, .62, 1)
        _Zenith("Cenit", Color) = (.37, .22, .67, 1)
        _Cloud("Nubes", Color) = (.43, .23, .64, 1)
        _CloudLit("Borde de nube", Color) = (1, .53, .31, 1)
        _SunGlow("Brillo del sol", Float) = 2.7
        _PrintScale("Densidad de puntos", Range(100,600)) = 345
        _PrintStrength("Trama de cielo", Range(0,1)) = .72
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
            half _SunGlow, _PrintScale, _PrintStrength;
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
                sky = lerp(sky, _Zenith.rgb, smoothstep(.12, .32, h));
                sky += half3(1.22, .19, .012) * (1 - smoothstep(.02, .39, sun));
                // El sol se dibuja antes que las nubes para que lo puedan tapar.
                sky = lerp(sky, half3(1, .79, .19) * _SunGlow, 1 - smoothstep(.043, .0445, sun));

                // Nubes en franjas horizontales: el ruido se toma sobre un cilindro alrededor de la cámara,
                // estirado en el giro y fino en la altura, como pinceladas de lado a lado. Se mezclan dos
                // muestras según hacia dónde mira cada dirección, así no aparece una costura.
                float2 around = normalize(dir.xz + 1e-4);
                float2 weight = abs(around) / (abs(around.x) + abs(around.y));
                float lift = h * 15 + .8 * sin(around.x * 3.1 + around.y * 2.3);
                float body = InkFbm(float2(around.x * 3.2, lift) + 3.7) * weight.x
                           + InkFbm(float2(around.y * 3.2 + 9.4, lift)) * weight.y;
                // Trazos secos que deshilachan el borde de cada nube.
                float2 brushUV = float2(around.x * 29 + around.y * 17, h * 74);
                float brush = InkFbm(brushUV + 2.1 * InkNoise(brushUV * .33)) - .5;
                brush += .24 * (InkNoise(brushUV * 3.35) - .5);
                float band = smoothstep(.03, .12, h) * (1 - smoothstep(.42, .7, h));
                float cloud = smoothstep(.485, .518, body + brush * .51) * band;

                float towardSun = saturate(dot(normalize(dir.xz + 1e-4), normalize(sunDir.xz)));
                float rim = smoothstep(.55, .82, body + brush * .15) ;
                half3 cloudColor = lerp(_Cloud.rgb, _CloudLit.rgb, towardSun * (1 - rim) * (1 - smoothstep(.49,.57,body + brush * .51)));
                sky = lerp(sky, cloudColor, cloud * .96);
                float shade = saturate(cloud * .85 + h * .75);
                float2 dome = dir.xz / max(.3, 1 + h);
                float dots = InkDots(dome, _PrintScale, shade);
                float fleck = InkNoise(dome * 380);
                sky = lerp(sky, lerp(sky * .47, _CloudLit.rgb * 1.25, saturate(cloud * (.21 + .45 * fleck))),
                           dots * _PrintStrength * smoothstep(.05,.15,sun));
                sky *= 1.15 + .12 * (fleck - .5);
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
