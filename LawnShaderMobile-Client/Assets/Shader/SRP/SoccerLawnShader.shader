Shader "Fran/SoccerLawnShader"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _ColorGround ("Color Ground", Color) = (1,1,1,1)
        
        _MainIntensity("Intensity", Float) = 1
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue" = "Geometry"}

        Pass
        {
            ZWrite On
            Cull Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _ColorGround;
            half _MainIntensity;
            
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                //Blend Far
                half farBlend = smoothstep(0, 0.6, clamp(0,1, i.worldPos.y * 4));
                
                //Main Texture // Optimize THIS
                half3 main = tex2D(_MainTex, half2(i.worldPos.x, i.worldPos.z)  * half2(_MainTex_ST.x, _MainTex_ST.y) + half2(_MainTex_ST.z, _MainTex_ST.w));
                main *= _MainIntensity;
                
                half3 colorGroundFar = lerp(_ColorGround, half3(1,1,1), smoothstep(0.05,0,farBlend));
                
                main = lerp(main * colorGroundFar, main, i.uv.y);

                //Random Color
                half3 colorRnd = main * i.color.r;
                
                //Far Blend Color
                half3 outColor = lerp(main, colorRnd, farBlend);
                
                return half4(outColor, 1);
            }
            ENDCG
        }
    }
}
