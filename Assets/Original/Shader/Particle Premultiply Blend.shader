// Render states and color equation recovered from 2.6's original compiled shader,
// Documentation/Recovered/Shaders26/Particle Premultiply Blend.shader.txt.
// The previous generic replacement used SrcAlpha blending and ignored vertex color,
// which darkened the premultiplied edges of the super-candy trail.
Shader "Particles/Alpha Blended Premultiply" {
Properties {
    _MainTex ("Particle Texture", 2D) = "white" {}
    _InvFade ("Soft Particles Factor", Range(.01,3)) = 1
}
SubShader {
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        ZWrite Off
        Cull Off
        Blend One OneMinusSrcAlpha
        ColorMask RGB
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_particles
        #include "UnityCG.cginc"
        struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
        struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0;
        #ifdef SOFTPARTICLES_ON
            float4 projected:TEXCOORD1;
        #endif
        };
        sampler2D _MainTex; float4 _MainTex_ST; float _InvFade;
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        v2f vert(appdata v)
        {
            v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=TRANSFORM_TEX(v.uv,_MainTex);
        #ifdef SOFTPARTICLES_ON
            o.projected=ComputeScreenPos(o.vertex); COMPUTE_EYEDEPTH(o.projected.z);
        #endif
            return o;
        }
        fixed4 frag(v2f i):SV_Target
        {
        #ifdef SOFTPARTICLES_ON
            float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.projected)));
            i.color.a*=saturate(_InvFade*(depth-i.projected.z));
        #endif
            // Original: (color * texture) * color.a, written to a One/OneMinusSrcAlpha blend.
            return i.color*tex2D(_MainTex,i.uv)*i.color.a;
        }
        ENDCG
    }
}
}
