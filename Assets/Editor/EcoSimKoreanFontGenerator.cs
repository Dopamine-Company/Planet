using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.TextCore.LowLevel;
using TMPro;

/// <summary>
/// 한글 TMP 폰트 자동 생성. Windows 맑은 고딕을 프로젝트로 복사 →
/// Dynamic TMP 폰트 에셋 생성(필요 글리프 실시간 추가) → Resources/Fonts 저장.
/// 이후 Tools ▸ EcoSim ▸ Generate HUD Prefabs 재실행하면 로그 프리팹에 자동 적용.
/// </summary>
public static class EcoSimKoreanFontGenerator
{
    private const string FontDir  = "Assets/Resources/Fonts";
    private const string TtfPath  = FontDir + "/malgun.ttf";
    private const string SdfPath  = FontDir + "/MalgunSDF.asset";
    private const string SysFont  = @"C:\Windows\Fonts\malgun.ttf";

    [MenuItem("Tools/EcoSim/Generate Korean TMP Font")]
    public static void Generate()
    {
        if (!File.Exists(SysFont))
        {
            Debug.LogError($"[EcoSim] 시스템 폰트 없음: {SysFont}");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(FontDir))
            AssetDatabase.CreateFolder("Assets/Resources", "Fonts");

        File.Copy(SysFont, TtfPath, overwrite: true);
        AssetDatabase.Refresh();

        var font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (font == null)
        {
            Debug.LogError("[EcoSim] ttf 임포트 실패");
            return;
        }

        // Dynamic 모드: 아틀라스에 글리프를 사용 시점에 추가 → 한글 전 범위 사전 베이크 불필요.
        var fa = TMP_FontAsset.CreateFontAsset(
            font, 60, 6, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic);

        AssetDatabase.CreateAsset(fa, SdfPath);
        fa.material.name = fa.name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        if (fa.atlasTextures != null && fa.atlasTextures.Length > 0)
        {
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EcoSim] 한글 TMP 폰트 생성 완료: {SdfPath}. 이제 Tools ▸ EcoSim ▸ Generate HUD Prefabs 재실행.");
    }
}
