Shader "DriftPaint/Depósito de pintura"
{
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Link { float4 clip:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Link Vert(float4 vertex:POSITION, float2 uv:TEXCOORD0, half4 color:COLOR) {
                Link o; o.clip=TransformObjectToHClip(vertex.xyz); o.uv=uv; o.color=color; return o;
            }
            half4 Frag(Link i):SV_Target {
                float2 p=i.uv*2-1;
                float r=length(p);
                float lip=1-smoothstep(.02,.12,abs(r-.69));
                float shine=lip*saturate(.48-p.x*.62+p.y*.42);
                return half4(i.color.rgb*(1-.12*lip)+shine*.32,i.color.a*(1-smoothstep(.83,1,r)));
            }
            ENDHLSL
        }
    }
}
