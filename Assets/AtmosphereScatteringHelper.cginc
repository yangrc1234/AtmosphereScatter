#define PI 3.1415926

float _RayleighMolecularDensity;
float _AirRefractionIndex;
float _MieG;

static const float3 WaveLength = float3(6.8e-7, 5.5e-7, 4.4e-7);	//10-7 meter

#define EARTH_RADIUS 6.36e7
#define EARTH_CENTER float3(0, -EARTH_RADIUS, 0)
#define ATMOSPHERE_TOP 6e4
#define RAYLEIGH_SCALE_HEIGHT 8e3
#define MIE_SCALE_HEIGHT 1.2e3

#define OPTICAL_DEPTH_STEP_COUNT 8
#define MAIN_SAMPLE_STEP_COUNT 32

// ¦Ñ(h)
float GetAtmDensityAt(float3 pos) {
	float height = length(pos - EARTH_CENTER) - EARTH_RADIUS;
	return exp(-height / RAYLEIGH_SCALE_HEIGHT);
}

float OpticalDepthRange(float3 start, float3 end) {
	float result = 0.0f;
	float3 step = (end - start) / OPTICAL_DEPTH_STEP_COUNT;
	[unroll]
	for (int i = 0; i < OPTICAL_DEPTH_STEP_COUNT; i++) {
		result += GetAtmDensityAt(start + (i + 0.5) * step);
	}
	result *= length(step);
	return result;
}

float RayleighPhaseFunction(float cosTheta) {
	return (3.0 / (16.0 * PI)) * (1 + cosTheta * cosTheta);
}

float3 RayleighScatteringCoefficient() {
	float n_square_minus_1_square = pow(pow(_AirRefractionIndex, 2) - 1, 2); // 3e-7
	float pi_pow3_m8 = 8 * pow(PI, 3);		//2.48e2
	float3 divider = 3 * _RayleighMolecularDensity * 1e25 * pow(WaveLength, 4);	//16.036
	return pi_pow3_m8 * n_square_minus_1_square / divider;	//about 4.6e-6
}

float MiePhaseFunction(float cosTheta) {
	float MieGSquare = _MieG * _MieG;
	float dividend = 3.0 * (1 - MieGSquare) * (1 + cosTheta * cosTheta);
	float divisor = 8 * PI * (2 + MieGSquare) * pow(1 + MieGSquare - 2 * _MieG * cosTheta, 1.5);
	return dividend / divisor;
}

float MieScatteringCoefficient() {
	return 2e-5;
}

//Code from https://area.autodesk.com/blogs/game-dev-blog/volumetric-clouds/.
float2 ray_trace_sphere(float3 center, float3 rd, float3 offset, float radius) {
	float3 p = center - offset;
	float b = dot(p, rd);
	float c = dot(p, p) - (radius * radius);

	float f = b * b - c;
	if (f >= 0.0) {
		float t1 = -b - sqrt(f);
		float t2 = -b + sqrt(f);
		return float2(t1, t2);
	}
	return -1.0;
}
//same as above
void find_atmosphere_intersections(float3 ws_origin, float3 ws_ray, out float3 intersection) {
	float2 inner_solutions = ray_trace_sphere(ws_origin, ws_ray, EARTH_CENTER, EARTH_RADIUS + ATMOSPHERE_TOP);
	float t_inner_sphere = max(inner_solutions.x, inner_solutions.y);

	intersection = ws_origin + ws_ray * t_inner_sphere;
}

float3 SampleColor(float3 startPos, float3 viewDir) {
	float3 intersection;
	find_atmosphere_intersections(startPos, viewDir, intersection);
	float3 step = (intersection - startPos) / MAIN_SAMPLE_STEP_COUNT;
	float ds = length(step);
	float3 aggerated = 0.0;
	float3 rayleiCoefficient = RayleighScatteringCoefficient();
	float cosTheta = dot(viewDir, _WorldSpaceLightPos0.xyz);
	float phase = RayleighPhaseFunction(cosTheta);

	float opticalDepthPA = 0;
	for (int i = 0; i < MAIN_SAMPLE_STEP_COUNT; i++) {
		float3 samplePos = startPos + step * (i + 0.5);
		float density = GetAtmDensityAt(samplePos);
		float opticalDepthSegment = density * ds;
		opticalDepthPA += opticalDepthSegment;

		float3 C;
		find_atmosphere_intersections(samplePos, _WorldSpaceLightPos0.xyz, C);
		float opticalDepthCP = OpticalDepthRange(samplePos, C);
		float3 transimittance = exp(
			-rayleiCoefficient * (opticalDepthPA + opticalDepthCP)
		);
		aggerated += transimittance * opticalDepthSegment;
	}
	return saturate(phase * rayleiCoefficient * aggerated);
}