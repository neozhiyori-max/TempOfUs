using System;
using System.Collections.Generic;
using System.Linq;
using TempMod.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TempMod.Plugin.UI;

/// <summary>
/// 会議用の既存ConfirmButtonだけを複製して、各プレイヤー横の「推測」ボタンと
/// クルー役職の選択一覧を作る。PlayerControlやネットワーク参加者は一切複製しない。
/// </summary>
internal static class MadGuesserMeetingPresenter
{
    private const string TargetButtonPrefix = "tempMOD_GuesserTarget_";
    private const string RoleButtonPrefix = "tempMOD_GuesserRole_";
    private static readonly List<GameObject> RoleButtons = new();
    private static byte? _selectedTarget;

    internal static void Refresh(MeetingHud meetingHud)
    {
        if (!TempModPlugin.Runtime.CanUseMadGuesserInMeeting())
        {
            RemoveCustomButtons(meetingHud);
            CloseRoleList();
            return;
        }

        var playerStates = meetingHud.playerStates;
        if (playerStates == null)
            return;

        for (var index = 0; index < playerStates.Length; index++)
        {
            var area = playerStates[index];
            if (area == null || PlayerControl.LocalPlayer == null)
                continue;

            // Steam build 24302054ではTargetPlayerIdがPlayerIdへ置き換えられた。
            // PlayerIdはbyteとの暗黙変換を持つため、役職エンジン用のIDへ明示的に正規化する。
            var targetId = (byte)area.PlayerId;
            if (area.AmDead || targetId == PlayerControl.LocalPlayer.PlayerId)
                continue;
            if (area.Buttons == null || area.ConfirmButton == null)
                continue;
            if (area.Buttons.transform.Find(TargetButtonPrefix + targetId) != null)
                continue;

            var buttonObject = UnityEngine.Object.Instantiate(area.ConfirmButton.gameObject, area.Buttons.transform);
            buttonObject.name = TargetButtonPrefix + targetId;
            buttonObject.SetActive(true);
            buttonObject.transform.localScale = new Vector3(.72f, .72f, 1f);
            buttonObject.transform.localPosition = new Vector3(1.35f, 0f, -2f);
            var button = buttonObject.GetComponent<PassiveButton>() ?? buttonObject.GetComponentInChildren<PassiveButton>(true);
            if (button == null)
            {
                UnityEngine.Object.Destroy(buttonObject);
                continue;
            }
            button.ChangeButtonText($"推測 ({TempModPlugin.Runtime.GetMadGuesserShotsRemaining()})");
            button.OnClick = new Button.ButtonClickedEvent();
            button.OnClick.AddListener((UnityAction)(() => OpenRoleList(meetingHud, targetId, area.ConfirmButton.gameObject)));
        }
    }

    private static void OpenRoleList(MeetingHud meetingHud, byte targetId, GameObject template)
    {
        _selectedTarget = targetId;
        CloseRoleList();
        var crewRoles = TempModSettings.SelectableRoles.Where(role => RoleCatalog.GetFaction(role) == Faction.Crew).ToArray();
        for (var index = 0; index < crewRoles.Length; index++)
        {
            var role = crewRoles[index];
            var buttonObject = UnityEngine.Object.Instantiate(template, meetingHud.meetingContents);
            buttonObject.name = RoleButtonPrefix + role;
            buttonObject.SetActive(true);
            buttonObject.transform.localScale = new Vector3(.62f, .62f, 1f);
            var column = index % 2;
            var row = index / 2;
            buttonObject.transform.localPosition = new Vector3(column == 0 ? -2.5f : 2.5f, 2.1f - row * .68f, -10f);
            var button = buttonObject.GetComponent<PassiveButton>() ?? buttonObject.GetComponentInChildren<PassiveButton>(true);
            if (button == null)
            {
                UnityEngine.Object.Destroy(buttonObject);
                continue;
            }
            button.ChangeButtonText(RoleCatalog.Get(role).DisplayName);
            button.OnClick = new Button.ButtonClickedEvent();
            button.OnClick.AddListener((UnityAction)(() =>
            {
                if (_selectedTarget is byte selected)
                    TempModPlugin.Runtime.TryUseMadGuesserGuess(selected, role);
                CloseRoleList();
            }));
            RoleButtons.Add(buttonObject);
        }
        HudManager.Instance?.ShowPopUp($"<color=#FF6666>推測するクルー役職を選択してください。残り {TempModPlugin.Runtime.GetMadGuesserShotsRemaining()} 回</color>");
    }

    private static void RemoveCustomButtons(MeetingHud meetingHud)
    {
        if (meetingHud.playerStates == null)
            return;
        for (var index = 0; index < meetingHud.playerStates.Length; index++)
        {
            var area = meetingHud.playerStates[index];
            if (area?.Buttons == null)
                continue;
            for (var childIndex = area.Buttons.transform.childCount - 1; childIndex >= 0; childIndex--)
            {
                var child = area.Buttons.transform.GetChild(childIndex);
                if (child != null && child.name.StartsWith(TargetButtonPrefix, StringComparison.Ordinal))
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    internal static void CloseRoleList()
    {
        foreach (var button in RoleButtons)
            if (button != null)
                UnityEngine.Object.Destroy(button);
        RoleButtons.Clear();
        _selectedTarget = null;
    }
}
