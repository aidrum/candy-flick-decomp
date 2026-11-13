// Direct translation of the original 2.6 GLES Toon/Basic BASE pass.
Shader "Toon/Basic" {
Properties {
    _Color ("Main Color", Color) = (.5,.5,.5,1)
    _MainTex ("Base (RGB)", 2D) = "white" {}
    _ToonShade ("ToonShader Cubemap(RGB)", Cube) = "" {}
}
SubShader {
    Tags { "RenderType"="Opaque" }
    Pass {
        Name "BASE"
        Cull Off
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
        struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float3 viewNormal:TEXCOORD1; };
        sampler2D _MainTex; float4 _MainTex_ST; samplerCUBE _ToonShade; float4 _Color;
        v2f vert(appdata v) {
            v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex);
            o.viewNormal=mul(UNITY_MATRIX_MV,float4(normalize(v.normal),0)).xyz; return o;
        }
        fixed4 frag(v2f i):SV_Target {
            float4 baseColor=_Color*tex2D(_MainTex,i.uv);
            float3 cube=texCUBE(_ToonShade,i.viewNormal).rgb;
            return float4((2.0*cube)*baseColor.rgb,baseColor.a);
        }
        ENDCG
    }
}
}
