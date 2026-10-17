Shader "RR/StarField"
{
    // Ambient (decorative) stars: world-space points with real depth. Expanded to billboards in clip space with a
    // pixel-size floor, dimmed with distance. They are scenery, not actionable systems.
    Properties
    {
        _Brightness ("Brightness", Range(0, 8)) = 1
        _RefDistance ("Reference distance", Float) = 300
        _MaxPixels ("Max size (px)", Float) = 5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-100" }
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
            CBUFFER_START(UnityPerMaterial)
                float _Brightness, _RefDistance, _MaxPixels;
            CBUFFER_END
            struct A { float4 posOS : POSITION; float2 uv : TEXCOORD0; float2 size : TEXCOORD1; float4 col : COLOR; };
            struct V { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; float4 col : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                float3 ws = TransformObjectToWorld(v.posOS.xyz);
                float4 c = TransformWorldToHClip(ws);
                float dist = length(GetCameraPositionWS() - ws);
                float px = v.size.x * _ScreenParams.y / (dist * 0.85);       // world size to pixels (approx. 45 degree fov)
                float r = clamp(px, 0.9, _MaxPixels);
                float2 off = v.uv * r * 2.0 / _ScreenParams.xy * c.w;
                o.posCS = c + float4(off, 0, 0);
                o.uv = v.uv;
                float fall = saturate((_RefDistance * _RefDistance) / (dist * dist));
                float energy = px / r;                                        // keep total light when clamped up to 1 px
                // A slow, subtle twinkle on the brighter stars, mostly calmed by reduced motion.
                float h = RR_Rand(RR_Hash(asuint(v.posOS.x) ^ RR_Hash(asuint(v.posOS.z))));
                float twinkle = 1 + 0.28 * _RR_Motion * sin(_RR_Time * (0.8 + 1.6 * h) + h * 60) * smoothstep(0.35, 0.8, v.col.a);
                o.col = float4(v.col.rgb * v.col.a * _Brightness * lerp(0.25, 1.0, fall) * saturate(energy * 2 + 0.25) * twinkle, 1);
                return o;
            }
            float4 frag(V i) : SV_Target
            {
                float d = length(i.uv);
                float a = saturate(1 - d);
                a = a * a * (1 + 2 * exp(-d * d * 18));
                return float4(i.col.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}