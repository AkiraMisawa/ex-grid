# 1 ページに複数のグリッドが並んでも、互いに干渉しない

同じページに複数の ExGrid を配置するのは普通の使い方である。**グローバルに触る箇所を
すべて器（インスタンスのルート要素）にスコープする。**

## 大半は押す形のおかげで自動的に独立している

グリッドはほとんど状態を持たない
（[ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) /
[ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)）。Window もソートも
フィルタも Consumer 側にあり、グリッドが持つのは Selection・Focus・スクロール位置・
編集中のテキストだけで、**いずれもインスタンスごとの一時状態**。

危険は**グローバルに触る 4 箇所**に集中する。

## 1. キー入力の捕捉を器にスコープする（最重要）

[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) で「核が捕捉フェーズでキーを
先に見る」と決めた。**これを `document` に張ると、ページ上の全グリッドが全打鍵を受け取る。**

```
❌ document.addEventListener('keydown', handler, true)
     → グリッド A で Ctrl+C したら、グリッド B もコピーしようとする

✅ gridRootElement.addEventListener('keydown', handler, true)
     → その器の中にフォーカスがあるときだけ発火
```

**器を `tabindex` でフォーカス可能にし、リスナはその器に張る。** どのグリッドがアクティブか
は、ブラウザのフォーカスがそのまま答えになる。

Ctrl+C（[ADR-0005](./0005-copy-refuses-rather-than-truncates.md)）、
Ctrl+A（[ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)）、
Enter の巡回（[ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)）が**すべてこれに
乗っている**ので、ここを誤ると全部が壊れる。

## 2. CSS のクラス名に接頭辞を付ける

`spikes/render-bench` の CSS は `.r`（行）`.c`（セル）`.sel`（選択）`.window` `.scroller` と
いう名前を使っている。**捨てる前提のスパイクだから許されるだけで、製品では論外**であり、
ホストアプリの CSS と衝突する。`.ex-row` `.ex-cell` のように接頭辞を付ける。

CSS 変数（`--ex-*`）は**器の要素に定義する**。`:root` に置くと 1 ページ 1 テーマになり、
インスタンスごとに違う見た目にできない。

## 3. JS はモジュール化し、インスタンスごとのハンドルを返す

スパイクの `window.bench = { _fps: null, ... }` は単一のグローバル状態で、2 つ目のグリッドが
呼ぶと 1 つ目を壊す。**モジュールにし、器ごとのハンドルを返す**形にする。

## 4. ポップオーバーは器の外へ出さない

フィルタパネルと列メニューはグリッドの上に浮くが、器は `overflow: auto` のスクロール器なので
中に置くと切り取られる。**Popover API と CSS Anchor Positioning を使う**
（[ADR-0017](./0017-target-chromium-browsers-only.md) で Chrome 系に限定したので使える）。
切り取りも重なり順もブラウザが面倒を見るため、`document.body` へポータルする必要がなく、
**複数インスタンスで座標と z-index が絡まない**。

## Consequences

- **選択オーバーレイと編集欄は、器と同じ座標空間に置く**
  （[ADR-0008](./0008-selection-is-painted-by-an-overlay.md) /
  [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)）。ページ座標で計算すると
  インスタンスごとにずれる。
- **選択数とフォーカス中セルの値の表示は、どのグリッドのものかが分かる必要がある**
  （[ADR-0014](./0014-paste-shape-rules-and-selection-count.md) /
  [ADR-0016](./0016-column-width-and-overflow.md)）。グリッドの中に置くなら自明だが、
  Consumer がアプリ共通のステータスバーに出すなら、**どのグリッドが対象かを Consumer が
  解決する**。核は値を出すだけで、置き場所は決めない。
- **Chrome の DI 登録（`AddExGridMudBlazor()`）は全インスタンス共通**だが、個別のグリッドで
  上書きできる。登録されたものは読むだけで、インスタンスが書き換えない。
- **Fluxor などで複数グリッドを使うなら、Consumer が Feature を分ける必要がある。** 2 つの
  グリッドが同じ `State.Value.Window` を読めば同じものが出る。**押す形にしたことで、これは
  Consumer 側の問題として素直に現れる** — グリッドが状態を隠し持っていて漏れる、という形に
  ならない。
- **フォーカスされていないグリッドは、キー操作に反応しない。** Selection の見た目は残るが
  操作されない。複数グリッドで作業するとき、どちらが効いているかが分かる必要があるので、
  **フォーカスの有無を視覚的に区別する**。
