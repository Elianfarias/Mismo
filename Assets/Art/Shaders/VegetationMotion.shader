Shader "Mismo/Vegetation Motion"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0
        _VertexColor ("Use vertex colors", Float) = 0
        _AlphaClip ("Alpha clip", Float) = 0
        _Cutoff ("Cutoff", Range(0,1)) = .5
        _Cull ("Cull", Float) = 2
        [HideInInspector] _VegetationShape ("Base height, height, rigid fraction, sway", Vector) = (0,1,0,.045)
        [HideInInspector] _VegetationResponse ("Player response", Vector) = (1,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST, _BaseColor;
        float _Smoothness, _VertexColor, _AlphaClip, _Cutoff, _Cull;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        UNITY_INSTANCING_BUFFER_START(Vegetation)
            UNITY_DEFINE_INSTANCED_PROP(float4, _VegetationShape)
            UNITY_DEFINE_INSTANCED_PROP(float4, _VegetationResponse)
        UNITY_INSTANCING_BUFFER_END(Vegetation)
        float4 _MismoWind, _MismoMotion, _MismoInteraction;
        float4 _MismoPlayerTrail[8];
        float3 _LightDirection, _LightPosition;

        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
            float2 uv:TEXCOORD0;
            float4 color:COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            float3 normalWS:TEXCOORD1;
            float2 uv:TEXCOORD2;
            float fog:TEXCOORD3;
            half3 vertexLighting:TEXCOORD4;
            float4 color:COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        float3 AnimateVegetation(float3 positionOS)
        {
            float4 shape = UNITY_ACCESS_INSTANCED_PROP(Vegetation, _VegetationShape);
            float response = UNITY_ACCESS_INSTANCED_PROP(Vegetation, _VegetationResponse).x;
            float3 world = TransformObjectToWorld(positionOS);
            float3 baseWS = TransformObjectToWorld(float3(0,shape.x,0));
            float height = saturate((positionOS.y-shape.x)/max(shape.y,.01));
            float flexible = smoothstep(shape.z,1,height);
            // Spatial gust field shared by all plants; a separate phase varies their sway.
            float time = _MismoMotion.x * _MismoMotion.y;
            float phase = dot(baseWS.xz,float2(.73,.49));
            float gust = .5+.5*sin(dot(baseWS.xz,_MismoWind.xy)*.12-time*.8);
            float sway = .55+.3*sin(time*1.7+phase)+.15*sin(time*2.9+phase*1.31);
            float2 offset = _MismoWind.xy * shape.w * _MismoWind.z * (1+gust*_MismoWind.w) * sway;
            // Take the strongest recent influence, not their sum: standing still cannot accumulate force.
            float influence = 0;
            float2 push = 0;
            if (response > 0 && _MismoInteraction.y > 0)
            {
                [unroll] for (int i=0;i<8;i++)
                {
                    float2 away = baseWS.xz-_MismoPlayerTrail[i].xz;
                    float distance = length(away);
                    float radial = 1-smoothstep(0,max(.01,_MismoInteraction.x),distance);
                    // Do not push plants on a different terrace or beneath a jumping player.
                    float vertical = 1-smoothstep(.6,1.8,abs(baseWS.y-_MismoPlayerTrail[i].y));
                    float weight = radial*vertical*smoothstep(0,1,_MismoPlayerTrail[i].w);
                    if (weight > influence)
                    {
                        influence = weight;
                        push = away/max(distance,.18)*weight;
                    }
                }
            }
            // Limit bending for very short flowers/grass. Root vertices remain fixed.
            float worldHeight = length(TransformObjectToWorldDir(float3(0,shape.y,0),false));
            offset += push*min(_MismoInteraction.y,worldHeight*.55)*response;
            world.xz += offset*flexible;
            return world;
        }
        Varyings Vert(Attributes v)
        {
            Varyings o=(Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionWS=AnimateVegetation(v.positionOS.xyz);
            o.positionCS=TransformWorldToHClip(o.positionWS);
            o.normalWS=TransformObjectToWorldNormal(v.normalOS);
            o.uv=TRANSFORM_TEX(v.uv,_BaseMap);
            o.color=lerp(float4(1,1,1,1),v.color,_VertexColor);
            o.fog=ComputeFogFactor(o.positionCS.z);
            o.vertexLighting=VertexLighting(o.positionWS,o.normalWS);
            return o;
        }
        half4 Albedo(Varyings i)
        {
            half4 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor*i.color;
            if(_AlphaClip>.5)clip(color.a-_Cutoff);
            return color;
        }
        Varyings ShadowVert(Attributes v)
        {
            Varyings o=Vert(v);
            float3 direction=_LightDirection;
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                direction=normalize(_LightPosition-o.positionWS);
            #endif
            o.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,direction)));
            return o;
        }
        half4 DepthFrag(Varyings i):SV_Target { UNITY_SETUP_INSTANCE_ID(i); Albedo(i); return i.positionCS.z; }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 color=Albedo(i);
                InputData input=(InputData)0;
                input.positionWS=i.positionWS;
                input.positionCS=i.positionCS;
                input.normalWS=normalize(i.normalWS);
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    input.shadowCoord=ComputeScreenPos(TransformWorldToHClip(i.positionWS));
                #endif
                input.bakedGI=SampleSH(input.normalWS);
                input.vertexLighting=i.vertexLighting;
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=color.rgb; surface.alpha=1; surface.occlusion=1;
                surface.smoothness=_Smoothness; surface.normalTS=half3(0,0,1);
                half4 result=UniversalFragmentPBR(input,surface);
                result.rgb=MixFog(result.rgb,i.fog);
                return result;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            void NormalFrag(Varyings i,out half4 normal:SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                ,out uint layers:SV_Target1
                #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(i); Albedo(i);
                float3 n=normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    normal=half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
                #else
                    normal=half4(n,0);
                #endif
                #ifdef _WRITE_RENDERING_LAYERS
                    layers=EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
