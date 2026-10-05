Shader "Mismo/Player Arrival"
{
    Properties
    {
        _BaseMap ("Skin texture", 2D) = "white" {}
        _BaseColor ("Skin color", Color) = (1,1,1,1)
        _NoiseTex ("Ellen respawn pattern", 2D) = "white" {}
        [HDR] _EdgeColor ("Arrival glow", Color) = (0,1.386,5.34,1)
        _ArrivalProgress ("Reveal", Range(0,1)) = 1
        _ArrivalOrigin ("Feet", Vector) = (0,0,0,0)
        _ArrivalHeight ("Height", Float) = 1.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor, _EdgeColor, _ArrivalOrigin;
            float _ArrivalProgress, _ArrivalHeight;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float3 relative = input.positionWS - _ArrivalOrigin.xyz;
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, relative.xy * 1.4 + float2(0, _ArrivalProgress * .4)).r;
                float level = saturate(relative.y / max(.1, _ArrivalHeight));
                float reveal = _ArrivalProgress * 1.35 - .17 - level - (noise - .5) * .23;
                clip(reveal);
                half edge = (1 - smoothstep(0, .12, reveal)) * (1 - smoothstep(.88, 1, _ArrivalProgress));
                half3 normal = normalize(input.normalWS);
                Light light = GetMainLight();
                half3 lighting = SampleSH(normal) + light.color * saturate(dot(normal, light.direction));
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                return half4(albedo * max(lighting, .18) + _EdgeColor.rgb * edge, 1);
            }
            ENDHLSL
        }
    }
}
