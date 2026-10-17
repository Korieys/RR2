Shader "RR/HexGrid"
{
    // Contextual command overlay for orbital Commandfall. Hex coordinates are computed per pixel from world position
    // (pointy-top axial: x = sqrt(3)(q + r/2), z = -1.5 r, times _HexSize). _Cells (32 x 16) holds per-cell state:
    // r = on board, g = terrain (0 open, 1 cover, 2 nebula, 3 wreck, 4 storm) / 4, b = highlight code / 8, a = zone (0.25 own, 0.5 enemy, 1 objective).
    Properties
    {
        _Cells ("Cells", 2D) = "black" {}
        _HexSize ("Hex size", Float) = 1
        _LineColor ("Line colour", Color) = (0.55, 0.65, 0.8, 1)
        _Line ("Line strength", Range(0, 1)) = 0.16
        _Hover ("Hover cell (q, r, on)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+40" }
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
            TEXTURE2D(_Cells); SAMPLER(sampler_Cells);
            CBUFFER_START(UnityPerMaterial)
                float _HexSize, _Line;
                float4 _LineColor, _Hover;
            CBUFFER_END
            struct A { float4 posOS : POSITION; };
            struct V { float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; };
            V vert(A v) { V o; o.posWS = TransformObjectToWorld(v.posOS.xyz); o.posCS = TransformWorldToHClip(o.posWS); return o; }

            float3 CubeRound(float3 c)
            {
                float3 r = round(c);
                float3 d = abs(r - c);
                if (d.x > d.y && d.x > d.z) r.x = -r.y - r.z;
                else if (d.y > d.z) r.y = -r.x - r.z;
                else r.z = -r.x - r.y;
                return r;
            }

            float4 frag(V i) : SV_Target
            {
                float2 p = float2(i.posWS.x, i.posWS.z) / _HexSize;
                float rr = -p.y / 1.5;
                float qq = p.x / 1.7320508 - rr * 0.5;
                float3 cube = CubeRound(float3(qq, rr, -qq - rr));
                float q = cube.x, r = cube.y;
                float2 center = float2(1.7320508 * (q + r * 0.5), -1.5 * r);
                float2 l = abs(p - center);
                float hd = max(l.x, l.x * 0.5 + l.y * 0.8660254) / 0.8660254;     // 1 at the edge
                float4 cell = SAMPLE_TEXTURE2D_LOD(_Cells, sampler_Cells, float2((q + 16 + 0.5) / 32, (r + 8 + 0.5) / 16), 0);
                if (cell.r < 0.5) return 0;
                float aa = fwidth(hd) * 1.2;
                float edge = smoothstep(1 - aa * 1.5, 1, hd);
                float code = floor(cell.b * 8 + 0.5);
                float3 hc = code == 1 ? float3(1.0, 0.78, 0.36) : code == 2 ? float3(1.0, 0.32, 0.25) : code == 3 ? float3(0.75, 0.45, 1.0)
                          : code == 4 ? float3(1.0, 0.95, 0.85) : code == 5 ? float3(1.0, 0.3, 0.25) : code == 6 ? float3(0.4, 0.85, 1.0) : 0;
                float3 col = _LineColor.rgb * edge * _Line;
                float inner = smoothstep(0.6, 0.98, hd);
                if (code > 0)
                {
                    float fill = code == 5 ? 0.05 : 0.07;
                    col += hc * (fill + inner * 0.18 + edge * 0.85);
                    if (code == 4) col += hc * (0.2 + 0.15 * sin(_RR_Time * 5));
                }
                // Deployment zones and objectives, faint.
                if (cell.a > 0.9) col += float3(1.0, 0.85, 0.45) * (edge * 0.6 + smoothstep(0.35, 0.5, hd) * (1 - smoothstep(0.5, 0.62, hd)) * 0.9);
                else if (cell.a > 0.4) col += float3(0.8, 0.4, 1.0) * edge * 0.12;
                else if (cell.a > 0.2) col += float3(0.35, 0.65, 1.0) * edge * 0.12;
                if (_Hover.z > 0.5 && abs(q - _Hover.x) < 0.1 && abs(r - _Hover.y) < 0.1) col += float3(1, 1, 1) * (edge * 0.7 + 0.03);
                // Fade grid lines toward the camera's far distance so the board reads as an overlay, not a slab.
                float dist = length(GetCameraPositionWS() - i.posWS);
                return float4(col * saturate(1.4 - dist / 120), 0);
            }
            ENDHLSL
        }
    }
}