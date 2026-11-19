# 星图档案 · CardCollectionWeb

《少女星途卡牌》的本地全栈收藏、构筑、资源编辑与战术百科工具。实时解析 `.tres` / YAML / 角色源码，提供程序化 Canvas 卡图、双语游戏文本超链接、行级无损资源写回和 CLI 校验。

## 启动

需要 Node.js 22+（推荐 24）与 npm。

```bash
npm install
npm run dev
```

- 前端：`http://127.0.0.1:5173`
- API：`http://127.0.0.1:8787`
- Vite 已代理 `/api`，前端无需额外环境变量。

## 四页签导航

| 页签              | 地址          | 功能                                                                                                                                            |
| ----------------- | ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| 01 收藏与构筑     | `#collection` | 三栏一体：战术筛选、卡牌网格、右侧卡组构筑抽屉；支持 10–20 张校验、费用曲线、兵种分布、主题生成及 JSON/文本交换                                 |
| 02 标签与主题工坊 | `#workshop`   | 61 张卡批量兵种/关键词/机制矩阵；「卡包编排」勾选多卡后一键归类到目标包或新建 DIY 包；3 套 ThemeProfile 权重与核心卡池可视化编辑；一键 Validate |
| 03 英雄与军械     | `#codex`      | 卡包档案（数量 + 卡牌预览筛选）与 4 位英雄、7 把武器、8 件藏品；技能与描述中的术语和关联卡均可悬停/跳转                                         |
| 04 三线战术       | `#wiki`       | 交互式 4/5/4 防线沙盘、情报演算，以及可筛选/锚点定位的完整词条百科                                                                              |

旧地址 `#builder` 自动重定向到 `#collection`，并确保构筑抽屉展开。

## 游戏文本超链接系统

`shared/glossary.ts` 是中英双语词条真源，覆盖全部 12 个战斗关键词、状态效果、四兵种与空间/资源机制。`RichGameText` 应用于网格卡面、检视台正文与战术说明、英雄技能、武器主被动、藏品和 Wiki：

- 最长匹配优先，英文大小写不敏感并带词边界保护，不会把 `Unsupported` 中的 `Support` 错认成词条。
- 悬停或键盘焦点打开视口自适应战术浮层，展示分类、双语名称与完整规则。
- 点击词条进入 `#wiki/term/<id>`，百科卡片平滑滚动并高亮。
- `「受身」`、`"Heavy Strike"`、`“Heavy Strike”` 等引用会映射到真实卡牌；浮层展示费用、类型、兵种/身材和描述，点击直接切换检视台。
- 卡牌网格中的链接阻止点击冒泡，不会误触发当前卡的检视或入组。

## 卡包系统（0.2.1）

`shared/packs.ts` 定义六个标准卡包（核心 / 无人智胜 / 未来追杀 / 超体计划 / 预备 / 先锋）与 `docs/draft/卡包.md` 的默认归属规则：

- `CardRecord.packId` 直接读取 `.tres` 的 `PackId`；资源缺字段时按显式卡单 + 机械族标签推断（显式名单优先，岷山与 40 系坦克带机械标签但归属无人智胜）。
- `GET /api/packs` 返回标准包与从 `.tres` 自动发现的自定义包，均带实时卡牌数量；自定义包显示名保存在浏览器本地。
- 收藏页「卡包系列」筛选与卡包档案页可按包预览；卡面、检视台显示系列徽章。
- 工坊「卡包编排」支持勾选多张卡后点目标卡包一键批量写回，或输入新 ID/显示名现场建包并归类；`CardTagEditor` 内也可单卡切换。
- `POST /api/packs/assign` 接收 `{ cardIds, packId }`，只写 `PackId`，返回最新 catalog；`PUT /api/cards/:id/tags` 也可带 `packId` 单卡写回。

## 无损资源编辑边界

`server/tres-writer.ts` 是原 C# CardTagEditor 的 TypeScript 行级 round-trip 实现：

- 未触碰的 `[gd_resource]`、`[ext_resource]`、`[sub_resource]`、脚本、特效、未知属性与注释逐字节保留。
- 保留原始 CRLF/LF、文件末尾换行状态；字段值未变化时不规范化原格式。
- 卡牌只允许写 `UnitClass`（仅 `Type = 0`，缺省先锋不插入冗余行）、`Keywords`、`MechanicTags`、`PackId`，保存时清理遗留 `Speed`。
- `Keywords` 同时读取裸数组与 `Array[int]`；实际变更时使用 Godot 类型化数组，变更为空时移除该行。
- 主题只允许写 `TagWeights`、`KeywordWeights`（缺失时可插入）、`CoreCardIds`。
- **不开放** `Cost`、`ActionCost`、`Attack`、`Defense`、`Health` 等平衡数值；API 对未知/只读字段返回 400。
- 服务端使用同目录临时文件 + 原子替换，避免半写入资源。

检视台与工坊均可编辑；保存后 API 重新读取资源并返回最新 `CardRecord` / `ThemeRecord`，前端即时刷新徽章和全局状态。

## CLI

```bash
npm run tag -- list                    # 列出所有卡牌与主题摘要
npm run tag -- dump spell_heavy_strike # JSON 转储单卡资源字段
npm run tag -- dump theme:ayame        # JSON 转储单主题
npm run tag -- validate                # 全量资源健康校验；错误时退出码为 1
```

CLI 仅提供只读命令。写回通过 Web/API 完成。

## 构建、测试与验收

```bash
npm test
npm run typecheck
npm run build
npm run verify       # 构建、启动生产服务、HTTP 验收并自动关闭服务
npm run test:browser # 构建并使用真实 Chrome 完成交互与截图验收
```

测试覆盖 Godot Variant 解析、全部真实资源的只读字节级 round-trip、CRLF/LF、未知字段与子资源保留、UnitClass 幂等、Speed 清理、`PackId` 插入/更新/幂等与临时仓库批量归类、默认卡包映射、主题字典插入、异常校验、卡组/沙盘/分词器。

浏览器验收会在**系统临时目录**复制 Resources 与 Scripts，并通过 `ODYSSEY_REPOSITORY_ROOT` 指向副本；所有 PUT 都只写临时夹具。验收覆盖四页签、`#builder` 重定向、沉重打击的易伤/失能浮层、关联卡跳转、内联保存、平衡字段拒绝、主题幂等和 Validate，输出：

- `dist/qa-collection.png`
- `dist/qa-inspector-hyperlinks.png`
- `dist/qa-workshop.png`

Node/Chrome 子进程均在 `finally` 中等待退出并清理临时目录。Chrome 可用 `CHROME_PATH` 指定。

另开生产服务后可运行 `npm run smoke`；`CHECK_CLIENT=1 npm run smoke` 同时检查生产 HTML。

## API

| 接口                            | 内容                                                                      |
| ------------------------------- | ------------------------------------------------------------------------- |
| `GET /api/health`               | 服务状态                                                                  |
| `GET /api/cards`                | 全量卡牌、原始属性、双语、效果、关联卡、卡图种子与战术元数据              |
| `GET /api/cards/:id`            | 单卡数据                                                                  |
| `PUT /api/cards/:id/tags`       | 无损写回 `{ mechanicTags, keywords, unitClass?, packId? }` 并返回最新卡牌 |
| `GET /api/packs`                | 全部卡包（标准 + 自动发现的自定义）及卡牌数量                             |
| `POST /api/packs/assign`        | `{ cardIds, packId }` 批量归类，只写 `PackId`，返回最新 catalog           |
| `GET /api/heroes`               | 英雄、英雄技能、武器与射程                                                |
| `GET /api/relics`               | 藏品                                                                      |
| `GET /api/themes`               | 主题 Profile                                                              |
| `PUT /api/themes/:id`           | 写回 `{ tagWeights, keywordWeights, coreCardIds }` 并返回最新主题         |
| `POST /api/themes/:id/generate` | 生成主题卡组；可传 `{ "seed": 42 }` 复现                                  |
| `GET /api/rules`                | 三线棋盘、兵种、关键词、防线优先级与情报规则                              |
| `GET /api/validate`             | 返回 `{ ok, cards, themes, errors, warnings, checkedAt }`                 |

CORS 允许 `GET, POST, PUT, OPTIONS`。

## 数据来源

每次 API 请求都会重新读取仓库，不维护静态卡表副本：

- `Resources/Cards/*.tres`：61 张卡牌及其效果子资源。
- `Resources/Themes/*.tres`：主题核心卡、标签/关键词权重与构筑约束。
- `Resources/Localization/zh.yaml`、`en.yaml`：双语名称、文本、技能和武器词条。
- `Scripts/Core/HeroProfile.cs`：英雄生命与初始配置。
- `Scripts/Card/WeaponSkill.cs`、`RailPistolPassive.cs`：武器数值与主被动。
- `Scripts/Relic/*Relic.cs`：8 件藏品定义。
- `wiki/keywords-status-domains.md`、`wiki/frontline-classes-and-defense.md`：词条语义参考；实现事实仍以代码和当前资源为准。

资源原始字段与 PRD 适配字段分开返回；显式资源字段优先。英文 YAML 缺项回退到资源原文并在检视台标明。主题生成复刻 `ThemedDeckGenerator.cs` 的合法池过滤、核心卡、权重、同名/领域上限、法力曲线和随从比例软约束。

## 卡图引擎与目录

`src/art/engine.ts` 使用与 Godot 生成器同源的 32 位 FNV-1a 卡牌 ID 种子和 12 类主符号，以高 DPR Canvas 绘制星云、星尘、Rune、Mecha 与 Abstract 卡图；卡框、双费用晶体、兵种徽章与稀有度在 React 层组合。

```text
server/          Express API、Godot/YAML 读取、无损写回、资源服务与测试
shared/          类型、常量、卡包定义与默认归属、规则、双语词条真源、分词器与纯防线计算
src/art/         确定性 Canvas 卡图与纹章引擎
src/components/  收藏卡、检视台、构筑台、工坊、图鉴、浮层与沙盘
src/lib/         卡组模型及纯函数测试
scripts/         CLI、HTTP 烟雾测试与临时副本 Chrome 验收
```
