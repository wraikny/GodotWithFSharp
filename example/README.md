# 動作サンプル

F# が四角形を移動させ、画面端で反射させるサンプルです。
画面上部に速度と座標を表示し、Reset position ボタンで初期位置に戻します。
画像などの外部アセットは不要です。

```text
example/
  apps/Godot/                      Godot の実行プロジェクトとシーン
    scripts/generated/             CLI が生成する C# ノード
  libs/Example.Implementation/      ノードの動作を実装する F# ライブラリ
```

.NET 8 SDK 以上と Godot 4.6.2 の **.NET 版**を使用してください。
リポジトリのルートで次を実行します。

```shell
dotnet fsi build.fsx --target Example.Build
```

F# 実装のビルド → C# ノード生成 → Godot アプリのビルドを順番に実行します。
その後、Godot で `example/apps/Godot/project.godot` を開き、F5 で実行してください。
CLI からは次のように起動できます（`godot` は .NET 版の実行ファイルに読み替えてください）。

```shell
godot --path example/apps/Godot --editor
godot --path example/apps/Godot
```

`BouncerImpl.fs` の `[<Export>]` が Godot のインスペクターに転送されます。
`GodotExportDefault(180.0f)` で既定値を明示し、エディターと実行時の初期値を一致させています。
`Main.tscn` の Bouncer ノードでは Speed を既定の 180 から 240 に変更しています。
`[<GenerateGodotOverride>]` により `_Ready` と `_Process` を F# に転送します。
`BouncerImpl.Update` では `[<GenerateGodotOverride("_Process")>]` で実装と override 先の名前を分けています。
`MainImpl.fs` は `GetNodeImpl` / `GetRuntimeNode` で子ノードとその実装を取得します。
F# の公開プロパティや属性を変更したら `Example.Build` を再実行してください。
生成 C# は直接編集せず、F# 側を編集します。

生成までを実行する場合は `dotnet fsi build.fsx --target Example.Gen` を使えます。
利用先でも「実装ビルド → CLI → アプリビルド」の順序を設けることで、
このリポジトリを submodule として参照する構成に置き換えられます。

ヘッドレスでの動作確認もできます。

```shell
godot --headless --path example/apps/Godot --editor --import
godot --headless --path example/apps/Godot --quit-after 120 -- --smoke-test
```

正常なら 30 フレーム後に `EXAMPLE_SMOKE_TEST_OK` を表示し、終了コード 0 で終了します。
シーンの Export 値、移動処理、実装取得、ボタンのシグナルによるリセットを確認します。
`--quit-after` は処理が呼び出されない場合の上限です。確認時は終了コードだけでなく成功メッセージも確認してください。
