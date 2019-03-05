using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yangrc.AtmosphereScattering {
    [ExecuteInEditMode]
    public class Test : MonoBehaviour {
        [SerializeField]
        private AtmosphereConfig config;

        private AtmosphereScatteringLutManager manager;

        //private void Start() {
        //    if (AtmosphereScatteringLutManager.instance == null) {
        //        AtmosphereScatteringLutManager.Init(config);
        //    }
        //    manager = AtmosphereScatteringLutManager.instance;
        //    manager.UpdateAllAtOnce();
        //}
    }
}
