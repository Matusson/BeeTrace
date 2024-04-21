// Most of this code comes from TrueTrace by Pjbomb2: https://github.com/Pjbomb2/TrueTrace-Unity-Pathtracer
// as well as Joe Schutte's blog: https://schuttejoe.github.io/post/disneybsdf/
// If you're interested in details on DisneyBSDF, please check these sources.

struct MaterialData
{
    float4 surfaceColor;
    float3 emission;
    float3 transmittanceColor;
    
    float roughness;
    float metallic;
    
    float sheen;
    float sheenTint;
    float ior;
    float relativeIor;
    float specular;
    float specularTint;
    float clearcoat;
    float clearcoatGloss;
    float aniso;
    
    float thin;
    float flatness;

    float diffTrans;
    float specTrans;
    float scatterDistance;
};

// Initializes a surface from shader properties
MaterialData InitializeSurface(float2 uv)
{
    MaterialData surface;
    float4 col;
#if _ALBEDO_MAP_ASSIGNED
    col = _AlbedoTex.SampleLevel(sampler__AlbedoTex, uv * _AlbedoTex_ST.xy + _AlbedoTex_ST.zw, 0);
    col *= _AlbedoTint;
#else
    col = _AlbedoTint;
#endif
    surface.surfaceColor = col;
    surface.transmittanceColor = (1 - col) * _TransmissiveColor;
    
#if _EMISSION_MAP_ASSIGNED
    float4 emCol = _EmissionTex.SampleLevel(sampler__EmissionTex, uv * _EmissionTex_ST.xy + _EmissionTex_ST.zw, 0);
    surface.emission = emCol * _EmissionTint;
#else
    surface.emission = _EmissionTint;
#endif

#if _USE_MASK_MAP
    #if _MASK_MAP_ASSIGNED
        float4 maskVal = _MaskTex.SampleLevel(sampler__MaskTex, uv * _MaskTex_ST.xy + _MaskTex_ST.zw, 0);
        float roughness = 1 - maskVal.w;
        float metallic = maskVal.x;
    
        surface.roughness = map(roughness, 0, 1, _RoughnessMinMax.x, _RoughnessMinMax.y);
        surface.metallic = map(metallic, 0, 1, _MetallicMinMax.x, _MetallicMinMax.y);
    #else
        surface.roughness = _RoughnessBase;
        surface.metallic = _MetallicBase;
    #endif
    
    #else // NO MASK MAP
        #if _ROUGHNESS_MAP_ASSIGNED
            float roughness = _RoughnessTex.SampleLevel(sampler__RoughnessTex, uv * _RoughnessTex_ST.xy + _RoughnessTex_ST.zw, 0);
            surface.roughness = map(roughness, 0, 1, _RoughnessMinMax.x, _RoughnessMinMax.y);
        #else    
            surface.roughness = _RoughnessBase;
        #endif

    #if _METALLIC_MAP_ASSIGNED
        float metallic = _MetallicTex.SampleLevel(sampler__MetallicTex, uv * _MetallicTex_ST.xy + _MetallicTex_ST.zw, 0);
        surface.metallic = map(metallic, 0, 1, _MetallicMinMax.x, _MetallicMinMax.y);
    #else    
        surface.metallic = _MetallicBase;
    #endif
#endif // NO MASK MAP END
    

    surface.specular = _Specular * 0.08; //Rescaled to Blender scale
    surface.ior = (2 * _Specular + 10 * SQRT2 * sqrt(_Specular) + 25) / (25 - _Specular);
    surface.specTrans = _Transmission * _TransmissionSpecularity;
    surface.diffTrans = _Transmission * (1 - _TransmissionSpecularity);
    surface.scatterDistance = _TransmissiveScattering;

    surface.aniso = _Anisotropy;


    surface.clearcoat = _Clearcoat;
    surface.clearcoatGloss = _ClearcoatGloss;

    surface.sheen = _Sheen;
    surface.sheenTint = _SheenTint;

    surface.specularTint = _SpecularTint;

    surface.thin = _Thin;
    surface.flatness = _Flatness;

    return surface;
}

struct BsdfSample
{
    float forwardPdfW;
    float reversePdfW;
    
    float3 reflectance;
    float3 wi;
    
    int inMedium;
};

BsdfSample InitNewSample()
{
    BsdfSample result;
    result.forwardPdfW = 0;
    result.reversePdfW = 0;
    result.reflectance = 0;
    result.wi = 0;
    result.inMedium = 0;
    return result;
}

// --- TRANSMITTANCE
bool Transmit(float3 wm, const float3 wi, const float n, inout float3 wo)
{
    float c = dot(wi, wm);
    if (c < 0.0f)
    {
        c = -c;
        wm = -wm;
    }

    float root = 1.0f - n * n * (1.0f - c * c);
    if (root <= 0)
        return false;

    wo = (n * c - sqrt(root)) * wm - n * wi;
    return true;
}

float ThinTransmissionRoughness(const float ior, const float roughness)
{
    // -- Disney scales by (.65 * eta - .35) based on figure 15 of the 2015 PBR course notes. Based on their figure
    // -- the results match a geometrically thin solid fairly well.
    return saturate((0.65f * ior - 0.35f) * roughness);
}

float3 CalculateTint(const float3 baseColor)
{
    // -- The color tint is never mentioned in the SIGGRAPH presentations as far as I recall but it was done in
    // --  the BRDF Explorer so I'll replicate that here.
    float luminance = dot(float3(0.3f, 0.6f, 1.0f), baseColor);
    return (luminance > 0.0f) ? (baseColor * (1.0f / luminance)) : 1;
}


// --- FRESNEL
float3 Schlick(const float3 r0, const float rad)
{
    float exponential = pow(1.0f - rad, 5.0f);
    return r0 + (1.0f - r0) * exponential;
}

float SchlickWeight(const float u)
{
    float m = saturate(1.0f - u);
    float m2 = m * m;
    return m * m2 * m2;
}

float SchlickR0FromRelativeIOR(const float eta)
{
    // https://seblagarde.wordpress.com/2013/04/29/memo-on-fresnel-equations/
    return pow(eta - 1.0f, 2) / pow(eta + 1.0f, 2);
}

float DielectricFresnel(float cosThetaI, float ni, float nt)
{
    // Copied from PBRT. This function calculates the full Fresnel term for a dielectric material.
    // https://seblagarde.wordpress.com/2013/04/29/memo-on-fresnel-equations/

    cosThetaI = clamp(cosThetaI, -1.0f, 1.0f);

    // Swap index of refraction if this is coming from inside the surface
    if (cosThetaI < 0.0f)
    {
        float temp = ni;
        ni = nt;
        nt = temp;

        cosThetaI = -cosThetaI;
    }

    float sinThetaI = sqrt(max(0.0f, 1.0f - cosThetaI * cosThetaI));
    float sinThetaT = ni / nt * sinThetaI;

    // Check for total internal reflection
    if (sinThetaT >= 1)
    {
        return 1;
    }

    float cosThetaT = sqrt(max(0.0f, 1.0f - sinThetaT * sinThetaT));

    float rParallel = ((nt * cosThetaI) - (ni * cosThetaT)) / ((nt * cosThetaI) + (ni * cosThetaT));
    float rPerpendicuar = ((ni * cosThetaI) - (nt * cosThetaT)) / ((ni * cosThetaI) + (nt * cosThetaT));
    return (rParallel * rParallel + rPerpendicuar * rPerpendicuar) / 2;
}

float3 DisneyFresnel(MaterialData hitDat, const float3 wo, const float3 wm, const float3 wi)
{
    float dotHV = dot(wm, wo);

    float3 tint = CalculateTint(hitDat.surfaceColor);

    // -- See section 3.1 and 3.2 of the 2015 PBR presentation + the Disney BRDF explorer (which does their 2012 remapping
    // -- rather than the SchlickR0FromRelativeIOR seen here but they mentioned the switch in 3.2).
    float3 R0 = SchlickR0FromRelativeIOR(hitDat.relativeIor) * lerp(1.0f, tint, hitDat.specularTint);
    R0 = lerp(R0, hitDat.surfaceColor, hitDat.metallic);

    float dielectricFresnel = DielectricFresnel(dotHV, 1.0f, hitDat.ior);
    float3 metallicFresnel = Schlick(R0, dot(wi, wm));

    return lerp(dielectricFresnel, metallicFresnel, hitDat.metallic);
}



// --- TRIG HELPERS
float CosTheta(const float3 w)
{
    return w.y;
}

float AbsCosTheta(const float3 w)
{
    return abs(CosTheta(w));
}

float Cos2Theta(const float3 w)
{
    return w.y * w.y;
}
float Sin2Theta(const float3 w)
{
    return max(0.0f, 1.0f - Cos2Theta(w));
}

float SinTheta(const float3 w)
{
    return sqrt(Sin2Theta(w));
}

float TanTheta(const float3 w)
{
    return SinTheta(w) / CosTheta(w);
}

float Tan2Theta(const float3 w)
{
    return Sin2Theta(w) / Cos2Theta(w);
}

float CosPhi(const float3 w)
{
    float sinTheta = SinTheta(w);
    return (sinTheta == 0) ? 1.0f : clamp(w.x / sinTheta, -1.0f, 1.0f);
}

float SinPhi(const float3 w)
{
    float sinTheta = SinTheta(w);
    return (sinTheta == 0) ? 1.0f : clamp(w.z / sinTheta, -1.0f, 1.0f);
}

float Cos2Phi(const float3 w)
{
    float cosPhi = CosPhi(w);
    return cosPhi * cosPhi;
}

float Sin2Phi(const float3 w)
{
    float sinPhi = SinPhi(w);
    return sinPhi * sinPhi;
}


// --- LOBE SAMPLING
float3 CosineSampleHemisphere(float2 coord)
{
    float3 dir;
    float r = sqrt(coord.x);
    float phi = K_TWO_PI * coord.y;
    dir.x = r * cos(phi);
    dir.z = r * sin(phi);
    dir.y = sqrt(max(0.0, 1.0 - dir.x * dir.x - dir.z * dir.z));
    return dir;
}

float GTR1(const float NDotH, const float a)
{
    if (a >= 1.0)
        return rcp(K_PI);
    
    float a2 = a * a;
    float t = 1.0 + (a2 - 1.0) * NDotH * NDotH;
    return (a2 - 1.0) / (K_PI * log(a2) * t);
}

// SmithGGX with 2 overloads depending on anisotropy parameters
float SeparableSmithGGXG1(const float3 w, const float3 wm, float ax, float ay)
{
    float absTanTheta = abs(TanTheta(w));
    if (!(absTanTheta < 0 || absTanTheta > 0 || absTanTheta == 0))
    {
        return 0.0f;
    }

    float a = sqrt(Cos2Phi(w) * ax * ax + Sin2Phi(w) * ay * ay);
    float a2Tan2Theta = pow(a * absTanTheta, 2);

    float lambda = 0.5f * (-1.0f + sqrt(1.0f + a2Tan2Theta));
    return 1.0f / (1.0f + lambda);
}

float SeparableSmithGGXG1(const float3 w, float a)
{
    float a2 = a * a;
    float absDotNV = AbsCosTheta(w);

    return 2.0f / (1.0f + sqrt(a2 + (1 - a2) * absDotNV * absDotNV));
}


// --- ANISO
void CalculateAnisotropicParams(float roughness, float aniso, inout float ax, inout float ay)
{
    float aspect = sqrt(1.0f - 0.9f * aniso);
    ax = max(0.0001f, (roughness * roughness) / aspect);
    ay = max(0.0001f, (roughness * roughness) * aspect);
}


float GgxAnisotropicD(const float3 wm, float ax, float ay)
{
    float dotHX2 = (wm.x * wm.x);
    float dotHY2 = (wm.z * wm.z);
    
    float cos2Theta = Cos2Theta(wm);
    
    float ax2 = (ax * ax);
    float ay2 = (ay * ay);

    return 1.0f / (K_PI * ax * ay * pow(dotHX2 / ax2 + dotHY2 / ay2 + cos2Theta, 2));
}


float3 SampleGgxVndfAnisotropic(const float3 wo, const float ax, const float ay, const float u1, const float u2)
{
    // -- Stretch the view vector so we are sampling as though roughness==1
    float3 v = normalize(float3(wo.x * ax, wo.y, wo.z * ay));

    // -- Build an orthonormal basis with v, t1, and t2
    float3 t1 = (v.y < 0.9999f) ? normalize(cross(v, float3(0, 1, 0))) : float3(1, 0, 0);
    float3 t2 = cross(t1, v);

    // -- Choose a point on a disk with each half of the disk weighted proportionally to its projection onto direction v
    float a = 1.0f / (1.0f + v.y);
    float r = sqrt(u1);
    float phi = (u2 < a) ? (u2 / a) * K_PI : K_PI + (u2 - a) / (1.0f - a) * K_PI;
    float p1 = r * cos(phi);
    float p2 = r * sin(phi) * ((u2 < a) ? 1.0f : v.y);

    // -- Calculate the normal in this stretched tangent space
    float3 n = p1 * t1 + p2 * t2 + sqrt(max(0.0f, 1.0f - p1 * p1 - p2 * p2)) * v;

    // -- unstretch and normalize the normal
    return normalize(float3(ax * n.x, n.y, ay * n.z));
}

void GgxVndfAnisotropicPdf(const float3 wi, const float3 wm, const float3 wo, float ax, float ay,
    inout float forwardPdfW, inout float reversePdfW)
{
    float D = GgxAnisotropicD(wm, ax, ay);

    float absDotNL = AbsCosTheta(wi);
    float absDotHL = abs(dot(wm, wi));
    float G1v = SeparableSmithGGXG1(wo, wm, ax, ay);
    forwardPdfW = G1v * absDotHL * D / absDotNL;
}