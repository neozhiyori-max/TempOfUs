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
/// SNR型の「カテゴリ -> 役職一覧 -> 設定」導線を、画像なしの文字ラベルで再現する。
/// 標準設定のタブ本体を操作せず、複製した専用パネルだけへ表示する。
/// </summary>
internal static class TempModRoleSettingsPresenter
{
    private const string EntryButtonName = "tempMOD_SettingsButton";
    private const string PanelName = "tempMOD_RoleSettingsPanel";
    private const string TabPrefix = "tempMOD_SNR_Tab_";
    private const string RowPrefix = "tempMOD_SNR_Row_";
    private const float RowSpacing = 0.43f;

    private static GameObject? _panelRoot;
    private static GameOptionsMenu? _panelMenu;
    private static GameSettingMenu? _ownerMenu;

    private enum Category { General, Crewmate, Impostor, Neutral }
    private readonly record struct RoleDisplay(RoleId Id, string JapaneseName, string Color);

    private static readonly RoleDisplay[] FirstImpostorWave =
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
        private static void Postfix(GameSettingMenu __instance) => EnsureEntryButton(__instance);
    }

    // Among Us側が標準ボタンを再設定しても、tempMOD入口の文字とクリックを維持する。
    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Update))]
    internal static class GameSettingMenuUpdatePatch
    {
        private static void Postfix(GameSettingMenu __instance) => EnsureEntryButton(__instance);
    }

    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Close))]
    internal static class GameSettingMenuClosePatch
    {
        private static void Postfix(GameSettingMenu __instance)
        {
            ClosePanel(__instance);
            if (CustomOptionSaver.IsLoaded)
                CustomOptionSaver.Save();
        }
    }

    private static void EnsureEntryButton(GameSettingMenu menu)
    {
        if (menu == null || menu.GameSettingsButton == null || menu.RoleSettingsButton == null)
            return;

        var existing = menu.transform.Find(EntryButtonName);
        var buttonObject = existing != null
            ? existing.gameObject
            : UnityEngine.Object.Instantiate(menu.GameSettingsButton.gameObject, menu.GameSettingsButton.transform.parent);
        buttonObject.name = EntryButtonName;
        buttonObject.transform.localPosition = menu.RoleSettingsButton.transform.localPosition + new Vector3(0f, -0.56f, 0f);
        buttonObject.transform.localScale = menu.GameSettingsButton.transform.localScale;

        var isPanelOpen = _panelRoot != null && _panelRoot.activeSelf;
        buttonObject.SetActive(!isPanelOpen);
        SetAllText(buttonObject, "tempMOD設定");

        var button = buttonObject.GetComponent(Il2CppType.Of<PassiveButton>()).TryCast<PassiveButton>();
        if (button == null)
            return;
        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener((UnityAction)(() => OpenPanel(menu, Category.Impostor)));
    }

    private static void OpenPanel(GameSettingMenu menu, Category category)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || menu.GameSettingsTab == null)
            return;

        _ownerMenu = menu;
        if (_panelRoot == null)
        {
            var source = menu.GameSettingsTab.gameObject;
            _panelRoot = UnityEngine.Object.Instantiate(source, menu.transform);
            _panelRoot.name = PanelName;
            _panelRoot.transform.localPosition = source.transform.localPosition;
            _panelRoot.transform.localScale = source.transform.localScale;
            _panelRoot.transform.SetAsLastSibling();
            _panelMenu = _panelRoot.GetComponent(Il2CppType.Of<GameOptionsMenu>()).TryCast<GameOptionsMenu>();
        }

        if (_panelMenu == null)
        {
            SuperNewRolesPlugin.Logger?.LogError("tempMOD専用設定パネルからGameOptionsMenuを取得できません。");
            return;
        }

        HideStandardMenu(menu);
        _panelRoot.SetActive(true);
        CreateCategoryTabs(menu, _panelRoot.transform, category);
        ShowCategory(_panelMenu, category);
        SuperNewRolesPlugin.Logger?.LogInfo($"tempMOD専用設定パネルを表示: {category}");
    }

    private static void ClosePanel(GameSettingMenu menu)
    {
        if (_panelRoot != null)
            UnityEngine.Object.Destroy(_panelRoot);
        _panelRoot = null;
        _panelMenu = null;
        _ownerMenu = null;
        RestoreStandardMenu(menu);
    }

    private static void HideStandardMenu(GameSettingMenu menu)
    {
        if (menu.PresetsTab != null) menu.PresetsTab.gameObject.SetActive(false);
        if (menu.GameSettingsTab != null) menu.GameSettingsTab.gameObject.SetActive(false);
        if (menu.RoleSettingsTab != null) menu.RoleSettingsTab.gameObject.SetActive(false);
        SetActive(menu.GameSettingsButton, false);
        SetActive(menu.RoleSettingsButton, false);
        foreach (var name in new[] { EntryButtonName, "PresetsButton", "GamePresetsButton", "PresetButton" })
        {
            var target = FindDeep(menu.transform, name);
            if (target != null) target.gameObject.SetActive(false);
        }
    }

    private static void RestoreStandardMenu(GameSettingMenu menu)
    {
        SetActive(menu.GameSettingsButton, true);
        SetActive(menu.RoleSettingsButton, true);
        foreach (var name in new[] { EntryButtonName, "PresetsButton", "GamePresetsButton", "PresetButton" })
        {
            var target = FindDeep(menu.transform, name);
            if (target != null) target.gameObject.SetActive(true);
        }
    }

    private static void CreateCategoryTabs(GameSettingMenu menu, Transform panelRoot, Category selected)
    {
        RemoveTabs(panelRoot);
        var labels = new[]
        {
            (Category.General, "基本設定", "#FFD166"),
            (Category.Crewmate, "クルー", "#55D7FF"),
            (Category.Impostor, "インポスター", "#FF6666"),
            (Category.Neutral, "第三陣営", "#55D7FF"),
        };

        for (var index = 0; index < labels.Length; index++)
        {
            var data = labels[index];
            var tabObject = UnityEngine.Object.Instantiate(menu.GameSettingsButton.gameObject, panelRoot);
            tabObject.name = TabPrefix + data.Item1;
            tabObject.transform.localScale = Vector3.one * 0.66f;
            tabObject.transform.localPosition = new Vector3(-2.35f + 1.57f * index, 1.78f, -2f);
            tabObject.SetActive(true);
            SetAllText(tabObject, data.Item1 == selected
                ? $"<color={data.Item3}>【{data.Item2}】</color>"
                : $"<color={data.Item3}>{data.Item2}</color>");

            var button = tabObject.GetComponent(Il2CppType.Of<PassiveButton>()).TryCast<PassiveButton>();
            if (button == null) continue;
            button.OnClick = new Button.ButtonClickedEvent();
            button.OnClick.AddListener((UnityAction)(() => OpenPanel(menu, data.Item1)));
        }
    }

    private static void ShowCategory(GameOptionsMenu menu, Category category)
    {
        if (menu == null || menu.stringOptionOrigin == null || menu.settingsContainer == null)
            return;

        for (var index = 0; index < menu.settingsContainer.childCount; index++)
            menu.settingsContainer.GetChild(index).gameObject.SetActive(false);
        RemoveExistingRows(menu.settingsContainer);

        var y = 0.90f;
        var title = category switch
        {
            Category.General => "<color=#FFD166>tempMOD 基本設定</color>",
            Category.Crewmate => "<color=#55D7FF>クルー役職</color>",
            Category.Impostor => "<color=#FF6666>インポスター役職</color>",
            Category.Neutral => "<color=#55D7FF>第三陣営役職</color>",
            _ => "tempMOD",
        };
        CreateHeader(menu, ref y, title, "SNR型レイアウト / tempMOD文字UI");

        switch (category)
        {
            case Category.General:
                CreateStaticRow(menu, ref y, "第1波", "インポスター5役職を登録済み", "WaveStatus");
                CreateStaticRow(menu, ref y, "同期・能力", "<color=#FFD166>次段階で有効化</color>", "SyncStatus");
                CreateStaticRow(menu, ref y, "画像資産", "SNRの画像・アイコンは使用しません", "AssetStatus");
                break;
            case Category.Impostor:
                CreateStaticRow(menu, ref y, "第1波", "5役職だけが設定・抽選対象です", "ImpostorStatus");
                foreach (var role in FirstImpostorWave) CreateRoleRows(menu, ref y, role);
                break;
            case Category.Crewmate:
                CreateStaticRow(menu, ref y, "準備中", "クルー役職は第2波以降に追加します", "CrewPlaceholder");
                break;
            case Category.Neutral:
                CreateStaticRow(menu, ref y, "準備中", "第三陣営は第1波の検証後に追加します", "NeutralPlaceholder");
                break;
        }
    }

    private static void CreateRoleRows(GameOptionsMenu menu, ref float y, RoleDisplay role)
    {
        var roleOption = RoleOptionManager.GetRoleOption(role.Id);
        if (roleOption == null)
        {
            CreateStaticRow(menu, ref y, role.JapaneseName, "<color=#FF7777>登録エラー</color>", role.Id + "Missing");
            return;
        }

        CreateTwoWayRow(menu, ref y, $"<color={role.Color}>{role.JapaneseName}</color>  人数", $"{roleOption.NumberOfCrews} 人", "−", "＋",
            () => UpdateRoleCount(role.Id, -1, menu), () => UpdateRoleCount(role.Id, +1, menu), role.Id + "Count");
        CreateTwoWayRow(menu, ref y, $"<color={role.Color}>{role.JapaneseName}</color>  出現率", $"{roleOption.Percentage}%", "−10%", "+10%",
            () => UpdateRoleRate(role.Id, -10, menu), () => UpdateRoleRate(role.Id, +10, menu), role.Id + "Rate");
    }

    private static void UpdateRoleCount(RoleId roleId, int delta, GameOptionsMenu menu)
    {
        var option = RoleOptionManager.GetRoleOption(roleId);
        if (option == null) return;
        var count = Math.Clamp((int)option.NumberOfCrews + delta, 0, 15);
        option.UpdateValues((byte)count, count == 0 ? 0 : Math.Max(10, option.Percentage));
        SaveAndRefresh(menu);
    }

    private static void UpdateRoleRate(RoleId roleId, int delta, GameOptionsMenu menu)
    {
        var option = RoleOptionManager.GetRoleOption(roleId);
        if (option == null) return;
        var rate = Math.Clamp(option.Percentage + delta, 0, 100);
        option.UpdateValues((byte)(rate == 0 ? 0 : Math.Max(1, (int)option.NumberOfCrews)), rate);
        SaveAndRefresh(menu);
    }

    private static void SaveAndRefresh(GameOptionsMenu menu)
    {
        if (CustomOptionSaver.IsLoaded) CustomOptionSaver.Save();
        ShowCategory(menu, Category.Impostor);
    }

    private static void CreateHeader(GameOptionsMenu menu, ref float y, string title, string value)
    {
        var row = CreateOption(menu, y, "Header_" + Math.Abs(y));
        if (row == null) return;
        row.TitleText.text = title;
        row.ValueText.text = "<size=55%>" + value + "</size>";
        HideButtons(row);
        y -= RowSpacing;
    }

    private static void CreateStaticRow(GameOptionsMenu menu, ref float y, string title, string value, string key)
    {
        var row = CreateOption(menu, y, key);
        if (row == null) return;
        row.TitleText.text = "<size=72%>" + title + "</size>";
        row.ValueText.text = "<size=60%>" + value + "</size>";
        HideButtons(row);
        y -= RowSpacing;
    }

    private static void CreateTwoWayRow(GameOptionsMenu menu, ref float y, string title, string value, string leftLabel, string rightLabel, Action onLeft, Action onRight, string key)
    {
        var row = CreateOption(menu, y, key);
        if (row == null) return;
        row.TitleText.text = "<size=74%>" + title + "</size>";
        row.ValueText.text = value;
        ConfigureButton(row.MinusBtn, leftLabel, onLeft);
        ConfigureButton(row.PlusBtn, rightLabel, onRight);
        y -= RowSpacing;
    }

    private static StringOption? CreateOption(GameOptionsMenu menu, float y, string key)
    {
        var clone = UnityEngine.Object.Instantiate(menu.stringOptionOrigin.gameObject, menu.settingsContainer);
        var option = clone.GetComponent(Il2CppType.Of<StringOption>()).TryCast<StringOption>();
        if (option == null) return null;
        clone.name = RowPrefix + key;
        clone.SetActive(true);
        clone.transform.localPosition = new Vector3(0f, y, -1f);
        clone.transform.localScale = Vector3.one;
        return option;
    }

    private static void ConfigureButton(PassiveButton? button, string label, Action action)
    {
        if (button == null) return;
        button.gameObject.SetActive(true);
        button.ChangeButtonText(label);
        foreach (var sprite in button.gameObject.GetComponentsInChildren<SpriteRenderer>(true)) sprite.color = Color.white;
        foreach (var text in button.gameObject.GetComponentsInChildren<TMP_Text>(true)) { text.text = label; text.color = Color.white; }
        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener((UnityAction)(() => action()));
    }

    private static void HideButtons(StringOption row)
    {
        if (row.MinusBtn != null) row.MinusBtn.gameObject.SetActive(false);
        if (row.PlusBtn != null) row.PlusBtn.gameObject.SetActive(false);
    }

    private static void RemoveExistingRows(Transform container)
    {
        for (var index = container.childCount - 1; index >= 0; index--)
        {
            var child = container.GetChild(index);
            if (child != null && child.name.StartsWith(RowPrefix, StringComparison.Ordinal)) UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static void RemoveTabs(Transform root)
    {
        for (var index = root.childCount - 1; index >= 0; index--)
        {
            var child = root.GetChild(index);
            if (child != null && child.name.StartsWith(TabPrefix, StringComparison.Ordinal)) UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static void SetActive(Component? component, bool active)
    {
        if (component != null) component.gameObject.SetActive(active);
    }

    private static Transform? FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (var index = 0; index < root.childCount; index++)
        {
            var found = FindDeep(root.GetChild(index), name);
            if (found != null) return found;
        }
        return null;
    }

    private static void SetAllText(GameObject gameObject, string text)
    {
        foreach (var label in gameObject.GetComponentsInChildren<TMP_Text>(true)) label.text = text;
        foreach (var label in gameObject.GetComponentsInChildren<TextMeshPro>(true)) label.text = text;
    }
}
