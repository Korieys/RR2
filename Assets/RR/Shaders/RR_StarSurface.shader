Shader "RR/StarSurface"
{
    Properties
    {
        _Color ("Photosphere colour", Color) = (1, 0.95, 0.85, 1)
        _Intensity ("Intensity", Range(0, 20)) = 5
        _Granulation ("Granulation scale", Float) = 28
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "RR_Common.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _Color; float _Intensity, _Granulation;
        CBUFFER_END
        struct A { float4 posOS : POSITION; };
        struct V { float4 posCS : SV_POSITION; float3 posOS : TEXCOORD0; float3 posWS : TEXCOORD1; };
        V vert(A v) { V o; o.posOS = v.posOS.xyz; o.posWS = TransformObjectToWorld(v.posOS.xyz); o.posCS = TransformWorldToHClip(o.posWS); return o; }
        ENDHLSL
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.posOS);
                float t = _RR_Time * 0.03;
                float2 cell = RR_Cell(d * _Granulation + float3(t, -t * 0.7, t * 0.4));
                float gran = saturate((cell.y - cell.x) * 1.4) * 0.6 + (RR_Fbm(d * 7 + t, 3) * 0.5 + 0.5) * 0.4;
                float spots = smoothstep(0.62, 0.8, RR_Fbm(d * 2.2 + t * 0.2, 3) * 0.5 + 0.5);
                float3 n = normalize(mul((float3x3)GetObjectToWorldMatrix(), d));
                float mu = saturate(dot(n, normalize(GetCameraPositionWS() - i.posWS)));
                float limb = 0.22 + 0.78 * pow(mu, 0.75);
                float3 hot = _Color.rgb * (0.82 + 0.28 * gran) * (1 - spots * 0.35);
                float3 col = hot * limb * _Intensity;
                col *= lerp(float3(1, 0.8, 0.65), float3(1, 1, 1), mu);
                return float4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragD
            float4 fragD(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}