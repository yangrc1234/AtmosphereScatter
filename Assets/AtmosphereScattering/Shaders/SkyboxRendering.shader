// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Skybox/AtmosphereScatteringPrecomputed"
{
	Properties
	{
		_SingleRayleigh("SingleRayleigh", 3D) = "white" {}
		_SingleMie("SingleMie", 3D) = "white" {}
		_MultipleScattering("MultipleScattering", 3D) = "white"{}
		_Transmittance("Transmittance", 2D) = "white"{}
		_ScatteringSize("ScatteringSize", Vector) = (32.0, 32.0, 128.0, 0.0)
		_TransmittanceSize("TransmittanceSize", Vector) = (512.0, 512.0, 0.0, 0.0)
		_LightScale("LightScale", Float) = 12.0
	}
	SubShader
	{
		// No culling or depth
		Cull Off 
		ZWrite Off 
		Tags{
			"PreviewType" = "Skybox"
		}
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
			sampler2D _Transmittance;
			float3 _ScatteringSize;
			float2 _TransmittanceSize;
			float _LightScale;

			RadianceSpectrum GetScattering(
				IN(AtmosphereParameters) atmosphere,
				IN(ScatteringTexture) scattering_texture,
				uint3 texture_size,
				Length r, Number mu, Number mu_s, bool ray_r_mu_intersects_ground
			);

			void ComputeSingleScattering(
				IN(AtmosphereParameters) atmosphere,
				IN(TransmittanceTexture) transmittance_texture,
				uint2 texture_size,
				Length r, Number mu, Number mu_s, Number nu,
				bool ray_r_mu_intersects_ground,
				OUT(IrradianceSpectrum) rayleigh, OUT(IrradianceSpectrum) mie);

			half4 frag (v2f i) : SV_Target
			{
				i.worldPos /= i.worldPos.w;
				float3 view_ray = normalize(i.worldPos.xyz - _WorldSpaceCameraPos);
				float3 sun_direction = normalize(_WorldSpaceLightPos0.xyz);
				AtmosphereParameters atm = GetAtmParameters();
				float3 camera = _WorldSpaceCameraPos + float3(0, atm.bottom_radius, 0);
				float r = length(camera);
				Length rmu = dot(camera, view_ray);

				Length distance_to_top_atmosphere_boundary = -rmu -
					sqrt(rmu * rmu - r * r + atm.top_radius * atm.top_radius);

				if (distance_to_top_atmosphere_boundary > 0.0 ) {
					camera = camera + view_ray * distance_to_top_atmosphere_boundary;
					r = atm.top_radius;
					rmu += distance_to_top_atmosphere_boundary;
				}
				else if (r > atm.top_radius) {
					// If the view ray does not intersect the atmosphere, simply return 0.
					return 0.0f;
				}

				// Compute the r, mu, mu_s and nu parameters needed for the texture lookups.
				Number mu = rmu / r;
				Number mu_s = dot(camera, sun_direction) / r;
				Number nu = dot(view_ray, sun_direction);
				bool ray_r_mu_intersects_ground = RayIntersectsGround(atm, r, mu);

				float3 testRayleigh, testMie;
				//ComputeSingleScattering(
				//	atm,
				//	_Transmittance,
				//	_TransmittanceSize,
				//	r,
				//	mu,
				//	mu_s,
				//	nu,
				//	ray_r_mu_intersects_ground,
				//	testRayleigh, testMie);
				//return float4(testRayleigh + testMie, 1.0f);

				float3 transmittance = ray_r_mu_intersects_ground ? 0.0f :
					GetTransmittanceToTopAtmosphereBoundary(
						atm, _Transmittance, _TransmittanceSize, r, mu);

				float3 direct_sun_strength = 0.0f;
				{
					float cos_sunedge = cos(atm.sun_angular_radius);
					if (nu > cos_sunedge) {
						direct_sun_strength = transmittance * (nu - cos_sunedge) / (1.0f - cos_sunedge);
					}
				}

				float3 rayleigh =
					GetScattering(atm,
						_SingleRayleigh,
						_ScatteringSize,
						r, mu, mu_s,
						ray_r_mu_intersects_ground) *
					RayleighPhaseFunction(nu);

				float3 mie = 
					GetScattering(atm,
						_SingleMie,
						_ScatteringSize,
						r, mu, mu_s,
						ray_r_mu_intersects_ground) *
					MiePhaseFunction(atm.mie_phase_function_g, nu);
			
				float3 multiple =
					GetScattering(atm,
						_MultipleScattering,
						_ScatteringSize,
						r, mu, mu_s,
						ray_r_mu_intersects_ground);

				return float4(_LightColor0.rgb * _LightScale * (direct_sun_strength + rayleigh + mie + multiple), 0.0f);
			}
			ENDCG
		}
	}
}
