Shader "Mismo/Voxel Landscape"
{
    Properties { _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0
        fixed4 _Color;
        struct Input { fixed4 tint; };
        void vert(inout appdata_full v, out Input o) { UNITY_INITIALIZE_OUTPUT(Input,o); o.tint=v.color; }
        void surf(Input IN, inout SurfaceOutput o) { o.Albedo=IN.tint.rgb*_Color.rgb; o.Alpha=1; }
        ENDCG
    }
    Fallback "Diffuse"
}
