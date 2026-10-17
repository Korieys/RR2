Shader "RR/Planet"
{
    Properties
    {
        _AlbedoCube ("Albedo (rgb) Roughness (a)", Cube) = "white" {}
        _NormalCube ("Object normal (rgb) Height (a)", Cube) = "bump" {}
        _DetailCube ("City (r) Emission (g) Water (b)", Cube) = "black" {}
        _CloudCube ("Clouds (r)", Cube) = "black" {}
        _AtmoColor ("Atmosphere colour", Color) = (0.35, 0.6, 1, 1)
        _AtmoStrength ("Atmosphere haze", Range(0, 3)) = 1
        _CloudColor ("Cloud colour", Color) = (1, 1, 1, 1)
        _CloudAmount ("Cloud amount", Range(0, 1)) = 0.8
        _CloudRot ("Cloud rotation (rad)", Float) = 0
        _CityColor ("City light colour", Color) = (1, 0.8, 0.5, 1)
        _EmisColor ("Surface emission colour", Color) = (1, 0.35, 0.05, 1)
        _SpecColor2 ("Water specular tint", Color) = (1, 0.95, 0.85, 1)
        _RingTex ("Ring bands (rgb colour, a density)", 2D) = "black" {}
        _RingInner ("Ring inner (planet radii)", Float) = 0
        _RingOuter ("Ring outer (planet radii)", Float) = 0
        _Glint ("Crystal glint", Range(0, 2)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "RR_Common.hlsl"
        TEXTURECUBE(_AlbedoCube); SAMPLER(sampler_AlbedoCube);
        TEXTURECUBE(_NormalCube); SAMPLER(sampler_NormalCube);
        TEXTURECUBE(_DetailCube); SAMPLER(sampler_DetailCube);
        TEXTURECUBE(_CloudCube); SAMPLER(sampler_CloudCube);
        TEXTURE2D(_RingTex); SAMPLER(sampler_RingTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _AtmoColor, _CloudColor, _CityColor, _EmisColor, _SpecColor2;
            float _AtmoStrength, _CloudAmount, _CloudRot, _RingInner, _RingOuter, _Glint;
        CBUFFER_END
        struct A { float4 posOS : POSITION; };
        struct V { float4 posCS : SV_POSITION; float3 posOS : TEXCOORD0; float3 posWS : TEXCOORD1; };
        V vert(A v)
        {
            V o;
            o.posOS = v.posOS.xyz;
            o.posWS = TransformObjectToWorld(v.posOS.xyz);
            o.posCS = TransformWorldToHClip(o.posWS);
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float3 RotY(float3 d, float a) { float c = cos(a), s = sin(a); return float3(c * d.x - s * d.z, d.y, s * d.x + c * d.z); }

            float4 frag(V i) : SV_Target
            {
                float3 dirOS = normalize(i.posOS);
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float radiusWS = length(TransformObjectToWorld(float3(1, 0, 0)) - center) * length(i.posOS);
                float3x3 o2w = (float3x3)GetObjectToWorldMatrix();
                float3 geoN = normalize(mul(o2w, dirOS));
                float4 alb = SAMPLE_TEXTURECUBE(_AlbedoCube, sampler_AlbedoCube, dirOS);
                float4 nh = SAMPLE_TEXTURECUBE(_NormalCube, sampler_NormalCube, dirOS);
                float4 det = SAMPLE_TEXTURECUBE(_DetailCube, sampler_DetailCube, dirOS);
                float3 n = normalize(mul(o2w, normalize(nh.xyz * 2 - 1)));
                float3 L = RR_StarDir(i.posWS);
                float3 Vv = normalize(GetCameraPositionWS() - i.posWS);
                float geoNdl = dot(geoN, L);
                float day = smoothstep(-0.12, 0.18, geoNdl);
                float3 LOS = normalize(mul(transpose(o2w), L));

                float cloud = SAMPLE_TEXTURECUBE(_CloudCube, sampler_CloudCube, RotY(dirOS, _CloudRot)).r * _CloudAmount;
                float cloudSh = SAMPLE_TEXTURECUBE(_CloudCube, sampler_CloudCube, RotY(normalize(dirOS + LOS * 0.02), _CloudRot)).r * _CloudAmount;

                float ringSh = 1;
                if (_RingOuter > 0)
                {
                    float3 ringN = normalize(mul(o2w, float3(0, 1, 0)));
                    float dn = dot(L, ringN);
                    float t = dot(center - i.posWS, ringN) / (abs(dn) > 1e-4 ? dn : 1e-4);
                    if (t > 0)
                    {
                        float r = length(i.posWS + L * t - center) / max(radiusWS, 1e-4);
                        if (r > _RingInner && r < _RingOuter)
                            ringSh = 1 - 0.85 * SAMPLE_TEXTURE2D(_RingTex, sampler_RingTex, float2((r - _RingInner) / (_RingOuter - _RingInner), 0.5)).a;
                    }
                }

                float3 starCol = _RR_StarColor.rgb;
                float diff = RR_Diffuse(dot(n, L), 0.04) * smoothstep(-0.2, 0.05, geoNdl);
                float water = det.b;
                float rough = alb.a;
                float3 surf = alb.rgb * starCol * diff * (1 - cloudSh * 0.55) * ringSh;
                float3 specN = normalize(lerp(n, geoN, water));
                surf += _SpecColor2.rgb * starCol * RR_GGX(specN, Vv, L, lerp(rough, 0.12, water)) * saturate(dot(specN, L)) * (water * 0.9 + 0.08) * ringSh * (1 - cloud);
                if (_Glint > 0)
                {
                    float sp = pow(saturate(RR_Noise(dirOS * 900) * 0.5 + 0.5), 18) * (1 - water);
                    surf += starCol * sp * _Glint * RR_GGX(n, Vv, L, 0.3) * saturate(dot(n, L)) * 0.3;
                }
                surf += alb.rgb * _RR_Ambient.rgb;
                float night = 1 - day;
                surf += _CityColor.rgb * det.r * night * (1 - cloud * 0.85) * 1.6;
                surf += _EmisColor.rgb * det.g * (0.35 + 0.65 * night) * (1 - cloud * 0.6);
                float cDiff = RR_Diffuse(geoNdl, 0.18);
                float3 cl = _CloudColor.rgb * (starCol * cDiff * ringSh + _RR_Ambient.rgb * 1.5);
                float3 col = lerp(surf, cl, saturate(cloud));
                float fres = pow(1 - saturate(dot(geoN, Vv)), 3);
                col += _AtmoColor.rgb * starCol * fres * RR_Diffuse(geoNdl, 0.3) * _AtmoStrength * 0.8;
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
            float4 fragD(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}