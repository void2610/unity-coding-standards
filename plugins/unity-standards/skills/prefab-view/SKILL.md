---
name: prefab-view
description: **Unity の UI View (MonoBehaviour) を実装・新規作成・修正するときは必ずこのスキルを呼ぶこと。** 画面・パネル・ダイアログ・オーバーレイ・リスト等、Image / TMP / Button / CanvasGroup を含む UI を伴う View を作るあらゆる場面で発動する。Prefab + SerializeField で構築する手順を定める。
---

# prefab-view

Unity の UI View を **最初から Prefab + SerializeField** で作るための標準手順。UI 階層をコードで組む (`new GameObject` / `AddComponent` で Image / TMP / Button / CanvasGroup 等を生成する) ことは禁止 (CLAUDE.md「UI構築原則」)。**新しい View を実装するとき、コードで UI を組み始める前にこの手順に沿う。**

## 大原則

- **View 実装 = Prefab を組む作業 + そこへ振る舞いを与える薄い MonoBehaviour**。UI の見た目・階層は Prefab に、View スクリプトは `[SerializeField]` 参照と表示ロジックだけを持つ。
- View だけが MonoBehaviour。Presenter / Model はピュア C# (DI設計原則)。
- コードで手続き的に UI GameObject を新規生成しない。Prefab に置いた既存オブジェクトを操作 (色・文字の差し替え / CanvasGroup の表示切替 / LitMotion で animate) するのは可。
- **`Assets/Scripts/Utils/Core/Extensions/LitMotionExtensions.cs` に既存のアニメーション拡張メソッド群がある。View のアニメーション・表示演出はこのファイルを起点に考える** (フェード・スライド等、`CanvasGroup` / `Image` / `TextMeshProUGUI` / `SpriteRenderer` / `RectTransform` 等の拡張が揃っている)。新しい演出が要る場面では **まずこのファイルを grep してから** `LMotion.Create(...)` を書き始める (詳細は下「鉄則: 表示/非表示は CanvasGroup、アニメーションは LitMotion + Utils 拡張メソッド」)。

## 中核の設計判断: 固定要素 と 反復要素 を分ける

作る UI を 2 種類に仕分けてから Prefab を設計する。

| 種別 | 例 | 置き場所 | View からの参照 |
| --- | --- | --- | --- |
| **固定要素** (1 個で、表示/非表示を切り替えるだけ) | ルート / コンテナ / タブバー / 全画面プレビュー / 閉じるボタン / ラベル | View の Prefab **内**に配置 | `[SerializeField]` で直接参照 |
| **反復要素** (データ数だけ動的に増減する) | タブ / グリッドのセル / リスト行 / トースト | **独立した Prefab ファイル**に切り出す | `[SerializeField] GameObject xxxPrefab` を保持し `Instantiate` |

**反復要素を独立 Prefab に切り出す理由**: View 本体の Prefab に「非活性テンプレート」を同居させる方式より、Prefab ファイルが分かれる方が **git の差分がファイル単位で追え、編集の衝突も減る**。テンプレートを View 内にコピーとして持たない。

`Instantiate(xxxPrefab, parent)` は正当な生成 (Prefab 参照からの複製)。禁止されるのは `new GameObject` / `AddComponent` での階層組み立て。

## 鉄則: プリミティブ UI は既存 Prefab を必ず探して再利用する

ボタン・テキスト・トグル・スライダー等の **UI プリミティブは自前で組まない**。プロジェクトには基底 Prefab が **必ず存在する** ので、まず探して再利用する。
同じ見た目・挙動 (SE / ホバー演出 / フォント配線) が全 View で揃い、二重メンテを避けられる。
基底が見つからない場合、ユーザーに新規作成の可否を確認する。

- **着手前に必ず** `ls Assets/Prefabs/UI/Buttons/` と `Assets/Prefabs/UI/<機能>/` を確認する。ボタンなら `TextButton` / `IconButton` / `ChoiceButton` 等、設定 UI なら `Settings_*` が既にある。
- 同種の要素が 2 つ以上 (例: OK / キャンセルの 2 ボタン) あれば、それらは **同一プリミティブの複数インスタンス**。手製で 2 回組まず、基底 Prefab をネスト配置 (固定数なら View Prefab 内にネスト prefab インスタンス、可変数なら `Instantiate`) して、ラベル文字やクリックだけを差し込む。
- 基底 Prefab をネストすれば、フォントも SE もホバー演出もその Prefab 側の設定を継承する (View 側でフォント配線しない)。
- 基底が見つからない・機能不足なら、まず基底 Prefab 側を拡張できないか検討する (手製の一点物を増やさない)。

`new GameObject(...) + AddComponent<Button>()` や TMP を手で組み立てるのは、この探索をサボった兆候。まず既存 Prefab を探す。

## 鉄則: 表示/非表示は CanvasGroup、アニメーションは LitMotionExtensions.cs を起点にする

- アニメーションは **LitMotion (`LMotion`) を使う**。`Coroutine` + 手書き lerp や `Update` でのフレーム毎手動補間は書かない。
- **`LMotion.Create(...)` を直接組み立てる前に、必ず `Assets/Scripts/Utils/Core/Extensions/LitMotionExtensions.cs` を grep して既存の拡張メソッドを探す** (車輪の再発明禁止 → [[check-utils-before-implementing]])。フェード: `CanvasGroup.FadeIn(duration)` / `FadeOut(duration)` / `FadeTo(duration, alpha)`、`Image.FadeIn` / `FadeOut`、`TextMeshProUGUI.FadeIn` / `FadeOut` など。完了時に `interactable` / `blocksRaycasts` を追従させる後処理も内包済みなので、手動で `LMotion.Create(...).BindToAlpha(...)` を書く前に必ず確認する。
    - 該当する拡張が無い場合のみ `LMotion.Create(...)` を直接組み立てる。その際も、他 View で同じ動きが今後も要りそうなら `LitMotionExtensions.cs` に拡張メソッドとして足すことを検討する (一点物として View 内に埋め込まない)。
- View やその子要素の表示/非表示は **`GameObject.SetActive` ではなく `CanvasGroup` を積極的に使う** (`alpha` / `interactable` / `blocksRaycasts`)。フェード演出が後から必要になっても構造を変えずに済み、`SetActive(false)` で失われる「非表示中もコルーチン/Tween/RectTransform 計算を継続させたい」ケースにも対応できる。
    - 即時切り替えでよい箇所は `Void2610.UnityTemplate.CanvasGroupExtensions` の `Show()` / `Hide()` を使う (`alpha`/`interactable`/`blocksRaycasts` を一括設定、手で3行書かない)。
    - フェードが要る箇所は上記 `LitMotionExtensions.cs` の `CanvasGroup.FadeIn()` / `FadeOut()` を使う。
    - `SetActive` を使ってよいのは、非表示中に一切の処理 (Tween 状態・RectTransform 計算・子オブジェクトの Update) を継続させる必要がない、純粋な ON/OFF のみ (例: ページめくりで前面画像を丸ごと隠す等)。表示状態の一部としてフェード演出が絡む対象は CanvasGroup にする。

## 鉄則: Canvas を新規に作らない

**シーンの Canvas は原則 1 つ**。View / オーバーレイ / ダイアログの Prefab に `Canvas` (+ `CanvasScaler` / `GraphicRaycaster`) を持たせない。UI ルートは素の `RectTransform` (全画面なら anchor 0..1 ストレッチ) にし、**シーンの既存 Canvas 配下に配置する** (`InstantiatePrefab(prefab, canvas.transform)`)。既存 View も Canvas を持たず単一 Canvas 配下にぶら下がる形にする。

- 前面に出したいだけなら Canvas を足さず、同一 Canvas 内で **末尾兄弟にする** (`SetAsLastSibling`)。描画順は階層順で決まる。
- 別 Canvas / `overrideSorting` / 専用 sortingOrder が本当に要る特殊用途 (別カメラ描画・ワールド空間 UI 等) は、**着手前にユーザーへ明示許可を求める**。勝手に増やさない。
- Prefab ルートの `RectTransform` は `m_LocalScale` を必ず `{1,1,1}` にする (0 のまま保存すると実行時に UI 全体が消える)。

## 鉄則: DI から複数の独立 Prefab を Instantiate するときは専用の親コンテナを用意する

`LifetimeScope.Configure` 等から `Object.Instantiate(xxxPrefab, parent)` を複数回呼んで独立 View 群を配置する場合、`parent` に「たまたま存在する別 View の transform」を借用しない。借用元の View を将来削除・移動すると Instantiate 先まで巻き添えで壊れ、依存関係が코드上に見えなくなる (`FindFirstObjectByType<SomeOtherView>().transform.parent` のような遠回りの参照になりがち)。

- 生成物専用の空 `RectTransform` (例: `PuzzlePartRoot`) をシーン上に用意し、`LifetimeScope` の `[SerializeField] private Transform xxxRoot;` で直接参照する。
- コンテナの `RectTransform` は Canvas 直下でフルストレッチ (anchorMin 0,0 / anchorMax 1,1) にし、それ自体は Canvas を持たない (上「鉄則: Canvas を新規に作らない」)。
- 複数種の Prefab をまとめて配置する用途に限らず、Instantiate 先の親は常に「その生成物のためだけに存在する」コンテナにする。

## 鉄則: 画面比率は 16:9 固定。アンカー/プリセットは変えず、相対座標とサイズで制御する

**このプロジェクトの画面比率は 16:9 に完全固定**で、解像度が変わるレスポンシブ対応は行わない。この前提があるため、要素の配置調整は **アンカー (`m_AnchorMin`/`m_AnchorMax`) やプリセットを変更するのではなく、`m_AnchoredPosition` (相対座標) と `m_SizeDelta` (サイズ) の数値だけで行う**。

- 既存要素の位置・サイズを調整するときは、**アンカー・ピボットのプリセットには触れず**、同じアンカーのまま `anchoredPosition` / `sizeDelta` を変更する。アンカーを変えると「基準点が変わる」ため既存の座標計算・スクリプト側のレイアウト前提 (中央寄せ計算等) が壊れやすく、原則として変更しない。
- 新規要素も、既存の兄弟要素や親のアンカー設計 (中央固定 `0.5,0.5` かフルストレッチ `0,0`-`1,1` か) に **合わせる**。View ごと・要素ごとにアンカー方式が混在すると、後から座標を追う人間にとって計算が複雑になる (「このオブジェクトはどの基準点から何px か」を要素ごとに調べ直す必要が生じる)。
- アンカーのプリセット変更が本当に必要な特殊ケース (例: 動的にアスペクトへ追従させたい要素を新規に作る等) は、**着手前にユーザーへ明示確認する**。勝手に変更しない。

## 手順 (新規 View 実装)

### 1. 設計する

- この View が表示する UI を洗い出し、**固定要素 / 反復要素**に仕分ける (上表)。
- **UI プリミティブ (ボタン・テキスト等) は先に既存 Prefab を探す** (`ls Assets/Prefabs/UI/Buttons/` 等)。見つけた基底 Prefab を再利用する前提で設計する (上「鉄則」)。
- 反復要素ごとに独立 Prefab を 1 つ作ると決める (例: リスト → `XxxRow.prefab`、グリッド → `XxxCell.prefab`)。
- レイアウト制御を決める。反復要素を並べる親に GridLayoutGroup / VerticalLayoutGroup 等を置き、セル側でサイズを持たせない設計にする。
- 配置は既存要素のアンカー方式に合わせ、`anchoredPosition`/`sizeDelta` の相対値で決める (上「鉄則: 画面比率は 16:9 固定」)。

### 2. View スクリプトを書く

- 固定要素は `[SerializeField]` フィールド、反復要素は `[SerializeField] GameObject xxxPrefab`。
- 表示/非表示の切り替えが要る固定要素は `[SerializeField] CanvasGroup` で持つ (`GameObject` で持って `SetActive` するのではなく)。表示演出は `CanvasGroupExtensions.Show()/Hide()` かフェードが要れば `LitMotionExtensions` の `FadeIn()/FadeOut()` を使う (上「鉄則: 表示/非表示は CanvasGroup」)。
- 反復生成は `Instantiate(xxxPrefab, parent)` → 中の Image/TMP/Button を `GetComponent` / `GetComponentInChildren` で取り出して値を差し込む。独立 Prefab はアクティブなルートで保存するので複製後の `SetActive(true)` は不要。
- **`[SerializeField]` に手動 null チェックを書かない** (アナライザ `VUA1001`。設定ミスは即クラッシュさせる方針)。`FindFirstObjectByType` で得た View も同様。
- **フォントをコードで配線しない** (`FindFirstObjectByType<TMP_Text>().font` や `xxx.font = template.font` は書かない)。フォントは Prefab の TMP にエディタ設定する。
- Presenter からは `FindFirstObjectByType<XxxView>()` で取得する (Presenter コンストラクタ、DI設計原則)。
- コメントは非自明な WHY のみ 1 行 (CLAUDE.md Comments)。

### 3. Prefab を uloop の dynamic code で構築する

UI 階層の組み立ては `uloop execute-dynamic-code` (Editor操作skill) でまとめて行う。
**uloop は MCP ではなく CLI ツールである。`uloop-*` skill から利用すること (→ `uloop-guide` skill)。**

- View 本体 Prefab: ルート (RectTransform) + 固定要素を組み、View コンポーネントを付ける。
- 反復要素 Prefab: 中身を組んで `SaveAsPrefabAsset(go, "Assets/Prefabs/UI/**/XxxCell.prefab")` で独立ファイル化。**アクティブなルート** (`SetActive(true)`) で保存する。
- **SerializeField の配線**: `new SerializedObject(view)` → `FindProperty("フィールド名").objectReferenceValue = 参照` → `ApplyModifiedPropertiesWithoutUndo()`。反復要素 Prefab は `AssetDatabase.LoadAssetAtPath<GameObject>(path)` で読んで配線。
- `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents` で開閉。
- dynamic code 内で `Object` は曖昧参照になるため `UnityEngine.Object.DestroyImmediate` と明示する。
- **落とし穴**: `FindProperty` はコンパイル済みアセンブリのフィールドを引く。C# のフィールドを追加・改名したら **先に `uloop-compile` してから**配線 dynamic code を流す (未コンパイルだと `FindProperty` が null → NRE)。
- 表示演出が要る固定要素は `CanvasGroup` コンポーネントを付けて配線する (Image 単体に頼らない)。アニメーションの実装自体はスクリプト側で `LitMotionExtensions.cs` の既存拡張を使う (上「鉄則」)。

### 4. 検証

1. `uloop-compile` (ForceRecompile) → `uloop-get-logs` (Error) で 0 件。
2. `uloop clear-console` して再コンパイルし、構築途中の一過性エラー (`SerializedObjectNotCreatableException` 等) が残っていないか確認。
3. `./unity-coding-standards/scripts/run-format.sh` を実行し警告全文を確認 (`warning|error|IDE[0-9]|VUA[0-9]|Unable to fix`)。
4. ロジックは PlayMode の `[LiminalScenario]` で検証する (無ければ書き足す。検証はシナリオ化して回帰コーパスを育てる → `liminal-palette-guide` skill)。`uloop-run-tests --test-mode PlayMode`。
5. 見た目は Play モードで目視。反復要素の実データが無いと確認できない場合は `uloop-execute-dynamic-code` で View の MonoBehaviour を直接見つけ、ダミーデータで `Show(...)` 等を駆動して目視できる状態を作る。Presenter はピュア C# なので `FindFirstObjectByType` では拾えない (View を直接叩く)。

## 既存 View の Prefab 化 (是正)

既にコード生成で組まれた View を直す場合も設計判断は同じ。追加で:

- 既存 Prefab の階層を `grep -nE "m_Name:" <prefab>` で掴み、動的生成メソッド (`Build...` / `Create...` / `Ensure...`) を洗い出す。
- View 内に一旦テンプレートを組んで独立 Prefab へ切り出す場合は、`SaveAsPrefabAsset(child.gameObject, newPath)` の後に `DestroyImmediate` で View 内から除去してから配線する。
- 既存コードにランタイム生成が残っていても手本にしない。触る箇所を Prefab 化していく。

## 注意

- フォント SDF アセット (`*SDF.asset`) がエディタ由来で差分に出ることがある。作業と無関係なら `git checkout` で破棄しコミットに含めない (不整合な multi-atlas SDF は TMP のクラッシュ源になり得るノイズ)。
