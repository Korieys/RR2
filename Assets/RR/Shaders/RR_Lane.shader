Shader "RR/Lane"
{
    // Jump lanes: ribbons expanded in screen space to a constant pixel width, dashed along their length.
    // Vertex: position on the lane centre line, normal = lane tangent, uv.x = distance along (world), uv.y = side (-1/1),
    // colour = lane tint (a = intensity).
    Properties
    {
        _Width ("Width (px)", Float) = 1.6
        _DashLength ("Dash length (world)", Float) = 4
        _DashFill ("Dash fill", Range(0, 1)) = 0.6
        _Flow ("Flow speed", Float) = 0
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+20" }
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
                float _Width, _DashLength, _DashFill, _Flow, _Fade;
            CBUFFER_END
            struct A { float4 posOS : POSITION; float3 tangent : NORMAL; float2 uv : TEXCOORD0; float4 col : COLOR; };
            struct V { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; float4 col : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                float3 ws = TransformObjectToWorld(v.posOS.xyz);
                float3 ws2 = TransformObjectToWorld(v.posOS.xyz + v.tangent);
                float4 c0 = TransformWorldToHClip(ws), c1 = TransformWorldToHClip(ws2);
                float2 s0 = c0.xy / c0.w, s1 = c1.xy / max(c1.w, 1e-4);
                float2 dir = normalize((s1 - s0) * _ScreenParams.xy + 1e-5);
                float2 perp = float2(-dir.y, dir.x);
                o.posCS = c0 + float4(perp * v.uv.y * _Width / _ScreenParams.xy * 2 * c0.w, 0, 0);
                o.uv = v.uv;
                o.col = v.col;
                return o;
            }
            float4 frag(V i) : SV_Target
            {
                float across = 1 - abs(i.uv.y);
                float f = frac(i.uv.x / _DashLength - _RR_Time * _Flow);
                float dash = _DashFill >= 0.999 ? 1 : smoothstep(0, 0.06, f) * smoothstep(_DashFill + 0.06, _DashFill, f);
                return float4(i.col.rgb * i.col.a * saturate(across * 1.6) * dash * _Fade, 0);
            }
            ENDHLSL
        }
    }
}