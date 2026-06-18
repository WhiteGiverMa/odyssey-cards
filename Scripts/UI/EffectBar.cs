using Godot;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using OdysseyCards.Localization;
using System;
using System.Collections.Generic;

namespace OdysseyCards.UI;

/// <summary>
/// 效果图标栏——横向排列的 Emoji 图标 + 层数标签。
/// 位于英雄/随从的 HP 条下方，支持 hover tooltip。
/// Buff 使用绿色色调，Debuff 使用红色色调。
/// </summary>
public partial class EffectBar : HBoxContainer
{
	private const int IconCellSize = 22;
	private const int IconFontSize = 13;
	private const int StackFontSize = 9;
	private const int Spacing = 2;
	private const float BuffAlpha = 0.85f;
	private const float DebuffAlpha = 0.85f;

	private static readonly Color BuffBgColor = new(0.15f, 0.45f, 0.15f, BuffAlpha);
	private static readonly Color DebuffBgColor = new(0.45f, 0.12f, 0.12f, DebuffAlpha);
	private static readonly Color BuffBorderColor = new(0.25f, 0.7f, 0.25f, 0.8f);
	private static readonly Color DebuffBorderColor = new(0.7f, 0.2f, 0.2f, 0.8f);
	private static readonly Color StackTextColor = new(1f, 1f, 1f, 0.95f);

	private IReadOnlyList<DisplayableEffect> _effects = Array.Empty<DisplayableEffect>();
	private DisplayableEffect[] _pendingEffects = Array.Empty<DisplayableEffect>();
	private EffectTooltip? _activeTooltip;
	private string _effectSignature = string.Empty;
	private bool _rebuildQueued;

	public EffectBar()
	{
		Alignment = AlignmentMode.Center;
		AddThemeConstantOverride("separation", Spacing);
	}

	/// <summary>
	/// 填充效果列表，安全重建所有图标。
	/// </summary>
	public void Populate(IReadOnlyList<DisplayableEffect> effects)
	{
		var sorted = new List<DisplayableEffect>(effects);
		sorted.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

		string signature = BuildSignature(sorted);
		if (signature == _effectSignature && !_rebuildQueued)
		{
			return;
		}

		_effects = sorted;
		_pendingEffects = sorted.ToArray();
		_effectSignature = signature;

		HideTooltip();
		QueueExistingIconsForRemoval();

		if (_pendingEffects.Length == 0)
		{
			Visible = false;
			return;
		}

		Visible = true;
		QueueIconRebuild();
	}

	/// <summary>
	/// 清空所有图标并隐藏。
	/// </summary>
	public void Clear()
	{
		_effects = Array.Empty<DisplayableEffect>();
		_pendingEffects = Array.Empty<DisplayableEffect>();
		_effectSignature = string.Empty;
		HideTooltip();
		QueueExistingIconsForRemoval();
		Visible = false;
	}

	/// <summary>
	/// 显示 tooltip 弹窗。
	/// </summary>
	internal void ShowTooltip(DisplayableEffect effect, Vector2 screenPos)
	{
		if (!CanMutateTooltip())
		{
			return;
		}

		HideTooltip();

		_activeTooltip = new EffectTooltip(effect);
		var root = GetTree().Root;
		root.AddChild(_activeTooltip);

		int estimatedW = 200;
		int estimatedH = string.IsNullOrEmpty(effect.Description) ? 40 : 70;

		int posX = (int)Mathf.Clamp(screenPos.X + 8, 4, root.Size.X - estimatedW - 8);
		int posY = (int)Mathf.Clamp(screenPos.Y - estimatedH - 8, 4, root.Size.Y - estimatedH - 8);

		_activeTooltip.Popup(new Rect2I(posX, posY, estimatedW, estimatedH));
	}

	/// <summary>
	/// 隐藏 tooltip。
	/// Hide() 立即执行以从 Godot GUI 系统注销；
	/// QueueFree 延迟到帧末，避免与 Godot 原生 PopupPanel 状态清理竞态 → SIG11。
	/// </summary>
	internal void HideTooltip()
	{
		if (_activeTooltip != null && GodotObject.IsInstanceValid(_activeTooltip))
		{
			_activeTooltip.Hide();
			_activeTooltip.CallDeferred("queue_free");
		}

		_activeTooltip = null;
	}

	private bool CanMutateTooltip()
	{
		return IsInsideTree() && !IsQueuedForDeletion() && GetTree() != null;
	}

	private static string BuildSignature(IReadOnlyList<DisplayableEffect> effects)
	{
		if (effects.Count == 0)
		{
			return string.Empty;
		}

		var parts = new string[effects.Count];
		for (int i = 0; i < effects.Count; i++)
		{
			var effect = effects[i];
			parts[i] = $"{effect.SortOrder}:{effect.Category}:{effect.SourceId}:{effect.Name}:{effect.Icon}:{effect.Stacks}:{effect.IsBuff}:{effect.Description}";
		}

		return string.Join("|", parts);
	}

	private void QueueExistingIconsForRemoval()
	{
		foreach (var child in GetChildren())
		{
			if (child is not EffectIcon icon)
			{
				continue;
			}

			if (icon.IsQueuedForDeletion())
			{
				continue;
			}

			icon.Disable();
			icon.Visible = false;
			RemoveChild(icon);
			icon.QueueFree();
		}
	}

	private void QueueIconRebuild()
	{
		if (_rebuildQueued)
		{
			return;
		}

		_rebuildQueued = true;
		CallDeferred(nameof(WaitForIconFreeDeferred));
	}

	private void WaitForIconFreeDeferred()
	{
		if (!IsInsideTree() || IsQueuedForDeletion())
		{
			_rebuildQueued = false;
			return;
		}

		CallDeferred(nameof(RebuildIconsDeferred));
	}

	private void RebuildIconsDeferred()
	{
		_rebuildQueued = false;

		if (!IsInsideTree() || IsQueuedForDeletion())
		{
			return;
		}

		if (_pendingEffects.Length == 0)
		{
			Visible = false;
			return;
		}

		foreach (var child in GetChildren())
		{
			if (child is EffectIcon icon && !icon.IsQueuedForDeletion())
			{
			icon.Disable();
				icon.Visible = false;
				RemoveChild(icon);
				icon.QueueFree();
				QueueIconRebuild();
				return;
			}
		}

		Visible = true;

		foreach (var effect in _pendingEffects)
		{
			AddChild(new EffectIcon(effect, this));
		}
	}

	// ==================================================================
	// EffectIcon —— 单个效果图标（嵌套类）
	// ==================================================================

	private partial class EffectIcon : Control
	{
		private readonly DisplayableEffect _effect;
		private readonly EffectBar _parent;
		private readonly ColorRect _background;
		private readonly Label _iconLabel;
		private readonly Label? _stackLabel;
		private bool _isHovered;
		private bool _disabled;

		// Godot C# 桥接层要求的无参构造——仅编辑器/反射使用
		private EffectIcon()
		{
			_effect = default;
			_parent = null!;
			_background = null!;
			_iconLabel = null!;
			_stackLabel = null;
		}

		public EffectIcon(DisplayableEffect effect, EffectBar parent)
		{
			_effect = effect;
			_parent = parent;

			CustomMinimumSize = new Vector2(IconCellSize, IconCellSize);
			MouseFilter = MouseFilterEnum.Stop;

			bool isBuff = effect.IsBuff;

			var borderStyle = new StyleBoxFlat
			{
				BgColor = isBuff ? BuffBorderColor : DebuffBorderColor,
				CornerRadiusTopLeft = 3,
				CornerRadiusTopRight = 3,
				CornerRadiusBottomLeft = 3,
				CornerRadiusBottomRight = 3,
			};
			var borderRect = new ColorRect
			{
				AnchorsPreset = (int)LayoutPreset.FullRect,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			borderRect.AddThemeStyleboxOverride("panel", borderStyle);
			AddChild(borderRect);

			_background = new ColorRect
			{
				Position = new Vector2(1, 1),
				Size = new Vector2(IconCellSize - 2, IconCellSize - 2),
			};
			var bgStyle = new StyleBoxFlat
			{
				BgColor = isBuff ? BuffBgColor : DebuffBgColor,
				CornerRadiusTopLeft = 2,
				CornerRadiusTopRight = 2,
				CornerRadiusBottomLeft = 2,
				CornerRadiusBottomRight = 2,
			};
			_background.AddThemeStyleboxOverride("panel", bgStyle);
			_background.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(_background);

			_iconLabel = new Label
			{
				Text = effect.Icon,
				Position = new Vector2(2, 1),
				Size = new Vector2(IconCellSize - 4, IconCellSize - 4),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
			};
			_iconLabel.AddThemeFontSizeOverride("font_size", IconFontSize);
			_iconLabel.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(_iconLabel);

			if (effect.Stacks > 0)
			{
				_stackLabel = new Label
				{
					Text = effect.Stacks.ToString(),
					Position = new Vector2(IconCellSize - 12, IconCellSize - 11),
					Size = new Vector2(10, 10),
					HorizontalAlignment = HorizontalAlignment.Right,
					VerticalAlignment = VerticalAlignment.Bottom,
				};
				_stackLabel.AddThemeColorOverride("font_color", StackTextColor);
				_stackLabel.AddThemeFontSizeOverride("font_size", StackFontSize);
				_stackLabel.MouseFilter = MouseFilterEnum.Ignore;
				AddChild(_stackLabel);
			}
		}

		public override void _GuiInput(InputEvent @event)
		{
			// 移动端：点击切换 tooltip 显示（无 hover）
			if (MobileInputHelper.IsMobile && @event is InputEventScreenTouch st && st.Pressed)
			{
				if (_isHovered)
				{
					OnMouseExit();
				}
				else
				{
					OnMouseEnter();
				}
				AcceptEvent();
				return;
			}

			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
			{
				// 不处理点击，仅用于确认事件可达
			}
		}

		public override void _Notification(int what)
		{
			if (_disabled || !IsInsideTree() || IsQueuedForDeletion())
			{
				return;
			}

			if (what == NotificationMouseEnter)
			{
				OnMouseEnter();
			}
			else if (what == NotificationMouseExit)
			{
				OnMouseExit();
			}
		}

		public void Disable()
		{
			_disabled = true;
			MouseFilter = MouseFilterEnum.Ignore;
			Scale = Vector2.One;

			if (GodotObject.IsInstanceValid(_parent) && _parent.IsInsideTree() && !_parent.IsQueuedForDeletion())
			{
				_parent.HideTooltip();
			}
		}

		private bool CanHandleHover()
		{
			return !_disabled
				&& IsInsideTree()
				&& !IsQueuedForDeletion()
				&& GodotObject.IsInstanceValid(_parent)
				&& _parent.IsInsideTree()
				&& !_parent.IsQueuedForDeletion();
		}

		private void OnMouseEnter()
		{
			if (!CanHandleHover())
			{
				return;
			}

			_isHovered = true;
			Scale = new Vector2(1.15f, 1.15f);
			// 延迟到帧末执行 ShowTooltip，避免在 Godot GUI 事件处理期间
			// 调用 Popup()/Hide() 导致原生控件树迭代重入 → SIG11
			CallDeferred(nameof(OnMouseEnterDeferred));
		}

		private void OnMouseEnterDeferred()
		{
			if (!CanHandleHover() || !_isHovered)
			{
				return;
			}

			_parent.ShowTooltip(_effect, GetGlobalMousePosition());
		}

		private void OnMouseExit()
		{
			if (!CanHandleHover())
			{
				return;
			}

			_isHovered = false;
			Scale = Vector2.One;
			// 同上——延迟到帧末
			CallDeferred(nameof(OnMouseExitDeferred));
		}

		private void OnMouseExitDeferred()
		{
			_parent.HideTooltip();
		}
	}

	// ==================================================================
	// EffectTooltip —— Hover 提示弹窗（嵌套类）
	// ==================================================================

	private partial class EffectTooltip : PopupPanel
	{
		public EffectTooltip(DisplayableEffect effect)
		{
			var style = new StyleBoxFlat
			{
				BgColor = new Color(0.08f, 0.08f, 0.1f, 0.92f),
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				BorderWidthTop = 1,
				BorderWidthBottom = 1,
				BorderColor = effect.IsBuff ? BuffBorderColor : DebuffBorderColor,
				CornerRadiusTopLeft = 4,
				CornerRadiusTopRight = 4,
				CornerRadiusBottomLeft = 4,
				CornerRadiusBottomRight = 4,
			};
			AddThemeStyleboxOverride("panel", style);

			var vbox = new VBoxContainer { Name = "TooltipContent" };
			AddChild(vbox);

			var titleRow = new HBoxContainer();
			var iconLabel = new Label
			{
				Text = effect.Icon,
			};
			iconLabel.AddThemeFontSizeOverride("font_size", 12);
			titleRow.AddChild(iconLabel);

			var nameLabel = new Label
			{
				Text = effect.Stacks > 0
					? $"{effect.Name} ×{effect.Stacks}"
					: effect.Name,
			};
			nameLabel.AddThemeColorOverride("font_color", effect.IsBuff
				? new Color(0.5f, 1f, 0.5f)
				: new Color(1f, 0.5f, 0.5f));
			nameLabel.AddThemeFontSizeOverride("font_size", 13);
			titleRow.AddChild(nameLabel);
			vbox.AddChild(titleRow);

			if (!string.IsNullOrEmpty(effect.Description))
			{
				var descLabel = new Label
				{
					Text = effect.Description,
					CustomMinimumSize = new Vector2(160, 0),
					AutowrapMode = TextServer.AutowrapMode.Word,
				};
				descLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.8f));
				descLabel.AddThemeFontSizeOverride("font_size", 10);
				vbox.AddChild(descLabel);
			}
		}
	}
}
