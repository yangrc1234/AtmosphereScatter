using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yangrc.AtmosphereScattering {
    public class Test : MonoBehaviour {
        public ComputeShader computeShader;
        public RenderTexture TransmittanceLUT;
        public RenderTexture SingleScatteringLUT;
        public AtmosphereConfig config;
        public Material volumePreview;

        public void UpdateTransmittance() {
            TransmittanceLUT = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            TransmittanceLUT.enableRandomWrite = true;
            TransmittanceLUT.name = "SkyboxLUT";
            TransmittanceLUT.Create();
                
            var kernal = computeShader.FindKernel("CalculateTransmittanceLUT");
            config.Apply(computeShader);
            computeShader.SetTexture(kernal, "TransmittanceLUTResult", TransmittanceLUT);
            computeShader.Dispatch(kernal, 512, 512, 1);
        }

        public void UpdateSingleScattering() {

            SingleScatteringLUT = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            SingleScatteringLUT.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            SingleScatteringLUT.volumeDepth = 128;
            SingleScatteringLUT.enableRandomWrite = true;
            SingleScatteringLUT.name = "SkyboxLUT";
            SingleScatteringLUT.Create();
            
            var kernal = computeShader.FindKernel("CalculateSingleScatteringLUT");
            config.Apply(computeShader);
            
            computeShader.SetTexture(kernal, "TransmittanceLUT", TransmittanceLUT);
            computeShader.SetTexture(kernal, "SingleScatteringLUTResult", SingleScatteringLUT);
            computeShader.Dispatch(kernal, 64, 64, 128);
            volumePreview.SetTexture("_MainTex", SingleScatteringLUT);
        }
    }
}
