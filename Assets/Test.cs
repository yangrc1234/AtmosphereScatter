using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yangrc.AtmosphereScattering {
    public class Test : MonoBehaviour {
        public ComputeShader computeShader;
        public RenderTexture TransmittanceLUT;
        public RenderTexture SingleScatteringLUTRayleigh;
        public RenderTexture SingleScatteringLUTMie;
        public AtmosphereConfig config;
        public Material volumePreview;

        public Vector2Int transmittanceSize = new Vector2Int(512, 512);
        public Vector3Int scatteringSize = new Vector3Int(32, 32, 128);

        private void updateParams() {
            computeShader.SetInts("TransmittanceLUT_size", transmittanceSize.x, transmittanceSize.y);
            computeShader.SetInts("Scattering_size", scatteringSize.x, scatteringSize.y, scatteringSize.z);
        }

        public void UpdateTransmittance() {
            TransmittanceLUT = new RenderTexture(transmittanceSize.x, transmittanceSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            TransmittanceLUT.enableRandomWrite = true;
            TransmittanceLUT.name = "Transmittance";
            TransmittanceLUT.Create();
                
            var kernal = computeShader.FindKernel("CalculateTransmittanceLUT");
            config.Apply(computeShader);
            updateParams();
            computeShader.SetTexture(kernal, "TransmittanceLUTResult", TransmittanceLUT);
            computeShader.Dispatch(kernal, transmittanceSize.x, transmittanceSize.y, 1);
        }

        public void UpdateSingleScattering() {

            SingleScatteringLUTMie = new RenderTexture(scatteringSize.x, scatteringSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            SingleScatteringLUTMie.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            SingleScatteringLUTMie.volumeDepth = scatteringSize.z;
            SingleScatteringLUTMie.enableRandomWrite = true;
            SingleScatteringLUTMie.name = "SingleMie";
            SingleScatteringLUTMie.Create();

            SingleScatteringLUTRayleigh = new RenderTexture(scatteringSize.x, scatteringSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            SingleScatteringLUTRayleigh.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            SingleScatteringLUTRayleigh.volumeDepth = scatteringSize.z;
            SingleScatteringLUTRayleigh.enableRandomWrite = true;
            SingleScatteringLUTRayleigh.name = "SingleRayleigh";
            SingleScatteringLUTRayleigh.Create();

            var kernal = computeShader.FindKernel("CalculateSingleScatteringLUT");
            config.Apply(computeShader);
            updateParams();

            computeShader.SetTexture(kernal, "TransmittanceLUT", TransmittanceLUT);
            computeShader.SetTexture(kernal, "SingleScatteringMieLUTResult", SingleScatteringLUTMie);
            computeShader.SetTexture(kernal, "SingleScatteringRayleighLUTResult", SingleScatteringLUTRayleigh);

            computeShader.Dispatch(kernal, scatteringSize.x, scatteringSize.y, scatteringSize.z);
            volumePreview.SetTexture("_MainTex", SingleScatteringLUTMie);
        }
    }
}
