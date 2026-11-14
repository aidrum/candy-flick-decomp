Shader "CandyFlick/Disco Hair Outline"
{
    Properties
    {
        _OutlineColor("Outline Color",Color)=(0,0,0,1)
        _Outline("Outline width",Range(.002,.03))=.005
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+1" "RenderType"="Opaque" }
        Pass
        {
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION;float3 normal:NORMAL; };
            float _Outline;float4 _OutlineColor;
            float4 vert(appdata v):SV_POSITION
            {
                float4 position=UnityObjectToClipPos(v.vertex);
                float3 normal=mul((float3x3)UNITY_MATRIX_IT_MV,normalize(v.normal));
                float2 offset=mul((float2x2)UNITY_MATRIX_P,normal.xy);
                float depth=-UnityObjectToViewPos(v.vertex).z;
                float nearPlane=_ProjectionParams.y,farPlane=_ProjectionParams.z;
                float originalClipZ=((farPlane+nearPlane)*depth-2*farPlane*nearPlane)/(farPlane-nearPlane);
                position.xy+=offset*originalClipZ*_Outline;
                return position;
            }
            fixed4 frag():SV_Target { return _OutlineColor; }
            ENDCG
        }
    }
}
