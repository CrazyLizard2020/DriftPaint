Shader "DriftPaint/Chorro de escape"
{
    Properties {
        _BaseColor("Pigmento",Color)=(0,1,1,1)
        _ExhaustShape("Alcance y punto de llegada",Vector)=(1,0,-.3,1.38)
        _JetFlow("Caudal, ciclo, semilla y apoyo",Vector)=(1,0,0,1)
        _TurboGlow("Brillo de aceleración y turbo",Range(0,4))=0
        _StreamWidth("Ancho visual",Range(.05,3))=1
    }
    SubShader {
        // Va en la cola de transparentes, aunque es opaco, para que el contorno de pantalla no lo cruce.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Transparent" "DisableBatching"="True" }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // La malla es un tubo de radio 1 y largo 1: cada vértice trae su vuelta (uv.x) y su avance (uv.y).
            // Toda la forma se arma acá para poder seguir el piso y el caudal sin tocar la malla.
            #define SIDES 12
            // Parte del recorrido en la que el chorro cae; el resto corre acostado sobre el piso.
            #define LANDING .8
            #define TAU 6.2831853

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _ExhaustShape; // x: alcance de 0 a 1; yzw: punto de llegada en el espacio del escape
            float4 _JetFlow;      // x: caudal de 0 a 1; y: ciclo de 0 a 1; z: semilla de cada escape; w: apoyo en el piso
            half _TurboGlow, _StreamWidth;
            CBUFFER_END

            struct Link {
                float4 clip:SV_POSITION;
                half3 normal:TEXCOORD0;
                half3 view:TEXCOORD1;
                float4 jet:TEXCOORD2; // x: vuelta; y: avance; z: cresta; w: apoyo sobre el piso
            };

            Link Vert(float3 ring:POSITION, float2 uv:TEXCOORD0)
            {
                float d = uv.y;
                float reach = _ExhaustShape.x;
                float3 tip = _ExhaustShape.yzw;
                float flow = _JetFlow.x;
                float cycle = _JetFlow.y * TAU;
                float seed = _JetFlow.z;

                // Sale horizontal, cae y termina acostado: así llega al piso sin un corte en la punta.
                float s = saturate(d / LANDING);
                float fall = s * s * (3 - 2 * s);
                float slope = 6 * s * (1 - s) / LANDING;
                // Solo se acuesta cuando de verdad llega al piso; con poco caudal la punta cuelga redonda.
                float foot = smoothstep(LANDING - .22, 1, d) * _JetFlow.w;
                float3 center = float3(tip.x * fall, tip.y * fall, tip.z * d);

                // Ondas que recorren el chorro de punta a punta; con más caudal se mueven más rápido y más amplias.
                float sway = flow * d * (1 - foot);
                center.x += sway * .05 * sin(d * 9 - cycle + seed * TAU);
                center.y += sway * .03 * sin(d * 7 - cycle + seed * 11);
                // Temblor fino de la presión.
                center.xy += flow * flow * .008 * float2(sin(d * 23 + cycle * 7), sin(d * 19 - cycle * 6));

                // Dientes de serrucho en el borde: suben despacio, cortan de golpe y viajan hacia atrás.
                // Cada costado arranca desfasado para que el contorno quede irregular.
                float column = fmod(floor(uv.x * SIDES + .5), SIDES);
                float offset = frac(sin(column * 12.9898 + seed * 78.233) * 43758.5453);
                float tooth = frac(d * 7 - _JetFlow.y * 2 + offset);
                float ripple = .5 + .5 * sin(uv.x * TAU * 3 + d * 13 - cycle * 2 + seed * 5);
                float rough = flow * sqrt(flow) * smoothstep(.05, .45, d) * (1 - .45 * foot);
                float crest = rough * (.75 * tooth * tooth * tooth + .4 * ripple * ripple);

                float radius = (.075 + .06 * d) * lerp(.55, 1, reach) * (1 + crest) * lerp(1, _StreamWidth, d);
                // Al apoyarse se ensancha y se aplana hasta quedar al ras del piso.
                float2 ellipse = radius * float2(1 + 1.4 * foot, lerp(1, .12, foot));
                float3 pos = center + float3(ring.xy * ellipse, 0);

                // Normal de la elipse, perpendicular al recorrido del chorro.
                float3 along = float3(tip.x * slope, tip.y * slope, tip.z);
                float3 normal = float3(ring.xy / ellipse, 0);
                normal -= along * dot(normal, along) / max(dot(along, along), 1e-4);

                Link o;
                float3 world = TransformObjectToWorld(pos);
                o.clip = TransformWorldToHClip(world);
                o.normal = TransformObjectToWorldNormal(normal);
                o.view = GetWorldSpaceNormalizeViewDir(world);
                o.jet = float4(uv.x, d, crest, foot);
                return o;
            }

            half4 Frag(Link i):SV_Target
            {
                half3 n = normalize(i.normal), view = normalize(i.view);
                half3 light = normalize(half3(-.45, .8, .39));
                half spec = pow(saturate(dot(n, normalize(light + view))), 18);
                half rim = pow(1 - saturate(dot(n, view)), 3);
                float flow = _JetFlow.x;

                // Vetas claras estiradas a lo largo del chorro que corren con el caudal.
                float streak = sin(i.jet.x * TAU * 4 + 1.4 * sin(i.jet.y * 8 - _JetFlow.y * TAU * 2));
                streak = smoothstep(.72, 1, streak) * flow * (1 - i.jet.w);

                half3 color = _BaseColor.rgb * (.79 + .24 * saturate(dot(n, light)));
                color += _BaseColor.rgb * .2 * saturate(i.jet.z) + .16 * streak + .14 * rim;
                // Sobre el piso toma el tono de la pintura que se asienta.
                color = lerp(color, _BaseColor.rgb * .88, .5 * i.jet.w);
                // El turbo empuja el pigmento por encima del umbral del Bloom sin cambiar su matiz.
                color *= 1 + _TurboGlow * (.9 + 1.4 * streak + .8 * rim) * (1 - .6 * i.jet.w);
                return half4(color + .5 * spec, 1);
            }
            ENDHLSL
        }
    }
}
