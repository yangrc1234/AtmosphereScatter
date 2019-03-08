using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Yangrc.AtmosphereScattering {

    public class AtmosphereScatteringLutManager : MonoBehaviour , ProgressiveLutUpdater.ITimeLogger{
        public AtmosphereScatteringLutManager instance {
            get {
                return _instance;
            }
        }
        private AtmosphereScatteringLutManager _instance;

        [SerializeField]
        private ComputeShader computeShader;
        [SerializeField]
        private bool outputDebug = false;
        public AtmLutGenerateConfig lutConfig;
        public AtmosphereConfig atmosphereConfig;
        public Material skyboxMaterial;

        //We prepare 3 updater, the 0-index one is "currently updating", 1-index is "just updated", and 2-index is the oldest one.
        //During rendering, we interpolate 1 and 2, and updating 0.
        private ProgressiveLutUpdater[] pingPongUpdaters = new ProgressiveLutUpdater[3];
        //Shift all updater to one right.
        private void RotatePingpongUpdater() {
            var temp = pingPongUpdaters[pingPongUpdaters.Length-1];
            for (int i = 1; i < pingPongUpdaters.Length; i++) {
                pingPongUpdaters[i] = pingPongUpdaters[i - 1];
            }
            pingPongUpdaters[0] = temp;
        }

        private void Start() {
            if (computeShader == null) {
                throw new System.InvalidOperationException("Compute shader not set!");
            }
            if (_instance != null) {
                throw new System.InvalidOperationException("AtmosphereScatteringLutManager already exists!");
            }
            _instance = this;
            AtmLutHelper.Init(computeShader);
            for (int i = 0; i < pingPongUpdaters.Length; i++) {
                pingPongUpdaters[i] = new ProgressiveLutUpdater(null, lutConfig, this);
            }

            KickOffUpdater(pingPongUpdaters[0]);
        }

        private void KickOffUpdater(ProgressiveLutUpdater updater) {
            updater.atmConfigToUse = atmosphereConfig;
            StartCoroutine(updater.UpdateCoroutine());
        }

        private void Update() {

            if (!pingPongUpdaters[0].working) {
                //Use the finished luts.
                UpdateSkyboxMaterial(pingPongUpdaters[0]);

                //Rotate to right.
                RotatePingpongUpdater();

                //Next updater.
                KickOffUpdater(pingPongUpdaters[0]);
            }
        }

        private void OnDestroy() {
            for (int i = 0; i < pingPongUpdaters.Length; i++) {
                pingPongUpdaters[i].Cleanup();
            }
        }

        public void UpdateSkyboxMaterial(ProgressiveLutUpdater updater) {
            if (this.skyboxMaterial==null)
                this.skyboxMaterial = new Material(Shader.Find("Skybox/AtmosphereScatteringPrecomputed"));
            updater.atmConfigUsedToUpdate.Apply(skyboxMaterial);  
            skyboxMaterial.SetTexture("_SingleRayleigh", updater.singleRayleigh);
            skyboxMaterial.SetTexture("_SingleMie", updater.singleMie);
            skyboxMaterial.SetTexture("_MultipleScattering", updater.multiScatteringCombine);
            skyboxMaterial.SetTexture("_Transmittance", updater.transmittance); 
            skyboxMaterial.SetVector("_ScatteringSize", (Vector3)lutConfig.scatteringSize);
            skyboxMaterial.SetVector("_TransmittanceSize", (Vector2)lutConfig.transmittanceSize);
            RenderSettings.skybox = this.skyboxMaterial;
        }

        public void Log(string itemName) {
            if (outputDebug)
                Debug.Log(itemName);
        }
    }
}