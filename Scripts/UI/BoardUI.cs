using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

/// <summary>三线 4/5/4 棋盘表现。位置统一为 BoardPosition；规则由 CombatManager 提供。</summary>
[Tool]
public partial class BoardUI : Control
{
	public event Action<BoardPosition>? OnSlotClicked;
	public event Action<BoardPosition>? OnSlotRightClicked;
	private readonly Dictionary<BoardPosition, BoardSlot> _slots = new();
	private readonly Dictionary<BoardPosition, EffectBar> _effectBars = new();
	private readonly Dictionary<BoardPosition, Minion> _pendingDeploy = new();
	private readonly Dictionary<BoardPosition, Action> _effectBarRebuiltHandlers = new();
	private readonly Dictionary<BoardRow, Label> _rowTitles = new();
	private readonly Dictionary<BoardRow, HBoxContainer> _rowLines = new();
	private FrontlineArenaBackdrop _backdrop = null!;
	private Action<FrontlineOwner, FrontlineOwner>? _frontlineOwnerChanged;
	private Board? _board;
	private BoardPosition? _focused;
	private bool _keyboardEnabled;
	private bool _includePlayer;
	private bool _includeEnemy;
	private bool _arenaSyncQueued;
	private int _availableMana = int.MaxValue;
	private UIScaler? _fontScaleSource;
	private const int RowTitleBaseFontSize = 12;

	/// <summary>下一波美术组件可按位置挂载，不能更改模型状态。</summary>
	public BoardSlot? GetSlotView(BoardPosition position) => _slots.GetValueOrDefault(position);
	public static string GetUnitClassName(UnitClass unitClass) => Loc.T($"unit_class.{unitClass.ToString().ToLowerInvariant()}", unitClass.ToString());

	/// <summary>将规则层兵种映射为视觉层矢量徽章，供 CardUI 与 BoardSlot 共用。</summary>
	internal static TacticalUnitIcon GetTacticalUnitIcon(UnitClass unitClass) => unitClass switch
	{
		UnitClass.Vanguard => TacticalUnitIcon.Vanguard,
		UnitClass.Assault => TacticalUnitIcon.Assault,
		UnitClass.Support => TacticalUnitIcon.Support,
		UnitClass.Skirmisher => TacticalUnitIcon.Skirmisher,
		_ => throw new ArgumentOutOfRangeException(nameof(unitClass)),
	};

	public override void _Ready()
	{
		_backdrop = new FrontlineArenaBackdrop
		{
			Name = "FrontlineArenaBackdrop",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_backdrop);
		MoveChild(_backdrop, 0);

		var rows = new VBoxContainer { Name = "BoardRows", Alignment = BoxContainer.AlignmentMode.Center };
		rows.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		rows.AddThemeConstantOverride("separation", 12);
		AddChild(rows);
		var effectLayer = new CanvasLayer { Name = "EffectLayer", Layer = 10 };
		AddChild(effectLayer);
		foreach (var row in Enum.GetValues<BoardRow>())
		{
			var title = new Label
			{
				Text = Loc.T($"board_row.{row.ToString().ToLowerInvariant()}", row.ToString()),
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			title.AddThemeFontSizeOverride("font_size", UIScaler.ApplyFontScale(RowTitleBaseFontSize));
			_rowTitles.Add(row, title);
			rows.AddChild(title);
			var line = new HBoxContainer { Name = row.ToString(), Alignment = BoxContainer.AlignmentMode.Center };
			line.AddThemeConstantOverride("separation", 10);
			_rowLines.Add(row, line);
			rows.AddChild(line);
			for (int slot = 0; slot < Board.GetRowCapacity(row); slot++)
			{
				var position = new BoardPosition(row, slot);
				var view = new BoardSlot(position, this);
				_slots.Add(position, view);
				line.AddChild(view);
				var bar = new EffectBar { Name = $"{row}EffectBar{slot}" };
				_effectBars.Add(position, bar);
				effectLayer.AddChild(bar);
				// 图标是延迟两帧才入树的，宽度变化必须回调重排，否则居中用的是旧宽度。
				Action rebuilt = () => RepositionEffectBar(position);
				_effectBarRebuiltHandlers.Add(position, rebuilt);
				bar.OnIconsRebuilt += rebuilt;
			}
		}
		QueueArenaSynchronization();

		_fontScaleSource = UIScaler.Instance;
		if (_fontScaleSource != null)
			_fontScaleSource.OnFontScaleChanged += OnFontScaleChanged;
	}

	public override void _ExitTree()
	{
		DisableKeyboardTargeting();
		UnbindBoard();
		foreach (var (position, handler) in _effectBarRebuiltHandlers)
		{
			if (_effectBars.TryGetValue(position, out var bar) && GodotObject.IsInstanceValid(bar))
				bar.OnIconsRebuilt -= handler;
		}
		_effectBarRebuiltHandlers.Clear();
		if (_fontScaleSource != null)
			_fontScaleSource.OnFontScaleChanged -= OnFontScaleChanged;
		_fontScaleSource = null;
	}

	/// <summary>界面字体倍率变化：刷新三线标题与全部槽位字号。</summary>
	private void OnFontScaleChanged()
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
			return;
		ApplyFontScale();
		QueueArenaSynchronization();
	}

	/// <summary>把三线标题与槽位文字统一接入界面字体缩放。</summary>
	public void ApplyFontScale()
	{
		foreach (var (_, title) in _rowTitles)
			title.AddThemeFontSizeOverride("font_size", UIScaler.ApplyFontScale(RowTitleBaseFontSize));
		foreach (var slot in _slots.Values)
			slot.ApplyFontScale();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueArenaSynchronization();
	}

	public void SetBoard(Board board)
	{
		ArgumentNullException.ThrowIfNull(board);
		if (!ReferenceEquals(_board, board))
			BindBoard(board);
		RefreshBoard();
	}

	private void BindBoard(Board board)
	{
		UnbindBoard();
		_board = board;
		_frontlineOwnerChanged = (_, _) =>
		{
			UpdateArenaOwner();
			QueueArenaSynchronization();
		};
		_board.OnFrontlineOwnerChanged += _frontlineOwnerChanged;
	}

	private void UnbindBoard()
	{
		if (_board != null && _frontlineOwnerChanged != null)
			_board.OnFrontlineOwnerChanged -= _frontlineOwnerChanged;
		_frontlineOwnerChanged = null;
		_pendingDeploy.Clear();
		_board = null;
	}

	public bool IsPlayerPosition(BoardPosition position) => _board?.GetMinionAt(position)?.IsPlayerSide
		?? (position.Row == BoardRow.PlayerSupport || position.Row == BoardRow.Frontline && _board?.FrontlineOwner != FrontlineOwner.Enemy);

	public void RefreshBoard()
	{
		foreach (var (row, title) in _rowTitles)
			title.Text = Loc.T($"board_row.{row.ToString().ToLowerInvariant()}", row.ToString());
		UpdateArenaOwner();
		QueueArenaSynchronization();
		if (_board == null)
			return;
		foreach (var (position, slot) in _slots)
		{
			RefreshSlot(position, slot);
		}
	}

	/// <summary>规则写入槽位时登记待揭幕单位；刷新仍画空槽，效果先在隐藏层准备。</summary>
	internal bool BeginPendingDeploy(BoardPosition position, Minion minion)
	{
		if (SceneLifecycleGuard.ShouldSkip(this) || !ReferenceEquals(_board?.GetMinionAt(position), minion)
			|| !_slots.TryGetValue(position, out BoardSlot? slot) || SceneLifecycleGuard.ShouldSkip(slot))
		{
			return false;
		}
		_pendingDeploy[position] = minion;
		RefreshSlot(position, slot);
		return true;
	}

	/// <summary>只有仍占据此槽的同一待揭幕单位可衔接触桌反馈。</summary>
	internal void RevealDeployedMinion(BoardPosition position, Minion minion, DeployImpactProfile impact, float speed)
	{
		if (!ClearPendingDeploy(position, minion) || minion.IsDead || !ReferenceEquals(_board?.GetMinionAt(position), minion))
		{
			return;
		}
		GetSlotView(position)?.PlayDeployImpact(impact, speed);
	}

	/// <summary>清除该实例所持的视觉占位并刷新实际内容；不能揭开后来占用同一槽位的牌。</summary>
	internal bool ClearPendingDeploy(BoardPosition position, Minion minion)
	{
		if (!_pendingDeploy.TryGetValue(position, out Minion? pending) || !ReferenceEquals(pending, minion))
		{
			return false;
		}
		_ = _pendingDeploy.Remove(position);
		if (SceneLifecycleGuard.ShouldSkip(this) || !_slots.TryGetValue(position, out BoardSlot? slot)
			|| SceneLifecycleGuard.ShouldSkip(slot))
		{
			return false;
		}
		RefreshSlot(position, slot);
		return true;
	}

	private void RefreshSlot(BoardPosition position, BoardSlot slot)
	{
		if (SceneLifecycleGuard.ShouldSkip(slot))
		{
			return;
		}
		Minion? minion = _board?.GetMinionAt(position);
		if (minion is { IsDead: true })
		{
			minion = null;
		}
		bool hidden = _pendingDeploy.TryGetValue(position, out Minion? pending) && ReferenceEquals(minion, pending);
		if (!hidden && pending != null)
		{
			_ = _pendingDeploy.Remove(position);
		}
		slot.UpdateDisplay(hidden ? null : minion);
		RefreshSlotDimming(slot);
		EffectBar bar = _effectBars[position];
		if (SceneLifecycleGuard.ShouldSkip(bar))
		{
			return;
		}
		if (hidden)
		{
			bar.SetPresentationSuppressed(true);
		}
		bar.Populate(minion?.GetDisplayableEffects() ?? (IReadOnlyList<DisplayableEffect>)Array.Empty<DisplayableEffect>());
		if (!hidden)
		{
			bar.SetPresentationSuppressed(false);
		}
		RepositionEffectBar(position);
	}

	/// <summary>把效果栏水平居中贴到对应槽位下沿；图标延迟重建后由 OnIconsRebuilt 再次调用。</summary>
	private void RepositionEffectBar(BoardPosition position)
	{
		if (!_effectBars.TryGetValue(position, out EffectBar? bar) || SceneLifecycleGuard.ShouldSkip(bar) || !bar.Visible)
		{
			return;
		}
		Rect2 rect = GetSlotScreenRect(position);
		if (!rect.HasArea())
		{
			return;
		}
		// Size.X 在容器布局前仍是旧值，合并最小宽度保证居中不漂移。
		float width = Mathf.Max(bar.Size.X, bar.GetCombinedMinimumSize().X);
		Vector2 anchor = bar.GetCanvasTransform().AffineInverse() * new Vector2(rect.GetCenter().X, rect.End.Y);
		bar.GlobalPosition = anchor - new Vector2(width * 0.5f, 0f);
	}

	private void UpdateArenaOwner()
	{
		if (!GodotObject.IsInstanceValid(_backdrop))
			return;
		FrontlineControlSide side = _board?.FrontlineOwner switch
		{
			FrontlineOwner.Player => FrontlineControlSide.Player,
			FrontlineOwner.Enemy => FrontlineControlSide.Enemy,
			_ => FrontlineControlSide.None,
		};
		_backdrop.SetOwner(side);
	}

	private void QueueArenaSynchronization()
	{
		if (_arenaSyncQueued || !IsInsideTree() || IsQueuedForDeletion())
			return;
		_arenaSyncQueued = true;
		SceneLifecycleGuard.CallDeferredSafe(this, nameof(SynchronizeArenaLayout));
	}

	/// <summary>布局完成后把三行实际矩形同步到底板；不依赖硬编码槽位尺寸。</summary>
	private void SynchronizeArenaLayout()
	{
		_arenaSyncQueued = false;
		if (!GodotObject.IsInstanceValid(_backdrop) || !_backdrop.IsInsideTree())
			return;
		if (!_rowLines.TryGetValue(BoardRow.EnemySupport, out HBoxContainer? enemyLine)
			|| !_rowLines.TryGetValue(BoardRow.Frontline, out HBoxContainer? frontlineLine)
			|| !_rowLines.TryGetValue(BoardRow.PlayerSupport, out HBoxContainer? playerLine))
			return;
		_backdrop.SetLayout(ToRowBackdropRect(enemyLine), ToRowBackdropRect(frontlineLine),
			ToRowBackdropRect(playerLine));
		foreach (BoardPosition position in _effectBars.Keys)
		{
			RepositionEffectBar(position);
		}
	}

	/// <summary>HBox 默认横向填满，取实际槽位并集才能得到支援线 4 格 / 前线 5 格真实宽度。</summary>
	private Rect2 ToRowBackdropRect(HBoxContainer line)
	{
		Rect2? union = null;
		foreach (Node child in line.GetChildren())
		{
			if (child is not Control slot || !slot.IsVisibleInTree())
				continue;
			Rect2 rect = ToBackdropRect(slot);
			union = union is Rect2 existing ? existing.Merge(rect) : rect;
		}
		return union ?? ToBackdropRect(line);
	}

	private Rect2 ToBackdropRect(Control control)
	{
		Transform2D transform = _backdrop.GetGlobalTransformWithCanvas().AffineInverse()
			* control.GetGlobalTransformWithCanvas();
		Vector2 origin = transform * Vector2.Zero;
		Vector2 end = transform * control.Size;
		return new Rect2(origin, end - origin);
	}

	public void HighlightPositions(IEnumerable<BoardPosition> positions, bool highlight = true)
	{
		foreach (var position in positions)
			GetSlotView(position)?.SetHighlighted(highlight);
	}

	public void HighlightMinions(IEnumerable<Minion> minions)
		=> HighlightPositions(minions.Where(m => m.BoardRow != null).Select(m => new BoardPosition(m.BoardRow!.Value, m.BoardSlotIndex)));

	public void HighlightSlots(List<int> indices, bool isPlayerSide, bool highlight)
		=> HighlightPositions(indices.Select(i => new BoardPosition(Board.SupportRow(isPlayerSide), i)), highlight);
	public void ClearHighlights()
	{
		foreach (var slot in _slots.Values)
			slot.SetHighlighted(false);
	}
	public void UpdateActionCostDimming(int availableMana)
	{
		_availableMana = availableMana;
		foreach (var slot in _slots.Values)
		{
			RefreshSlotDimming(slot);
		}
	}

	private void RefreshSlotDimming(BoardSlot slot)
	{
		slot.Modulate = slot.OccupyingMinion is { IsPlayerSide: true } minion && minion.ActionCost > _availableMana
			? new Color(0.5f, 0.5f, 0.5f) : Colors.White;
	}

	public BoardPosition? GetSlotAtPosition(Vector2 screenPos)
	{
		foreach (BoardPosition position in _slots.Keys)
		{
			if (GetSlotScreenRect(position).HasPoint(screenPos))
			{
				return position;
			}
		}
		return null;
	}
	public Vector2 GetSlotScreenCenter(BoardPosition position)
	{
		var rect = GetSlotScreenRect(position);
		return rect.Size == Vector2.Zero ? Vector2.Zero : rect.GetCenter();
	}
	/// <summary>槽位的稳定屏幕矩形；沉降只改内部外观，命中和飞行终点不跟着震。</summary>
	public Rect2 GetSlotScreenRect(BoardPosition position)
	{
		BoardSlot? slot = GetSlotView(position);
		if (slot == null || SceneLifecycleGuard.ShouldSkip(slot) || !slot.IsVisibleInTree())
		{
			return default;
		}
		Transform2D transform = slot.GetGlobalTransformWithCanvas();
		Vector2 origin = transform * Vector2.Zero;
		return new Rect2(origin, (transform * slot.Size) - origin);
	}
	public Vector2 GetSlotScreenCenter(int index, bool isPlayerSide) => GetSlotScreenCenter(new BoardPosition(Board.SupportRow(isPlayerSide), index));
	public Rect2 GetSlotScreenRect(int index, bool isPlayerSide) => GetSlotScreenRect(new BoardPosition(Board.SupportRow(isPlayerSide), index));
	public void SetSlotIntentText(BoardPosition position, string? text) => GetSlotView(position)?.SetIntentText(text);
	public void SetSlotIntentText(int index, bool isPlayerSide, string? text) => SetSlotIntentText(new BoardPosition(Board.SupportRow(isPlayerSide), index), text);

	public void EnableKeyboardTargeting(bool includePlayerSlots, bool includeEnemySlots)
	{
		DisableKeyboardTargeting();
		_includePlayer = includePlayerSlots;
		_includeEnemy = includeEnemySlots;
		_keyboardEnabled = true;
		var hm = HotkeyManager.Instance;
		if (hm == null)
			return;
		hm.PushPressedBinding(OdysseyInput.TabTarget, Next);
		hm.PushPressedBinding(OdysseyInput.Left, Left);
		hm.PushPressedBinding(OdysseyInput.Right, Right);
		hm.PushPressedBinding(OdysseyInput.Up, Up);
		hm.PushPressedBinding(OdysseyInput.Down, Down);
		hm.PushPressedBinding(OdysseyInput.Accept, Accept);
		hm.PushPressedBinding(OdysseyInput.Cancel, Cancel);
		Focus(ValidPositions().Cast<BoardPosition?>().FirstOrDefault());
	}
	public void DisableKeyboardTargeting()
	{
		if (!_keyboardEnabled)
			return;
		_keyboardEnabled = false;
		Focus(null);
		var hm = HotkeyManager.Instance;
		if (hm == null)
			return;
		hm.RemovePressedBinding(OdysseyInput.TabTarget, Next);
		hm.RemovePressedBinding(OdysseyInput.Left, Left);
		hm.RemovePressedBinding(OdysseyInput.Right, Right);
		hm.RemovePressedBinding(OdysseyInput.Up, Up);
		hm.RemovePressedBinding(OdysseyInput.Down, Down);
		hm.RemovePressedBinding(OdysseyInput.Accept, Accept);
		hm.RemovePressedBinding(OdysseyInput.Cancel, Cancel);
	}
	private IEnumerable<BoardPosition> ValidPositions() => _slots.Where(pair => pair.Value.IsHighlighted
		&& (IsPlayerPosition(pair.Key) ? _includePlayer || pair.Key.Row == BoardRow.Frontline : _includeEnemy)).Select(pair => pair.Key);
	private void Focus(BoardPosition? position)
	{
		if (_focused is BoardPosition old)
			GetSlotView(old)?.SetKeyboardFocused(false);
		_focused = position;
		if (position is BoardPosition current)
			GetSlotView(current)?.SetKeyboardFocused(true);
	}
	private void Next() => Navigate(1, vertical: false, sameRow: false);
	private void Left() => Navigate(-1, vertical: false, sameRow: true);
	private void Right() => Navigate(1, vertical: false, sameRow: true);
	private void Up() => Navigate(-1, vertical: true, sameRow: false);
	private void Down() => Navigate(1, vertical: true, sameRow: false);
	private void Navigate(int direction, bool vertical, bool sameRow)
	{
		var valid = ValidPositions().OrderBy(p => p.Row).ThenBy(p => p.SlotIndex).ToList();
		if (valid.Count == 0)
			return;
		if (_focused is not BoardPosition current)
		{
			Focus(valid[0]);
			return;
		}
		if (vertical)
		{
			for (int i = 1; i <= 3; i++)
			{
				var row = (BoardRow)(((int)current.Row + direction * i + 3) % 3);
				var next = valid.Where(p => p.Row == row).OrderBy(p => Math.Abs(p.SlotIndex - current.SlotIndex)).ToList();
				if (next.Count > 0)
				{
					Focus(next[0]);
					return;
				}
			}
			return;
		}
		if (sameRow)
			valid = valid.Where(p => p.Row == current.Row).ToList();
		if (valid.Count > 0)
			Focus(valid[(valid.IndexOf(current) + direction + valid.Count) % valid.Count]);
	}
	private void Accept()
	{
		if (_focused is BoardPosition position)
			OnSlotClicked?.Invoke(position);
	}
	private void Cancel() => OnSlotRightClicked?.Invoke(_focused ?? new BoardPosition(BoardRow.PlayerSupport, 0));

	/// <summary>可替换的单槽表现挂载点，保留 OccupyingMinion 与高亮接口。</summary>
	public partial class BoardSlot : PanelContainer
	{
		public BoardPosition PositionOnBoard { get; }
		public int SlotIndex => PositionOnBoard.SlotIndex;
		public bool IsPlayerSide => _parent?.IsPlayerPosition(PositionOnBoard) ?? false;
		public Minion? OccupyingMinion { get; private set; }
		public bool IsHighlighted { get; private set; }
		private readonly BoardUI? _parent;
		private readonly Label _content = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, ClipText = true, MouseFilter = MouseFilterEnum.Ignore };
		private readonly Label _intent = new() { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.Arbitrary, MaxLinesVisible = 2, ClipText = true, MouseFilter = MouseFilterEnum.Ignore };
		private readonly PanelContainer _surface = new() { Name = "SlotSurface", MouseFilter = MouseFilterEnum.Ignore };
		private readonly StyleBoxFlat _style = new() { BgColor = new Color("191826") };
		private bool _focused;
		private bool _hasDeployImpact;
		private float _impactElapsed;
		private float _impactSpeed;
		private DeployImpactProfile _impact;
		private Color _impactColor;
		private const int ContentBaseFontSize = 12;
		private const int IntentBaseFontSize = 11;
		private BoardSlot() { }
		public BoardSlot(BoardPosition position, BoardUI parent)
		{
			PositionOnBoard = position;
			_parent = parent;
			Name = $"Slot{position.SlotIndex}";
			CustomMinimumSize = new Vector2(110, 132);
			MouseFilter = MouseFilterEnum.Stop;
			AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
			// HBox 只布局外槽；无布局职责的壳让内层保持原 PanelContainer 边距和文字排版。
			var surfaceHost = new Control { MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true };
			AddChild(surfaceHost);
			surfaceHost.AddChild(_surface);
			_surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			_content.SizeFlagsVertical = SizeFlags.ExpandFill;
			_surface.AddChild(box);
			box.AddChild(_content);
			box.AddChild(_intent);
			ApplyFontScale();
			UpdateDisplay(null);
		}

		/// <summary>槽位随从名/攻血与意图文字跟随界面字体缩放。</summary>
		public void ApplyFontScale()
		{
			_content.AddThemeFontSizeOverride("font_size", UIScaler.ApplyFontScale(ContentBaseFontSize));
			_intent.AddThemeFontSizeOverride("font_size", UIScaler.ApplyFontScale(IntentBaseFontSize));
		}
		public void UpdateDisplay(Minion? minion)
		{
			if (!ReferenceEquals(OccupyingMinion, minion))
			{
				ResetDeployImpact();
			}
			OccupyingMinion = minion;
			string armorText = minion == null ? string.Empty : UiSymbolMode.Select(
				$"{Loc.T("ui.board.armor_prefix", "甲")}{minion.CurrentArmor}", $"🛡{minion.CurrentArmor}");
			string actionText = minion == null ? string.Empty : UiSymbolMode.Select(
				$"{Loc.T("ui.board.act_prefix", "行")}{minion.ActionCost}", $"◆{minion.ActionCost}");
			_content.Text = minion == null ? $"[{SlotIndex + 1}]\n{Loc.T("ui.combat.board_empty", "空")}"
				: $"{minion.GetLocalizedName()}\n{GetUnitClassName(minion.UnitClass)}\n{minion.Attack}/{minion.CurrentHealth}  {Loc.T("ui.board.defense_prefix", "防")}{minion.Defense}  {armorText}\n{actionText}  {Keywords(minion)}";
			if (minion == null)
				SetIntentText(null);
			ApplyStyle();
			QueueRedraw();
		}

		/// <summary>单次触桌压缩与阵营色反馈，不改变被容器管理的槽位本体。</summary>
		internal void PlayDeployImpact(DeployImpactProfile impact, float speed)
		{
			if (SceneLifecycleGuard.ShouldSkip(this) || OccupyingMinion is not Minion minion)
			{
				return;
			}
			_impact = impact;
			_impactColor = minion.IsPlayerSide ? UIThemeFactory.CyanGold : UIThemeFactory.Crimson;
			_impactElapsed = 0f;
			_impactSpeed = speed;
			_hasDeployImpact = true;
			ApplySettlement();
			SetProcess(true);
		}

		public override void _Ready()
		{
			SetProcess(_hasDeployImpact);
		}

		public override void _Process(double delta)
		{
			if (SceneLifecycleGuard.ShouldSkip(this) || SceneLifecycleGuard.ShouldSkip(_surface) || !_hasDeployImpact)
			{
				return;
			}
			_impactElapsed += (float)delta * _impactSpeed;
			if (_impactElapsed >= MinionDeployMath.ImpactDuration)
			{
				ResetDeployImpact();
				return;
			}
			ApplySettlement();
		}

		private void ApplySettlement()
		{
			DeploySettlement settlement = MinionDeployMath.SampleSettlement(_impactElapsed, _impact);
			_surface.PivotOffset = Size * 0.5f;
			_surface.Scale = new Vector2(settlement.Scale.X, settlement.Scale.Y);
			float direction = (SlotIndex & 1) == 0 ? -1f : 1f;
			_surface.Position = new Vector2(settlement.Offset.X * direction, settlement.Offset.Y);
			QueueRedraw();
		}

		private void ResetDeployImpact()
		{
			if (!_hasDeployImpact)
			{
				return;
			}
			_hasDeployImpact = false;
			_surface.Scale = Vector2.One;
			_surface.Position = Vector2.Zero;
			SetProcess(false);
			QueueRedraw();
		}

		/// <summary>在槽位左上绘制兵种徽章，右上绘制嘲讽/守护战术微标。</summary>
		public override void _Draw()
		{
			if (OccupyingMinion is not Minion minion || Size.X < 24f || Size.Y < 24f)
			{
				return;
			}
			if (_hasDeployImpact)
			{
				Vector2 scale = _surface.Scale;
				Vector2 origin = _surface.Position + ((Vector2.One - scale) * _surface.PivotOffset);
				DrawSetTransform(origin, 0f, scale);
			}
			DrawBadges(minion);
			if (_hasDeployImpact)
			{
				DrawSetTransform(Vector2.Zero);
				DrawDeployImpact();
			}
		}

		private void DrawBadges(Minion minion)
		{
			Color primary = minion.IsPlayerSide ? UIThemeFactory.CyanGold : UIThemeFactory.Crimson;
			Color outline = new(0.04f, 0.05f, 0.09f, 0.95f);
			TacticalIconDrawer.DrawUnitClass(this, GetTacticalUnitIcon(minion.UnitClass),
				new Vector2(19f, 19f), 22f, primary, outline);
			if (minion.HasTaunt)
			{
				TacticalIconDrawer.DrawTaunt(this, new Vector2(Size.X - 18f, 18f), 17f,
					UIThemeFactory.WarmGold, outline);
			}
			if (minion.HasGuard)
			{
				TacticalIconDrawer.DrawGuard(this, new Vector2(Size.X - 18f, minion.HasTaunt ? 38f : 18f), 17f,
					UIThemeFactory.CyanGold.Lerp(UIThemeFactory.WarmGold, 0.35f), outline);
			}
		}

		private void DrawDeployImpact()
		{
			float t = Mathf.Clamp(_impactElapsed / MinionDeployMath.ImpactDuration, 0f, 1f);
			float travel = MinionDeployMath.EaseOutCubic(t);
			float fade = (1f - t) * (1f - t);
			float rim = _impact.RingStrength * fade;
			DrawImpactOutline(new Rect2(Vector2.Zero, Size).Grow(1f + (9f * travel)), new Color(_impactColor, rim * 0.66f));
			DrawImpactOutline(new Rect2(Vector2.Zero, Size).Grow(-3f + (2f * travel)), new Color(_impactColor, rim * fade * 0.34f));

			// 两扇贴住底边向外散开的低矮尘流，叠细线与羽化光点，速度和高度逐粒错开。
			Color dust = _impactColor.Lerp(UIThemeFactory.WarmGold, 0.32f);
			for (int i = 0; i < 12; i++)
			{
				float side = (i & 1) == 0 ? -1f : 1f;
				float band = (i / 2) / 5f;
				float streak = 0.8f + (band * 2.7f);
				float edge = Size.X * (0.18f + (0.26f * band));
				float spread = travel * (16f + (24f * band));
				float x = (Size.X * 0.5f) + (side * (edge + spread));
				float y = Size.Y + 1.5f - (travel * (2f + ((i * 3) % 7)));
				var point = new Vector2(x, y);
				float alpha = _impact.DustStrength * fade * (0.36f - (band * 0.12f));
				DrawCircle(point, 1.9f + (band * 1.2f) + travel, new Color(dust, alpha * 0.22f), antialiased: true);
				DrawLine(point, point - new Vector2(side * streak, -0.45f), new Color(dust, alpha), 0.9f, antialiased: true);
			}
		}

		private void DrawImpactOutline(Rect2 rect, Color color)
		{
			const float corner = 4f;
			float l = rect.Position.X;
			float t = rect.Position.Y;
			float r = rect.End.X;
			float b = rect.End.Y;
			Span<Vector2> outline = stackalloc Vector2[]
			{
				new(l + corner, t), new(r - corner, t), new(r, t + corner), new(r, b - corner),
				new(r - corner, b), new(l + corner, b), new(l, b - corner), new(l, t + corner), new(l + corner, t),
			};
			DrawPolyline(outline, color, 1.25f, antialiased: true);
		}

		private static string Keywords(Minion minion)
		{
			var words = new List<string>();
			if (minion.HasTaunt)
				words.Add(Loc.T("keyword.taunt", "嘲讽"));
			if (minion.HasGuard)
				words.Add(Loc.T("keyword.guard", "守护"));
			if (minion.HasCharge)
				words.Add(Loc.T("keyword.charge", "闪击"));
			if (minion.HasAmbush)
				words.Add(Loc.T("keyword.ambush", "伏击"));
			if (minion.HasImpact)
				words.Add(Loc.T("keyword.impact", "冲击"));
			if (minion.HasRecycle)
				words.Add(Loc.T("keyword.recycle", "轮战"));
			return string.Join(" ", words);
		}
		public void SetIntentText(string? text)
		{
			string fullText = text ?? "";
			// 槽位固定 110px；意图文字过长会反向撑宽容器，显示截断并保留完整 tooltip。
			_intent.Text = fullText.Length > 8 ? fullText[..8] : fullText;
			_intent.TooltipText = fullText;
			_intent.Visible = OccupyingMinion != null && !string.IsNullOrEmpty(fullText);
		}
		public void SetHighlighted(bool value) { IsHighlighted = value; ApplyStyle(); }
		public void SetKeyboardFocused(bool value) { _focused = value; ApplyStyle(); }
		private void ApplyStyle()
		{
			var owner = _parent?._board?.FrontlineOwner ?? FrontlineOwner.None;
			var border = PositionOnBoard.Row == BoardRow.Frontline && owner == FrontlineOwner.None
				? new Color("777185") : IsPlayerSide ? new Color("7fd8ff") : new Color("ff6b7a");
			_style.BorderColor = _focused ? Colors.Gold : IsHighlighted ? Colors.LightGreen : border;
			_style.SetBorderWidthAll(IsHighlighted || _focused ? 3 : 1);
			_style.SetCornerRadiusAll(4);
			_surface.AddThemeStyleboxOverride("panel", _style);
		}
		public override void _GuiInput(InputEvent @event)
		{
			// 战斗表面统一主指针，避免同一次触摸与模拟鼠标双发。
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			{
				_parent?.OnSlotClicked?.Invoke(PositionOnBoard);
				AcceptEvent();
			}
			else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
			{
				_parent?.OnSlotRightClicked?.Invoke(PositionOnBoard);
				AcceptEvent();
			}
		}
	}
}
