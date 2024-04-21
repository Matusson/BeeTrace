using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(BeeTraceOutput))]
public class BeeTraceOutputEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement ins = new VisualElement();

        InspectorElement.FillDefaultInspector(ins, serializedObject, this);

        // Save button
        System.Action saveAction = () => ((BeeTraceOutput)target).SaveImage();

        Button saveButton = new Button(saveAction);
        saveButton.text = "Save Image";
        ins.Add(saveButton);
        return ins;
    }
}
