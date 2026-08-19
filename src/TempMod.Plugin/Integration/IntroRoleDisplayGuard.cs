using System;
using UnityEngine;

namespace TempMod.Plugin.Integration;

/// <summary>
/// バニラの開始演出コルーチンが標準の陣営名を書き戻した後に、
/// 確定済みのtempMOD役職名を毎フレーム再適用するためのIL2CPPコンポーネント。
/// </summary>
public sealed class IntroRoleDisplayGuard : MonoBehaviour
{
    public IntroRoleDisplayGuard(IntPtr pointer)
        : base(pointer)
    {
    }

    public void Update()
    {
        var intro = IntroCutscene.Instance;
        if (intro != null)
            TempModPlugin.Runtime.ApplyRoleIntro(intro);
    }
}
