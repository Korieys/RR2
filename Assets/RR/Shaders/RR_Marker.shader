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
        _Extent ("Quad extent (multiples of the halo radius)", Float) = 1.6
        _Spikes ("Diffraction spikes", Range(0, 2)) = 0.12
        _Breathe ("Slow breathing pulse (capitals)", Range(0, 1)) = 0
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
                float4 _Color; float _MinPx, _MaxPx, _WorldRadius, _Intensity, _Pulse, _Extent, _Spikes, _Breathe;
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
                // The quad reaches _Extent halo radii so the outer glow and spikes are not cut off; uv stays in halo radii.
                float ext = max(_Extent, 1);
                o.posCS = cc + float4(v.uv * r * ext * 2 / _ScreenParams.xy * cc.w, -0.0001 * cc.w, 0);
                o.uv = v.uv * ext;
                o.px = r;
                return o;
            }
            float4 frag(V i) : SV_Target
            {
                float ext = max(_Extent, 1);
                float d = length(i.uv);
                float edge = saturate(1 - d / ext);                                  // fade to zero at the quad edge
                float core = exp(-d * d * 60);
                float halo = exp(-d * 5.5) * 0.35 * saturate(1 - d);
                float glow = exp(-d * 2.2) * 0.14 * edge * edge;                    // soft outer halo in the star colour
                // Four-point diffraction spikes along screen axes, thin and tapering, plus faint diagonals when strong.
                float2 a = abs(i.uv);
                float px = 1.0 / max(i.px, 1.0);                                     // one pixel in halo radii
                float w = max(0.035, px * 0.9);
                float main = (exp(-a.x / w) * exp(-a.y * 1.4 / ext) + exp(-a.y / w) * exp(-a.x * 1.4 / ext));
                float2 r45 = abs(float2(i.uv.x + i.uv.y, i.uv.x - i.uv.y)) * 0.7071;
                float diag = (exp(-r45.x / w) * exp(-r45.y * 3.0 / ext) + exp(-r45.y / w) * exp(-r45.x * 3.0 / ext)) * 0.35 * saturate(_Spikes - 0.5);
                float spikes = (main + diag) * _Spikes * edge * 0.55;
                float3 col = _Color.rgb * (halo + glow) + lerp(_Color.rgb, 1, 0.55) * spikes + lerp(_Color.rgb, 1, 0.75) * core * 2;
                float breathe = 1 + _Breathe * _RR_Motion * 0.12 * sin(_RR_Time * 1.4);    // about a 4.5 s cycle
                return float4(col * _Intensity * (1 + _Pulse * 0.6) * breathe, 0);
            }
            ENDHLSL
        }
    }
}