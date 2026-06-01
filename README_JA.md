> [中文](README.md) | [English](README_EN.md) | **日本語**

# Shoujo Odyssey Cards<br><small>少女オデッセイカード</small>

ハースストーン風のターン制カードバトル Roguelite — Godot 4.6 + C#

> **ブランチ:** `main` | **状態:** プレイ可能 MVP
> 59 `.cs` ファイル、約 17,700 行。ターン制戦闘ループが完全に動作し、カードコレクション、マップルート、セーブ機能を搭載。

## コアシステム

### カードバトル

2×5 のミニオンボード（両サイド各 5 スロット）でのターン制戦闘。マナクリスタルシステム（初期 1、毎ターン +1、最大 10）。

- **ミニオン (Minion)**：ボードに配置可能、攻撃力/体力を持ち、7 種のキーワードに対応
- **呪文 (Spell)**：手札からプレイすると即座に効果を発揮
- **領域 (Domain)**：永続的なフィールド効果、グローバルルールに影響
- **武器 (Weapon)**：ヒーロー装備、攻撃力と武器スキルを提供
- **ヒーロー (Hero)**：各ヒーローはヒーローパワー（未実装）とアーマー機構を持つ

### キーワード

| キーワード | 効果 |
|------------|------|
| 突撃 (Charge) | 召喚されたターンに攻撃可能 |
| 挑発 (Taunt) | 敵ミニオンはこのミニオンを優先的に攻撃しなければならない |
| 雄叫び (Battlecry) | 手札からプレイされた時に効果を発動 |
| 断末魔 (Deathrattle) | ミニオンが死亡した時に効果を発動 |
| 疾風 (Windfury) | 毎ターン 2 回攻撃可能 |
| 伏撃 (Ambush) | 毎ターン最初に攻撃された時、攻撃者より先に反撃ダメージを与える |
| 衝撃 (Impact) | 攻撃時、すべての反撃ダメージを無効化（使い切り） |

### ダメージ計算

三段階パイプライン：`ADDITIVE → MULTIPLICATIVE → CAPPING`（DamageResolver）。最小ダメージは 1 にクランプ。

### AI システム

Slay the Spire 式の意図ローテーション、3 種の敵タイプ：

- **カルティスト (Cultist)**：HP 20、パターン Attack(6)→Attack(6)→Defend(5)
- **スライムボス (SlimeBoss)**：HP 40、パターン Attack(8)→Summon(1)→Defend(4)、1/1 スライムを召喚
- **ウルフライダー (WolfRider)**：HP 12、パターン Attack(5)、安定したダメージ出力

### Roguelike

戦闘後 3 択の戦利品（EventSelector + RewardUI）、Fisher-Yates シャッフル。マップルート選択（MapUI）。

> ⚠️ EventSelector の戦闘後報酬ロジックは完成しているが、戦闘ループに未接続。

### カードコレクション

CollectionUI はカード閲覧とデッキ編集を提供。レアリティ別の色分け、説明文の適応表示、削除確認に対応。デッキにはソフトキャップあり。

### ローカライゼーション

YAML ベースの多言語システム（`Scripts/Localization/`）、中国語/英語の二言語対応。すべての UI テキストは `GameManager.LanguageChanged` イベントで動的に更新。

### 開発者コンソール

`DevConsole`（Autoload シングルトン）— `` ` `` キーで表示切替。11 以上のコマンド（`/damage`、`/draw`、`/mana`、`/heal`、`/armor`、`/end` など）で迅速なテストとデバッグが可能。

### ポーズメニュー

ESC またはボタンで全画面オーバーレイを表示。ゲーム再開、設定（言語切替）、セーブ、クイックセーブ/ロードを含む。

### セーブシステム

SaveDataManager + GameSaveData でゲーム進行の永続化を提供。

## 技術スタック

- **エンジン**: Godot 4.6
- **言語**: C# (.NET 8.0, Godot.NET.Sdk/4.6.2)
- **テスト**: xUnit（4 テストファイル、303 行）
- **プラットフォーム**: Windows

## プロジェクト構造

```
Scripts/
├── Core/ (16)           # CardData, DamageResolver, GameManager (Autoload), Keyword, CardType, SaveDataManager…
├── UI/ (15)             # CombatUI, BoardUI, HandUI, CardUI, CollectionUI, MapUI, PauseMenu, DiscoverUI, RewardUI…
├── Card/ (9)            # Card, Minion, Spell, Hero, Weapon, WeaponSkill, ActiveDomain, StatusEffect (純粋 C#)
├── Character/ (5)       # Player, CommanderCore, Deck, CombatDeckState, ICommander
├── Combat/ (3)          # CombatManager (1740 行), Board, GameState (純粋 C#)
├── AI/ (1)              # IntentAI (Cultist/SlimeBoss/WolfRider)
├── Roguelike/ (3)       # EventSelector, RoomData, GameRunState
├── Localization/ (5)    # YAML ベースの多言語システム
└── Infrastructure/ (1)  # DevConsole (Autoload) — 開発者コンソール
Resources/Cards/         # 16 枚のカードデータ .tres（7 呪文 + 6 ミニオン + 3 領域）
Resources/Localization/  # zh.yaml / en.yaml 翻訳ファイル
Scenes/                  # Main.tscn, Combat.tscn, Collection.tscn, Map.tscn（4 シーン）
```

### アーキテクチャの特徴

- **プログラム的 UI**：CombatUI および子コンポーネントはすべてコードで生成、.tscn に非依存（Combat.tscn はレイアウトコンテナのみ提供）
- **純粋 C# コア**：Card/Minion/Hero/Board/GameState は Godot Node を継承せず、シーンツリーとゼロ結合
- **二重 CommanderCore**：Player と CombatManager がそれぞれ CommanderCore を保持し、`internal Deck setter` でデッキを共有
- **C# イベント**：Godot `[Signal]` 不使用 — すべて `event Action<...>` を使用
- **Pull 式 UI 更新**：`CombatUI.RefreshAll()` で駆動、`_Process` ポーリングなし
- **自動初期化**：シーン読み込み後 `CallDeferred` で戦闘を自動開始、12 枚の初期デッキ

## ビルド

```bash
# デバッグビルド
dotnet build

# リリースビルド
dotnet build -c Release

# フォーマットチェック (CI)
dotnet format OdysseyCards.sln --verify-no-changes

# 自動フォーマット
dotnet format OdysseyCards.sln

# テスト実行
dotnet test
```

## シーン

| シーン | パス | 説明 |
|--------|------|------|
| メインメニュー | `Scenes/Main.tscn` | エントリーシーン |
| 戦闘 | `Scenes/Combat.tscn` | 戦闘シーン、プログラム的 UI レイアウト |
| コレクション | `Scenes/Collection.tscn` | カードコレクションとデッキ編集 |
| マップ | `Scenes/Map.tscn` | Roguelike ルート選択 |

## Autoload シングルトン

- **GameManager** (`Scripts/Core/GameManager.cs`) — グローバル状態、戦闘間の永続化、言語切替
- **UIScaler** (`Scripts/UI/UIScaler.cs`) — UI スケーリング、基準解像度 1152×648
- **DevConsole** (`Scripts/Infrastructure/DevConsole.cs`) — 開発者コンソール、`` ` `` キーで表示切替

## 既知の制限

- ⚠️ **Spell.cs が未インスタンス化** — CombatManager はすべてのカードに Card 基底クラスを使用（デッドコード）
- ⚠️ **EventSelector 未接続** — 戦闘後報酬ロジックは完成しているが呼び出し元なし
- ⚠️ **ヒーローパワー未実装** — IHeroPower インターフェースが空
- ⚠️ **手札上限なし / 疲労なし** — デッキ切れ時の処理未実装

## ライセンス

本プロジェクトは混合ライセンスを採用しています：

- **コード**（`Scripts/` 以下の `.cs` ソースファイルおよびプロジェクト設定ファイル）：[MIT](LICENSE_CODE)
- **アート/オーディオアセット**（`Assets/` 以下の画像、音声などのメディアファイル）：[CC BY 4.0](LICENSE_ASSETS)

## 謝辞

本プロジェクトのアーキテクチャ設計は [slay-the-model](https://github.com/wkzMagician/slay-the-model) を参考にしています。これは構造の明確な『Slay the Spire』コアフレームワークであり、カードゲームアーキテクチャ設計の貴重な学習リソースとなりました。
