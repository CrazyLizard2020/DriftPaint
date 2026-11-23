Shader "DriftPaint/Tribunas"
{
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Link { float4 clip:SV_POSITION; half3 color:COLOR; };
            Link Vert(float4 vertex:POSITION, float3 n:NORMAL, half4 color:COLOR) {
                Link o; o.clip=TransformObjectToHClip(vertex.xyz);
                o.color=color.rgb*(.72+.28*saturate(dot(n,normalize(float3(-.4,.8,.3))))); return o;
            }
            half4 Frag(Link i):SV_Target { return half4(i.color,1); }
            ENDHLSL
        }
    }
}
