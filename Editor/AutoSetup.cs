using PT.Denoising;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AutoSetup
{
    [MenuItem("BeeTrace/Setup Scene", priority = 0)]
    public static void SetupScene()
    {
        if (GameObject.FindObjectOfType<BeeTraceManager>())
        {
            Debug.LogWarning("BeeTrace Manager is already present in the scene - setup will not run.");
            return;
        }

        // Create a "Manager" GO
        GameObject manGo = new GameObject("BeeTrace Manager");
        manGo.AddComponent<BeeTraceManager>();
        manGo.AddComponent<BeeTraceOutput>();

        // Create a "World" GO
        GameObject worldGo = new GameObject("BeeTrace World");
        worldGo.AddComponent<BeeTraceWorld>();

        // Find the main camera or create one
        GameObject camGo;
        Camera cam;
        if (Camera.main == null)
        {
            camGo = new GameObject("Camera");
            cam = camGo.AddComponent<Camera>();
        }
        else
        {
            cam = Camera.main;
            camGo = cam.gameObject;
        }
        cam.cullingMask = 0;
        cam.clearFlags = CameraClearFlags.Nothing;
        cam.useOcclusionCulling = false;

        camGo.AddComponent<BeeTraceCamera>();
        camGo.AddComponent<BeeTraceTransformWatcher>();
        camGo.AddComponent<Tonemapping>();
        var denoising = camGo.AddComponent<Denoising>();
        denoising.enabled = false;

        camGo.AddComponent<FlyCamera>();
    }
}
