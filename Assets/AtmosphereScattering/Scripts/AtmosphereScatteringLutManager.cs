using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Yangrc.AtmosphereScattering {

    public class AtmosphereScatteringLutManager : MonoBehaviour , ProgressiveLutUpdater.ITimeLogger{
        public bool outputDebug  = false;
        public bool forceRefresh;
        public AtmosphereScatteringLutManager instance {
            get {
                return _instance;
            }
        }
        private AtmosphereScatteringLutManager _instance;

        [SerializeField]
        private ComputeShader computeShader;
        private ProgressiveLutUpdater updater;
        public AtmLutGenerateConfig lutConfig;
        public AtmosphereConfig atmosphereConfig;
        public Material skyboxMaterial;

        private void Awake() {
            if (computeShader == null) {
                throw new System.InvalidOperationException("Compute shader not set!");
            }
            if (_instance != null) {
                throw new System.InvalidOperationException("AtmosphereScatteringLutManager already exists!");
            }
            _instance = this;
            AtmLutHelper.Init(computeShader);
            updater = new ProgressiveLutUpdater(atmosphereConfig, lutConfig, this);
            StartCoroutine(updater.UpdateRoutine());
        }

        private void Update() {
            if (updater.isDone) {
                if (forceRefresh) {
                    //forceRefresh = false;
                    StartCoroutine(updater.UpdateRoutine());
                }
                UpdateSkyboxMaterial();
            }
        }

        public void UpdateSkyboxMaterial() {
            if (this.skyboxMaterial==null)
                this.skyboxMaterial = new Material(Shader.Find("Skybox/AtmosphereScatteringPrecomputed"));
            atmosphereConfig.Apply(skyboxMaterial);
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