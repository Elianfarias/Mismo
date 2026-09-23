Shader "Mismo/Loot Glow"
{
    Properties
    {
        [HDR] _Tint("Color", Color) = (0.3,0.8,1,1)
        _Intensity("Intensity", Range(0,5)) = 1.6
        _Ground("Ground ring", Float) = 0
        _Kind("0 Weapon / 1 Component / 2 Consumable", Float) = 0
        _Beam("Beam strength", Range(0,1)) = 0.5
        _Seed("Animation offset", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Intensity, _Ground, _Kind, _Beam, _Seed;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float t=_Time.y+_Seed;
                float pulse=0.88+0.12*sin(t*2.4);
                float glow=0, core=0;
                if(_Ground>0.5)
                {
                    float2 p=i.uv*2-1;float r=length(p);
                    float ring=exp(-abs(r-0.65)*65);
                    float angle=atan2(p.y,p.x);
                    float arcs=0.55+0.45*pow(abs(sin(angle*3+t*.6)),6);
                    glow=(exp(-r*r*7)*.2+ring*arcs*.5)*pulse;
                }
                else
                {
                    float2 p=float2((i.uv.x-.5)*.6,i.uv.y-.23);
                    p.y-=sin(t*2)*.012;
                    float radius=length(p);
                    glow=exp(-radius*25)*.85;
                    float shape=radius;
                    if(_Kind>0.5&&_Kind<1.5)shape=(abs(p.x)+abs(p.y))*.8;
                    if(_Kind>1.5)shape=min(max(abs(p.x)-.013,abs(p.y)-.045),max(abs(p.x)-.045,abs(p.y)-.013))+.028;
                    core=1-smoothstep(.018,.033,shape);
                    glow+=exp(-abs(shape-.038)*180)*.3;
                    float beam=exp(-abs(p.x)*100)*smoothstep(.18,.28,i.uv.y)*pow(saturate(1-i.uv.y),1.7);
                    glow+=beam*_Beam*1.8;
                    // Small rising sparks are procedural, with no texture or particle simulation.
                    [unroll] for(int k=0;k<5;k++)
                    {
                        float phase=frac(t*(.17+k*.013)+k*.213);
                        float2 spark=float2(sin(k*7.13+_Seed)*(.06+phase*.09),phase*.55);
                        float d=length(p-spark);
                        glow+=exp(-d*220)*sin(phase*3.14159)*.35;
                    }
                    glow*=pulse;
                }
                float edge=smoothstep(0,.08,i.uv.x)*smoothstep(0,.08,1-i.uv.x)*smoothstep(0,.06,i.uv.y)*smoothstep(0,.08,1-i.uv.y);
                float3 color=(_Tint.rgb*glow+lerp(_Tint.rgb,1,.65)*core)*_Intensity*edge;
                return half4(color,0);
            }
            ENDHLSL
        }
    }
}
