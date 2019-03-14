using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class AerialPerspective : MonoBehaviour {
    private new Camera camera;
    private void Start() {
        camera = GetComponent<Camera>();
        if (camera.depthTextureMode == DepthTextureMode.None)
            camera.depthTextureMode = DepthTextureMode.Depth;
        material = new Material(Shader.Find("Hidden/Yangrc/AerialPerspective"));
    }

    private Material material;

    [ImageEffectOpaque]
    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        material.SetVector("_ProjectionExtents", camera.GetProjectionExtents());
        Graphics.Blit(source, destination, material, 0);
    }
}

public static class CameraExtension {
    public static Vector4 GetProjectionExtents(this Camera camera, float texelOffsetX, float texelOffsetY) {
        if (camera == null)
            return Vector4.zero;

        float oneExtentY = camera.orthographic ? camera.orthographicSize : Mathf.Tan(0.5f * Mathf.Deg2Rad * camera.fieldOfView);
        float oneExtentX = oneExtentY * camera.aspect;
        float texelSizeX = oneExtentX / (0.5f * camera.pixelWidth);
        float texelSizeY = oneExtentY / (0.5f * camera.pixelHeight);
        float oneJitterX = texelSizeX * texelOffsetX;
        float oneJitterY = texelSizeY * texelOffsetY;

        return new Vector4(oneExtentX, oneExtentY, oneJitterX, oneJitterY);// xy = frustum extents at distance 1, zw = jitter at distance 1
    }

    public static Vector4 GetProjectionExtents(this Camera camera) {
        return GetProjectionExtents(camera, 0.0f, 0.0f);
    }
}