using Godot;

namespace OdysseyCards.UI
{
    public partial class CardAnimation : Node
    {
        public static CardAnimation Instance { get; private set; }

        private const float _defaultShowcaseDuration = 0.5f;
        private const float _defaultDrawDuration = 0.3f;
        private const float _defaultDeployDuration = 0.3f;
        private const float _defaultReturnDuration = 0.2f;

        public override void _Ready()
        {
            Instance = this;
        }

        public async void PlayCardShowcase(CardUI card, Vector2 showcasePosition, Vector2 endPosition, float duration = _defaultShowcaseDuration)
        {
            if (card == null)
            {
                return;
            }

            Vector2 originalScale = card.Scale;
            Vector2 showcaseScale = new(1.2f, 1.2f);

            card.GlobalPosition = showcasePosition;
            card.Scale = showcaseScale;

            _ = await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);

            Tween tween = CreateTween();
            _ = tween.SetParallel(true);
            _ = tween.TweenProperty(card, "global_position", endPosition, 0.3f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
            _ = tween.TweenProperty(card, "scale", originalScale, 0.3f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);

            _ = await ToSignal(tween, Tween.SignalName.Finished);
        }

        public async void PlayDrawCard(CardUI card, Vector2 fromPosition, Vector2 toPosition, float duration = _defaultDrawDuration)
        {
            if (card == null)
            {
                return;
            }

            Vector2 originalScale = card.Scale;
            card.GlobalPosition = fromPosition;
            card.Scale = new Vector2(0.5f, 0.5f);
            card.Modulate = new Color(1, 1, 1, 0.5f);

            Tween tween = CreateTween();
            _ = tween.SetParallel(true);
            _ = tween.TweenProperty(card, "global_position", toPosition, duration)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            _ = tween.TweenProperty(card, "scale", originalScale, duration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            _ = tween.TweenProperty(card, "modulate", new Color(1, 1, 1, 1f), duration * 0.5f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            _ = await ToSignal(tween, Tween.SignalName.Finished);
        }

        public async void PlayDeployAnimation(Control unitDisplay, Vector2 position, float duration = _defaultDeployDuration)
        {
            if (unitDisplay == null)
            {
                return;
            }

            Vector2 originalScale = unitDisplay.Scale;
            Vector2 bounceScale = new(1.15f, 1.15f);

            unitDisplay.GlobalPosition = position;

            Tween tween = CreateTween();
            _ = tween.TweenProperty(unitDisplay, "scale", bounceScale, duration * 0.3f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            _ = tween.TweenProperty(unitDisplay, "scale", originalScale, duration * 0.7f)
                .SetTrans(Tween.TransitionType.Bounce)
                .SetEase(Tween.EaseType.Out);

            _ = await ToSignal(tween, Tween.SignalName.Finished);
        }

        public async void PlayReturnAnimation(CardUI card, Vector2 originalPosition, float duration = _defaultReturnDuration)
        {
            if (card == null)
            {
                return;
            }

            Tween tween = CreateTween();
            _ = tween.TweenProperty(card, "global_position", originalPosition, duration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            _ = await ToSignal(tween, Tween.SignalName.Finished);
        }

        public async void AnimatePosition(Control control, Vector2 from, Vector2 to, float duration)
        {
            if (control == null)
            {
                return;
            }

            control.GlobalPosition = from;
            Tween tween = CreateTween();
            _ = tween.TweenProperty(control, "global_position", to, duration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            _ = await ToSignal(tween, Tween.SignalName.Finished);
        }

        public async void AnimateScale(Control control, Vector2 from, Vector2 to, float duration)
        {
            if (control == null)
            {
                return;
            }

            control.Scale = from;
            Tween tween = CreateTween();
            _ = tween.TweenProperty(control, "scale", to, duration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            _ = await ToSignal(tween, Tween.SignalName.Finished);
        }

        public async void AnimateRotation(Control control, float fromRotation, float toRotation, float duration)
        {
            if (control == null)
            {
                return;
            }

            control.Rotation = fromRotation;
            Tween tween = CreateTween();
            _ = tween.TweenProperty(control, "rotation", toRotation, duration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            _ = await ToSignal(tween, Tween.SignalName.Finished);
        }

        public async void AnimateModulate(Control control, Color fromColor, Color toColor, float duration)
        {
            if (control == null)
            {
                return;
            }

            control.Modulate = fromColor;
            Tween tween = CreateTween();
            _ = tween.TweenProperty(control, "modulate", toColor, duration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            _ = await ToSignal(tween, Tween.SignalName.Finished);
        }
    }
}
