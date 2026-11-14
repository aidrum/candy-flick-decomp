// Direct translation of original BASE + OUTLINE; preserve the original GL clip-Z width equation.
Shader "CandyFlick/TimeTravel Opaque" {
Properties {
    _Color ("Main Color", Color) = (.5,.5,.5,1)
    _OutlineColor ("Outline Color", Color) = (0,0,0,1)
    _Outline ("Outline width", Range(.002,.03)) = .005
    _MainTex ("Base (RGB)", 2D) = "white" {}
    _ToonShade ("ToonShader Cubemap(RGB)", Cube) = "" {}
}
SubShader {
    Tags { "RenderType"="Opaque" }
    Pass {
        Name "BASE"
        Cull Back
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        #include "TimeTravelLensMask.cginc"
        struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
        struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float3 viewNormal:TEXCOORD1; };
        sampler2D _MainTex; float4 _MainTex_ST; samplerCUBE _ToonShade; float4 _Color;
        v2f vert(appdata v) {
            v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex);
            o.viewNormal=mul(UNITY_MATRIX_MV,float4(normalize(v.normal),0)).xyz; return o;
        }
        fixed4 frag(v2f i):SV_Target {
            float4 sampled=tex2D(_MainTex,i.uv);clip(.5-TimeTravelLensMask(sampled));
            float4 baseColor=_Color*sampled;
            float3 cube=texCUBE(_ToonShade,i.viewNormal).rgb;
            return float4((2.0*cube)*baseColor.rgb,baseColor.a);
        }
        ENDCG
    }
}
}
