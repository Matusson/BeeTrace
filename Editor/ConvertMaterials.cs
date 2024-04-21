using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ConvertMaterials
{
    [MenuItem("BeeTrace/Convert Materials", priority = 1)]
    public static void Convert()
    {
        string[] targetShaderGuids = AssetDatabase.FindAssets("t:Shader PathTracingDisney");

        if (targetShaderGuids.Length == 0)
        {
            Debug.LogError("No target shader found.");
            return;
        }
        Shader targetShader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(targetShaderGuids[0]));

        // Find materials in the currently loaded scenes
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.InstanceID);
        foreach (Renderer renderer in renderers)
        {
            foreach(var mat in renderer.sharedMaterials)
            {
                // Only support converting BRP Standad shaders
                if (mat.shader.name != "Standard")
                    continue;

                Undo.RecordObject(mat, "Update Material");

                var albedo = mat.GetTexture("_MainTex");
                Vector2 offset = mat.GetTextureOffset("_MainTex");
                Vector2 scale = mat.GetTextureScale("_MainTex");

                var normal = mat.GetTexture("_BumpMap");
                float normalScale = mat.GetFloat("_BumpScale");

                // No roughness map on the original shader
                var metallic = mat.GetTexture("_MetallicGlossMap");
                var emission = mat.GetTexture("_EmissionMap");

                var color = mat.GetVector("_Color");

                // Swap shader
                mat.shader = targetShader;

                mat.SetTexture("_AlbedoTex", albedo);
                mat.SetTextureOffset("_AlbedoTex", offset);
                mat.SetTextureScale("_AlbedoTex", scale);

                mat.SetTexture("_NormalTex", normal);
                mat.SetTextureOffset("_NormalTex", offset);
                mat.SetTextureScale("_NormalTex", scale);
                mat.SetFloat("_NormalScale", normalScale);


                mat.SetTexture("_MetallicMap", metallic);
                mat.SetTextureOffset("_MetallicMap", offset);
                mat.SetTextureScale("_MetallicMap", scale);

                mat.SetTexture("_EmissionMap", emission);
                mat.SetTextureOffset("_EmissionMap", offset);
                mat.SetTextureScale("_EmissionMap", scale);

                mat.SetVector("_AlbedoTint", color);
            }
        }

        //var matsGuid = AssetDatabase.FindAssets("t:Material");
        //for (int m = 0; m < matsGuid.Length; m++)
        //{
        //    string path = AssetDatabase.GUIDToAssetPath(matsGuid[m]);
        //    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        //
        //    // Only convert materials with the "Standard" shader
        //    Shader shader = mat.shader;
        //    if (shader.name != "Standard")
        //        continue;
        //
        //    mat.shader = targetShader;
        //}
        //Debug.Log($"Converted {matsGuid.Length} materials.");
        //
        //AssetDatabase.SaveAssets();
    }
}
