Shader "RR/UnitSurface"
{
 Properties
 {
  _BaseMap("Authored surface",2D)="white"{}
  [Normal] _NormalMap("Surface detail",2D)="bump"{}
  _MaskMap("Occlusion roughness metallic",2D)="white"{}
  _Energy("Energy",Color)=(.4,.7,1,1)
  _BaseTint("Tint",Color)=(1,1,1,1)
  _Bio("Chitin",Float)=0
  _Damage("Damage",Range(0,1))=0
  _Dissolve("Dissolve",Range(0,1))=0
  _RimTint("Selection",Color)=(0,0,0,0)
 }
 SubShader
 {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
  #include "RR_Common.hlsl"
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
  TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
  CBUFFER_START(UnityPerMaterial)
  float4 _BaseMap_ST,_Energy,_RimTint,_BaseTint;float _Bio,_Damage,_Dissolve;
  CBUFFER_END
  struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float4 t:TANGENT;};
  struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float3 obj:TEXCOORD3;float4 tangent:TEXCOORD4;};
  V vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.n);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.obj=i.p.xyz;o.tangent=float4(TransformObjectToWorldDir(i.t.xyz),i.t.w*GetOddNegativeScale());return o;}
  float noiseAt(float3 p){return RR_Noise(p*18)*.5+.5;}
  void cut(V i){if(_Dissolve>.001)clip(noiseAt(i.obj)-_Dissolve*1.07);}
  ENDHLSL
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   half4 frag(V i):SV_Target
   {
    cut(i);float3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseTint.rgb;
    // Recover readable midtones from the source maps' very dark painted armor.
    base=lerp(base,sqrt(max(base,0)),.22);
    float3 n=normalize(i.normal),v=normalize(GetCameraPositionWS()-i.world),l=RR_StarDir(i.world);
    float3 detail=UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,i.uv),.65);
    if(dot(i.tangent.xyz,i.tangent.xyz)>.1){float3 t=normalize(i.tangent.xyz);n=normalize(t*detail.x+cross(n,t)*i.tangent.w*detail.y+n*detail.z);}
    float3 masks=SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,i.uv).rgb;
    float ndl=saturate(dot(n,l)),rim=pow(1-saturate(dot(n,v)),3);
    float hi=max(base.r,max(base.g,base.b)),lo=min(base.r,min(base.g,base.b));
    float crystal=smoothstep(.10,.36,hi-lo)*smoothstep(.25,.7,hi);
    float rough=clamp(masks.g,.24,.85);
    float fill=saturate(dot(n,normalize(v+float3(0,.45,0))));
    float3 c=base*(_RR_StarColor.rgb*ndl*.78+max(_RR_Ambient.rgb,float3(.22,.25,.31))*(.7+.3*n.y)+float3(.45,.49,.58)*fill);
    c*=lerp(.5,1,masks.r);
    c+=RR_GGX(n,v,l,rough)*ndl*_RR_StarColor.rgb*lerp(.035,.14,masks.b);
    c+=base*crystal*(.45+.10*sin(_Time.y*1.8+i.obj.y*4));
    c+=_Energy.rgb*rim*(.10+crystal*.16)+_RimTint.rgb*_RimTint.a*rim;
    float burn=smoothstep(1-_Damage,1-_Damage+.2,noiseAt(i.obj))*step(.01,_Damage);
    c*=1-burn*.58;
    if(_Dissolve>.001)c+=_Energy.rgb*smoothstep(.06,0,abs(noiseAt(i.obj)-_Dissolve*1.07))*3;
    return half4(c,1);
   }
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment depth
   half4 depth(V i):SV_Target{cut(i);return 0;}
   ENDHLSL
  }
 }
}
