using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace SuperNewRoles.UI;

/// <summary>
/// tempMOD独自ロゴを、ゲーム起動後に最初に開くタイトル画面だけへ表示する。
/// ゲーム画面、ロビー、設定画面には表示しない。
/// </summary>
internal static class TitleLogoPresenter
{
    private const string ObjectName = "tempMOD_TitleLogo";
    private const string AnchorPath = "MainMenuManager/MainUI/AspectScaler/RightPanel";
    private static Sprite? _logo;
    private static bool _shownAtInitialTitle;

    [HarmonyLib.HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    internal static class MainMenuManagerStartPatch
    {
        private static void Postfix() => ShowAtInitialTitleOnly();
    }

    internal static void ShowAtInitialTitleOnly()
    {
        if (_shownAtInitialTitle)
            return;

        try
        {
            if (GameObject.Find(ObjectName) != null)
                return;

            var anchor = GameObject.Find(AnchorPath);
            if (anchor == null)
            {
                // タイトル画面へ遷移する前・ロビー滞在中は基準点が存在しない。次フレームで再試行する。
                return;
            }

            var root = new GameObject(ObjectName);
            root.transform.SetParent(anchor.transform, false);
            root.transform.localPosition = new Vector3(0f, 0.10f, -0.25f);
            root.transform.localScale = Vector3.one;

            var logo = new GameObject("TempOfUsLogo");
            logo.transform.SetParent(root.transform, false);
            logo.transform.localPosition = new Vector3(0f, 0.16f, -0.05f);
            logo.transform.localScale = new Vector3(0.93f, 0.93f, 1f);

            var renderer = logo.AddComponent(Il2CppType.Of<SpriteRenderer>()).TryCast<SpriteRenderer>()
                ?? throw new InvalidOperationException("タイトルロゴ用のSpriteRendererを生成できません。");
            renderer.sprite = GetLogo();
            renderer.sortingOrder = 50;
            _shownAtInitialTitle = true;
            SuperNewRolesPlugin.Logger?.LogInfo("tempMODのタイトルロゴを表示しました。");
        }
        catch (Exception exception)
        {
            SuperNewRolesPlugin.Logger?.LogError($"tempMODタイトルロゴの生成に失敗しました: {exception}");
        }
    }

    private static Sprite GetLogo()
    {
        if (_logo != null)
            return _logo;

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .First(name => name.EndsWith("Resources.tempofus_logo.png", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("tempMODロゴリソースを開けません。");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var bytes = new Il2CppStructArray<byte>(memory.ToArray());
        if (!ImageConversion.LoadImage(texture, bytes, false))
            throw new InvalidOperationException("tempMODロゴPNGのデコードに失敗しました。");

        _logo = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        return _logo;
    }
}
