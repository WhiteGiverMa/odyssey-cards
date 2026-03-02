using Godot;
using OdysseyCards.Map;

namespace OdysseyCards.UI
{
	public partial class MapEdgeUI : Control
	{
		private MapEdge _edge;
		private Line2D _line;

		public MapEdge Edge => _edge;

		public MapEdgeUI()
		{
			_line = new Line2D();
			_line.Width = 2.0f;
			_line.DefaultColor = new Color(0.5f, 0.5f, 0.5f);
			AddChild(_line);

			MouseFilter = MouseFilterEnum.Ignore;
		}

		public override void _Ready()
		{
			if (_line == null)
			{
				_line = new Line2D();
				_line.Width = 2.0f;
				_line.DefaultColor = new Color(0.5f, 0.5f, 0.5f);
				AddChild(_line);
			}

			MouseFilter = MouseFilterEnum.Ignore;
		}

		public void SetEdge(MapEdge edge, Vector2 fromPos, Vector2 toPos)
		{
			_edge = edge;
			UpdateLine(fromPos, toPos);
		}

		public void UpdateLine(Vector2 fromPos, Vector2 toPos)
		{
			_line.ClearPoints();
			_line.AddPoint(fromPos);
			_line.AddPoint(toPos);
		}

		public void SetHighlight(bool highlight)
		{
			_line.DefaultColor = highlight ? Colors.Yellow : new Color(0.5f, 0.5f, 0.5f);
			_line.Width = highlight ? 3.0f : 2.0f;
		}
	}
}
