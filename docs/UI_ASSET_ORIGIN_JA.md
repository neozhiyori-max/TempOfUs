# tempMOD UI資産台帳

## 方針

tempMODの配布DLLには、SuperNewRolesの設定画面用スプライトバンドル、役職アイコン、設定プリファブ由来の画像を含めません。設定画面はAmong Us実行中の標準設定行を動的に複製するだけであり、ゲーム外へInnerslothの画像資産を再配布しません。

## 除去したSNR由来の画像資産

| 対象 | 状態 | 理由 |
|---|---|---|
| `Resources/snrsprites.bundle` | 削除済み | SNR設定画面・役職アイコンを含む画像バンドルのため。 |
| `Resources/snrsprites_android.bundle` | 削除済み | Android向けの同等画像バンドルのため。 |
| `AssetManager.Load()` を使うSNR設定UI | 起動処理から除外済み | SNRの画像プリファブ・役職画像を表示しないため。 |
| `CustomOptionsMenu` / `RoleOptionMenu` のSNR UIパッチ | 起動処理から除外済み | SNRの画面構成・アイコンを使用しないため。 |

## tempMOD独自アイコン

`Resources/tempmod_ui/` の5つのPNGは、tempMOD用に新規生成した抽象的なUIアイコンです。既存MOD、Among Usのクルーメイト形状、既存ロゴ、既存キャラクター、文章を含めないように指定しています。

| ファイル | 用途 | 意匠 |
|---|---|---|
| `icon_impostor.png` | インポスター分類 | 赤い幾何学ターゲットと稲妻。 |
| `icon_crew.png` | クルー分類 | 青い幾何学シールド。 |
| `icon_neutral.png` | 第三陣営分類 | 水色の軌道リングとダイヤ形状。 |
| `icon_settings.png` | 設定分類 | 金色の調整スライダー。 |
| `icon_controls.png` | 制御分類 | 紫色の切替スイッチ。 |

> 現在の第1波設定画面は、可読性と安定性を優先して文字と標準設定行だけで構成します。独自アイコンは、後続の分類タブやメニューを追加する時だけ使用します。

## SNRコードとの区別

役職モデル・設定モデル・同期モデルはGPL-3.0のSNR由来コードを段階的に採用します。しかし、UI画像・アイコン・設定画面プリファブはSNRから採用しません。コード由来と画像由来を明確に分離し、`NOTICE.md` とこの台帳で追跡します。
