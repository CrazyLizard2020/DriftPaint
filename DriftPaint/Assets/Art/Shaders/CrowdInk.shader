Shader "DriftPaint/Público de tribuna"
{
    Properties
    {
        _Celebrate("Celebración (3 cuadros)", 2D) = "white" {}
        _Wave("Saludo (3 cuadros)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-40" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Celebrate); SAMPLER(sampler_Celebrate);
            TEXTURE2D(_Wave); SAMPLER(sampler_Wave);
            struct Link
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half variant : TEXCOORD1;
                half3 pigment : COLOR;
            };
            Link Vert(float3 position : POSITION, float2 uv : TEXCOORD0, float3 anim : TEXCOORD1, half4 color : COLOR)
            {
                Link o;
                float frame = floor(frac((_Time.y * anim.y + anim.x) / 3.0) * 3.0);
                o.position = TransformObjectToHClip(position);
                o.uv = float2((uv.x + frame) / 3.0, uv.y);
                o.variant = anim.z;
                o.pigment = color.rgb;
                return o;
            }
            half4 Frag(Link i) : SV_Target
            {
                half coverage = i.variant < .5 ? SAMPLE_TEXTURE2D(_Celebrate, sampler_Celebrate, i.uv).a
                                               : SAMPLE_TEXTURE2D(_Wave, sampler_Wave, i.uv).a;
                return half4(i.pigment, coverage);
            }
            ENDHLSL
        }
    }
}
