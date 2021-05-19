Shader "Fran/SoccerLawnFieldShader"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _MainIntensity("Intensity", Float) = 1
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalRenderPipeline"}
        
        Pass
        {
            ZWrite On
            Cull Back
            
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma target 2.0
            
            #pragma multi_compile_instancing
            
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half _MainIntensity;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half3 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, half2(i.worldPos.x, i.worldPos.z)  * half2(_MainTex_ST.x, _MainTex_ST.y) + half2(_MainTex_ST.z, _MainTex_ST.w));
                main *= _MainIntensity;
                
                return half4(main, 1);
            }
            ENDHLSL
        }
    }
}
