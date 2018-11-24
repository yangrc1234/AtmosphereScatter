// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Skybox/AtmosphereScatteringPrecomputed"
{
	Properties
	{
		_SingleRayleigh("SingleRayleigh", 3D) = "white" {}
		_SingleMie("SingleMie", 3D) = "white" {}
		_MultipleScattering("MultipleScattering", 3D) = "white"{}
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
			#include "Lighting.cginc"
			#include "MultipleScatteringHelper.cginc"
			
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
			
			sampler3D _SingleRayleigh;
			sampler3D _SingleMie;
			sampler3D _MultipleScattering;
			float3 _ScatteringSize;

			RadianceSpectrum GetScattering(
				IN(AtmosphereParameters) atmosphere,
				IN(ScatteringTexture) scattering_texture,
				uint3 texture_size,
				Length r, Number mu, Number mu_s, bool ray_r_mu_intersects_ground
			);

			fixed4 frag (v2f i) : SV_Target
			{
				i.worldPos /= i.worldPos.w;
				float3 viewDir = normalize(i.worldPos.xyz - _WorldSpaceCameraPos);
				float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);

				AtmosphereParameters atm = GetAtmParameters();

				float r = length(_WorldSpaceCameraPos + float3(0, atm.bottom_radius, 0));

				float mu = dot(viewDir, float3(0.0f, 1.0f, 0.0f));
				float mu_s = dot(float3(0.0f, 1.0f, 0.0f), lightDir);

				float nu = dot(viewDir, lightDir);

				float3 scattering_size = _ScatteringSize;

				float3 rayleigh =
					GetScattering(atm,
						_SingleRayleigh,
						scattering_size,
						r, mu, mu_s,
						false) *
					RayleighPhaseFunction(nu);

				float3 mie = 
					GetScattering(atm,
						_SingleMie,
						scattering_size,
						r, mu, mu_s,
						false) *
					MiePhaseFunction(atm.mie_phase_function_g, nu);
			
				float3 multiple =
					GetScattering(atm,
						_MultipleScattering,
						scattering_size,
						r, mu, mu_s,
						false);

				return float4(_LightColor0 * (rayleigh + mie + multiple), 0.0f);
			}
			ENDCG
		}
	}
}
