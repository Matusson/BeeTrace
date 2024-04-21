using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

/// <summary>
/// Main class for scheduling path tracing.
/// </summary>
[AddComponentMenu("BeeTrace/Managers/BeeTrace Manager")]
public class BeeTraceManager : MonoBehaviour
{
    public PathTracingSettings pathTracingSettings;
    
    [Space]
    [Tooltip("Current sample, updated while rendering.")]
    public int currentSample = 0;
    [Tooltip("Target sample - rendering will stop when this number is reached. Set to 0 to disable.")]
    public int maxSamples = 1000;

    [Space]
    [Tooltip("Time elapsed since start of rendering in seconds.")]
    public float timeElapsedSeconds = 0;
    [Tooltip("Time limit for rendering - rendering will stop if this number of seconds is reached. Set to 0 to disable.")]
    public float timeLimitSeconds = 0;

    [Space]
    [Tooltip("If disabled, previous render samples will be discarded. This can help when adjusting the scene.")]
    public bool accumulate = true;

    [Header("Debug")]
    [Tooltip("Draw debug rays in the scene view.")]
    public bool drawDebugRays = false;
    public int drawDebugRaysCount = 10;

    private BeeTraceCamera _camManager;
    private BeeTraceWorld _world;

    internal ComputeBuffer cameraBuf;
    internal ComputeBuffer pointLightBuf;
    internal ComputeBuffer spotLightBuf;

    [SerializeField]
    private RayTracingShader rayGenerator;
    private ComputeBuffer raysDebug;
    private RayTracingAccelerationStructure rtas = null;
    private CommandBuffer com;

    private int4 _renderDimensions;
    private DebugRay[] _debugRaysCpu = new DebugRay[1000000];
    private GlobalKeyword _disableVolumetrics;
    private bool _wasPreviousRendered = true;

    struct DebugRay
    {
        public float4 Origin;
        public float4 Direction;
        public float4 TargetPoint;
        public float4 Color;
        public float Transparency;
        public int WasScattered;
    };
    
    private int GetDebugRaySize()
    {
        return Marshal.SizeOf<DebugRay>();
    }

    private void Start()
    {
        // Dependency setup
        _world = FindObjectOfType<BeeTraceWorld>();

        _camManager = FindAnyObjectByType<BeeTraceCamera>();
        _camManager.manager = this;

        cameraBuf = new(1, Marshal.SizeOf<CameraData>(), ComputeBufferType.Default);
        cameraBuf.SetData(new CameraData[] { _camManager.camData });

        _disableVolumetrics = GlobalKeyword.Create("DISABLE_VOLUMETRICS");

        raysDebug = new(_debugRaysCpu.Length, GetDebugRaySize(), ComputeBufferType.Append);

        RayTracingAccelerationStructure.RASSettings settings = new();
        settings.rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything;
        settings.managementMode = RayTracingAccelerationStructure.ManagementMode.Automatic;
        settings.layerMask = 255;
        rtas = new(settings);
        rtas.Build();
        com = new();

        MediumDataUpdater.Initialize();
    }

    private void Reset()
    {
#if UNITY_EDITOR
        rayGenerator = AssetDatabase.LoadAssetAtPath<RayTracingShader>("Packages/com.matusson.beetrace/Runtime/Shaders/PathTracing/RayGenerator.raytrace");
#endif

        pathTracingSettings.samplesPerBatch = 1;
        pathTracingSettings.maxBouncesSurface = 5;
        pathTracingSettings.maxBouncesVolume = 5;

        pathTracingSettings.enableVolumetrics = true;
        pathTracingSettings.raymarchingSteps = 25;
        pathTracingSettings.minStepSize = 0.001f;
        pathTracingSettings.extinctionBoost = 0;

        pathTracingSettings.filterSize = 1.5f;
        pathTracingSettings.radianceClamp = 100;
    }

    private void Update()
    {
        if (!drawDebugRays)
            return;

        // Display debug information
        for (int i = 0; i < math.min(raysDebug.count, drawDebugRaysCount); i ++)
        {
            var ray = _debugRaysCpu[i];

            float4 target = ray.TargetPoint;
            Color col = new Color(ray.Color.x, ray.Color.y, ray.Color.z, ray.Color.w);

            // DebugExtension from Arkham Interactive
            //DebugExtension.DebugArrow(ray.Origin.xyz, target.xyz - ray.Origin.xyz, col);

            Debug.DrawLine(ray.Origin.xyz, target.xyz, col);
        }
    }


    private void OnDestroy()
    {
        cameraBuf.Release();
        pointLightBuf.Release();
        spotLightBuf.Release();
        raysDebug.Release();

        com.Dispose();

        LightUpdateUtil.Dispose();
        MediumDataUpdater.Dispose();
    }

    public void ForceReset(bool resetScene = false)
    {
        currentSample = resetScene ? 0 : 1;
        timeElapsedSeconds = 0;

        com.Clear();

        // Clear textures
        com.SetRenderTarget(_camManager._accumulationTexture);
        com.ClearRenderTarget(false, true, Color.black);
        com.SetRenderTarget(_camManager._sampleCountsTexture);
        com.ClearRenderTarget(false, true, Color.black);
        com.SetRenderTarget(_camManager._renderTexture);
        com.ClearRenderTarget(false, true, Color.black);

        Graphics.ExecuteCommandBuffer(com);

        _camManager.UpdateCamera();
        cameraBuf.SetData(new CameraData[] { _camManager.camData });
    }


    /// <summary>
    /// Request the next rendering step.
    /// </summary>
    /// <param name="resetRequested"></param>
    internal void NextStep(bool resetRequested)
    {
        // If the scene should be reset
        if (resetRequested)
        {
            currentSample = 0;
            timeElapsedSeconds = 0;
        }

        if (!accumulate)
            ForceReset(false);

        com.Clear();

        // Check if goal reached
        bool sampleLimitAllows = currentSample < maxSamples || maxSamples == 0;
        bool timeLimitAllows = timeElapsedSeconds < timeLimitSeconds || timeLimitSeconds == 0;

        if (sampleLimitAllows && timeLimitAllows)
        {
            PathTraceRT();
            _wasPreviousRendered = true;
        }
        else
        {
            // If true, this is the final iteration
            if(_wasPreviousRendered)
            {
                FindObjectOfType<BeeTraceOutput>().RenderingFinishedCallback();
            }

            _wasPreviousRendered = false;
        }
    }



    private void PathTraceRT()
    {
        _renderDimensions = new(0, 0, _camManager._accumulationTexture.width, _camManager._accumulationTexture.height);

        // Update lighting information
        LightUpdateUtil.UpdateLights(ref pointLightBuf, ref spotLightBuf);
        MediumDataUpdater.UpdateMaterials();

        // Set buffers
        cameraBuf.SetData(new CameraData[] { _camManager.camData });
        com.SetRayTracingShaderPass(rayGenerator, "PathTracing");
        com.SetRayTracingBufferParam(rayGenerator, "camdata", cameraBuf);

        if (drawDebugRays)
        {
            raysDebug.Release();
            raysDebug = new(_debugRaysCpu.Length, GetDebugRaySize(), ComputeBufferType.Append);
        }

        rayGenerator.SetBuffer("_Rays", raysDebug);
        rayGenerator.SetBool("debugRays", drawDebugRays);

        com.SetRayTracingBufferParam(rayGenerator, "pointLights", pointLightBuf);
        com.SetRayTracingIntParam(rayGenerator, "pointLightCount", pointLightBuf.count);

        com.SetRayTracingBufferParam(rayGenerator, "spotLights", spotLightBuf);
        com.SetRayTracingIntParam(rayGenerator, "spotLightCount", spotLightBuf.count);

        float apertureDiameter = _camManager.focalLength / _camManager.aperture;

        com.SetRayTracingFloatParam(rayGenerator, "dofFocalLength", _camManager.focalLength);
        com.SetRayTracingFloatParam(rayGenerator, "dofAperture", apertureDiameter);
        com.SetRayTracingFloatParam(rayGenerator, "dofBladeCount", _camManager.bladeCount);

        com.SetRayTracingFloatParam(rayGenerator, "filterSize", pathTracingSettings.filterSize);
        com.SetRayTracingFloatParam(rayGenerator, "radianceClamp", pathTracingSettings.radianceClamp);
        com.SetRayTracingIntParam(rayGenerator, "curSample", currentSample);

        com.SetRayTracingTextureParam(rayGenerator, "Accumulation", _camManager._accumulationTexture);
        com.SetRayTracingTextureParam(rayGenerator, "SampleCount", _camManager._sampleCountsTexture);
        com.SetRayTracingTextureParam(rayGenerator, "Result", _camManager._renderTexture);
        com.SetRayTracingTextureParam(rayGenerator, "AlbedoBuf", _camManager._albedoTexture);
        com.SetRayTracingTextureParam(rayGenerator, "NormalBuf", _camManager._normalTexture);

        com.SetKeyword(_disableVolumetrics, !pathTracingSettings.enableVolumetrics);
        com.SetGlobalInt("g_maxDepthSurface", pathTracingSettings.maxBouncesSurface + 1);
        com.SetGlobalInt("g_maxDepthVolume", pathTracingSettings.maxBouncesVolume);

        com.SetGlobalFloat("g_maxMarchingSamples", pathTracingSettings.raymarchingSteps);
        com.SetGlobalFloat("g_minStepSize", pathTracingSettings.minStepSize);
        com.SetGlobalFloat("g_volumetricExtinctionBoost", pathTracingSettings.extinctionBoost);


        com.SetGlobalFloat("g_minRayT", _camManager.cam.nearClipPlane);
        com.SetGlobalFloat("g_maxRayT", _camManager.cam.farClipPlane);


        bool useEnvTexture = _world.environment != null;
        // Hack: RTShaders do not support IFDEFs, so a texture is required to be assigned even if not actually used
        // We assign a black placeholder texture to fix this
        if (useEnvTexture)
        {
            com.SetRayTracingTextureParam(rayGenerator, "environmentTexture", _world.environment);
        }
        else
        {
            com.SetRayTracingTextureParam(rayGenerator, "environmentTexture", _world._envPlaceholder);
            com.SetRayTracingVectorParam(rayGenerator, "environmentColor", _world.backgroundColor.linear);
        }
        com.SetRayTracingIntParam(rayGenerator, "useEnvironmentTexture", useEnvTexture ? 1 : 0);
        com.SetRayTracingFloatParam(rayGenerator, "environmentIntensity", _world.environmentStrength);


        com.SetRayTracingIntParam(rayGenerator, "samplesPerBatch", pathTracingSettings.samplesPerBatch);
        com.SetRayTracingIntParam(rayGenerator, "randomSeedOffset", UnityEngine.Random.Range(0, 15153831));

        com.BuildRayTracingAccelerationStructure(rtas);
        com.SetRayTracingAccelerationStructure(rayGenerator, "g_rtas", rtas);

        com.DispatchRays(rayGenerator, "MyRaygenShader", (uint)_renderDimensions.z, (uint)_renderDimensions.w, 1);

        Graphics.ExecuteCommandBuffer(com);

        if (drawDebugRays)
        {
            raysDebug.GetData(_debugRaysCpu);
        }

        currentSample++;
        timeElapsedSeconds += Time.unscaledDeltaTime;
    }
}