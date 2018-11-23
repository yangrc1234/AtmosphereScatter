using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yangrc.AtmosphereScattering {
    public class Test : MonoBehaviour {
        public ComputeShader computeShader;
        public RenderTexture TransmittanceLUT;
        public RenderTexture SingleScatteringLUTRayleigh;
        public RenderTexture SingleScatteringLUTMie;
        public RenderTexture MultipleScatteringDensityLUT;
        public RenderTexture MultipleScatteringLUT;
        public RenderTexture IrradianceLUT;
        public AtmosphereConfig config;
        public Material volumePreview;
        public Material volumePreview2;

        public Vector2Int transmittanceSize = new Vector2Int(512, 512);
        public Vector3Int scatteringSize = new Vector3Int(32, 32, 128);
        public Vector2Int irradianceSize = new Vector2Int(32, 32);


        private void CreateLUT(ref RenderTexture result, string name, int width, int height, int zsize, RenderTextureFormat format) {
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
            result.Create();
        }

        private void updateParams() {
            config.Apply(computeShader);
            computeShader.SetInts("TransmittanceSize", transmittanceSize.x, transmittanceSize.y);
            computeShader.SetInts("ScatteringSize", scatteringSize.x, scatteringSize.y, scatteringSize.z);
            computeShader.SetInts("IrradianceSize", irradianceSize.x, irradianceSize.y);
        }

        public void UpdateTransmittance() {
            CreateLUT(ref TransmittanceLUT, "Transmittance", transmittanceSize.x, transmittanceSize.y, 0, RenderTextureFormat.ARGBFloat);
                
            var kernal = computeShader.FindKernel("CalculateTransmittanceLUT");
            updateParams();
            computeShader.SetTexture(kernal, "TransmittanceLUTResult", TransmittanceLUT);
            computeShader.Dispatch(kernal, transmittanceSize.x, transmittanceSize.y, 1);
        }

        public void UpdateSingleScattering() {

            CreateLUT(ref SingleScatteringLUTMie, "SingleMie", scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBFloat);
            CreateLUT(ref SingleScatteringLUTRayleigh, "SingleRayleigh", scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBFloat);

            var kernal = computeShader.FindKernel("CalculateSingleScatteringLUT");
            updateParams();

            computeShader.SetTexture(kernal, "TransmittanceLUT", TransmittanceLUT);
            computeShader.SetTexture(kernal, "SingleScatteringMieLUTResult", SingleScatteringLUTMie);
            computeShader.SetTexture(kernal, "SingleScatteringRayleighLUTResult", SingleScatteringLUTRayleigh);

            computeShader.Dispatch(kernal, scatteringSize.x, scatteringSize.y, scatteringSize.z);
            volumePreview.SetTexture("_MainTex", SingleScatteringLUTMie);
        }
        public RenderTexture[] groundIrrdiance;
        public RenderTexture[] multipleScattering;
        public RenderTexture multipleScatteringDensity;

        [Range(2, 5)]
        public uint MultipleScatteringIterations = 3;
        public void UpdateMultipleScattering() {
            updateParams();

            groundIrrdiance = new RenderTexture[MultipleScatteringIterations + 1];
            multipleScattering = new RenderTexture[MultipleScatteringIterations + 1];
            multipleScatteringDensity = null;

            //Create tons of rt to store intermediate results.
            for (int i = 0; i <= MultipleScatteringIterations; i++) {
                this.CreateLUT(ref groundIrrdiance[i], "Ground Irrdiance Order " + i, irradianceSize.x, irradianceSize.y, 0 , RenderTextureFormat.ARGBFloat);
                this.CreateLUT(ref multipleScattering[i], "Multiple Scattering Order " + i, scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBFloat);
            }
            this.CreateLUT(ref multipleScatteringDensity, "Multiple Scattering Density", scatteringSize.x, scatteringSize.y, scatteringSize.z, RenderTextureFormat.ARGBFloat);

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
                    computeShader.SetTexture(densityKernal, "IrrdianceLUT", groundIrrdiance[scatteringOrder-1]);
                    computeShader.SetInt("ScatteringOrder", scatteringOrder);
                    computeShader.SetTexture(densityKernal, "MultipleScatteringDensityResult", multipleScatteringDensity);
                    computeShader.Dispatch(densityKernal, scatteringSize.x, scatteringSize.y, scatteringSize.z);
                    volumePreview.SetTexture("_MainTex", multipleScatteringDensity);

                    //Multiple scattering.
                    computeShader.SetTexture(scatteringKernal, "TransmittanceLUT", TransmittanceLUT);
                    computeShader.SetTexture(scatteringKernal, "MultipleScatteringDensityLUT", multipleScatteringDensity);
                    computeShader.SetTexture(scatteringKernal, "MultipleScatteringResult", multipleScattering[scatteringOrder]);
                    computeShader.Dispatch(scatteringKernal, scatteringSize.x, scatteringSize.y, scatteringSize.z);
                    volumePreview2.SetTexture("_MainTex", multipleScattering[scatteringOrder]);
                }

                computeShader.SetTexture(groundIndirectKernal, "SingleRayleighScatteringLUT", SingleScatteringLUTRayleigh);
                computeShader.SetTexture(groundIndirectKernal, "SingleMieScatteringLUT", SingleScatteringLUTMie);
                computeShader.SetTexture(groundIndirectKernal, "MultipleScatteringLUT", multipleScattering[scatteringOrder]);
                computeShader.SetInt("ScatteringOrder", scatteringOrder);
                computeShader.SetTexture(groundIndirectKernal, "GroundIndirectIrradianceResult", groundIrrdiance[scatteringOrder]);
                computeShader.Dispatch(groundIndirectKernal, irradianceSize.x, irradianceSize.y, 1);
            }
        }
    }
}
