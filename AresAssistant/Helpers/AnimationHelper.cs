using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AresAssistant.Helpers;

/// <summary>
/// Provides performance-aware animation utilities.
/// In "avanzado" mode: richer, longer animations with glow/scale effects.
/// In "ligero" mode: simpler, faster animations for lower resource usage.
/// </summary>
public static class AnimationHelper
{
    public static bool IsAdvanced =>
        App.ConfigManager?.Config.PerformanceMode == "avanzado";

    // ─── Durations ────────────────────────────────────────────
    public static Duration Fast => IsAdvanced
        ? new(TimeSpan.FromMilliseconds(300))
        : new(TimeSpan.FromMilliseconds(150));

    public static Duration Normal => IsAdvanced
        ? new(TimeSpan.FromMilliseconds(500))
        : new(TimeSpan.FromMilliseconds(250));

    public static Duration Slow => IsAdvanced
        ? new(TimeSpan.FromMilliseconds(750))
        : new(TimeSpan.FromMilliseconds(350));

    // ─── Easing ───────────────────────────────────────────────
    public static IEasingFunction EaseOut => IsAdvanced
        ? new CubicEase { EasingMode = EasingMode.EaseOut }
        : new QuadraticEase { EasingMode = EasingMode.EaseOut };

    public static IEasingFunction EaseInOut => IsAdvanced
        ? new CubicEase { EasingMode = EasingMode.EaseInOut }
        : new QuadraticEase { EasingMode = EasingMode.EaseInOut };

    // ─── Slide distances ──────────────────────────────────────
    public static double SlideDistance => IsAdvanced ? 40 : 16;
    public static double SlideDistanceSmall => IsAdvanced ? 20 : 8;

    // ─── Fade-in an element with optional slide ───────────────
    public static void FadeSlideIn(UIElement el, double fromY = 0, double delayMs = 0)
    {
        var dur = Normal;
        var ease = EaseOut;
        if (fromY == 0) fromY = SlideDistanceSmall;

        var sb = new Storyboard();

        // Opacity
        var fadeAnim = new DoubleAnimation(0, 1, dur)
        {
            EasingFunction = ease,
            BeginTime = TimeSpan.FromMilliseconds(delayMs)
        };
        Storyboard.SetTarget(fadeAnim, el);
        Storyboard.SetTargetProperty(fadeAnim, new PropertyPath("Opacity"));
        sb.Children.Add(fadeAnim);

        // Translate Y
        var tt = new TranslateTransform(0, fromY);
        el.RenderTransform = tt;
        var slideAnim = new DoubleAnimation(fromY, 0, dur)
        {
            EasingFunction = ease,
            BeginTime = TimeSpan.FromMilliseconds(delayMs)
        };
        Storyboard.SetTarget(slideAnim, el);
        Storyboard.SetTargetProperty(slideAnim,
            new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        sb.Children.Add(slideAnim);

        // Advanced: additional scale bounce
        if (IsAdvanced)
        {
            var st = new ScaleTransform(0.96, 0.96);
            el.RenderTransform = new TransformGroup
            {
                Children = { tt, st }
            };
            el.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

            var scaleX = new DoubleAnimation(0.96, 1.0, dur)
            {
                EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 8, EasingMode = EasingMode.EaseOut },
                BeginTime = TimeSpan.FromMilliseconds(delayMs)
            };
            Storyboard.SetTarget(scaleX, el);
            Storyboard.SetTargetProperty(scaleX,
                new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(ScaleTransform.ScaleX)"));

            var scaleY = new DoubleAnimation(0.96, 1.0, dur)
            {
                EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 8, EasingMode = EasingMode.EaseOut },
                BeginTime = TimeSpan.FromMilliseconds(delayMs)
            };
            Storyboard.SetTarget(scaleY, el);
            Storyboard.SetTargetProperty(scaleY,
                new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(ScaleTransform.ScaleY)"));

            // Fix slide path for TransformGroup
            Storyboard.SetTargetProperty(slideAnim,
                new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(TranslateTransform.Y)"));

            sb.Children.Add(scaleX);
            sb.Children.Add(scaleY);
        }

        el.Opacity = 0;
        sb.Begin();
    }

    /// <summary>
    /// Slide a panel out to the left and a new one in from the right (wizard page transition).
    /// </summary>
    public static void SlideTransition(FrameworkElement outEl, FrameworkElement inEl, bool forward = true)
    {
        double distance = forward ? -outEl.ActualWidth : outEl.ActualWidth;
        if (distance == 0) distance = forward ? -560 : 560;

        var dur = IsAdvanced
            ? new Duration(TimeSpan.FromMilliseconds(450))
            : new Duration(TimeSpan.FromMilliseconds(250));
        var ease = EaseInOut;

        // --- Outgoing element ---
        var outTt = new TranslateTransform(0, 0);
        outEl.RenderTransform = outTt;
        var outSlide = new DoubleAnimation(0, distance, dur) { EasingFunction = ease };
        var outFade = new DoubleAnimation(1, 0, dur) { EasingFunction = ease };

        outSlide.Completed += (_, _) => outEl.Visibility = Visibility.Collapsed;
        outTt.BeginAnimation(TranslateTransform.XProperty, outSlide);
        outEl.BeginAnimation(UIElement.OpacityProperty, outFade);

        // --- Incoming element ---
        inEl.Visibility = Visibility.Visible;
        var inTt = new TranslateTransform(-distance, 0);
        inEl.RenderTransform = inTt;
        inEl.Opacity = 0;

        var inSlide = new DoubleAnimation(-distance, 0, dur) { EasingFunction = ease };
        var inFade = new DoubleAnimation(0, 1, dur) { EasingFunction = ease };

        inTt.BeginAnimation(TranslateTransform.XProperty, inSlide);
        inEl.BeginAnimation(UIElement.OpacityProperty, inFade);
    }

    /// <summary>
    /// Subtle press/bounce on a card when selected (advanced mode only).
    /// </summary>
    public static void BounceSelect(UIElement el)
    {
        if (!IsAdvanced) return;

        el.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        var st = new ScaleTransform(1, 1);
        el.RenderTransform = st;

        var dur = new Duration(TimeSpan.FromMilliseconds(200));
        var shrink = new DoubleAnimation(1, 0.93, new Duration(TimeSpan.FromMilliseconds(100)))
        {
            AutoReverse = true
        };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
    }

    /// <summary>
    /// Pulsing glow animation on a border (advanced mode only). Returns the storyboard to stop it.
    /// </summary>
    public static Storyboard? PulseGlow(UIElement el)
    {
        if (!IsAdvanced) return null;

        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        var pulse = new DoubleAnimation(0.4, 1.0, new Duration(TimeSpan.FromMilliseconds(1500)))
        {
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(pulse, el);
        Storyboard.SetTargetProperty(pulse, new PropertyPath("Opacity"));
        sb.Children.Add(pulse);
        sb.Begin();
        return sb;
    }
}
