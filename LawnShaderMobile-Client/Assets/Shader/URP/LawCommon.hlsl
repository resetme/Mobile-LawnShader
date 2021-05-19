 //Projection Matrix Global
float4x4 _CameraMatrix;
float4x4 _ProjectionMatrix;
half _CubeShadowQuality;
            
CBUFFER_START(Common)
//Deformation
half4 _DeformationUV;
TEXTURE2D(_Deformation);
SAMPLER(sampler_Deformation);
            
//Cube Shadow
TEXTURE2D(_CubeShadow);
SAMPLER(sampler_CubeShadow);

half4 _CubeMapUV;
half _CubeShadowIntensity;

//Cloud Shadow
TEXTURE2D(_CloudShadow);
SAMPLER(sampler_CloudShadow);

half4 _CloudShadowUV1;
half _CloudShadowIntensity1;

half4 _CloudShadowUV2;
half _CloudShadowIntensity2;

//Cube Shadow
TEXTURE2D(_SnowShadow);
SAMPLER(sampler_SnowShadow);

//Top Color
half4 _TopColor;
half _TopColorLevel;
CBUFFER_END


half3 CloudShadow(half3 baseColor, half shadow, half2 uv)
{
    half cloud01 = SAMPLE_TEXTURE2D(_CloudShadow, sampler_CloudShadow, uv * _CloudShadowUV1.xy + half2(_Time.y * _CloudShadowUV1.z, _Time.y * _CloudShadowUV1.w)).r;
    half cloud02 = SAMPLE_TEXTURE2D(_CloudShadow, sampler_CloudShadow, uv * _CloudShadowUV2.xy + half2(_Time.y * _CloudShadowUV2.z, _Time.y * _CloudShadowUV2.w)).g;
    
    half clouds = saturate(cloud01 * cloud02);
    clouds = smoothstep(_CloudShadowIntensity1, _CloudShadowIntensity2, clouds);
    shadow = saturate(shadow * clouds);
    
    half3 outColor = lerp(baseColor * _CubeShadowIntensity, baseColor, shadow);
    
    return outColor;
}

half TopMask(half2 uv)
{
    half topMask = SAMPLE_TEXTURE2D(_SnowShadow, sampler_SnowShadow, uv).r;
    return topMask;
}