Shader "RR/Marker"
{
    // Actionable star systems on the galaxy map: a bright core and halo kept within a pixel-size band so they read at
    // every zoom without becoming oversized discs. Uses a camera-facing quad (radially symmetric, so rotation is invisible).
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _MinPx ("Min radius (px)", Float) = 5
        _MaxPx ("Max radius (px)", Float) = 26
        _WorldRadius ("World radius", Float) = 2
        _Intensity ("Intensity", Range(0, 10)) = 2.4
        _Pulse ("Pulse (hover/selection)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+30" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RR_Common.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _MinPx, _MaxPx, _WorldRadius, _Intensity, _Pulse;
            CBUFFER_END
            struct A { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; float px : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                float3 c = TransformObjectToWorld(float3(0, 0, 0));
                float4 cc = TransformWorldToHClip(c);
                float dist = length(GetCameraPositionWS() - c);
                float px = _WorldRadius * _ScreenParams.y / (dist * 0.85);
                float r = clamp(px, _MinPx, _MaxPx) * (1 + _Pulse * 0.25);
                o.posCS = cc + float4(v.uv * r * 2 / _ScreenParams.xy * cc.w, -0.0001 * cc.w, 0);
                o.uv = v.uv;
                o.px = r;
                return o;
            }
            float4 frag(V i) : SV_Target
            {
                float d = length(i.uv);
                float core = exp(-d * d * 60);
                float halo = exp(-d * 5.5) * 0.35;
                float spikes = (exp(-abs(i.uv.x) * 40) + exp(-abs(i.uv.y) * 40)) * exp(-d * 3) * 0.12;
                float3 col = _Color.rgb * (halo + spikes) + lerp(_Color.rgb, 1, 0.75) * core * 2;
                return float4(col * _Intensity * saturate(1 - d) * (1 + _Pulse * 0.6), 0);
            }
            ENDHLSL
        }
    }
}