> [中文](README.md) | [English](README_EN.md) | [日本語](README_JA.md) | **한국어**

# Odyssey Cards<br><small>소녀 오디세이 카드</small>

하스스톤 스타일의 턴제 카드 배틀 Roguelite — Godot 4.6 + C#.

> **브랜치:** `dev` | **상태:** 플레이 가능 MVP, 확장 중<br>
> 175개 `Scripts/*.cs` 파일, 약 37,000줄의 게임 코드. 전투 루프, 컬렉션, 맵 루트, 세이브, 다국어, 개발자 콘솔, 런타임 QA가 동작. 용어 대조표（도메인 = STS2 Power, 유닛 = Hero+Minion 등）는 루트 `AGENTS.md`의 Architecture Rules 섹션 참조.

## 핵심 시스템

### 카드 배틀

2×5 미니언 보드에서 진행되는 턴제 전투. 마나, 방어도, 무기, 도메인, 유물 후크, Heat 데미지 압력이 존재합니다.

- **미니언 (Minion)**: 보드 유닛. 공격력/생명력/키워드를 가짐
- **주문 (Spell)**: 카드 타입은 존재. 런타임은 공통 `Card` 경로로 처리
- **도메인 (Domain)**: 영구 Power（STS2 Power 상당）. 전투 이벤트에서 지속 발동하는 필드 효과. 시한 마운트 효과（四夜雷電光, 星途精神 다음 턴 수익 등）는 StatusEffect 채널을 사용하며, 도메인이 아님
- **무기 (Weapon)**: 영웅 장비와 스킬
- **영웅 (Hero)**: 방어도 연결 완료. 4개의 영웅 파워 구현이 존재하며, 전투 UI는 영웅별 실기 검증 필요
- **유물 (Relic)**: 라이프사이클 후크 존재. 리소스화 진행 중
- **Heat**: 전투 전체의 템포 압력. 데미지 파이프라인에 연결

### 키워드

| 키워드 | 영문 | 효과 |
|--------|------|------|
| 돌진 | Charge | 소환된 턴에 공격 가능 |
| 도발 | Taunt | 적 미니언은 이 미니언을 우선 공격해야 함 |
| 전투의 함성 | Battlecry | 손에서 사용 시 효과 발동 |
| 죽음의 메아리 | Deathrattle | 미니언 사망 시 효과 발동 |
| 질풍 | Windfury | 매 턴 2회 공격 가능 |
| 매복 | Ambush | 매 턴 최초 피격 시, 공격자보다 먼저 반격 |
| 충격 | Impact | 공격 시 반격 데미지를 1회 무효화 |

### 데미지 계산

4단계 파이프라인: `ADDITIVE → MULTIPLICATIVE → HEAT → CAPPING` (DamageResolver). 방어도 흡수는 그 이후.

### AI / 인텐트

적은 독립 actor. 각 적이 HP, MoveState 체인, 인텐트를 가집니다. 새 인텐트 체계는 `Scripts/AI/Intents/`에 있으며, 다중 인텐트, 동적 데미지 표시, 아이콘, 툴팁을 다룹니다.

### Roguelike

맵 루트, 보상/발견, 이벤트/상점/휴식 UI가 존재합니다. 이벤트/상점/휴식은 MapUI와 전투 보상 흐름 연결이 아직 진행 중입니다.

### 컬렉션 / 다국어 / ChatScreen

CollectionUI는 카드 열람과 덱 편집을 제공합니다. 다국어는 YAML (`zh.yaml` / `en.yaml`)로, `Localization.T()`와 `GameManager.LanguageChanged`를 사용합니다. ChatScreen은 리소스, 데미지, 소환, 유물, 전투 점프, QA 명령을 갖춘 Autoload입니다.

## 기술 스택

- **엔진**: Godot 4.6
- **언어**: C# (.NET 8.0, Godot.NET.Sdk/4.6.2)
- **테스트**: xUnit (10 Unit + 1 Integration. Integration은 Godot Resource 의존성으로 skip)
- **플랫폼**: Windows. Android 익스포트 스크립트 존재

## 프로젝트 구조

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
├── Localization/ (5)    # YAML 다국어
└── Infrastructure/ (20) # ChatScreen, InputManager, HotkeyManager, MobileInputRouter, Commands/8
Resources/Cards/         # 37 .tres resources
Resources/Localization/  # zh.yaml / en.yaml
Scenes/                  # Main, Combat, Collection, Map + Card/Board/CombatPreview
```

### 아키텍처 특징

- 순수 C# 도메인: Card/Minion/Hero/Board/GameState/EnemyUnit은 Node를 상속하지 않음
- 프로그래매틱 UI: Combat.tscn은 주로 컨테이너. Card/Board/Combat용 `[Tool]` 프리뷰 있음
- CombatUI는 core/Layout/Refresh/Selection의 partial 분할
- CombatManager는 순수 C# 보조 시스템으로 위임 (생성자 주입 + Action callback)
- Godot `[Signal]` 미사용. C# `event Action<>`
- InputManager → HotkeyManager → scene UI
- 익스포트 시 DirAccess 제한에 대한 리소스 fallback

## 빌드 / 테스트 / 익스포트

```bash
dotnet build
dotnet build -c Release
dotnet test
dotnet format OdysseyCards.sln --verify-no-changes

./build_export.ps1 [-Debug] [-SkipBuild]
./build_android.ps1 [-SkipBuild] [-ExportOnly]
./package_release.ps1 [version] [-OpenFolder]
```

현재 GitHub Actions / Dockerfile / Makefile은 없습니다. GUT는 도입되었으나 GDScript 테스트는 없습니다.

## 씬

| 씬 | 경로 | 설명 |
|----|------|------|
| 메인 메뉴 | `Scenes/Main.tscn` | 엔트리 씬 |
| 전투 | `Scenes/Combat.tscn` | 전투, 프로그래매틱 UI |
| 컬렉션 | `Scenes/Collection.tscn` | 컬렉션과 덱 편집 |
| 맵 | `Scenes/Map.tscn` | Roguelike 루트 맵 |
| 카드 프리뷰 | `Scenes/CardPreview.tscn` | 에디터 프리뷰 |
| 보드 프리뷰 | `Scenes/BoardPreview.tscn` | 에디터 프리뷰 |
| 전투 프리뷰 | `Scenes/CombatPreview.tscn` | 에디터 프리뷰 |

## Autoload 싱글톤

- **GameManager** (`Scripts/Core/GameManager.cs`) — 글로벌 상태, 카드 등록, 영속화, 언어 전환
- **UIScaler** (`Scripts/UI/UIScaler.cs`) — UI 스케일링, 현재 기준 1152×648
- **ChatScreen** (`Scripts/Infrastructure/ChatScreen.cs`) — 개발자 콘솔
- **MobileInputHelper** (`Scripts/Infrastructure/MobileInputHelper.cs`) — 구형 터치 보조. 비전투 UI에서 사용 중
- **MobileInputRouter** (`Scripts/Infrastructure/MobileInputRouter.cs`) — 모바일 입력 라우팅과 모달 스택
- **InputManager** (`Scripts/Infrastructure/InputManager.cs`) — 물리 키에서 논리 액션으로
- **HotkeyManager** (`Scripts/Infrastructure/HotkeyManager.cs`) — 액션 callback 스택

## 알려진 제한 사항

- `Spell.cs`는 미인스턴스화. 런타임은 공통 `Card` 경로
- `RailPistolPassive.cs`와 `SafeAreaContainer.cs`는 현재 격리 상태
- Shop/Rest/Event UI는 존재하나 MapUI 흐름에 완전히 연결되지 않음
- IronWill / 별빛 보급 / 화력 선별 / 재정비 영웅 파워 구현 존재. 전투 UI는 영웅별 실기 검증 필요
- 손패 상한 없음. 피로 시스템 미완성 (`Status_Fatigue.tres`는 존재)
- `InfoScreen.cs`는 비권장 Godot API `SplitOffset` 사용 중

## 라이선스

본 프로젝트는 혼합 라이선스입니다:

- **코드** (`Scripts/` 이하 `.cs` 및 설정): [MIT](LICENSE_CODE)
- **아트/오디오 애셋** (`Assets/`): [CC BY 4.0](LICENSE_ASSETS)

## 스토리 요약

2048년, 전(前) AGI 시대. 강대국 간 초한전과 우주 패권 경쟁 속에 중국은 「성도 계획(星途計劃)」을 발족, 민간에 「성도 카드(星途卡牌)」라는 엔터테인먼트를 보급하여 각계각층의 시민이 열성적으로 참여하게 된다. 알려진 바에 따르면, 「794국」이 전 국민의 대전 데이터를 수집하여 우주 공간에서의 생산·생활, 나아가 군사 분야에까지 투입될 AGI 에이전트 「네비게이터」를 훈련시키고 있다고 한다. 졸업을 앞둔 한여름, 소녀 치멍(Qimeng)은 《성도 카드》 데스크톱 버전을 설치하고 많은 동료를 사귄다. 그녀는 카드 실력을 갈고닦으며 전략을 다듬어 나가는데…… 대회에서 차례로 승리한 끝에, 그녀를 기다리는 결승 상대는 과연……

## 감사의 말

본 프로젝트의 아키텍처 설계는 [slay-the-model](https://github.com/wkzMagician/slay-the-model)을 참고하였습니다. 카드 게임 설계의 귀중한 학습 자료입니다.

또한, [김연규 (code-yeongyu)](https://github.com/code-yeongyu)님께서 개발하신 [Oh My OpenAgent](https://github.com/code-yeongyu/oh-my-openagent)에 깊은 감사를 드립니다. 본 프로젝트의 개발에 큰 도움이 되었습니다.

---

> 특별히 감사드립니다 — [김연규](https://github.com/code-yeongyu) · [Oh My OpenAgent](https://github.com/code-yeongyu/oh-my-openagent)
