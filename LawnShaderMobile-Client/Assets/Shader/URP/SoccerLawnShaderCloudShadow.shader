Shader "Fran/SoccerLawnShaderCloudShadow"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _BaseTex("Base Texture", 2D) = "white" {}
        _FieldMask("Field Mask", 2D) = "white" {}
        _ColorGround ("Color Ground", Color) = (1,1,1,1)
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
            Cull Off

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma target 2.0
            
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UnityInstancing.cginc"
            #include "LawCommon.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD2;
                float4 color : COLOR;
                
                //CubeShadow
                float4 texCoordProj : TEXCOORD4;

                UNITY_VERTEX_INPUT_INSTANCE_ID
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
            half4 _ColorGround;
            half _MainIntensity;
            half _FieldMaskIntensity;
            CBUFFER_END
           
            
            float4 TextureProjection(float4 v)
            {
              
                float4x4 modelMatrix = UNITY_MATRIX_M;//unity_ObjectToWorldArray[unity_InstanceID];
                float4x4 constantMatrix = {0.5, 0, 0, 0.5, 0, 0.5, 0, 0.5, 0, 0, 0.5, 0.5, 0, 0, 0, 1};
                float4x4 textureMatrix;
                float4 modelForTexture = mul(modelMatrix, v);
                textureMatrix = mul(mul(constantMatrix, _ProjectionMatrix), _CameraMatrix);
                
                return  mul(textureMatrix, modelForTexture);
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
  
                o.worldPos = mul(UNITY_MATRIX_M, v.vertex);
                o.color = v.color;
                o.uv2.xy = half2(o.worldPos.x, o.worldPos.z)  * half2(_FieldMask_ST.x, _FieldMask_ST.y) + half2(_FieldMask_ST.z, _FieldMask_ST.w);//field
                o.uv2.zw = half2(o.worldPos.x, o.worldPos.z)  * half2(_MainTex_ST.x, _MainTex_ST.y) + half2(_MainTex_ST.z, _MainTex_ST.w);//main
                o.uv.xy = half2(o.worldPos.x, o.worldPos.z) * half2(_CubeMapUV.x, _CubeMapUV.y) + half2(_CubeMapUV.z, _CubeMapUV.w);
                
                half deformation = SAMPLE_TEXTURE2D_LOD(_Deformation, sampler_Deformation, half2(o.worldPos.x, o.worldPos.z) * half2(_DeformationUV.x, _DeformationUV.y) + half2(_DeformationUV.z, _DeformationUV.w), 0).r;
                v.vertex.y -= (1-deformation) * 0.2;
                
                //Move
                v.vertex.x += (sin(_Time.y + ((v.color.b * 3.14))) * o.color.g) * 0.02f;
                v.vertex.z += (sin(_Time.y + ((v.color.b * 3.14))) * o.color.g) * 0.02f;
 
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                
                #ifdef UNITY_INSTANCING_ENABLED
                o.texCoordProj = TextureProjection(v.vertex);
                #endif
                
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                //Cache Textures first for optimization // Optimize inside vertex
                half3 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv2.xy);
                half3 base = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, i.uv.xy);
                half3 filedMask = SAMPLE_TEXTURE2D(_FieldMask, sampler_FieldMask, i.uv2.xy).rgb;
                main *= _MainIntensity;
 
                //Blend Far
                half farBlend = smoothstep(0, 0.6, clamp(0,1, i.worldPos.y * 4));
                half3 colorGroundFar = lerp(_ColorGround, half3(1,1,1), smoothstep(0.05,0,farBlend));
                main = lerp(main * colorGroundFar, main, i.color.g);

                //Random Color
                half3 colorRnd = main * i.color.r;
                
                //Far Blend Color
                half3 outColor = lerp(main, colorRnd, farBlend);

                half cubeShadow = SAMPLE_TEXTURE2D_LOD(_CubeShadow, sampler_CubeShadow, half2(i.texCoordProj.xy/i.texCoordProj.w) , _CubeShadowQuality).r;

                //Shadows
                half shadows = (1-filedMask.g) * cubeShadow;
                
                //Add Top Color
                half3 topColor = lerp(outColor, _TopColor , i.color.g);
                outColor = lerp(outColor, _TopColor * i.color.r, smoothstep(_TopColorLevel, 1, i.uv.y));
                
                //Field Mask
                outColor = lerp(main, main +(step(0.11, main.r) * (filedMask.r) * _FieldMaskIntensity), filedMask.r);

                 //Old Damage
                outColor = lerp(base, outColor, 1-filedMask.b);
                
                //Add Shadow
                outColor = CloudShadow(outColor, shadows, half2(i.worldPos.x, i.worldPos.z));

                //Light
 
                return half4(outColor, 1);
            }
            ENDHLSL
        }
    }
}
