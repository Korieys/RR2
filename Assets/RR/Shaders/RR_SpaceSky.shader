Shader "RR/SpaceSky"
{
    // Effectively infinite background: faint distant stars and a dim galactic band. Deliberately quiet; the sense of
    // depth comes from finite objects in front of it (galaxy volume, systems, planets, ships).
    Properties
    {
        _GalNormal ("Galactic plane normal", Vector) = (0, 1, 0, 0)
        _BandTintA ("Band tint A", Color) = (0.35, 0.45, 0.8, 1)
        _BandTintB ("Band tint B", Color) = (0.55, 0.3, 0.75, 1)
        _BandStrength ("Band strength", Range(0, 1)) = 0.12
        _StarStrength ("Star strength", Range(0, 4)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RR_Common.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _GalNormal, _BandTintA, _BandTintB;
                float _BandStrength, _StarStrength;
            CBUFFER_END
            struct A { float4 posOS : POSITION; };
            struct V { float4 posCS : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert(A v) { V o; o.posCS = TransformObjectToHClip(v.posOS.xyz); o.dir = v.posOS.xyz; return o; }

            float3 StarLayer(float3 d, float k, float density, float size)
            {
                float3 q = d * k;
                int3 c = (int3)floor(q);
                uint h = RR_Hash3(c);
                if (RR_Rand(h) > density) return 0;
                float3 o = float3(RR_Rand(RR_Hash(h + 1U)), RR_Rand(RR_Hash(h + 2U)), RR_Rand(RR_Hash(h + 3U))) * 0.5 + 0.25;
                float3 sp = normalize(float3(c) + o) * k;
                float dist = length(q - sp);
                float px = fwidth(dist) + 1e-4;
                float s = saturate(1 - dist / max(size, px * 1.2));
                float b = pow(RR_Rand(RR_Hash(h + 4U)), 6) * 2.5 + 0.15;
                float t = RR_Rand(RR_Hash(h + 5U));
                float3 tint = t < 0.2 ? float3(0.7, 0.8, 1.2) : t > 0.85 ? float3(1.2, 0.85, 0.6) : float3(1, 0.97, 0.92);
                return tint * s * s * b;
            }

            float4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float g = dot(d, normalize(_GalNormal.xyz));
                float band = exp(-g * g / 0.09) * 0.75 + exp(-g * g / 0.012) * 0.6;
                float clouds = RR_Fbm(d * 3.0, 5) * 0.5 + 0.5;
                float dust = smoothstep(0.45, 0.7, RR_Fbm(d * 6.0 + 11, 4) * 0.5 + 0.5);
                float3 neb = lerp(_BandTintA.rgb, _BandTintB.rgb, saturate(RR_Fbm(d * 1.5 + 3, 3) + 0.5));
                float3 col = neb * band * clouds * (1 - dust * 0.8) * _BandStrength;
                float starBoost = 1 + band * 2.5;
                col += StarLayer(d, 160, 0.55, 0.06) * 0.35 * _StarStrength;
                col += StarLayer(d, 380, 0.5 * starBoost / 3.5, 0.05) * 0.22 * _StarStrength;
                col += StarLayer(d.zxy, 800, 0.35 * starBoost / 3.5, 0.045) * 0.14 * _StarStrength;
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}