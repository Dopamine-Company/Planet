using System.IO;
using UnityEngine;
using UnityEditor;
using EcoSim.Core;

/// <summary>
/// EcoSim 원클릭(제로클릭) 셋업. 스크립트 컴파일 후 자동 실행:
/// 1) 한글 TMP 폰트 없으면 생성
/// 2) HUD 프리팹 없으면 생성
/// 3) SimulationConfig 에셋 없으면 생성(빠른 관찰용 0.05초/틱)
/// 전부 있으면 아무것도 안 함(멱등).
/// </summary>
public static class EcoSimAutoSetup
{
    private const string FontSdf   = "Assets/Resources/Fonts/MalgunSDF.asset";
    private const string GraphPfb  = "Assets/Resources/UI/HUD/PopulationGraphHUD.prefab";
    private const string LogPfb    = "Assets/Resources/UI/HUD/EventLogHUD.prefab";
    private const string InspPfb   = "Assets/Resources/UI/HUD/CellInspectorHUD.prefab";
    private const string CellPfb   = "Assets/Resources/UI/HUD/CellGraphHUD.prefab";
    private const string ToolPfb   = "Assets/Resources/UI/HUD/ToolBarHUD.prefab";
    private const string FlashPfb  = "Assets/Resources/UI/HUD/EventFlashHUD.prefab";
    private const string ConfigPth = "Assets/Resources/EcoSimConfig.asset";

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        bool did = false;

        if (!File.Exists(FontSdf))
        {
            EcoSimKoreanFontGenerator.Generate();
            did = true;
        }

        if (!File.Exists(GraphPfb) || !File.Exists(LogPfb)
            || !File.Exists(InspPfb) || !File.Exists(CellPfb) || !File.Exists(ToolPfb)
            || !File.Exists(FlashPfb))
        {
            EcoSimHudPrefabGenerator.Generate();
            did = true;
        }

        if (!File.Exists(ConfigPth))
        {
            var cfg = ScriptableObject.CreateInstance<SimulationConfig>();
            cfg.realSecondsPerTick = 0.05f; // 관찰용 고속. 원래 감각은 2.
            AssetDatabase.CreateAsset(cfg, ConfigPth);
            AssetDatabase.SaveAssets();
            did = true;
        }

        if (did) Debug.Log("[EcoSim] 자동 셋업 완료 — Play만 누르면 됨.");
    }
}
