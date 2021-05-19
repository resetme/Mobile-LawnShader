Shader "Fran/LedPanelRGB"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LedTex("Led Texture", 2D) = "white" {}
        _LedAnimation("Led Animation Offset", 2D) = "white" {}
        _LedPixels("Led Pixels", Vector) = (3,3,0,0)
        
        _Pixels("Pixels", Vector) = (10,10,0,0)
        
        _LedIntensity("Led Intensity", Float) = 1
        _LedSpeed("Led Speed", Float) = 1
        _LedUvScale("Led Scale", Range(0,1)) = 1
        _LedColor("Led Color", Color) = (1,1,1,1)
        _LedColorIntensity("Led Color Intensity", Range(0,1)) = 1
        
        _LerpLed("Lerp Led", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalRenderPipeline"}

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl" 

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float distance : TEXCOORD1;
            };

            TEXTURE2D (_MainTex);
            SAMPLER (sampler_MainTex);
            TEXTURE2D (_LedTex);
            SAMPLER (sampler_LedTex);
            TEXTURE2D (_LedAnimation);
            SAMPLER (sampler_LedAnimation);
            
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            
            half4 _LedPixels;
            half4 _Pixels;
            
            half _LedIntensity;
            half _LedSpeed;
            half _LedUvScale;
            half _LedColorIntensity;
            half4 _LedColor;
            
            half _LerpLed;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex).xy;

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half2 uv = round(i.uv * _Pixels.xy + 0.5) / _Pixels.xy;
                half2 lerpUv = lerp(i.uv, uv, _LerpLed);

                half3 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, lerpUv.xy).rgb;

                half2 ledUV = (i.uv * _Pixels.xy) / _LedPixels;
                half3 led = SAMPLE_TEXTURE2D(_LedTex, sampler_LedTex, ledUV).rgb;
                
                half ledAnimation = SAMPLE_TEXTURE2D(_LedAnimation, sampler_LedAnimation, ledUV * _LedUvScale).r;
                half ledOffset = ledAnimation * 2 -1;
                ledOffset = sin((_Time.y * _LedSpeed) + (ledOffset * 3.14));
                ledOffset = abs(ledOffset) * step(0.1, ledAnimation);
              
                                
                half3 ledMix =  (col * led) * (ledOffset * _LedIntensity);
                ledMix = lerp(ledMix, _LedColor.rgb, _LedColorIntensity);
                col = lerp(col, ledMix, _LerpLed);
                
                return half4(col.rgb, 1);
            }
            ENDHLSL
        }
    }
}