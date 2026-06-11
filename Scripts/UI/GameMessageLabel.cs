using System.Collections.Generic;
using Godot;

namespace OdysseyCards.UI
{
	public partial class GameMessageLabel : Control
	{
		private Label _label;
		private ColorRect _background;
		private Tween _fadeTween;
		private readonly Queue<(string message, float duration, bool isError)> _messageQueue = new();
		private bool _isDisplaying;

		public static GameMessageLabel Instance { get; private set; }

		public override void _Ready()
		{
			Instance = this;
			MouseFilter = MouseFilterEnum.Ignore;

			_background = new ColorRect
			{
				Color = new Color(0, 0, 0, 0.6f),
				MouseFilter = MouseFilterEnum.Ignore
			};
			AddChild(_background);

			_label = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = MouseFilterEnum.Ignore
			};

			string fontPath = "res://Assets/Fonts/NotoSansSC-Regular.ttf";
			LabelSettings settings = new()
			{
				FontSize = 20,
				FontColor = Colors.White
			};
			if (ResourceLoader.Exists(fontPath))
			{
				FontFile fontFile = GD.Load<FontFile>(fontPath);
				if (fontFile != null)
				{
					settings.Font = fontFile;
				}
			}
			_label.LabelSettings = settings;

			AddChild(_label);

			SetAnchorsPreset(LayoutPreset.TopLeft);
			OffsetLeft = 20;
			OffsetTop = 20;
			CustomMinimumSize = new Vector2(300, 40);
			_background.SetAnchorsPreset(LayoutPreset.FullRect);

			Visible = false;
		}

		public void ShowMessage(string message, float duration = 2.0f, bool isError = false)
		{
			_messageQueue.Enqueue((message, duration, isError));

			if (!_isDisplaying)
			{
				ProcessQueue();
			}
		}

		private void ProcessQueue()
		{
			if (_messageQueue.Count == 0)
			{
				_isDisplaying = false;
				Visible = false;
				return;
			}

			_isDisplaying = true;
			(string message, float duration, bool isError) = _messageQueue.Dequeue();
			DisplayMessage(message, duration, isError);
		}

		private void DisplayMessage(string message, float duration, bool isError)
		{
			_fadeTween?.Kill();

			_label.Text = message;
			if (_label.LabelSettings != null)
			{
				_label.LabelSettings.FontColor = isError ? new Color(1, 0.3f, 0.3f) : Colors.White;
			}

			Modulate = new Color(1, 1, 1, 1);
			Visible = true;

			float width = 300;
			float height = 40;

			if (_label.LabelSettings?.Font != null)
			{
				Vector2 textSize = _label.LabelSettings.Font.GetStringSize(
					message,
					fontSize: _label.LabelSettings.FontSize
				);
				width = Mathf.Min(textSize.X + 40, 400);
				height = Mathf.Max(textSize.Y + 20, 40);
			}

			CustomMinimumSize = new Vector2(width, height);
			_background.CustomMinimumSize = CustomMinimumSize;

			_fadeTween = CreateTween();
			_ = _fadeTween.TweenInterval(duration);
			_ = _fadeTween.TweenProperty(this, "modulate:a", 0.0f, 0.5f);
			_ = _fadeTween.TweenCallback(Callable.From(ProcessQueue));
		}

		public void ShowSuccess(string message)
		{
			ShowMessage(message, 2.0f, false);
		}

		public void ShowError(string message)
		{
			ShowMessage(message, 2.5f, true);
		}

		public override void _ExitTree()
		{
			if (Instance == this)
			{
				Instance = null;
			}
		}
	}
}
