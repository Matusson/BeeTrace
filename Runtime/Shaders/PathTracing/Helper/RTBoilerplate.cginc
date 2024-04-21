#define INTERPOLATE_ATTRIBUTE(attr) v.attr = v0.attr * barycentrics.x + v1.attr * barycentrics.y + v2.attr * barycentrics.z\

struct AttributeData
{
    float2 barycentrics;
};

struct Vertex
{
    float3 position;
    float3 tangent;
    float3 normal;
    float2 uv;
};

Vertex FetchVertex(uint vertexIndex)
{
    Vertex v;
    v.position = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributePosition);
    v.normal = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributeNormal);
    v.tangent = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributeTangent);
    v.uv = UnityRayTracingFetchVertexAttribute2(vertexIndex, kVertexAttributeTexCoord0);
    return v;
}

Vertex InterpolateVertices(Vertex v0, Vertex v1, Vertex v2, float3 barycentrics)
{
    Vertex v;
    INTERPOLATE_ATTRIBUTE(position);
    INTERPOLATE_ATTRIBUTE(normal);
    INTERPOLATE_ATTRIBUTE(tangent);
    INTERPOLATE_ATTRIBUTE(uv);
    return v;
}


void GetHitPosNormals(const AttributeData attribs, out Vertex v, out float3 posObjectSpace, out float3 normalObjectSpace, out bool isFrontFace)
{
    uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());

    Vertex v0, v1, v2;
    v0 = FetchVertex(triangleIndices.x);
    v1 = FetchVertex(triangleIndices.y);
    v2 = FetchVertex(triangleIndices.z);

    float3 barycentricCoords = float3(1.0 - attribs.barycentrics.x - attribs.barycentrics.y, attribs.barycentrics.x, attribs.barycentrics.y);
    v = InterpolateVertices(v0, v1, v2, barycentricCoords);
                
    isFrontFace = HitKind() == HIT_KIND_TRIANGLE_FRONT_FACE;
    posObjectSpace = v.position;
    normalObjectSpace = v.normal;
}