using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Data storage object that stores information related to the background.
/// </summary>
[AddComponentMenu("BeeTrace/Managers/BeeTrace World")]
public class BeeTraceWorld : MonoBehaviour
{
    [Tooltip("HDR image that can be used as environment lighting.")]
    public Texture environment;
    [Tooltip("Rotation expressed in angles applied to the environment texture.")]
    public float3 environmentRotation;

    [Space]
    [Tooltip("Color to use if not environment texture specified")]
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.1f);

    [Space]
    [Tooltip("Multiplier applied to environment lighting.")]
    public float environmentStrength = 1f;

    [SerializeField, HideInInspector]
    internal Texture _envPlaceholder;

    public void Reset()
    {
#if UNITY_EDITOR
        _envPlaceholder = AssetDatabase.LoadAssetAtPath<Texture>("Packages/com.matusson.beetrace/Runtime/External/env_placeholder.jpg");
#endif
    }

}
