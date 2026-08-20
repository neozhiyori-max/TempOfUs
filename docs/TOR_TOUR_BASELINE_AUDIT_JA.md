# TOR / TOU-R 基盤選定監査台帳

**作成日:** 2026-08-20  
**対象:** tempMOD（Among Us Steam / x86 IL2CPP / build 24302054）  
**結論:** 公開済みの完成DLLをそのまま導入するのではなく、**TownOfUs-Reworked（TOU-R）を主な役職・設定・同期の移植元、TheOtherRoles（TOR）を不足役職・実装パターンの補助移植元**として採用する。いずれも GPL-3.0 の公開ソースであり、tempMODはこれらを必要に応じて取り込んで現行ゲーム参照DLLで再ビルドする**単体配布MOD**とする。

> **決定事項:** ユーザーにTOR・TOU-R・NOSなどの外部MOD本体を別途導入させない。完成したtempMOD配布物に必要なBepInEx本体とtempMOD DLLを同梱する。

## 1. 前提と検証結果

実機は Steam build `24302054`、Unity `2022.3.44f1`、32ビット IL2CPP である。SteamDB の当該Depotは2026年7月および8月にも更新記録があり、両候補の公開済み完成DLLが対象とする2024年版のAmong Usより新しいゲーム世代である。[1]

公式NOS配布物をそのまま導入してアドオンを載せる方針は、ユーザーの要件に反するため中止した。中断した配置は退避コピーから復元済みであり、BepInExログ上で既存の `tempMOD SNR foundation 0.4.0` が読み込まれることを確認した。

| 項目 | 実機 | TOR v4.8.0 | TOU-R v5.3.1 / 参照ソース |
|---|---:|---:|---:|
| Among Us 世代 | Steam build 24302054（2026年更新） | 2024.11.26 | 公開リリース表: 2024.10.29、参照ソースの `GameVersion`: 2025.3.25 |
| 実行形式 | x86 IL2CPP | IL2CPP | IL2CPP |
| .NETターゲット | .NET 6互換 | `net6.0` | `net6.0` |
| BepInEx 参照 | 実機は be.785 | be.697 | be.733 |
| ライセンス | — | GPL-3.0 | GPL-3.0 |
| 最新公開リリース | — | 2025-02-23 | 2025-04-27 |

この差異により、`TheOtherRoles.dll` や `TownOfUs.dll` を現在のゲームへコピーすることは採用しない。IL2CPP生成型・メソッド署名・UIプレハブが一致しない恐れがあり、過去に発生した会議後暗転、ボタン消失、同期不整合を再発させうるためである。

## 2. 基盤比較

| 評価軸 | TOR（TheOtherRoles） | TOU-R（TownOfUs-Reworked） | tempMODへの評価 |
|---|---|---|---|
| 知名度・実績 | TheOtherRoles v4.8.0として役職・設定・ロールドラフト・推測者を提供 | Town of Us系の大規模役職MODとして多数の役職・設定・結果処理を提供 | どちらも有力な公開移植元 |
| 現行世代への近さ | 2024.11.26向け | 参照ソースは2025.3.25向け | **TOU-Rが優位** |
| 役職実装の分割 | 主要コードは約78 C#ファイル、集中型の `RoleInfo`・`RPC`・`Buttons` | 約444 C#ファイル、クルー・インポスター・第三陣営・設定が役職別に分割 | **TOU-Rが優位**。5役職ずつの移植・検証と整合する |
| ユーザー要件との一致 | Guesser、Cleaner、Morphling、Vampire、Warlock、Ninja等を含む | Sheriff、Medic、Mayor、Tracker、Investigator、Janitor、Morphling、Undertaker、Vampire、Warlock、Jester、Arsonist等を含む | **TOU-Rが優位**。希望役職との重複が多い |
| 設定・同期 | `CustomOptionHolder`、`RPC`、会議・開始・終了パッチがまとまっている | `CustomOption`型群、役職別パッチ、会議ボタン、終了処理が分割されている | TOU-Rを主軸、TORのEvil Guesser等を補助参照 |
| 配布形態 | DLLおよびBepInEx同梱ZIPを公開 | DLLおよびBepInEx同梱ZIPを公開 | tempMODも同様に独立配布できるが、完成DLLの転用はしない |

## 3. 採用アーキテクチャ

tempMODは、TOU-Rの公開ソースから役職・設定・RPC・会議・終了処理の**実績ある構造**を取り込み、現行のゲーム参照DLL群（build 24302054対応）へ移植してビルドする。TORは、マッドゲッサー用の会議中推測フロー、Cleaner、Warlock、Ninjaなど、TOU-Rにない又はユーザー要件へより近い役職の移植元として利用する。

| レイヤー | 採用元 | tempMODでの扱い |
|---|---|---|
| 起動・IL2CPPフック | BepInEx 6（実機対応版） | 配布物へ同梱。ゲーム版と同じ生成参照を使用 |
| 役職登録・設定・同期の主構造 | TOU-R | 独自名前空間とtempMOD設定へ移植し、現行ゲームAPIへ適合 |
| 役職別能力 | TOU-Rを優先、TORを補助 | 既存実装を優先移植。独自実装は対応元がない役職だけ |
| 会議中推測 | TOR Evil Guesser型 | 会議中のみ対象プレイヤーの横へ表示する方式を移植 |
| ロゴ・文言・色 | tempMOD独自 | 起動画面では一回だけ表示。第三陣営は水色、インポスター系は赤 |
| 配布・更新 | tempMOD GitHub Releases | `neozhiyori-max/TempOfUs` でtempMOD独自の更新情報を配布 |

## 4. 安全境界

次の処理は**永久に採用しない**。

| 禁止事項 | 理由 |
|---|---|
| `GameData.AddDummy`、`AmongUsClient.Spawn`、`PlayerControl`複製による疑似参加者追加 | 実際のネットワーク参加者として扱われ、切断・再接続制限を発生させたため |
| 旧版TOR / TOU-Rの完成DLLを現行ゲームへ直接配置 | 生成IL2CPP参照とゲームAPI世代が不一致のため |
| 外部MOD本体を必須にするアドオン構成 | tempMODを単体配布するという要件に反するため |
| 既存設定UIの画像資産を無許可で流用すること | 権利上の懸念を避けるため。構造・操作パターンのみを参照する |

## 5. 次の実装順

最初の5役職は、移植元が明確で現行基盤へのポート量を抑えられる順に実装する。第一セットはインポスター役職とし、**Cleaner（TOU-R Janitor）・Morphling（TOU-R Morphling）・Undertaker（TOU-R Undertaker）・Warlock（TOU-R Warlock）・Mad Guesser（TOR Evil Guesser型）**を候補とする。

各セットでは、(1) 設定UIへの登録、(2) 開始時割当、(3) 専用HUD又は会議ボタン、(4) RPC同期、(5) 会議後の再生成、(6) ホストとクライアント双方のログ、の順に検証する。実機へはビルド・静的監査・禁止API検査を通過したDLLだけを配置する。

## 参考文献

[1]: https://steamdb.info/depot/945361/ "SteamDB — Depot 945361 (Among Us Content)"
[2]: https://github.com/TheOtherRolesAU/TheOtherRoles "TheOtherRolesAU/TheOtherRoles"
[3]: https://github.com/TheOtherRolesAU/TheOtherRoles/releases/tag/v4.8.0 "The Other Roles v4.8.0"
[4]: https://github.com/eDonnes124/Town-Of-Us-R "eDonnes124/Town-Of-Us-R"
[5]: https://github.com/eDonnes124/Town-Of-Us-R/releases/tag/v5.3.1 "Town-Of-Us-R v5.3.1"

[1]: https://steamdb.info/depot/945361/
[2]: https://github.com/TheOtherRolesAU/TheOtherRoles
[3]: https://github.com/TheOtherRolesAU/TheOtherRoles/releases/tag/v4.8.0
[4]: https://github.com/eDonnes124/Town-Of-Us-R
[5]: https://github.com/eDonnes124/Town-Of-Us-R/releases/tag/v5.3.1
