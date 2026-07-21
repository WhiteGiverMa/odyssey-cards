#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Roguelike;

namespace OdysseyCards.UI;

/// <summary>
/// 地图路径可视化——层间节点贝塞尔连线 + 房间类型几何符号 + 当前位置脉动高亮。
/// 纯 _Draw 渲染：节点不是子控件；布局在 Refresh/Resized 时同步预算，_Draw 纯画。
/// 布局：第 1 层在底部、Boss 层在顶部（爬塔隐喻）。
/// 交互：仅当前层节点可激活，桌面端 _GuiInput 点击；移动端走下方房间卡片（避免双触控路径）。
/// </summary>
public partial class MapPathView : Control
{
	/// <summary>当前层节点被点击（仅当前层节点可激活）。</summary>
	public event Action<RoomDefinition>? OnCurrentRoomActivated;

	private const float NodeRadius = 21f;
	private const float VerticalMargin = 34f;

	private GameRunState? _run;

	/// <summary>预算节点（房间 / 中心点 / 是否当前层）。</summary>
	private readonly List<(RoomDefinition Room, Vector2 Center, bool IsCurrentLayer)> _nodes = new();

	/// <summary>预算连线（起终点 / 是否已走过）。</summary>
	private readonly List<(Vector2 From, Vector2 To, bool IsTraveled)> _links = new();

	public MapPathView()
	{
		MouseFilter = MouseFilterEnum.Stop;
		ClipContents = true;
	}

	public override void _Ready()
	{
		Resized += OnViewResized;
	}

	public override void _ExitTree()
	{
		Resized -= OnViewResized;
	}

	/// <summary>绑定运行状态并重绘。每次地图刷新（换层/完成房间）时调用。</summary>
	public void Refresh(GameRunState run)
	{
		_run = run;
		RebuildLayout();
		QueueRedraw();
	}

	private void OnViewResized()
	{
		RebuildLayout();
		QueueRedraw();
	}

	// ===== 布局预算（不依赖 _Draw，Refresh 后立即可命中测试） =====

	private void RebuildLayout()
	{
		_nodes.Clear();
		_links.Clear();
		if (_run?.CurrentPlane == null || Size.X < 40f || Size.Y < 40f)
		{
			return;
		}

		List<PlaneLayer> layers = _run.CurrentPlane.Layers;
		int current = _run.CurrentLayerIndex;
		bool runActive = !_run.IsPlaneComplete && !_run.IsRunComplete;

		// 连线：相邻层全连接（当前结构每层任选其一，全部皆为可能路径）
		for (int l = 0; l < layers.Count - 1; l++)
		{
			List<RoomDefinition> from = layers[l].Choices;
			List<RoomDefinition> to = layers[l + 1].Choices;
			for (int a = 0; a < from.Count; a++)
			{
				for (int b = 0; b < to.Count; b++)
				{
					_links.Add((
						GetNodeCenter(l, a, from.Count, layers.Count),
						GetNodeCenter(l + 1, b, to.Count, layers.Count),
						IsTraveled: l < current));
				}
			}
		}

		// 节点
		for (int l = 0; l < layers.Count; l++)
		{
			List<RoomDefinition> choices = layers[l].Choices;
			for (int c = 0; c < choices.Count; c++)
			{
				bool isCurrent = runActive && l == current;
				_nodes.Add((choices[c], GetNodeCenter(l, c, choices.Count, layers.Count), isCurrent));
			}
		}
	}

	/// <summary>层索引 → 节点中心。第 0 层在底部，逐层向上爬升。</summary>
	private Vector2 GetNodeCenter(int layerIndex, int choiceIndex, int choiceCount, int totalLayers)
	{
		float usable = Size.Y - VerticalMargin * 2f;
		float t = totalLayers <= 1 ? 0f : (float)layerIndex / (totalLayers - 1);
		float y = Size.Y - VerticalMargin - t * usable;
		float x = choiceCount == 1
			? Size.X * 0.5f
			: (choiceIndex == 0 ? Size.X * 0.36f : Size.X * 0.64f);
		return new Vector2(x, y);
	}

	// ===== 命中测试 =====

	/// <summary>命中测试——返回命中的当前层房间（仅当前层可交互）。</summary>
	private RoomDefinition? HitTest(Vector2 localPos)
	{
		foreach ((RoomDefinition room, Vector2 center, bool isCurrent) in _nodes)
		{
			if (!isCurrent)
			{
				continue;
			}

			if (localPos.DistanceTo(center) <= NodeRadius + 10f)
			{
				return room;
			}
		}
		return null;
	}

	public override void _GuiInput(InputEvent @event)
	{
		// 移动端触控走下方房间卡片 TapZone（同一动作单一触控路径），此处仅响应桌面鼠标
		if (Infrastructure.MobileInputRouter.IsMobile)
		{
			return;
		}

		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
		{
			RoomDefinition? room = HitTest(mb.Position);
			if (room != null)
			{
				OnCurrentRoomActivated?.Invoke(room);
				AcceptEvent();
			}
		}
	}

	// ===== 绘制 =====

	public override void _Process(double delta)
	{
		// 当前层脉动光环需要持续重绘
		QueueRedraw();
	}

	public override void _Draw()
	{
		// 连线先画（节点盖在上面）
		foreach ((Vector2 from, Vector2 to, bool traveled) in _links)
		{
			Color color = traveled
				? new Color(1f, 0.85f, 0.55f, 0.5f)   // 已走过：暖金
				: new Color(0.5f, 0.6f, 0.75f, 0.2f); // 未来：青灰细线
			DrawCubicBezier(from, to, color, traveled ? 2.5f : 1.5f);
		}

		foreach ((RoomDefinition room, Vector2 center, bool isCurrent) in _nodes)
		{
			DrawNode(center, room, isCurrent);
		}
	}

	/// <summary>三次贝塞尔：垂直中点控制点，让层间连线呈柔和 S 形。</summary>
	private void DrawCubicBezier(Vector2 p0, Vector2 p1, Color color, float width)
	{
		var c0 = new Vector2(p0.X, (p0.Y + p1.Y) / 2f);
		var c1 = new Vector2(p1.X, (p0.Y + p1.Y) / 2f);
		const int steps = 14;
		Vector2 prev = p0;
		for (int i = 1; i <= steps; i++)
		{
			float t = i / (float)steps;
			float u = 1f - t;
			Vector2 p = u * u * u * p0 + 3f * u * u * t * c0 + 3f * u * t * t * c1 + t * t * t * p1;
			DrawLine(prev, p, color, width, true);
			prev = p;
		}
	}

	private void DrawNode(Vector2 center, RoomDefinition room, bool isCurrent)
	{
		// 状态着色
		Color bg = new(0.11f, 0.09f, 0.19f, 0.92f);
		Color edge;
		Color symbol;
		float edgeWidth = 1.5f;

		if (room.IsCompleted)
		{
			edge = new Color(1f, 0.85f, 0.55f, 0.95f); // 暖金：已完成
			symbol = edge;
		}
		else if (isCurrent)
		{
			edge = new Color(1f, 0.62f, 0.82f, 1f); // 星粉：当前可选
			symbol = Colors.White;
			edgeWidth = 2.5f;
		}
		else if (_run != null && IsFutureRoom(room))
		{
			edge = new Color(0.5f, 0.85f, 1f, 0.55f); // 青金淡：未来层
			symbol = new Color(0.75f, 0.85f, 0.95f, 0.8f);
		}
		else
		{
			// 过去层中未被选择的分支——压暗
			bg = new Color(0.08f, 0.07f, 0.13f, 0.7f);
			edge = new Color(0.4f, 0.4f, 0.5f, 0.35f);
			symbol = new Color(0.5f, 0.5f, 0.6f, 0.4f);
		}

		// 当前层脉动光环（全局时钟，与棋盘呼吸同一节律来源）
		if (isCurrent)
		{
			float pulse = 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / 380f);
			DrawCircle(center, NodeRadius + 7f + pulse * 3f, new Color(1f, 0.62f, 0.82f, 0.08f + 0.1f * pulse));
			DrawArc(center, NodeRadius + 4f, 0, Mathf.Tau, 40, new Color(1f, 0.62f, 0.82f, 0.3f + 0.45f * pulse), 2f, true);
		}

		// 底盘 + 描边
		DrawCircle(center, NodeRadius, bg);
		DrawArc(center, NodeRadius, 0, Mathf.Tau, 40, edge, edgeWidth, true);

		// 房间类型几何符号
		DrawRoomSymbol(center, room.Type, symbol, NodeRadius * 0.52f);

		// 已完成：右上角打勾徽章
		if (room.IsCompleted)
		{
			Vector2 badge = center + new Vector2(NodeRadius * 0.66f, -NodeRadius * 0.66f);
			DrawCircle(badge, 6.5f, new Color(0.11f, 0.09f, 0.19f, 0.95f));
			DrawArc(badge, 6.5f, 0, Mathf.Tau, 20, edge, 1f, true);
			DrawLine(badge + new Vector2(-3f, 0.2f), badge + new Vector2(-0.8f, 2.4f), edge, 1.6f, true);
			DrawLine(badge + new Vector2(-0.8f, 2.4f), badge + new Vector2(3.2f, -2.6f), edge, 1.6f, true);
		}
	}

	/// <summary>判断房间是否属于未来层（在当前层之后）。</summary>
	private bool IsFutureRoom(RoomDefinition room)
	{
		if (_run?.CurrentPlane == null)
		{
			return false;
		}

		List<PlaneLayer> layers = _run.CurrentPlane.Layers;
		int current = _run.CurrentLayerIndex;
		for (int l = current + 1; l < layers.Count; l++)
		{
			if (layers[l].Choices.Contains(room))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>房间类型几何符号——战斗三角剑 / 精英菱形 / Boss 五角星 / 奖励宝箱 / 商店钱币 / 休息篝火 / 事件四角星。</summary>
	private void DrawRoomSymbol(Vector2 center, RoomType type, Color color, float s)
	{
		switch (type)
		{
			case RoomType.Monster:
			{
				// 剑：向上三角剑尖 + 底部横杠
				var blade = new Vector2[]
				{
					center + new Vector2(0, -s),
					center + new Vector2(s * 0.55f, s * 0.25f),
					center + new Vector2(-s * 0.55f, s * 0.25f),
				};
				DrawColoredPolygon(blade, color);
				DrawLine(center + new Vector2(-s * 0.7f, s * 0.6f), center + new Vector2(s * 0.7f, s * 0.6f), color, 2f, true);
				break;
			}
			case RoomType.Elite:
			{
				// 菱形 + 中心点
				var diamond = new Vector2[]
				{
					center + new Vector2(0, -s),
					center + new Vector2(s * 0.75f, 0),
					center + new Vector2(0, s),
					center + new Vector2(-s * 0.75f, 0),
				};
				DrawColoredPolygon(diamond, color);
				DrawCircle(center, s * 0.2f, new Color(0.11f, 0.09f, 0.19f, 1f));
				break;
			}
			case RoomType.Boss:
			{
				// 五角星
				var points = new Vector2[10];
				for (int i = 0; i < 10; i++)
				{
					float r = i % 2 == 0 ? s : s * 0.45f;
					float a = -Mathf.Pi / 2f + i * Mathf.Pi / 5f;
					points[i] = center + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
				}
				DrawColoredPolygon(points, color);
				break;
			}
			case RoomType.Treasure:
			{
				// 宝箱：方形身 + 顶盖线
				var body = new Rect2(center - new Vector2(s * 0.8f, s * 0.45f), new Vector2(s * 1.6f, s * 1.05f));
				DrawRect(body, color);
				DrawLine(center + new Vector2(-s * 0.8f, -s * 0.45f), center + new Vector2(s * 0.8f, -s * 0.45f), color, 2.5f, true);
				DrawCircle(center + new Vector2(0, s * 0.1f), s * 0.14f, new Color(0.11f, 0.09f, 0.19f, 1f));
				break;
			}
			case RoomType.Shop:
			{
				// 钱币：圆 + 内横线
				DrawArc(center, s * 0.75f, 0, Mathf.Tau, 24, color, 2.2f, true);
				DrawLine(center + new Vector2(-s * 0.42f, 0), center + new Vector2(s * 0.42f, 0), color, 2f, true);
				break;
			}
			case RoomType.RestSite:
			{
				// 篝火：火焰三角 + 底部柴堆交叉线
				var flame = new Vector2[]
				{
					center + new Vector2(0, -s),
					center + new Vector2(s * 0.6f, s * 0.15f),
					center + new Vector2(-s * 0.6f, s * 0.15f),
				};
				DrawColoredPolygon(flame, color);
				DrawLine(center + new Vector2(-s * 0.65f, s * 0.75f), center + new Vector2(s * 0.65f, s * 0.4f), color, 2f, true);
				DrawLine(center + new Vector2(s * 0.65f, s * 0.75f), center + new Vector2(-s * 0.65f, s * 0.4f), color, 2f, true);
				break;
			}
			case RoomType.Event:
			default:
			{
				// 事件：四角星（未知与闪光）
				float w = s * 0.3f;
				var star = new Vector2[]
				{
					center + new Vector2(0, -s),
					center + new Vector2(w, -w),
					center + new Vector2(s, 0),
					center + new Vector2(w, w),
					center + new Vector2(0, s),
					center + new Vector2(-w, w),
					center + new Vector2(-s, 0),
					center + new Vector2(-w, -w),
				};
				DrawColoredPolygon(star, color);
				break;
			}
		}
	}
}
