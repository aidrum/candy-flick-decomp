Shader "Particles/Alpha Blended" {
Properties {
    _TintColor ("Tint Color", Color) = (.5,.5,.5,.5)
    _MainTex ("Particle Texture", 2D) = "white" {}
    _InvFade ("Soft Particles Factor", Range(.01,3)) = 1
}
SubShader {
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        CGPROGRAM
        #pragma vertex vert26
        #pragma fragment frag26
        #pragma multi_compile_particles
        #include "Particle26.cginc"
        ENDCG
    }
}
}
