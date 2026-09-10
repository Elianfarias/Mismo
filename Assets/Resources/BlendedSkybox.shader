Shader "Mismo/Blended Skybox"
{
    Properties
    {
        _SkyA ("Sky A", Cube) = "grey" {}
        _SkyB ("Sky B", Cube) = "grey" {}
        _Blend ("Blend", Range(0,1)) = 0
        _ExposureA ("Exposure A", Float) = 1
        _ExposureB ("Exposure B", Float) = 1
        _Rotation ("Rotation", Range(0,360)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            samplerCUBE _SkyA,_SkyB;
            float4 _SkyA_HDR,_SkyB_HDR;
            float _Blend,_ExposureA,_ExposureB,_Rotation;
            struct v2f {float4 vertex:SV_POSITION;float3 direction:TEXCOORD0;};
            v2f vert(float4 vertex:POSITION){v2f o;o.vertex=UnityObjectToClipPos(vertex);o.direction=vertex.xyz;return o;}
            float4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.direction);float angle=_Rotation*UNITY_PI/180;
                d.xz=float2(cos(angle)*d.x-sin(angle)*d.z,sin(angle)*d.x+cos(angle)*d.z);
                float3 a=DecodeHDR(texCUBE(_SkyA,d),_SkyA_HDR)*_ExposureA;
                float3 b=DecodeHDR(texCUBE(_SkyB,d),_SkyB_HDR)*_ExposureB;
                return float4(lerp(a,b,_Blend),1);
            }
            ENDCG
        }
    }
    Fallback Off
}
