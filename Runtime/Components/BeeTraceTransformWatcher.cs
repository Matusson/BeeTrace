using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Used to reset rendering if a transform is modified.
/// </summary>
[AddComponentMenu("BeeTrace/Components/BeeTrace Transform Watcher")]
public class BeeTraceTransformWatcher : MonoBehaviour
{
    public bool displayUi = true;

    private Vector3 _lastTranslation;
    private Quaternion _lastRotation;

    private Camera _relatedCamera;
    private BeeTraceManager _manager;

    private void Reset()
    {
        _relatedCamera = GetComponent<Camera>();

        displayUi = _relatedCamera != null;
    }

    private void Start()
    {
        _lastTranslation = transform.position;
        _lastRotation = transform.rotation;
        _manager = FindObjectsByType<BeeTraceManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)[0];
        _relatedCamera = GetComponent<Camera>();
    }

    public void Update()
    {
        Vector3 currentTranslation = transform.position;
        if (math.length(currentTranslation - _lastTranslation) > 0.00001 || _lastRotation != transform.rotation)
        {
            _lastTranslation = currentTranslation;
            _lastRotation = transform.rotation;
            _manager.ForceReset();
        }

        if (_relatedCamera != null)
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                _manager.ForceReset(true);
            }
        }
    }

    private void OnGUI()
    {
        if (!displayUi || _relatedCamera == null)
            return;

        Rect buttonRect = new(Screen.width - 100 - 10, Screen.height - 30 - 10, 100, 30);

        if (GUI.Button(buttonRect, "Reset"))
        {
            _manager.ForceReset(true);
        }
    }
}
