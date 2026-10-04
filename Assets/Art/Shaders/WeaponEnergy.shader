Shader "Mismo/Weapon Energy"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Soft ("Soft radial glow", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Soft;
            CBUFFER_END
            struct appdata { float4 vertex : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert(appdata input)
            {
                v2f output;
                output.position = TransformObjectToHClip(input.vertex.xyz);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }
            half4 frag(v2f input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                half falloff = saturate(1 - dot(p,p));
                return half4(input.color.rgb, input.color.a * lerp(1, falloff * falloff, _Soft));
            }
            ENDHLSL
        }
    }
}
