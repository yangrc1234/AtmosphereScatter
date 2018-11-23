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
            if (GUILayout.Button("TestMultipleScattering")) {
                (target as Test).UpdateMultipleScattering();
                serializedObject.Update();
            }
            if (GUILayout.Button("Update All")) {
                (target as Test).UpdateTransmittance();
                (target as Test).UpdateSingleScattering();
                (target as Test).UpdateMultipleScattering();
                serializedObject.Update();
            }
        }
    }
}
