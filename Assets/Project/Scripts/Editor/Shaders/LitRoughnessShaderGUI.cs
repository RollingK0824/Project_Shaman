using System;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

// ProjectShaman/Lit Roughness 머티리얼 인스펙터.
// URP Lit 인스펙터(LitShader, internal)와 같은 구성에 Roughness Map 줄만 더했다. Detail Inputs는 뺐다.
public class LitRoughnessShaderGUI : BaseShaderGUI
{
    private const string ROUGHNESS_KEYWORD = "_ROUGHNESSMAP";

    private static readonly string[] WORKFLOW_MODE_NAMES = Enum.GetNames(typeof(LitGUI.WorkflowMode));
    private static readonly GUIContent ROUGHNESS_MAP_TEXT = EditorGUIUtility.TrTextContent(
        "Roughness Map", "흑백 Roughness 맵(R 채널). Smoothness 슬라이더가 결과에 곱해진다.");
    private static readonly GUIContent MAP_IS_SMOOTHNESS_TEXT = EditorGUIUtility.TrTextContent(
        "Map Is Smoothness", "맵이 Roughness가 아니라 Smoothness/Gloss면 체크.");

    private LitGUI.LitProperties litProperties;
    private MaterialProperty roughnessMapProp;
    private MaterialProperty roughnessMapIsSmoothnessProp;


    public override void FindProperties(MaterialProperty[] properties)
    {
        base.FindProperties(properties);
        litProperties = new LitGUI.LitProperties(properties);
        roughnessMapProp = FindProperty("_RoughnessMap", properties, false);
        roughnessMapIsSmoothnessProp = FindProperty("_RoughnessMapIsSmoothness", properties, false);
    }

    public override void ValidateMaterial(Material material)
    {
        SetMaterialKeywords(material, LitGUI.SetMaterialKeywords, SetRoughnessKeyword);
    }

    public override void DrawSurfaceOptions(Material material)
    {
        EditorGUIUtility.labelWidth = 0f;

        if (litProperties.workflowMode != null)
        {
            DoPopup(LitGUI.Styles.workflowModeText, litProperties.workflowMode, WORKFLOW_MODE_NAMES);
        }

        base.DrawSurfaceOptions(material);
    }

    public override void DrawSurfaceInputs(Material material)
    {
        base.DrawSurfaceInputs(material);
        LitGUI.Inputs(litProperties, materialEditor, material);

        if (roughnessMapProp != null)
        {
            materialEditor.TexturePropertySingleLine(ROUGHNESS_MAP_TEXT, roughnessMapProp);

            if (roughnessMapIsSmoothnessProp != null && roughnessMapProp.textureValue != null)
            {
                EditorGUI.indentLevel += 2;
                materialEditor.ShaderProperty(roughnessMapIsSmoothnessProp, MAP_IS_SMOOTHNESS_TEXT);
                EditorGUI.indentLevel -= 2;
            }
        }

        DrawEmissionProperties(material, true);
        DrawTileOffset(materialEditor, baseMapProp);
    }

    public override void DrawAdvancedOptions(Material material)
    {
        if (litProperties.reflections != null && litProperties.highlights != null)
        {
            materialEditor.ShaderProperty(litProperties.highlights, LitGUI.Styles.highlightsText);
            materialEditor.ShaderProperty(litProperties.reflections, LitGUI.Styles.reflectionsText);
        }

        base.DrawAdvancedOptions(material);
    }

    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        base.AssignNewShaderToMaterial(material, oldShader, newShader);
        SetupMaterialBlendMode(material);
    }

    private static void SetRoughnessKeyword(Material material)
    {
        bool hasMap = material.HasProperty("_RoughnessMap") && material.GetTexture("_RoughnessMap") != null;
        CoreUtils.SetKeyword(material, ROUGHNESS_KEYWORD, hasMap);
    }
}
