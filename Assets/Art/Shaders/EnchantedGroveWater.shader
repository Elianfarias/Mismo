Shader "Mismo/Enchanted Grove Water"
{
    Properties
    {
        _ShallowColor("Shore turquoise", Color) = (0.22,0.58,0.52,1)
        _DeepColor("Deep teal", Color) = (0.035,0.22,0.25,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.8
        _WorldSpaceSurface("World surface without radial shore UV", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half _Smoothness;
                half _WorldSpaceSurface;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; half fog:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.uv=input.uv;output.fog=ComputeFogFactor(output.positionCS.z);return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.positionWS.xz;
                float2 cells=floor(p*7)/7;
                float wave=sin(cells.x*3.2+cells.y*2.7+_Time.y*.55)*sin(cells.y*4.3-_Time.y*.38);
                float shore=smoothstep(.45,.98,length(input.uv*2-1));
                shore=lerp(shore,.25,_WorldSpaceSurface);
                half3 base=lerp(_DeepColor.rgb,_ShallowColor.rgb,saturate(shore*.8+wave*.09+.15));
                InputData lighting=(InputData)0;
                lighting.positionWS=input.positionWS;
                lighting.normalWS=normalize(float3(cos(p.x*2+_Time.y)*.025,1,sin(p.y*2-_Time.y)*.025));
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI=SampleSH(lighting.normalWS);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=base;surface.metallic=.12;surface.smoothness=_Smoothness;
                surface.normalTS=half3(0,0,1);surface.occlusion=1;surface.alpha=1;
                surface.emission=_ShallowColor.rgb*pow(saturate(wave),18)*.15;
                half4 color=UniversalFragmentPBR(lighting,surface);
                color.rgb=MixFog(color.rgb,input.fog);return color;
            }
            ENDHLSL
        }
    }
}
