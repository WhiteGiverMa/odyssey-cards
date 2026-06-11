using Godot;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// 键盘/手柄输入动作名称注册表 — 项目唯一权威来源。
///
/// 所有游戏代码只引用此处的 StringName 常量，绝不硬编码 Key 值或 action 字符串。
/// 对齐 STS2 MegaInput 模式：动作名是 UI 层和输入层的唯一通信协议。
///
/// 命名规则：
///   - 标准导航复用 Godot 内置 ui_* 动作（已在 project.godot InputMap 中定义）
///   - Odyssey 特有动作用 odyssey_ 前缀
/// </summary>
public static class OdysseyInput
{
	// ===== 标准导航（Godot 内置 ui_*，方向键/回车/ESC）=====

	public static readonly StringName Up = "ui_up";
	public static readonly StringName Down = "ui_down";
	public static readonly StringName Left = "ui_left";
	public static readonly StringName Right = "ui_right";
	public static readonly StringName Accept = "ui_accept";
	public static readonly StringName Cancel = "ui_cancel";
	public static readonly StringName Select = "ui_select";
	public static readonly StringName FocusNext = "ui_focus_next";
	public static readonly StringName FocusPrev = "ui_focus_prev";

	// ===== 战斗 — 手牌操作 =====

	/// <summary>直接选择手牌第 1 张（数字键 1）</summary>
	public static readonly StringName SelectCard1 = "odyssey_select_card_1";
	public static readonly StringName SelectCard2 = "odyssey_select_card_2";
	public static readonly StringName SelectCard3 = "odyssey_select_card_3";
	public static readonly StringName SelectCard4 = "odyssey_select_card_4";
	public static readonly StringName SelectCard5 = "odyssey_select_card_5";
	public static readonly StringName SelectCard6 = "odyssey_select_card_6";
	public static readonly StringName SelectCard7 = "odyssey_select_card_7";
	public static readonly StringName SelectCard8 = "odyssey_select_card_8";
	public static readonly StringName SelectCard9 = "odyssey_select_card_9";
	public static readonly StringName SelectCard10 = "odyssey_select_card_10";

	/// <summary>键盘选牌动作数组，索引 0 对应第 1 张牌（SelectCard1）</summary>
	public static readonly StringName[] SelectCardActions =
	[
		SelectCard1, SelectCard2, SelectCard3, SelectCard4, SelectCard5,
		SelectCard6, SelectCard7, SelectCard8, SelectCard9, SelectCard10,
	];

	// ===== 战斗 — 目标选择 =====

	/// <summary>循环切换可选目标（Tab 键）</summary>
	public static readonly StringName TabTarget = "odyssey_tab_target";

	// ===== 战斗 — 全局命令 =====

	/// <summary>结束回合（E 键）</summary>
	public static readonly StringName EndTurn = "odyssey_end_turn";

	/// <summary>暂停/返回（Escape，上下文区分）</summary>
	public static readonly StringName Pause = "odyssey_pause";

	/// <summary>查看牌库（D 键）</summary>
	public static readonly StringName ViewDeck = "odyssey_view_deck";

	/// <summary>查看弃牌堆（S 键）</summary>
	public static readonly StringName ViewDiscard = "odyssey_view_discard";

	// ===== 场景导航 =====

	/// <summary>翻页向上（PageUp）</summary>
	public static readonly StringName PageUp = "odyssey_page_up";

	/// <summary>翻页向下（PageDown）</summary>
	public static readonly StringName PageDown = "odyssey_page_down";

	/// <summary>跳过/放弃（Backspace 键，选牌场景）</summary>
	public static readonly StringName Skip = "odyssey_skip";

	// ===== 全局界面 =====

	/// <summary>综合信息管理界面（CapsLock 键）</summary>
	public static readonly StringName InfoScreen = "odyssey_info_screen";

	/// <summary>使用英雄技能（H 键）</summary>
	public static readonly StringName HeroPower = "odyssey_hero_power";

	// ===== 所有可被阻塞的动作（AddBlockingScreen 用）=====

	/// <summary>阻塞屏幕时拦截的所有动作列表。</summary>
	public static readonly StringName[] AllInputs =
	[
		Up, Down, Left, Right, Accept, Cancel, Select, FocusNext, FocusPrev,
		SelectCard1, SelectCard2, SelectCard3, SelectCard4, SelectCard5,
		SelectCard6, SelectCard7, SelectCard8, SelectCard9, SelectCard10,
		TabTarget, EndTurn, Pause, ViewDeck, ViewDiscard,
		PageUp, PageDown, Skip, InfoScreen, HeroPower,
	];
}
