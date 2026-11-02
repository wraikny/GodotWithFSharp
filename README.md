# Godot with F#

F# の実装クラスから Godot の C# ノードを生成するライブラリと CLI です。

本リポジトリのライブラリ、コードジェネレータ、テスト、サンプルの実装には、
OpenAI Codex（利用モデル: GPT-6.1 Sol / `gpt-6.1-sol`）によるコード生成を利用しています。

## 最低動作環境

| 項目 | 最低対応バージョン |
| --- | --- |
| .NET | .NET 8.0（ビルド・コード生成には .NET 8.0 SDK 以上が必要） |
| Godot | Godot 4.6.2 の .NET 版（C# 対応版） |

対象フレームワークは `net8.0` です。CLI の実行には .NET 8.0 ランタイムが必要です。
Godot は動作確認済みの 4.6.2 を最低対応バージョンとし、それ未満での互換性は未検証です。
エンジン本体はコード生成・単体テストに不要で、サンプルや生成ノードの実行時に必要です。
`GodotVersion` MSBuild プロパティで GodotSharp のバージョンを指定できます。
利用先の Godot / GodotSharp / Godot.NET.Sdk のバージョンを揃えてください。

## 構成

- `src/libs/GodotWithFSharp`: 属性、`Impl`、`RuntimeObject`、ノード取得の拡張メソッド
- `src/tools/GodotWithFSharp.CodeGenerator`: リフレクションによる生成 API と CLI
- `test/GodotWithFSharp.Test`: 生成・ファイル更新・CLI・生成された C# のコンパイルテスト
- `example/`: `apps/` と `libs/` に分けた [動作サンプル](example/README.md)。`dotnet fsi build.fsx --target Example.Build` で生成・ビルドできます。

コード生成には Microsoft 提供の Roslyn (`Microsoft.CodeAnalysis.CSharp`) を使用します。
`SyntaxFactory` で型・クラス・プロパティ・メソッド・式の構文ノードを組み立て、
`CompilationUnitSyntax` を作ってから `NormalizeWhitespace` / `ToFullString` で C# に変換します。
`Generator.generateSyntax` から文字列化前の構文木を取得できます。
Roslyn は .NET 標準ライブラリ同梱ではなく、ジェネレータの NuGet 依存です。
ランタイム用の `GodotWithFSharp` ライブラリや利用先のアプリには Roslyn の参照を追加する必要はありません。

## submodule から利用する

利用先のルートにある `Directory.Build.props` で、GodotSharp のバージョンをまとめて指定できます。

```xml
<Project>
  <PropertyGroup>
    <GodotVersion>4.6.2</GodotVersion>
  </PropertyGroup>
</Project>
```

このリポジトリの `Directory.Build.props` は、さらに上位にある最寄りの同名ファイルを明示的に読み込みます。
そのため submodule 内のライブラリ・ジェネレータにも利用先の指定が適用され、
CLI を別プロセスで起動するときもバージョンの引数を繰り返す必要がありません。
上位ファイルがない場合や `GodotVersion` が未指定の場合は `4.6.2` を使用します。
`-p:GodotVersion=...` による上書きも可能です。
上位ファイルの読み込みは通常の MSBuild Import のため、他のプロパティや ItemGroup も読み込みます。
このリポジトリ側では対象フレームワークを `net8.0`、XML ドキュメント生成を有効に設定します。
Godot エンジンと利用先の `Godot.NET.Sdk` のバージョンも指定に揃えてください。

利用先の F# プロジェクトにライブラリの参照を追加します。

```xml
<ProjectReference Include="../../libs/GodotWithFSharp/src/libs/GodotWithFSharp/GodotWithFSharp.fsproj" />
```

```fsharp
namespace Example.Implementation

open Godot
open GodotWithFSharp

[<GenerateGodotNode("PlayerNode", typeof<Node2D>)>]
type PlayerImpl(node: Node2D) =
  interface Impl<Node2D>

  [<Export(PropertyHint.Range, "0,100,0.5")>]
  [<System.ComponentModel.DefaultValue(10.0)>]
  member val Speed = 10.0 with get, set

  [<GenerateGodotOverride("_Process")>]
  member this.Update(delta: double) =
    node.Position <- node.Position + Vector2(float32 (this.Speed * delta), 0.0f)
```

実装アセンブリをビルドした後、**利用先のルートをカレントディレクトリとして**呼び出します。
生成先の C# プロジェクトからも実装プロジェクトを参照してください。

```shell
dotnet build src/Example.Implementation/Example.Implementation.fsproj
dotnet run --project libs/GodotWithFSharp/src/tools/GodotWithFSharp.CodeGenerator -- --assembly src/Example.Implementation/bin/Debug/net8.0/Example.Implementation.dll --namespace Example.Generated --output apps/Godot/scripts/generated
```

Export の既定値は `System.ComponentModel.DefaultValue` で明示してください。宣言値は生成ノードの既定値と実装生成時の初期値の両方に使われます。
複合型は `[<GodotExportDefaultProvider(typeof<Defaults>, "Offset")>]` で公開 static プロパティまたは引数なし static メソッドを指定できます。
提供メンバーはプロパティと同じ型を返し、副作用を持たず、可変な値は呼び出しごとに作成してください。ジェネレータは提供メンバーを実行しません。
既定値の省略・型の不一致・二重指定は生成時のエラーです。

生成コードは `Node2D` を継承した `public partial class PlayerNode` となります。
コンストラクタで `new PlayerImpl(this)` を呼び、`Impl` プロパティを公開し、
Export プロパティと GenerateGodotOverride メソッドを実装クラスに転送します。
実装クラスは public な非抽象・非ジェネリック型で、生成ノードを受け取る public コンストラクタと
`Impl<指定したNode型>` の実装が必要です。Export は public なインスタンス get/set プロパティ、
GenerateGodotOverride は対応する Godot の public virtual メソッドを対象とします。
`[<GenerateGodotOverride>]` は実装メソッドと同名のメソッドを override し、
`[<GenerateGodotOverride("_Process")>]` のように指定すると、実装側は `Update` など別の名前にできます。
引数と戻り値の型は override 先と一致する必要があります。同じメソッドへの重複指定はエラーです。
ref/out およびジェネリックメソッドは対象外です。識別子は ASCII 英数字と `_` を使用してください。

`--assembly` は複数回指定できます。namespace と出力先は明示指定です。
DLL の依存関係は `.deps.json` と DLL と同じディレクトリから解決します。
ビルド時の依存 DLL を含む出力ディレクトリを使用してください。
生成処理は Godot ノードや実装クラスをインスタンス化しません。

出力先には専用ディレクトリを使ってください。生成マーカーがある古い `.cs` のみ削除し、
同名の手書きファイルがあればエラーにします。内容が変わらないファイルは再書き込みしません。
1つの出力先にまとめる実装 DLL は1回のコマンドですべて渡してください。

FAKE のターゲットでも同じ CLI を呼び出せます。パスは呼び出し元を基準に解釈します。

```shell
dotnet fsi libs/GodotWithFSharp/build.fsx --target Gen -- --assembly src/Example.Implementation/bin/Debug/net8.0/Example.Implementation.dll --namespace Example.Generated --output apps/Godot/scripts/generated
```

推奨は CLI を直接呼び出し、利用先の build.fsx に「実装ビルド → CLI 呼び出し」の依存関係を置く方法です。
これなら利用先が FAKE 以外でも同じ入口を使用できます。
CLI は `PackAsTool` に対応しているため、将来は `dotnet pack` したパッケージを
ローカルツールとして登録し、`dotnet godot-fsharp-gen ...` で実行する運用にも移行できます。
パッケージの公開・ツールのインストールはこのリポジトリでは行いません。
MSBuild の自動実行は実装 DLL が必要になるため循環参照を避ける必要があり、明示的な生成ステップを推奨します。

## ランタイムヘルパー

`open GodotWithFSharp` で以下を利用できます。

- `node.GetUnsafeImpl<Implementation>()`: ノードの実装を取得
- `node.GetNodeImpl<Implementation>(path)`: 子ノードの実装を取得
- `node.GetRuntimeNode<NodeType, Implementation>(path)`: 子ノードを取得
- `node.GetRuntimeImpl<NodeType, Implementation>(path)`: 子ノードの実装を取得
- `scene.InstantiateWithImpl<NodeType, Implementation>()`: ノードと実装のペアを生成
- `GD.LoadNodeWithImpl<NodeType, Implementation>(path)`: PackedScene をロードしてペアを生成

## ビルドとテスト

```shell
dotnet build GodotWithFSharp.sln
dotnet test GodotWithFSharp.sln
dotnet fsi build.fsx --target Build -- Release
dotnet fsi build.fsx --target Test -- Release
dotnet tool restore
dotnet fsi build.fsx --target Format.Check
```

テストは GodotSharp の実 API を使い、生成された C# を一時ディレクトリでビルドします。
Godot エディタ内のシーン実行はこのテストの対象外です。
