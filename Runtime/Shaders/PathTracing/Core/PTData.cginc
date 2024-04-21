// --- MAIN
struct RayPayload
{
    // Surface properties
    float3 albedo;
    float3 emission;
    
    // Medium stack
    uint mediumStack;
    int mediumStackCounter;
    

    // Control properties
    float3 bounceRayOrigin;
    float3 bounceRayDirection;
    
    int depthSurface;
    int depthVolume;

    bool wasScattered;
    bool isFrontFace;
    float3 energyLoss;
    float t;

    uint rngState;

    float3 denoisingAlbedo;
    float3 denoisingNormal;
};

struct MediumData
{
    float3 mediumColor;
    float3 mediumEmission;
    float mediumScatteringCoeff;
    float mediumAnisotropy;
    uint mediumHomogeneous;
};

struct CamData
{
    float4x4 projectionMatrix;
    float4x4 projectionMatrixInverse;
    float4x4 cameraToWorldMatrix;
    float4x4 localToWorldMatrix;

    float3 origin;
    float3 forwardDirection;
    float3 upDirection;
};


// --- LIGHTS
struct PointLightData
{
    float3 position;
    float radius;
    
    float3 emission;
};

struct SpotLightData
{
    float3 position;
    float3 dir;
    float angle;
    float radius;
    
    float3 emission;
};

// --- OTHER
struct DebugRay
{
    float4 Origin;
    float4 Direction;
    float4 TargetPoint;
    float4 Color;
    float Transparency;
    int WasScattered;
};