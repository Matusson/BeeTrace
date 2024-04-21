using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(BeeTraceCamera))]
public class BeeTraceCameraEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement ins = new();
        InspectorElement.FillDefaultInspector(ins, serializedObject, this);

        Action clickAction = () =>
        {
            var mainCam = Camera.main;

            float radius = 0.1f;
            if (Physics.SphereCast(mainCam.transform.position, radius, mainCam.transform.forward, out RaycastHit hit))
            {
                ((BeeTraceCamera)target).focalLength = hit.distance + radius;
            }
            else
            {
                Debug.Log("Could not autofocus. Note that this function relies on colliders.");
            }
        };
        Button autofocusButton = new Button(clickAction);
        autofocusButton.text = "Autofocus on Center";

        ins.Add(autofocusButton);

        return ins;
    }
}
