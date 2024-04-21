#define PT_SAMPLE_VOLUME_DENSITY(worldPos) 1;
#define PT_SAMPLE_VOLUME_COLOR(worldPos) 1;
#define PT_SAMPLE_VOLUME_EMISSION(worldPos) 0;

#include "PTMaterialStack.cginc"
#pragma multi_compile __ DISABLE_VOLUMETRICS

float3 LocalToWorld(const float3 dir, const float3 tangent, const float3 binormal, const float3 normal) {
    return float3(
        tangent.x * dir.x + binormal.x * dir.y + normal.x * dir.z,
        tangent.y * dir.x + binormal.y * dir.y + normal.y * dir.z,
        tangent.z * dir.x + binormal.z * dir.y + normal.z * dir.z);
}

void OrthonormalBasis(const float3 normal, inout float3 tangent, inout float3 binormal) 
{
    // This method seems to create weird seams? Frisvad method below has branches but avoids this problem
    float sign2 = (normal.z >= 0.0f) ? 1.0f : -1.0f;
    float a = -1.0f / (sign2 + normal.z);
    float b = normal.x * normal.y * a;

    tangent = float3(1.0f + sign2 * normal.x * normal.x * a, sign2 * b, -sign2 * normal.x);
    binormal = float3(b, sign2 + normal.y * normal.y * a, -normal.y);
}

void OrthonormalBasisFrisvad(const float3 normal, out float3 tangent, out float3 binormal)
{
    if(normal.z  < -0.99999)
    {
        tangent = float3(0, -1, 0);
        binormal = float3(-1, 0, 0);
        return;
    }

    float a = 1 / (1 + normal.z);
    float b = -normal.x * normal.y * a;

    tangent = float3(1 - normal.x * normal.x * a, b, -normal.x);
    binormal = float3(b, 1 - normal.y * normal.y * a, -normal.y);
}


float3 HenyeyGreenstein(const float3 omega, const float g, inout uint rngState) {
    float cos_theta;
    float u1 = RandomFloat01(rngState);
    float u2 = RandomFloat01(rngState);

    if (abs(g) < 0.01f) 
    {
        // Isotropic case
        cos_theta = 1.0f - 2.0f * u1;
    }
    else 
    {
        float gSq = g * g;
        
        float sqr_term = (1.0f - gSq) / (1.0f + g - 2.0f * g * u1);
        cos_theta = -(1.0f + gSq - sqr_term * sqr_term) / (2.0f * g);
    }
    float sin_theta = sqrt(max(1.0f - cos_theta * cos_theta, 0.0f));

    float phi = K_TWO_PI * u2;
    float sin_phi, cos_phi;
    sincos(phi, sin_phi, cos_phi);

    float3 direction = float3(sin_theta * cos_phi, sin_theta * sin_phi, cos_theta);
    float3 v1, v2;
    OrthonormalBasisFrisvad(omega, v1, v2);

    return LocalToWorld(direction, v1, v2, omega);
}

// NOTE: The following code is technically capable of rendering non-homogeneous volumes. However, the problem is sampling density.
// Since I'm using Unity's RTShaders for this, only information about the current material is available. This makes sampling mediums-within-mediums impossible like this.
// This is mainly a problem with density texture references, but also sampling logic (transformations between spaces, scaling, etc)
// While this is probably possible to fix, perhaps by storing a 3D Array of textures for all mediums in the scene, 
// I chose to simplify it to only allow homogeneous materials with default shaders, especially since heterogeneous scattering is very expensive. 

// In case you need to extend it for heterogeneous, you should add a "_Homogeneous" property to the shader to allow you to switch code paths here.
// I've made it so macros like PT_SAMPLE_VOLUME_DENSITY are used to define how to sample a point in space. You can use it to get hacky heterogeneous scattering if really necessary.

void CalculateVolumetrics(inout RayPayload payload) 
{ 
#if DISABLE_VOLUMETRICS
    return;
#else
    float3 newOrigin = payload.bounceRayOrigin;
    float3 newDirection = payload.bounceRayDirection;
    
    MediumData medium = GetCurrentMaterial(payload);
    
    bool wasScattered = false;
    // Is the ray already in a medium?
    if (GetCurrentMaterialId(payload) != 0)
    {
        float3 t0 = WorldRayOrigin();
        float3 t1 = payload.bounceRayOrigin;
        float dist = distance(t1, t0);
        
        float3 transparency = 1;
        
        // Homogeneous check (homogeneous is preferred as it's much simpler)
        if (medium.mediumHomogeneous == 1)
        {
            // Random scatter distance
            float scatter_distance = -log(RandomFloat01(payload.rngState)) / medium.mediumScatteringCoeff;
            float dist_travelled = min(dist, scatter_distance);
            
            float3 extinctionCoeff = medium.mediumColor + medium.mediumScatteringCoeff * g_volumetricExtinctionBoost;
            float3 sample_attenuation = exp(-dist_travelled * extinctionCoeff);
            transparency *= sample_attenuation;
            
            float3 pointEmission = medium.mediumEmission;
            payload.emission += pointEmission * dist_travelled;
            
            // Scatter if scatter distance is within and max depth allows it
            if (scatter_distance < dist && payload.depthVolume < g_maxDepthVolume)
            {
                wasScattered = true;
                newOrigin = WorldRayOrigin() + scatter_distance * WorldRayDirection();
                newDirection = HenyeyGreenstein(WorldRayDirection(), medium.mediumAnisotropy, payload.rngState);
                payload.t = dist_travelled;
                payload.depthVolume++;
            }
        }
        else
        {
            int n = 0;
            
            uint max_steps = g_maxMarchingSamples;
            float step_size = dist / max_steps;
            step_size = max(step_size, g_minStepSize);
            
            // Heterogeneous media works similiarly to homogeneous but path is subdivided into steps, and density is sampled at each step
            while (n < max_steps && !wasScattered)
            {
                float random_stepsize_offset = RandomFloat01(payload.rngState) * step_size;
                float t = random_stepsize_offset + step_size * (n + 0.5);
                t = max(t, 0);
                
                float3 sample_pos = WorldRayOrigin() + t * WorldRayDirection();
                
                float3 extinctionCoeff = medium.mediumColor * PT_SAMPLE_VOLUME_COLOR(sample_pos) + (medium.mediumScatteringCoeff * g_volumetricExtinctionBoost);
                float pointDensity = PT_SAMPLE_VOLUME_DENSITY(sample_pos);
                float3 sample_attenuation = exp(-step_size * extinctionCoeff * pointDensity);
                transparency *= sample_attenuation;
                
                float3 pointEmission = medium.mediumEmission * PT_SAMPLE_VOLUME_EMISSION(sample_pos);
                payload.emission += pointEmission;
                
                float sample_scattering = exp(-step_size * medium.mediumScatteringCoeff * pointDensity);
                float scatter_chance = RandomFloat01(payload.rngState);
                bool shouldScatter = scatter_chance < (1 - sample_scattering);
                
                if (shouldScatter && payload.depthVolume < g_maxDepthVolume)
                {
                    wasScattered = true;
                    float scatter_dist_relative = scatter_chance / (1 - sample_scattering);
                    float next_t = random_stepsize_offset + step_size * (n + 1.5);
                    
                    newOrigin = lerp(sample_pos, WorldRayOrigin() + next_t * WorldRayDirection(), 1 - scatter_dist_relative);
                    newDirection = HenyeyGreenstein(WorldRayDirection(), medium.mediumAnisotropy, payload.rngState);
                    payload.t = next_t;
                    payload.depthVolume++;
                }
                
                // Russian roulette to terminate paths with no energy
                // (mostly useful for thick smoke)
                int d = 2;
                if (all(transparency < 1e-3)) 
                {
                    if (RandomFloat01(payload.rngState) > 1.0 / d)
                        break;
                    else
                        transparency *= d;
                }
                n++;
            }
        }
        payload.energyLoss *= transparency;
    }
    payload.wasScattered = wasScattered;
    
    payload.bounceRayOrigin = newOrigin;
    payload.bounceRayDirection = newDirection;
#endif
}
