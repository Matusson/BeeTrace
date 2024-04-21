inline bool RaySphereIntersection(in RayDesc ray, in PointLightData light, out float intersectT)
{
    float sphereRadius = light.radius;

    float a = dot(ray.Direction, ray.Direction);
    float3 s0_r0 = ray.Origin - light.position;
    float b = 2.0 * dot(ray.Direction, s0_r0);
    float c = dot(s0_r0, s0_r0) - (sphereRadius * sphereRadius);
    if (b * b - 4.0 * a * c < 0.0)
    {
        intersectT = -1;
        return false;
    }
    intersectT = (-b - sqrt((b * b) - 4.0 * a * c)) / (2.0 * a);
    
    return intersectT > 0;
}

inline bool RaySpotlightIntersection(in RayDesc ray, in SpotLightData light, out float intersectT)
{
    // First need to intersect the sphere
    float sphereRadius = light.radius;

    float a = dot(ray.Direction, ray.Direction);
    float3 s0_r0 = ray.Origin - light.position;
    float b = 2.0 * dot(ray.Direction, s0_r0);
    float c = dot(s0_r0, s0_r0) - (sphereRadius * sphereRadius);
    if (b * b - 4.0 * a * c < 0.0)
    {
        intersectT = -1;
        return false;
    }
    intersectT = (-b - sqrt((b * b) - 4.0 * a * c)) / (2.0 * a);

    if (intersectT < 0)
        return false;
    
    float3 rayDir = -ray.Direction;

    // Then need to be aligned with the direction vector
    float angleBetween = acos(dot(rayDir, light.dir));
    if (angleBetween < light.angle)
        return true;
    else
        return false;
}
