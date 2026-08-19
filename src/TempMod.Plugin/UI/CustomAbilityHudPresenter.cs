using TempMod.Plugin.Integration;
using UnityEngine;

namespace TempMod.Plugin.UI;

/// <summary>
/// バニラのAbilityButtonを一つだけ再利用して、カスタム役職の主能力を表示する。
/// UIオブジェクトの複製は行わず、標準ボタンのラベル・色・状態だけを更新する。
/// </summary>
internal static class CustomAbilityHudPresenter
{
    private static bool _customAbilityVisible;

    internal static void Refresh(HudManager hud)
    {
        if (hud == null || hud.AbilityButton == null)
            return;

        var button = hud.AbilityButton;
        if (!TempModPlugin.Runtime.TryGetAbilityButtonState(out var state))
        {
            if (_customAbilityVisible)
            {
                button.ToggleVisible(false);
                _customAbilityVisible = false;
            }
            return;
        }

        _customAbilityVisible = true;
        button.gameObject.SetActive(true);
        button.ToggleVisible(true);
        button.OverrideText(state.Label);
        button.OverrideColor(state.Color);
        if (state.UsesRemaining >= 0)
            button.SetUsesRemaining(state.UsesRemaining);
        else
            button.SetInfiniteUses();

        // SetCoolDownはバニラHUDに残り時間を描かせる。最大値が未設定の能力にも破綻しないよう、
        // 残り時間と60秒の大きい方を表示スケールとして渡す。
        button.SetCoolDown(state.CooldownRemaining, Mathf.Max(60f, state.CooldownRemaining));
        if (state.IsReady)
            button.SetEnabled();
        else
            button.SetDisabled();
    }

    internal static bool TryHandleClick(AbilityButton button)
    {
        if (!_customAbilityVisible || HudManager.Instance == null || HudManager.Instance.AbilityButton != button)
            return false;
        if (PlayerControl.LocalPlayer == null)
            return true;

        TempModPlugin.Runtime.TryUsePrimaryAbility(PlayerControl.LocalPlayer);
        return true;
    }
}
