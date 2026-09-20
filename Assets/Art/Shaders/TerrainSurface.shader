Shader "Mismo/Textured Terrain"
{
    Properties
    {
        [HideInInspector] _Cull ("Cull", Float) = 2

        _GrassColor ("Grass color", Color) = (0.24,0.38,0.12,1)
        _DirtColor ("Dirt color", Color) = (0.39,0.27,0.14,1)
        _DetailStrength ("Texture detail", Range(0,1)) = 0.45
        _TextureScale ("Pixels per metre", Range(2,24)) = 10
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
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    Fallback Off
}
