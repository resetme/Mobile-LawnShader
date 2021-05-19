Shader "Fran/ParticlesStencil"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        
            [Header(Hardware stencil)]
        HARDWARE_StencilRef ("Stencil REF", Range(0, 255)) = 0
        HARDWARE_ReadMask ("Stencil Read Mask", Range(0, 255)) = 255
        HARDWARE_WriteMask ("Stencil Write Mask", Range(0, 255)) = 255
        
        [Enum(UnityEngine.Rendering.CompareFunction)] HARDWARE_StencilComp ("Stencil comparison", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] HARDWARE_StencilPass ("Stencil Pass", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] HARDWARE_StencilFail ("Stencil Fail", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] HARDWARE_StencilZFail ("Stencil Z Fail", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }

        Pass
        {
        
             ZWrite On
             ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha 
        
            Stencil
			{
				Ref 5
				Comp [HARDWARE_StencilComp]
				
				Pass [HARDWARE_StencilPass]
				Fail [HARDWARE_StencilFail]
				ZFail [HARDWARE_StencilZFail]
			}
			
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return half4(col.rgb, col.r);
            }
            ENDCG
        }
    }
}
