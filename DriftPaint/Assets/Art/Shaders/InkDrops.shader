Shader "DriftPaint/Tinta en movimiento"
{
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
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
                float r=dot(p,p);
                clip(.99-r);
                float gleam=1-smoothstep(.04,.37,length(p-float2(-.29,.36)));
                return half4(i.color.rgb*(1-.18*r)+gleam*.38,i.color.a*saturate((1-r)*17));
            }
            ENDHLSL
        }
    }
}
