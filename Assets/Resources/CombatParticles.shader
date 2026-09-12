Shader "Mismo/Combat Particles"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            CBUFFER_END
            struct appdata { float4 vertex : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = TransformObjectToHClip(input.vertex.xyz);
                output.color = input.color * _Color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            half4 frag(v2f input) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color; }
            ENDHLSL
        }
    }
}
