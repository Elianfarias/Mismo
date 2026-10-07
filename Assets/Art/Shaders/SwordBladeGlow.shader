Shader "Mismo/Sword Blade Glow"
{
    Properties { _Color ("Glow", Color) = (0.12,0.95,1,0.85) }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Offset -1, -1
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Color, _BladeStart, _BladeEnd;
            struct Input { float4 position : POSITION; };
            struct Output { float4 position : SV_POSITION; float3 world : TEXCOORD0; };
            Output vert(Input i) { Output o; o.world=TransformObjectToWorld(i.position.xyz); o.position=TransformWorldToHClip(o.world); return o; }
            half4 frag(Output i) : SV_Target
            {
                float3 axis=_BladeEnd.xyz-_BladeStart.xyz;
                float along=dot(i.world-_BladeStart.xyz,axis)/max(dot(axis,axis),0.0001);
                clip(along); clip(1.025-along);
                return _Color;
            }
            ENDHLSL
        }
    }
}
