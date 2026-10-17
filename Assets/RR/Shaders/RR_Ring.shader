Shader "RR/Ring"
{
    Properties
    {
        _RingTex ("Bands (rgb colour, a density)", 2D) = "black" {}
        _Inner ("Inner radius (object)", Float) = 1.4
        _Outer ("Outer radius (object)", Float) = 2.4
        _PlanetRadius ("Planet radius (object)", Float) = 1
        _Opacity ("Opacity", Range(0, 1)) = 0.9
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RR_Common.hlsl"
            TEXTURE2D(_RingTex); SAMPLER(sampler_RingTex);
            CBUFFER_START(UnityPerMaterial)
                float _Inner, _Outer, _PlanetRadius, _Opacity;
            CBUFFER_END
            struct A { float4 posOS : POSITION; };
            struct V { float4 posCS : SV_POSITION; float3 posOS : TEXCOORD0; float3 posWS : TEXCOORD1; };
            V vert(A v) { V o; o.posOS = v.posOS.xyz; o.posWS = TransformObjectToWorld(v.posOS.xyz); o.posCS = TransformWorldToHClip(o.posWS); return o; }
            float4 frag(V i) : SV_Target
            {
                float r = length(i.posOS.xz);
                float u = (r - _Inner) / (_Outer - _Inner);
                clip(u * (1 - u));
                float4 band = SAMPLE_TEXTURE2D(_RingTex, sampler_RingTex, float2(u, 0.5));
                float3 c = TransformObjectToWorld(float3(0, 0, 0));
                float scale = length(TransformObjectToWorld(float3(1, 0, 0)) - c);
                float Rp = _PlanetRadius * scale;
                float3 L = RR_StarDir(i.posWS);
                float3 oc = i.posWS - c;
                float bq = dot(oc, L);
                float cq = dot(oc, oc) - Rp * Rp;
                float disc = bq * bq - cq;
                float sh = (disc > 0 && -bq - sqrt(disc) > 0) ? 0.06 : 1.0;
                float3 Vv = normalize(GetCameraPositionWS() - i.posWS);
                float3 n = normalize(mul((float3x3)GetObjectToWorldMatrix(), float3(0, 1, 0)));
                float lit = 0.35 + 0.65 * abs(dot(n, L));
                float back = pow(saturate(dot(-Vv, L)), 6) * (1 - band.a) * 2.0;
                float3 col = band.rgb * _RR_StarColor.rgb * (lit + back) * sh + band.rgb * _RR_Ambient.rgb;
                float edge = 1 - abs(dot(n, Vv));
                float a = saturate(band.a * _Opacity * (1 + edge * 1.5));
                return float4(col, a);
            }
            ENDHLSL
        }
    }
}