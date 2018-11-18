using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yangrc.AtmosphereScattering {
    [CreateAssetMenu]
    public class AtmosphereConfig : ScriptableObject {

        public void Apply(ComputeShader shader) {
            shader.SetFloat("atmosphere_top_radius", atmosphere_top_radius);
            shader.SetFloat("atmosphere_bot_radius", atmosphere_bot_radius);
            shader.SetFloat("atmosphere_sun_angular_radius", atmosphere_sun_angular_radius);
            shader.SetVector("rayleigh_scattering", rayleigh_scattering * 1e-6f);
            shader.SetFloat("rayleigh_scale_height", rayleigh_scale_height);
            shader.SetFloat("mie_extinction", mie_extinction * 1e-6f);
            shader.SetFloat("mie_scale_height", mie_scale_height);
            shader.SetFloat("absorption_extinction", absorption_extinction);
            shader.SetFloat("absorption_extinction_scale_height", absorption_extinction_scale_height);
        }

        public float atmosphere_top_radius = 6.36e7f + 6e4f;
        public float atmosphere_bot_radius = 6.36e7f;
        public float atmosphere_sun_angular_radius = 5.0f;
        public Vector3 rayleigh_scattering = new Vector3(4.6f, 8.0f, 12.0f);
        public float rayleigh_scale_height = 8000.0f;
        public float mie_extinction = 1.0f;
        public float mie_scale_height = 1200.0f;
        public float absorption_extinction = 0.0f;
        public float absorption_extinction_scale_height = 1000.0f;
    }
}
