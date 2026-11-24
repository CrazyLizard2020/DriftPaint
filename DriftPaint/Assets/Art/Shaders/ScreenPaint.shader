Shader "DriftPaint/UI/Salpicadura de impacto"
{
    Properties
    {
        [PerRendererData] _MainTex("UI", 2D) = "white" {}
        _PaintColor("Pigmento", Color) = (1,1,1,1)
        _Hit("Cobertura, opacidad, progreso y semilla", Vector) = (1,0,0,0)
        _Flow("Longitud, velocidad, irregularidad y aspecto", Vector) = (.72,1,.72,1.778)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            #include "InkCommon.hlsl"
            float4 _Hit, _Flow, _PaintColor;

            float JoinPaint(float a, float b, float radius)
            {
                float neck = max(radius - abs(a - b),0) / radius;
                return min(a,b) - neck * neck * radius * .25;
            }

            // Distancias en unidades de altura de pantalla. Cada columna tiene su propio hilo y gota libre.
            float Runs(float2 uv, float bank, float size, float seed)
            {
                float column = uv.x * 8.5;
                float2 key = float2(floor(column), seed);
                float rand = InkHash(key);
                float speed = .42 + .86 * InkHash(key + 7.9);
                float t = saturate(_Hit.z * _Flow.y * speed);
                float dx = (frac(column) - lerp(.22,.78,rand)) / 8.5;
                float y = uv.y - bank;
                float reach = (.009 + .068 * rand * rand + (.035 + .17 * rand) * _Flow.x * t) * size;
                float radius = (.004 + .014 * rand * rand) * size;
                float tip = length(float2(dx, (y - reach) * .80)) - radius;
                float thread = max(abs(dx) - radius * (.18 + .92 * saturate(1 - y / max(.001,reach))), y - reach);
                float beadY = bank + (.034 + .095 * rand + .12 * t * speed) * size;
                float bead = length(float2(dx + .034, (uv.y - beadY) * .77)) - radius * .85;
                return min(JoinPaint(tip,thread,.012),bead);
            }

            half4 Frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float aspect = max(.2,_Flow.w);
                float side = min(uv.x,1-uv.x);
                float top = 1 - uv.y;
                if (top > .35 && uv.y > .13 && side > .16) return 0;
                float seed = 17 + _Hit.w * 67;
                float size = min(1.4,_Hit.x) * (1 - .30 * _Hit.z);
                float2 p = float2(uv.x * aspect, top);
                float wave = InkFbm(float2(p.x * 10,seed));
                float sideWave = InkFbm(float2(uv.y * 11,seed + (uv.x < .5 ? 5 : 17)));
                float bank = (.021 + .106 * lerp(.45,wave,_Flow.z)) * size;
                float sideBank = (.002 + .090 * lerp(.45,sideWave*sideWave,_Flow.z)) * size;
                float bottomBank = (.003 + .043 * InkNoise(float2(p.x * 13,seed + 1))) * size;
                float d = JoinPaint(top-bank, Runs(p,bank,size,seed),.032);
                d = JoinPaint(d, min(side*aspect-sideBank,uv.y-bottomBank),.055);
                // Lenguas en los márgenes y gotas satélite que caen desde cada una.
                float sideSeed = seed + (uv.x < .5 ? 13 : 27);
                float row = top * 5;
                float bud = InkHash(float2(floor(row),sideSeed));
                float dy = (frac(row) - .40) / 5;
                float reach = sideBank + (.004 + .058 * bud * bud) * size;
                float2 delta = float2(side*aspect - reach,dy);
                float tongue = length(float2(delta.x,delta.y + delta.x*.40) * float2(.83,1.15)) - (.005 + .024*bud)*size;
                float beadDrop = (.021 + .035*bud + .061*saturate(_Hit.z*_Flow.y)) * size;
                float satellite = length(float2(delta.x-.023*size,(dy-beadDrop)*.72)) - (.003+.009*bud)*size;
                d = min(JoinPaint(d,tongue,.047),satellite);
                float aa = max(.00045, fwidth(d));
                float mask = 1 - smoothstep(-aa,aa,d);
                // Reserva óptica independiente de la escala, también en pantallas angostas.
                mask *= max(1 - smoothstep(.28,.35,top),
                        max(1 - smoothstep(.10,.13,uv.y), 1 - smoothstep(.12,.16,side)));
                float lip = saturate(1 - abs(d + .003) / .0023) * smoothstep(.42,.70,wave + .15*sideWave);
                float grain = InkDots(p,260,.45);
                half3 pigment = _PaintColor.rgb * (.76 + .24 * wave + .12*saturate(1 + d*46) - .10 * grain);
                return half4(lerp(pigment,half3(1,.95,.87),.54 * lip),mask * _Hit.y);
            }
            ENDCG
        }
    }
}
