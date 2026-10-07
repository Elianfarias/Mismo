Shader "Mismo/Status/Poison Overlay"
{
    Properties
    {
        [HDR] _Color ("Poison glow", Color) = (0.38,1.1,0.04,1)
        _Intensity ("Intensity", Range(0,1)) = 0.6
        _EffectTime ("Local animation time", Float) = 0
        _PatternScale ("Pattern scale", Float) = 5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Intensity, _EffectTime, _PatternScale;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 local : TEXCOORD2; };
            float hash(float3 p) { return frac(sin(dot(p, float3(127.1,311.7,74.7))) * 43758.5453); }
            float noise(float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);
            }
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.local = input.positionOS.xyz;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float rim = pow(1-saturate(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS))),2.5);
                float pattern = noise(input.local*_PatternScale-float3(0,_EffectTime*.32,0));
                float patches = smoothstep(.45,.78,pattern);
                float pulse = .8+.2*sin(_EffectTime*3.4);
                float alpha = saturate((rim*.46+patches*.19)*_Intensity*pulse);
                return half4(_Color.rgb*(.65+rim*.6),alpha);
            }
            ENDHLSL
        }
    }
}
