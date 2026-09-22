Shader "Mismo/Textured Terrain"
{
    Properties
    {
        [HideInInspector] _Cull ("Cull", Float) = 2

        _GrassColor ("Grass color", Color) = (0.24,0.38,0.12,1)
        _DirtColor ("Dirt color", Color) = (0.39,0.27,0.14,1)
        _DetailStrength ("Texture detail", Range(0,1)) = 0.45
        _TextureScale ("Pixels per metre", Range(2,24)) = 10
        [HideInInspector] _DistantTerrain ("Distant terrain", Float) = 0
        [HideInInspector] _TerrainCoverage ("Loaded chunks", 2D) = "black" {}
        [HideInInspector] _CoverageGrid ("Coverage origin and size", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _Color, _GrassColor, _DirtColor, _CoverageGrid;
        float _DetailStrength, _TextureScale, _DistantTerrain;
        CBUFFER_END
        TEXTURE2D(_TerrainCoverage);
        SAMPLER(sampler_TerrainCoverage);

        void ClipLoadedTerrain(float3 worldPos)
        {
            if (_DistantTerrain > .5)
            {
                // Sample the chunk centre, including at negative world coordinates.
                float2 cell = floor(worldPos.xz / 32.0) - _CoverageGrid.xy;
                if (all(cell >= 0) && all(cell < _CoverageGrid.zw))
                {
                    float2 uv = (cell + .5) / _CoverageGrid.zw;
                    clip(.5 - SAMPLE_TEXTURE2D_LOD(_TerrainCoverage, sampler_TerrainCoverage, uv, 0).r);
                }
            }
        }
        struct TerrainDepthAttributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
        struct TerrainDepthVaryings
        {
            float4 positionCS:SV_POSITION;
            float3 worldPos:TEXCOORD0;
            float3 worldNormal:TEXCOORD1;
        };
        TerrainDepthVaryings TerrainDepthVertex(TerrainDepthAttributes v)
        {
            TerrainDepthVaryings o;
            VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
            o.positionCS = p.positionCS;
            o.worldPos = p.positionWS;
            o.worldNormal = TransformObjectToWorldNormal(v.normalOS);
            return o;
        }
        ENDHLSL
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
                ClipLoadedTerrain(IN.worldPos);
                            float3 original=IN.tint.rgb;
            float grass=step(original.r*1.08,original.g)*step(original.b*1.3,original.g);
            float earth=step(original.b*1.3,original.r)*step(original.g,original.r);
            float slope=1-step(.6,IN.worldNormal.y);
            float3 color=lerp(original,_DirtColor.rgb,earth);
            color=lerp(color,lerp(_GrassColor.rgb,_DirtColor.rgb*.8,slope),grass);
            // World-aligned pixel detail has no seams between streamed chunks.
            float2 uv=IN.worldPos.xz;
            if(slope>.5)uv=float2(IN.worldPos.x+IN.worldPos.z,IN.worldPos.y);
            float2 pixel=floor(uv*_TextureScale);
            float grain=random(pixel);
            float patch=random(floor(uv*1.7));
            float flecks=step(.9,grain)*.28-step(grain,.12)*.22;
            float detail=(patch-.5)*.18+(grain-.5)*.16+flecks;
            float fade=1-smoothstep(.5,2,max(fwidth(uv.x),fwidth(uv.y))*_TextureScale);
            color*=1+_DetailStrength*(detail*fade+(patch-.5)*.09);

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
        // The horizon must also disappear from depth/normal prepasses; otherwise
        // it can still occlude the road or contribute incorrect ambient occlusion.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TerrainDepthVertex
            #pragma fragment TerrainDepthFragment
            half TerrainDepthFragment(TerrainDepthVaryings IN):SV_Target
            {
                ClipLoadedTerrain(IN.worldPos);
                return IN.positionCS.z;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TerrainDepthVertex
            #pragma fragment TerrainNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            void TerrainNormalsFragment(TerrainDepthVaryings IN, out half4 normal:SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                , out uint layers:SV_Target1
                #endif
            )
            {
                ClipLoadedTerrain(IN.worldPos);
                float3 normalWS = normalize(IN.worldNormal);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormal = PackNormalOctQuadEncode(normalWS);
                normal = half4(PackFloat2To888(saturate(octNormal * .5 + .5)), 0);
                #else
                normal = half4(normalWS, 0);
                #endif
                #ifdef _WRITE_RENDERING_LAYERS
                layers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
