using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TempMod.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TempMod.Plugin.UI;

/// <summary>
/// 標準のGameOptionsMenuをそのまま利用するtempMOD設定画面。
/// 役職一覧と役職詳細を別ページにし、標準の一列レイアウト以外の座標計算を行わない。
/// </summary>
internal static class LobbySettingsPresenter
{
    private const string Prefix = "tempMOD_Stable_";
    private const float RowSpacing = 0.43f;

    internal static void ShowCategoryList(GameOptionsMenu menu)
    {
        Prepare(menu);
        var y = GameOptionsMenu.START_POS_Y - 0.10f;
        CreateHeader(menu, ref y, "<color=#FFE76A>tempMOD 役職設定</color>", "陣営別人数と役職詳細をホストが設定します");
        CreateTwoWayRow(menu, ref y, "<color=#55D7FF>クルー役職</color>", $"< {TempModPlugin.Settings.GetFactionCount(Faction.Crew)} / 15 >", "－", "＋", () =>
        {
            TempModPlugin.Settings.AdjustFactionCount(Faction.Crew, -1);
            ShowCategoryList(menu);
        }, () =>
        {
            TempModPlugin.Settings.AdjustFactionCount(Faction.Crew, +1);
            ShowCategoryList(menu);
        }, "CrewLimit");
        CreateActionRow(menu, ref y, "<color=#55D7FF>◆ クルー役職の詳細</color>", "シェリフ・ドクターなど10役職", "開く", () => ShowRoleList(menu, "クルー"), "OpenCrew");
        CreateTwoWayRow(menu, ref y, "<color=#FF6666>インポスター役職</color>", $"< {TempModPlugin.Settings.GetFactionCount(Faction.Impostor)} / 15 >", "－", "＋", () =>
        {
            TempModPlugin.Settings.AdjustFactionCount(Faction.Impostor, -1);
            ShowCategoryList(menu);
        }, () =>
        {
            TempModPlugin.Settings.AdjustFactionCount(Faction.Impostor, +1);
            ShowCategoryList(menu);
        }, "ImpostorLimit");
        CreateActionRow(menu, ref y, "<color=#FF6666>◆ インポスター役職の詳細</color>", "ニンジャ・クリーナーなど26役職", "開く", () => ShowRoleList(menu, "インポスター"), "OpenImpostor");
        CreateTwoWayRow(menu, ref y, "<color=#D890FF>第三陣営役職</color>", $"< {TempModPlugin.Settings.GetFactionCount(Faction.Neutral)} / 15 >", "－", "＋", () =>
        {
            TempModPlugin.Settings.AdjustFactionCount(Faction.Neutral, -1);
            ShowCategoryList(menu);
        }, () =>
        {
            TempModPlugin.Settings.AdjustFactionCount(Faction.Neutral, +1);
            ShowCategoryList(menu);
        }, "NeutralLimit");
        CreateActionRow(menu, ref y, "<color=#D890FF>◆ 第三陣営役職の詳細</color>", "神・ジェスター・ゾンビなど20役職", "開く", () => ShowRoleList(menu, "第三陣営"), "OpenNeutral");
        CreateStaticRow(menu, ref y, "出現率", "10%刻みで変更できます", "ChanceHelp");
        CreateStaticRow(menu, ref y, "少人数開始", "1人から開始可能。ラバーズは2人以上で有効です。", "PlayerCountHelp");
#if TEMPMOD_ADMIN
        CreateHeader(menu, ref y, "<color=#FFCF5A>管理者検証</color>", "公開版には含まれないローカルテスト機能です");
        CreateTwoWayRow(menu, ref y, "ゲームを終了しない", TempModPlugin.AdminSettings.PreventGameEnd.Value ? "<color=#78FF91>ON</color>" : "<color=#FF7777>OFF</color>", "OFF", "ON", () =>
        {
            TempModPlugin.AdminSettings.SetPreventGameEnd(false);
            ShowCategoryList(menu);
        }, () =>
        {
            TempModPlugin.AdminSettings.SetPreventGameEnd(true);
            ShowCategoryList(menu);
        }, "AdminNoEnd");
        CreateStaticRow(menu, ref y, "廃村", "ホスト: Shift + L + Enter", "AdminAbandon");
#endif
    }

    internal static void ShowRoleList(GameOptionsMenu menu, string category, int page = 0, RoleId? selectedRole = null)
    {
        const int pageSize = 7;
        Prepare(menu);
        var y = GameOptionsMenu.START_POS_Y - 0.10f;
        var rows = TempModPlugin.Settings.GetLobbyRows().Where(row => row.Category == category).ToArray();
        var pageCount = Math.Max(1, (int)Math.Ceiling(rows.Length / (double)pageSize));
        page = Math.Clamp(page, 0, pageCount - 1);
        const string hoverGuide = "カーソルを役職名に合わせると能力・ペナルティ・勝利条件を表示します";
        var initialDescription = selectedRole is RoleId selected ? RoleDescriptionCatalog.Get(selected) : hoverGuide;
        var hoverHeader = CreateHeader(menu, ref y, CategoryTitle(category), $"{initialDescription}  < {page + 1} / {pageCount} >");

        foreach (var row in rows.Skip(page * pageSize).Take(pageSize))
        {
            var role = row.Role;
            var isEnabled = row.Enabled;
            CreateTwoWayRow(menu, ref y, row.Label, isEnabled ? "<color=#78FF91>ON</color>" : "<color=#FF7777>OFF</color>", "切替", "設定", () =>
            {
                // 一覧内だけで即時反転し、現在のページを維持する。
                TempModPlugin.Settings.SetRoleEnabled(role, !isEnabled);
                ShowRoleList(menu, category, page, role);
            }, () =>
            {
                ShowRoleDetail(menu, category, role);
            }, "Role_" + role, hoverHeader?.ValueText, RoleDescriptionCatalog.Get(role), hoverGuide);
        }

        CreateTwoWayRow(menu, ref y, "一覧ページ", $"< {page + 1} / {pageCount} >", "前へ", "次へ", () =>
        {
            ShowRoleList(menu, category, page - 1);
        }, () =>
        {
            ShowRoleList(menu, category, page + 1);
        }, "RolePage");

        if (category == "第三陣営" && page == pageCount - 1)
        {
            CreateActionRow(menu, ref y, "ラバーズ", TempModPlugin.Settings.EnableLovers.Value ? "<color=#78FF91>ON</color>    <size=55%>追加設定</size>" : "<color=#FF7777>OFF</color>    <size=55%>追加設定</size>", "設定", () =>
            {
                ShowLoversDetail(menu, category);
            }, "Lovers");
        }
    }

    internal static void ShowRoleDetail(GameOptionsMenu menu, string category, RoleId role)
    {
        Prepare(menu);
        var y = GameOptionsMenu.START_POS_Y - 0.10f;
        var setting = TempModPlugin.Settings.GetLobbyRows().First(row => row.Role == role);
        CreateHeader(menu, ref y, ColorText(setting.Label + " 設定", category), GetShortDescription(role));

        CreateTwoWayRow(menu, ref y, "有効化", setting.Enabled ? "<color=#78FF91>ON</color>" : "<color=#FF7777>OFF</color>", "OFF", "ON", () =>
        {
            TempModPlugin.Settings.SetRoleEnabled(role, false);
            ShowRoleDetail(menu, category, role);
        }, () =>
        {
            TempModPlugin.Settings.SetRoleEnabled(role, true);
            ShowRoleDetail(menu, category, role);
        }, "Enabled");

        CreateTwoWayRow(menu, ref y, "人数", $"{setting.Count} / 15", "－", "＋", () =>
        {
            TempModPlugin.Settings.AdjustRoleCount(role, -1);
            ShowRoleDetail(menu, category, role);
        }, () =>
        {
            TempModPlugin.Settings.AdjustRoleCount(role, +1);
            ShowRoleDetail(menu, category, role);
        }, "Count");

        CreateTwoWayRow(menu, ref y, "出現率", $"{setting.ChancePercent} %", "－", "＋", () =>
        {
            TempModPlugin.Settings.AdjustRoleChance(role, -1);
            ShowRoleDetail(menu, category, role);
        }, () =>
        {
            TempModPlugin.Settings.AdjustRoleChance(role, +1);
            ShowRoleDetail(menu, category, role);
        }, "Chance");

        var details = TempModPlugin.Settings.GetRoleDetails(role);
        if (details.Length > 0)
        {
            CreateHeader(menu, ref y, "詳細設定", "");
            foreach (var detail in details)
            {
                CreateTwoWayRow(menu, ref y, detail.Label, detail.Value, "－", "＋", () =>
                {
                    TempModPlugin.Settings.AdjustDetail(detail.Key, -1);
                    ShowRoleDetail(menu, category, role);
                }, () =>
                {
                    TempModPlugin.Settings.AdjustDetail(detail.Key, +1);
                    ShowRoleDetail(menu, category, role);
                }, "Detail_" + detail.Key);
            }
        }

        CreateActionRow(menu, ref y, "陣営一覧へ戻る", "", "戻る", () => ShowCategoryList(menu), "Back");
    }

    internal static void ShowLoversDetail(GameOptionsMenu menu, string category)
    {
        Prepare(menu);
        var y = GameOptionsMenu.START_POS_Y - 0.10f;
        CreateHeader(menu, ref y, ColorText("ラバーズ 設定", category), "2人以上の場合のみランダムな2名をペアにします");
        CreateTwoWayRow(menu, ref y, "有効化", TempModPlugin.Settings.EnableLovers.Value ? "<color=#78FF91>ON</color>" : "<color=#FF7777>OFF</color>", "OFF", "ON", () =>
        {
            if (TempModPlugin.Settings.EnableLovers.Value)
                TempModPlugin.Settings.ToggleLovers();
            ShowLoversDetail(menu, category);
        }, () =>
        {
            if (!TempModPlugin.Settings.EnableLovers.Value)
                TempModPlugin.Settings.ToggleLovers();
            ShowLoversDetail(menu, category);
        }, "LoversEnabled");
        CreateStaticRow(menu, ref y, "少人数時", "1人では割り当てません", "LoversNote");
        CreateActionRow(menu, ref y, "陣営一覧へ戻る", "", "戻る", () => ShowCategoryList(menu), "Back");
    }

    private static void Prepare(GameOptionsMenu menu)
    {
        if (menu.stringOptionOrigin == null || menu.settingsContainer == null)
            return;
        for (var index = 0; index < menu.settingsContainer.childCount; index++)
            menu.settingsContainer.GetChild(index).gameObject.SetActive(false);
        RemoveExisting(menu.settingsContainer);
    }

    private static StringOption? CreateHeader(GameOptionsMenu menu, ref float y, string title, string value)
    {
        var row = CreateOption(menu, y, "Header_" + Math.Abs(y));
        if (row == null)
            return null;
        row.TitleText.text = title;
        row.ValueText.text = "<size=55%>" + value + "</size>";
        HideButtons(row);
        y -= RowSpacing;
        return row;
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

    private static void CreateActionRow(GameOptionsMenu menu, ref float y, string title, string value, string actionLabel, Action action, string key)
    {
        var row = CreateOption(menu, y, key);
        if (row == null)
            return;
        row.TitleText.text = title;
        row.ValueText.text = value;
        if (row.MinusBtn != null) row.MinusBtn.gameObject.SetActive(false);
        ConfigureButton(row.PlusBtn, actionLabel, action);
        y -= RowSpacing;
    }

    private static void CreateTwoWayRow(GameOptionsMenu menu, ref float y, string title, string value, string leftLabel, string rightLabel, Action onLeft, Action onRight, string key, TMP_Text? hoverDescriptionTarget = null, string? hoverText = null, string? hoverDefaultText = null)
    {
        var row = CreateOption(menu, y, key);
        if (row == null)
            return;
        row.TitleText.text = title;
        row.ValueText.text = value;
        ConfigureButton(row.MinusBtn, leftLabel, onLeft);
        ConfigureButton(row.PlusBtn, rightLabel, onRight);
        if (hoverDescriptionTarget != null && !string.IsNullOrWhiteSpace(hoverText) && !string.IsNullOrWhiteSpace(hoverDefaultText))
        {
            // StringOption全体にはマウスイベントが来ないため、実際に入力を受ける左右のPassiveButtonへ登録する。
            RoleHoverHint.Configure(row.MinusBtn, hoverDescriptionTarget, hoverText, hoverDefaultText);
            RoleHoverHint.Configure(row.PlusBtn, hoverDescriptionTarget, hoverText, hoverDefaultText);
        }
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

    private static string CategoryTitle(string category) => ColorText("◆ " + category + "役職", category);
    private static string ColorText(string text, string category) => $"<color={CategoryColor(category)}>{text}</color>";
    private static string CategoryColor(string category) => category switch
    {
        "クルー" => "#55D7FF",
        "インポスター" => "#FF6666",
        _ => "#D890FF",
    };

    private static string GetShortDescription(RoleId role) => RoleDescriptionCatalog.Get(role);

    /*
    {
        RoleId.Sheriff => "敵を直接キル",
        RoleId.Doctor => "死亡推定時刻",
        RoleId.MadScientist => "遠隔バイタル",
        RoleId.Tracker => "対象を追跡",
        RoleId.TimeTraveler => "位置を巻戻し",
        RoleId.Seer => "死者と会話",
        RoleId.BarrierNic => "一度だけキル防止",
        RoleId.LightWorker => "停電視界を維持",
        RoleId.Investigator => "足跡を追跡",
        RoleId.Mayor => "会議で2票",
        RoleId.Ninja => "サイレントキル",
        RoleId.Warlock => "呪いによるキル",
        RoleId.Mafia => "連続サボタージュ",
        RoleId.Puppeteer => "他者を操作",
        RoleId.Eraser => "役職を秘匿",
        RoleId.Undertaker => "死体を運搬",
        RoleId.Jester => "追放で単独勝利",
        RoleId.Jackal => "全員をキル",
        RoleId.Vampire => "時間差キル",
        _ => string.Empty,
    };
    */

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
