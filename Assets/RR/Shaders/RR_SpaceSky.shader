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
                // Pixel footprint from the continuous lattice coordinate. (fwidth(dist) jumps at every cell boundary,
                // which lit whole pixel rows along cell faces and showed as short dashes.)
                float px = length(fwidth(q)) * 0.7 + 1e-4;
                float s = saturate(1 - dist / max(size, px * 1.2));
                float b = pow(RR_Rand(RR_Hash(h + 4U)), 6) * 2.5 + 0.15;
                float t = RR_Rand(RR_Hash(h + 5U));
                float3 tint = t < 0.2 ? float3(0.7, 0.8, 1.2) : t > 0.85 ? float3(1.2, 0.85, 0.6) : float3(1, 0.97, 0.92);
                return tint * s * s * b;
            }

            // Distant galaxies: a few dozen faint, small spirals and ellipticals scattered over the sky. Directions and shapes
            // are generated once on the CPU (Look.SeedDistantGalaxies), so the per-pixel loop is mostly one dot product each.
            float4 _RR_Galaxies[40];       // xyz direction, w angular radius (radians)
            float4 _RR_GalaxyShape[40];    // x rotation, y inclination, z kind (< 0.7 spiral), w brightness
            float4 _RR_GalaxyTint[40];     // rgb arm tint
            float3 DistantGalaxies(float3 d)
            {
                float3 col = 0;
                [loop] for (int gi = 0; gi < 40; gi++)
                {
                    float4 g = _RR_Galaxies[gi];
                    if (dot(d, g.xyz) < cos(g.w * 2.5)) continue;
                    float4 s = _RR_GalaxyShape[gi];
                    float3 t1 = normalize(cross(g.xyz, abs(g.y) < 0.9 ? float3(0, 1, 0) : float3(1, 0, 0)));
                    float3 t2 = cross(g.xyz, t1);
                    float2 uv = float2(dot(d, t1), dot(d, t2)) / g.w;
                    uv = float2(cos(s.x) * uv.x - sin(s.x) * uv.y, sin(s.x) * uv.x + cos(s.x) * uv.y);
                    uv.y /= s.y;
                    float r = length(uv);
                    float core = exp(-r * r * 30);
                    float body;
                    if (s.z < 0.7)
                    {
                        float ang = atan2(uv.y, uv.x);
                        float arms = pow(0.5 + 0.5 * cos(2 * ang - 5.5 * log(max(r, 0.03))), 3);
                        body = (arms * 0.8 + 0.25) * exp(-r * 3.2);
                    }
                    else body = exp(-r * r * 4.5);                                          // elliptical
                    col += (_RR_GalaxyTint[gi].rgb * body + float3(1.0, 0.86, 0.66) * core * 1.6) * s.w * saturate(1.25 - r * 0.5);
                }
                return col;
            }

            float4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float g = dot(d, normalize(_GalNormal.xyz));
                float band = exp(-g * g / 0.045) * 0.6 + exp(-g * g / 0.006) * 0.9;
                float clouds = RR_Fbm(d * 3.0, 5) * 0.5 + 0.5;
                float dust = smoothstep(0.45, 0.7, RR_Fbm(d * 6.0 + 11, 4) * 0.5 + 0.5);
                float3 neb = lerp(_BandTintA.rgb, _BandTintB.rgb, saturate(RR_Fbm(d * 1.5 + 3, 3) + 0.5));
                float3 col = neb * band * clouds * (1 - dust * 0.8) * _BandStrength;
                float starBoost = 1 + band * 2.5;
                col += StarLayer(d, 160, 0.22, 0.045) * 0.30 * _StarStrength;
                col += StarLayer(d, 380, 0.20 * starBoost / 3.5, 0.035) * 0.13 * _StarStrength;
                col += StarLayer(d.zxy, 800, 0.10 * starBoost / 3.5, 0.03) * 0.07 * _StarStrength;
                col += DistantGalaxies(d) * _StarStrength;
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}