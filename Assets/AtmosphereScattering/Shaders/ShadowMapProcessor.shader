Shader "Hidden/Yangrc/ShadowMapProcessor"
{
	Properties
	{
		_MainTex("MainTex", 2D) = "white"
	}
	SubShader
	{
		Tags { 
			"RenderType"="Opaque" 
		}
		LOD 100

		//Capture shadow map.
		Pass
		{
			ZTest Always
			ZWrite Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "AutoLight.cginc"
			#include "Lighting.cginc"
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _ApShadowMap;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			
			half4 frag (v2f i) : SV_Target
			{
				half4 col = tex2D(_ApShadowMap, i.uv);
				return col;
			}
			ENDCG
		}
	}
}
