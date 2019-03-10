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
            for (int i = pingPongUpdaters.Length - 1; i > 0; i--) {
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
                pingPongUpdaters[i].name = "Updater " + i;
            }

            //Quickly complete two set luts.
            for (int i = 1; i <= 2; i++) {
                pingPongUpdaters[i].atmConfigToUse = atmosphereConfig;
                var t = pingPongUpdaters[i].UpdateCoroutine();
                while (t.MoveNext()) ;
            }
            UpdateSkyboxMaterial(pingPongUpdaters[1], pingPongUpdaters[2]);

            KickOffUpdater(pingPongUpdaters[0]);
        }

        private void KickOffUpdater(ProgressiveLutUpdater updater) {
            updater.atmConfigToUse = atmosphereConfig;
            StartCoroutine(updater.UpdateCoroutine());
        }

        private float lerpValue = 0.0f;
        private void Update() {
            if (!pingPongUpdaters[0].working) {
                //Use the finished luts.
                UpdateSkyboxMaterial(pingPongUpdaters[0], pingPongUpdaters[1]);

                lerpValue = 0.0f;

                //Rotate to right.
                RotatePingpongUpdater();

                //Next updater.
                KickOffUpdater(pingPongUpdaters[0]);
            }
            UpdateLerpValue();
        }

        private void UpdateLerpValue() {
            //We now require 19 frames to update
            
            lerpValue += 1.0f / 19.0f;
            if (skyboxMaterial != null)
                skyboxMaterial.SetFloat("_LerpValue", lerpValue);
        }

        private void OnDestroy() {
            for (int i = 0; i < pingPongUpdaters.Length; i++) {
                pingPongUpdaters[i].Cleanup();
            }
        }

        public void UpdateSkyboxMaterial(ProgressiveLutUpdater updater, ProgressiveLutUpdater oldUpdater) {
            if (this.skyboxMaterial==null)
                this.skyboxMaterial = new Material(Shader.Find("Skybox/AtmosphereScatteringPrecomputed"));
            updater.atmConfigUsedToUpdate.Apply(skyboxMaterial);
            skyboxMaterial.SetTexture("_SingleRayleigh_1", oldUpdater.singleRayleigh);
            skyboxMaterial.SetTexture("_SingleMie_1", oldUpdater.singleMie);
            skyboxMaterial.SetTexture("_SingleRayleigh_2", updater.singleRayleigh);
            skyboxMaterial.SetTexture("_SingleMie_2", updater.singleMie);
            skyboxMaterial.SetTexture("_MultipleScattering_1", oldUpdater.multiScatteringCombine);
            skyboxMaterial.SetTexture("_MultipleScattering_2", updater.multiScatteringCombine);
            skyboxMaterial.SetTexture("_Transmittance_1", oldUpdater.transmittance);
            skyboxMaterial.SetTexture("_Transmittance_2", updater.transmittance);
            skyboxMaterial.SetTexture("_GroundIrradiance_1", oldUpdater.groundIrradianceCombine);
            skyboxMaterial.SetTexture("_GroundIrradiance_2", updater.groundIrradianceCombine);
            skyboxMaterial.SetVector("_ScatteringSize", (Vector3)lutConfig.scatteringSize);
            skyboxMaterial.SetVector("_GroundIrradianceSize", (Vector2)lutConfig.irradianceSize);
            skyboxMaterial.SetVector("_TransmittanceSize", (Vector2)lutConfig.transmittanceSize);
            RenderSettings.skybox = this.skyboxMaterial;
        }

        public void Log(string itemName) {
            if (outputDebug)
                Debug.Log(itemName);
        }
    }
}