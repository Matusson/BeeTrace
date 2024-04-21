#define K_PI                    3.1415926535f
#define K_HALF_PI               1.5707963267f
#define K_TWO_PI                6.283185307f
#define FLOAT_MAX               3.402823466e+38
#define SQRT3_OVER_3            0.57735026919f
#define SQRT2                   1.41421356237f
#define K_RAY_ORIGIN_OFFSET     0.000001

// --- RANDOM

uint WangHash(inout uint seed)
{
    seed = (seed ^ 61) ^ (seed >> 16);
    seed *= 9;
    seed = seed ^ (seed >> 4);
    seed *= 0x27d4eb2d;
    seed = seed ^ (seed >> 15);
    return seed;
}

float RandomFloat01(inout uint seed)
{
    return float(WangHash(seed)) / float(0xFFFFFFFF);
}

float3 RandomUnitVector(inout uint state)
{
    float z = RandomFloat01(state) * 2.0f - 1.0f;
    float a = RandomFloat01(state) * K_TWO_PI;
    float r = sqrt(1.0f - z * z);
    float x = r * cos(a);
    float y = r * sin(a);
    return float3(x, y, z);
}


// --- TRANSFORMATIONS

float3 ToLocal(float3x3 X, float3 V)
{
    return normalize(mul(X, V));
}

float3 ToWorld(float3x3 X, float3 V)
{
    return normalize(mul(V, X));//V.x * X + V.y * Y + V.z * Z;
}

float3x3 GetTangentSpace(const float3 normal) {
    // Choose a helper floattor for the cross product
    float3 helper = float3(1, 0, 0);
    if (abs(normal.x) > 0.99f)
        helper = float3(0, 0, 1);

    // Generate floattors
    float3 tangent = normalize(cross(normal, helper));
    float3 binormal = cross(normal, tangent);

    return float3x3(tangent, normal, binormal);
}


// --- OTHER

void DecodeNormalMap(float3 sampledNormalMap, inout float3 normalWorld, const float3 tangentWorld, const float normalStrength)
{
    sampledNormalMap = (sampledNormalMap * 2.0) - 1.0;

    // Construct TBN frame
    float3 binormalWorld = normalize(cross(normalWorld, tangentWorld));
    float3x3 ntb = float3x3(normalWorld, tangentWorld, binormalWorld);

    float3 worldSpaceNewNormal = normalize(mul(sampledNormalMap, ntb).xyz);
    worldSpaceNewNormal = clamp(worldSpaceNewNormal, -1, 1);

    if (abs(worldSpaceNewNormal.x) == abs(worldSpaceNewNormal.y) == abs(worldSpaceNewNormal.z))
        worldSpaceNewNormal = normalWorld;

    normalWorld = lerp(normalWorld, worldSpaceNewNormal, normalStrength);
}

float map(const float value, const float min1, const float max1, const float min2, const float max2)
{
    // Convert the current value to a percentage
    // 0% - min1, 100% - max1
    float perc = (value - min1) / (max1 - min1);

    // Do the same operation backwards with min2 and max2
    float val = perc * (max2 - min2) + min2;
    return val;
}

RayPayload NewPayload(uint rngState)
{
    RayPayload hit;
    hit.bounceRayOrigin = float3(0, 0, 0);
    hit.bounceRayDirection = float3(0, 0, 0);
    
    hit.albedo = float3(1, 1, 1);
    hit.emission = float3(0, 0, 0);

    hit.mediumStack = 0;
    hit.mediumStackCounter = -1;

    hit.wasScattered = false;

    hit.depthSurface = 0;
    hit.depthVolume = 0;

    hit.rngState = rngState;
    hit.isFrontFace = true;
    hit.energyLoss = 1;
    hit.t = 0;

    hit.denoisingAlbedo = -1;
    hit.denoisingNormal = -2;

    return hit;
}