Shader "Mismo/Buff Symbol"
{
    Properties { _Color ("Tint and opacity", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
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
            CBUFFER_END
            struct Attributes { float4 vertex : POSITION; half4 color : COLOR; };
            struct Varyings { float4 position : SV_POSITION; half4 color : COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings output; output.position=TransformObjectToHClip(input.vertex.xyz);
                output.color=input.color*_Color; return output;
            }
            half4 frag(Varyings input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}
