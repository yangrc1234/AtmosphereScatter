Shader "Hidden/Yangrc/AerialPerspective"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
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
			#include "AerialPerspectiveHelper.cginc"
			float4 _ProjectionExtents;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float4 scrPos: TEXCOORD1;
				float2 vsray : TEXCOORD2;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.scrPos = ComputeScreenPos(o.vertex);
				o.vsray = (2.0 * v.uv - 1.0) * _ProjectionExtents.xy + _ProjectionExtents.zw;
				return o;
			}
			
			sampler2D _MainTex;
			sampler2D _CameraDepthTexture;
			float _LightScale;

			void CalculateRMuMusForDistancePoint(Length r, Number mu, Number mu_s, Number nu, Number d, OUT(Length) r_d, OUT(Number) mu_d, OUT(Number) mu_s_d);
			void CalculateRMuMusFromPosViewdir(AtmosphereParameters atm, float3 pos, float3 view_ray, float3 sun_direction, OUT(float) mu, OUT(float) mu_s, OUT(float) nu);
			float3 GetTransmittanceToTopAtmosphereBoundaryLerped(float r, float mu);

			half4 frag (v2f i) : SV_Target
			{ 
				float3 vspos = float3(i.vsray, 1.0);
				float4 worldPos = mul(unity_CameraToWorld, float4(vspos, 1.0));
				worldPos /= worldPos.w;

				half4 original = tex2D(_MainTex, i.uv);

				AtmosphereParameters atm = GetAtmParameters();
				float3 view_ray = normalize(worldPos.xyz - _WorldSpaceCameraPos);
				float distance = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, i.scrPos));
				if (distance > _ProjectionParams.z - 1.0f) {
					return original;
				}

				float r, mu, mu_s, nu;
				float r_d, mu_d, mu_s_d;	//Current pixel on screen's info
				CalculateRMuMusFromPosViewdir(atm, _WorldSpaceCameraPos, view_ray, _WorldSpaceLightPos0, r, mu, mu_s, nu);
				CalculateRMuMusForDistancePoint(r, mu, mu_s, nu, distance, r_d, mu_d, mu_s_d);

				bool ray_r_mu_intersects_ground = RayIntersectsGround(atm, r, mu);
				//Transmittance to target point.
				float3 transmittanceToTarget = GetTransmittanceLerped(r, mu, distance, ray_r_mu_intersects_ground);
				float3 scatteringBetween =
					GetTotalScatteringLerped(r, mu, mu_s, nu)
					- GetTotalScatteringLerped(r_d, mu_d, mu_s_d, nu) * transmittanceToTarget;
				scatteringBetween *= _LightScale;
				return half4(original * transmittanceToTarget + scatteringBetween, 1.0);
			}
			ENDCG
		}
	}
}
