using System.Diagnostics.CodeAnalysis;

namespace OdysseyCards.Tools.CardTagEditor.Web;

/// <summary>
/// 内嵌的 Web UI 单页应用（Alpine.js via CDN + vanilla fetch）。
/// 零构建步骤——不依赖 node/npm。
/// 两个视图：卡牌标签编辑器 + 主题画像编辑器。
/// </summary>
internal static class IndexPage
{
	[SuppressMessage("MSBuild", "CA1051", Justification = "内嵌常量")]
	public const string Html = """
<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<title>CardTagEditor — 星途卡牌标签编辑器</title>
<meta name="viewport" content="width=device-width, initial-scale=1">
<script defer src="https://unpkg.com/alpinejs@3.x.x/dist/cdn.min.js"></script>
<style>
:root { --bg:#1a1a1a; --panel:#2a2a2a; --fg:#e0e0e0; --muted:#888; --accent:#4a9eff; --warn:#ffa500; --ok:#4caf50; }
* { box-sizing: border-box; }
body { margin:0; background:var(--bg); color:var(--fg); font-family:system-ui,-apple-system,sans-serif; font-size:14px; }
header { padding:12px 20px; background:var(--panel); border-bottom:1px solid #444; display:flex; align-items:center; gap:16px; }
header h1 { margin:0; font-size:16px; font-weight:600; }
header nav { display:flex; gap:12px; }
header nav button { background:transparent; border:1px solid #444; color:var(--fg); padding:6px 14px; border-radius:4px; cursor:pointer; }
header nav button.active { background:var(--accent); border-color:var(--accent); }
header nav button:hover:not(.active) { border-color:var(--accent); }
main { display:grid; grid-template-columns: 380px 1fr; height:calc(100vh - 50px); }
aside { background:var(--panel); border-right:1px solid #444; overflow-y:auto; padding:8px; }
aside input[type=search] { width:100%; padding:8px; background:#222; border:1px solid #555; color:var(--fg); border-radius:4px; }
.unit-pills { display:flex; flex-wrap:wrap; gap:8px; margin:10px 0 18px; }
.unit-pills.compact { gap:5px; margin:8px 0 10px; }
button.unit-pill { background:#222; color:var(--fg); border:1px solid #555; border-radius:999px; padding:6px 13px; cursor:pointer; transition:background .15s,border-color .15s,color .15s; }
button.unit-pill:hover { border-color:var(--accent); color:#fff; }
button.unit-pill.active { background:var(--accent); border-color:var(--accent); color:#fff; box-shadow:0 0 0 2px #4a9eff22; }
.unit-pills.compact button.unit-pill { padding:4px 9px; font-size:12px; }
.unit-pill .english { margin-left:5px; opacity:.7; font-size:11px; }
.unit-badge { display:inline-block; margin-left:6px; padding:1px 7px; border:1px solid #666; border-radius:999px; color:#b9d8f5; font-size:10px; font-weight:400; vertical-align:1px; }
aside .card.active .unit-badge { border-color:#ffffff88; color:#fff; }
aside .card { padding:8px 10px; border-radius:4px; cursor:pointer; margin:4px 0; }
aside .card:hover { background:#333; }
aside .card.active { background:var(--accent); color:#fff; }
aside .card .id { font-size:11px; color:var(--muted); }
aside .card.active .id { color:#e0f0ff; }
aside .card .name { font-weight:500; }
section { padding:20px 28px; overflow-y:auto; }
section h2 { margin:0 0 4px; font-size:18px; }
section .subtitle { color:var(--muted); font-size:12px; margin-bottom:18px; }
.tags-grid { display:grid; grid-template-columns: repeat(auto-fill,minmax(180px,1fr)); gap:8px; margin-bottom:20px; }
.tags-grid label { display:flex; align-items:center; gap:6px; padding:6px 8px; background:var(--panel); border-radius:4px; cursor:pointer; }
.tags-grid label:hover { background:#333; }
.tags-grid input[type=checkbox] { accent-color:var(--accent); }
.dirty-banner { background:var(--warn); color:#000; padding:6px 12px; border-radius:4px; display:inline-block; margin-left:12px; font-size:12px; }
button.save { background:var(--ok); color:#fff; border:none; padding:8px 20px; border-radius:4px; cursor:pointer; font-weight:500; }
button.save:disabled { background:#555; cursor:not-allowed; }
button.reset { background:transparent; color:var(--fg); border:1px solid #444; padding:8px 16px; border-radius:4px; cursor:pointer; margin-left:8px; }
.theme-table { width:100%; border-collapse:collapse; margin-bottom:20px; }
.theme-table th, .theme-table td { padding:6px 10px; border:1px solid #444; text-align:left; }
.theme-table th { background:var(--panel); }
.theme-table input { width:80px; background:#222; border:1px solid #555; color:var(--fg); padding:4px 6px; border-radius:3px; }
.theme-table input.kw { width:340px; }
.list-line { color:var(--muted); font-size:12px; }
.toast { position:fixed; bottom:20px; right:20px; background:var(--ok); color:#fff; padding:10px 18px; border-radius:4px; z-index:99; }
.toast.error { background:#c33; }
</style>
</head>
<body x-data="editor()">
<header>
	<h1>CardTagEditor</h1>
	<nav>
		<button :class="{active: view==='cards'}" @click="view='cards'">卡牌标签</button>
		<button :class="{active: view==='themes'}" @click="view='themes'">主题画像</button>
	</nav>
	<span class="list-line" x-text="status"></span>
</header>

<!-- 卡牌视图 -->
<template x-if="view==='cards'">
<main>
	<aside>
		<input type="search" placeholder="搜索卡牌..." x-model="cardSearch" @input="filterCards()">
		<div class="unit-pills compact" aria-label="按兵种筛选">
			<button type="button" class="unit-pill" :class="{active: unitClassFilter==='all'}" @click="setUnitClassFilter('all')">全部</button>
			<template x-for="unit in schema.unitClasses" :key="unit.value">
				<button type="button" class="unit-pill" :class="{active: unitClassFilter===unit.value}"
					@click="setUnitClassFilter(unit.value)" x-text="unit.displayName"></button>
			</template>
		</div>
		<template x-for="c in filteredCards" :key="c.id">
			<div class="card" :class="{active: selectedCardId===c.id}" @click="loadCard(c.id)">
				<div class="id" x-text="c.id"></div>
				<div class="name"><span x-text="c.cardName + ' [' + typeLabel(c.type) + ']'"></span><span class="unit-badge" x-show="c.type===0" x-text="unitClassLabel(c.unitClass)"></span></div>
			</div>
		</template>
	</aside>
	<section x-show="currentCard">
		<template x-if="!currentCard"><p class="subtitle">从左侧选择一张卡牌开始编辑。</p></template>
		<template x-if="currentCard">
			<div>
				<h2 x-text="currentCard.cardName"></h2>
				<p class="subtitle" x-text="currentCard.id + ' · ' + typeLabel(currentCard.type)"></p>

				<template x-if="currentCard.type===0">
					<div>
						<h3>兵种</h3>
						<div class="unit-pills" aria-label="随从兵种">
							<template x-for="unit in schema.unitClasses" :key="unit.value">
								<button type="button" class="unit-pill" :class="{active: cardEdit.unitClass===unit.value}"
									:aria-pressed="cardEdit.unitClass===unit.value" @click="setCardUnitClass(unit.value)">
									<span x-text="unit.displayName"></span><span class="english" x-text="unit.name"></span>
								</button>
							</template>
						</div>
					</div>
				</template>

				<h3>机制标签</h3>
				<div class="tags-grid">
					<!-- x-model 装字符串名数组；value 用 name 才能匹配勾选 -->
					<template x-for="tag in schema.mechanicTags" :key="tag.bit">
						<label><input type="checkbox" :value="tag.name" x-model="cardEdit.mechanicTagsBitNames" @change="markCardDirty()">
							<span x-text="tag.displayName || tag.name"></span></label>
					</template>
				</div>

				<h3>关键词</h3>
				<div class="tags-grid">
					<template x-for="kw in schema.keywords" :key="kw.value">
						<label><input type="checkbox" :value="kw.value" x-model="cardEdit.keywordValues" @change="markCardDirty()">
							<span x-text="kw.displayName || kw.name"></span></label>
					</template>
				</div>

				<p style="margin-top:24px;">
					<span class="dirty-banner" x-show="cardDirty">未保存</span>
					<button class="save" :disabled="!cardDirty" @click="saveCard()">保存</button>
					<button class="reset" @click="loadCard(selectedCardId)">重置</button>
				</p>
			</div>
		</template>
	</section>
</main>
</template>

<!-- 主题视图 -->
<template x-if="view==='themes'">
<main>
	<aside>
		<template x-for="t in themes" :key="t.heroId">
			<div class="card" :class="{active: selectedThemeId===t.heroId}" @click="loadTheme(t.heroId)">
				<div class="id" x-text="t.heroId"></div>
				<div class="name" x-text="t.themeName"></div>
			</div>
		</template>
	</aside>
	<section x-show="currentTheme">
		<template x-if="!currentTheme"><p class="subtitle">从左侧选择一个主题画像开始编辑。</p></template>
		<template x-if="currentTheme">
			<div>
				<h2 x-text="currentTheme.themeName"></h2>
				<p class="subtitle" x-text="currentTheme.heroId"></p>

				<h3>机制标签权重 <span class="list-line">（key=位值, value=权重，正=偏好/负=不擅长）</span></h3>
				<table class="theme-table">
					<thead><tr><th>标签</th><th>位值</th><th>权重</th></tr></thead>
					<tbody>
						<template x-for="tag in schema.mechanicTags" :key="tag.bit">
							<tr>
								<td x-text="tag.displayName || tag.name"></td>
								<td x-text="tag.bit"></td>
								<td><input type="number" :data-bit="tag.bit" x-model.number="themeEdit.tagWeights[tag.bit]" @input="markThemeDirty()"></td>
							</tr>
						</template>
					</tbody>
				</table>

				<h3>关键词权重</h3>
				<table class="theme-table">
					<thead><tr><th>关键词</th><th>值</th><th>权重</th></tr></thead>
					<tbody>
						<template x-for="kw in schema.keywords" :key="kw.value">
							<tr>
								<td x-text="kw.displayName || kw.name"></td>
								<td x-text="kw.value"></td>
								<td><input type="number" :data-kw="kw.value" x-model.number="themeEdit.keywordWeights[kw.value]" @input="markThemeDirty()"></td>
							</tr>
						</template>
					</tbody>
				</table>

				<h3>核心卡牌 ID</h3>
				<textarea class="kw" x-model="themeEdit.coreCardIdsText" @input="markThemeDirty()" rows="4"
					placeholder="每行一个卡牌 ID"></textarea>

				<p style="margin-top:24px;">
					<span class="dirty-banner" x-show="themeDirty">未保存</span>
					<button class="save" :disabled="!themeDirty" @click="saveTheme()">保存</button>
					<button class="reset" @click="loadTheme(selectedThemeId)">重置</button>
				</p>
			</div>
		</template>
	</section>
</main>
</template>

<div class="toast" x-show="toast.show" x-text="toast.msg" :class="{error: toast.isError}"></div>

<script>
function editor() {
	return {
		view: 'cards',
		status: '加载中...',
		cards: [],
		filteredCards: [],
		cardSearch: '',
		unitClassFilter: 'all',
		selectedCardId: null,
		currentCard: null,
		cardDirty: false,
		cardEdit: { mechanicTagsBitNames: [], keywordValues: [], unitClass: 0 },

		themes: [],
		selectedThemeId: null,
		currentTheme: null,
		themeDirty: false,
		themeEdit: { tagWeights: {}, keywordWeights: {}, coreCardIdsText: '' },

		schema: { mechanicTags: [], keywords: [], unitClasses: [] },

		toast: { show: false, msg: '', isError: false },

		async init() {
			try {
				const [cards, themes, schema] = await Promise.all([
					fetch('/api/cards').then(r => r.json()),
					fetch('/api/themes').then(r => r.json()),
					fetch('/api/schema').then(r => r.json()),
				]);
				this.cards = cards;
				this.themes = themes;
				this.schema = schema;
				this.filteredCards = cards;
				this.status = `共 ${cards.length} 张卡牌 / ${themes.length} 个主题`;
			} catch (e) {
				this.status = '加载失败: ' + e.message;
				this.showToast('加载失败: ' + e.message, true);
			}
		},

		filterCards() {
			const q = this.cardSearch.toLowerCase();
			this.filteredCards = this.cards.filter(c => {
				const matchesSearch = q === '' || c.id.toLowerCase().includes(q) || (c.cardName||'').toLowerCase().includes(q);
				const matchesUnitClass = this.unitClassFilter === 'all'
					|| (c.type === 0 && Number(c.unitClass ?? 0) === Number(this.unitClassFilter));
				return matchesSearch && matchesUnitClass;
			});
		},

		setUnitClassFilter(value) {
			this.unitClassFilter = value;
			this.filterCards();
		},

		async loadCard(id) {
			if (this.cardDirty && !confirm('当前卡牌有未保存改动，切换将丢弃。继续？')) return;
			this.selectedCardId = id;
			try {
				const c = await fetch('/api/card/' + encodeURIComponent(id)).then(r => r.json());
				this.currentCard = c;
				// MechanicTags 位掩码 → 位名列表
				this.cardEdit.mechanicTagsBitNames = this.schema.mechanicTags
					.filter(t => (c.mechanicTags & t.bit) !== 0).map(t => t.name);
				// Keywords int[] → int values；旧随从缺省为先锋（0）
				this.cardEdit.keywordValues = (c.keywords || []).map(k => k);
				this.cardEdit.unitClass = c.type === 0 ? Number(c.unitClass ?? 0) : null;
				this.cardDirty = false;
			} catch (e) { this.showToast('加载卡牌失败: ' + e.message, true); }
		},

		markCardDirty() { this.cardDirty = true; },

		setCardUnitClass(value) {
			const next = Number(value);
			if (this.cardEdit.unitClass === next) return;
			this.cardEdit.unitClass = next;
			this.markCardDirty();
		},

		async saveCard() {
			if (!this.currentCard) return;
			// 位名列表 → 位掩码
			let mask = 0;
			for (const name of this.cardEdit.mechanicTagsBitNames) {
				const t = this.schema.mechanicTags.find(t => t.name === name);
				if (t) mask |= t.bit;
			}
			const body = { mechanicTags: mask, keywords: this.cardEdit.keywordValues };
			if (this.currentCard.type === 0) body.unitClass = this.cardEdit.unitClass ?? 0;
			try {
				await fetch('/api/card/' + encodeURIComponent(this.currentCard.id), {
					method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(body),
				});
				this.cardDirty = false;
				this.currentCard.mechanicTags = mask;
				this.currentCard.keywords = this.cardEdit.keywordValues;
				if (this.currentCard.type === 0) this.currentCard.unitClass = body.unitClass;
				// 同步左侧列表与兵种筛选
				const c = this.cards.find(c => c.id === this.currentCard.id);
				if (c) {
					c.mechanicTags = mask;
					c.keywords = this.cardEdit.keywordValues;
					if (c.type === 0) c.unitClass = body.unitClass;
				}
				this.filterCards();
				this.showToast('已保存：' + this.currentCard.cardName);
			} catch (e) { this.showToast('保存失败: ' + e.message, true); }
		},

		async loadTheme(heroId) {
			if (this.themeDirty && !confirm('当前主题有未保存改动，切换将丢弃。继续？')) return;
			this.selectedThemeId = heroId;
			try {
				const t = await fetch('/api/theme/' + encodeURIComponent(heroId)).then(r => r.json());
				this.currentTheme = t;
				// 初始化编辑副本——所有 schema 标签/关键词都列出来，未列的权重为空（不写入）
				this.themeEdit.tagWeights = {};
				for (const tag of this.schema.mechanicTags)
					this.themeEdit.tagWeights[tag.bit] = t.tagWeights[tag.bit] ?? '';
				this.themeEdit.keywordWeights = {};
				for (const kw of this.schema.keywords)
					this.themeEdit.keywordWeights[kw.value] = (t.keywordWeights||{})[kw.value] ?? '';
				this.themeEdit.coreCardIdsText = (t.coreCardIds||[]).join('\n');
				this.themeDirty = false;
			} catch (e) { this.showToast('加载主题失败: ' + e.message, true); }
		},

		markThemeDirty() { this.themeDirty = true; },

		async saveTheme() {
			if (!this.currentTheme) return;
			// 过滤掉空值权重——只写非空非零项（保留显式 0？暂不保留，空=不列）
			const tagWeights = {};
			for (const [bit, w] of Object.entries(this.themeEdit.tagWeights)) {
				if (w !== '' && w !== null && !Number.isNaN(w)) tagWeights[bit] = Number(w);
			}
			const keywordWeights = {};
			for (const [v, w] of Object.entries(this.themeEdit.keywordWeights)) {
				if (w !== '' && w !== null && !Number.isNaN(w)) keywordWeights[v] = Number(w);
			}
			const coreCardIds = this.themeEdit.coreCardIdsText.split('\n').map(s=>s.trim()).filter(s=>s.length>0);
			const body = { tagWeights, keywordWeights, coreCardIds };
			try {
				await fetch('/api/theme/' + encodeURIComponent(this.currentTheme.heroId), {
					method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(body),
				});
				this.themeDirty = false;
				this.showToast('已保存主题：' + this.currentTheme.themeName);
			} catch (e) { this.showToast('保存失败: ' + e.message, true); }
		},

		typeLabel(t) { return ({0:'随从',1:'法术',2:'领域'})[t] || ('Type'+t); },

		unitClassLabel(value) {
			const unit = this.schema.unitClasses.find(u => u.value === Number(value ?? 0));
			return unit ? unit.displayName : '先锋';
		},

		showToast(msg, isError=false) {
			this.toast = { show: true, msg, isError };
			setTimeout(() => this.toast.show = false, 2500);
		},
	};
}
</script>
</body>
</html>
""";
}
