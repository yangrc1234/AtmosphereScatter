using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Yangrc.AtmosphereScattering {

    public static class HighResolutionDateTime {
        public static bool IsAvailable { get; private set; }

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern void GetSystemTimePreciseAsFileTime(out long filetime);

        public static DateTime UtcNow {
            get {
                if (!IsAvailable) {
                    throw new InvalidOperationException(
                        "High resolution clock isn't available.");
                }

                long filetime;
                GetSystemTimePreciseAsFileTime(out filetime);

                return DateTime.FromFileTimeUtc(filetime);
            }
        }

        static HighResolutionDateTime() {
            try {
                long filetime;
                GetSystemTimePreciseAsFileTime(out filetime);
                IsAvailable = true;
            } catch (EntryPointNotFoundException) {
                // Not running Windows 8 or higher.
                IsAvailable = false;
            }
        }
    }

    public class ProgressiveLutUpdater {

        AtmosphereConfig atmConfig;
        AtmLutGenerateConfig lutConfig;
        public ProgressiveLutUpdater(AtmosphereConfig atmConfig, AtmLutGenerateConfig lutConfig) {
            this.atmConfig = atmConfig;
            this.lutConfig = lutConfig;
        }
        public const int k_MultiScatteringOrderDepth = 4;
        
        private RenderTexture[] groundIrradianceTemp = new RenderTexture[k_MultiScatteringOrderDepth + 1];
        private RenderTexture multiScatteringDensity;
        private RenderTexture[] multiScatteringTemp = new RenderTexture[k_MultiScatteringOrderDepth + 1];

        public RenderTexture transmittance;
        public RenderTexture singleRayleigh, singleMie;
        public RenderTexture multiScatteringCombine, groundIrradianceCombine;

        public bool isDone { get; private set; }

        public IEnumerator UpdateRoutine() {
            isDone = false;
            AtmLutHelper.CreateTransmittanceTexture(ref transmittance, lutConfig);

            //Update transmittance in 5 frames.
            for (int i = 0; i < 1; i++) {
                AtmLutHelper.ApplyComputeShaderParams(lutConfig, atmConfig);
                using (new ConvenientStopwatch("Transmittance " + i)) {
                    AtmLutHelper.UpdateTransmittance(
                        transmittance,
                        lutConfig,
                        0.0f,
                        1.0f
                        );
                    yield return null;
                }
            }

            //Update GroundDirect
            using (new ConvenientStopwatch("GroundDirect")) {
                AtmLutHelper.ApplyComputeShaderParams(lutConfig, atmConfig);
                AtmLutHelper.CreateGroundIrradianceTexture(ref groundIrradianceTemp[0], 0, lutConfig);
                AtmLutHelper.UpdateGroundDirectIrradiance(groundIrradianceTemp[0], transmittance, lutConfig, 0.0f, 1.0f);
            }
            yield return null;

            //Update SingleRayleigh/Mie
            using (new ConvenientStopwatch("Single Rayleigh/Mie")) {
                AtmLutHelper.ApplyComputeShaderParams(lutConfig, atmConfig);
                AtmLutHelper.CreateSingleRayleighMieTexture(ref singleRayleigh, ref singleMie, lutConfig);
                AtmLutHelper.UpdateSingleRayleighMie(singleRayleigh, singleMie, transmittance, lutConfig, 0.0f, 1.0f);
            }
            yield return null;

            AtmLutHelper.CreateMultiScatteringTexture(ref multiScatteringTemp[1], 1, lutConfig);    //This texture is not "Meaningful"(Since the 1-st order should be SingleRayleigh and SingleMie, and this tex won't be used in actual computation either), but we need to make an empty one to avoid shader error.

            using (new ConvenientStopwatch("GroundIrradiance 1")) {
                //Ground irradiance of 1-st order scattering.
                AtmLutHelper.ApplyComputeShaderParams(lutConfig, atmConfig);
                AtmLutHelper.CreateGroundIrradianceTexture(ref groundIrradianceTemp[1], 1, lutConfig);
                AtmLutHelper.UpdateGroundIrradiance(groundIrradianceTemp[1], singleRayleigh, singleMie, multiScatteringTemp[1], 1, lutConfig, 0.0f, 1.0f);
            }
                yield return null;

            //Real game start.
            AtmLutHelper.CreateMultiScatteringDensityTexture(ref multiScatteringDensity, lutConfig);

            for (int i = 2; i <= 4; i++) {
                using (new ConvenientStopwatch("Multi Scattering " + i)) {
                    AtmLutHelper.ApplyComputeShaderParams(lutConfig, atmConfig);
                    AtmLutHelper.CreateMultiScatteringTexture(ref multiScatteringTemp[i], i, lutConfig);
                    AtmLutHelper.UpdateMultiScatteringDensity(multiScatteringDensity, transmittance, singleRayleigh, singleMie, multiScatteringTemp[i - 1], groundIrradianceTemp[i - 2], i - 1, lutConfig, 0.0f, 1.0f);
                    AtmLutHelper.UpdateMultiScatteringCombineDensity(multiScatteringTemp[i], transmittance, multiScatteringDensity, lutConfig, 0.0f, 1.0f);

                    AtmLutHelper.CreateGroundIrradianceTexture(ref groundIrradianceTemp[i], i, lutConfig);
                    AtmLutHelper.UpdateGroundIrradiance(groundIrradianceTemp[i], singleRayleigh, singleMie, multiScatteringTemp[i], i, lutConfig, 0.0f, 1.0f);
                }
                    yield return null;
            }

            //Combine our multiscattering texture.
            using (new ConvenientStopwatch("Texture Combine ")) {
                AtmLutHelper.ApplyComputeShaderParams(lutConfig, atmConfig);
                AtmLutHelper.CreateFinalCombinedTexture(ref multiScatteringCombine, ref groundIrradianceCombine, lutConfig);
                for (int i = 2; i <= 4; i++) {
                    AtmLutHelper.UpdateFinalCombinedMultiScatter(multiScatteringCombine, multiScatteringTemp[i], lutConfig);
                }

                for (int i = 0; i <= 4; i++) {
                    AtmLutHelper.UpdateFinalCombinedIrradiance(groundIrradianceCombine, groundIrradianceTemp[i], lutConfig);
                }
            }
            //Done!
            isDone = true;
        }
    }

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

    public class ConvenientStopwatch : System.IDisposable {
        public ConvenientStopwatch(string name) {
            this.name = name;
            start = HighResolutionDateTime.UtcNow;
        }
        DateTime start;
        string name;
        public void Dispose() {
            var timeSpan = HighResolutionDateTime.UtcNow - start;
            Debug.Log(name + ":" + timeSpan.TotalMilliseconds);
        }
    }

    public static class AtmLutHelper {

        public static void Init(ComputeShader shader) {
            if (computeShader == null) {
                AtmLutHelper.computeShader = shader;
                SetupKernals(computeShader);
            }
        }

        public static ComputeShader computeShader;
        private static int CalculateTransmittanceLUT;
        private static int CalculateSingleScatteringLUT;
        private static int CalculateGroundDirectIrradianceLUT;
        private static int CalculateGroundIndirectIrradianceLUT;
        private static int CalculateMultipleScatteringDensityLUT;
        private static int CalculateMultipleScatteringLUT;
        private static int SumGroundIrradianceLUT;
        private static int SumMultipleScatteringLUT;

        /// <summary>
        /// These functions help do all the "SetXXX" stuff.
        /// So we can focus on how to generate Luts.
        /// </summary>

        private static void SetupKernals(ComputeShader computeShader) {
            CalculateTransmittanceLUT = computeShader.FindKernel("CalculateTransmittanceLUT");
            CalculateSingleScatteringLUT = computeShader.FindKernel("CalculateSingleScatteringLUT");
            CalculateGroundDirectIrradianceLUT = computeShader.FindKernel("CalculateGroundDirectIrradianceLUT");
            CalculateGroundIndirectIrradianceLUT = computeShader.FindKernel("CalculateGroundIndirectIrradianceLUT");
            CalculateMultipleScatteringDensityLUT = computeShader.FindKernel("CalculateMultipleScatteringDensityLUT");
            CalculateMultipleScatteringLUT = computeShader.FindKernel("CalculateMultipleScatteringLUT");
            SumGroundIrradianceLUT = computeShader.FindKernel("CombineGroundIrradianceLUT");
            SumMultipleScatteringLUT = computeShader.FindKernel("CombineMultipleScatteringLUT");
        }

        public static void ApplyComputeShaderParams(AtmLutGenerateConfig lutConfig, AtmosphereConfig atmConfig) {
            lutConfig.Apply(computeShader);
            atmConfig.Apply(computeShader);
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

        private static void NormalizeProgressPointer(float start, float end, int length, out int startInt, out int endInt) {
            if (end < start) {
                var t = start;
                start = end;
                end = t;
            }

            start = Mathf.Clamp01(start);
            end = Mathf.Clamp01(end);
            startInt = Mathf.RoundToInt(length * start);
            endInt = Mathf.RoundToInt(length * end);
        }

        public static void CreateTransmittanceTexture(
            ref RenderTexture target,
            AtmLutGenerateConfig lutconfig) {
            CreateLUT(ref target, "Transmittance", lutconfig.transmittanceSize.x, lutconfig.transmittanceSize.y, 0, RenderTextureFormat.ARGBHalf);
        }

        public static void UpdateTransmittance(
            RenderTexture target, 
            AtmLutGenerateConfig lutConfig, 
            float start, 
            float end) 
            {
            int xStart, xEnd;
            NormalizeProgressPointer(start, end, lutConfig.transmittanceSize.x, out xStart, out xEnd);
            computeShader.SetInts("_ThreadOffset", xStart, 0, 0);
            computeShader.SetTexture(CalculateTransmittanceLUT, "TransmittanceLUTResult", target);
            computeShader.Dispatch(CalculateTransmittanceLUT, xEnd - xStart, lutConfig.transmittanceSize.y, 1);
        }

        public static void CreateSingleRayleighMieTexture(ref RenderTexture rayleigh, ref RenderTexture mie, AtmLutGenerateConfig lutconfig) {
            CreateLUT(ref rayleigh, "SingleMie", lutconfig.scatteringSize.x, lutconfig.scatteringSize.y, lutconfig.scatteringSize.z, RenderTextureFormat.ARGBHalf);
            CreateLUT(ref mie, "SingleRayleigh", lutconfig.scatteringSize.x, lutconfig.scatteringSize.y, lutconfig.scatteringSize.z, RenderTextureFormat.ARGBHalf);
        }

        public static void UpdateSingleRayleighMie(
            RenderTexture rayleightarget, 
            RenderTexture mietarget, 
            RenderTexture TransmittanceLUT, 
            AtmLutGenerateConfig lutConfig, 
            float start, 
            float end
            ) {

            int xStart, xEnd;
            NormalizeProgressPointer(start, end, lutConfig.scatteringSize.x, out xStart, out xEnd);
            computeShader.SetInts("_ThreadOffset", xStart, 0, 0);

            computeShader.SetTexture(CalculateSingleScatteringLUT, "TransmittanceLUT", TransmittanceLUT);
            computeShader.SetTexture(CalculateSingleScatteringLUT, "SingleScatteringMieLUTResult", mietarget);
            computeShader.SetTexture(CalculateSingleScatteringLUT, "SingleScatteringRayleighLUTResult", rayleightarget);

            computeShader.Dispatch(CalculateSingleScatteringLUT, xEnd - xStart, lutConfig.scatteringSize.y, lutConfig.scatteringSize.z);
        }

        public static void CreateGroundIrradianceTexture(ref RenderTexture target, int order, AtmLutGenerateConfig lutconfig) {
            CreateLUT(ref target, "Ground Irrdiance Order " + order, lutconfig.irradianceSize.x, lutconfig.irradianceSize.y, 0, RenderTextureFormat.ARGBHalf);
        }

        public static void UpdateGroundDirectIrradiance(
            RenderTexture target,
            RenderTexture TransmittanceLUT,
            AtmLutGenerateConfig lutConfig,
            float start,
            float end
            ) {
            int xStart, xEnd;
            NormalizeProgressPointer(start, end, lutConfig.irradianceSize.x, out xStart, out xEnd);
            computeShader.SetInts("_ThreadOffset", xStart, 0, 0);

            computeShader.SetTexture(CalculateGroundDirectIrradianceLUT, "TransmittanceLUT", TransmittanceLUT);
            computeShader.SetTexture(CalculateGroundDirectIrradianceLUT, "GroundDirectIrradianceResult", target);
            computeShader.Dispatch(CalculateGroundDirectIrradianceLUT, xEnd-xStart, lutConfig.irradianceSize.y, 1);
        }

        public static void UpdateGroundIrradiance(
            RenderTexture target,
            RenderTexture singleRayleigh,
            RenderTexture singleMie,
            RenderTexture multiScattering,
            int scatteringOrder,
            AtmLutGenerateConfig lutConfig,
            float start,
            float end
            ) {
            int xStart, xEnd;
            NormalizeProgressPointer(start, end, lutConfig.irradianceSize.x, out xStart, out xEnd);
            computeShader.SetInts("_ThreadOffset", xStart, 0, 0);

            computeShader.SetInt("ScatteringOrder", scatteringOrder);
            computeShader.SetTexture(CalculateGroundIndirectIrradianceLUT, "SingleRayleighScatteringLUT", singleRayleigh);
            computeShader.SetTexture(CalculateGroundIndirectIrradianceLUT, "SingleMieScatteringLUT", singleMie);
            computeShader.SetTexture(CalculateGroundIndirectIrradianceLUT, "MultipleScatteringLUT", multiScattering);
            computeShader.SetTexture(CalculateGroundIndirectIrradianceLUT, "GroundIndirectIrradianceResult", target);
            computeShader.Dispatch(CalculateGroundIndirectIrradianceLUT, xEnd - xStart, lutConfig.irradianceSize.y, 1);
        }

        public static void UpdateMultiScatteringDensity(
            RenderTexture target,
            RenderTexture TransmittanceLUT,
            RenderTexture SingleScatteringLUTRayleigh,
            RenderTexture SingleScatteringLUTMie,
            RenderTexture MultiScatteringOfLastOrder,
            RenderTexture GroundDirectIrradianceLUT,
            int scatteringOrder,
            AtmLutGenerateConfig lutConfig,
            float start,
            float end
            ) {
            int xStart, xEnd;
            NormalizeProgressPointer(start, end, lutConfig.scatteringSize.x, out xStart, out xEnd);
            computeShader.SetInts("_ThreadOffset", xStart, 0, 0);

            computeShader.SetTexture(CalculateMultipleScatteringDensityLUT, "TransmittanceLUT", TransmittanceLUT);
            computeShader.SetTexture(CalculateMultipleScatteringDensityLUT, "SingleRayleighScatteringLUT", SingleScatteringLUTRayleigh);
            computeShader.SetTexture(CalculateMultipleScatteringDensityLUT, "SingleMieScatteringLUT", SingleScatteringLUTMie);
            computeShader.SetTexture(CalculateMultipleScatteringDensityLUT, "IrradianceLUT", GroundDirectIrradianceLUT);
            computeShader.SetTexture(CalculateMultipleScatteringDensityLUT, "MultipleScatteringLUT", MultiScatteringOfLastOrder);
            computeShader.SetInt("ScatteringOrder", scatteringOrder);
            computeShader.SetTexture(CalculateMultipleScatteringDensityLUT, "MultipleScatteringDensityResult", target);
            computeShader.Dispatch(CalculateMultipleScatteringDensityLUT, xEnd-xStart, lutConfig.scatteringSize.y, lutConfig.scatteringSize.z);
        }

        public static void CreateMultiScatteringDensityTexture(
            ref RenderTexture result,
            AtmLutGenerateConfig lutConfig
            ) {
            CreateLUT(ref result, "MultiScatteringDensity", lutConfig.scatteringSize.x, lutConfig.scatteringSize.y, lutConfig.scatteringSize.z, RenderTextureFormat.ARGBHalf);
        }

        public static void CreateMultiScatteringTexture(
            ref RenderTexture result,
            int order,
            AtmLutGenerateConfig lutConfig
            ) {
            CreateLUT(ref result, "MultiScattering " + order, lutConfig.scatteringSize.x, lutConfig.scatteringSize.y, lutConfig.scatteringSize.z, RenderTextureFormat.ARGBHalf);
        }

        public static void UpdateMultiScatteringCombineDensity(
            RenderTexture target,
            RenderTexture TransmittanceLUT,
            RenderTexture multipleScatteringDensity,
            AtmLutGenerateConfig lutConfig,
            float start,
            float end
            ) {
            int xStart, xEnd;
            NormalizeProgressPointer(start, end, lutConfig.scatteringSize.x, out xStart, out xEnd);
            computeShader.SetInts("_ThreadOffset", xStart, 0, 0);

            computeShader.SetTexture(CalculateMultipleScatteringLUT, "TransmittanceLUT", TransmittanceLUT);
            computeShader.SetTexture(CalculateMultipleScatteringLUT, "MultipleScatteringDensityLUT", multipleScatteringDensity);
            computeShader.SetTexture(CalculateMultipleScatteringLUT, "MultipleScatteringResult", target);
            computeShader.Dispatch(CalculateMultipleScatteringLUT, xEnd-xStart, lutConfig.scatteringSize.y, lutConfig.scatteringSize.z);
        }

        public static void CreateFinalCombinedTexture(
            ref RenderTexture MultipleScatteringLUT, 
            ref RenderTexture IrradianceLUT,
            AtmLutGenerateConfig lutConfig
            ) {
            CreateLUT(ref MultipleScatteringLUT, "Multiple Scattering Combined Final", lutConfig.scatteringSize.x, lutConfig.scatteringSize.y, lutConfig.scatteringSize.z, RenderTextureFormat.ARGBHalf);
            CreateLUT(ref IrradianceLUT, "Irradiance Combined Final", lutConfig.irradianceSize.x, lutConfig.irradianceSize.y, 0, RenderTextureFormat.ARGBHalf);
        }

        public static void UpdateFinalCombinedMultiScatter(
            RenderTexture multiScatteringTarget,
            RenderTexture multiScatteringOfSingleOrder,
            AtmLutGenerateConfig lutConfig
            ) {
            computeShader.SetTexture(SumMultipleScatteringLUT, "ScatteringSumTarget", multiScatteringTarget);
            computeShader.SetTexture(SumMultipleScatteringLUT, "ScatteringSumAdd", multiScatteringOfSingleOrder);
            computeShader.Dispatch(SumMultipleScatteringLUT, lutConfig.scatteringSize.x, lutConfig.scatteringSize.y, lutConfig.scatteringSize.z);
        }

        public static void UpdateFinalCombinedIrradiance(
            RenderTexture target,
            RenderTexture irradianceOfSingleOrder,
            AtmLutGenerateConfig lutConfig
            ) {
            computeShader.SetTexture(SumGroundIrradianceLUT, "GroundIrradianceSumTarget", target);
            computeShader.SetTexture(SumGroundIrradianceLUT, "GroundIrradianceSumAdder", irradianceOfSingleOrder);
            computeShader.Dispatch(SumGroundIrradianceLUT, lutConfig.irradianceSize.x, lutConfig.irradianceSize.y, 1);
        }
    }
    
    public class AtmosphereScatteringLutManager : MonoBehaviour {
        public bool autoInit = true;
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
        private IEnumerator updater_enumerator;

        private void Awake() {
            if (computeShader == null) {
                throw new System.InvalidOperationException("Compute shader not set!");
            }
            if (_instance != null) {
                throw new System.InvalidOperationException("AtmosphereScatteringLutManager already exists!");
            }
            _instance = this;
            AtmLutHelper.Init(computeShader);
            updater = new ProgressiveLutUpdater(atmosphereConfig, lutConfig);
            StartCoroutine(updater.UpdateRoutine());
        }

        private void Update() {
            if (updater.isDone) {
                if (forceRefresh) {
                    forceRefresh = false;
                    StartCoroutine(updater.UpdateRoutine());
                } else {
                    UpdateSkyboxMaterial();
                }
            }
        }

        private void Start() {
            if (autoInit) {
                UpdateAllAtOnce();
            }
        }

        public AtmLutGenerateConfig lutConfig;
        public AtmosphereConfig atmosphereConfig;
        public Material skyboxMaterial;

        private RenderTexture TransmittanceLUT;
        private RenderTexture SingleScatteringLUTRayleigh;
        private RenderTexture SingleScatteringLUTMie;
        private RenderTexture MultipleScatteringLUT;
        private RenderTexture IrradianceLUT;

        public Vector2Int transmittanceSize {
            get {
                return lutConfig.transmittanceSize;
            }
        }
        public Vector3Int scatteringSize {
            get {
                return lutConfig.scatteringSize;
            }
        }
        public Vector2Int irradianceSize {
            get {
                return lutConfig.irradianceSize;
            }
        }

        private void SetupComputeShaderCommonParameters() {
            atmosphereConfig.Apply(computeShader);
            computeShader.SetInts("TransmittanceSize", transmittanceSize.x, transmittanceSize.y);
            computeShader.SetInts("ScatteringSize", scatteringSize.x, scatteringSize.y, scatteringSize.z);
            computeShader.SetInts("IrradianceSize", irradianceSize.x, irradianceSize.y);
        }
        
        /// <summary>
        /// Update transmittance texture.
        /// </summary>
        public void UpdateTransmittanceLUT() {
            CreateLUT(ref TransmittanceLUT, "Transmittance", transmittanceSize.x, transmittanceSize.y, 0, RenderTextureFormat.ARGBHalf);
            var kernal = computeShader.FindKernel("CalculateTransmittanceLUT");
            SetupComputeShaderCommonParameters();
            computeShader.SetTexture(kernal, "TransmittanceLUTResult", TransmittanceLUT);
            computeShader.Dispatch(kernal, transmittanceSize.x, transmittanceSize.y, 1);
        }

        public void UpdateSingleScatteringLUT() {
            CreateLUT(ref SingleScatteringLUTMie, "SingleMie", scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBHalf);
            CreateLUT(ref SingleScatteringLUTRayleigh, "SingleRayleigh", scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBHalf);

            var kernal = computeShader.FindKernel("CalculateSingleScatteringLUT");
            SetupComputeShaderCommonParameters();

            computeShader.SetTexture(kernal, "TransmittanceLUT", TransmittanceLUT);
            computeShader.SetTexture(kernal, "SingleScatteringMieLUTResult", SingleScatteringLUTMie);
            computeShader.SetTexture(kernal, "SingleScatteringRayleighLUTResult", SingleScatteringLUTRayleigh);

            computeShader.Dispatch(kernal, scatteringSize.x, scatteringSize.y, scatteringSize.z);
        }

        public void UpdateMultipleScattering(int MultipleScatteringIterations = 4) {
            RenderTexture[] groundIrrdiance = new RenderTexture[10];
            RenderTexture[] multipleScattering = new RenderTexture[10];
            RenderTexture multipleScatteringDensity = null;

            SetupComputeShaderCommonParameters();

            //Create tons of rt to store intermediate results.
            for (int i = 0; i <= MultipleScatteringIterations; i++) {
                CreateLUT(ref groundIrrdiance[i], "Ground Irrdiance Order " + i, irradianceSize.x, irradianceSize.y, 0, RenderTextureFormat.ARGBHalf);
                CreateLUT(ref multipleScattering[i], "Multiple Scattering Order " + i, scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBHalf);
            }
            CreateLUT(ref multipleScatteringDensity, "Multiple Scattering Density", scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBHalf);

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
            CreateLUT(ref MultipleScatteringLUT, "Multiple Scattering Combined Final", scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBHalf);
            CreateLUT(ref IrradianceLUT, "Irradiance Combined Final", irradianceSize.x, irradianceSize.y, 0, RenderTextureFormat.ARGBHalf);

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
            var t = new System.Diagnostics.Stopwatch();

            using (new ConvenientStopwatch("Transmittance")) {
                UpdateTransmittanceLUT();
            }

            using (new ConvenientStopwatch("SingleScattering")) {
                UpdateSingleScatteringLUT();
            }
            using (new ConvenientStopwatch("MultiScattering")) {
                UpdateMultipleScattering();
            }
            UpdateSkyboxMaterial();
        }

        public void UpdateSkyboxMaterial() {
            if (this.skyboxMaterial==null)
                this.skyboxMaterial = new Material(Shader.Find("Skybox/AtmosphereScatteringPrecomputed"));
            atmosphereConfig.Apply(skyboxMaterial);
            skyboxMaterial.SetTexture("_SingleRayleigh", updater.singleRayleigh);
            skyboxMaterial.SetTexture("_SingleMie", updater.singleMie);
            skyboxMaterial.SetTexture("_MultipleScattering", updater.multiScatteringCombine);
            skyboxMaterial.SetTexture("_Transmittance", updater.transmittance); 
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