using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yangrc.AtmosphereScattering {
    [RequireComponent(typeof(Light))]
    public class SunIntensityController : MonoBehaviour {

        private new Light light;

        private void Awake() {
            light = GetComponent<Light>();
        }

        // Update is called once per frame
        void Update() {
            var t = AtmosphereScatteringLutManager.instance;
            if (t != null) {
                var mu_s = Vector3.Dot(Vector3.down, transform.forward);
                var radianceAtZero = t.GetRadianceAtPosZero(mu_s);

                light.intensity = radianceAtZero.magnitude / (2 * Mathf.PI);
                var normalized = radianceAtZero.normalized;
                light.color = new Color(normalized.x, normalized.y, normalized.z);
            }
        }
    }
}
