using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yangrc.AtmosphereScattering {
    [CreateAssetMenu]
    public class AtmosphereConfig : ScriptableObject {

        Vector3 OZoneConst = 6e-7f * new Vector3(3.426f, 8.298f, 0.356f);
        Vector3 RayleighScatteringConst = 1e-6f * new Vector3(5.8f, 13.5f, 33.1f);
        float MieScatteringConst = 2e-6f;

        private static class Keys {
            public static int atmosphere_top_radius = Shader.PropertyToID("atmosphere_top_radius");
            public static int atmosphere_bot_radius = Shader.PropertyToID("atmosphere_bot_radius");
            public static int atmosphere_sun_angular_radius = Shader.PropertyToID("atmosphere_sun_angular_radius");
            public static int rayleigh_scattering = Shader.PropertyToID("rayleigh_scattering");
            public static int rayleigh_scale_height = Shader.PropertyToID("rayleigh_scale_height");
            public static int mie_scattering = Shader.PropertyToID("mie_scattering");
            public static int mie_extinction = Shader.PropertyToID("mie_extinction");
            public static int mie_scale_height = Shader.PropertyToID("mie_scale_height");
            public static int mie_phase_function_g = Shader.PropertyToID("mie_phase_function_g");
            public static int absorption_extinction = Shader.PropertyToID("absorption_extinction");
            public static int absorption_extinction_scale_height = Shader.PropertyToID("absorption_extinction_scale_height");
            public static int lightingScale = Shader.PropertyToID("_LightScale");
        }

        public void Apply(Material mat) {
            mat.SetFloat(Keys.atmosphere_top_radius, atmosphere_top_radius);
            mat.SetFloat(Keys.atmosphere_bot_radius, atmosphere_bot_radius);
            mat.SetFloat(Keys.atmosphere_sun_angular_radius, atmosphere_sun_angular_radius);
            mat.SetVector(Keys.rayleigh_scattering, AtmosphereDensity  * rayleigh_scattering * RayleighScatteringConst);
            mat.SetFloat(Keys.rayleigh_scale_height, rayleigh_scale_height);
            mat.SetFloat(Keys.mie_scattering, AtmosphereDensity  * mie_scattering * MieScatteringConst);
            mat.SetFloat(Keys.mie_extinction, AtmosphereDensity  * mie_scattering * 1.1f * MieScatteringConst);
            mat.SetFloat(Keys.mie_scale_height, mie_scale_height);
            mat.SetFloat(Keys.mie_phase_function_g, mie_phase_function_g);
            mat.SetVector(Keys.absorption_extinction, AtmosphereDensity * absorption_extinction * OZoneConst);
            mat.SetFloat(Keys.absorption_extinction_scale_height, absorption_extinction_scale_height);

            mat.SetFloat(Keys.lightingScale, LightingScale);

        }

        public void Apply(ComputeShader shader) {
            shader.SetFloat(Keys.atmosphere_top_radius, atmosphere_top_radius);
            shader.SetFloat(Keys.atmosphere_bot_radius, atmosphere_bot_radius);
            shader.SetFloat(Keys.atmosphere_sun_angular_radius, atmosphere_sun_angular_radius);
            shader.SetVector(Keys.rayleigh_scattering, AtmosphereDensity  * rayleigh_scattering * RayleighScatteringConst);
            shader.SetFloat(Keys.rayleigh_scale_height, rayleigh_scale_height);
            shader.SetFloat(Keys.mie_scattering, AtmosphereDensity * mie_scattering * MieScatteringConst);
            shader.SetFloat(Keys.mie_extinction, AtmosphereDensity * mie_scattering * 1.1f * MieScatteringConst);
            shader.SetFloat(Keys.mie_scale_height, mie_scale_height);
            shader.SetFloat(Keys.mie_phase_function_g, mie_phase_function_g);
            shader.SetVector(Keys.absorption_extinction, AtmosphereDensity * absorption_extinction * OZoneConst);
            shader.SetFloat(Keys.absorption_extinction_scale_height, absorption_extinction_scale_height);
        }

        public float AtmosphereDensity = 1.0f;
        public float LightingScale = 2.0f * 3.1415926f;
        public float atmosphere_top_radius = 6.36e7f + 6e4f;
        public float atmosphere_bot_radius = 6.36e7f;
        public float atmosphere_sun_angular_radius = 5.0f;
        public float rayleigh_scattering = 1.0f;
        public float rayleigh_scale_height = 8000.0f;
        public float mie_scattering = 1.0f;
        public float mie_scale_height = 1200.0f;
        public float mie_phase_function_g = 0.95f;
        public float absorption_extinction = 1.0f;
        public float absorption_extinction_scale_height = 8000.0f;
    }
}
