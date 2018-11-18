#ifndef __PRAGUE_HELPER__
#define __PRAGUE_HELPER__
#include "Common.cginc"

vec3 GetScatteringUvwFromRMuMus(IN(AtmosphereParameters) atmosphere,
	Length r, Number mu, Number mu_s int WIDTH, int HEIGHT, int DEPTH) {
	Number height = r - atmosphere.bot_radius;
	Number u = SafeSqrt(
		(r*r - atmosphere.bot_radius * atmosphere.bot_radius) / (atmosphere.top_radius * atmosphere.top_radius - atmosphere.bot_radius * atmosphere.bot_radius)
	);
	Number v = (1 + mu) / 2;
	Number w = (1 - exp(-2.8 * mu_s - 0.8)) / (1 - exp(-3.6));

	return vec3(
		GetTextureCoordFromUnitRange(u, WIDTH),
		GetTextureCoordFromUnitRange(v, HEIGHT),
		GetTextureCoordFromUnitRange(w, DEPTH),
		);
}

vec3 GetScatteringRMuMusFromUvw(IN(AtmosphereParameters) atmosphere,
	IN(vec3) uvw, OUT(Length) r, OUT(Number) mu, OUT(Number) mu_s int WIDTH, int HEIGHT, int DEPTH) {

	Number x_r = GetUnitRangeFromTextureCoord(uvw.x, HEIGHT);
	Number x_mu = GetUnitRangeFromTextureCoord(uvw.y, WIDTH);
	Number x_mu_s = GetUnitRangeFromTextureCoord(uvw.z, DEPTH);


}

void GetRMuFromTransmittanceTextureUv(IN(AtmosphereParameters) atmosphere,
	IN(vec2) uv, OUT(Length) r, OUT(Number) mu, int TRANSMITTANCE_TEXTURE_WIDTH, int TRANSMITTANCE_TEXTURE_HEIGHT) {
	assert(uv.x >= 0.0 && uv.x <= 1.0);
	assert(uv.y >= 0.0 && uv.y <= 1.0);
	Number x_mu = GetUnitRangeFromTextureCoord(uv.x, TRANSMITTANCE_TEXTURE_WIDTH);
	Number x_r = GetUnitRangeFromTextureCoord(uv.y, TRANSMITTANCE_TEXTURE_HEIGHT);
	// Distance to top atmosphere boundary for a horizontal ray at ground level.
	Length H = sqrt(atmosphere.top_radius * atmosphere.top_radius -
		atmosphere.bottom_radius * atmosphere.bottom_radius);
	// Distance to the horizon, from which we can compute r:
	Length rho = H * x_r;
	r = sqrt(rho * rho + atmosphere.bottom_radius * atmosphere.bottom_radius);
	// Distance to the top atmosphere boundary for the ray (r,mu), and its minimum
	// and maximum values over all mu - obtained for (r,1) and (r,mu_horizon) -
	// from which we can recover mu:
	Length d_min = atmosphere.top_radius - r;
	Length d_max = rho + H;
	Length d = d_min + x_mu * (d_max - d_min);
	mu = d == 0.0 ? Number(1.0) : (H * H - rho * rho - d * d) / (2.0 * r * d);
	mu = ClampCosine(mu);
}

DimensionlessSpectrum GetTransmittanceToTopAtmosphereBoundary(
	IN(AtmosphereParameters) atmosphere,
	IN(TransmittanceTexture) transmittance_texture,
	Length r, Number mu) {
	assert(r >= atmosphere.bottom_radius && r <= atmosphere.top_radius);

	int width, height;
	transmittance_texture.GetDimensions(width, height);
	vec2 uv = GetTransmittanceTextureUvFromRMu(atmosphere, r, mu, width, height);
	return DimensionlessSpectrum(texture(transmittance_texture, uv));
}

DimensionlessSpectrum GetTransmittanceToSun(
	IN(AtmosphereParameters) atmosphere,
	IN(TransmittanceTexture) transmittance_texture,
	Length r, Number mu_s) {
	Number sin_theta_h = atmosphere.bottom_radius / r;
	Number cos_theta_h = -sqrt(max(1.0 - sin_theta_h * sin_theta_h, 0.0));
	return GetTransmittanceToTopAtmosphereBoundary(
		atmosphere, transmittance_texture, r, mu_s) *
		smoothstep(-sin_theta_h * atmosphere.sun_angular_radius / rad,
			sin_theta_h * atmosphere.sun_angular_radius / rad,
			mu_s - cos_theta_h);
}

Length ComputeOpticalLengthToTopAtmosphereBoundary(
	IN(AtmosphereParameters) atmosphere, Length r, Number mu, Length scale_height) {
	assert(r >= atmosphere.bottom_radius && r <= atmosphere.top_radius);
	assert(mu >= -1.0 && mu <= 1.0);
	// Number of intervals for the numerical integration.
	const int SAMPLE_COUNT = 500;
	// The integration step, i.e. the length of each integration interval.
	Length dx =
		DistanceToTopAtmosphereBoundary(atmosphere, r, mu) / Number(SAMPLE_COUNT);
	// Integration loop.
	Length result = 0.0;
	for (int i = 0; i <= SAMPLE_COUNT; ++i) {
		Length d_i = Number(i) * dx;
		// Distance between the current sample point and the planet center.
		Length r_i = sqrt(d_i * d_i + 2.0 * r * mu * d_i + r * r);
		// Number density at the current sample point (divided by the number density
		// at the bottom of the atmosphere, yielding a dimensionless number).
		Number y_i = GetScaleHeight(r_i - atmosphere.bottom_radius, scale_height);
		// Sample weight (from the trapezoidal rule).
		Number weight_i = i == 0 || i == SAMPLE_COUNT ? 0.5 : 1.0;
		result += y_i * weight_i * dx;
	}
	return result;
}

DimensionlessSpectrum ComputeTransmittanceToTopAtmosphereBoundary(
	IN(AtmosphereParameters) atmosphere, Length r, Number mu) {
	assert(r >= atmosphere.bottom_radius && r <= atmosphere.top_radius);
	assert(mu >= -1.0 && mu <= 1.0);
	return exp(-(
		atmosphere.rayleigh_scattering *
		ComputeOpticalLengthToTopAtmosphereBoundary(
			atmosphere, r, mu, atmosphere.rayleigh_scale_height) +
		atmosphere.mie_extinction *
		ComputeOpticalLengthToTopAtmosphereBoundary(
			atmosphere, r, mu, atmosphere.mie_scale_height) +
		atmosphere.absorption_extinction *
		ComputeOpticalLengthToTopAtmosphereBoundary(
			atmosphere, r, mu, atmosphere.absorption_extinction_scale_height)
		)
	);
	//return exp(-(
	//	atmosphere.rayleigh_scattering *
	//	ComputeOpticalLengthToTopAtmosphereBoundary(
	//		atmosphere, r, mu, atmosphere.rayleigh_scale_height)
	//));
}

DimensionlessSpectrum GetTransmittance(
	IN(AtmosphereParameters) atmosphere,
	IN(TransmittanceTexture) transmittance_texture,
	Length r, Number mu, Length d, bool ray_r_mu_intersects_ground) {
	assert(r >= atmosphere.bottom_radius && r <= atmosphere.top_radius);
	assert(mu >= -1.0 && mu <= 1.0);
	assert(d >= 0.0 * m);

	Length r_d = ClampRadius(atmosphere, sqrt(d * d + 2.0 * r * mu * d + r * r));
	Number mu_d = ClampCosine((r * mu + d) / r_d);

	if (ray_r_mu_intersects_ground) {
		return min(
			GetTransmittanceToTopAtmosphereBoundary(
				atmosphere, transmittance_texture, r_d, -mu_d) /
			GetTransmittanceToTopAtmosphereBoundary(
				atmosphere, transmittance_texture, r, -mu),
			DimensionlessSpectrum(1.0));
	}
	else {
		return min(
			GetTransmittanceToTopAtmosphereBoundary(
				atmosphere, transmittance_texture, r, mu) /
			GetTransmittanceToTopAtmosphereBoundary(
				atmosphere, transmittance_texture, r_d, mu_d),
			DimensionlessSpectrum(1.0));
	}

#endif