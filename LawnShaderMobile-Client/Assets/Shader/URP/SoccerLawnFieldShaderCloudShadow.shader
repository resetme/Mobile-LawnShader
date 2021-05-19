Shader "Fran/SoccerLawnFieldShaderCloudShadow"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _BaseTex("Base Texture", 2D) = "white" {}
        _FieldMask("Field Mask", 2D) = "white" {}
        _MainIntensity("Intensity", Float) = 1
        _FieldMaskIntensity("Field Mask Intensity", Float) = 1
        
         _Deformation("Deformation Texture", 2D) = "white" {}
         _DeformationUV("Deformation UV", Vector) = (0,0,0,0)
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

            #pragma vertex vert
            #pragma fragment frag


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            #include "LawCommon.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD2;
                
                //CubeShadow
                float4 texCoordProj : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_FieldMask);
            SAMPLER(sampler_FieldMask);
            
            TEXTURE2D(_BaseTex);
            SAMPLER(sampler_BaseTex);
            
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _FieldMask_ST;
            half _MainIntensity;
            half _FieldMaskIntensity;
            CBUFFER_END
            
            float4 TextureProjection(float4 v)
            {
                float4x4 modelMatrix = unity_ObjectToWorld;
                float4x4 constantMatrix = {0.5, 0, 0, 0.5, 0, 0.5, 0, 0.5, 0, 0, 0.5, 0.5, 0, 0, 0, 1};
                float4x4 textureMatrix;
                float4 modelForTexture = mul(modelMatrix, v);
                textureMatrix = mul(mul(constantMatrix, _ProjectionMatrix), _CameraMatrix);
                
                return  mul(textureMatrix, modelForTexture);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                
                o.uv.xy = half2(o.worldPos.x, o.worldPos.z) * half2(_MainTex_ST.x, _MainTex_ST.y) + half2(_MainTex_ST.z, _MainTex_ST.w);
                o.uv.zw = half2(o.worldPos.x, o.worldPos.z) * half2(_FieldMask_ST.x, _FieldMask_ST.y) + half2(_FieldMask_ST.z, _FieldMask_ST.w);
                
                o.uv2.xy = half2(o.worldPos.x, o.worldPos.z) * half2(_DeformationUV.x, _DeformationUV.y) + half2(_DeformationUV.z, _DeformationUV.w);
                o.uv2.zw = half2(o.worldPos.x, o.worldPos.z) * half2(_CubeMapUV.x, _CubeMapUV.y) + half2(_CubeMapUV.z, _CubeMapUV.w);
                
                o.texCoordProj = TextureProjection(v.vertex);
                
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                //Cache Texture
                half3 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
                half3 base = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, i.uv.xy);
                half deformation = SAMPLE_TEXTURE2D(_Deformation, sampler_Deformation, i.uv2.xy).r;
                half3 filedMask = SAMPLE_TEXTURE2D(_FieldMask, sampler_FieldMask, i.uv.zw).rgb;
                half cubeShadow = SAMPLE_TEXTURE2D_LOD(_CubeShadow, sampler_CubeShadow, half2(i.texCoordProj.xy/i.texCoordProj.w), _CubeShadowQuality).r;
                
                main = lerp(base, main, deformation);
                main *= _MainIntensity;
   
                //Shadows
                half shadows = (1-filedMask.g) * cubeShadow;
                
                //Field Mask
                main = lerp(main, main +(step(0.11, main.r) * (filedMask.r) * _FieldMaskIntensity), filedMask.r);
                //Old Damage
                main = lerp(base, main, 1-filedMask.b);
                
                //Add Cloud Shadow
                main = CloudShadow(main, shadows, half2(i.worldPos.x, i.worldPos.z));
                
                return half4(main, 1);
            }
            ENDHLSL
        }
    }
}
