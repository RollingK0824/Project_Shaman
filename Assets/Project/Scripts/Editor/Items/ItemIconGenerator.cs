using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ItemIconGenerator
{
    // =========================================================
    // Settings
    // =========================================================

    private const int PREVIEW_LAYER = 31;

    private const int TEXTURE_SIZE = 512;
    private const int ALPHA_THRESHOLD = 10;
    private const int CROP_PADDING = 24;

    // 1에 가까울수록 아이템이 화면을 더 꽉 채운다.
    private const float FRAME_FILL_RATIO = 0.9f;

    // 향처럼 세로로 긴 오브젝트 때문에
    // 본체가 지나치게 작아지는 것을 완화한다.
    private const float VERTICAL_FRAME_WEIGHT = 0.72f;

    private static readonly Vector3 CAMERA_EULER =
        new Vector3(20f, -25f, 0f);

    private static readonly Vector3 LIGHT_EULER =
        new Vector3(35f, -35f, 0f);

    private const float LIGHT_INTENSITY = 1.15f;


    // =========================================================
    // Menu
    // =========================================================

    [MenuItem(
        "Assets/Project Shaman/Generate Selected Item Icons",
        true
    )]
    private static bool ValidateGenerateSelectedItemIcons()
    {
        return Selection.objects
            .OfType<ItemData>()
            .Any();
    }


    [MenuItem(
        "Assets/Project Shaman/Generate Selected Item Icons"
    )]
    private static void GenerateSelectedItemIcons()
    {
        ItemData[] selectedItems =
            Selection.objects
                .OfType<ItemData>()
                .ToArray();

        if (selectedItems.Length == 0)
        {
            Debug.LogWarning(
                "[ItemIconGenerator] " +
                "ItemData 에셋을 하나 이상 선택해주세요."
            );

            return;
        }

        int successCount = 0;
        int failedCount = 0;

        foreach (ItemData itemData in selectedItems)
        {
            try
            {
                if (GenerateIcon(itemData))
                {
                    successCount++;
                }
                else
                {
                    failedCount++;
                }
            }
            catch (Exception exception)
            {
                failedCount++;

                Debug.LogException(
                    new Exception(
                        $"[ItemIconGenerator] " +
                        $"{itemData.name} 아이콘 생성 중 오류",
                        exception
                    ),
                    itemData
                );
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[ItemIconGenerator] 작업 완료 / " +
            $"성공 {successCount} / 실패 {failedCount}"
        );
    }


    // =========================================================
    // Generate
    // =========================================================

    private static bool GenerateIcon(
        ItemData itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        GameObject sourcePrefab =
            GetSourcePrefab(itemData);

        if (sourcePrefab == null)
        {
            Debug.LogError(
                $"[ItemIconGenerator] " +
                $"{itemData.name}: 사용할 프리팹이 없습니다.",
                itemData
            );

            return false;
        }

        GameObject previewInstance = null;
        GameObject cameraObject = null;
        GameObject lightObject = null;

        RenderTexture renderTexture = null;

        Texture2D capturedTexture = null;
        Texture2D squareTexture = null;

        try
        {
            // -------------------------------------------------
            // Preview Object
            // -------------------------------------------------

            previewInstance =
                PrefabUtility.InstantiatePrefab(
                    sourcePrefab
                ) as GameObject;

            if (previewInstance == null)
            {
                Debug.LogError(
                    $"[ItemIconGenerator] " +
                    $"{itemData.name}: 프리팹 생성 실패",
                    itemData
                );

                return false;
            }

            previewInstance.hideFlags =
                HideFlags.HideAndDontSave;

            SetLayerRecursively(
                previewInstance,
                PREVIEW_LAYER
            );

            Renderer[] renderers =
                previewInstance
                    .GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                Debug.LogError(
                    $"[ItemIconGenerator] " +
                    $"{itemData.name}: Renderer가 없습니다.",
                    itemData
                );

                return false;
            }

            CenterPreviewObject(
                previewInstance,
                renderers
            );

            // 이동 후 Bounds 다시 계산
            renderers =
                previewInstance
                    .GetComponentsInChildren<Renderer>();

            Bounds bounds =
                GetCombinedBounds(renderers);


            // -------------------------------------------------
            // Camera
            // -------------------------------------------------

            cameraObject =
                CreatePreviewCamera(
                    bounds
                );

            Camera previewCamera =
                cameraObject.GetComponent<Camera>();


            // -------------------------------------------------
            // Light
            // -------------------------------------------------

            lightObject =
                CreatePreviewLight();


            // -------------------------------------------------
            // Render
            // -------------------------------------------------

            renderTexture =
                CreateRenderTexture();

            capturedTexture =
                RenderPreview(
                    previewCamera,
                    renderTexture
                );


            // -------------------------------------------------
            // Crop
            // 항상 정사각형 Texture로 반환한다.
            // -------------------------------------------------

            squareTexture =
                CropToSquare(
                    capturedTexture,
                    CROP_PADDING
                );


            // -------------------------------------------------
            // Save
            // -------------------------------------------------

            string iconAssetPath =
                GetIconAssetPath(itemData);

            if (!SaveTextureAsPng(
                    squareTexture,
                    iconAssetPath))
            {
                return false;
            }


            // -------------------------------------------------
            // Import
            // -------------------------------------------------

            ConfigureTextureImporter(
                iconAssetPath
            );


            // -------------------------------------------------
            // Assign
            // -------------------------------------------------

            Sprite iconSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    iconAssetPath
                );

            if (iconSprite == null)
            {
                Debug.LogError(
                    $"[ItemIconGenerator] " +
                    $"{itemData.name}: Sprite 로드 실패",
                    itemData
                );

                return false;
            }

            if (!AssignIconToItemData(
                    itemData,
                    iconSprite))
            {
                return false;
            }

            Debug.Log(
                $"[ItemIconGenerator] 생성 완료: " +
                $"{itemData.DisplayName} → " +
                $"{iconAssetPath}",
                itemData
            );

            return true;
        }
        finally
        {
            DestroyTemporaryObjects(
                previewInstance,
                cameraObject,
                lightObject,
                renderTexture,
                capturedTexture,
                squareTexture
            );
        }
    }


    // =========================================================
    // Source Prefab
    // =========================================================

    private static GameObject GetSourcePrefab(
        ItemData itemData)
    {
        // 퀵슬롯 아이콘은 월드에서 보이는 모습에
        // 최대한 가깝게 만드는 것을 우선한다.
        if (itemData.WorldPrefab != null)
        {
            return itemData.WorldPrefab;
        }

        return itemData.VisualPrefab;
    }


    // =========================================================
    // Preview Object
    // =========================================================

    private static void CenterPreviewObject(
        GameObject previewInstance,
        Renderer[] renderers)
    {
        Bounds bounds =
            GetCombinedBounds(renderers);

        previewInstance.transform.position -=
            bounds.center;
    }


    private static Bounds GetCombinedBounds(
        Renderer[] renderers)
    {
        Bounds bounds =
            renderers[0].bounds;

        for (int i = 1;
             i < renderers.Length;
             i++)
        {
            bounds.Encapsulate(
                renderers[i].bounds
            );
        }

        return bounds;
    }


    private static void SetLayerRecursively(
        GameObject gameObject,
        int layer)
    {
        gameObject.layer = layer;

        foreach (Transform child
                 in gameObject.transform)
        {
            SetLayerRecursively(
                child.gameObject,
                layer
            );
        }
    }


    // =========================================================
    // Camera
    // =========================================================

    private static GameObject CreatePreviewCamera(
        Bounds bounds)
    {
        GameObject cameraObject =
            new GameObject(
                "__ItemIconCamera"
            );

        cameraObject.hideFlags =
            HideFlags.HideAndDontSave;

        Camera camera =
            cameraObject.AddComponent<Camera>();

        camera.clearFlags =
            CameraClearFlags.SolidColor;

        camera.backgroundColor =
            new Color(
                0f,
                0f,
                0f,
                0f
            );

        camera.cullingMask =
            1 << PREVIEW_LAYER;

        camera.orthographic = true;

        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 1000f;

        camera.allowHDR = false;
        camera.allowMSAA = false;

        camera.aspect = 1f;

        cameraObject.transform.rotation =
            Quaternion.Euler(
                CAMERA_EULER
            );

        float radius =
            Mathf.Max(
                bounds.extents.magnitude,
                0.1f
            );

        Vector3 viewDirection =
            cameraObject.transform.forward;

        cameraObject.transform.position =
            bounds.center -
            viewDirection *
            (radius * 4f + 2f);

        camera.orthographicSize =
            CalculateOrthographicSize(
                camera,
                bounds
            );

        return cameraObject;
    }


    private static float CalculateOrthographicSize(
        Camera camera,
        Bounds bounds)
    {
        Vector3[] corners =
            GetBoundsCorners(bounds);

        float maxAbsX = 0f;
        float maxAbsY = 0f;

        foreach (Vector3 corner in corners)
        {
            Vector3 localPoint =
                camera.transform
                    .InverseTransformPoint(
                        corner
                    );

            maxAbsX =
                Mathf.Max(
                    maxAbsX,
                    Mathf.Abs(localPoint.x)
                );

            maxAbsY =
                Mathf.Max(
                    maxAbsY,
                    Mathf.Abs(localPoint.y)
                );
        }

        // 세로로 매우 긴 아이템의 본체가
        // 지나치게 작아지는 것을 완화한다.
        float weightedHeight =
            maxAbsY *
            VERTICAL_FRAME_WEIGHT;

        float requiredSize =
            Mathf.Max(
                weightedHeight,
                maxAbsX / camera.aspect
            );

        requiredSize /=
            FRAME_FILL_RATIO;

        return Mathf.Max(
            requiredSize,
            0.01f
        );
    }


    private static Vector3[] GetBoundsCorners(
        Bounds bounds)
    {
        Vector3 center =
            bounds.center;

        Vector3 extents =
            bounds.extents;

        return new Vector3[]
        {
            center + new Vector3(
                -extents.x,
                -extents.y,
                -extents.z
            ),

            center + new Vector3(
                -extents.x,
                -extents.y,
                 extents.z
            ),

            center + new Vector3(
                -extents.x,
                 extents.y,
                -extents.z
            ),

            center + new Vector3(
                -extents.x,
                 extents.y,
                 extents.z
            ),

            center + new Vector3(
                 extents.x,
                -extents.y,
                -extents.z
            ),

            center + new Vector3(
                 extents.x,
                -extents.y,
                 extents.z
            ),

            center + new Vector3(
                 extents.x,
                 extents.y,
                -extents.z
            ),

            center + new Vector3(
                 extents.x,
                 extents.y,
                 extents.z
            )
        };
    }


    // =========================================================
    // Light
    // =========================================================

    private static GameObject CreatePreviewLight()
    {
        GameObject lightObject =
            new GameObject(
                "__ItemIconLight"
            );

        lightObject.hideFlags =
            HideFlags.HideAndDontSave;

        Light light =
            lightObject.AddComponent<Light>();

        light.type =
            LightType.Directional;

        light.intensity =
            LIGHT_INTENSITY;

        light.color =
            Color.white;

        light.shadows =
            LightShadows.None;

        light.cullingMask =
            1 << PREVIEW_LAYER;

        lightObject.transform.rotation =
            Quaternion.Euler(
                LIGHT_EULER
            );

        return lightObject;
    }


    // =========================================================
    // Rendering
    // =========================================================

    private static RenderTexture CreateRenderTexture()
    {
        RenderTexture renderTexture =
            new RenderTexture(
                TEXTURE_SIZE,
                TEXTURE_SIZE,
                24,
                RenderTextureFormat.ARGB32
            );

        renderTexture.antiAliasing = 1;

        renderTexture.Create();

        return renderTexture;
    }


    private static Texture2D RenderPreview(
        Camera camera,
        RenderTexture renderTexture)
    {
        RenderTexture previous =
            RenderTexture.active;

        camera.targetTexture =
            renderTexture;

        RenderTexture.active =
            renderTexture;

        camera.Render();

        Texture2D texture =
            new Texture2D(
                TEXTURE_SIZE,
                TEXTURE_SIZE,
                TextureFormat.RGBA32,
                false
            );

        texture.ReadPixels(
            new Rect(
                0,
                0,
                TEXTURE_SIZE,
                TEXTURE_SIZE
            ),
            0,
            0
        );

        texture.Apply();

        camera.targetTexture = null;

        RenderTexture.active =
            previous;

        return texture;
    }


    // =========================================================
    // Crop
    // =========================================================

    private static Texture2D CropToSquare(
        Texture2D source,
        int padding)
    {
        Color32[] pixels =
            source.GetPixels32();

        int width =
            source.width;

        int height =
            source.height;

        if (!TryFindVisibleBounds(
                pixels,
                width,
                height,
                out int minX,
                out int minY,
                out int maxX,
                out int maxY))
        {
            return CopyTexture(source);
        }

        int contentWidth =
            maxX - minX + 1;

        int contentHeight =
            maxY - minY + 1;

        int sideLength =
            Mathf.Max(
                contentWidth,
                contentHeight
            ) + padding * 2;

        sideLength =
            Mathf.Min(
                sideLength,
                Mathf.Max(
                    width,
                    height
                )
            );

        float centerX =
            (minX + maxX) * 0.5f;

        float centerY =
            (minY + maxY) * 0.5f;

        int startX =
            Mathf.RoundToInt(
                centerX -
                sideLength * 0.5f
            );

        int startY =
            Mathf.RoundToInt(
                centerY -
                sideLength * 0.5f
            );

        Texture2D result =
            new Texture2D(
                sideLength,
                sideLength,
                TextureFormat.RGBA32,
                false
            );

        Color32[] resultPixels =
            new Color32[
                sideLength *
                sideLength
            ];

        Color32 transparent =
            new Color32(
                0,
                0,
                0,
                0
            );

        for (int i = 0;
             i < resultPixels.Length;
             i++)
        {
            resultPixels[i] =
                transparent;
        }

        for (int y = 0;
             y < sideLength;
             y++)
        {
            int sourceY =
                startY + y;

            if (sourceY < 0 ||
                sourceY >= height)
            {
                continue;
            }

            for (int x = 0;
                 x < sideLength;
                 x++)
            {
                int sourceX =
                    startX + x;

                if (sourceX < 0 ||
                    sourceX >= width)
                {
                    continue;
                }

                resultPixels[
                    y * sideLength + x
                ] =
                    pixels[
                        sourceY * width +
                        sourceX
                    ];
            }
        }

        result.SetPixels32(
            resultPixels
        );

        result.Apply();

        return result;
    }


    private static bool TryFindVisibleBounds(
        Color32[] pixels,
        int width,
        int height,
        out int minX,
        out int minY,
        out int maxX,
        out int maxY)
    {
        minX = width;
        minY = height;

        maxX = -1;
        maxY = -1;

        for (int y = 0;
             y < height;
             y++)
        {
            for (int x = 0;
                 x < width;
                 x++)
            {
                Color32 color =
                    pixels[
                        y * width + x
                    ];

                if (color.a <=
                    ALPHA_THRESHOLD)
                {
                    continue;
                }

                minX =
                    Mathf.Min(
                        minX,
                        x
                    );

                minY =
                    Mathf.Min(
                        minY,
                        y
                    );

                maxX =
                    Mathf.Max(
                        maxX,
                        x
                    );

                maxY =
                    Mathf.Max(
                        maxY,
                        y
                    );
            }
        }

        return
            maxX >= minX &&
            maxY >= minY;
    }


    private static Texture2D CopyTexture(
        Texture2D source)
    {
        Texture2D copy =
            new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false
            );

        copy.SetPixels32(
            source.GetPixels32()
        );

        copy.Apply();

        return copy;
    }


    // =========================================================
    // Save
    // =========================================================

    private static string GetIconAssetPath(
        ItemData itemData)
    {
        string itemAssetPath =
            AssetDatabase.GetAssetPath(
                itemData
            );

        string folderPath =
            Path.GetDirectoryName(
                itemAssetPath
            )?.Replace(
                "\\",
                "/"
            );

        return
            $"{folderPath}/" +
            $"{itemData.name}_Icon.png";
    }


    private static bool SaveTextureAsPng(
        Texture2D texture,
        string assetPath)
    {
        if (texture == null)
        {
            return false;
        }

        byte[] pngData =
            texture.EncodeToPNG();

        if (pngData == null ||
            pngData.Length == 0)
        {
            Debug.LogError(
                "[ItemIconGenerator] " +
                "PNG 인코딩 실패"
            );

            return false;
        }

        string projectRoot =
            Directory.GetParent(
                Application.dataPath
            )?.FullName;

        if (string.IsNullOrEmpty(
                projectRoot))
        {
            Debug.LogError(
                "[ItemIconGenerator] " +
                "프로젝트 루트 경로를 찾지 못했습니다."
            );

            return false;
        }

        string fullPath =
            Path.Combine(
                projectRoot,
                assetPath
            );

        File.WriteAllBytes(
            fullPath,
            pngData
        );

        AssetDatabase.ImportAsset(
            assetPath,
            ImportAssetOptions.ForceUpdate
        );

        return true;
    }


    // =========================================================
    // Import
    // =========================================================

    private static void ConfigureTextureImporter(
        string assetPath)
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(
                assetPath
            ) as TextureImporter;

        if (importer == null)
        {
            return;
        }

        importer.textureType =
            TextureImporterType.Sprite;

        importer.spriteImportMode =
            SpriteImportMode.Single;

        importer.alphaIsTransparency =
            true;

        importer.mipmapEnabled =
            false;

        importer.wrapMode =
            TextureWrapMode.Clamp;

        importer.filterMode =
            FilterMode.Bilinear;

        importer.textureCompression =
            TextureImporterCompression.Uncompressed;

        importer.SaveAndReimport();
    }


    // =========================================================
    // ItemData
    // =========================================================

    private static bool AssignIconToItemData(
        ItemData itemData,
        Sprite iconSprite)
    {
        SerializedObject serializedItem =
            new SerializedObject(
                itemData
            );

        SerializedProperty iconProperty =
            serializedItem.FindProperty(
                "_icon"
            );

        if (iconProperty == null)
        {
            Debug.LogError(
                $"[ItemIconGenerator] " +
                $"{itemData.name}: " +
                "ItemData의 _icon 필드를 찾지 못했습니다.",
                itemData
            );

            return false;
        }

        iconProperty.objectReferenceValue =
            iconSprite;

        serializedItem
            .ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(
            itemData
        );

        return true;
    }


    // =========================================================
    // Cleanup
    // =========================================================

    private static void DestroyTemporaryObjects(
        GameObject previewInstance,
        GameObject cameraObject,
        GameObject lightObject,
        RenderTexture renderTexture,
        Texture2D capturedTexture,
        Texture2D squareTexture)
    {
        if (previewInstance != null)
        {
            UnityEngine.Object.DestroyImmediate(
                previewInstance
            );
        }

        if (cameraObject != null)
        {
            UnityEngine.Object.DestroyImmediate(
                cameraObject
            );
        }

        if (lightObject != null)
        {
            UnityEngine.Object.DestroyImmediate(
                lightObject
            );
        }

        if (renderTexture != null)
        {
            renderTexture.Release();

            UnityEngine.Object.DestroyImmediate(
                renderTexture
            );
        }

        if (capturedTexture != null)
        {
            UnityEngine.Object.DestroyImmediate(
                capturedTexture
            );
        }

        if (squareTexture != null)
        {
            UnityEngine.Object.DestroyImmediate(
                squareTexture
            );
        }
    }
}