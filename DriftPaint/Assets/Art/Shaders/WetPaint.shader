Shader "DriftPaint/Pintura húmeda"
{
    Properties { _BaseMap("Grilla de pintura", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Input { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct Link { float4 clip:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
            Link Vert(Input v) {
                Link o; o.world = TransformObjectToWorld(v.vertex.xyz);
                o.world += TransformObjectToWorldNormal(v.normal)*.007;
                o.clip = TransformWorldToHClip(o.world); o.uv=o.world.xz/128+.5; return o;
            }
            half4 Frag(Link i):SV_Target {
                float2 p=i.uv*128;
                float waves=sin(p.x*4.8+p.y*2.1)+.46*sin(p.y*10.4-p.x*5.9)+.16*sin(p.x*32+p.y*14);
                float2 q=p+float2(waves,.65*sin(p.y*6.2+p.x*3.8))*.16;
                float2 f=frac(q-.5), base=floor(q-.5)+.5;
                f=smoothstep(.16,.84,f);
                half4 a=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,base/128);
                half4 b=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,(base+float2(1,0))/128);
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,(base+float2(0,1))/128);
                half4 d=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,(base+1)/128);
                half4 mixInk=lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y);
                clip(mixInk.a-.47);
                // La cobertura redondea el menisco; cada celda conserva el pigmento de su dueño.
                half4 ink=f.y<.5 ? (f.x<.5 ? a:b) : (f.x<.5 ? c:d);
                half3 color=ink.a>.5 ? ink.rgb : mixInk.rgb/max(.01,mixInk.a);
                float rim=1-smoothstep(.49,.87,mixInk.a);
                float3 view=normalize(GetCameraPositionWS()-i.world);
                float streak=sin(p.x*1.9+sin(p.y*.85)*2.3+p.y*1.4);
                float spec=smoothstep(.94,1,streak)*(.035+.08*pow(1-saturate(view.y),3));
                return half4(color*(.95+.035*waves)+rim*.14+spec*.70,1);
            }
            ENDHLSL
        }
    }
}
