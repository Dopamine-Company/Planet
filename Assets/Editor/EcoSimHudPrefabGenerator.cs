using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using EcoSim.View;

/// <summary>
/// EcoSim HUD 프리팹 자동 생성기.
/// Tools ▸ EcoSim ▸ Generate HUD Prefabs 실행 →
/// Assets/Resources/UI/HUD/ 에 PopulationGraphHUD / EventLogHUD 프리팹 생성.
/// 이미 있으면 덮어씀. 생성 후 위치·크기·폰트는 프리팹에서 자유롭게 수정.
/// </summary>
public static class EcoSimHudPrefabGenerator
{
    private const string Dir = "Assets/Resources/UI/HUD";

    [MenuItem("Tools/EcoSim/Generate HUD Prefabs")]
    public static void Generate()
    {
        EnsureDir();

        CreateGraphPrefab();
        CreateLogPrefab();
        CreateInspectorPrefab();
        CreateCellGraphPrefab();
        CreateToolBarPrefab();
        CreateFlashPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EcoSim] HUD 프리팹 생성 완료 → {Dir}");

        if (TMP_Settings.instance == null)
            Debug.LogWarning("[EcoSim] TMP Essential Resources 미임포트. Window ▸ TextMeshPro ▸ Import TMP Essential Resources 실행 필요.");
    }

    private static void EnsureDir()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/Resources/UI", "HUD");
    }

    private static void CreateGraphPrefab()
    {
        var root = NewUIRoot("PopulationGraphHUD");
        root.AddComponent<PopulationGraphHUD>();

        var img = new GameObject("GraphImage", typeof(RectTransform), typeof(RawImage));
        img.transform.SetParent(root.transform, false);
        var rt = img.GetComponent<RectTransform>();
        // 좌하단 고정, 512×256
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(10f, 10f);
        rt.sizeDelta = new Vector2(512f, 256f);
        img.GetComponent<RawImage>().color = Color.white;

        SavePrefab(root, $"{Dir}/PopulationGraphHUD.prefab");
    }

    private static void CreateLogPrefab()
    {
        var root = NewUIRoot("EventLogHUD");
        root.AddComponent<EventLogHUD>();

        var txt = new GameObject("LogText", typeof(RectTransform), typeof(TextMeshProUGUI));
        txt.transform.SetParent(root.transform, false);
        var rt = txt.GetComponent<RectTransform>();
        // 좌상단 고정, 500×400
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(10f, -10f);
        rt.sizeDelta = new Vector2(500f, 400f);

        var tmp = txt.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.color = new Color(1f, 1f, 1f, 0.9f);
        tmp.text = "— 관찰 로그 —";
        ApplyKoreanFont(tmp);

        SavePrefab(root, $"{Dir}/EventLogHUD.prefab");
    }

    private static void CreateInspectorPrefab()
    {
        var root = NewUIRoot("CellInspectorHUD");
        root.AddComponent<EcoSim.View.CellInspectorHUD>();

        var txt = new GameObject("InfoText", typeof(RectTransform), typeof(TextMeshProUGUI));
        txt.transform.SetParent(root.transform, false);
        var rt = txt.GetComponent<RectTransform>();
        // 우상단 고정, 620×130
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-10f, -10f);
        rt.sizeDelta = new Vector2(620f, 130f);

        var tmp = txt.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.TopRight;
        tmp.color = new Color(1f, 1f, 1f, 0.9f);
        tmp.text = "셀 인스펙터";
        ApplyKoreanFont(tmp);

        SavePrefab(root, $"{Dir}/CellInspectorHUD.prefab");
    }

    private static void CreateCellGraphPrefab()
    {
        var root = NewUIRoot("CellGraphHUD");
        root.AddComponent<EcoSim.View.CellGraphHUD>();

        var img = new GameObject("CellGraphImage", typeof(RectTransform), typeof(RawImage));
        img.transform.SetParent(root.transform, false);
        var rt = img.GetComponent<RectTransform>();
        // 우하단 고정, 384×192
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-10f, 10f);
        rt.sizeDelta = new Vector2(384f, 192f);
        img.GetComponent<RawImage>().color = Color.white;

        SavePrefab(root, $"{Dir}/CellGraphHUD.prefab");
    }

    private static void CreateToolBarPrefab()
    {
        var root = NewUIRoot("ToolBarHUD");
        root.AddComponent<EcoSim.View.ToolBarHUD>();

        // 하단 중앙 가로 바 컨테이너
        var bar = new GameObject("Bar", typeof(RectTransform));
        bar.transform.SetParent(root.transform, false);
        var brt = bar.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 20f);
        brt.sizeDelta = new Vector2(560f, 60f);

        string[] names  = { "BtnPlant", "BtnHerb", "BtnPred", "BtnBarrier" };
        string[] labels = { "나무(1)", "초식(2)", "포식(3)", "벽(4)" };
        for (int i = 0; i < 4; i++)
            MakeButton(bar.transform, names[i], labels[i], -270f + i * 120f);

        // 브러시 슬라이더
        MakeSlider(bar.transform, "BrushSlider", 210f);

        SavePrefab(root, $"{Dir}/ToolBarHUD.prefab");
    }

    private static void MakeButton(Transform parent, string name, string label, float x)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(110f, 44f);
        go.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 0.9f);

        var txt = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txt.transform.SetParent(go.transform, false);
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var tmp = txt.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        ApplyKoreanFont(tmp);
    }

    private static void MakeSlider(Transform parent, string name, float x)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(120f, 20f);

        var slider = go.GetComponent<Slider>();

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.9f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        Stretch(fillArea.GetComponent<RectTransform>());
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        Stretch(fill.GetComponent<RectTransform>());
        fill.GetComponent<Image>().color = new Color(0.4f, 0.7f, 0.4f, 1f);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        Stretch(handleArea.GetComponent<RectTransform>());
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<RectTransform>().sizeDelta = new Vector2(16f, 0f);
        handle.GetComponent<Image>().color = Color.white;

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void CreateFlashPrefab()
    {
        var root = NewUIRoot("EventFlashHUD");
        root.AddComponent<EcoSim.View.EventFlashHUD>();

        var img = new GameObject("FlashOverlay", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(root.transform, false);
        Stretch(img.GetComponent<RectTransform>()); // 풀스크린
        var image = img.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = false;

        SavePrefab(root, $"{Dir}/EventFlashHUD.prefab");
    }

    private static void ApplyKoreanFont(TMP_Text tmp)
    {
        // Resources/Fonts 에 TMP 폰트 있으면 적용(없으면 기본 폰트 → 한글 □ 가능).
        var fonts = Resources.LoadAll<TMP_FontAsset>("Fonts");
        if (fonts != null && fonts.Length > 0)
            tmp.font = fonts[0];
        else
            Debug.LogWarning("[EcoSim] Resources/Fonts 에 TMP 폰트 없음. 한글 □ 가능 — Generate Korean TMP Font 먼저 실행.");
    }

    private static GameObject NewUIRoot(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        // 부모 캔버스에 풀스트레치
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    private static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }
}
