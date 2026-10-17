Shader "RR/Haze"
{
    // Soft volumetric puff on a sphere: density from the view ray's chord length through the sphere (no billboards).
    Properties
    {
        _Color ("Colour", Color) = (0.6, 0.4, 1, 1)
        _Density ("Density", Range(0, 4)) = 1
        _Flicker ("Flicker", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-5" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Front
            ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RR_Common.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Density, _Flicker;
            CBUFFER_END
            struct A { float4 posOS : POSITION; };
            struct V { float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; };
            V vert(A v) { V o; o.posWS = TransformObjectToWorld(v.posOS.xyz); o.posCS = TransformWorldToHClip(o.posWS); return o; }
            float4 frag(V i) : SV_Target
            {
                float3 c = TransformObjectToWorld(float3(0, 0, 0));
                float R = length(TransformObjectToWorld(float3(1, 0, 0)) - c);
                float3 cam = GetCameraPositionWS();
                float3 dir = normalize(i.posWS - cam);
                float3 oc = cam - c;
                float b = dot(oc, dir);
                float cc = dot(oc, oc) - R * R;
                float h = b * b - cc;
                if (h <= 0) return 0;
                float s = sqrt(h);
                float t0 = max(-b - s, 0), t1 = -b + s;
                float chord = max(t1 - t0, 0) / (2 * R);
                float3 mid = cam + dir * (t0 + t1) * 0.5;
                float n = RR_Fbm((mid - c) / R * 1.8 + _RR_Time * 0.03, 3) * 0.5 + 0.5;
                float fl = 1 + _Flicker * (step(0.93, frac(sin(floor(_RR_Time * 9) * 12.9898 + c.x) * 43758.5)) * 2.5 - 0.2);
                float d = chord * chord * (0.4 + n) * _Density * fl;
                return float4(_Color.rgb * d, 0);
            }
            ENDHLSL
        }
    }
}