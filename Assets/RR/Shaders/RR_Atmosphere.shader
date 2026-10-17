Shader "RR/Atmosphere"
{
    Properties
    {
        _AtmoColor ("Colour", Color) = (0.35, 0.6, 1, 1)
        _SunsetColor ("Forward scatter colour", Color) = (1, 0.55, 0.3, 1)
        _Intensity ("Intensity", Range(0, 6)) = 1.6
        _PlanetRadius ("Planet radius (fraction of shell)", Float) = 0.95
        _Falloff ("Falloff", Range(0.5, 12)) = 4
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-20" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RR_Common.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _AtmoColor, _SunsetColor;
                float _Intensity, _PlanetRadius, _Falloff;
            CBUFFER_END
            struct A { float4 posOS : POSITION; };
            struct V { float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; };
            V vert(A v) { V o; o.posWS = TransformObjectToWorld(v.posOS.xyz); o.posCS = TransformWorldToHClip(o.posWS); return o; }
            float4 frag(V i) : SV_Target
            {
                float3 c = TransformObjectToWorld(float3(0, 0, 0));
                float scale = length(TransformObjectToWorld(float3(1, 0, 0)) - c);
                float Rp = _PlanetRadius * scale;
                float Ra = scale;
                float3 cam = GetCameraPositionWS();
                float3 rayDir = normalize(i.posWS - cam);
                float3 oc = c - cam;
                float tc = dot(oc, rayDir);
                float b = length(oc - rayDir * tc);
                float h = (b - Rp) / max(Ra - Rp, 1e-4);
                // Over the limb the light is evaluated at the ray's closest approach; over the disk at the shell point
                // the ray enters (the closest approach degenerates to the planet centre there).
                float3 P = h > 0 ? cam + rayDir * tc : i.posWS;
                float3 N = normalize(P - c);
                float3 L = RR_StarDir(P);
                float dens;
                if (h > 0) dens = exp(-h * _Falloff) * saturate(1 - h);
                else dens = 0.08 + 0.92 * pow(saturate(b / Rp), 14);
                float lit = RR_Diffuse(dot(N, L), 0.35);
                float fw = pow(saturate(dot(rayDir, L)), 10) * saturate(1 - abs(h));
                float3 col = (_AtmoColor.rgb * lit + _SunsetColor.rgb * fw * 0.8) * _RR_StarColor.rgb * dens * _Intensity * 0.35;
                return float4(col, 0);
            }
            ENDHLSL
        }
    }
}