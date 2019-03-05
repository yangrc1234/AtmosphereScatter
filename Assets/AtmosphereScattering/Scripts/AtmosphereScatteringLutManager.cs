using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Yangrc.AtmosphereScattering {

    [System.Serializable]
    public class AtmLutGenerateConfig {
        public Vector2Int transmittanceSize = new Vector2Int(512, 512);
        public Vector3Int scatteringSize = new Vector3Int(32, 32, 128);
        public Vector2Int irradianceSize = new Vector2Int(32, 32);
        private static class Keys {
            private static readonly int transmittanceSize = Shader.PropertyToID("TransmittanceSize");
            private static readonly int scatteringSize = Shader.PropertyToID("ScatteringSize");
            private static readonly int irradianceSize = Shader.PropertyToID("IrradianceSize");
        }
        public void Apply(ComputeShader shader) {
            shader.SetInts("TransmittanceSize", transmittanceSize.x, transmittanceSize.y);
            shader.SetInts("ScatteringSize", scatteringSize.x, scatteringSize.y, scatteringSize.z);
            shader.SetInts("IrradianceSize", irradianceSize.x, irradianceSize.y);
        }
    }

    public static class AtmLutHelper {

        public static void CreateTransmittanceTexture(ref RenderTexture target, AtmLutGenerateConfig lutconfig) {
            CreateLUT(ref target, "Transmittance", lutconfig.transmittanceSize.x, lutconfig.transmittanceSize.y, 0, RenderTextureFormat.ARGBFloat);
        }

        private static void NormalizeProgressPointer(float start, float end, int length, out int startInt, out int endInt) {
            if (end > start) {
                var t = start;
                start = end;
                end = t;
            }
            start = Mathf.Clamp01(start);
            end = Mathf.Clamp01(end);
            startInt = Mathf.FloorToInt(length * start);
            endInt = Mathf.FloorToInt(length * end);
        }

        public static void UpdateTransmittance(RenderTexture target, AtmosphereConfig atmosphereConfig, AtmLutGenerateConfig lutConfig, ComputeShader computeShader, float start, float end) {
            var kernal = computeShader.FindKernel("CalculateTransmittanceLUT");
            atmosphereConfig.Apply(computeShader);
            lutConfig.Apply(computeShader);
            int xStart, xEnd;
            NormalizeProgressPointer(start, end, lutConfig.transmittanceSize.x, out xStart, out xEnd);
            computeShader.SetInts("_ThreadOffset", xStart, 0, 0);
            computeShader.SetTexture(kernal, "TransmittanceLUTResult", target);
            computeShader.Dispatch(kernal, xEnd - xStart, lutConfig.transmittanceSize.y, 1);
        }

        private static void CreateLUT(ref RenderTexture result, string name, int width, int height, int zsize, RenderTextureFormat format) {
            if (result != null)
                result.Release();
            result = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear);
            result.name = name;
            result.enableRandomWrite = true;
            if (zsize > 0) {
                result.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                result.volumeDepth = zsize;
            } else {
                result.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            }
            result.filterMode = FilterMode.Bilinear;
            result.wrapMode = TextureWrapMode.Clamp;
            result.Create();
        }

        public static void CreateSingleRayleighMieTexture(ref RenderTexture rayleigh, ref RenderTexture mie, AtmLutGenerateConfig lutconfig) {
            CreateLUT(ref rayleigh, "SingleMie", lutconfig.scatteringSize.x, lutconfig.scatteringSize.y, lutconfig.scatteringSize.z, RenderTextureFormat.ARGBFloat);
            CreateLUT(ref mie, "SingleRayleigh", lutconfig.scatteringSize.x, lutconfig.scatteringSize.y, lutconfig.scatteringSize.z, RenderTextureFormat.ARGBFloat);
        }

        public static void UpdateSingleRayleighMie(RenderTexture rayleightarget, RenderTexture mietarget, RenderTexture TransmittanceLUT, AtmosphereConfig atmosphereConfig, AtmLutGenerateConfig lutConfig, ComputeShader computeShader, float start, float end) {

            var kernal = computeShader.FindKernel("CalculateSingleScatteringLUT");
            atmosphereConfig.Apply(computeShader);
            lutConfig.Apply(computeShader);

            int xStart, xEnd;
            NormalizeProgressPointer(start, end, lutConfig.scatteringSize.x, out xStart, out xEnd);

            computeShader.SetInts("_ThreadOffset", xStart, 0, 0);
            computeShader.SetTexture(kernal, "TransmittanceLUT", TransmittanceLUT);
            computeShader.SetTexture(kernal, "SingleScatteringMieLUTResult", mietarget);
            computeShader.SetTexture(kernal, "SingleScatteringRayleighLUTResult", rayleightarget);

            computeShader.Dispatch(kernal, xEnd - xStart, lutConfig.scatteringSize.y, lutConfig.scatteringSize.z);
        }


    }

    public class AtmLutUpdater {
        private Vector2Int transmittanceSize;
        private Vector2Int scatteringSize;

        public AtmLutUpdater(Vector2Int transmittanceSize, Vector2Int scatteringSize) {
            this.transmittanceSize = transmittanceSize;
            this.scatteringSize = scatteringSize;
        }

        public class TransmittanceUpdater {

        }
    }

    public class AtmosphereScatteringLutManager : MonoBehaviour {
        public bool autoInit = true;
        public AtmosphereScatteringLutManager instance {
            get {
                return _instance;
            }
        }
        private AtmosphereScatteringLutManager _instance;

        [SerializeField]
        private ComputeShader computeShader;

        private void Awake() {
            this.config = config;
            if (computeShader == null) {
                throw new System.InvalidOperationException("Compute shader not set!");
            }
            if (_instance != null) {
                throw new System.InvalidOperationException("AtmosphereScatteringLutManager already exists!");
            }
            _instance = this;
        }

        private void Update() {
            UpdateSkyboxMaterial();
        }

        private void Start() {
            if (autoInit) {
                UpdateAllAtOnce();
            }
        }

        public AtmosphereConfig config;
        public Material skyboxMaterial;

        private RenderTexture TransmittanceLUT;
        private RenderTexture SingleScatteringLUTRayleigh;
        private RenderTexture SingleScatteringLUTMie;
        private RenderTexture MultipleScatteringLUT;
        private RenderTexture IrradianceLUT;

        public Vector2Int transmittanceSize = new Vector2Int(512, 512);
        public Vector3Int scatteringSize = new Vector3Int(32, 32, 128);
        public Vector2Int irradianceSize = new Vector2Int(32, 32);

        private void SetupComputeShaderCommonParameters() {
            config.Apply(computeShader);
            computeShader.SetInts("TransmittanceSize", transmittanceSize.x, transmittanceSize.y);
            computeShader.SetInts("ScatteringSize", scatteringSize.x, scatteringSize.y, scatteringSize.z);
            computeShader.SetInts("IrradianceSize", irradianceSize.x, irradianceSize.y);
        }

        private void SetupAllLuts() {
            CreateLUT(ref TransmittanceLUT, "Transmittance", transmittanceSize.x, transmittanceSize.y, 0, RenderTextureFormat.ARGBFloat);
            CreateLUT(ref SingleScatteringLUTMie, "SingleMie", scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBFloat);
            CreateLUT(ref SingleScatteringLUTRayleigh, "SingleRayleigh", scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBFloat);
        }

        /// <summary>
        /// Update transmittance texture.
        /// </summary>
        public void UpdateTransmittanceLUT(float partUpdateStart = 0.0f, float partUpdateEnd = 1.0f) {
            var kernal = computeShader.FindKernel("CalculateTransmittanceLUT");
            SetupComputeShaderCommonParameters();
            var xStart = Mathf.FloorToInt(transmittanceSize.x * partUpdateStart);
            var xEnd = Mathf.FloorToInt(transmittanceSize.x * partUpdateEnd);
            computeShader.SetInts("_ThreadOffset", xStart, 0, 0);
            computeShader.SetTexture(kernal, "TransmittanceLUTResult", TransmittanceLUT);
            computeShader.Dispatch(kernal, transmittanceSize.x, transmittanceSize.y, 1);
        }

        public void UpdateSingleScatteringLUT() {

            var kernal = computeShader.FindKernel("CalculateSingleScatteringLUT");
            SetupComputeShaderCommonParameters();

            computeShader.SetTexture(kernal, "TransmittanceLUT", TransmittanceLUT);
            computeShader.SetTexture(kernal, "SingleScatteringMieLUTResult", SingleScatteringLUTMie);
            computeShader.SetTexture(kernal, "SingleScatteringRayleighLUTResult", SingleScatteringLUTRayleigh);

            computeShader.Dispatch(kernal, scatteringSize.x, scatteringSize.y, scatteringSize.z);
        }

        public void UpdateMultipleScattering(int MultipleScatteringIterations = 3) {
            RenderTexture[] groundIrrdiance = new RenderTexture[10];
            RenderTexture[] multipleScattering = new RenderTexture[10];
            RenderTexture multipleScatteringDensity = null;

            SetupComputeShaderCommonParameters();

            //Create tons of rt to store intermediate results.
            for (int i = 0; i <= MultipleScatteringIterations; i++) {
                CreateLUT(ref groundIrrdiance[i], "Ground Irrdiance Order " + i, irradianceSize.x, irradianceSize.y, 0, RenderTextureFormat.ARGBFloat);
                CreateLUT(ref multipleScattering[i], "Multiple Scattering Order " + i, scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBFloat);
            }
            CreateLUT(ref multipleScatteringDensity, "Multiple Scattering Density", scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBFloat);

            var densityKernal = computeShader.FindKernel("CalculateMultipleScatteringDensityLUT");
            var scatteringKernal = computeShader.FindKernel("CalculateMultipleScatteringLUT");
            var groundIndirectKernal = computeShader.FindKernel("CalculateGroundIndirectIrradianceLUT");
            var groundDirectKernal = computeShader.FindKernel("CalculateGroundDirectIrradianceLUT");

            //Compute ground direct.
            computeShader.SetTexture(groundDirectKernal, "TransmittanceLUT", TransmittanceLUT);
            computeShader.SetTexture(groundDirectKernal, "GroundDirectIrradianceResult", groundIrrdiance[0]);
            computeShader.Dispatch(groundDirectKernal, irradianceSize.x, irradianceSize.y, 1);

            //Multiple iterations.
            for (int scatteringOrder = 1; scatteringOrder <= MultipleScatteringIterations; scatteringOrder++) {

                if (scatteringOrder == 1) {

                } else {
                    //Calculate density.
                    computeShader.SetTexture(densityKernal, "TransmittanceLUT", TransmittanceLUT);
                    computeShader.SetTexture(densityKernal, "SingleRayleighScatteringLUT", SingleScatteringLUTRayleigh);
                    computeShader.SetTexture(densityKernal, "SingleMieScatteringLUT", SingleScatteringLUTMie);
                    computeShader.SetTexture(densityKernal, "MultipleScatteringLUT", multipleScattering[scatteringOrder - 1]);
                    computeShader.SetTexture(densityKernal, "IrradianceLUT", groundIrrdiance[scatteringOrder - 1]);
                    computeShader.SetInt("ScatteringOrder", scatteringOrder);
                    computeShader.SetTexture(densityKernal, "MultipleScatteringDensityResult", multipleScatteringDensity);
                    computeShader.Dispatch(densityKernal, scatteringSize.x, scatteringSize.y, scatteringSize.z);

                    //Multiple scattering.
                    computeShader.SetTexture(scatteringKernal, "TransmittanceLUT", TransmittanceLUT);
                    computeShader.SetTexture(scatteringKernal, "MultipleScatteringDensityLUT", multipleScatteringDensity);
                    computeShader.SetTexture(scatteringKernal, "MultipleScatteringResult", multipleScattering[scatteringOrder]);
                    computeShader.Dispatch(scatteringKernal, scatteringSize.x, scatteringSize.y, scatteringSize.z);
                }

                computeShader.SetTexture(groundIndirectKernal, "SingleRayleighScatteringLUT", SingleScatteringLUTRayleigh);
                computeShader.SetTexture(groundIndirectKernal, "SingleMieScatteringLUT", SingleScatteringLUTMie);
                computeShader.SetTexture(groundIndirectKernal, "MultipleScatteringLUT", multipleScattering[scatteringOrder]);
                computeShader.SetInt("ScatteringOrder", scatteringOrder);
                computeShader.SetTexture(groundIndirectKernal, "GroundIndirectIrradianceResult", groundIrrdiance[scatteringOrder]);
                computeShader.Dispatch(groundIndirectKernal, irradianceSize.x, irradianceSize.y, 1);
            }

            //Combine.
            CreateLUT(ref MultipleScatteringLUT, "Multiple Scattering Combined Final", scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBFloat);
            CreateLUT(ref IrradianceLUT, "Irradiance Combined Final", irradianceSize.x, irradianceSize.y, 0, RenderTextureFormat.ARGBFloat);

            var combineGroundKernal = computeShader.FindKernel("CombineGroundIrradianceLUT");
            var combineScatterKernal = computeShader.FindKernel("CombineMultipleScatteringLUT");

            for (int i = 0; i <= MultipleScatteringIterations; i++) {
                computeShader.SetTexture(combineGroundKernal, "GroundIrradianceSumTarget", IrradianceLUT);
                computeShader.SetTexture(combineGroundKernal, "GroundIrradianceSumAdder", groundIrrdiance[i]);
                computeShader.Dispatch(combineGroundKernal, irradianceSize.x, irradianceSize.y, 1);

                if (i > 1) {
                    computeShader.SetTexture(combineScatterKernal, "ScatteringSumTarget", MultipleScatteringLUT);
                    computeShader.SetTexture(combineScatterKernal, "ScatteringSumAdd", multipleScattering[i]);
                    computeShader.Dispatch(combineScatterKernal, scatteringSize.x, scatteringSize.y, scatteringSize.z);
                }
            }
        }

        public void UpdateAllAtOnce() {

            UpdateTransmittanceLUT();
            UpdateSingleScatteringLUT();
            UpdateMultipleScattering();
            UpdateSkyboxMaterial();
        }

        public void UpdateSkyboxMaterial() {
            if (this.skyboxMaterial==null)
                this.skyboxMaterial = new Material(Shader.Find("Skybox/AtmosphereScatteringPrecomputed"));
            config.Apply(skyboxMaterial);
            skyboxMaterial.SetTexture("_SingleRayleigh", SingleScatteringLUTRayleigh);
            skyboxMaterial.SetTexture("_SingleMie", SingleScatteringLUTMie);
            skyboxMaterial.SetTexture("_MultipleScattering", MultipleScatteringLUT);
            skyboxMaterial.SetTexture("_Transmittance", TransmittanceLUT); 
            skyboxMaterial.SetVector("_ScatteringSize", (Vector3)scatteringSize);
            skyboxMaterial.SetVector("_TransmittanceSize", (Vector2)transmittanceSize);
            RenderSettings.skybox = this.skyboxMaterial;
        }

        private static void CreateLUT(ref RenderTexture result, string name, int width, int height, int zsize, RenderTextureFormat format) {
            if (result != null)
                result.Release();
            result = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear);
            result.name = name;
            result.enableRandomWrite = true;
            if (zsize > 0) {
                result.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                result.volumeDepth = zsize;
            } else {
                result.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            }
            result.filterMode = FilterMode.Bilinear;
            result.wrapMode = TextureWrapMode.Clamp;
            result.Create();
        }
    }
}