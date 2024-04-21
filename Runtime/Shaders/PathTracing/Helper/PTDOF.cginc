// Parameters
float dofFocalLength;
float dofAperture;
int dofBladeCount;
Texture2D<float> dofBokehTex;
SamplerState linearClampSampler;

float2 SampleWithinCircle(inout uint rngState)
{
    float angle = RandomFloat01(rngState) * K_TWO_PI;
    float radius = sqrt(RandomFloat01(rngState));
    return float2(cos(angle), sin(angle)) * radius * dofAperture;
}

float2 SampleWithinNgon(inout uint rngState, float sides)
{
    float offset = K_PI / sides;

    float angle = RandomFloat01(rngState) * K_TWO_PI;
    //float max_radii = 1.0 / sqrt(2) / cos(K_PI / 8 - angle % (K_PI / 4));
    float max_radii = 1.0 / cos(abs(angle % (K_PI / (sides / 2)) - offset));
    float radii = sqrt(RandomFloat01(rngState) * max_radii);

    return float2(radii * cos(angle), radii * sin(angle)) * dofAperture;
}

// Custom kernel based on rejection-sampling a texture
float2 SampleWithinTexture(inout uint rngState)
{
    float2 offset;
    for (int i = 0; i < 256; i++)
    {
        offset = float2(RandomFloat01(rngState), RandomFloat01(rngState)) - float2(0.5, 0.5);
        float value = dofBokehTex.SampleLevel(linearClampSampler, offset * 2, 0);

        if (value > 0.5)
            break;
    }

    return offset * dofAperture;
}

void ApplyDof(inout RayDesc ray, in CamData cam, inout uint rngState)
{
    float3 focalPoint = ray.Origin + ray.Direction * dofFocalLength;
    float2 offset = SampleWithinNgon(rngState, dofBladeCount);

    ray.Origin += cam.upDirection * offset.y;
    ray.Origin += cross(cam.upDirection, cam.forwardDirection) * offset.x;

    // Calculate the direction that passes through the focal point
    ray.Direction = normalize(focalPoint - ray.Origin);
}