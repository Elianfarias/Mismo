Shader "Mismo/Status/Poison Mist"
{
    Properties
    {
        [HDR] _Color ("Mist tint", Color) = (0.32,0.7,0.055,1)
        _EffectTime ("Local animation time", Float) = 0
        _Intensity ("Intensity", Range(0,1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _EffectTime, _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;output.color=input.color*_Color;return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 uv=input.uv;
                float n=noise(uv*5+float2(_EffectTime*.13,-_EffectTime*.21));
                n=n*.65+noise(uv*11+float2(n,-_EffectTime*.14))*.35;
                float edge=1-smoothstep(.13,.5,length(uv-.5));
                float alpha=edge*smoothstep(.22,.75,n)*input.color.a*_Intensity;
                return half4(input.color.rgb*(.65+n*.5),alpha);
            }
            ENDHLSL
        }
    }
}
