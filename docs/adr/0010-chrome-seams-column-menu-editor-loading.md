# 残りの Chrome — 列メニュー・セル編集欄・読み込み表示

[ADR-0009](./0009-filter-panel-contract.md) でフィルタパネルの契約を決めたのに続き、
差し替え可能な UI（`IGridChrome`）の残り 3 つを定める。規則は共通 —
**Chrome は描画とコールバックだけを行い、意味は決めない。**

## 列メニュー — 項目を決めるのは核

```csharp
public sealed record ColumnMenuContext(
    ColumnInfo Column,
    IReadOnlyList<GridCommand> Commands,   // 核が決める。Chrome は並べるだけ
    Action Close);

public sealed record GridCommand(string Id, string Label, bool Enabled, Action Invoke);
```

**メニューに何を並べるかを Chrome に決めさせない。** 決めさせると既定版と MudBlazor 版で
項目が食い違い、「Chrome を差し替えても振る舞いは変わらない」という前提が崩れる。

核が出す標準コマンド: 昇順/降順で並べ替え、フィルタを開く、この列を隠す、列を固定、
幅を自動調整。**すべて View State を動かすので、`Invoke` の実体は Consumer への通知**になる
（[ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)）。

**Consumer は独自コマンドを足せる。** `poke` なら「このブックのレポートを開く」のような
ものが出る。足すのは Consumer であって Chrome ではない — Chrome は依然として並べるだけ。

## 読み込み表示 — 受け取って描くだけ

```csharp
public sealed record LoadingContext(bool IsLoading, int PlaceholderRowCount);
```

**Placeholder は 1 つの機構**（取得待ちと高速スクロール中の間引きが同じもの、
[ADR-0004](./0004-cap-the-cells-touched-per-frame.md)）なので、Chrome から見て区別する必要も
ない。

## セル編集欄 — 行の中に置かない。浮かせて 1 個

行の中に `<input>` を入れてはいけない。行のメモ化が壊れ
（[ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)）、DOM も重くなる。
**フォーカスされたセルの上に浮かせる 1 個**にする — 選択のオーバーレイ
（[ADR-0008](./0008-selection-is-painted-by-an-overlay.md)）と同じ考え方で、座標計算も共有
できる。

```csharp
public sealed record CellEditorContext(
    ColumnInfo Column,
    object? InitialValue,      // Caret なら現在値、Overwrite なら打鍵した文字
    CellEditMode Mode,         // Overwrite / Caret
    Action<object?> Commit,
    Action Cancel);
```

### 編集の状態を 2 つ持つ

Excel には編集の状態が 2 つあり、**同じ矢印キーが状態によって違う意味になる**。

| 状態 | 入り方 | 元の値 | 矢印キー |
|---|---|---|---|
| **Overwrite**（入力） | セルを選んでいきなり文字を打つ | 消えて置き換わる | **確定して隣のセルへ移動** |
| **Caret**（編集） | F2、またはダブルクリック | 残り、中にカーソルが入る | **文字列の中でカーソルを動かす** |

F2 は両者を行き来する。

多くのグリッド製品はこれを実装せず、常に Caret 相当にしている。すると「値を打って矢印で
次のセルへ」という連続入力が成立せず、Excel から来たユーザは必ず違和感を持つ。この部品は
**「Excel のような操作性」を看板にしており、その看板の中身がまさにこれ**なので実装する。

却下した案: **Caret だけにする** — 実装は単純で矢印は常に編集欄のものになるが、
連続入力が効かない。看板を一段下げることになる。

### キーの調停は核が握る。Chrome には委ねない

編集欄が `MudTextField` のようなコンポーネントだと、それ自身がキーを処理する。放置すると
モードによる矢印の意味の切り替えが成立しない。

**核が捕捉フェーズ（capture phase）でキー入力を先に見る。** グリッドの器に capture の
keydown リスナを張り、核が処理すべきキーはそこで奪って `preventDefault` する。編集欄まで
届くのは、核が渡すと決めたキーだけ。

| キー | 誰が処理するか |
|---|---|
| Esc / Enter / Tab | **常に核**（取り消し・確定・移動） |
| 矢印 / Home / End | **Overwrite なら核**（確定して移動）、**Caret なら編集欄**（カーソル移動） |
| その他 | 編集欄 |

バブリングでは間に合わない — 編集欄が先に処理してカーソルを動かしたあとでは、取り消せない。
**捕捉フェーズであることが要点。**

この方式なら **Chrome の協力を必要としない。** 「編集欄が keydown を核に転送する」という
契約にすると、その転送を実装するかどうかで Chrome ごとに挙動が変わり、上の規則が崩れる。

## Consequences

- **核が小さな JS を持つ。** capture フェーズのリスナは JS interop でしか張れない。
  スクロール位置の読み取りやクリップボードでもどのみち必要になるので、新たな依存では
  ない。
- **編集欄はセルの外にあるので、セルの見た目と完全には一致しない。** 浮かせた要素の
  書体・行高・余白を、セルの CSS 変数から引いて合わせる必要がある。ずれると編集の
  瞬間に文字が跳ねて見える。
- **編集欄の位置計算は、選択オーバーレイと同じ座標系を使う。** 行高が C# パラメータを
  通らなければならない理由がここにもある（CSS だけで行高を変えると編集欄がずれる）。
- **`GridCommand.Id` は安定した識別子にする。** Chrome がコマンドごとにアイコンを
  割り当てたり、Consumer が特定コマンドを差し替えたりするための足がかりになる。
- **Overwrite モードの「打鍵した文字」を取りこぼさない。** 最初の 1 文字は編集欄が存在
  する前に押されている。核がそれを保持して `InitialValue` として渡す。
