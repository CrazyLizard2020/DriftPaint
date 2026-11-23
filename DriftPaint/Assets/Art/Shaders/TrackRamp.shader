Shader "DriftPaint/Rampa de pista"
{
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "DisableBatching"="True" }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Link { float4 clip:SV_POSITION; float3 local:TEXCOORD0; float3 normal:TEXCOORD1; };
            Link Vert(float4 vertex:POSITION,float3 normal:NORMAL) {
                Link o; o.local=vertex.xyz; o.normal=normal; o.clip=TransformObjectToHClip(vertex.xyz); return o;
            }
            half4 Frag(Link i):SV_Target {
                float2 uv=i.local.xz+.5;
                half3 base=lerp(half3(.19,.18,.25),half3(.32,.29,.37),saturate(i.normal.y));
                float2 seam=abs(frac(uv*float2(6,8))-.5);
                base*=1-.20*smoothstep(.47,.5,max(seam.x,seam.y));
                float outside=abs(i.local.x);
                float stripes=step(.40,frac(uv.y*5-outside*5));
                float edge=step(.34,outside);
                half3 hazard=lerp(half3(.32,.24,.18),half3(.98,.59,.055),stripes);
                return half4(lerp(base,hazard,edge),1);
            }
            ENDHLSL
        }
    }
}
