#ifndef __AERIAL_PERSPECTIVE_HELPER__
#define __AERIAL_PERSPECTIVE_HELPER__

#include "Common.cginc"
#include "TransmittanceHelper.cginc"

#define LERP_TEXTURE(dim, name) \
sampler ## dim ## D name ## _1; \
sampler ## dim ## D name ## _2; 

LERP_TEXTURE(2, _Transmittance)
LERP_TEXTURE(3, _SingleRayleigh)
LERP_TEXTURE(3, _SingleMie)
LERP_TEXTURE(3, _MultipleScattering)

float2 _TransmittanceSize;
float3 _ScatteringSize;

uniform float _LerpValue;

float3 GetTransmittanceToTopAtmosphereBoundaryLerped(float r, float mu) {
	float3 lerp1 = GetTransmittanceToTopAtmosphereBoundary(
		GetAtmParameters(), _Transmittance_1, _TransmittanceSize, r, mu);
	float3 lerp2 = GetTransmittanceToTopAtmosphereBoundary(
		GetAtmParameters(), _Transmittance_2, _TransmittanceSize, r, mu);

	return lerp(lerp1, lerp2, _LerpValue);
}

float3 InternalGetRayleighLerped(AtmosphereParameters atm, float r, float mu, float mu_s, float nu, bool ray_r_mu_intersects_ground) {

	return  GetScatteringLerped(atm,
			_SingleRayleigh_1,
			_SingleRayleigh_2,
			_LerpValue,
			_ScatteringSize,
			r, mu, mu_s,
			ray_r_mu_intersects_ground) *
		RayleighPhaseFunction(nu);
}

float3 InternalGetMieLerped(AtmosphereParameters atm, float r, float mu, float mu_s, float nu, bool ray_r_mu_intersects_ground) {
	return GetScatteringLerped(atm,
			_SingleMie_1,
			_SingleMie_2,
			_LerpValue,
			_ScatteringSize,
			r, mu, mu_s,
			ray_r_mu_intersects_ground) *
		MiePhaseFunction(atm.mie_phase_function_g, nu);
}

float3 InternalGetMultipleLerped(AtmosphereParameters atm, float r, float mu, float mu_s, float nu, bool ray_r_mu_intersects_ground) {
	return
		GetScatteringLerped(atm,
			_MultipleScattering_1,
			_MultipleScattering_2,
			_LerpValue,
			_ScatteringSize,
			r, mu, mu_s,
			ray_r_mu_intersects_ground);
	
}

float3 GetTotalScatteringLerped(float r, float mu, float mu_s, float nu) {
	AtmosphereParameters atm = GetAtmParameters();
	bool ray_r_mu_intersects_ground = RayIntersectsGround(atm, r, mu);

	return
		InternalGetRayleighLerped(atm, r, mu, mu_s, nu, ray_r_mu_intersects_ground)
		+ InternalGetMieLerped(atm, r, mu, mu_s, nu, ray_r_mu_intersects_ground)
		+ InternalGetMultipleLerped(atm, r, mu, mu_s, nu, ray_r_mu_intersects_ground);
}
#endif