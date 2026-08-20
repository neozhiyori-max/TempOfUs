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
/// SNR型のカテゴリ切替・役職一覧・詳細設定フローを保ちながら、
/// SNR由来の画像、アイコン、プリファブを使用しないtempMOD文字ラベルUI。
/// </summary>
internal static class TempModRoleSettingsPresenter
{
    private const string EntryButtonName = "tempMOD_SettingsButton";
    private const string TabPrefix = "tempMOD_SNR_Tab_";
    private const string RowPrefix = "tempMOD_SNR_Row_";
    private const float RowSpacing = 0.43f;

    private enum Category
    {
        General,
        Crewmate,
        Impostor,
        Neutral,
    }

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
        private static void Postfix(GameSettingMenu __instance) => AddEntryButton(__instance);
    }

    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Close))]
    internal static class GameSettingMenuClosePatch
    {
        private static void Postfix(GameSettingMenu __instance)
        {
            RemoveTabs(__instance);
            if (CustomOptionSaver.IsLoaded)
                CustomOptionSaver.Save();
        }
    }

    private static void AddEntryButton(GameSettingMenu menu)
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
        buttonObject.SetActive(true);
        SetAllText(buttonObject, "tempMOD設定");

        var button = buttonObject.GetComponent(Il2CppType.Of<PassiveButton>()).TryCast<PassiveButton>();
        if (button == null)
            return;

        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener((UnityAction)(() => OpenCategory(menu, Category.Impostor)));
    }

    private static void OpenCategory(GameSettingMenu menu, Category category)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || menu.GameSettingsTab == null)
            return;

        if (menu.PresetsTab != null)
            menu.PresetsTab.gameObject.SetActive(false);
        if (menu.RoleSettingsTab != null)
            menu.RoleSettingsTab.gameObject.SetActive(false);

        menu.GameSettingsTab.gameObject.SetActive(true);
        CreateCategoryTabs(menu, category);
        ShowCategory(menu.GameSettingsTab, category);
    }

    /// <summary>
    /// SNRの上部カテゴリ切替と同じ情報構造を、文字ボタンだけで提供する。
    /// 画像タブ・役職アイコンは使用しない。
    /// </summary>
    private static void CreateCategoryTabs(GameSettingMenu menu, Category selected)
    {
        RemoveTabs(menu);

        var labels = new[]
        {
            (Category.General, "基本設定", "#FFD166"),
            (Category.Crewmate, "クルー", "#55D7FF"),
            (Category.Impostor, "インポスター", "#FF6666"),
            (Category.Neutral, "第三陣営", "#55D7FF"),
        };

        var parent = menu.GameSettingsTab != null ? menu.GameSettingsTab.transform : menu.transform;
        for (var index = 0; index < labels.Length; index++)
        {
            var data = labels[index];
            var tabObject = UnityEngine.Object.Instantiate(menu.GameSettingsButton.gameObject, parent);
            tabObject.name = TabPrefix + data.Item1;
            tabObject.transform.localScale = Vector3.one * 0.72f;
            tabObject.transform.localPosition = new Vector3(-2.4f + 1.6f * index, 2.15f, -2f);
            tabObject.SetActive(true);
            SetAllText(tabObject, data.Item1 == selected
                ? $"<color={data.Item3}>【{data.Item2}】</color>"
                : $"<color={data.Item3}>{data.Item2}</color>");

            var button = tabObject.GetComponent(Il2CppType.Of<PassiveButton>()).TryCast<PassiveButton>();
            if (button == null)
                continue;
            button.OnClick = new Button.ButtonClickedEvent();
            button.OnClick.AddListener((UnityAction)(() => OpenCategory(menu, data.Item1)));
        }
    }

    private static void ShowCategory(GameOptionsMenu menu, Category category)
    {
        if (menu == null || menu.stringOptionOrigin == null || menu.settingsContainer == null)
            return;

        for (var index = 0; index < menu.settingsContainer.childCount; index++)
            menu.settingsContainer.GetChild(index).gameObject.SetActive(false);
        RemoveExistingRows(menu.settingsContainer);

        var y = 1.35f;
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
                foreach (var role in FirstImpostorWave)
                    CreateRoleRows(menu, ref y, role);
                break;
            case Category.Crewmate:
                CreateStaticRow(menu, ref y, "準備中", "クルー役職は第2波以降に追加します", "CrewPlaceholder");
                break;
            case Category.Neutral:
                CreateStaticRow(menu, ref y, "準備中", "第三陣営はインポスター第1波の検証後に追加します", "NeutralPlaceholder");
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

    private static void UpdateRoleCount(RoleId roleId, int delta, GameOptionsMenu menu)
    {
        var roleOption = RoleOptionManager.GetRoleOption(roleId);
        if (roleOption == null)
            return;

        var count = Math.Clamp((int)roleOption.NumberOfCrews + delta, 0, 15);
        var rate = count == 0 ? 0 : Math.Max(10, roleOption.Percentage);
        roleOption.UpdateValues((byte)count, rate);
        SaveAndRefresh(menu, Category.Impostor);
    }

    private static void UpdateRoleRate(RoleId roleId, int delta, GameOptionsMenu menu)
    {
        var roleOption = RoleOptionManager.GetRoleOption(roleId);
        if (roleOption == null)
            return;

        var rate = Math.Clamp(roleOption.Percentage + delta, 0, 100);
        var count = rate == 0 ? 0 : Math.Max(1, (int)roleOption.NumberOfCrews);
        roleOption.UpdateValues((byte)count, rate);
        SaveAndRefresh(menu, Category.Impostor);
    }

    private static void SaveAndRefresh(GameOptionsMenu menu, Category category)
    {
        if (CustomOptionSaver.IsLoaded)
            CustomOptionSaver.Save();
        ShowCategory(menu, category);
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

    private static void RemoveExistingRows(Transform container)
    {
        for (var index = container.childCount - 1; index >= 0; index--)
        {
            var child = container.GetChild(index);
            if (child != null && child.name.StartsWith(RowPrefix, StringComparison.Ordinal))
                UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static void RemoveTabs(GameSettingMenu menu)
    {
        if (menu == null)
            return;
        var root = menu.GameSettingsTab != null ? menu.GameSettingsTab.transform : menu.transform;
        for (var index = root.childCount - 1; index >= 0; index--)
        {
            var child = root.GetChild(index);
            if (child != null && child.name.StartsWith(TabPrefix, StringComparison.Ordinal))
                UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static void SetAllText(GameObject gameObject, string text)
    {
        foreach (var label in gameObject.GetComponentsInChildren<TMP_Text>(true))
            label.text = text;
    }
}
