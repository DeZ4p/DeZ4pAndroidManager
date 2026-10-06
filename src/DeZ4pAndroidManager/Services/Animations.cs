// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Helper methods for common UI animations: fade, slide, stagger.
/// All animations use CubicEase (EaseOut) for natural motion.
/// </summary>
public static class Animations
{
    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

    /// <summary>
    /// Fades the element in and slides it from the left.
    /// </summary>
    public static void FadeInFromLeft(FrameworkElement? el, int delayMs = 0, int durationMs = 220)
    {
        if (el == null) return;

        el.BeginAnimation(UIElement.OpacityProperty, null);

        var transform = new TranslateTransform(-16, 0);
        el.RenderTransform = transform;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = EaseOut,
            FillBehavior = FillBehavior.HoldEnd
        };
        var slide = new DoubleAnimation(-16, 0, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = EaseOut,
            FillBehavior = FillBehavior.HoldEnd
        };

        el.BeginAnimation(UIElement.OpacityProperty, fade);
        transform.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    /// <summary>
    /// Fades the element in and slides it up from the bottom.
    /// </summary>
    public static void FadeInFromBottom(FrameworkElement? el, int delayMs = 0, int durationMs = 220)
    {
        if (el == null) return;

        el.BeginAnimation(UIElement.OpacityProperty, null);

        var transform = new TranslateTransform(0, 14);
        el.RenderTransform = transform;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = EaseOut,
            FillBehavior = FillBehavior.HoldEnd
        };
        var slide = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = EaseOut,
            FillBehavior = FillBehavior.HoldEnd
        };

        el.BeginAnimation(UIElement.OpacityProperty, fade);
        transform.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    /// <summary>
    /// Simple fade-in.
    /// </summary>
    public static void FadeIn(FrameworkElement? el, int delayMs = 0, int durationMs = 220)
    {
        if (el == null) return;

        el.BeginAnimation(UIElement.OpacityProperty, null);

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = EaseOut,
            FillBehavior = FillBehavior.HoldEnd
        };
        el.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>
    /// Stagger fade-in from left on all children of a panel.
    /// </summary>
    public static void StaggerChildrenFromLeft(Panel? panel, int baseDelayMs = 0, int perItemMs = 12)
    {
        if (panel == null) return;
        int i = 0;
        foreach (var child in panel.Children)
        {
            if (child is FrameworkElement fe)
                FadeInFromLeft(fe, baseDelayMs + i * perItemMs, 200);
            i++;
        }
    }

    /// <summary>
    /// Stagger fade-in from bottom on all children of a panel.
    /// </summary>
    public static void StaggerChildrenFromBottom(Panel? panel, int baseDelayMs = 0, int perItemMs = 40)
    {
        if (panel == null) return;
        int i = 0;
        foreach (var child in panel.Children)
        {
            if (child is FrameworkElement fe)
                FadeInFromBottom(fe, baseDelayMs + i * perItemMs, 220);
            i++;
        }
    }

    /// <summary>
    /// Smooth fade-out and collapse, then invokes the callback.
    /// </summary>
    public static void FadeOutAndCollapse(FrameworkElement? el, Action? onComplete = null, int durationMs = 260)
    {
        if (el == null) { onComplete?.Invoke(); return; }

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.HoldEnd
        };
        fade.Completed += (_, _) =>
        {
            el.Visibility = Visibility.Collapsed;
            onComplete?.Invoke();
        };
        el.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}