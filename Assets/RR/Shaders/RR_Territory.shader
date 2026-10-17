Shader "RR/Territory"
{
    // Political overlay on the galactic plane, drawn from a signed distance field (fidelity plan, phase 4).
    // _Field: distance in world units to the territory edge, positive inside. _Territory: owner colour (rgb) and the
    // disputed-zone mask (a). Each sector gets a crisp glowing border line of constant pixel width just inside its edge,
    // frilled by the same detail noise as the nebula, plus a soft inner gradient. Toggled with the overlays.
    Properties
    {
        _Territory ("Territory colour (rgb) and disputed mask (a)", 2D) = "black" {}
        _Field ("Signed edge distance (world units)", 2D) = "black" {}
        _Detail ("Detail noise", 3D) = "grey" {}
        _DetailScale ("Detail tiles per world unit", Float) = 0.045
        _Inset ("Border inset (world)", Float) = 1.3
        _Fill ("Fill", Range(0, 1)) = 0.08
        _Border ("Border", Range(0, 4)) = 1.2
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+5" }
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
            TEXTURE2D(_Territory); SAMPLER(sampler_Territory);
            TEXTURE2D(_Field); SAMPLER(sampler_Field);
            TEXTURE3D(_Detail); SAMPLER(sampler_Detail);
            CBUFFER_START(UnityPerMaterial)
                float _DetailScale, _Inset, _Fill, _Border, _Fade;
            CBUFFER_END
            struct A { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; float3 posWS : TEXCOORD1; };
            V vert(A v) { V o; o.posWS = TransformObjectToWorld(v.posOS.xyz); o.posCS = TransformWorldToHClip(o.posWS); o.uv = v.uv; return o; }
            float4 frag(V i) : SV_Target
            {
                float4 t = SAMPLE_TEXTURE2D(_Territory, sampler_Territory, i.uv);
                float d = SAMPLE_TEXTURE2D(_Field, sampler_Field, i.uv).r;
                if (d < -12) return 0;
                float dist = length(GetCameraPositionWS() - i.posWS);
                float pixAngle = 2.0 / (abs(UNITY_MATRIX_P[1][1]) * _ScreenParams.y);
                float lod = log2(max(1.0, dist * pixAngle * _DetailScale * 64.0));
                // Frilled, map-like edge: two octaves of the shared detail noise displace the line by about a world unit.
                float2 q = float2(0.8 * i.posWS.x - 0.6 * i.posWS.z, 0.6 * i.posWS.x + 0.8 * i.posWS.z);
                float n = SAMPLE_TEXTURE3D_LOD(_Detail, sampler_Detail, float3(q.x, 3.1, q.y) * _DetailScale, lod).r;
                float n2 = SAMPLE_TEXTURE3D_LOD(_Detail, sampler_Detail, float3(q.y, 8.7, q.x) * _DetailScale * 3.1, lod + 1.6).r;
                d += (n - 0.5) * 3.2 + (n2 - 0.5) * 1.4;
                float px = max(fwidth(d), 1e-4);                 // world units per pixel across the line
                float far = saturate((dist - 60) / 380);
                float overview = saturate((dist - 380) / 320);
                float x = d - _Inset;
                float core = saturate(1.0 - abs(x) / (px * 0.85));                  // about 1.7 px, anti-aliased
                float halo = exp(-abs(x) / max(px * 3.5, 0.6)) * 0.55;              // tight glow in pixels
                float bloom = exp(-max(x, 0) / (2.5 + 5.0 * far)) * step(0, x) * 0.22;  // soft light just inside
                float fill = smoothstep(0, 40, d) * _Fill * (0.4 + 0.6 * far) * (1 - 0.8 * overview);
                float3 c = t.rgb;
                float3 col = c * ((core * 1.25 + halo + bloom) * _Border * (0.6 + 0.4 * far)) + lerp(c, 1, 0.55) * core * 0.35;
                col += c * fill * step(0, d);
                // Disputed space: anti-aliased diagonal hatching.
                float s = (i.posWS.x + i.posWS.z) * 0.32;
                float fs = fwidth(s);
                float stripe = smoothstep(0.5 - fs, 0.5 + fs, abs(frac(s) - 0.5) * 2.0 + 0.25);
                col += lerp(float3(1, 0.45, 0.5), 1, 0.3) * stripe * t.a * 0.22;
                return float4(col * _Fade, 0);
            }
            ENDHLSL
        }
    }
}
