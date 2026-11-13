// Translation of the 2.6 GLES programs, Documentation/Recovered/Shaders26/Particle Add.shader.txt.
#include "UnityCG.cginc"
struct appdata26 { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
struct v2f26 { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0;
#ifdef SOFTPARTICLES_ON
float4 projected:TEXCOORD1;
#endif
};
sampler2D _MainTex; float4 _MainTex_ST; fixed4 _TintColor; float _InvFade;
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
v2f26 vert26(appdata26 v)
{
    v2f26 o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=TRANSFORM_TEX(v.uv,_MainTex);
#ifdef SOFTPARTICLES_ON
    o.projected=ComputeScreenPos(o.vertex); COMPUTE_EYEDEPTH(o.projected.z);
#endif
    return o;
}
fixed4 frag26(v2f26 i):SV_Target
{
#ifdef SOFTPARTICLES_ON
    float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.projected)));
    i.color.a*=saturate(_InvFade*(depth-i.projected.z));
#endif
    fixed4 color=(2.0*i.color)*_TintColor*tex2D(_MainTex,i.uv);
    clip(color.a-.01);
    return color;
}
