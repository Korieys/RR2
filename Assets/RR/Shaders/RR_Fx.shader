Shader "RR/Fx"
{
    // Unlit additive effect: orbit lines, selection rings, beams, impact glows. uv.x runs along a line, uv.y across it.
    Properties
    {
        _Color ("Colour (HDR)", Color) = (1, 1, 1, 1)
        _Dash ("Dash count (0 = solid)", Float) = 0
        _DashFill ("Dash fill", Range(0, 1)) = 0.55
        _Scroll ("Dash scroll speed", Float) = 0
        _Radial ("Radial glow mode", Range(0, 1)) = 0
        _Soft ("Edge softness", Range(0.01, 1)) = 0.5
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }
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
                float4 _Color; float _Dash, _DashFill, _Scroll, _Radial, _Soft, _Fade;
            CBUFFER_END
            struct A { float4 posOS : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
            struct V { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; float4 col : TEXCOORD1; };
            V vert(A v) { V o; o.posCS = TransformObjectToHClip(v.posOS.xyz); o.uv = v.uv; o.col = v.col; return o; }
            float4 frag(V i) : SV_Target
            {
                float a;
                if (_Radial > 0.5)
                {
                    float r = length(i.uv);
                    a = saturate(1 - r) ; a = a * a;
                }
                else
                {
                    float across = abs(i.uv.y * 2 - 1);
                    a = saturate((1 - across) / _Soft);
                    if (_Dash > 0)
                    {
                        float f = frac(i.uv.x * _Dash - _RR_Time * _Scroll);
                        a *= smoothstep(0, 0.08, f) * smoothstep(_DashFill + 0.08, _DashFill, f);
                    }
                }
                float4 vc = i.col.a > 0 ? i.col : float4(1, 1, 1, 1);
                return float4(_Color.rgb * vc.rgb * a * vc.a * _Fade, 0);
            }
            ENDHLSL
        }
    }
}