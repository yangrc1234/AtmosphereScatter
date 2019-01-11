using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WatchIt : MonoBehaviour {

    public float time;
    public float speed;
    public new Light light;
    public Yangrc.AtmosphereScattering.Test test;
    // Update is called once per frame

    private void Start() {
        test.UpdateTransmittance();
        test.UpdateSingleScattering();
        test.UpdateMultipleScattering();
    }

    void Update () {
        time += Time.deltaTime * speed;
        light.transform.eulerAngles = new Vector3(
            time * 180.0f, 0.0f, 0.0f
            );
	}
}
