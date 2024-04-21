using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BasePostProcess : MonoBehaviour
{
    public abstract int GetPriority();

    public abstract void Process(RenderTexture source, RenderTexture dest);
}
