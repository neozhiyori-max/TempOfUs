# インポスター第1波 — SNR基盤登録台帳

**対象基盤:** SuperNewRoles `713c98779e14000479f7578a28705264645f07e5`  
**tempMOD基盤版:** `0.4.0`  
**段階:** 役職モデル・役職設定の登録まで完了。能力、会議、カスタムRPC、役職抽選、勝利判定は**未有効**です。

## 目的

最初の5役職は、SNRに直接対応するインポスター役職を選びます。未検証役職を一括で起動せず、tempMODの役職登録スコープにはこの5役職だけを含めます。SNRの役職モデル・設定モデルが現行Among Us環境で安定して登録されることを確認した後、同期、会議、HUD、能力、抽選を順に有効化します。

| tempMOD表示名 | SNR実装 | SNR RoleId | SNR基準の主な機能 | 現在の状態 |
|---|---|---|---|---|
| ニンジャ | `Roles/Impostor/Kunoichi.cs` | `Kunoichi` | クナイによるキルと潜伏系設定 | 役職・設定登録済み |
| マフィア | `Roles/Impostor/Mafia.cs` | `Mafia` | 上流SNRのマフィアAbilityとキル条件 | 役職・設定登録済み |
| パペッティア | `Roles/Impostor/RemoteController.cs` | `RemoteController` | マークした対象の遠隔操作 | 役職・設定登録済み |
| マッドゲッサー | `Roles/Impostor/EvilGuesser.cs` | `EvilGuesser` | 会議中の役職推測ショット | 役職・設定登録済み |
| ブラックアウト | `Roles/Impostor/Jammer.cs` | `Jammer` | 対象への妨害・暗転系Ability | 役職・設定登録済み |

## 現在の安全境界

> 起動時に登録される役職は上表の5件だけです。SNRの修飾子・ゴースト役職・その他の役職は登録しません。

SNRの`CustomRoleManager`と`CustomOptionManager`にはtempMOD用の探索スコープを設けました。これにより、反射走査を使うSNR基盤でも、未検証の役職が静的インスタンス化や設定登録を受けません。実機ログでは5役職、修飾子0件、ゴースト役職0件の登録を確認しています。

現段階では、次の機能を**意図的に無効**にしています。Harmonyの全パッチ適用、カスタムRPC登録と送受信、役職抽選、Abilityボタン、会議処理、勝利判定、独自サーバ、外部API、解析、SNR更新、告知、カスタムコスメ、CPUアフィニティ変更です。

## 次の受入ゲート

| ゲート | 合格条件 |
|---|---|
| 設定UI | 5役職だけがtempMODの設定画面に表示され、出現率・人数・個別設定を変更できる。 |
| 抽選と同期 | ホストだけが5役職から抽選し、導入済みクライアント間で役職と設定が一致する。 |
| HUDとAbility | 対応するSNR Abilityが役職本人だけに表示され、会議後にも正しく復帰する。 |
| 会議 | マッドゲッサーの会議ショット、会議開始／終了での状態解除、未認識RPCの安全な無視を確認する。 |
| 役職別動作 | 各役職の能力・キル・死亡・勝利・クールダウンを1役職ずつ回帰テストする。 |
| 安全監査 | `GameData.AddDummy`、`AmongUsClient.Spawn`、`PlayerControl`複製を実行コードに含めない。 |

## 参照

- [SuperNewRoles fixed reference commit](https://github.com/SuperNewRoles/SuperNewRoles/tree/713c98779e14000479f7578a28705264645f07e5)
- [SNR Kunoichi](https://github.com/SuperNewRoles/SuperNewRoles/blob/713c98779e14000479f7578a28705264645f07e5/SuperNewRoles/Roles/Impostor/Kunoichi.cs)
- [SNR Mafia](https://github.com/SuperNewRoles/SuperNewRoles/blob/713c98779e14000479f7578a28705264645f07e5/SuperNewRoles/Roles/Impostor/Mafia.cs)
- [SNR RemoteController](https://github.com/SuperNewRoles/SuperNewRoles/blob/713c98779e14000479f7578a28705264645f07e5/SuperNewRoles/Roles/Impostor/RemoteController.cs)
- [SNR EvilGuesser](https://github.com/SuperNewRoles/SuperNewRoles/blob/713c98779e14000479f7578a28705264645f07e5/SuperNewRoles/Roles/Impostor/EvilGuesser.cs)
- [SNR Jammer](https://github.com/SuperNewRoles/SuperNewRoles/blob/713c98779e14000479f7578a28705264645f07e5/SuperNewRoles/Roles/Impostor/Jammer.cs)
