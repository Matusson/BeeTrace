// Most of this code comes from TrueTrace by Pjbomb2: https://github.com/Pjbomb2/TrueTrace-Unity-Pathtracer
// as well as Joe Schutte's blog: https://schuttejoe.github.io/post/disneybsdf/
// If you're interested in details on DisneyBSDF, please check these sources.

float4 CalculateLobePdfs(const MaterialData hitData)
{
    float metallicBRDF = hitData.metallic;
    float specularBSDF = (1.0f - hitData.metallic) * hitData.specTrans;
    float dielectricBRDF = (1.0f - hitData.specTrans) * (1.0f - hitData.metallic);
    
    // Components in order:  Specular, Clearcoat, Diffuse, Transmission
    float4 P = float4(metallicBRDF + hitData.specular, 1.0f * saturate(hitData.clearcoat), dielectricBRDF, specularBSDF);

    float norm = 1.0f / (P.x + P.y + P.z + P.w);
    return P * norm;
}


// --- DIFFUSE

float EvaluateDisneyRetroDiffuse(const MaterialData hitData, const float3 wo, const float3 wm, const float3 wi)
{
    float dotNL = AbsCosTheta(wi);
    float dotNV = AbsCosTheta(wo);

    float roughness = hitData.roughness * hitData.roughness;

    float rr = 0.5f + 2.0f * dotNL * dotNL * roughness;
    float fl = SchlickWeight(dotNL);
    float fv = SchlickWeight(dotNV);

    return rr * (fl + fv + fl * fv * (rr - 1.0f));
}

float3 EvaluateSheen(const MaterialData hitData, const float3 wo, const float3 wm, const float3 wi)
{
    if (hitData.sheen <= 0.0f) 
    {
        return 0;
    }

    float dotHL = dot(wm, wi);
    float3 tint = CalculateTint(hitData.surfaceColor);
    return hitData.sheen * lerp(1.0f, tint, hitData.sheenTint) * SchlickWeight(dotHL);
}

float EvaluateDisneyDiffuse(const MaterialData hitData, const float3 wo, const float3 wm, const float3 wi)
{
    float dotNL = AbsCosTheta(wi);
    float dotNV = AbsCosTheta(wo);

    float fl = SchlickWeight(dotNL);
    float fv = SchlickWeight(dotNV);

    float hanrahanKrueger = 0.0f;

    // Thin SSS approximation (as otherwise SSS is calculated by sending a ray inside, which is impossible for thin materials)
    if (hitData.thin == 1 && hitData.flatness > 0.0f) 
    {
        float roughness = hitData.roughness * hitData.roughness;

        float dotHL = dot(wm, wi);
        float fss90 = dotHL * dotHL * roughness;
        float fss = lerp(1.0f, fss90, fl) * lerp(1.0f, fss90, fv);

        float ss = 1.25f * (fss * (1.0f / (dotNL + dotNV) - 0.5f) + 0.5f);
        hanrahanKrueger = ss;
    }

    float lambert = 1.0f;
    float retro = EvaluateDisneyRetroDiffuse(hitData, wo, wm, wi);
    float subsurfaceApprox = lerp(lambert, hanrahanKrueger, hitData.thin == 1 ? hitData.flatness : 0.0f);

    return rcp(K_PI) * (retro + subsurfaceApprox * (1.0f - 0.5f * fl) * (1.0f - 0.5f * fv));
}

bool SampleDisneyDiffuse(const MaterialData hitData, RayPayload ray, const float3 v, const float3x3 tnb, inout BsdfSample sample, inout bool refracted)
{
    float3 wo = ToLocal(tnb, v); // NDotL = L.z; NDotV = V.z; NDotH = H.z
    float sig = sign(CosTheta(wo));

    // -- Sample cosine lobe
    float2 rand = float2(RandomFloat01(ray.rngState), RandomFloat01(ray.rngState));
    float3 wi = CosineSampleHemisphere(rand);
    float3 wm = normalize(wi + wo);

    float dotNL = CosTheta(wi);
    if (dotNL == 0.0f) 
    {
        sample.forwardPdfW = 0.0f;
        sample.reversePdfW = 0.0f;
        sample.reflectance = 0;
        sample.wi = 0;
        return false;
    }

    float dotNV = CosTheta(wo);

    float3 color = hitData.surfaceColor;
    float pdf = 1;
    sample.inMedium = 0;

    // SSS
    float p = RandomFloat01(ray.rngState);
    if (p <= hitData.diffTrans) 
    {
        wi = -wi;
        pdf = hitData.diffTrans;
        refracted = true;

        if (hitData.thin == 1)
        {
            color = sqrt(color);
        }
        else
        {
            sample.inMedium = 1;
        }
    }
    else
    {
        pdf = (1.0f - hitData.diffTrans);
    }

    float3 sheen = EvaluateSheen(hitData, wo, wm, wi);
    float diffuse = EvaluateDisneyDiffuse(hitData, wo, wm, wi) * pdf; // Not quite sure if multiplying by PDF here is right, but otherwise it adds energy
    // if diffTrans is between 0-1 (but not exactly 0 or 1)
    
    sample.reflectance = (sheen + color * (diffuse / pdf));
    sample.wi = normalize(ToWorld(tnb, wi));
    sample.forwardPdfW = abs(dotNL) * pdf;
    sample.reversePdfW = abs(dotNV) * pdf;
    return true;
}



// --- BRDF

static bool SampleDisneyBRDF(const MaterialData hitData, RayPayload ray, const float3 v, const float3x3 tnb, inout BsdfSample sample)
{
    float3 wo = ToLocal(tnb, v); // NDotL = L.z; NDotV = V.z; NDotH = H.z

    // -- Calculate Anisotropic params
    float ax, ay;
    CalculateAnisotropicParams(hitData.roughness, hitData.aniso, ax, ay);

    // -- Sample visible distribution of normals
    float r0 = RandomFloat01(ray.rngState);
    float r1 = RandomFloat01(ray.rngState);
    float3 wm = SampleGgxVndfAnisotropic(wo, ax, ay, r0, r1);

    // -- Reflect over wm
    float3 wi = normalize(reflect(-wo, wm));
    if (CosTheta(wi) <= 0.0f) 
    {
        sample.forwardPdfW = 0.0f;
        sample.reversePdfW = 0.0f;
        sample.reflectance = 0;
        sample.wi = 0;
        return false;
    }

    // -- Fresnel term for this lobe is complicated since we're blending with both the metallic and the specularTint
    // -- parameters plus we must take the IOR into account for dielectrics
    float3 F = DisneyFresnel(hitData, wo, wm, wi);

    // -- Since we're sampling the distribution of visible normals the pdf cancels out with a number of other terms.
    // -- We are left with the weight G2(wi, wo, wm) / G1(wi, wm) and since Disney uses a separable masking function
    // -- we get G1(wi, wm) * G1(wo, wm) / G1(wi, wm) = G1(wo, wm) as our weight.
    float G1v = SeparableSmithGGXG1(wo, wm, ay, ax);
    float3 specular = G1v * F;

    sample.inMedium = 0;
    sample.reflectance = specular;
    sample.wi = normalize(ToWorld(tnb, wi));
    GgxVndfAnisotropicPdf(wi, wm, wo, ay, ax, sample.forwardPdfW, sample.reversePdfW);

    sample.forwardPdfW *= (1.0f / (4 * abs(dot(wo, wm))));
    sample.reversePdfW *= (1.0f / (4 * abs(dot(wi, wm))));

    return true;
}


// --- CLEARCOAT

bool SampleDisneyClearcoat(const MaterialData hitData, RayPayload ray, const float3 v, const float3x3 tnb, inout BsdfSample sample)
{
    float3 wo = ToLocal(tnb, v); // NDotL = L.z; NDotV = V.z; NDotH = H.z

    float gloss = lerp(0.1f, 0.001f, hitData.clearcoatGloss);
    float gloss2 = gloss * gloss;

    float r0 = RandomFloat01(ray.rngState);
    float r1 = RandomFloat01(ray.rngState);
    float cosTheta = sqrt(max(1e-6, (1.0f - pow(gloss2, 1.0f - r0)) / (1.0f - gloss2)));
    float sinTheta = sqrt(max(1e-6, 1.0f - cosTheta * cosTheta));
    float phi = K_TWO_PI * r1;

    float3 wm = float3(sinTheta * cos(phi), cosTheta, sinTheta * sin(phi));
    if (dot(wm, wo) < 0.0f) 
    {
        wm = -wm;
    }

    float3 wi = reflect(-wo, wm);

    float clearcoatWeight = hitData.clearcoat;
    float clearcoatGloss = hitData.clearcoatGloss;

    float dotNH = CosTheta(wm);
    float dotLH = dot(wm, wi);

    float d = GTR1(abs(dotNH), lerp(0.1f, 0.001f, clearcoatGloss));
    float FH = SchlickWeight(dotLH);
    float f = lerp(0.04f, 1.0f, FH);
    float g = SeparableSmithGGXG1(wi, 0.25f) * SeparableSmithGGXG1(wo, 0.25f);

    float fPdf = d / (4.0f * dot(wo, wm));

    sample.reflectance = (0.25f * clearcoatWeight * g * f * d) / fPdf;
    sample.wi = normalize(ToWorld(tnb, wi));
    sample.forwardPdfW = fPdf;
    sample.reversePdfW = d / (4.0f * dot(wi, wm));

    return true;
}


//--- TRANSMISSION

static bool SampleDisneySpecTransmission(const MaterialData hitData, in RayPayload ray, float3 v, inout BsdfSample sample, float3x3 tnb, out float refracted)
{
    float3 wo = ToLocal(tnb, v); // NDotL = L.z; NDotV = V.z; NDotH = H.z
    refracted = false;
    if (CosTheta(wo) == 0.0) 
    {
        sample.forwardPdfW = 0.0f;
        sample.reversePdfW = 0.0f;
        sample.reflectance = 0;
        sample.wi = 0;
        return false;
    }

    // -- Scale roughness based on IOR
    float rscaled = hitData.thin == 1 ? ThinTransmissionRoughness(hitData.ior, hitData.roughness) : hitData.roughness;

    // -- Aniso
    float tax, tay;
    CalculateAnisotropicParams(rscaled, hitData.aniso, tax, tay);

    // -- Sample visible distribution of normals
    float r0 = RandomFloat01(ray.rngState);
    float r1 = RandomFloat01(ray.rngState);
    float3 wm = SampleGgxVndfAnisotropic(wo, tax, tay, r0, r1);
    
    float dotVH = dot(wo, wm);
    if (wm.y < 0.0f) 
    {
        dotVH = -dotVH;
    }

    // -- Disney uses the full dielectric Fresnel equation for transmission. We also importance sample F
    // -- to switch between refraction and reflection at glancing angles.
    float F = DielectricFresnel(dotVH, 1.0f, hitData.ior);

    // -- Since we're sampling the distribution of visible normals the pdf cancels out with a number of other terms.
    // -- We are left with the weight G2(wi, wo, wm) / G1(wi, wm) and since Disney uses a separable masking function
    // -- we get G1(wi, wm) * G1(wo, wm) / G1(wi, wm) = G1(wo, wm) as our weight.
    float G1v = SeparableSmithGGXG1(wo, wm, tax, tay);

    float pdf = 1;
    refracted = false;

    float3 wi;
    if (RandomFloat01(ray.rngState) <= F) 
    {
        wi = normalize(reflect(-wo, wm));

        sample.reflectance = G1v * hitData.surfaceColor;

        float jacobian = (4 * abs(dot(wo, wm)));
        pdf = F / jacobian;
    }
    else 
    {
        if (hitData.thin == 1) 
        {
            // -- When the surface is thin so it refracts into and then out of the surface during this shading event.
            // -- So the ray is just reflected then flipped and we use the sqrt of the surface color.
            wi = reflect(-wo, wm);
            wi.y = -wi.y;
            refracted = true;
            sample.reflectance = G1v * sqrt(hitData.surfaceColor);

            // -- Since this is a thin surface we are not ending up inside of a volume so we treat this as a scatter event.
            sample.inMedium = 0;
        }
        else 
        {
            if (Transmit(wm, wo, hitData.relativeIor, wi))
            {
                sample.inMedium = 1;
                refracted = true;
            }
            else 
            {
                sample.inMedium = 0;
                wi = reflect(-wo, wm);
            }

            sample.reflectance = G1v * hitData.surfaceColor;
        }

        wi = normalize(wi);

        float dotLH = abs(dot(wi, wm));
        float jacobian = dotLH / (pow(dotLH + hitData.relativeIor * dotVH, 2));
        pdf = (1.0f - F) / jacobian;
    }

    if (CosTheta(wi) == 0.0f) 
    {
        sample.forwardPdfW = 0.0f;
        sample.reversePdfW = 0.0f;
        sample.reflectance = 0;
        sample.wi = 0;
        refracted = false;
        return false;
    }

    // -- calculate VNDF pdf terms and apply Jacobian and Fresnel sampling adjustments
    GgxVndfAnisotropicPdf(wi, wm, wo, tax, tay, sample.forwardPdfW, sample.reversePdfW);
    sample.forwardPdfW *= pdf;
    sample.reversePdfW *= pdf;
    
    // -- convert wi back to world space
    sample.wi = normalize(ToWorld(tnb, wi));

    return true;
}



// --- SAMPLE

bool SampleDisney(inout RayPayload ray, inout MaterialData hitData, const float3 v, const float3x3 tnbMat, inout BsdfSample sample)
{
    hitData.surfaceColor *= K_PI;

    // Sample probabilities for hit point
    float4 P = CalculateLobePdfs(hitData);
    float pSpecular = P.x;
    float pClearcoat = P.y;
    float pDiffuse = P.z;
    float pTransmission = P.w;
    
    // Generate a random number and select a ray type to sample
    float pLobe = 0.0;
    bool refracted = false;
    float p = RandomFloat01(ray.rngState);

    // SPECULAR BRDF
    if (p <= pSpecular) 
    {
        hitData.surfaceColor /= K_PI;
        SampleDisneyBRDF(hitData, ray, v, tnbMat, sample);
        pLobe = pSpecular;
    }
    
    // CLEARCOAT
    else if (p <= (pSpecular + pClearcoat)) 
    {
        SampleDisneyClearcoat(hitData, ray, v, tnbMat, sample);
        pLobe = pClearcoat;
    }
    
    // DIFFUSE
    else if (p <= (pSpecular + pClearcoat + pDiffuse)) 
    {
        SampleDisneyDiffuse(hitData, ray, v, tnbMat, sample, refracted);
        pLobe = pDiffuse;
    }
    
    // TRANSMISSION
    else
    {
        hitData.surfaceColor /= K_PI;
        SampleDisneySpecTransmission(hitData, ray, v, sample, tnbMat, refracted);
        pLobe = pTransmission;
    }


    if (pLobe > 0.0f) 
    {
        sample.reflectance = sample.reflectance * (1.0f / pLobe);
        sample.forwardPdfW *= pLobe;
        sample.reversePdfW *= pLobe;
    }

    return refracted;
}