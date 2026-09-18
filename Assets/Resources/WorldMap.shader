Shader "Mismo/World Map"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION; float3 normal:NORMAL; fixed4 color:COLOR;};
            float4 _MapClipRect;
            struct v2f {float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 mapPosition:TEXCOORD0;};
            v2f vert(appdata v)
            {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);
                o.mapPosition=mul(unity_ObjectToWorld,v.vertex).xz;
                float light=.56+.44*saturate(dot(UnityObjectToWorldNormal(v.normal),normalize(float3(-.5,1,-.4))));
                o.color=fixed4(v.color.rgb*light,1);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                clip(i.mapPosition-_MapClipRect.xy);
                clip(_MapClipRect.zw-i.mapPosition);
                return i.color;
            }
            ENDCG
        }
    }
}
