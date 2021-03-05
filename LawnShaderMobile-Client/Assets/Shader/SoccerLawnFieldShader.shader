Shader "Fran/SoccerLawnFieldShader"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _MainIntensity("Intensity", Float) = 1
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _MainIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half3 main = tex2D(_MainTex, half2(i.worldPos.x, i.worldPos.z)  * half2(_MainTex_ST.x, _MainTex_ST.y) + half2(_MainTex_ST.z, _MainTex_ST.w));
                main *= _MainIntensity;
                
                return half4(main, 1);
            }
            ENDCG
        }
    }
}
