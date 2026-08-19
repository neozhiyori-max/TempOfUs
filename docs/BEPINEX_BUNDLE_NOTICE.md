# BepInEx同梱配布メモ

## 対象

tempMODの対象は32ビット（x86）のWindows IL2CPP版 Among Usであるため、BepInExは **Unity.IL2CPP-win-x86** 系統を同梱対象とする。

## 配布構成

フル版ZIPには、BepInExの起動に必要なルートブートストラップ、`BepInEx/core`、`BepInEx/patchers`、tempMODプラグインを含める。ゲーム本体、個人設定、ログ、ゲームごとに初回生成されるインターオプ解析物は含めない。利用者はZIPをAmong Usのゲームフォルダに展開し、初回起動時にBepInExが必要ファイルを生成する。

## ライセンス通知

BepInExの公式リポジトリはLGPL-2.1を掲げている。フル版ZIPにはBepInExのライセンス文書と公式配布元へのリンクを添付し、tempMOD自体のGPL-3.0ライセンス・NOTICEと区別して明記する。

## 参照

- BepInEx公式IL2CPP導入手順: https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html
- BepInEx公式ライセンス: https://github.com/BepInEx/BepInEx/blob/master/LICENSE
- BepInEx公式ビルド配布: https://builds.bepinex.dev/projects/bepinex_be
