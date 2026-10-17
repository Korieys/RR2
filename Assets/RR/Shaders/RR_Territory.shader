Shader "RR/Territory"
{
    // Political overlay on the galactic plane. _Territory: r = owner index / 15, g = border proximity, b = claim
    // strength, a = disputed. Colours come from _Palette (16 x 1). Fades with camera distance; toggled with overlays.
    Properties
    {
        _Territory ("Territory", 2D) = "black" {}
        _Palette ("Palette", 2D) = "black" {}
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
            TEXTURE2D(_Palette); SAMPLER(sampler_Palette);
            CBUFFER_START(UnityPerMaterial)
                float _Fill, _Border, _Fade;
            CBUFFER_END
            struct A { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; float3 posWS : TEXCOORD1; };
            V vert(A v) { V o; o.posWS = TransformObjectToWorld(v.posOS.xyz); o.posCS = TransformWorldToHClip(o.posWS); o.uv = v.uv; return o; }
            float4 frag(V i) : SV_Target
            {
                float4 t = SAMPLE_TEXTURE2D(_Territory, sampler_Territory, i.uv);
                if (t.b < 0.01) return 0;
                float3 c = SAMPLE_TEXTURE2D(_Palette, sampler_Palette, float2((floor(t.r * 15 + 0.5) + 0.5) / 16, 0.5)).rgb;
                float dist = length(GetCameraPositionWS() - i.posWS);
                float far = saturate((dist - 60) / 380);
                float border = smoothstep(0.55, 0.95, t.g);
                float px = fwidth(t.g);
                border = max(border * 0.35, smoothstep(0.86 - px * 2, 0.95, t.g));
                float hatch = t.a * step(0.5, frac((i.posWS.x + i.posWS.z) * 0.35));
                float3 col = c * (t.b * _Fill * (0.4 + 0.6 * far) + border * _Border * (0.5 + 0.5 * far)) + c * hatch * 0.05;
                return float4(col * _Fade, 0);
            }
            ENDHLSL
        }
    }
}