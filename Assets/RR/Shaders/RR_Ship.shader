Shader "RR/Ship"
{
    // Hull shader for Blender-generated ships and stations. Vertex colour: R = material class (0 hull, 0.5 trim,
    // 1 glass/crystal/bio), G = emission, B = baked ambient occlusion. Lit by the system's star (_RR_StarPosWS).
    Properties
    {
        _HullColor ("Hull", Color) = (0.72, 0.71, 0.69, 1)
        _HullColor2 ("Hull variation", Color) = (0.55, 0.55, 0.56, 1)
        _TrimColor ("Trim", Color) = (0.16, 0.17, 0.2, 1)
        _GlassColor ("Glass / crystal / bio glow", Color) = (0.3, 0.6, 1.0, 1)
        _GlowIntensity ("Glow intensity", Range(0, 12)) = 3
        _HullRough ("Hull roughness", Range(0.05, 1)) = 0.7
        _TrimRough ("Trim roughness", Range(0.05, 1)) = 0.35
        _Metal ("Trim metalness", Range(0, 1)) = 0.6
        _DetailScale ("Detail scale", Float) = 9
        _Stone ("Granite speckle (Silex)", Range(0, 1)) = 0
        _Chitin ("Chitin sheen (Craigon)", Range(0, 1)) = 0
        _SheenColor ("Sheen colour", Color) = (0.7, 0.3, 1.0, 1)
        _RimTint ("Rim tint (selection / ownership)", Color) = (0, 0, 0, 0)
        _Damage ("Damage", Range(0, 1)) = 0
        _Dissolve ("Dissolve", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "RR_Common.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _HullColor, _HullColor2, _TrimColor, _GlassColor, _SheenColor, _RimTint;
            float _GlowIntensity, _HullRough, _TrimRough, _Metal, _DetailScale, _Stone, _Chitin, _Damage, _Dissolve;
        CBUFFER_END
        struct A { float4 posOS : POSITION; float3 nOS : NORMAL; float4 col : COLOR; };
        struct V { float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; float3 nWS : TEXCOORD1; float4 col : TEXCOORD2; float3 posOS : TEXCOORD3; };
        V vert(A v)
        {
            V o;
            o.posWS = TransformObjectToWorld(v.posOS.xyz);
            o.posCS = TransformWorldToHClip(o.posWS);
            o.nWS = TransformObjectToWorldNormal(v.nOS);
            o.col = v.col;
            o.posOS = v.posOS.xyz;
            return o;
        }
        void DissolveClip(V i)
        {
            if (_Dissolve > 0) clip(RR_Noise(i.posOS * 14) * 0.5 + 0.5 - _Dissolve * 1.05);
        }
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
                DissolveClip(i);
                float cls = i.col.r, emis = i.col.g, ao = lerp(0.35, 1.0, i.col.b);
                float isGlass = smoothstep(0.75, 0.95, cls);
                float isTrim = smoothstep(0.25, 0.45, cls) * (1 - isGlass);
                float isHull = 1 - isGlass - isTrim;
                float3 p = i.posOS * _DetailScale;
                float3 n = normalize(i.nWS);
                // Procedural surface detail (object space, so it never swims with the camera).
                float speck = 0, vein = 0;
                if (_Stone > 0)
                {
                    float2 c = RR_Cell(p * 2.2);
                    speck = saturate(1 - c.x * 2.2) * 0.35 + (RR_Noise(p * 7) * 0.5 + 0.5) * 0.5;
                    vein = smoothstep(0.02, 0.0, abs(RR_Fbm(p * 0.6, 3)));
                }
                float streak = _Chitin > 0 ? RR_Fbm(float3(p.x * 0.4, p.y * 3.0, p.z * 0.4), 3) * 0.5 + 0.5 : 0.5;
                float3 hull = lerp(_HullColor.rgb, _HullColor2.rgb, saturate(speck * _Stone + (streak - 0.5) * _Chitin * 1.5));
                hull *= 1 - vein * 0.35 * _Stone;
                // Bump from the detail field via screen-space derivatives (cheap, stable).
                float h = speck * _Stone + streak * _Chitin * 0.6;
                float3 dpx = ddx(i.posWS), dpy = ddy(i.posWS);
                float dhx = ddx(h), dhy = ddy(h);
                float3 r1 = cross(dpy, n), r2 = cross(n, dpx);
                float det = dot(dpx, r1);
                float3 grad = (det != 0) ? (r1 * dhx + r2 * dhy) / det : 0;
                n = normalize(n - grad * 0.012 * isHull);
                float3 albedo = hull * isHull + _TrimColor.rgb * isTrim + _GlassColor.rgb * 0.25 * isGlass;
                // Battle damage: scorched plates.
                float burn = smoothstep(1 - _Damage, 1 - _Damage + 0.15, RR_Fbm(i.posOS * 3.1, 3) * 0.5 + 0.5) * step(0.01, _Damage);
                albedo *= 1 - burn * 0.75;
                float rough = _HullRough * isHull + _TrimRough * isTrim + 0.08 * isGlass;
                float metal = _Metal * isTrim;
                float3 L = RR_StarDir(i.posWS);
                float3 Vv = normalize(GetCameraPositionWS() - i.posWS);
                float ndl = saturate(dot(n, L));
                float3 star = _RR_StarColor.rgb;
                float3 spec = lerp(float3(1, 1, 1), albedo * 2, metal) * RR_GGX(n, Vv, L, rough) * ndl;
                float3 col = albedo * (1 - metal * 0.7) * star * ndl * ao + spec * star * ao;
                // Indirect fill and a faint bounce rim so silhouettes survive on the night side.
                float rim = pow(1 - saturate(dot(n, Vv)), 3);
                col += albedo * _RR_Ambient.rgb * ao * (0.6 + 0.4 * n.y);
                col += _RR_Ambient.rgb * rim * _RR_Ambient.a * ao;
                // Craigon chitin: thin-film iridescent sheen.
                if (_Chitin > 0)
                {
                    float f = pow(1 - saturate(dot(n, Vv)), 2);
                    float3 film = 0.5 + 0.5 * cos(6.2831 * (f * 1.6 + float3(0.0, 0.33, 0.67)));
                    col += film * _SheenColor.rgb * f * _Chitin * (star * (0.25 + ndl) * 0.25 + 0.04) * isHull;
                }
                // Glass, crystal and bioluminescence.
                float fres = pow(1 - saturate(dot(n, Vv)), 2);
                col += _GlassColor.rgb * _GlowIntensity * (emis * (0.6 + 0.4 * fres) + 0.15 * isGlass) * (1 - burn * 0.6);
                col += star * RR_GGX(n, Vv, L, 0.06) * ndl * isGlass * 0.6;
                // Damage embers.
                col += float3(1, 0.35, 0.08) * burn * saturate(RR_Noise(i.posOS * 20 + _RR_Time) - 0.2) * 3 * _Damage;
                col += _RimTint.rgb * rim * _RimTint.a * 2.5;
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
            float4 fragD(V i) : SV_Target { DissolveClip(i); return 0; }
            ENDHLSL
        }
    }
}