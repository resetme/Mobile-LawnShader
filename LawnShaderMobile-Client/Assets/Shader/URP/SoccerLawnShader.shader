Shader "Fran/SoccerLawnShader"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _ColorGround ("Color Ground", Color) = (1,1,1,1)
        
        _MainIntensity("Intensity", Float) = 1
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalRenderPipeline"}

        Pass
        {
            ZWrite On
            Cull Off
            
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma target 2.0
            
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _ColorGround;
            half _MainIntensity;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.worldPos = mul(UNITY_MATRIX_M, v.vertex);
                
                o.color = v.color;
                o.uv = v.uv;

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                //Blend Far
                half farBlend = smoothstep(0, 0.6, clamp(0,1, i.worldPos.y * 4));
                
                //Main Texture // Optimize THIS
                half3 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, half2(i.worldPos.x, i.worldPos.z)  * half2(_MainTex_ST.x, _MainTex_ST.y) + half2(_MainTex_ST.z, _MainTex_ST.w));
                main *= _MainIntensity;
                
                half3 colorGroundFar = lerp(_ColorGround, half3(1,1,1), smoothstep(0.05,0,farBlend));
                
                main = lerp(main * colorGroundFar, main, i.uv.y);

                //Random Color
                half3 colorRnd = main * i.color.r;
                
                //Far Blend Color
                half3 outColor = lerp(main, colorRnd, farBlend);
                
                return half4(outColor, 1);
            }
            ENDHLSL
        }
    }
}
