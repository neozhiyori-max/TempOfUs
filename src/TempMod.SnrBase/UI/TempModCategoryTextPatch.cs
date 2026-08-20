using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using HarmonyLib;
using SuperNewRoles.Modules;

namespace SuperNewRoles.UI;

/// <summary>
/// SNR標準のカテゴリボタンを破壊せず、画像だけを隠して文字ラベルを重ねる。
/// PassiveButton、BoxCollider2D、選択枠、ホバー、SNR側のクリック処理には変更を加えない。
/// </summary>
internal static class TempModCategoryTextPatch
{
    private const string SelectorName = "OptionsMenuSelector(Clone)";
    private const string LabelName = "tempMOD_CategoryLabel";

    private readonly record struct LabelSpec(string ObjectName, string Text, Color Color);

    private static readonly LabelSpec[] Labels =
    {
        new("Setting_Vanilla", "基本設定", new Color(1f, 0.82f, 0.40f, 1f)),
        new("Setting_Crewmate", "クルー", new Color(0.33f, 0.84f, 1f, 1f)),
        new("Setting_Impostor", "インポスター", new Color(1f, 0.40f, 0.40f, 1f)),
        new("Setting_Neutral", "第三陣営", new Color(0.33f, 0.84f, 1f, 1f)),
    };

    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Start))]
    [HarmonyPriority(Priority.Last)]
    internal static class GameSettingMenuStartPatch
    {
        private static void Postfix(GameSettingMenu __instance)
        {
            // SNR側のRoleOptionMenuStartPatchがselector prefabを生成した後に適用する。
            new LateTask(() => Apply(__instance), 0.1f, "tempMOD_CategoryText");
        }
    }

    private static void Apply(GameSettingMenu menu)
    {
        if (menu == null)
            return;

        var selector = menu.transform.Find(SelectorName);
        if (selector == null)
        {
            SuperNewRolesPlugin.Logger?.LogWarning("tempMOD: SNRカテゴリセレクターが見つかりません。文字ラベルは未適用です。");
            return;
        }

        var template = menu.GameSettingsButton != null
            ? menu.GameSettingsButton.GetComponentInChildren<TextMeshPro>(true)
            : null;
        if (template == null)
        {
            SuperNewRolesPlugin.Logger?.LogWarning("tempMOD: カテゴリ文字ラベルのテンプレートが見つかりません。");
            return;
        }

        foreach (var spec in Labels)
        {
            var category = selector.Find(spec.ObjectName);
            if (category == null)
                continue;

            // 元のカテゴリ画像だけを非表示にする。クリック・選択・ホバー用のコンポーネントは保持する。
            foreach (var renderer in category.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.enabled = false;

            var existing = category.Find(LabelName);
            var labelObject = existing != null
                ? existing.gameObject
                : UnityEngine.Object.Instantiate(template.gameObject, category);
            labelObject.name = LabelName;
            labelObject.SetActive(true);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.15f);
            labelObject.transform.localScale = Vector3.one * 0.78f;

            var label = labelObject.GetComponent(Il2CppType.Of<TextMeshPro>()).TryCast<TextMeshPro>();
            if (label == null)
                continue;
            label.text = spec.Text;
            label.color = spec.Color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.fontSize = 2.4f;
        }

        SuperNewRolesPlugin.Logger?.LogInfo("tempMOD: SNR上部カテゴリ4件へ文字ラベルを適用しました。");
    }
}
