using UnityEngine;

namespace TempMod.Plugin.UI;

/// <summary>
/// 会議画面が標準で持つMeetingAbilityButtonだけを再利用する。
/// 投票パネルやプレイヤー状態の複製は行わず、次に選択した投票対象を能力対象として扱う。
/// </summary>
internal static class MeetingAbilityHudPresenter
{
    private static AbilityButton? _meetingButton;
    private static bool _visible;

    internal static void Refresh(MeetingHud meetingHud)
    {
        if (meetingHud == null || meetingHud.MeetingAbilityButton == null)
            return;

        var button = meetingHud.MeetingAbilityButton;
        _meetingButton = button;
        if (!TempModPlugin.Runtime.TryGetMeetingAbilityButtonState(out var state))
        {
            if (_visible)
            {
                button.ToggleVisible(false);
                _visible = false;
            }
            return;
        }

        _visible = true;
        button.gameObject.SetActive(true);
        button.ToggleVisible(true);
        button.OverrideText(state.Label);
        button.OverrideColor(state.Color);
        button.SetInfiniteUses();
        button.SetCoolDown(state.CooldownRemaining, Mathf.Max(60f, state.CooldownRemaining));
        if (state.IsReady)
            button.SetEnabled();
        else
            button.SetDisabled();
    }

    internal static bool TryHandleClick(AbilityButton button)
    {
        if (!_visible || _meetingButton == null || button != _meetingButton)
            return false;
        TempModPlugin.Runtime.ArmMeetingAbility();
        return true;
    }
}
