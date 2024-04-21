using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class PTDisneyGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        Material mat = materialEditor.target as Material;

        DrawTextureAlbedoProperty(mat, materialEditor, properties);
        DrawTextureNormalProperty(mat, materialEditor, properties);

        // Mask map toggle
        MaterialProperty useMaskMapProp = FindProperty("_USE_MASK_MAP", properties);
        bool useMaskMap = useMaskMapProp.floatValue == 1;
        useMaskMap = EditorGUILayout.Toggle("Use HDRP Mask map", useMaskMap);
        useMaskMapProp.floatValue = useMaskMap ? 1 : 0;

        LocalKeyword useMaskKw = new(mat.shader, "_USE_MASK_MAP");
        mat.SetKeyword(useMaskKw, useMaskMap);

        if (!useMaskMap)
        {
            DrawTextureRemappableProperty("Roughness", mat, materialEditor, properties);
            DrawTextureRemappableProperty("Metallic", mat, materialEditor, properties);
        }
        else
        {
            DrawTextureMaskMapProperty(mat, materialEditor, properties);
        }
        DrawTextureEmissionProperty(mat, materialEditor, properties);

        EditorGUILayout.Space(20);

        // Spec / IOR
        MaterialProperty specProp = FindProperty("_Specular", properties);
        specProp.floatValue = EditorGUILayout.Slider(new GUIContent("Specular Value (Blender)",
            "Specular value, same as in Blender's Principled Shader." +
            " Affects IOR as well as amount of specular reflections."), specProp.floatValue, 0f, 1.5f);

        MaterialProperty iorProp = FindProperty("_IOR", properties);
        float spec = specProp.floatValue;
        iorProp.floatValue = (2 * spec + 10 * math.SQRT2 * math.sqrt(spec) + 25) / (25 - spec);
        EditorGUILayout.Slider("Index of Refraction (autocalculated)", iorProp.floatValue, 1f, 2.8f);

        EditorGUILayout.Space(20);

        // Aniso
        MaterialProperty anisoProp = FindProperty("_Anisotropy", properties);
        anisoProp.floatValue = EditorGUILayout.Slider(new GUIContent("Anisotropy",
           "Affects the direction light gets scattered to. 0 means isotropic scattering, typical for most materials, while 1 means anisotropic, used for some metals."), anisoProp.floatValue, 0f, 1f);

        EditorGUILayout.Space(20);

        // Sheen
        MaterialProperty sheenProp = FindProperty("_Sheen", properties);
        sheenProp.floatValue = EditorGUILayout.Slider(new GUIContent("Sheen",
           "Simulates thin fibers for cloth materials, or dust."), sheenProp.floatValue, 0f, 1f);

        MaterialProperty sheenTintProp = FindProperty("_SheenTint", properties);
        sheenTintProp.floatValue = EditorGUILayout.Slider(new GUIContent("Sheen Tint",
           "0 means white sheen, 1 means base color sheen."), sheenTintProp.floatValue, 0f, 1f);

        EditorGUILayout.Space(20);

        // Clearcoat
        MaterialProperty clearcoatProp = FindProperty("_Clearcoat", properties);
        clearcoatProp.floatValue = EditorGUILayout.Slider(new GUIContent("Clearcoat",
           "Simulates thin layer of gloss on top of the material, for example on car paint."), clearcoatProp.floatValue, 0f, 1f);

        MaterialProperty clearcoatGlossProp = FindProperty("_ClearcoatGloss", properties);
        clearcoatGlossProp.floatValue = EditorGUILayout.Slider(new GUIContent("Clearcoat Glossiness",
           "Controls the glossiness of the clearcoat layer."), clearcoatGlossProp.floatValue, 0f, 1f);

        EditorGUILayout.Space(20);

        // Transmissive
        EditorGUILayout.LabelField("Transmissive", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Glass and Subsurface Scattering in BeeTrace are simulated with the same method." +
            " To toggle between them, use the Transmission Specularity slider (Glass is specular, SSS is diffuse)", MessageType.Info);

        MaterialProperty transmissiveProp = FindProperty("_Transmission", properties);
        transmissiveProp.floatValue = EditorGUILayout.Slider(new GUIContent("Transmission",
           "Simulates light penetrating the material, for glass and SSS."), transmissiveProp.floatValue, 0f, 1f);
        
        if (transmissiveProp.floatValue > 0)
        {
            MaterialProperty transmissiveSpecProp = FindProperty("_TransmissionSpecularity", properties);
            transmissiveSpecProp.floatValue = EditorGUILayout.Slider(new GUIContent("Transmission Specularity",
               "Changes behaviour of transmission. 0 means diffuse transmission (SSS), 1 means specular transmission (glass)."), transmissiveSpecProp.floatValue, 0f, 1f);

            MaterialProperty transmissiveColProp = FindProperty("_TransmissiveColor", properties);
            transmissiveColProp.colorValue = EditorGUILayout.ColorField(new GUIContent("Transmission Color",
               "Color absorbed in transmission."), transmissiveColProp.colorValue);

            MaterialProperty transmissiveScatterProp = FindProperty("_TransmissiveScattering", properties);
            transmissiveScatterProp.floatValue = EditorGUILayout.Slider(new GUIContent("Transmission Scattering",
               "Controls how easily light gets scattered inside the object. Larger values can be used for frosted glass or diffuse transmission, while lower are for clear glass."), transmissiveScatterProp.floatValue, 0f, 1000f);
        }
        EditorGUILayout.Space(20);

        // Other

        MaterialProperty specTintProp = FindProperty("_SpecularTint", properties);
        specTintProp.floatValue = EditorGUILayout.Slider(new GUIContent("Specular Tint (Non-PBR)",
           "Tints specular light by base color. This is non-PBR as specular should be white except for metals. For PBR materials, keep at 0."), specTintProp.floatValue, 0f, 1f);

        MaterialProperty thinProp = FindProperty("_Thin", properties);
        thinProp.floatValue = EditorGUILayout.IntSlider(new GUIContent("Thin (no volume)",
           "Keep at 0 if the material has volume. If it's planar, (like a billboard model), change to 1 to allow proper simulation of SSS."), (int)thinProp.floatValue, 0, 1);

        if (thinProp.floatValue > 0)
        {
            MaterialProperty flatnessProp = FindProperty("_Flatness", properties);
            flatnessProp.floatValue = EditorGUILayout.Slider(new GUIContent("Flatness",
               "Controls SSS approximation."), flatnessProp.floatValue, 0f, 1f);
        }
        EditorGUILayout.Space(30);
    }

    private void DrawTextureAlbedoProperty(Material mat, MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        var textureProp = ShaderGUI.FindProperty($"_AlbedoTex", properties);
        var colorProp = ShaderGUI.FindProperty($"_AlbedoTint", properties);

        colorProp.colorValue = materialEditor.ColorProperty(colorProp, "Albedo Tint");
        textureProp.textureValue = materialEditor.TextureProperty(textureProp, "Albedo Texture");

        bool assigned = textureProp.textureValue != null;
        LocalKeyword assignedKw = new(mat.shader, $"_ALBEDO_MAP_ASSIGNED");
        mat.SetKeyword(assignedKw, assigned);

        EditorGUILayout.EndVertical();
    }

    private void DrawTextureNormalProperty(Material mat, MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUILayout.Separator();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        var textureProp = ShaderGUI.FindProperty($"_NormalTex", properties);
        var strengthProperty = ShaderGUI.FindProperty($"_NormalStrength", properties);

        textureProp.textureValue = materialEditor.TextureProperty(textureProp, "Normal Texture");
        strengthProperty.floatValue = EditorGUILayout.Slider("Normal Map Strength", strengthProperty.floatValue, 0, 1);

        bool assigned = textureProp.textureValue != null;
        LocalKeyword assignedKw = new(mat.shader, $"_NORMAL_MAP_ASSIGNED");
        mat.SetKeyword(assignedKw, assigned);

        EditorGUILayout.EndVertical();
    }

    private void DrawTextureMaskMapProperty(Material mat, MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUILayout.Separator();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        var textureProp = ShaderGUI.FindProperty($"_MaskTex", properties);
        textureProp.textureValue = materialEditor.TextureProperty(textureProp, $"HDRP Mask Texture");

        // Remap slider
        bool assigned = textureProp.textureValue != null;
        LocalKeyword assignedKw = new(mat.shader, $"_MASK_MAP_ASSIGNED");
        mat.SetKeyword(assignedKw, assigned);

        if (assigned)
        {
            var remapRoughnessProp = ShaderGUI.FindProperty($"_RoughnessMinMax", properties);
            Vector4 curRoughnessVal = remapRoughnessProp.vectorValue;
            EditorGUILayout.MinMaxSlider($"Roughness Remap", ref curRoughnessVal.x, ref curRoughnessVal.y, 0, 1);
            remapRoughnessProp.vectorValue = curRoughnessVal;

            var remapMetalProp = ShaderGUI.FindProperty($"_MetallicMinMax", properties);
            Vector4 curMetalVal = remapMetalProp.vectorValue;
            EditorGUILayout.MinMaxSlider($"Metallic Remap", ref curMetalVal.x, ref curMetalVal.y, 0, 1);
            remapMetalProp.vectorValue = curMetalVal;
        }
        else
        {
            var baseRoughnessProp = ShaderGUI.FindProperty($"_RoughnessBase", properties);
            baseRoughnessProp.floatValue = EditorGUILayout.Slider("Roughness", baseRoughnessProp.floatValue, 0, 1);

            var baseMetalProp = ShaderGUI.FindProperty($"_MetallicBase", properties);
            baseMetalProp.floatValue = EditorGUILayout.Slider("Metallic", baseMetalProp.floatValue, 0, 1);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawTextureRemappableProperty(string name, Material mat, MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUILayout.Separator();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        var textureProp = ShaderGUI.FindProperty($"_{name}Tex", properties);
        textureProp.textureValue = materialEditor.TextureProperty(textureProp, $"{name} Texture");

        // Remap slider
        bool assigned = textureProp.textureValue != null;
        LocalKeyword assignedKw = new(mat.shader, $"_{name.ToUpper()}_MAP_ASSIGNED");
        mat.SetKeyword(assignedKw, assigned);

        if (assigned)
        {
            var remapProp = ShaderGUI.FindProperty($"_{name}MinMax", properties);
            Vector4 curVal = remapProp.vectorValue;
            EditorGUILayout.MinMaxSlider($"{name} Remap", ref curVal.x, ref curVal.y, 0, 1);
            remapProp.vectorValue = curVal;
        }
        else
        {
            var baseProp = ShaderGUI.FindProperty($"_{name}Base", properties);
            baseProp.floatValue = EditorGUILayout.Slider(name, baseProp.floatValue, 0, 1);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawTextureEmissionProperty(Material mat, MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUILayout.Separator();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        LocalKeyword emissionKw = new(mat.shader, $"_EMISSION");
        bool enabled = mat.IsKeywordEnabled(emissionKw);

        enabled = EditorGUILayout.Toggle("Emission", enabled);
        mat.SetKeyword(emissionKw, enabled);

        if (enabled)
        {
            var textureProp = ShaderGUI.FindProperty($"_EmissionTex", properties);
            var colorProperty = ShaderGUI.FindProperty($"_EmissionTint", properties);

            textureProp.textureValue = materialEditor.TextureProperty(textureProp, "Emission Texture");
            colorProperty.colorValue = EditorGUILayout.ColorField(new GUIContent("Emission Color", "Emission texture is multiplied by this if assigned, can be used to regulate emission strength."),
                colorProperty.colorValue, false, false, true);

            bool assigned = textureProp.textureValue != null;
            LocalKeyword assignedKw = new(mat.shader, $"_EMISSION_MAP_ASSIGNED");
            mat.SetKeyword(assignedKw, assigned);
        }

        EditorGUILayout.EndVertical();
    }
}
