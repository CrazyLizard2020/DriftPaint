Shader "DriftPaint/Cielo de atardecer"
{
    SubShader {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Link { float4 clip:SV_POSITION; float3 dir:TEXCOORD0; };
            Link Vert(float4 vertex:POSITION) { Link o; o.clip=TransformObjectToHClip(vertex.xyz); o.dir=vertex.xyz; return o; }
            half4 Frag(Link i):SV_Target {
                float3 dir=normalize(i.dir);
                float sun=distance(dir,normalize(float3(.52,.095,.84)));
                half3 sky=lerp(half3(1,.52,.35),half3(.46,.28,.65),smoothstep(-.04,.34,dir.y));
                sky=lerp(sky,half3(.24,.33,.65),smoothstep(.24,.82,dir.y));
                float az=atan2(dir.z,dir.x);
                float bank=.24+.07*sin(3*az)+.045*sin(9*az)+.019*sin(17*az);
                float thickness=.029+.032*saturate(sin(4*az+.6));
                float cloud=(1-smoothstep(thickness,thickness+.008,abs(dir.y-bank)))*smoothstep(-.35,.42,sin(5*az));
                sky=lerp(sky,half3(.69,.46,.68),cloud*.74);
                sky+=half3(.37,.17,.02)*(1-smoothstep(.025,.34,sun));
                sky=lerp(sky,half3(1,.92,.48),1-smoothstep(.055,.058,sun));
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
