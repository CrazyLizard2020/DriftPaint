Shader "DriftPaint/Estela ondulada"
{
    Properties { _BaseColor("Pigmento",Color)=(1,1,1,1) }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _FlightVelocity;
            float4 _Wave;
            CBUFFER_END
            struct Link { float4 clip:SV_POSITION; half3 n:TEXCOORD0; half3 eye:TEXCOORD1; };
            Link Vert(float4 vertex:POSITION) {
                // La malla guarda las secciones de un tubo; el eje se reconstruye hacia atrás sobre la parábola.
                float u=vertex.z, age=u*_Wave.x;
                float3 head=TransformObjectToWorld(float3(0,0,0));
                float3 velocity=_FlightVelocity.xyz;
                float3 forward=normalize(velocity+float3(0,_FlightVelocity.w*age,0)+float3(0,0,.0001));
                float3 right=normalize(cross(abs(forward.y)>.98 ? float3(0,0,1) : float3(0,1,0), forward));
                float3 up=cross(forward,right);
                float phase=(_Wave.y-age)*55;
                float swing=_Wave.z*smoothstep(0,.22,u);
                float3 center=head-velocity*age-float3(0,.5*_FlightVelocity.w*age*age,0);
                center+=swing*(right*sin(phase)+up*.70*cos(phase));
                float3 n=right*vertex.x+up*vertex.y;
                float radius=.135*(1-smoothstep(.10,1,u))*(.92+.08*cos(phase));
                float3 world=center+n*radius;
                Link o; o.clip=TransformWorldToHClip(world); o.n=n; o.eye=GetWorldSpaceNormalizeViewDir(world); return o;
            }
            half4 Frag(Link i):SV_Target {
                half3 n=normalize(i.n), l=normalize(half3(-.4,.85,.33));
                half spec=pow(saturate(dot(n,normalize(l+normalize(i.eye)))),14);
                return half4(_BaseColor.rgb*(.70+.32*saturate(dot(n,l)))+.47*spec,1);
            }
            ENDHLSL
        }
    }
}
