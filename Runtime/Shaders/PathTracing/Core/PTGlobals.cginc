RaytracingAccelerationStructure g_rtas : register(t0, space1);

StructuredBuffer<MediumData> gMediums;

uint g_maxDepthSurface;
uint g_maxDepthVolume;

uint g_maxMarchingSamples;
float g_minStepSize;

float g_minRayT;
float g_maxRayT;

float g_volumetricExtinctionBoost;

// Minimum volumetric material density before a material affects denoising
#define MIN_DENSITY_FOR_DENOISING 0.25