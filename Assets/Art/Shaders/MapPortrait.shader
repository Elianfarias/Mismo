Shader "Mismo/Map Portrait"
{
    Properties { _Color("Color",Color)=(1,1,1,1) _MainTex("Texture",2D)="white"{} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _MainTex_ST;fixed4 _Color;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float light:TEXCOORD1;};
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.light=.65+.35*saturate(dot(UnityObjectToWorldNormal(v.normal),normalize(float3(-.4,.8,1))));return o;}
            fixed4 frag(v2f i):SV_Target{return fixed4(tex2D(_MainTex,i.uv).rgb*_Color.rgb*i.light,1);}
            ENDCG
        }
    }
}
