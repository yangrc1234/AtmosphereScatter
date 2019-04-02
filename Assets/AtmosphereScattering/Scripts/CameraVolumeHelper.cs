using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yangrc.AtmosphereScattering {

    /// <summary>
    /// This helper class provides function to build camera-frustrum aligned volume texture. which stores scattering and transmittance for fast look-up.
    /// </summary>
    public static class CameraVolumeHelper {
        public static ComputeShader computeShader;
        public static void Init(ComputeShader shader) {
            if (computeShader == null) {
                CameraVolumeHelper.computeShader = shader;
                SetupKernals(computeShader);
            }
        }

        private static int CalculateCameraScatteringVolume;
        private static void SetupKernals(ComputeShader computeShader) {
            CalculateCameraScatteringVolume = computeShader.FindKernel("CalculateCameraScatteringVolume");
        }

        public static void CreateShadowTexture(
            ref RenderTexture shadow,
            ref RenderTexture shadowAccumulate,
            Vector3Int volumeTexSize
            ) {
            AtmLutHelper.CreateLUT(ref shadow,
                "CameraVolumeShadow",
                volumeTexSize.x,
                volumeTexSize.y,
                volumeTexSize.z,
                RenderTextureFormat.R8,
                false);

            AtmLutHelper.CreateLUT(ref shadow,
                "CameraVolumeShadowAccumulate",
                volumeTexSize.x,
                volumeTexSize.y,
                volumeTexSize.z,
                RenderTextureFormat.RFloat,
                false);
        }

        public static void CreateShadowTexture(
            RenderTexture shadow,
            RenderTexture shadowAccumulate,
            RenderTexture sunShadowMap,
            Vector3Int volumeTexSize
            ) {
            AtmLutHelper.CreateLUT(ref shadow,
                "CameraVolumeShadow",
                volumeTexSize.x,
                volumeTexSize.y,
                volumeTexSize.z,
                RenderTextureFormat.R8,
                false);

            AtmLutHelper.CreateLUT(ref shadow,
                "CameraVolumeShadowAccumulate",
                volumeTexSize.x,
                volumeTexSize.y,
                volumeTexSize.z,
                RenderTextureFormat.RFloat,
                false);
        }

        public static void CreateCameraAlignedVolumeTexture(
                    ref RenderTexture transmittance,
                    ref RenderTexture scattering,
                    Vector3Int volumeTexSize
                    ) {
            AtmLutHelper.CreateLUT(ref transmittance,
                "CameraVolumeTransmittance",
                volumeTexSize.x,
                volumeTexSize.y,
                volumeTexSize.z,
                RenderTextureFormat.ARGBFloat,
                false);
            AtmLutHelper.CreateLUT(ref scattering,
                "CameraVolumeScattering",
                volumeTexSize.x,
                volumeTexSize.y,
                volumeTexSize.z,
                RenderTextureFormat.ARGBFloat,
                false);
        }

        /// <summary>
        /// Calculate camera frustrum aligned volume.
        /// See frostbite slider for more.
        /// </summary>
        /// <param name="transmittanceTarget">Target to store transmittance</param>
        /// <param name="scatteringTarget">Target to store scattering</param>
        /// <param name="volumeSize">Volume tex size, used to determine dispatch call params</param>
        /// <param name="cameraPos">Camera world pos</param>
        /// <param name="sunDirection">Sun direction(pointing towards sun)</param>
        /// <param name="frustrumCorners">four corners of camera frustrum(bl, br, tl, tr). We can't use projection matrix since we divide depth equal range, so we manually interpolate uvw using these corners and near/far plane</param>
        /// <param name="nearFarPlane">Near and far plane distance</param>
        public static void UpdateCameraVolume(
            RenderTexture transmittanceTarget,
            RenderTexture scatteringTarget,
            Vector3Int volumeSize,
            Vector3 cameraPos,
            Vector3 sunDirection,
            Vector3[] frustrumCorners,
            Vector2 nearFarPlane
            ) {
            AtmosphereScatteringLutManager.instance.UpdateComputeShaderValueForLerpedAp(computeShader, CalculateCameraScatteringVolume);


            computeShader.SetTexture(CalculateCameraScatteringVolume, "CameraVolumeTransmittance", transmittanceTarget);
            computeShader.SetTexture(CalculateCameraScatteringVolume, "CameraVolumeScattering", scatteringTarget);
            computeShader.SetVector("_CameraPos", cameraPos);
            computeShader.SetVector("_SunDir", sunDirection);

            computeShader.SetVector("_CamBotLeft", frustrumCorners[0]);
            computeShader.SetVector("_CamBotRight", frustrumCorners[1]);
            computeShader.SetVector("_CamTopLeft", frustrumCorners[2]);
            computeShader.SetVector("_CamTopRight", frustrumCorners[3]);
            computeShader.SetVector("_NearFarPlane", nearFarPlane);
            computeShader.Dispatch(CalculateCameraScatteringVolume, volumeSize.x / 8, volumeSize.y / 8, volumeSize.z / 8);
        }
    }
}