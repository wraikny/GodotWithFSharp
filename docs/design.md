# 設計と実装状況

F# の実装から薄い C# ノードを生成し、Godot.NET.Sdk の通常のコンパイル・コード生成に渡す。
独自の初期化ライフサイクルは追加しない。元プロジェクトは参照専用とする。

## 承認された方針

- [x] 解析・検証・Roslyn AST 生成を分離し、バッチ全体を出力前に検証する。
- [x] 入力 DLL を MetadataLoadContext で解析し、入力コードや属性コンストラクタを実行しない。
- [x] Export の既定値を明示し、エディターと実行時を一致させる。
- [x] 生成・Ready・ツリーへの出入り・最終破棄を区別する。
- [x] ノード型と実装型の対応を検証し、Try 取得 API を用意する。
- [ ] manifest、内容のハッシュ、更新計画、中断からの復旧で出力を管理する。
- [ ] submodule の共有設定とツール・テストの内部設定を分離する。
- [ ] 通常の Godot ビルドで F# ビルド後・C# コンパイル前に生成する。
- [ ] CLI の別プロセス実行、Godot ビルド・ヘッドレス検証を自動化する。
- [ ] SDK と依存を固定し、Windows / Linux、submodule / ツール配布を CI で検証する。

## Export

既定値には専用の `GodotExportDefault` を使用する。
標準の `System.ComponentModel.DefaultValue` は初期値適用の契約として使わない。
複合型は `GodotExportDefaultProvider` で公開 static プロパティまたは引数なし static メソッドを指定する。
提供メンバーは型が一致し、副作用がなく、可変な値を共有しないものとする。

順序は実装コンストラクタ、宣言した既定値の適用、シーンに保存された値の適用、EnterTree、Ready とする。
コンストラクタ内では属性値やシーン値の反映を前提にしない。動作中の通常の代入は既定値で補正しない。
生成プロパティは実装側の更新を getter で取得し、setter は実装側に転送する。

## ライフサイクルと所有権

実装オブジェクトはノードのコンストラクタで一度だけ生成する。
シーン依存の初期化は `GenerateGodotOverride("_Ready")` で記述する。
EnterTree / ExitTree / Ready は Godot の呼び出しどおり転送し、再入場で実装を作り直さない。
`RequestReady()` の挙動も変更しない。`GenerateGodotInitialize` は追加しない。

`Tool = true` は生成ノードを Godot の Tool スクリプトにする。
`IDisposable` 実装の最終破棄はノードが所有し、`DisposeImplementation = false` で無効化できる。
ツリー退出では破棄しない。利用者の破棄通知 callback と後始末は合成し、後始末は finally で実行する。

## コミットと検証

全体方針の承認後、段階ごとに実装・検証・コミットする。
各段階に回帰テストを含め、最後のテスト拡充段階まで検証を先送りしない。
コミットには実際の Codex モデル名を含む Co-authored-by を付ける。
