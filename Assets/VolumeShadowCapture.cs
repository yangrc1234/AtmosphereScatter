using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Light))]
public class VolumeShadowCapture : MonoBehaviour {

    private void Awake() {
        light = GetComponent<Light>();
        if (light.type != LightType.Directional) {
            throw new UnityException("Volume shadow only supported for main directional light");
        }
    }

    private new Light light;
    public RenderTexture csmTemp;
    private Material material;
	// Use this for initialization
	void Start () {
        csmTemp = new RenderTexture(256, 256, 24, RenderTextureFormat.RHalf);
        csmTemp.name = "CapturedCascadeShadowMap";
        csmTemp.Create();
        material = new Material(Shader.Find("Hidden/Yangrc/ShadowMapProcessor"));
        var afterShadowPass = new CommandBuffer();

        afterShadowPass.SetGlobalTexture("_ApShadowMap", new UnityEngine.Rendering.RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive));
        light.AddCommandBuffer(LightEvent.AfterShadowMap, afterShadowPass);

        var beforeScreenMask = new CommandBuffer();
        beforeScreenMask.Blit(null, csmTemp, material, 0); //Capture shadowmap to csmTemp.
        light.AddCommandBuffer(LightEvent.BeforeScreenspaceMask, beforeScreenMask);
    }

    private void OnDestroy() {
        csmTemp.Release();
    }
}
