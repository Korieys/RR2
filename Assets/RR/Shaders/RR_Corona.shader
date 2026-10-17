Shader "RR/Corona"
{
    // Radially symmetric glow on a camera-facing quad. The streamer noise is sampled in world-space directions,
    // so the pattern does not swim when the quad turns with the camera.
    Properties
    {
        _Color ("Colour", Color) = (1, 0.9, 0.75, 1)
        _Intensity ("Intensity", Range(0, 10)) = 1.4
        _StarRadius ("Star radius (fraction of quad half-size)", Range(0.05, 0.9)) = 0.22
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RR_Common.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Intensity, _StarRadius;
            CBUFFER_END
            struct A { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; float3 dirWS : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                float3 c = TransformObjectToWorld(float3(0, 0, 0));
                float size = length(TransformObjectToWorld(float3(1, 0, 0)) - c);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 p = c + (right * v.posOS.x + up * v.posOS.y) * size;
                o.posCS = TransformWorldToHClip(p);
                o.uv = v.posOS.xy;
                o.dirWS = normalize(p - c);
                return o;
            }
            float4 frag(V i) : SV_Target
            {
                float r = length(i.uv);
                float rs = _StarRadius;
                float x = saturate((r - rs) / (1 - rs));
                float glow = exp(-x * 9) * 0.9 + exp(-x * 3) * 0.12;
                float streak = RR_Fbm(i.dirWS * 3.5 + _RR_Time * 0.01, 3) * 0.5 + 0.5;
                glow *= lerp(0.75, 1.25, streak);
                glow *= saturate(1 - x) * step(rs * 0.98, r);
                return float4(_Color.rgb * glow * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}