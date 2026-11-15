# 星图档案 · CardCollectionWeb

《少女星途卡牌》的本地全栈收藏与战术工具。包含实时卡牌 API、程序化 Canvas 卡图、收藏筛选、卡组构筑、主题卡组生成、英雄/武器/藏品图鉴，以及可交互的三线前线规则沙盘。

## 启动

需要 Node.js 22+（推荐 24）与 npm。

```bash
npm install
npm run dev
```

- 前端：`http://127.0.0.1:5173`
- API：`http://127.0.0.1:8787`
- Vite 已代理 `/api`，前端无需额外环境变量。

## 构建与生产运行

```bash
npm run typecheck
npm run build
npm start
```

生产服务在 `http://127.0.0.1:8787` 同时提供 API 与 `dist/client` 静态站点。构建产物包含：

- `dist/client/`：Vite/React 前端。
- `dist/server/server/index.js`：TypeScript 编译后的 Node 服务。

## 自测

```bash
npm test
npm run typecheck
npm run build
```

另开终端启动生产服务后：

```bash
npm run smoke
# 同时检查生产 HTML 时（bash）：
CHECK_CLIENT=1 npm run smoke
```

烟雾测试实际请求所有 API，验证 61 张卡、19 个随从兵种、4 位英雄、7 把武器、8 件藏品、3 份主题、4/5/4 规则，以及固定种子的 20 张主题卡组可复现生成。

```bash
npm run verify       # 构建、启动生产服务、HTTP 验收并自动关闭服务
npm run test:browser # 额外使用本机 Chrome 做真实浏览器交互验收
```

浏览器验收覆盖 61 张卡渲染、检视台、入组与刷新后的本地持久化、图鉴数量、13 槽沙盘、场景切换和中英切换。`verify-production.mjs` / `browser-smoke.mjs` 均直接管理 Node 子进程并在 `finally` 中关停；Chrome 可用 `CHROME_PATH` 指定。

## 数据来源

每次 API 请求都会重新读取仓库，不维护静态卡表副本：

- `Resources/Cards/*.tres`：61 张卡牌及其效果子资源。
- `Resources/Themes/*.tres`：主题核心卡、标签权重与构筑约束。
- `Resources/Localization/zh.yaml`、`en.yaml`：中英文名称、文本、英雄技能和武器词条。
- `Scripts/Core/HeroProfile.cs`：英雄生命与初始配置。
- `Scripts/Card/WeaponSkill.cs`、`RailPistolPassive.cs`：武器数值、主动/被动与充能。
- `Scripts/Relic/*Relic.cs`：8 件藏品定义。
- `docs/draft/PRD-kards-frontline-refactor.md`：四兵种、射程、防线优先级与情报规则。

资源原始字段与 PRD 适配字段分开返回。若资源尚无 `UnitClass` / `Guard`，19 个随从使用 PRD §7.1 映射；显式资源字段优先。所有 61 张卡都有战术定位，非法术随从之外的卡牌以指令、领域、资源调度等定位表示，兵种为 `not-applicable`。英文 YAML 缺项回退到资源原文，并在卡牌检视台标明。

## API

| 接口                            | 内容                                                         |
| ------------------------------- | ------------------------------------------------------------ |
| `GET /api/health`               | 服务状态                                                     |
| `GET /api/cards`                | 全量卡牌、原始属性、双语、效果、关联卡、卡图种子与战术元数据 |
| `GET /api/cards/:id`            | 单卡数据                                                     |
| `GET /api/heroes`               | 英雄、英雄技能、武器与射程                                   |
| `GET /api/relics`               | 藏品                                                         |
| `GET /api/themes`               | 主题 Profile                                                 |
| `POST /api/themes/:id/generate` | 生成主题卡组；可传 `{ "seed": 42 }` 复现                     |
| `GET /api/rules`                | 三线棋盘、兵种、关键词、防线优先级与情报规则                 |

主题生成复刻 `ThemedDeckGenerator.cs` 的合法池过滤、核心卡、机制/关键词权重、同名与领域上限、法力曲线和随从比例软约束。

## 前端操作

- **收藏集**：英雄主题、双费用、类型、兵种、稀有度、关键词、机制标签和全文搜索组合过滤；支持中英文与两套稀有度色板。
- **卡牌检视**：点击卡牌打开高清检视台，查看词条、PRD 定位、关联衍生卡与 `.tres` 原始属性。关联卡可连续跳转。
- **构筑**：卡牌右下角 `+` 入组，检视台也可入组；右侧显示 10–20 张状态、稀有度同名上限、费用曲线和兵种/类型分布。
- **主题生成**：在构筑台选择绮梦、理惠或溯光，实时调用后端生成 20 张预览卡组。
- **保存与交换**：自动保存到 `localStorage`；支持 JSON、文本列表导入导出。超限导入不会被静默截断，而是显示诊断。
- **战术沙盘**：选择预设或手动布置双方单位，切换攻击者兵种，查看 HQ 与所有随从的合法性和拦截原因。右键槽位快速拆除。
- 页面支持 `#collection`、`#builder`、`#codex`、`#wiki` 地址。

## 卡图引擎

`src/art/engine.ts` 使用与 Godot 生成器同源的 32 位 FNV-1a 卡牌 ID 种子和 12 类主符号。高 DPR Canvas 分层绘制多焦点星云、星尘与扫描网格、星座、Rune 星盘/月相/神圣几何、Mecha 装甲/瞄准分划/HUD/弹道、Abstract 晶格/引力环/脉冲。卡框、双费用晶体、兵种 SVG 徽章与稀有度数字在 React 层组合。

## 目录

```text
server/          Express API、Godot/YAML 读取、主题生成与测试
shared/          前后端共享类型、常量、规则和纯防线计算
src/art/         确定性 Canvas 卡图与纹章引擎
src/components/  收藏卡、检视台、构筑台、图鉴与沙盘
src/lib/         卡组模型及纯函数测试
scripts/         真实 HTTP 烟雾测试
```

规则沙盘对应三线重构 Draft v1.0；它是规则交互预览，不表示当前 Godot 主分支已完成全部战斗改造。
