> [中文](README.md) | [English](README_EN.md) | **日本語** | [한국어](README_KO.md)

# Shoujo Odyssey Cards<br><small>少女オデッセイカード</small>

ハースストーン風のターン制カードバトル Roguelite — Godot 4.6 + C#。

> **ブランチ:** `dev` | **状態:** プレイ可能 MVP、拡張中<br>
> 175 個の `Scripts/*.cs`、約 37,000 行のゲームコード。戦闘ループ、コレクション、マップ、セーブ、多言語、開発者コンソール、ランタイム QA が動作。

## コアシステム

### カードバトル

2×5 のミニオンボードで行うターン制戦闘。マナ、アーマー、武器、領域、レリックフック、Heat ダメージ圧力が存在します。

- **ミニオン**：ボードユニット。攻撃力/体力/キーワードを持つ
- **呪文**：カード種別は存在。実行時は共通 `Card` 経路で処理
- **領域**：戦闘イベントで継続発動するフィールド効果
- **武器**：ヒーロー装備とスキル
- **ヒーロー**：アーマー接続済み。4 つのヒーローパワー実装が存在し、戦闘 UI はヒーローごとの実機確認が必要
- **レリック**：ライフサイクルフックあり。リソース化は進行中
- **Heat**：戦闘全体のテンポ圧力。ダメージパイプラインに接続

### キーワード

| キーワード | 効果 |
|------------|------|
| 突撃 (Charge) | 召喚ターンに攻撃可能 |
| 挑発 (Taunt) | 敵ミニオンはこのミニオンを優先攻撃 |
| 雄叫び (Battlecry) | 手札からプレイ時に発動 |
| 断末魔 (Deathrattle) | 死亡時に発動 |
| 疾風 (Windfury) | 毎ターン 2 回攻撃可能 |
| 伏撃 (Ambush) | 毎ターン最初に攻撃された時、先制反撃 |
| 衝撃 (Impact) | 攻撃時の反撃ダメージを一度だけ無効化 |

### ダメージ計算

四段階パイプライン：`ADDITIVE → MULTIPLICATIVE → HEAT → CAPPING`。アーマー吸収はその後。

### AI / Intent

敵は独立 actor。各敵が HP、MoveState、Intent を持ちます。新 Intent 体系は `Scripts/AI/Intents/` にあり、複数 Intent、動的ダメージ表示、アイコン、tooltip を扱います。

### Roguelike

マップ、報酬/発見、イベント/ショップ/休憩 UI が存在します。イベント/ショップ/休憩は MapUI と戦闘報酬フローへの接続がまだ途中です。

### コレクション / 多言語 / DevConsole

CollectionUI はカード閲覧とデッキ編集を提供。多言語は YAML（`zh.yaml` / `en.yaml`）で、`Localization.T()` と `GameManager.LanguageChanged` を使います。DevConsole はリソース、ダメージ、召喚、レリック、戦闘ジャンプ、QA コマンドを持つ Autoload です。

## 技術スタック

- **エンジン**: Godot 4.6
- **言語**: C# (.NET 8.0, Godot.NET.Sdk/4.6.2)
- **テスト**: xUnit（10 Unit + 1 Integration。Integration は Godot Resource 依存で skip）
- **プラットフォーム**: Windows。Android エクスポートスクリプトあり

## プロジェクト構造

```
Scripts/
├── Core/ (35)           # CardData, DamageResolver, GameManager, HeroProfile, RarityColorScheme…
├── UI/ (39)             # CombatUI partials, CardUI, BoardUI, CollectionUI, MapUI, Shop/Rest/Event UI…
├── Card/ (15)           # Card, Minion, Hero, Weapon, StatusEffect, ActiveDomain, HeroPowers/*
├── Character/ (5)       # Player, CommanderCore, Deck, CombatDeckState
├── Combat/ (14)         # CombatManager + AttackTracker/SelectionSystem/DeathHandler/WeaponAttackSystem/DomainTriggerManager…
├── AI/ (27)             # EnemyEncounter, EnemyRegistry, Brains, Intents/(19)
├── Heat/ (2)            # HeatSystem + HeatDamageModifier
├── Relic/ (7)           # AbstractRelic, RelicManager, concrete relics
├── Roguelike/ (5)       # EventSelector, RoomData, GameRunState, EventData, BlessingData
├── Localization/ (5)    # YAML localization
└── Infrastructure/ (20) # DevConsole, InputManager, HotkeyManager, MobileInputRouter, Commands/8
Resources/Cards/         # 37 .tres resources
Resources/Localization/  # zh.yaml / en.yaml
Scenes/                  # Main, Combat, Collection, Map + Card/Board/CombatPreview
```

### アーキテクチャの特徴

- 純 C# ドメイン：Card/Minion/Hero/Board/GameState/EnemyUnit は Node を継承しない
- プログラム的 UI：Combat.tscn は主にコンテナ。Card/Board/Combat の `[Tool]` プレビューあり
- CombatUI は core/Layout/Refresh/Selection の partial 分割
- CombatManager は純 C# 補助システムへ委譲（コンストラクタ注入 + Action callback）
- Godot `[Signal]` 不使用。C# `event Action<>`
- InputManager → HotkeyManager → scene UI
- エクスポート時の DirAccess 制限に対するリソース fallback

## ビルド / テスト / エクスポート

```bash
dotnet build
dotnet build -c Release
dotnet test
dotnet format OdysseyCards.sln --verify-no-changes

./build_export.ps1 [-Debug] [-SkipBuild]
./build_android.ps1 [-SkipBuild] [-ExportOnly]
./package_release.ps1 [version] [-OpenFolder]
```

現在 GitHub Actions / Dockerfile / Makefile はありません。GUT は導入済みですが GDScript テストはありません。

## シーン

| シーン | パス | 説明 |
|--------|------|------|
| メインメニュー | `Scenes/Main.tscn` | エントリーシーン |
| 戦闘 | `Scenes/Combat.tscn` | 戦闘、プログラム的 UI |
| コレクション | `Scenes/Collection.tscn` | コレクションとデッキ編集 |
| マップ | `Scenes/Map.tscn` | Roguelike ルートマップ |
| カードプレビュー | `Scenes/CardPreview.tscn` | エディタープレビュー |
| ボードプレビュー | `Scenes/BoardPreview.tscn` | エディタープレビュー |
| 戦闘プレビュー | `Scenes/CombatPreview.tscn` | エディタープレビュー |

## Autoload シングルトン

- **GameManager** (`Scripts/Core/GameManager.cs`) — グローバル状態、カード登録、永続化、言語切替
- **UIScaler** (`Scripts/UI/UIScaler.cs`) — UI スケーリング、現在基準 1152×648
- **DevConsole** (`Scripts/Infrastructure/DevConsole.cs`) — 開発者コンソール
- **MobileInputHelper** (`Scripts/Infrastructure/MobileInputHelper.cs`) — 旧タッチ補助。非戦闘 UI で使用中
- **MobileInputRouter** (`Scripts/Infrastructure/MobileInputRouter.cs`) — モバイル入力ルーティングとモーダルスタック
- **InputManager** (`Scripts/Infrastructure/InputManager.cs`) — 物理キーから論理アクションへ
- **HotkeyManager** (`Scripts/Infrastructure/HotkeyManager.cs`) — アクション callback スタック

## 既知の制限

- `Spell.cs` は未インスタンス化。実行時は共通 `Card` 経路
- `RailPistolPassive.cs` と `SafeAreaContainer.cs` は現在孤立
- Shop/Rest/Event UI は存在するが MapUI フローへ完全接続されていない
- IronWill / 星光補給 / 火力筛选 / 重整 のヒーローパワー実装が存在。戦闘 UI はヒーローごとの実機確認が必要
- 手札上限なし。疲労は未完（`Status_Fatigue.tres` は存在）
- `InfoScreen.cs` は非推奨 Godot API `SplitOffset` を使用中

## ライセンス

本プロジェクトは混合ライセンスです：

- **コード**（`Scripts/` 以下の `.cs` と設定）：[MIT](LICENSE_CODE)
- **アート/オーディオアセット**（`Assets/`）：[CC BY 4.0](LICENSE_ASSETS)

## 謝辞

本プロジェクトのアーキテクチャ設計は [slay-the-model](https://github.com/wkzMagician/slay-the-model) を参考にしています。カードゲーム設計の貴重な学習リソースです。
