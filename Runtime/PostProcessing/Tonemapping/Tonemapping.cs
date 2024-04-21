using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class Tonemapping : BasePostProcess
{
    public float exposure = 1f;
    public float contrast = 1f;
    public float brightness = 0f;

    [Space]
    public TonemapperType type = TonemapperType.AGX;
    public float whitePoint = 5f;

    [SerializeField]
    private Material tonemapShader;

    private LocalKeyword linearKw;
    private LocalKeyword reinhardKw;
    private LocalKeyword filmicKw;
    private LocalKeyword acesKw;
    private LocalKeyword agxKw;
    private LocalKeyword agxPunchyKw;


    public enum TonemapperType
    {
        Linear,
        Reinhard,
        Filmic,
        ACES,
        AGX,
        AGXPunchy
    }

    private void Start()
    {
        linearKw = new LocalKeyword(tonemapShader.shader, "MODE_LINEAR");
        reinhardKw = new LocalKeyword(tonemapShader.shader, "MODE_REINHARD");
        filmicKw = new LocalKeyword(tonemapShader.shader, "MODE_FILMIC");
        acesKw = new LocalKeyword(tonemapShader.shader, "MODE_ACES");
        agxKw = new LocalKeyword(tonemapShader.shader, "MODE_AGX");
        agxPunchyKw = new LocalKeyword(tonemapShader.shader, "MODE_AGX_PUNCHY");
    }

    private void Reset()
    {
#if UNITY_EDITOR
        tonemapShader = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.matusson.beetrace/Runtime/Shaders/Tonemapping/TonemapMat.mat");
#endif
    }

    public override int GetPriority()
    {
        return 0;
    }

    private void SetTonemapperMode()
    {
        tonemapShader.SetKeyword(linearKw,      type == TonemapperType.Linear);
        tonemapShader.SetKeyword(reinhardKw,    type == TonemapperType.Reinhard);
        tonemapShader.SetKeyword(filmicKw,      type == TonemapperType.Filmic);
        tonemapShader.SetKeyword(acesKw,        type == TonemapperType.ACES);
        tonemapShader.SetKeyword(agxKw,         type == TonemapperType.AGX);
        tonemapShader.SetKeyword(agxPunchyKw,   type == TonemapperType.AGXPunchy);
    }

    public override void Process(RenderTexture source, RenderTexture destination)
    {
        SetTonemapperMode();

        tonemapShader.SetFloat("whiteness", whitePoint);
        tonemapShader.SetFloat("exposure", exposure);
        tonemapShader.SetFloat("contrast", contrast);
        tonemapShader.SetFloat("brightness", brightness);

        Graphics.Blit(source, destination, tonemapShader);
    }
}
