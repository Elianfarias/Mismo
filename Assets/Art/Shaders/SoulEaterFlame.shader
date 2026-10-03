Shader "Mismo/SoulEater/Flowing Flame"
{
    Properties { _Intensity("Intensity", Range(0,3)) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Flame"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input) { Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz); o.color=input.color; o.uv=input.uv; return o; }
            half4 Frag(Varyings input) : SV_Target
            {
                half edge=smoothstep(0,.24,input.uv.x)*smoothstep(0,.24,1-input.uv.x);
                return half4(input.color.rgb * _Intensity,input.color.a*edge);
            }
            ENDHLSL
        }
    }
}
