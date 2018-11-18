// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Skybox/AtmosphereScattering"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_RayleighMolecularDensity("RayleighMolecularDensity", float) = 2.504
		_AirRefractionIndex("AirRefractionIndex", float) = 1.00029
		_MieG("MieG", float) = 0.79
		_RayleighScatteringCoefficient("RayleighScatteringCoefficient", Vector) = (4.6, 4.6, 4.6)
		_MieScatteringCoefficient("MieScatteringCoefficient", float) = 2
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "AtmosphereScatteringHelper.cginc"
			#include "Lighting.cginc"
			
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float4 worldPos : TEXCOORD1;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				return o;
			}
			
			sampler2D _MainTex;

			fixed4 frag (v2f i) : SV_Target
			{
				i.worldPos /= i.worldPos.w;
				float3 viewDir = normalize(i.worldPos.xyz - _WorldSpaceCameraPos);
				return float4(_LightColor0 * SampleColor(_WorldSpaceCameraPos, viewDir), 1.0);
				//return col;
			}
			ENDCG
		}
	}
}
