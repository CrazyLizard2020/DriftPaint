Shader "DriftPaint/Chorro viscoso"
{
    Properties {
        _BaseColor("Pigmento",Color)=(0,1,1,1)
        _ExhaustShape("Ancho, largo y contacto",Vector)=(1,1,0,0)
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "DisableBatching"="True" }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _ExhaustShape;
            CBUFFER_END
            struct Link { float4 clip:SV_POSITION; half3 normal:TEXCOORD0; half3 view:TEXCOORD1; };
            Link Vert(float4 v:POSITION,float3 normal:NORMAL) {
                float d=v.z;
                float phase=_Time.y*17+d*11+unity_ObjectToWorld._m03;
                float along=saturate(d/1.38);
                float bend=along*along;
                float ripple=d*(1-along)*_ExhaustShape.x;
                v.x=v.x*_ExhaustShape.x+bend*_ExhaustShape.w+.035*ripple*sin(phase);
                v.y=v.y*_ExhaustShape.x+bend*_ExhaustShape.z+.024*ripple*sin(phase*1.7);
                v.z*=_ExhaustShape.y;
                Link o; float3 pos=TransformObjectToWorld(v.xyz); o.clip=TransformWorldToHClip(pos);
                o.normal=TransformObjectToWorldNormal(normal); o.view=GetWorldSpaceNormalizeViewDir(pos);return o;
            }
            half4 Frag(Link i):SV_Target {
                half3 n=normalize(i.normal), light=normalize(half3(-.45,.8,.39));
                half spec=pow(saturate(dot(n,normalize(light+normalize(i.view)))),18);
                return half4(_BaseColor.rgb*(.79+.24*saturate(dot(n,light)))+.50*spec,1);
            }
            ENDHLSL
        }
    }
}
