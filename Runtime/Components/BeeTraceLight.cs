using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility component for storing extra data for a light, necessary for path tracing.
/// </summary>
[AddComponentMenu("BeeTrace/Components/BeeTrace Light")]
public class BeeTraceLight : MonoBehaviour
{
    [Tooltip("Radius of the light source.")]
    public float radius = 0.1f;
}
