# ExGrid と ExSheet は 1 つのリポジトリに置き、パッケージだけ分ける

`ExGrid`（表示主体）と `ExSheet`（編集主体）、および連携パッケージは**同じリポジトリ**で
開発する。**NuGet パッケージは分ける** — リポジトリの構造とパッケージの構造は別の話である。

```
ex-grid/                      ← リポジトリ 1 つ
├── CONTEXT.md                ← 用語集も 1 つ
├── AGENTS.md
├── docs/adr/                 ← ADR も 1 つの連番
├── src/
│   ├── ExGrid/               → NuGet: ExGrid            （依存なし）
│   ├── ExGrid.MudBlazor/     → NuGet: ExGrid.MudBlazor  （Chrome の実装）
│   ├── ExGrid.Fluxor/        → NuGet: ExGrid.Fluxor     （押す形と Store の橋渡し）
│   └── ExSheet/              → NuGet: ExSheet           （将来）
├── tests/
│   ├── ExGrid.Tests/         ← 純粋ロジック（xUnit）
│   ├── ExGrid.Components/    ← コンポーネント（bUnit）
│   └── ExGrid.Browser/       ← ブラウザ（CDP ドライバ）
└── spikes/render-bench/      ← 描画性能の計測。捨ててよい
```

グリッドだけ欲しい Consumer は `ExGrid` だけを参照する。**そのためにリポジトリを分ける必要は
ない。**

## 理由

**共有される部分の方が圧倒的に多い。** 設計の初期に「共有カーネル / 完全に別 / 1 つ＋
オプション」の 3 案を保留にしたが、その後の計測と設計で答えが出た。

```
共有   Viewport・仮想化・行メモ化・Column・Selection・Anchor/Focus
       キーボード操作・クリップボード・Row Identity・Chrome の差し替え口
       ADR で言えば 0002〜0006, 0008〜0014, 0016, 0018 —— ほぼ全部

相違   データ所有権（ADR-0001）と数式エンジン
```

**用語集と ADR が既に両方をカバーしている。** `CONTEXT.md` は ExGrid と ExSheet を並べて
定義し、行メモ化も選択モデルも ExSheet にそのまま効く。リポジトリを割れば、**用語集と ADR を
分割するか複製する**ことになり、どちらも劣化する。

**外部の消費者がまだいない。** 別リポジトリの利点は独立したリリース周期だが、それを強制する
ものが存在しない。逆に、核を変えるたびにパッケージを公開して ExSheet 側を追随させるコストが
先に来る。

## ExSheet は ExGrid の兄弟か、Consumer か — 未決

[ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md) は「グリッドは Edit Intent を
出すだけ、確定した状態は Consumer が所有し、Overlay を適用した Window を押し込む」と定めた。
**ExSheet はまさに「セルの可変モデルを所有する Consumer」である。**

```
ExSheet（セルモデル・数式エンジンを所有）
   ↓ Window を押し込む / Edit Intent を受け取る
ExGrid（描画・選択・キーボード）
```

これが成立するなら、共有カーネルを別パッケージに切り出す必要すらない。**押す形にしたことが
ここでも効く。** [ADR-0016](./0016-column-width-and-overflow.md) で入れた「フォーカス中セルの
値の常時表示」は、そのまま数式バーになる。

摩擦もある。ExSheet の行挿入は `RowSequenceVersion` を上げるので、**行を挿すたびに選択が
消える**（[ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)）。
Sheet としては厳しすぎるかもしれない。

**いま決めない。** ExGrid を作れば ExSheet が何を追加で必要とするかが具体的に見える。

## Consequences

- **ADR の連番は 1 本。** ExSheet 固有の決定も同じ `docs/adr/` に積む。決定の多くは両方に
  効くので、分けると相互参照だらけになる。
- **CI は 1 回の実行で全部を検証する。** 核を変えたとき、ExSheet と連携パッケージが同時に
  ビルドされる。別リポジトリなら気づくのが遅れる。
- **`ExGrid` は依存を持たない。** MudBlazor も Fluxor も連携パッケージに閉じる
  （[ADR-0017](./0017-target-chromium-browsers-only.md) / [ADR-0018](./0018-multiple-instances-must-be-independent.md)）。
  同じリポジトリにあることと、依存が混ざることは別である。**プロジェクト参照の向きで
  構造的に守る。**
- **リポジトリ名は `ex-grid` のままでよい。** ExSheet を実際に作るときに、総称が要るかを
  含めて考え直せる。総称の 1 語は現時点では作らない。
