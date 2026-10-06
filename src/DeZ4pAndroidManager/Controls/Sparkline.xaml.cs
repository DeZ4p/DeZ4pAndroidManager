// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeZ4pAndroidManager.Controls;

/// <summary>
/// Lightweight sparkline - renders an ObservableCollection&lt;double&gt; as a line chart.
/// Auto-scales on resize and redraws whenever the collection changes.
/// No external chart libraries required.
/// </summary>
public partial class Sparkline : UserControl
{
    public Sparkline()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    // ─── Values ───
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(nameof(Values), typeof(ObservableCollection<double>),
            typeof(Sparkline), new PropertyMetadata(null, OnValuesChanged));

    public ObservableCollection<double>? Values
    {
        get => (ObservableCollection<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    // ─── Stroke brush ───
    public static readonly DependencyProperty StrokeBrushProperty =
        DependencyProperty.Register(nameof(StrokeBrush), typeof(Brush),
            typeof(Sparkline), new PropertyMetadata(Brushes.DeepSkyBlue, OnValuesChanged));

    public Brush StrokeBrush
    {
        get => (Brush)GetValue(StrokeBrushProperty);
        set => SetValue(StrokeBrushProperty, value);
    }

    // ─── Stroke color (for fill gradient) ───
    public static readonly DependencyProperty StrokeColorProperty =
        DependencyProperty.Register(nameof(StrokeColor), typeof(Color),
            typeof(Sparkline), new PropertyMetadata(Colors.DeepSkyBlue));

    public Color StrokeColor
    {
        get => (Color)GetValue(StrokeColorProperty);
        set => SetValue(StrokeColorProperty, value);
    }

    // ─── Maximum value (fixed scale, e.g. 100 for %) ───
    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(double),
            typeof(Sparkline), new PropertyMetadata(100.0, OnValuesChanged));

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    // ─── Auto-scale when MaxValue is 0 ───
    public static readonly DependencyProperty AutoScaleProperty =
        DependencyProperty.Register(nameof(AutoScale), typeof(bool),
            typeof(Sparkline), new PropertyMetadata(false, OnValuesChanged));

    public bool AutoScale
    {
        get => (bool)GetValue(AutoScaleProperty);
        set => SetValue(AutoScaleProperty, value);
    }

    // ─── Internal ───
    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var s = d as Sparkline;
        if (s == null) return;

        if (e.Property == ValuesProperty)
        {
            if (e.OldValue is ObservableCollection<double> oldCol)
                oldCol.CollectionChanged -= s.OnCollectionChanged;
            if (e.NewValue is ObservableCollection<double> newCol)
                newCol.CollectionChanged += s.OnCollectionChanged;
        }

        s.Redraw();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 2 || h <= 2) return;

        if (Values == null || Values.Count < 2)
        {
            Line.Points = new PointCollection();
            FillArea.Points = new PointCollection();
            return;
        }

        double max = MaxValue;
        if (AutoScale || max <= 0)
        {
            double localMax = Values.Count > 0 ? Values.Max() : 1;
            max = Math.Max(1, localMax * 1.1); // 10% headroom
        }

        double stepX = w / Math.Max(1, Values.Count - 1);
        var pts = new PointCollection(Values.Count);

        for (int i = 0; i < Values.Count; i++)
        {
            double norm = Math.Max(0, Math.Min(1, Values[i] / max));
            double y = h - norm * (h - 2) - 1;
            pts.Add(new Point(i * stepX, y));
        }

        Line.Points = pts;

        // Fill polygon (same points + bottom corners)
        var fillPts = new PointCollection(pts.Count + 2);
        foreach (var p in pts) fillPts.Add(p);
        fillPts.Add(new Point(w, h));
        fillPts.Add(new Point(0, h));
        FillArea.Points = fillPts;
    }
}