# tempMOD v0.2.0

**tempMOD** は、Steam版 Among Us 向けのカスタム役職MODです。20種類の役職、ホスト用役職設定、少人数開始、役職専用HUD、試合リザルト、公開版の自動更新を提供します。タイトル画面には、背景を覆わない透過ロゴ **TempOfUs** を表示します。

> **公開テスト版です。** オンラインで遊ぶ参加者全員が、同じ `tempMOD` 公開版を導入してください。未導入または異なる版の参加者を混在させると、役職・能力・リザルトの同期は保証されません。

> tempMODは Innersloth LLC と提携・承認・後援関係にありません。Among Us本体を更新する前後は、別コピーのゲームフォルダで互換性を確認してください。

## 対象環境

| 項目 | 検証値 |
| --- | --- |
| 対象ゲーム | Steam版 Among Us（PC / Windows） |
| Steam build ID | `23244517` |
| 実行形式 | 32ビット（x86）IL2CPP |
| Unity | `2022.3.44f1` |
| MOD基盤 | BepInEx 6.0.0-be.785（Unity IL2CPP win-x86） |
| tempMOD公開版 | `0.2.0` |

BepInExのIL2CPP導入手順では、32ビットWindows実行形式には `Unity.IL2CPP-win-x86` を使用するよう案内されています。[1]

## インストール

1. [最新リリース](../../releases/latest) から **`tempMOD-full.zip`** をダウンロードします。
2. ZIPをAmong Usのインストールフォルダへ展開します。Steamの標準例は `C:\Program Files (x86)\Steam\steamapps\common\Among Us\` です。
3. `BepInEx\plugins\TempMod.dll` と `BepInEx\plugins\TempMod.Core.dll` が配置されていることを確認します。
4. ゲームを起動します。タイトルにTempOfUsロゴが表示され、`BepInEx\LogOutput.log` に `Loading [tempMOD 0.2.0]` があれば読み込み成功です。

`tempMOD-full.zip` には、対象環境で動作確認済みの **BepInEx 6 IL2CPP x86ランタイム** を同梱しています。ゲーム本体、個人設定、ログ、ゲームごとに生成されるインターオプ解析物は含めません。BepInExを既に導入済みの場合は、軽量な `tempMOD-public.zip` を展開するだけでも更新できます。

## 自動更新

公開版は起動時にこのリポジトリのGitHub Releasesを確認します。新しい公開版の `tempMOD-public.zip` がある場合、バックグラウンドで取得し、**ゲーム終了後に自動で展開**します。新しい版は次回起動時から有効です。自動更新はBepInEx本体を上書きしない軽量なMOD更新だけを適用します。

更新確認に失敗してもゲームは通常どおり起動し、現在の版は変更されません。更新が適用されない場合は、ReleasesからZIPを手動で上書き展開してください。管理者検証版は公開版へ置き換わらないよう、自動更新を行いません。

## マルチプレイヤー公開テスト

ホストはtempMOD公開版を導入した状態で通常どおりオンライン部屋を作り、参加者は同じ公開版を導入してルームコードで入室します。開始時にホストが役職を抽選し、役職・死亡・死体・クールダウンの確定状態を参加者へ同期します。能力要求は参加者からホストへ送信され、ホストだけが距離・生死・クールダウン・会議状態を判定します。

| 確認項目 | テスト方法 |
| --- | --- |
| 役職同期 | 2人以上で開始し、各自の開始演出で割当役職と陣営色が表示されることを確認します。 |
| アンダーテイカー | 死体へ近づくと右下の標準AbilityButtonが **`牽引`**、担いだ後は **`配置`** に変わることを確認します。 |
| キル・能力 | シェリフ、ジャッカル、ヴァンパイア、ウォーロックなどで、ホストと参加者の死亡・死体・クールダウン状態が一致することを確認します。 |
| リザルト | 終了時にチャットと終了画面へ勝者・役職・キル記録が表示されることを確認します。 |

**必須条件**は、ホストと参加者全員が同じ公開版を使うことです。役職のON/OFF、人数、出現率、詳細設定はホストが設定します。管理者検証版を公開マルチへ混在させないでください。

## 実装済みの主な機能

| 分類 | 内容 |
| --- | --- |
| 役職 | クルー10、インポスター6、第三陣営4。シェリフ、ドクター、アンダーテイカー、ジェスター、ラバーズなどを含みます。 |
| 設定 | 陣営別役職数、役職ON/OFF、人数、10%刻みの出現率、シェリフ詳細設定など。 |
| 開始 | 1人から開始可能。開始演出で役職名、陣営色、説明文を表示します。 |
| HUD | 標準AbilityButtonを役職専用ボタンとして利用します。アンダーテイカーは `牽引` / `配置`、シェリフなどは残り使用回数を表示します。 |
| 同期 | ホスト権限で役職、能力要求、死亡、死体、クールダウンを同期します。能力要求はRPC発信元と要求者IDの一致も検証します。 |
| 結果 | チャットと終了画面に勝者・役職・キル記録を表示します。 |

全ルール判定はホスト側の `RoleEngine` が確定します。クライアント側は能力要求と確定状態の表示を担当します。

## 管理者検証版

`tempMOD-admin.zip` は開発・検証用です。`ゲームを終了しない` 設定と、ホスト向けの `Shift + L + Enter` 廃村操作を含みます。一般参加者へ配布したり、公開テスト部屋へ混在させたりしないでください。描画やネットワーク状態を壊すテストダミー機能は含まれていません。

## ビルド

ゲーム参照DLLを `game_refs/AU` に用意したLinux環境では、次のコマンドでビルドできます。

```bash
# 公開版
/home/ubuntu/.dotnet/dotnet build src/TempMod.Plugin/TempMod.Plugin.csproj \
  --nologo -p:AmongUsRoot=/home/ubuntu/tempMOD/game_refs/AU

# 管理者検証版
/home/ubuntu/.dotnet/dotnet build src/TempMod.Plugin/TempMod.Plugin.csproj \
  --nologo -p:AmongUsRoot=/home/ubuntu/tempMOD/game_refs/AU -p:AdminBuild=true
```

## ライセンスと謝辞

tempMODは [GPL-3.0](LICENSE) で公開します。開始演出などの構造設計では [SuperNewRoles](https://github.com/SuperNewRoles/SuperNewRoles) を参考にし、当リポジトリで独自実装しています。詳細な通知は [NOTICE.md](NOTICE.md) を参照してください。

## 参照

[1]: https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html "BepInEx: Installing BepInEx on Il2Cpp Unity"
[2]: https://github.com/SuperNewRoles/SuperNewRoles "SuperNewRoles/SuperNewRoles"
