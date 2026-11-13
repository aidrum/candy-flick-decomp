// Direct translation of original BASE + OUTLINE; preserve the original GL clip-Z width equation.
Shader "Toon/Basic Outline" {
Properties {
    _Color ("Main Color", Color) = (.5,.5,.5,1)
    _OutlineColor ("Outline Color", Color) = (0,0,0,1)
    _Outline ("Outline width", Range(.002,.03)) = .005
    _MainTex ("Base (RGB)", 2D) = "white" {}
    _ToonShade ("ToonShader Cubemap(RGB)", Cube) = "" {}
}
SubShader {
    Tags { "RenderType"="Opaque" }
    UsePass "Toon/Basic/BASE"
    Pass {
        Name "OUTLINE"
        Cull Front
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
        float _Outline; float4 _OutlineColor;
        float4 vert(appdata v):SV_POSITION {
            float4 clipPosition=UnityObjectToClipPos(v.vertex);
            float3 normal=mul((float3x3)UNITY_MATRIX_IT_MV,normalize(v.normal));
            float2 offset=mul((float2x2)UNITY_MATRIX_P,normal.xy);
            // Modern D3D/reversed-Z clip coordinates differ from the original GLES clip coordinates.
            float depth=-UnityObjectToViewPos(v.vertex).z;
            float nearPlane=_ProjectionParams.y, farPlane=_ProjectionParams.z;
            float originalClipZ=((farPlane+nearPlane)*depth-2*farPlane*nearPlane)/(farPlane-nearPlane);
            clipPosition.xy+=offset*originalClipZ*_Outline;
            return clipPosition;
        }
        fixed4 frag():SV_Target { return _OutlineColor; }
        ENDCG
    }
}
}
