using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[AddComponentMenu("BeeTrace/Managers/BeeTrace Output")]
public class BeeTraceOutput : MonoBehaviour
{
    [Tooltip("Set dimensions to current viewport size. Recommended for preview renders only.")]
    public bool autoDimensions = false;

    public int2 renderDimensions = new(1920, 1080);

    [Space]
    [Tooltip("Directory where renders will be saved.")]
    public string outputPath;
    [Tooltip("File name for this render.")]
    public string fileName;

    [Tooltip("File format for renders. Recommended EXR for final renders.")]
    public ImageFormat format = ImageFormat.PNG;

    [Space]
    [Tooltip("If set, the image will be automatically saved when sample limit or time limit is reached.")]
    public bool saveWhenFinished = false;

    public enum ImageFormat
    {
        PNG,
        TGA,
        EXR
    }

    public void Reset()
    {
        // Default to Documents
        outputPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        fileName = SceneManager.GetActiveScene().name + "-001";
    }

    public void Awake()
    {
        UpdateAutoDimensions();

        float actualAspect = (float)Screen.width / Screen.height;
        float desiredAspect = (float)renderDimensions.x / renderDimensions.y;

#if UNITY_EDITOR
        // Correct resolution
        if (math.abs(actualAspect - desiredAspect) > 0.00001f)
        {
            PlayModeWindow.SetCustomRenderingResolution((uint)renderDimensions.x, (uint)renderDimensions.y, "Render Resolution");
        }
#endif
    }

    public void Update()
    {
        UpdateAutoDimensions();
    }


    public void SaveImage()
    {
        BeeTraceCamera manager = FindObjectOfType<BeeTraceCamera>();
        RenderTexture toSave = manager.GetMostRecentTexture();

        // Readback to CPU memory
        var imgData = new NativeArray<float>(toSave.width * toSave.height * 4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        // request the texture data back from the GPU:
        var request = AsyncGPUReadback.RequestIntoNativeArray(ref imgData, toSave);
        request.WaitForCompletion();


        NativeArray<byte> encoded;
        string formatExt;

        if (format == ImageFormat.PNG)
        {
            // Convert image data to SRGB if PNG
            NativeArray<float4> cols = imgData.Reinterpret<float4>(sizeof(float));
            for (int i = 0; i < cols.Length; i++)
            {
                float4 dat = cols[i];
                Color colLin = new Color(dat.x, dat.y, dat.z);
                Color colSrgb = colLin.gamma;
                cols[i] = new float4(colSrgb.r, colSrgb.g, colSrgb.b, dat.w);
            }

            encoded = ImageConversion.EncodeNativeArrayToPNG(imgData, toSave.graphicsFormat, (uint)toSave.width, (uint)toSave.height);
            formatExt = ".png";
        }
        else if (format == ImageFormat.TGA)
        {
            encoded = ImageConversion.EncodeNativeArrayToTGA(imgData, toSave.graphicsFormat, (uint)toSave.width, (uint)toSave.height);
            formatExt = ".tga";
        }
        else // EXR
        {
            Texture2D.EXRFlags exrFlags = Texture2D.EXRFlags.OutputAsFloat | Texture2D.EXRFlags.CompressZIP;
            encoded = ImageConversion.EncodeNativeArrayToEXR(imgData, toSave.graphicsFormat, (uint)toSave.width, (uint)toSave.height, 0, exrFlags);
            formatExt = ".exr";
        }

        string finalPath = System.IO.Path.Combine(outputPath, fileName + formatExt);

        System.IO.File.WriteAllBytes(finalPath, encoded.ToArray());
    }

    internal void RenderingFinishedCallback()
    {
        if (saveWhenFinished)
            SaveImage();
    }

    private void UpdateAutoDimensions()
    {
        if (autoDimensions)
        {
            renderDimensions.x = Screen.width;
            renderDimensions.y = Screen.height;
        }

        if (Screen.width != renderDimensions.x || Screen.height != renderDimensions.y)
        {
            Camera.main.aspect = (float)renderDimensions.x / renderDimensions.y;
        }
    }
}