Shader "CandyFlick/TimeTravel Lens"
{
    Properties { _MainTex("Lens tint",2D)="white"{} _Opacity("Opacity",Range(0,1))=1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "TimeTravelLensMask.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            sampler2D _MainTex; float _Opacity;
            v2f vert(appdata v) { v2f o; o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o; }
            fixed4 frag(v2f i):SV_Target { fixed4 color=tex2D(_MainTex,i.uv);clip(TimeTravelLensMask(color)-.5);color.a*=_Opacity;return color; }
            ENDCG
        }
    }
}
