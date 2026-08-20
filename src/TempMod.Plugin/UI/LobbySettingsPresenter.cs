using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TempMod.Plugin.UI;

/// <summary>
/// 役職再実装中に維持するtempMOD設定画面の枠組みです。
/// カスタム役職の選択、出現率、能力設定、フリープレイ固定配布は含めません。
/// </summary>
internal static class LobbySettingsPresenter
{
    private const string Prefix = "tempMOD_Foundation_";
    private const float RowSpacing = 0.43f;

    internal static void ShowFoundationPage(GameOptionsMenu menu)
    {
        Prepare(menu);
        var y = GameOptionsMenu.START_POS_Y - 0.10f;
        CreateHeader(menu, ref y, "<color=#FFE76A>tempMOD 設定</color>", "役職再実装用のクリーン基盤");
        CreateStaticRow(menu, ref y, "カスタム役職", "<color=#FFCF5A>未導入: すべて撤去済み</color>", "RoleResetState");
        CreateStaticRow(menu, ref y, "再実装方針", "役職ごとに上流参照・受入テスト・同期を確認して追加します", "RoleResetPlan");
        CreateStaticRow(menu, ref y, "オンライン安全性", "プレイヤー複製・ダミー生成・参加者追加は使用しません", "NetworkSafety");

        CreateHeader(menu, ref y, "<color=#FFCF5A>ゲーム制御</color>", "ホストのみ変更できます。通常のマルチプレイではOFFを推奨します");
        CreateTwoWayRow(menu, ref y, "ゲームを終了しない", TempModPlugin.MatchSettings.PreventGameEnd.Value ? "<color=#78FF91>ON</color>" : "<color=#FF7777>OFF</color>", "OFF", "ON", () =>
        {
            TempModPlugin.MatchSettings.SetPreventGameEnd(false);
            ShowFoundationPage(menu);
        }, () =>
        {
            TempModPlugin.MatchSettings.SetPreventGameEnd(true);
            ShowFoundationPage(menu);
        }, "PreventGameEnd");
        CreateStaticRow(menu, ref y, "廃村", "ホスト: Shift + L + Enter", "Abandon");
    }

    private static void Prepare(GameOptionsMenu menu)
    {
        if (menu.stringOptionOrigin == null || menu.settingsContainer == null)
            return;
        for (var index = 0; index < menu.settingsContainer.childCount; index++)
            menu.settingsContainer.GetChild(index).gameObject.SetActive(false);
        RemoveExisting(menu.settingsContainer);
    }

    private static void CreateHeader(GameOptionsMenu menu, ref float y, string title, string value)
    {
        var row = CreateOption(menu, y, "Header_" + Math.Abs(y));
        if (row == null)
            return;
        row.TitleText.text = title;
        row.ValueText.text = "<size=55%>" + value + "</size>";
        HideButtons(row);
        y -= RowSpacing;
    }

    private static void CreateStaticRow(GameOptionsMenu menu, ref float y, string title, string value, string key)
    {
        var row = CreateOption(menu, y, key);
        if (row == null)
            return;
        row.TitleText.text = "<size=72%>" + title + "</size>";
        row.ValueText.text = "<size=65%>" + value + "</size>";
        HideButtons(row);
        y -= RowSpacing;
    }

    private static void CreateTwoWayRow(GameOptionsMenu menu, ref float y, string title, string value, string leftLabel, string rightLabel, Action onLeft, Action onRight, string key)
    {
        var row = CreateOption(menu, y, key);
        if (row == null)
            return;
        row.TitleText.text = title;
        row.ValueText.text = value;
        ConfigureButton(row.MinusBtn, leftLabel, onLeft);
        ConfigureButton(row.PlusBtn, rightLabel, onRight);
        y -= RowSpacing;
    }

    private static StringOption? CreateOption(GameOptionsMenu menu, float y, string key)
    {
        if (menu.stringOptionOrigin == null || menu.settingsContainer == null)
            return null;
        var cloneObject = UnityEngine.Object.Instantiate(menu.stringOptionOrigin.gameObject, menu.settingsContainer);
        var option = cloneObject.GetComponent(Il2CppType.Of<StringOption>()).TryCast<StringOption>();
        if (option == null)
            return null;
        cloneObject.name = Prefix + key;
        cloneObject.SetActive(true);
        cloneObject.transform.localPosition = new Vector3(0f, y, -1f);
        cloneObject.transform.localScale = Vector3.one;
        return option;
    }

    private static void ConfigureButton(PassiveButton? button, string label, Action action)
    {
        if (button == null)
            return;
        button.gameObject.SetActive(true);
        button.transform.localScale = Vector3.one;
        button.ChangeButtonText(label);
        foreach (var sprite in button.gameObject.GetComponentsInChildren<SpriteRenderer>(true))
            sprite.color = Color.white;
        foreach (var text in button.gameObject.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = label;
            text.color = Color.white;
        }
        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener((UnityAction)(() => action()));
    }

    private static void HideButtons(StringOption row)
    {
        if (row.MinusBtn != null) row.MinusBtn.gameObject.SetActive(false);
        if (row.PlusBtn != null) row.PlusBtn.gameObject.SetActive(false);
    }

    private static void RemoveExisting(Transform container)
    {
        for (var index = container.childCount - 1; index >= 0; index--)
        {
            var child = container.GetChild(index);
            if (child != null && child.name.StartsWith(Prefix, StringComparison.Ordinal))
                UnityEngine.Object.Destroy(child.gameObject);
        }
    }
}
