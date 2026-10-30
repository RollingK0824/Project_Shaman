using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Animal pack deluxe의 레거시 머티리얼/임포트 설정을 URP용으로 정리하는 1회용 메뉴.
// 1) 팩 안의 모든 .mat을 URP/Lit으로 바꾸고 Base Map/Normal/AO를 다시 연결한다.
// 2) FBX 임포트 설정을 Import via MaterialDescription + Use Embedded Materials로 바꾸고,
//    기존에 쓰던 외부 머티리얼을 Remapped Materials로 이어 붙인다.
// 여러 번 실행해도 같은 결과가 되도록 작성했다. 정리가 끝나면 이 파일은 지워도 된다.
public static class AnimalPackUrpMaterialSetup
{
    private const string PACK_PATH = "Assets/ThirdParty/Animal pack deluxe";
    private const string TEXTURE_PATH = PACK_PATH + "/Textures";
    private const string URP_LIT_SHADER = "Universal Render Pipeline/Lit";
    private const string TITLE = "Animal Pack URP 머티리얼 정리";


    [MenuItem("Tools/Project Shaman/Setup/Animal Pack 머티리얼 URP 변환")]
    private static void Run()
    {
        Shader litShader = Shader.Find(URP_LIT_SHADER);

        if (litShader == null)
        {
            EditorUtility.DisplayDialog(TITLE, $"'{URP_LIT_SHADER}' 셰이더를 찾지 못했습니다.", "확인");
            return;
        }

        List<string> missingBaseMaps = new List<string>();
        int materialCount = ConvertMaterials(litShader, missingBaseMaps);
        int modelCount = FixModelImporters();

        AssetDatabase.SaveAssets();

        string message = $"머티리얼 {materialCount}개, 모델 {modelCount}개를 정리했습니다.";

        if (missingBaseMaps.Count > 0)
        {
            message += "\n\nBase Map을 찾지 못한 머티리얼:\n" + string.Join("\n", missingBaseMaps);
        }

        Debug.Log($"[{TITLE}] {message}");
        EditorUtility.DisplayDialog(TITLE, message, "확인");
    }

    private static int ConvertMaterials(Shader litShader, List<string> missingBaseMaps)
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { PACK_PATH });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // FBX 안에 들어있는 임베디드 머티리얼은 건드리지 않는다.
            if (!path.EndsWith(".mat"))
            {
                continue;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                continue;
            }

            // 셰이더를 바꾸기 전에 레거시 값을 읽어 둔다.
            Texture baseMap = GetTexture(material, "_BaseMap") ?? GetTexture(material, "_MainTex") ?? FindBaseMapByName(material.name);
            Texture normalMap = GetTexture(material, "_BumpMap");
            Texture occlusionMap = GetTexture(material, "_OcclusionMap");
            Color color = GetColor(material, "_BaseColor", GetColor(material, "_Color", Color.white));
            float smoothness = GetFloat(material, "_Smoothness", GetFloat(material, "_Glossiness", 0f));
            float metallic = GetFloat(material, "_Metallic", 0f);

            Undo.RecordObject(material, TITLE);

            if (material.shader != litShader)
            {
                material.shader = litShader;
            }

            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_MainTex", baseMap);
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);

            material.SetTexture("_BumpMap", normalMap);
            SetKeyword(material, "_NORMALMAP", normalMap != null);

            material.SetTexture("_OcclusionMap", occlusionMap);
            SetKeyword(material, "_OCCLUSIONMAP", occlusionMap != null);

            if (baseMap == null && material.name != "default" && !material.name.EndsWith("Default"))
            {
                missingBaseMaps.Add(path);
            }

            EditorUtility.SetDirty(material);
            count++;
        }

        return count;
    }

    // 모델 쪽 머티리얼(brown_bear_col4 등)은 _MainTex가 비어 있어서, 이름이 같은 *_unity 텍스처를 찾아 연결한다.
    private static Texture FindBaseMapByName(string materialName)
    {
        string[] texturePaths = AssetDatabase.FindAssets("t:Texture2D", new[] { TEXTURE_PATH })
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();

        string exact = texturePaths.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == materialName + "_unity");

        if (exact != null)
        {
            return AssetDatabase.LoadAssetAtPath<Texture>(exact);
        }

        // butterfly_col8 -> butterfly_col9_unity 처럼 번호만 다른 경우.
        int colIndex = materialName.IndexOf("_col");

        if (colIndex < 0)
        {
            return null;
        }

        string prefix = materialName.Substring(0, colIndex + "_col".Length);
        string similar = texturePaths.FirstOrDefault(p =>
        {
            string name = Path.GetFileNameWithoutExtension(p);
            return name.StartsWith(prefix) && name.EndsWith("_unity");
        });

        return similar != null ? AssetDatabase.LoadAssetAtPath<Texture>(similar) : null;
    }

    private static int FixModelImporters()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { PACK_PATH });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer == null)
            {
                continue;
            }

            bool alreadyModern = importer.materialImportMode == ModelImporterMaterialImportMode.ImportViaMaterialDescription
                && importer.materialLocation == ModelImporterMaterialLocation.InPrefab;

            // 레거시 설정에서 렌더러 슬롯마다 실제로 쓰던 외부 머티리얼을 먼저 기록해 둔다.
            List<Material> previousMaterials = alreadyModern ? null : CollectSlotMaterials(path);

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();

            if (previousMaterials != null)
            {
                RemapEmbeddedMaterials(path, importer, previousMaterials);
            }

            count++;
        }

        return count;
    }

    private static void RemapEmbeddedMaterials(string path, ModelImporter importer, List<Material> previousMaterials)
    {
        List<Material> embeddedMaterials = CollectSlotMaterials(path);
        bool changed = false;

        for (int i = 0; i < embeddedMaterials.Count && i < previousMaterials.Count; i++)
        {
            Material embedded = embeddedMaterials[i];
            Material external = previousMaterials[i];

            // 레거시에서도 외부 .mat이 아니었으면(임베디드였으면) 그대로 둔다.
            if (embedded == null || external == null || !AssetDatabase.GetAssetPath(external).EndsWith(".mat"))
            {
                continue;
            }

            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), external);
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static List<Material> CollectSlotMaterials(string path)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        List<Material> materials = new List<Material>();

        if (model == null)
        {
            return materials;
        }

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            materials.AddRange(renderer.sharedMaterials);
        }

        return materials;
    }

    private static Texture GetTexture(Material material, string property)
    {
        return material.HasProperty(property) ? material.GetTexture(property) : null;
    }

    private static Color GetColor(Material material, string property, Color fallback)
    {
        return material.HasProperty(property) ? material.GetColor(property) : fallback;
    }

    private static float GetFloat(Material material, string property, float fallback)
    {
        return material.HasProperty(property) ? material.GetFloat(property) : fallback;
    }

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled)
        {
            material.EnableKeyword(keyword);
        }
        else
        {
            material.DisableKeyword(keyword);
        }
    }
}
