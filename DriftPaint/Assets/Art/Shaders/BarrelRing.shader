Shader "DriftPaint/Aro de barril"
{
    Properties {
        _BaseColor("Color del equipo (alfa: opacidad)",Color)=(0,1,1,1)
        _Pulse("Período, ancho de la onda, radio del halo e intensidad",Vector)=(1.5,.07,.3,2.4)
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass {
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _Pulse; // x: segundos por pulso; y: ancho de la onda; z: radio del halo fijo (0 a 1); w: intensidad
            CBUFFER_END

            struct Link {
                float4 clip:SV_POSITION;
                float2 disc:TEXCOORD0; // posición en el disco, de -1 a 1
                float seed:TEXCOORD1;
            };

            // La malla es un cuadrado acostado: todo el dibujo sale de la distancia al centro.
            Link Vert(float3 position:POSITION, float2 uv:TEXCOORD0)
            {
                Link o;
                o.clip = TransformObjectToHClip(position);
                o.disc = uv * 2 - 1;
                // Cada barril pulsa desfasado según dónde quedó, así dos barriles juntos no laten a la vez.
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
                return o;
            }

            half4 Frag(Link i):SV_Target
            {
                float r = length(i.disc);
                clip(1 - r);

                float period = max(_Pulse.x, .05);
                float halo = _Pulse.z;
                float t = frac(_Time.y / period + i.seed);

                // La onda nace en el halo, se expande frenando y se apaga al llegar al borde.
                float grow = 1 - (1 - t) * (1 - t);
                float front = lerp(halo, .96, grow);
                float width = _Pulse.y * (.7 + .9 * t);
                float life = pow(1 - t, 1.5);
                float wave = exp(-pow((r - front) / width, 2)) * life;

                // Estela difuminada detrás de la onda, más fuerte cerca del frente.
                float wake = step(r, front) * smoothstep(halo * .7, front, r) * .3 * life;

                // Halo fijo bajo el barril que destella cada vez que sale una onda.
                float flash = .55 + .45 * pow(1 - t, 6);
                float ring = exp(-pow((r - halo) / .045, 2)) * flash;
                float core = exp(-pow(r / max(halo, .01), 2)) * .18;

                float glow = (wave + wake + ring + core) * saturate((1 - r) / .05);
                half3 color = _BaseColor.rgb * glow + (wave * wave + ring * ring) * .35;
                return half4(color * _Pulse.w * _BaseColor.a, 1);
            }
            ENDHLSL
        }
    }
}
