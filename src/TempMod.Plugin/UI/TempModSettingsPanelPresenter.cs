using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TempMod.Plugin.UI;

/// <summary>
/// tempMOD設定ボタンを標準設定メニューへ追加する。
/// 陣営切替は表示領域外の左ボタンではなく、開いた設定ページ内に必ず表示する。
/// </summary>
internal static class TempModSettingsPanelPresenter
{
    private const string TempButtonName = "tempMOD_SettingsButton";
    private static readonly string[] LegacyCategoryButtons =
    {
        "tempMOD_CrewButton",
        "tempMOD_ImpostorButton",
        "tempMOD_NeutralButton",
    };

    internal static void AddButton(GameSettingMenu menu)
    {
        if (menu == null || menu.GameSettingsButton == null || menu.RoleSettingsButton == null)
            return;

        // 以前の実装で画面外に生成された陣営ボタンを除去する。
        foreach (var name in LegacyCategoryButtons)
        {
            var oldButton = menu.transform.Find(name);
            if (oldButton != null)
                UnityEngine.Object.Destroy(oldButton.gameObject);
        }

        var existing = menu.transform.Find(TempButtonName);
        var buttonObject = existing != null
            ? existing.gameObject
            : UnityEngine.Object.Instantiate(menu.GameSettingsButton.gameObject, menu.GameSettingsButton.transform.parent);
        buttonObject.name = TempButtonName;
        buttonObject.transform.localPosition = menu.RoleSettingsButton.transform.localPosition + new Vector3(0f, -0.56f, 0f);
        buttonObject.transform.localScale = menu.GameSettingsButton.transform.localScale;
        buttonObject.SetActive(true);

        var button = buttonObject.GetComponent(Il2CppType.Of<PassiveButton>()).TryCast<PassiveButton>();
        if (button == null)
            return;
        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener((UnityAction)(() => OpenFoundationPage(menu)));
        SetAllText(buttonObject, "tempMOD設定");
    }

    internal static void RefreshButtonLabel(GameSettingMenu menu)
    {
        var buttonTransform = menu.transform.Find(TempButtonName);
        if (buttonTransform != null)
            SetAllText(buttonTransform.gameObject, "tempMOD設定");
    }

    private static void OpenFoundationPage(GameSettingMenu menu)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return;
        if (menu.GameSettingsTab == null)
            return;

        menu.PresetsTab.gameObject.SetActive(false);
        menu.RoleSettingsTab.gameObject.SetActive(false);
        menu.GameSettingsTab.gameObject.SetActive(true);
        LobbySettingsPresenter.ShowFoundationPage(menu.GameSettingsTab);
    }

    private static void SetAllText(GameObject gameObject, string text)
    {
        foreach (var label in gameObject.GetComponentsInChildren<TMP_Text>(true))
            label.text = text;
    }
}
