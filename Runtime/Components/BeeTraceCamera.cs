using System;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Manages interacting with the camera component, including blitting.
/// </summary>
[RequireComponent(typeof(Camera))]
[AddComponentMenu("BeeTrace/Components/BeeTrace Camera")]
public class BeeTraceCamera : MonoBehaviour
{
    [Header("Depth of Field")]
    [Tooltip("Focal length for DOF.")]
    public float focalLength = 1;

    [Tooltip("Aperture size for DOF. Controls the strength of the bokeh effect.")]
    public float aperture = 0;

    [Tooltip("Number of blades of the aperture. Controls the shape of the bokeh effect.")]
    [Range(3f, 9f)]
    public int bladeCount = 5;


    [HideInInspector] public BeeTraceManager manager;

    private RenderTexture _viewportTexture;

    internal RenderTexture _renderTexture;
    internal RenderTexture _accumulationTexture;
    internal RenderTexture _sampleCountsTexture;

    internal RenderTexture _albedoTexture;
    internal RenderTexture _normalTexture;

    private RenderTexture[] _postTextures;

    private BeeTraceOutput _outputManager;
    private RenderTexture _lastTexture;

    internal CameraData camData;
    internal Camera cam;

    private void Awake()
    {
        _outputManager = FindObjectOfType<BeeTraceOutput>();
        cam = GetComponent<Camera>();
        UpdateCamera();
    }

    public void UpdateCamera()
    {
        camData = new(cam);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        bool wasUpdated = TryUpdateRT();
        UpdateCamera();

#if UNITY_EDITOR
        if (EditorApplication.isPaused)
        {
            Graphics.Blit(_renderTexture, destination);
            return;
        }
#endif

        // Request the manager to render a step
        manager.NextStep(wasUpdated);

        // Apply post processing
        RenderTexture dest = ApplyPostProcessing();

        // Resample to viewport resolution for preview
        _lastTexture = dest;
        Graphics.Blit(dest, _viewportTexture);

        Graphics.Blit(_viewportTexture, destination);
    }

    /// <summary>
    /// Applies post processing effects in order.
    /// </summary>
    /// <returns></returns>
    private RenderTexture ApplyPostProcessing()
    {
        // Get the first source
        Graphics.Blit(_renderTexture, _postTextures[0]);

        var postProcesses = GetComponents<BasePostProcess>().OrderBy(x => x.GetPriority());
        int counter = 0;

        foreach (var post in postProcesses)
        {
            if (!post.enabled)
                continue;

            // Ping-pong textures
            RenderTexture src = counter % 2 == 0 ? _postTextures[0] : _postTextures[1];
            RenderTexture dest = counter % 2 == 0 ? _postTextures[1] : _postTextures[0];

            post.Process(src, dest);

            counter++;
        }

        RenderTexture lastDest = (counter - 1) % 2 == 0 ? _postTextures[1] : _postTextures[0];
        return lastDest;
    }


    public RenderTexture GetMostRecentTexture()
    {
        return _lastTexture;
    }

    /// <summary>
    /// Updates the render textures if necessary. Returns true if updated.
    /// </summary>
    /// <param name="newTex"></param>
    public bool TryUpdateRT()
    {
        // See if render dimensions are up to date
        int2 dims = _outputManager.renderDimensions;

        // Create post textures if not already done
        if (_postTextures == null || _postTextures.Length == 0)
        {
            _postTextures = new RenderTexture[2];
            for (int i = 0; i < 2; i++)
            {
                RenderTexture postTex = new(dims.x, dims.y, 0, RenderTextureFormat.ARGBFloat);
                postTex.enableRandomWrite = true;
                postTex.Create();

                _postTextures[i] = postTex;
            }
        }

        if (_renderTexture == null || _renderTexture.width != dims.x || _renderTexture.height != dims.y)
        {
            Debug.Log($"RT initializing with resolution: {dims.x} x {dims.y}");

            _renderTexture = FreeAndRecreateTex(_renderTexture, dims, RenderTextureFormat.ARGBFloat);
            _accumulationTexture = FreeAndRecreateTex(_accumulationTexture, dims, RenderTextureFormat.ARGBFloat);
            _sampleCountsTexture = FreeAndRecreateTex(_sampleCountsTexture, dims, RenderTextureFormat.RFloat);

            _albedoTexture = FreeAndRecreateTex(_albedoTexture, dims, RenderTextureFormat.ARGBFloat);
            _normalTexture = FreeAndRecreateTex(_normalTexture, dims, RenderTextureFormat.ARGBFloat);

            _viewportTexture = FreeAndRecreateTex(_viewportTexture, new int2(Screen.width, Screen.height), RenderTextureFormat.ARGBFloat);

            for (int p = 0; p < _postTextures.Length; p++)
            {
                _postTextures[p] = FreeAndRecreateTex(_postTextures[p], dims, RenderTextureFormat.ARGBFloat);
            }


            manager.ForceReset();
            return true;
        }

        return false;
    }


    private RenderTexture FreeAndRecreateTex(RenderTexture tex, int2 dimensions, RenderTextureFormat format)
    {
        // Release current
        if (tex != null)
            tex.Release();

        tex = new(dimensions.x, dimensions.y, 0, format);
        tex.enableRandomWrite = true;
        tex.Create();

        return tex;
    }
}