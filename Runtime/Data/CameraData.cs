using Unity.Mathematics;
using UnityEngine;

public struct CameraData
{
    public float4x4 projectionMatrix;
    public float4x4 projectionMatrixInverse;
    public float4x4 cameraToWorldMatrix;
    public float4x4 localToWorldMatrix;

    public float3 origin;
    public float3 forwardDirection;
    public float3 upDirection;

    public CameraData(Camera unityCamera)
    {
        projectionMatrix = unityCamera.projectionMatrix;
        cameraToWorldMatrix = unityCamera.cameraToWorldMatrix;
        localToWorldMatrix = unityCamera.transform.localToWorldMatrix;

        projectionMatrixInverse = math.inverse(projectionMatrix);

        origin = unityCamera.transform.position;
        forwardDirection = unityCamera.transform.forward;
        upDirection = unityCamera.transform.up;
    }
}
