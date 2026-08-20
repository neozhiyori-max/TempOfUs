# NOS（Nebula on the Ship）主基盤採用監査

> **監査日:** 2026-08-20  
> **目的:** tempMODをNOS基盤へ切り替える前に、ソース可用性、ライセンス、ゲーム版互換性、持込禁止機能を確認する。

## 1. 参照対象

| 対象 | 固定参照 | 内容 | 結論 |
| --- | --- | --- | --- |
| 公式Nebula配布リポジトリ | `Dolly1016/Nebula`、`ceb72fe` | 言語データ、案内、更新情報、組込みアドオンのみ。プラグイン本体のC#ソースは未収録。 | **配布・導入参照には使えるが、tempMODの再実装基盤には使えない。** |
| 公開Remakeソース | `lnll2021/Nebula-on-the-ship-Remake`、`f121b51` | Nebula、NebulaAPI、NebulaPluginNova等のC#ソースを含む。 | **技術参照・移植元候補として利用可能。ただし現行ゲーム版への適合が必須。** |

## 2. ライセンスと配布上の扱い

公開RemakeリポジトリにはGPL-3.0本文が含まれる。tempMODがそのコードを改変・配布する場合は、GPL-3.0の条件に従い、派生元、変更内容、対応するソースを明示する。

公式Nebula配布リポジトリは、作品・画像などに複数の由来があることを案内している。公式配布物の画像や素材をtempMODへ安易に混在させず、採用する資産単位で出所と許諾条件を確認する。

## 3. 互換性監査

| 項目 | 公開Remakeソースの状態 | tempMODの対象環境 | 判定 |
| --- | --- | --- | --- |
| ターゲット | .NET 6 | .NET 6 | 基本整合 |
| フレームワーク | BepInEx IL2CPP | BepInEx 6 IL2CPP | 移植可能性あり |
| Among Us版指定 | `2023.3.28` | Steam build 24302054 / Unity 2022.3.44f1 | **API再適合が必要** |
| 役職・UI規模 | Nebula本体260、NebulaAPI123、Nova326 C#ファイル | 最初は5役職だけ | 段階的な限定ロードが必要 |

## 4. 持込禁止・無効化対象

公開Remakeのプラグイン初期化には、tempMODの安定性方針と合わない機能が含まれる。NOS基盤を採用する場合、以下は最小起動版から外す。

| 機能 | 根拠 | tempMOD方針 |
| --- | --- | --- |
| CPUAffinityEditorの実行ファイル生成 | `Nebula.cs` の `InstallTools` | **無効化** |
| 独自サーバー・リージョン情報初期化 | `RegionMenuOpenPatch.Initialize` | **無効化** |
| Harmony全パッチ | `Harmony.PatchAll()` | **使用しない。必要なパッチを個別有効化。** |
| 全RPCロード | `RemoteProcessBase.Load()` | **使用しない。5役職の同期経路だけを段階導入。** |
| バン状態の強制解除 | `AmBannedPatch` | **絶対に導入しない。** |
| テクスチャ・コスメ・マップの一括ロード | `AssetLoader`、`TexturePack`、`MapEditor`等 | 初期基盤では**無効化** |

## 5. 結論

NOSをそのまま差し替えて動かすことはしない。公開RemakeソースをGPL-3.0準拠の参照基盤として扱い、現行Among Us版に適合した**最小ローダー**を先に作る。最小ローダーは、外部通信、サーバー、CPU設定、全パッチ、バン状態変更、全RPC、コスメ、マップ編集を含まない。

その最小ローダーの起動検証後に、NOS型の設定UIを最初の5役職に限定して段階有効化する。

## 参考

1. [公式Nebula配布リポジトリ](https://github.com/Dolly1016/Nebula)
2. [Nebula on the Ship Remake公開ソース](https://github.com/lnll2021/Nebula-on-the-ship-Remake)
3. [GNU General Public License v3.0](https://www.gnu.org/licenses/gpl-3.0.html)
