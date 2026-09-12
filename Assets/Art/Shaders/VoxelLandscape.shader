Shader "Mismo/Voxel Landscape"
{
    Properties
    {
        [HideInInspector] _Cull ("Cull", Float) = 2
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color, _GrassColor, _DirtColor;
            float _DetailStrength, _TextureScale;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 worldPos:TEXCOORD0;
                float3 worldNormal:TEXCOORD1;
                float4 tint:COLOR;
                float fog:TEXCOORD2;
                half3 vertexLighting:TEXCOORD3;
                float4 screenPos:TEXCOORD4;
            };
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.worldPos=p.positionWS;
                o.worldNormal=TransformObjectToWorldNormal(v.normalOS);
                o.tint=v.color; o.fog=ComputeFogFactor(p.positionCS.z);
                o.vertexLighting=VertexLighting(p.positionWS,o.worldNormal);
                o.screenPos=ComputeScreenPos(p.positionCS);
                return o;
            }
            float random(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            half4 frag(Varyings IN):SV_Target
            {
                float3 color=IN.tint.rgb*_Color.rgb;
                InputData input=(InputData)0;
                input.positionWS=IN.worldPos;
                input.positionCS=IN.positionCS;
                input.normalWS=normalize(IN.worldNormal);
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(IN.worldPos);
                input.shadowCoord=TransformWorldToShadowCoord(IN.worldPos);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                input.shadowCoord=IN.screenPos;
                #endif
                input.bakedGI=SampleSH(input.normalWS);
                input.vertexLighting=IN.vertexLighting;
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(IN.positionCS);
                input.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=color; surface.alpha=1; surface.occlusion=1;
                surface.normalTS=half3(0,0,1);
                half4 result=UniversalFragmentBlinnPhong(input,surface);
                result.rgb=MixFog(result.rgb,IN.fog);
                return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    Fallback Off
}
