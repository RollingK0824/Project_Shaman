using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Puts the speech bubble art (Sprites/UI/Dialogue/DialogueSpeechBubble.png, used as drawn) behind the villager dialogue
// window: imported as a 9-sliced sprite so the brush outline and the tail keep their shape however long the talk runs,
// the box padded to keep the words inside the outline and above the tail, the text in ink on the paper.
// Run with the game scene open, then save the scene
public static class DialogueBubbleSetup
{
    private const string BUBBLE_PATH = "Assets/Project/Visuals/Sprites/UI/Dialogue/DialogueSpeechBubble.png";

    // In pixels of the 1024 art: the transparent margin plus the brush outline on each side, the tail in the bottom one
    private static readonly Vector4 BORDER = new Vector4(200f, 370f, 200f, 257f);   // left, bottom, right, top
    // The art is drawn large: shown at 1/1.5 so the outline matches the window's 820 width
    private const float PIXELS_PER_UNIT_MULTIPLIER = 1.5f;
    private static readonly RectOffset PADDING = new RectOffset(140, 140, 175, 250);

    private static readonly Color INK = new Color(0.20f, 0.14f, 0.10f, 1f);
    private static readonly Color NAME = new Color(0.42f, 0.20f, 0.10f, 1f);

    [MenuItem("ProjectShaman/UI/Apply Dialogue Speech Bubble")]
    private static void Apply()
    {
        Sprite bubble = ImportAsSlicedSprite();
        if (bubble == null)
        {
            return;
        }

        VillagerDialoguePanel[] panels = Object.FindObjectsByType<VillagerDialoguePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (panels.Length == 0)
        {
            Debug.LogError("[DialogueBubble] No VillagerDialoguePanel in the open scene");
            return;
        }

        foreach (VillagerDialoguePanel panel in panels)
        {
            SerializedObject so = new SerializedObject(panel);
            TMP_Text speaker = so.FindProperty("_speaker").objectReferenceValue as TMP_Text;
            TMP_Text line = so.FindProperty("_line").objectReferenceValue as TMP_Text;
            Transform box = speaker != null ? speaker.transform.parent : null;
            Image background = box != null ? box.GetComponent<Image>() : null;
            if (background == null)
            {
                Debug.LogError($"[DialogueBubble] {panel.name}: no Box image above the speaker label");
                continue;
            }

            Undo.RecordObject(background, "Apply Dialogue Speech Bubble");
            background.sprite = bubble;
            background.type = Image.Type.Sliced;
            background.fillCenter = true;
            background.pixelsPerUnitMultiplier = PIXELS_PER_UNIT_MULTIPLIER;
            background.color = Color.white;

            VerticalLayoutGroup layout = box.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                Undo.RecordObject(layout, "Apply Dialogue Speech Bubble");
                layout.padding = new RectOffset(PADDING.left, PADDING.right, PADDING.top, PADDING.bottom);
            }

            Undo.RecordObject(speaker, "Apply Dialogue Speech Bubble");
            speaker.color = NAME;
            if (line != null)
            {
                Undo.RecordObject(line, "Apply Dialogue Speech Bubble");
                line.color = INK;
            }

            Debug.Log($"[DialogueBubble] {panel.gameObject.scene.name}/{panel.name}: speech bubble applied to {box.name}");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static Sprite ImportAsSlicedSprite()
    {
        TextureImporter importer = AssetImporter.GetAtPath(BUBBLE_PATH) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[DialogueBubble] {BUBBLE_PATH} not found");
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spriteBorder = BORDER;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(BUBBLE_PATH);
    }
}
