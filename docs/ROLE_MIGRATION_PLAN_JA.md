# tempMOD 役職移行・安定化計画

## 方針

tempMODは、独自の仕組みを増やすのではなく、**公開済みで実績のあるAmong Us MODの役職挙動を優先して移行する**。対象は [Town Of Us R][tou]、[SuperNewRoles][snr]、[TheOtherRoles][tor]、[Nebula][nebula] とする。既存MODに同等の役職がある場合は、役職の能力、対象選択、クールダウン、会議遷移、表示、状態同期を参照元のパターンへ寄せる。参照元のない役職だけを独自役職として扱い、共有基盤の範囲に限定して実装する。

> 参照コードを取り込む場合は、参照先のライセンス条件を守り、変更履歴とクレジットを `NOTICE.md` およびREADMEへ追記する。Town Of Us R、SuperNewRoles、TheOtherRolesは取得時点でGPL-3.0ライセンスを確認している。[1] [2] [3]

## 安定化の共通原則

| 原則 | 実装上のルール |
|---|---|
| 対象判定 | HUDの点灯、クリック時の対象検索、RoleEngineの最終検証で、同じ射程・生存・陣営条件を使う。 |
| 会議遷移 | 会議開始と終了を対にして扱う。`RpcClose`では会議状態を必ず解除し、通常HUDを再生成可能にする。 |
| 状態変更 | 勧誘・感染・陣営変更は、役職エンジン、ゲーム本体のネイティブ役職、名前色／HUD、同期データを同じイベントで更新する。 |
| ネットワーク安全 | オンライン・ローカルロビーで偽プレイヤーを生成しない。ダミーを作るAPI、プレイヤー複製、Spawn RPCは使用しない。 |
| 検証 | 役職単位テストに加え、対象外拒否、会議前後、同陣営キル禁止、同期イベント、HUD点灯の共通不変条件を回帰テストにする。 |

## 役職対応表

**区分**は、`直接移植候補` が公開参照先に同名または明確な同等実装を確認できたもの、`近縁移植候補` が能力の核となる既存パターンを確認できたもの、`独自補完` が現時点で同等公開実装を確認できず独自要件として扱うものを示す。Nebulaはこの調査時点で公開リポジトリに実装ソースを確認できなかったため、UI・仕様の参照候補として扱い、コード移植元には含めない。

| 陣営 | tempMOD役職 | 区分 | 主な参照候補 | 移行時の要点 |
|---|---|---|---|---|
| クルー | シェリフ | 第1段階移植・回帰確認済み | TOU `Sheriff.cs`、SNR `Sheriff.cs` | 誤射・キル回数・対象陣営の設定判定をホスト権限エンジンへ適合済み。敵のキル、クルー誤射時の自爆、第三陣営キル可否、上限回数をセルフテストで回帰確認する。 |
| クルー | ドクター | 近縁移植候補 | TOU `Medic.cs` | 防御ではなく死亡時刻表示を独自補完し、死体対象処理は共有化する。 |
| クルー | マッドサイエンティスト | 近縁移植候補 | バニラScientist／SNR能力基盤 | バイタル表示とクールダウンを標準端末UI再利用で安定化する。 |
| クルー | トラッカー | 直接移植候補 | TOU `Tracker.cs` | 対象選択、矢印寿命、会議リセットを移行する。 |
| クルー | タイムトラベラー | 近縁移植候補 | TOU Time Lord系 | 位置履歴と会議中の時間停止を共通化する。 |
| クルー | シーア | 直接移植候補 | TOU/SNR `Seer`・`Medium` | 死者対象の表示と会議中無効化を移行する。 |
| クルー | バリアニック | 近縁移植候補 | TOU `Medic.cs`、Barrier系 | バリア消費・キル拒否を共通防御イベントへ統一する。 |
| クルー | ライトワーカー | 直接移植候補 | SNR `Lighter.cs` | 視界計算だけを差し替え、他HUDへ介入しない。 |
| クルー | インベスティゲーター | 直接移植候補 | TOU `Investigator.cs`／`Detective.cs` | 足跡生成と消滅をホスト確定状態へ寄せる。 |
| クルー | 市長 | 直接移植候補 | TOU/SNR `Mayor.cs` | 会議票数を会議専用の集計処理へ集約する。 |
| インポスター | ニンジャ | 近縁移植候補 | TOR `NinjaTrace.cs` | 無音キル・可視性をキルイベントの表示フラグへ集約する。 |
| インポスター | ウォーロック | 直接移植候補 | TOU `Warlock.cs` | 呪い対象、すれ違い判定、会議中タイマー停止を移行する。 |
| インポスター | マフィア | 第1段階移植・回帰確認済み | SNR `Mafia.cs` | SNRの`Mafia.IsKillFlag`へ適合。他の生存インポスターが残る間はキルを拒否し、全員マフィアまたは他インポスター死亡後に通常キルを解放する。 |
| インポスター | パペッティア | 近縁移植候補 | SNR操作系Ability | 操作固定・解除・会議リセットを共通拘束状態へ移す。 |
| インポスター | イレイザー | 近縁移植候補 | TOR Eraser系 | キルと役職隠蔽を独立した死亡属性として同期する。 |
| インポスター | アンダーテイカー | 直接移植候補 | TOU `Undertaker.cs` | 担ぎ上げ・配置・速度低下・会議解除を移行する。 |
| インポスター | クリーナー | 近縁移植候補 | TOU系Cleanerパターン | 死体対象・硬直・通報不可を死体状態へ集約する。 |
| インポスター | マッドゲッサー | 直接移植候補 | SNR `EvilGuesser.cs` | 会議中だけの対象ボタン・正誤判定・自爆を移行する。 |
| インポスター | モーフィング | 直接移植候補 | TOU `Morphling.cs` | DNA採取、変身時間、解除時の復帰を移行する。 |
| インポスター | マリオネット | 近縁移植候補 | SNR操作・キルAbility | 強制キルと死体非表示は別イベントで処理する。 |
| インポスター | ボマー | 直接移植候補 | TOU `Bomber.cs`、SNR `SelfBomber.cs` | 設置、範囲、遅延、会議中停止を移行する。 |
| インポスター | スパイ | 直接移植候補 | TOU `Spy.cs` | 情報表示だけを対象にし、チャット通信は改変しない。 |
| インポスター | トラッパー | 直接移植候補 | TOU `Trapper.cs` | 罠数上限、入口判定、持続時間を移行する。 |
| インポスター | ブラックアウト | 近縁移植候補 | SNR BlackHat Hacker系 | 対象範囲の暗転を一時効果として同期する。 |
| インポスター | ファントム | 直接移植候補 | TOU `Phantom.cs` | 幽体化中のキル禁止、解除、当たり判定を移行する。 |
| インポスター | バウンティハンター | 近縁移植候補 | SNR `Hitman.cs` | ターゲット指定と成功／失敗時クールダウンを移行する。 |
| インポスター | ヴァンパイアロード | 近縁移植候補 | SNR `VampireDependent.cs` | 蘇生・従者状態は独自補完だが、従属状態の同期を参照する。 |
| インポスター | ハッカー | 直接移植候補 | SNR `EvilHacker.cs`／`Datahacker.cs` | 端末使用中だけの偽装状態として実装する。 |
| インポスター | イリュージョニスト | 近縁移植候補 | SNR `Mirage.cs` | 分身／偽情報を視覚専用状態として扱う。 |
| インポスター | サイレンサー | 独自補完 | 会議沈黙の既存会議Ability基盤 | 対象の会議入力制限を明示的に同期する。 |
| インポスター | グラトニー | 独自補完 | 死体清掃・捕食の共通基盤 | 清掃との差分は演出のみとし、死体状態を共有する。 |
| インポスター | タイムシーフ | 独自補完 | クールダウン／時間系基盤 | タスク制限時間の扱いをバニラ勝利条件と衝突させない。 |
| インポスター | ディセプター | 独自補完 | SNR会議票系Ability | 投票改竄は会議集計前の明示的な一回限り効果にする。 |
| インポスター | ネクロマンサー | 直接移植候補 | SNR `Necromancer.cs` | 死体操作は死体状態の移動に限定する。 |
| インポスター | ウィッチ | 近縁移植候補 | TOU Warlock系 | 二者リンク・後追いキルをリンク状態として同期する。 |
| インポスター | アルケミスト | 独自補完 | BodyHidden共通基盤 | 透明死体と解除音を死体可視属性として扱う。 |
| 第三陣営 | 神（ゴッド） | 第1段階移植・実機検証待ち | SNR `God.cs`、`KnowOtherAbility.cs` | SNR同様に受動的な情報役職として扱い、全知ボタンを廃止。神視点では全プレイヤーの名前下へ役職を常時表示し、神自身は金色で表示する。 |
| 第三陣営 | ジェスター | 直接移植候補 | TOU `Jester.cs`、SNR `Teruteru.cs` | 追放時勝利を会議終了処理で最優先する。 |
| 第三陣営 | ジャッカル | 第1段階の完全移植 | SNR `JackalAbility.cs`、`CustomSidekickButtonAbility.cs`、`JSidekickAbility.cs` | 1人だけ勧誘、味方色、相互キル禁止、親死亡時昇格、他キラー陣営を残した誤勝利防止を移行する。正確なファイル別クレジットは次節を参照。 |
| 第三陣営 | ヴァンパイア | 第1段階移植・回帰確認済み | TOU/SNR `Vampire.cs` | 噛みつき遅延、会議中停止、死亡時解除をホスト権限エンジンへ適合済み。会議中は残り時間が停止し、会議終了後に正確な残り時間から再開する。 |
| 第三陣営 | シュレディンガーの猫 | 直接移植候補 | SNR `SchrodingersCat.cs` | 最初の攻撃／投票での陣営同調を役職変更イベントで同期する。 |
| 第三陣営 | ゾンビ | 独自補完 | 感染・従属状態の共通基盤 | 子ゾンビ化と勝利判定を専用陣営状態で管理する。 |
| 第三陣営 | アパシー | 独自補完 | タスク削除の共通基盤 | 死亡方法別の勝敗を死亡イベントで判定する。 |
| 第三陣営 | アドボケイト | 近縁移植候補 | 会議票数Ability基盤 | 買収対象の零票・自身二票を会議集計に限定する。 |
| 第三陣営 | ピエロ | 近縁移植候補 | Jester系＋操作反転基盤 | 操作反転は短時間入力効果として実装する。 |
| 第三陣営 | アルソニスト | 第1段階移植・回帰確認済み | TOU/SNR `Arsonist.cs` | 生存者への注油、全対象への注油完了後の点火、即時勝利をホスト権限エンジンへ適合済み。注油進捗は会議後も同期状態として保持される。 |
| 第三陣営 | テロリスト | 近縁移植候補 | SNR `SelfBomber.cs` | 自爆範囲と対象陣営判定を分離する。 |
| 第三陣営 | ハゲタカ | 第1段階移植・実機検証待ち | SNR `Vulture.cs` | SNR既定の30秒回収クールダウン・必要死体数3体を設定化。死体回収時に既存DeadBodyを通報不能として無効化し、必要数到達時は即座に単独勝利を確定する。 |
| 第三陣営 | コレクター | 独自補完 | アイテム／タスク進捗基盤 | タスク妨害と収集数を明示的な状態値で同期する。 |
| 第三陣営 | ガーディアン | 近縁移植候補 | TOU `GuardianAngel.cs` | 守護対象・残数・対象死亡時の失格を同期する。 |
| 第三陣営 | ファナティック | 近縁移植候補 | SNR `JackalFriends.cs` | 崇拝対象と共同勝利をチームリンクとして扱う。 |
| 第三陣営 | シーフ | 近縁移植候補 | SNR `Robber.cs` | 外見・名前の奪取と元役職能力無効化を分離する。 |
| 第三陣営 | ゴーストハンター | 独自補完 | ゴースト可視／捕獲基盤 | タスク総数への影響を慎重に検証する。 |
| 第三陣営 | バウンサー | 独自補完 | ドア・移動拘束基盤 | 強制退場とドアロックを別効果にする。 |
| 第三陣営 | スペクテイター | 近縁移植候補 | Watcher／幽体化基盤 | 本体無防備と壁透過を分離する。 |
| 第三陣営 | アサシン | 直接移植候補 | TOU `Assassin.cs` | 一回限り暗殺と位置通知をイベント化する。 |

## ファイル単位の移植クレジット

| tempMOD対象 | 実装状態 | 上流MOD・固定コミット | 参照／適合元ファイル | tempMOD側の適合差分 |
|---|---|---|---|---|
| ジャッカル／サイドキック | 第1段階の完全移植 | SuperNewRoles `713c98779e14000479f7578a28705264645f07e5`（GPL-3.0） | `Roles/Ability/JackalAbility.cs`、`Roles/Ability/CustomSidekickButtonAbility.cs`、`Roles/Ability/JSidekickAbility.cs`、`Modules/ExPlayerControl.cs`、`Modules/CheckEndGame.cs` | SNRのAbility基盤をtempMODのホスト権限`RoleEngine`へ適合。勧誘対象・専用クールダウン・1人上限・チーム可視性・相互キル禁止・親死亡時昇格・他キラー陣営を除外する勝利条件を移行した。チーム内の名前色は第三陣営の水色で表示する。`PlayerControl`の追加／複製やSpawn RPCは使用しない。 |
| マフィア | 第1段階移植・回帰確認済み | SuperNewRoles `713c98779e14000479f7578a28705264645f07e5`（GPL-3.0） | `Roles/Impostor/Mafia.cs` | SNRの`Mafia.IsKillFlag`と同じく、他の生存インポスターがいる間はキルを拒否し、最後に残った時点で通常キルを解放する。専用の連続サボタージュは使用しない。 |
| アンダーテイカー | 第1段階移植・実機検証待ち | TownOfUs-Reworked `943c46200cd12b2772cfa883a1c1a37cf9c3bb35`（GPL-3.0） | `source/Patches/Roles/Undertaker.cs`、`source/Patches/ImpostorRoles/UndertakerMod/DragBody.cs`、`PerformKillButton.cs`、`KillButtonTarget.cs`、`PlayerControlUpdate.cs`、`UpdateSpeed.cs` | 死体の射程内選択、牽引中の追従・緑アウトライン・減速、配置、死亡／会議／ベント時の安全な自動配置をtempMODの`BodyState`とホスト同期へ適合した。実機で牽引表示・ベント・梯子の最終確認を待つ。 |
| クリーナー | 第1段階移植・実機検証待ち | TownOfUs-Reworked `943c46200cd12b2772cfa883a1c1a37cf9c3bb35`（GPL-3.0） | `source/Patches/Roles/Janitor.cs`、`source/Patches/ImpostorRoles/JanitorMod/PerformKillButton.cs`、`KillButtonTarget.cs`、`PlayerControlUpdate.cs`、`Coroutine.cs` | 射程内死体だけを対象化し、清掃硬直中に移動を止め、清掃進捗をホスト確定で同期する。既存`DeadBody`は清掃中にフェードし、完了時に通報不能として無効化する。実機でフェード・硬直解除の最終確認を待つ。 |
| マッドゲッサー | 第1段階移植・実機検証待ち | SuperNewRoles `713c98779e14000479f7578a28705264645f07e5`（GPL-3.0） | `Roles/Impostor/EvilGuesser.cs`、`Roles/Ability/GuesserAbility.cs`、`Events/GuesserShotEvent.cs`、`Roles/Ability/GuesserTrophies.cs` | 会議限定の対象選択、役職一覧、正誤による対象キル／自爆、会議ごとの残弾・上限・次会議での回復を`MeetingHud`互換UIとホスト同期へ適合した。推測ボタンは残り回数を表示する。ページング演出は後続段階で上流UIへ寄せる。 |
| モーフィング | 第1段階移植・実機検証待ち | TownOfUs-Reworked `943c46200cd12b2772cfa883a1c1a37cf9c3bb35`（GPL-3.0） | `source/Patches/Roles/Morphling.cs`、`source/Patches/ImpostorRoles/MorphlingMod/HudManagerUpdate.cs`、`MorphUnmorph.cs`、`PerformKill.cs`、`SetTarget.cs` | DNA採取と時間制限は既存ホスト同期を使用し、変身中は`RawSetOutfit`で既存PlayerControlの見た目だけを対象の外見へ切替える。ネットワーク外見RPC・PlayerControl複製は使用せず、終了時に本人の外見へ復元する。 |
| シェリフ | 第1段階移植・回帰確認済み | TownOfUs-Reworked `943c46200cd12b2772cfa883a1c1a37cf9c3bb35`（GPL-3.0） | `source/Patches/Roles/Sheriff.cs`、`source/Patches/CrewmateRoles/SheriffMod/` | 直接キル、クルー誤射時の自爆、ゲーム中のキル回数上限、第三陣営をキル可能にする設定を既存のホスト権限キル処理へ適合した。 |
| ヴァンパイア | 第1段階移植・回帰確認済み | TownOfUs-Reworked `943c46200cd12b2772cfa883a1c1a37cf9c3bb35`（GPL-3.0） | `source/Patches/Roles/Vampire.cs`、`source/Patches/NeutralRoles/VampireMod/PerformKill.cs` | 噛みつきで確定した死亡予定時刻をホスト同期し、会議中は残り時間を停止、会議終了後に再開する。対象が先に死亡した場合は予定を解除する。 |
| アルソニスト | 第1段階移植・回帰確認済み | SuperNewRoles `713c98779e14000479f7578a28705264645f07e5`（GPL-3.0） | `SuperNewRoles/Roles/Neutral/Arsonist.cs`、`SuperNewRoles/Roles/Ability/ArsonistAbility.cs` | 生存者全員へ注油するまで点火を拒否し、達成後はホストが対象全員を点火キルして単独勝利を確定する。注油済み状態は会議をまたいで維持する。 |
| 神（ゴッド） | 第1段階移植・実機検証待ち | SuperNewRoles `713c98779e14000479f7578a28705264645f07e5`（GPL-3.0） | `SuperNewRoles/Roles/Neutral/God.cs`、`SuperNewRoles/Roles/Ability/KnowOtherAbility.cs` | 全知ボタンを廃止し、SNRの受動情報能力に合わせて神視点だけで全員の名前下へ役職を常時表示する。表示の再適用には既存`Cosmetics.nameText`だけを使用し、プレイヤー複製や外見RPCは使わない。神自身の名前・役職表示は金色に統一する。 |
| ハゲタカ | 第1段階移植・回帰確認済み | SuperNewRoles `713c98779e14000479f7578a28705264645f07e5`（GPL-3.0） | `SuperNewRoles/Roles/Neutral/Vulture.cs`、`Roles/Ability/EatDeadBodyAbility` | SNR既定の回収CD 30秒・必要死体数3体を個別設定化し、回収済み死体を通報不能として無効化する。必要数到達時はSNR同様に回収操作で即座に単独勝利を発火する。 |

> この表で「完全移植」と表記するものは、上流の役職単位の状態遷移・対象判定・会議／死亡処理・勝利判定を移植対象としていることを示します。異なるネットワーク層・Unity／IL2CPP版・プロジェクト構造に合わせた適合コードはtempMOD側で独自に記述し、上流ファイル、コミット、ライセンス、差分をこの表に追記します。

## 実装の優先順

最初に、すべての役職で共通となる対象選択、会議開始／終了、キル、死体、役職変更、HUD再生成、状態同期を安定化する。その次に、実機で確認頻度が高く、既存MODの直接移植候補が明確なシェリフ、アンダーテイカー、クリーナー、マッドゲッサー、モーフィング、ジャッカル、ヴァンパイア、アルソニスト、ハゲタカを優先する。独自補完の役職は、その共通基盤がテスト済みになるまで複雑な演出を増やさない。

## 参照先とライセンス

| 参照MOD | 取得コミット | 利用方針 |
|---|---:|---|
| TheOtherRoles | `b782da4feb433a4b0426aefac63db29d530dc523` | GPL-3.0。役職の状態遷移・チーム可視性・ボタン可否の参照。 |
| Town Of Us R | `943c46200cd12b2772cfa883a1c1a37cf9c3bb35` | GPL-3.0。クルー／中立役職、会議・死亡・役職ボタンの参照。 |
| SuperNewRoles | `713c98779e14000479f7578a28705264645f07e5` | GPL-3.0。会議中能力、ジャッカル、複雑な役職Ability基盤の参照。 |
| Nebula | `ceb72fe1336ba08e662f772f369891c835da2c2e` | UI・仕様の参照候補。今回取得した公開リポジトリには移植対象のC#ソースを確認できなかったため、コード流用はしない。 |

[1]: https://github.com/TheOtherRolesAU/TheOtherRoles/blob/master/LICENSE "TheOtherRoles GPL-3.0 license"
[2]: https://github.com/eDonnes124/Town-Of-Us-R/blob/master/LICENSE "Town Of Us R GPL-3.0 license"
[3]: https://github.com/SuperNewRoles/SuperNewRoles/blob/master/LICENSE "SuperNewRoles GPL-3.0 license"
[tou]: https://github.com/eDonnes124/Town-Of-Us-R "Town Of Us R"
[snr]: https://github.com/SuperNewRoles/SuperNewRoles "SuperNewRoles"
[tor]: https://github.com/TheOtherRolesAU/TheOtherRoles "TheOtherRoles"
[nebula]: https://github.com/Dolly1016/Nebula "Nebula"
