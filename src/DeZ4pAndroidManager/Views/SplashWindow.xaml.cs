// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DeZ4pAndroidManager.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Spinner rotation
            if (SpinnerRotation != null)
            {
                var rotate = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.4))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };
                SpinnerRotation.BeginAnimation(RotateTransform.AngleProperty, rotate);
            }

            // Pulsing logo ring
            if (LogoRing != null)
            {
                var pulse = new DoubleAnimation(0.25, 0.6, TimeSpan.FromMilliseconds(900))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                LogoRing.BeginAnimation(OpacityProperty, pulse);
            }

            // Fire icon gentle pulse
            if (LogoFire != null)
            {
                LogoFire.RenderTransformOrigin = new Point(0.5, 0.5);
                var scale = new ScaleTransform(1, 1);
                LogoFire.RenderTransform = scale;

                var fireScale = new DoubleAnimation(0.96, 1.06, TimeSpan.FromMilliseconds(800))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, fireScale);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, fireScale);
            }

            // Progress bar slide
            if (ProgressSlide != null)
            {
                double target = ActualWidth > 100 ? ActualWidth : 460;
                var slide = new DoubleAnimation(-110, target, TimeSpan.FromSeconds(1.4))
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                ProgressSlide.RenderTransform = new TranslateTransform();
                ((TranslateTransform)ProgressSlide.RenderTransform)
                    .BeginAnimation(TranslateTransform.XProperty, slide);
            }
        }
        catch
        {
            // Animations are non-critical - never crash the splash
        }
    }

    /// <summary>
    /// Updates the status message shown under the title.
    /// </summary>
    public void SetStatus(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetStatus(message));
            return;
        }
        if (StatusText != null) StatusText.Text = message;
    }
}