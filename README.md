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

利用先のルートに共有用の `GodotWithFSharp.props` を置き、GodotSharp のバージョンをまとめて指定します。

```xml
<Project>
  <PropertyGroup>
    <GodotVersion>4.6.2</GodotVersion>
  </PropertyGroup>
</Project>
```

利用先の `Directory.Build.props` からも、この共有ファイルを Import してください。

```xml
<Project>
  <Import Project="$(MSBuildThisFileDirectory)GodotWithFSharp.props" />
</Project>
```

submodule は上位にある最寄りの `GodotWithFSharp.props` を読みます。任意の場所を使う場合は `GodotWithFSharpConfiguration` にファイルパスを指定できます。
共有ファイルには Godot 関連の共有値だけを置いてください。上位の `Directory.Build.props` 全体は既定では取り込みません。
共有値がない場合の GodotVersion は `4.6.2` です。`-p:GodotVersion=...` による上書きも可能です。
ライブラリ・ツール・テストの対象フレームワークは各プロジェクトで `net8.0` に設定します。

従来の上位 props 継承から移行する間は `-p:GodotWithFSharpImportParentProps=true` で旧方式を明示的に有効化できます。
この互換モードは上位ファイルの PackageReference や ItemGroup も取り込むため、共有ファイルへの移行を推奨します。
Godot エンジンと利用先の `Godot.NET.Sdk` も同じバージョンに揃えてください。リポジトリ内の Godot プロジェクトでは SDK の参照バージョンとの不一致を GFS200 / GFS201 で検出します。

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
  [<GodotExportDefault(10.0)>]
  member val Speed = 10.0 with get, set

  [<GenerateGodotOverride("_Process")>]
  member this.Update(delta: double) =
    node.Position <- node.Position + Vector2(float32 (this.Speed * delta), 0.0f)
```

Godot の C# プロジェクトから実装を参照し、生成用 targets を Import します。パスは配置に合わせて調整してください。

```xml
<Project Sdk="Godot.NET.Sdk/4.6.2">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <GodotWithFSharpNamespace>Example.Generated</GodotWithFSharpNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Example.Implementation/Example.Implementation.fsproj"
                      GodotWithFSharpImplementation="true" />
  </ItemGroup>
  <Import Project="../../libs/GodotWithFSharp/build/GodotWithFSharp.targets" />
</Project>
```

`dotnet build` と Godot エディターのビルドから、実装 DLL のビルド、C# の生成、Godot SDK のコンパイルを順に実行します。
実装側から生成先の C# プロジェクトを参照しないでください。生成先の既定値は Godot プロジェクト内の `scripts/generated` です。
`GodotWithFSharpOutput` で変更でき、相対パスは C# プロジェクト基準です。
生成ファイルは初回ビルド中にコンパイル対象へ追加します。DesignTimeBuild では生成しません。
毎回所有権と内容を確認し、変更がない生成ファイルは再書き込みしません。

CLI を単独で使う場合は、**利用先のルートをカレントディレクトリとして**実装ビルド後に呼び出します。

```shell
dotnet build src/Example.Implementation/Example.Implementation.fsproj
dotnet run --project libs/GodotWithFSharp/src/tools/GodotWithFSharp.CodeGenerator -- --assembly src/Example.Implementation/bin/Debug/net8.0/Example.Implementation.dll --namespace Example.Generated --output apps/Godot/scripts/generated
```

Export の既定値は `GodotExportDefault` で明示してください。宣言値は生成ノードの既定値と実装生成時の初期値の両方に使われます。
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

出力先には専用ディレクトリを使ってください。`.godotwithfsharp.manifest.json` に所有者・入力 DLL 名・生成ファイルのハッシュを記録します。
1つの出力先にまとめる実装 DLL は1回のコマンドですべて渡してください。所有者の既定値は生成先の namespace で、`--owner` で明示できます。
別の所有者、手書きファイルとの衝突、生成ファイルの手動変更はエラーにします。内容が変わらないファイルは再書き込みしません。

- `--dry-run`: 更新・削除予定を検証して表示し、書き込みません。
- `--adopt-existing`: 旧方式の生成マーカー付きファイルを初回だけ manifest に取り込みます。
- `--allow-empty`: 生成対象がなくなったことを確認したうえで、所有する `.cs` をすべて削除できます。既定では空生成を拒否します。
- `--allow-input-change`: 入力 DLL の集合が変わることを明示的に許可します。

更新は一時ファイル経由で行い、中断時の状態を記録します。次回生成時に更新前の内容へ復旧してから再実行します。
中断後にさらにファイルが変更されていた場合は上書きせず、復旧記録を保持してエラーにします。
Godot が管理する `.uid` はこの生成処理では削除しません。

FAKE のターゲットでも同じ CLI を呼び出せます。パスは呼び出し元を基準に解釈します。

```shell
dotnet fsi libs/GodotWithFSharp/build.fsx --target Gen -- --assembly src/Example.Implementation/bin/Debug/net8.0/Example.Implementation.dll --namespace Example.Generated --output apps/Godot/scripts/generated
```

submodule では targets の Import を推奨します。MSBuild 以外の仕組みでは CLI を直接呼び出せます。
`GodotWithFSharpOwner` の既定値は namespace です。CLI の明示的な移行操作に対応する
`GodotWithFSharpAllowEmpty`、`GodotWithFSharpAllowInputChange`、`GodotWithFSharpAdoptExisting` は既定ではすべて false です。
必要な移行時だけ true にしてください。
CLI は `PackAsTool` に対応しています。外部へのパッケージ公開は行いません。

## ランタイムヘルパー

実装オブジェクトはノードのコンストラクタで一度だけ生成します。コンストラクタはノード参照の保持と軽量な状態準備に限定し、
シーンから設定される Export 値や子ノードに依存する処理は `GenerateGodotOverride("_Ready")` で実装してください。
EnterTree / ExitTree / Ready は Godot の呼び出しどおりに転送します。再入場で実装を作り直したり、独自の初期化フックを挿入したりしません。

`GenerateGodotNode(..., Tool = true)` は生成ノードに Godot の Tool 属性を付けます。既定では付けません。
実装が `IDisposable` を実装している場合、既定ではノードの最終破棄通知で一度だけ Dispose します。
`DisposeImplementation = false` で自動破棄を無効化できます。ツリー退出では Dispose しません。
利用者の `_Notification` 転送と後始末は合成され、後始末は `finally` で実行します。実装側の Dispose から所有元のノードを再び破棄しないでください。

`open GodotWithFSharp` で以下を利用できます。

- `node.GetUnsafeImpl<Implementation>()`: ノードの実装を取得
- `node.TryGetImpl<Implementation>()`: 生存中の対応する実装があれば `Some`、なければ `None`
- `node.GetNodeImpl<Implementation>(path)`: 子ノードの実装を取得
- `node.TryGetNodeImpl<Implementation>(path)`: 子ノードの実装を安全に取得
- `node.GetRuntimeNode<NodeType, Implementation>(path)`: 子ノードを取得
- `node.GetRuntimeImpl<NodeType, Implementation>(path)`: 子ノードの実装を取得
- `node.TryGetRuntimeNode<NodeType, Implementation>(path)` / `TryGetRuntimeImpl`: ノード型と実装の対応を検証して取得
- `scene.InstantiateWithImpl<NodeType, Implementation>()`: ノードと実装のペアを生成
- `GD.LoadNodeWithImpl<NodeType, Implementation>(path)`: PackedScene をロードしてペアを生成

`GetRuntimeNode` は通常のノード取得に加えて `RuntimeObject<NodeType, Implementation>` の実装も確認します。
`InstantiateWithImpl` の実装型は `Impl<NodeType>` に制約されます。シーンのルートが契約に合わない場合は生成したノードを解放してから例外にします。

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
