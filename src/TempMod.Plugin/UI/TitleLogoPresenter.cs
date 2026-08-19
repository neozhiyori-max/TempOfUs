using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace TempMod.Plugin.UI;

/// <summary>タイトル画面の中央パネルにtempMODロゴを追加する。</summary>
internal static class TitleLogoPresenter
{
    private const string ObjectName = "tempMOD_TitleLogo";
    private const string AnchorPath = "MainMenuManager/MainUI/AspectScaler/RightPanel";
    private static Sprite? _logo;
    private static bool _shownAtInitialTitle;

    /// <summary>ゲーム起動後、最初に開くタイトル画面で一度だけロゴを表示する。</summary>
    internal static void ShowAtInitialTitleOnly()
    {
        if (_shownAtInitialTitle)
            return;
        _shownAtInitialTitle = true;

        try
        {
            var existing = GameObject.Find(ObjectName);
            if (existing != null)
                return;

            var anchor = GameObject.Find(AnchorPath);
            if (anchor == null)
            {
                TempModPlugin.Instance.Log.LogWarning($"タイトルパネルの基準点が見つかりません: {AnchorPath}");
                return;
            }

            var root = new GameObject(ObjectName);
            root.transform.SetParent(anchor.transform, false);
            root.transform.localPosition = new Vector3(0f, 0.1f, -0.25f);
            root.transform.localScale = Vector3.one;

            var logo = new GameObject("TempOfUsLogo");
            logo.transform.SetParent(root.transform, false);
            logo.transform.localPosition = new Vector3(0f, 0.16f, -0.05f);
            logo.transform.localScale = new Vector3(0.93f, 0.93f, 1f);
            var logoRenderer = logo.AddComponent(Il2CppType.Of<SpriteRenderer>()).TryCast<SpriteRenderer>() ?? throw new InvalidOperationException("ロゴレンダラーを生成できません。");
            logoRenderer.sprite = GetLogo();
            logoRenderer.sortingOrder = 50;

            TempModPlugin.Instance.Log.LogInfo("tempMODのタイトルロゴを表示しました。");
        }
        catch (Exception exception)
        {
            TempModPlugin.Instance.Log.LogError($"タイトルロゴの生成に失敗しました: {exception}");
        }
    }

    private static Sprite GetLogo()
    {
        if (_logo != null)
            return _logo;

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .First(name => name.EndsWith("Assets.tempofus_logo.png", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("tempMODのロゴリソースを開けません。");
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
