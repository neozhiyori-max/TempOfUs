# tempMOD 引き継ぎサマリー

**作成日時:** 2026-08-19（JST）  
**目的:** PC版 Among Us 用の役職MOD「tempMOD」を作成する。ユーザーが指定した20役職と、起動画面ロゴ表示を実装する。

## ユーザー確定要件

MODの正式名称は **`tempMOD`** である。起動画面では、参考として提示されたSuperNewRolesのように、中央ニュースパネル領域に専用ロゴを表示する。ユーザーが提供したロゴ画像の文字は **`TempOfUs`** であり、現時点では「正式名称は tempMOD、ロゴ表示名は TempOfUs」として扱っている。ユーザーにはこの解釈を通知済みだが、名称統一の最終確認は未了である。

| 陣営 | 役職 |
|---|---|
| クルー | シェリフ、ドクター、マッドサイエンティスト、トラッカー、タイムトラベラー、シーア、バリアニック、ライトワーカー、インベスティゲーター、市長 |
| インポスター | ニンジャ、ウォーロック、マフィア、パペッティア、イレイザー、アンダーテイカー |
| 第三陣営 | ジェスター、ジャッカル、ヴァンパイア、ラバーズ |

役職ごとの能力・ペナルティについては、ユーザーの原文を反映した仕様書 `docs/ROLE_AND_UI_SPEC.md` に全件を記載済みである。

## ユーザーとのやり取りで確認済みの方針

ユーザーはAmong Usのファイル共有を提案しており、既存MODを参考にすることも許可している。こちらから、最終ビルドには対象ゲーム版の `Among Us.exe`、`GameAssembly.dll`、`Among Us_Data/Managed/`、および `BepInEx/`（導入済みなら）をZIPで共有してほしいと依頼した。**ゲームファイルはまだ受領していない。**

公開参考実装として SuperNewRoles と TheOtherRoles の構成を調査した。第三者のコードやアセットはコピー・再配布せず、設計パターンだけを参考にして独自実装する方針を明示済みである。なお、両リポジトリはGPL-3.0で公開されているため、そこから実質的な派生コードを配布する場合はGPLの義務を改めて検討する必要がある。

## 調査済みの技術基盤

Among UsのPC版はIL2CPP環境を前提とし、BepInExのIL2CPP版を使う構成を採用する。BepInEx公式文書では、ゲームのビット数に合う `Unity.IL2CPP-win-x86` または `Unity.IL2CPP-win-x64` をゲームルートへ展開し、初回起動で設定とログを生成するよう案内している。[1]

SuperNewRolesの現行公開プロジェクトは `net6.0` を対象にしており、`BepInEx/core/*.dll` と `BepInEx/interop/*.dll` を参照する構成である。この構成をtempMODのプラグイン側の目標にする。対象ゲーム版が届くまでは型名・UI階層・RPCシグネチャを確定できないため、ゲーム依存層の実装・実ビルドは保留である。[2]

## 作成済みファイル

プロジェクトルートは `/home/ubuntu/tempMOD` である。重要ファイルは次のとおり。

| パス | 状態 | 内容 |
|---|---|---|
| `docs/ROLE_AND_UI_SPEC.md` | 完成 | 20役職の仕様、初期値、勝利優先順位、同期モデル、タイトル画面ロゴ要件。 |
| `assets/tempofus_logo_source.png` | 完成 | ユーザー提供ロゴの原本（502×144 px）。 |
| `assets/tempofus_logo.png` | 完成 | 白背景を透明化した実行時用PNG。 |
| `tools_prepare_logo.py` | 完成 | ロゴの透明化を再実行するPythonスクリプト。 |
| `src/TempMod.Core/TempMod.Core.csproj` | 完成 | `net6.0` のゲーム非依存ロジックプロジェクト。 |
| `src/TempMod.Core/Domain.cs` | 完成 | 全役職ID・陣営・能力ID・設定値・状態・イベント・ロールカタログ。 |
| `src/TempMod.Core/RoleEngine.cs` | 作成済み・未コンパイル | ホスト権限の能力処理、死亡、バリア、呪い、噛みつき、後追い、投票重み、勝利判定、位置履歴、足跡など。 |
| `tempMOD_research.md` | 下書き | 技術調査とロゴ検証のメモ。 |

実装済みの `RoleEngine` は、ゲーム本体のAPIを呼ばない純粋なC#ロジックである。ゲーム連携は `IRoleGameGateway` インターフェースに抽象化しているため、次工程ではこのイベントをAmong UsのRPC・HUD・死亡・会議イベントに接続すればよい。

## 実装済みロジックの要点

`RoleEngine` には、ホストだけが能力の有効性と状態変更を確定するサーバー権威型の設計が入っている。シェリフ誤射時の自爆、バリア消費時のキル無効化、ニンジャのサイレントキルフラグ、マッドサイエンティストの一時バイタル、トラッカー、タイムトラベラーの位置履歴、シーアの死者指定、ウォーロックの近接呪いキル、マフィアのキル拒否、パペッティアの支配状態、イレイザーの役職秘匿、アンダーテイカーの死体運搬、ヴァンパイアの遅延死、ラバーズ後追い、市長の2票、ジェスターの追放勝利を含む。

コードレビュー・コンパイルは未了である。特に以下は今後検証・調整が必要である。

| 優先度 | 項目 | 内容 |
|---:|---|---|
| 高 | コアのコンパイル | .NET SDKがない環境のため、コンパイル検証が未完了。`RoleEngine.cs` のnull許容、LINQ、条件式を最初に修正する。 |
| 高 | ゲーム依存プラグイン | `TempMod.Plugin` のBepInEx `BasePlugin`、Harmonyパッチ、カスタムRPC、UIアダプタを新規実装する。 |
| 高 | バージョン固定 | ユーザーのAmong Usファイルから、`BepInEx/core` と `interop` のDLLを参照し、ゲーム版の実型・メソッドを確認する。 |
| 高 | 実機マルチプレイ | ホスト・参加者双方にMODを導入し、役職同期とゲーム終了判定をテストする。 |
| 中 | 勝利条件の最終調整 | 現在、ジャッカルとヴァンパイアは「最後の1人」で勝利。インポスターは生存インポスター数が非インポスター数以上で勝利。クルーはインポスターと敵対第三陣営が全滅して勝利。ユーザーが望む詳細ルールがあれば調整する。 |
| 中 | ロゴの見た目 | 透過PNGは生成済み。実機の黒いニュースパネル上でサイズ・位置・アンチエイリアスを確認する。 |

## 中断時の状態

`dotnet`、`msbuild`、`csc`、`mcs` は初期環境に存在しなかった。`apt-get install dotnet-sdk-6.0` はパッケージ候補がなく失敗した。その後、公式SDKの `6.0.428` Linux x64 tarballを `/tmp/dotnet-sdk-6.0.428-linux-x64.tar.gz` へダウンロードし、`/home/ubuntu/.dotnet` に展開するコマンドを起動したが、ダウンロードが**約2%・推定18分**の段階で中断された。セッションIDは `tempmod-install-dotnet-tar` である。再開する場合はセッション状態を確認し、必要なら中止して別経路（Microsoft公式APTリポジトリまたは別SDK版）を用いる。

## 次に行うべきこと

まずユーザーに対象Among UsファイルのZIPを共有してもらう。その間、C#ビルド環境を復旧し、`TempMod.Core` をコンパイルしてユニットテストを追加する。次にゲーム本体DLLを `$(AmongUs)/BepInEx/core` と `$(AmongUs)/BepInEx/interop` として参照する `TempMod.Plugin.csproj` を作り、`BasePlugin`、Harmonyパッチ、`IRoleGameGateway` 実装、カスタムRPC、タイトル画面ロゴ描画を実装する。

タイトル画面ロゴについては、SuperNewRolesの公開実装で確認できた `MainMenuManager/MainUI/AspectScaler/RightPanel/ScreenMask` 周辺を参照候補にしつつ、ユーザー版ゲームの実際のUI階層を確認して、既存ニュースパネルを複製するか独自パネルを配置する。ゲーム更新により階層名が変わる可能性があるため、複数パスを試すフォールバックが必要である。

## 参考情報

[1]: https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html "BepInEx: Installing BepInEx on Il2Cpp Unity"
[2]: https://github.com/SuperNewRoles/SuperNewRoles "SuperNewRoles/SuperNewRoles"
[3]: https://github.com/TheOtherRolesAU/TheOtherRoles "TheOtherRolesAU/TheOtherRoles"
