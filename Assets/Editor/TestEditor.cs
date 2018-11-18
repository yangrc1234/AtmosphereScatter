using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yangrc.AtmosphereScattering {
    [CustomEditor(typeof(Test))]
    public class TestEditor : Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (GUILayout.Button("TestTransmittance")) {
                (target as Test).UpdateTransmittance();
            }
            if (GUILayout.Button("TestSingleScattering")) {
                (target as Test).UpdateSingleScattering();
            }
        }
    }
}
