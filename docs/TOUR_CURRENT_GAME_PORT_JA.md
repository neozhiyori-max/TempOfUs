# TOU-R由来 tempMOD 基盤の現行ゲーム移植記録

## 目的

この記録は、**TownOfUs-Reworked（TOU-R）公開ソースを参照したtempMOD単体配布基盤**を、ユーザー環境の Among Us Steam build `24302054` 向けIL2CPP生成参照DLLで再ビルドするために行った差分を残すものです。これはTOU-Rの完成済みDLLをそのまま導入する方式ではなく、tempMOD固有の識別子・配布物・資産・アップデート先を持つ再ターゲット版です。

> 上流の設定・役職・同期設計を学び、互換性差分を明示的に移植する。ユーザー環境にはTOU-R・TOR・NOSのいずれも事前導入を要求しない。

## 採用根拠

| 観点 | 採用内容 |
| --- | --- |
| 主基盤 | TownOfUs-Reworked（公開GPL-3.0ソース、固定参照 `943c462`） |
| 補助参照 | TheOtherRoles（特に会議中の役職推測型など、必要な役職単位で参照） |
| 対象ゲーム | Steam build `24302054`、Unity 2022.3.44f1、IL2CPP x86 |
| 配布形態 | tempMODの単体配布。ユーザーによる他MOD事前導入は不要 |
| 実装順序 | 役職は5件ずつ。各セットで起動・HUD・会議後・ホスト同期を確認してから次へ進む |

TOU-Rの上流ビルドは旧ゲーム世代を対象としているため、上流DLLを現行ゲームへ直接配置してはいけません。今回の移植は、現行ゲームから生成した `Assembly-CSharp.dll` 等をコンパイル参照に用いて行います。

## 現行APIへの移植差分

初回の再ターゲットでは454件のコンパイル差分が発生しましたが、主因は会議投票APIの命名・引数変更でした。投票者ID・投票先ID・投票状態・会議完了通知を、現行バインディング定義へ対応させた結果、基盤DLLはコンパイル成功しました。

| 旧実装の代表差分 | 現行buildでの対応 |
| --- | --- |
| `PlayerVoteArea.TargetPlayerId` | `PlayerVoteArea.PlayerId` |
| `PlayerVoteArea.VotedFor` | `PlayerVoteArea.VotedForId` |
| `MeetingHud.VoteStates` | `MeetingHud.MeetingStates` |
| 引数なしの `ClearVote()` | 対象投票者IDとローカル操作フラグを明示 |
| 旧形式の `RpcVotingComplete` | `wasOverruled` と `overruleNonce` を明示 |
| IL2CPP役職リストのLINQ | 添字アクセスだけを用いる互換ヘルパー |

## 資産とブランドの境界

上流画像はtempMOD配布物に含めません。移植ツリーの埋め込みリソースは、既存tempMODの自作 `tempofus_logo.png` のみです。旧リソース名を要求する未移植役職は、初期版では同ロゴへフォールバックします。役職ごとの独自能力アイコンは、各5役職セットを有効化する段階でtempMOD独自資産に置き換えます。

現在のプラグイン識別子と表示名は下記です。

| 項目 | 値 |
| --- | --- |
| BepInEx ID | `com.neozhiyori.tempofus` |
| 表示名 | `TempOfUs` |
| 作業版 | `0.5.0-dev` |
| Harmony ID | `com.neozhiyori.tempofus` |

## 安全監査

次の禁止実装を静的検索し、移植ツリー内で検出されないことを確認しました。

| 禁止対象 | 監査結果 |
| --- | --- |
| `GameData.AddDummy` | 検出なし |
| `AmongUsClient.Spawn` | 検出なし |
| `PlayerControl`の複製生成 | 検出なし |

この確認はダミー参加者の生成が発生しないことを保証するための**静的検査**です。実機でのオンライン同期検証は、5役職セットのホスト・参加者テストを通るまで完了扱いにしません。

## 次の実施順序

まずtempMOD専用の起動確認用パッケージを隔離配置し、ロビー画面までの読込ログを確認します。続いて、初期の5役職を有効化し、役職配布、HUDボタン、会議遷移後の復帰、ホスト同期の順で検証します。既存SNR基盤の実機環境は、検証版がこの基準を満たすまで保持します。

## 参照

[1] [TownOfUs-Reworked — GitHub repository](https://github.com/AlchlcDvl/TOU-Reworked)

[2] [TheOtherRoles — GitHub repository](https://github.com/TheOtherRolesAU/TheOtherRoles)

[3] [GNU General Public License v3.0](https://www.gnu.org/licenses/gpl-3.0.en.html)
