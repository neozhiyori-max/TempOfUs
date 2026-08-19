using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace TempMod.Plugin.UI;

/// <summary>
/// GameSettingMenuの左側に専用のtempMOD設定ボタンを追加する。
/// クリック時は既存のゲーム設定一覧を開き、その下部へtempMOD設定行を差し込む。
/// </summary>
internal static class TempModSettingsTabPresenter
{
    private const string ButtonName = "tempMOD_SettingsTabButton";

    internal static void AddButton(GameSettingMenu menu)
    {
        if (menu == null || menu.GameSettingsButton == null)
            return;
        if (menu.transform.Find(ButtonName) != null)
            return;

        var source = menu.GameSettingsButton.gameObject;
        var buttonObject = UnityEngine.Object.Instantiate(source, source.transform.parent);
        buttonObject.name = ButtonName;
        buttonObject.transform.localPosition = menu.RoleSettingsButton.transform.localPosition + new Vector3(0f, -0.58f, 0f);

        foreach (var text in buttonObject.GetComponentsInChildren<TMP_Text>(true))
            text.text = "tempMOD設定";

        var button = buttonObject.GetComponent<PassiveButton>();
        if (button == null)
            return;
        button.ChangeButtonText("tempMOD設定");
        button.OnClick.AddListener((UnityAction)(() => OpenTempModSettings(menu)));
    }

    private static void OpenTempModSettings(GameSettingMenu menu)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return;

        // ゲーム本体の「ゲーム設定」ボタンに登録済みの遷移イベントをそのまま実行する。
        // これにより、内部タブ状態・マスク・戻る操作も標準処理に従う。
        menu.GameSettingsButton.OnClick.Invoke();
        if (menu.MenuDescriptionText != null)
            menu.MenuDescriptionText.text = "tempMODの役職設定。左矢印でON/OFF、右矢印で出現率を変更。カーソルで参考元を表示。";
    }

}
