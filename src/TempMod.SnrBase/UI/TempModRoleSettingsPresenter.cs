using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using HarmonyLib;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;

namespace SuperNewRoles.UI;

/// <summary>
/// tempMOD独自の役職設定画面です。SNRの設定プリファブ・画像・アイコンは利用しません。
/// Among Us標準の設定行を複製し、現在有効な5役職だけを表示します。
/// </summary>
internal static class TempModRoleSettingsPresenter
{
    private const string ButtonName = "tempMOD_SNR_SettingsButton";
    private const string RowPrefix = "tempMOD_SNR_Row_";
    private const float RowSpacing = 0.43f;

    private readonly record struct RoleDisplay(RoleId Id, string JapaneseName, string Color);

    private static readonly RoleDisplay[] FirstWave =
    {
        new(RoleId.Kunoichi, "ニンジャ", "#FF6666"),
        new(RoleId.Mafia, "マフィア", "#FF6666"),
        new(RoleId.RemoteController, "パペッティア", "#FF6666"),
        new(RoleId.EvilGuesser, "マッドゲッサー", "#FF6666"),
        new(RoleId.Jammer, "ブラックアウト", "#FF6666"),
    };

    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Start))]
    internal static class GameSettingMenuStartPatch
    {
        private static void Postfix(GameSettingMenu __instance) => AddButton(__instance);
    }

    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Close))]
    internal static class GameSettingMenuClosePatch
    {
        private static void Postfix()
        {
            if (CustomOptionSaver.IsLoaded)
                CustomOptionSaver.Save();
        }
    }

    private static void AddButton(GameSettingMenu menu)
    {
        if (menu == null || menu.GameSettingsButton == null || menu.RoleSettingsButton == null)
            return;

        var existing = menu.transform.Find(ButtonName);
        var buttonObject = existing != null
            ? existing.gameObject
            : UnityEngine.Object.Instantiate(menu.GameSettingsButton.gameObject, menu.GameSettingsButton.transform.parent);

        buttonObject.name = ButtonName;
        buttonObject.transform.localPosition = menu.RoleSettingsButton.transform.localPosition + new Vector3(0f, -0.56f, 0f);
        buttonObject.transform.localScale = menu.GameSettingsButton.transform.localScale;
        buttonObject.SetActive(true);
        SetAllText(buttonObject, "tempMOD役職");

        var button = buttonObject.GetComponent(Il2CppType.Of<PassiveButton>()).TryCast<PassiveButton>();
        if (button == null)
            return;

        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener((UnityAction)(() => OpenRolePage(menu)));
    }

    private static void OpenRolePage(GameSettingMenu menu)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || menu.GameSettingsTab == null)
            return;

        if (menu.PresetsTab != null)
            menu.PresetsTab.gameObject.SetActive(false);
        if (menu.RoleSettingsTab != null)
            menu.RoleSettingsTab.gameObject.SetActive(false);

        menu.GameSettingsTab.gameObject.SetActive(true);
        Show(menu.GameSettingsTab);
    }

    private static void Show(GameOptionsMenu menu)
    {
        if (menu == null || menu.stringOptionOrigin == null || menu.settingsContainer == null)
            return;

        for (var index = 0; index < menu.settingsContainer.childCount; index++)
            menu.settingsContainer.GetChild(index).gameObject.SetActive(false);
        RemoveExisting(menu.settingsContainer);

        var y = 1.45f;
        CreateHeader(menu, ref y, "<color=#55D7FF>tempMOD 役職設定</color>", "インポスター第1波 — SNR基盤 / 5役職のみ");
        CreateStaticRow(menu, ref y, "同期状態", "<color=#FFD166>ローカル設定のみ</color> 役職同期・能力は次段階で有効化", "SyncNotice");
        CreateStaticRow(menu, ref y, "操作方法", "人数は左右、出現率は次の行で10%ずつ変更", "ControlNotice");

        foreach (var role in FirstWave)
        {
            var roleOption = RoleOptionManager.GetRoleOption(role.Id);
            if (roleOption == null)
            {
                CreateStaticRow(menu, ref y, role.JapaneseName, "<color=#FF7777>登録エラー</color>", role.Id + "Missing");
                continue;
            }

            CreateTwoWayRow(
                menu,
                ref y,
                $"<color={role.Color}>{role.JapaneseName}</color>  人数",
                $"{roleOption.NumberOfCrews} 人",
                "−",
                "＋",
                () => UpdateRoleCount(role.Id, -1, menu),
                () => UpdateRoleCount(role.Id, +1, menu),
                role.Id + "Count");

            CreateTwoWayRow(
                menu,
                ref y,
                $"<color={role.Color}>{role.JapaneseName}</color>  出現率",
                $"{roleOption.Percentage}%",
                "−10%",
                "+10%",
                () => UpdateRoleRate(role.Id, -10, menu),
                () => UpdateRoleRate(role.Id, +10, menu),
                role.Id + "Rate");
        }
    }

    private static void UpdateRoleCount(RoleId roleId, int delta, GameOptionsMenu menu)
    {
        var roleOption = RoleOptionManager.GetRoleOption(roleId);
        if (roleOption == null)
            return;

        var count = Math.Clamp(roleOption.NumberOfCrews + delta, 0, 15);
        var rate = count == 0 ? 0 : Math.Max(10, roleOption.Percentage);
        roleOption.UpdateValues((byte)count, rate);
        SaveAndRefresh(menu);
    }

    private static void UpdateRoleRate(RoleId roleId, int delta, GameOptionsMenu menu)
    {
        var roleOption = RoleOptionManager.GetRoleOption(roleId);
        if (roleOption == null)
            return;

        var rate = Math.Clamp(roleOption.Percentage + delta, 0, 100);
        var count = rate == 0 ? 0 : Math.Max(1, (int)roleOption.NumberOfCrews);
        roleOption.UpdateValues((byte)count, rate);
        SaveAndRefresh(menu);
    }

    private static void SaveAndRefresh(GameOptionsMenu menu)
    {
        if (CustomOptionSaver.IsLoaded)
            CustomOptionSaver.Save();
        Show(menu);
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
        row.ValueText.text = "<size=60%>" + value + "</size>";
        HideButtons(row);
        y -= RowSpacing;
    }

    private static void CreateTwoWayRow(GameOptionsMenu menu, ref float y, string title, string value, string leftLabel, string rightLabel, Action onLeft, Action onRight, string key)
    {
        var row = CreateOption(menu, y, key);
        if (row == null)
            return;
        row.TitleText.text = "<size=74%>" + title + "</size>";
        row.ValueText.text = value;
        ConfigureButton(row.MinusBtn, leftLabel, onLeft);
        ConfigureButton(row.PlusBtn, rightLabel, onRight);
        y -= RowSpacing;
    }

    private static StringOption? CreateOption(GameOptionsMenu menu, float y, string key)
    {
        var cloneObject = UnityEngine.Object.Instantiate(menu.stringOptionOrigin.gameObject, menu.settingsContainer);
        var option = cloneObject.GetComponent(Il2CppType.Of<StringOption>()).TryCast<StringOption>();
        if (option == null)
            return null;

        cloneObject.name = RowPrefix + key;
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
        if (row.MinusBtn != null)
            row.MinusBtn.gameObject.SetActive(false);
        if (row.PlusBtn != null)
            row.PlusBtn.gameObject.SetActive(false);
    }

    private static void RemoveExisting(Transform container)
    {
        for (var index = container.childCount - 1; index >= 0; index--)
        {
            var child = container.GetChild(index);
            if (child != null && child.name.StartsWith(RowPrefix, StringComparison.Ordinal))
                UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static void SetAllText(GameObject gameObject, string text)
    {
        foreach (var label in gameObject.GetComponentsInChildren<TMP_Text>(true))
            label.text = text;
    }
}
