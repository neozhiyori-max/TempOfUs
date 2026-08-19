using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TempMod.Plugin.UI;

/// <summary>
/// Among Us標準PassiveButtonのマウスイベントに対応した役職説明レジストリ。
/// 行全体のColliderには依存せず、実際にクリックできる左右ボタンで確実に説明を切り替える。
/// </summary>
public sealed class RoleHoverHint : MonoBehaviour
{
    private sealed record HintBinding(TMP_Text DescriptionTarget, string Hint, string DefaultText);
    private static readonly Dictionary<int, HintBinding> Bindings = new();

    public RoleHoverHint(IntPtr pointer)
        : base(pointer)
    {
    }

    internal static void Configure(PassiveButton? button, TMP_Text descriptionTarget, string hint, string defaultText)
    {
        if (button != null)
            Bindings[button.GetInstanceID()] = new HintBinding(descriptionTarget, hint, defaultText);
    }

    internal static void Show(PassiveButton button)
    {
        if (Bindings.TryGetValue(button.GetInstanceID(), out var binding) && binding.DescriptionTarget != null)
            binding.DescriptionTarget.text = "<size=48%>" + binding.Hint + "</size>";
    }

    internal static void Restore(PassiveButton button)
    {
        if (Bindings.TryGetValue(button.GetInstanceID(), out var binding) && binding.DescriptionTarget != null)
            binding.DescriptionTarget.text = "<size=55%>" + binding.DefaultText + "</size>";
    }
}
