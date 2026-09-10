Shader "Mismo/Textured Terrain"
{
    Properties
    {
        _GrassColor ("Grass color", Color) = (0.24,0.38,0.12,1)
        _DirtColor ("Dirt color", Color) = (0.39,0.27,0.14,1)
        _DetailStrength ("Texture detail", Range(0,1)) = 0.45
        _TextureScale ("Pixels per metre", Range(2,24)) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0
        float4 _GrassColor,_DirtColor;
        float _DetailStrength,_TextureScale;
        struct Input { float4 tint; float3 worldPos; float3 worldNormal; };
        void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.tint=v.color;}
        float random(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        void surf(Input IN,inout SurfaceOutput o)
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
            o.Albedo=color;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
