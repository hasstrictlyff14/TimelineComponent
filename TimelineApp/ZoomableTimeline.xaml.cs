using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TimelineApp
{
    public partial class ZoomableTimeline : UserControl
    {
        public ZoomableTimeline()
        {
            InitializeComponent();
            Loaded += (_, __) => RebuildAll();
            SizeChanged += (_, __) => Redraw();
        }

        // ========== Dependency Properties ==========

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ZoomableTimeline),
                new PropertyMetadata(null, OnItemsChanged));

        public DateTime ExtentStart
        {
            get => (DateTime)GetValue(ExtentStartProperty);
            set => SetValue(ExtentStartProperty, value);
        }
        public static readonly DependencyProperty ExtentStartProperty =
            DependencyProperty.Register(nameof(ExtentStart), typeof(DateTime), typeof(ZoomableTimeline),
                new PropertyMetadata(DateTime.UtcNow.AddHours(-12), OnExtentChanged));

        public DateTime ExtentEnd
        {
            get => (DateTime)GetValue(ExtentEndProperty);
            set => SetValue(ExtentEndProperty, value);
        }
        public static readonly DependencyProperty ExtentEndProperty =
            DependencyProperty.Register(nameof(ExtentEnd), typeof(DateTime), typeof(ZoomableTimeline),
                new PropertyMetadata(DateTime.UtcNow.AddHours(12), OnExtentChanged));

        /// <summary>Pixels per TimeSpan tick (100ns). Defaults to ~100 px per hour.</summary>
        public double PixelsPerTick
        {
            get => (double)GetValue(PixelsPerTickProperty);
            set => SetValue(PixelsPerTickProperty, value);
        }
        public static readonly DependencyProperty PixelsPerTickProperty =
            DependencyProperty.Register(nameof(PixelsPerTick), typeof(double), typeof(ZoomableTimeline),
                new PropertyMetadata(100.0 / TimeSpan.FromHours(1).Ticks, OnScaleChanged));

        // ========== Internals ==========

        private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (ZoomableTimeline)d;
            ctl.AutoSetExtentFromItems();
            ctl.RebuildAll();
        }

        private static void OnExtentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (ZoomableTimeline)d;
            ctl.RebuildAll();
        }

        private static void OnScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (ZoomableTimeline)d;
            ctl.RebuildAll();
        }

        private void AutoSetExtentFromItems()
        {
            if (ItemsSource == null) return;

            DateTime? min = null, max = null;

            foreach (var obj in ItemsSource)
            {
                if (obj is TimelineBlock b)
                {
                    if (min == null || b.Start < min) min = b.Start;
                    if (max == null || b.End > max) max = b.End;
                }
            }

            if (min != null && max != null && min < max)
            {
                // Add a small padding
                var pad = TimeSpan.FromMinutes(5);
                ExtentStart = min.Value - pad;
                ExtentEnd = max.Value + pad;
            }
        }

        private long TotalTicks => (ExtentEnd - ExtentStart).Ticks;

        private double XOf(DateTime t) => (t - ExtentStart).Ticks * PixelsPerTick;

        // ========== Rendering ==========

        private void RebuildAll()
        {
            // Set the total content width so we can scroll
            var width = Math.Max(0, TotalTicks * PixelsPerTick);
            TicksCanvas.Width = width;
            BlocksSurface.Width = width;

            PositionBlocks();
            RedrawTicks();
        }

        private void Redraw() => RebuildAll();

        private void PositionBlocks()
        {
            if (PART_Items == null || ItemsSource == null) return;

            PART_Items.UpdateLayout();

            foreach (var item in ItemsSource)
            {
                var container = (FrameworkElement)PART_Items.ItemContainerGenerator.ContainerFromItem(item);
                if (container is null) continue;

                if (item is TimelineBlock b)
                {
                    var left = XOf(b.Start);
                    var width = Math.Max(1.0, (b.End - b.Start).Ticks * PixelsPerTick);

                    Canvas.SetLeft(container, left);
                    Canvas.SetTop(container, 12);
                    container.Width = width;
                    container.Height = 24;
                }
            }
        }

        private void RedrawTicks()
        {
            TicksCanvas.Children.Clear();

            if (TotalTicks <= 0) return;

            var visibleStart = ScreenToTime(0);
            var visibleEnd = ScreenToTime(ViewportWidth());
            var visible = visibleEnd - visibleStart;
            if (visible <= TimeSpan.Zero) visible = ExtentEnd - ExtentStart;

            var (step, format) = ChooseTickStep(visible);

            var t = AlignUp(visibleStart, step);

            // Draw baseline
            TicksCanvas.Children.Add(new Line
            {
                X1 = XOf(visibleStart),
                X2 = XOf(visibleEnd),
                Y1 = 27,
                Y2 = 27,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            });

            for (var cur = t; cur <= visibleEnd; cur += step)
            {
                var x = XOf(cur);

                var line = new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = 0,
                    Y2 = 27,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1
                };
                TicksCanvas.Children.Add(line);

                var tb = new TextBlock
                {
                    Text = cur.ToString(format),
                    Margin = new Thickness(2, 2, 2, 0)
                };
                TicksCanvas.Children.Add(tb);
                Canvas.SetLeft(tb, x + 2);
                Canvas.SetTop(tb, 2);
            }
        }

        private (TimeSpan step, string format) ChooseTickStep(TimeSpan visible)
        {
            if (visible.TotalDays > 365 * 2) return (TimeSpan.FromDays(365), "yyyy");
            if (visible.TotalDays > 120) return (TimeSpan.FromDays(30), "MMM yyyy");
            if (visible.TotalDays > 14) return (TimeSpan.FromDays(1), "MMM d");
            if (visible.TotalHours > 6) return (TimeSpan.FromHours(1), "HH:mm");
            if (visible.TotalMinutes > 10) return (TimeSpan.FromMinutes(5), "HH:mm");
            if (visible.TotalMinutes > 2) return (TimeSpan.FromMinutes(1), "HH:mm");
            if (visible.TotalSeconds > 20) return (TimeSpan.FromSeconds(5), "HH:mm:ss");
            return (TimeSpan.FromSeconds(1), "HH:mm:ss");
        }

        private static DateTime AlignUp(DateTime start, TimeSpan step)
        {
            var ticks = (start.Ticks + step.Ticks - 1) / step.Ticks * step.Ticks;
            return new DateTime(ticks, start.Kind);
        }

        private double ViewportWidth() => PART_Scroll?.ViewportWidth > 0 ? PART_Scroll.ViewportWidth : ActualWidth;

        private DateTime ScreenToTime(double x)
        {
            var offset = PART_Scroll?.HorizontalOffset ?? 0.0;
            var totalX = x + offset;
            var ticks = totalX / PixelsPerTick;
            return ExtentStart.AddTicks((long)ticks);
        }

        private Point? _dragStart;
        private double _dragOriginOffset;

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _dragStart = e.GetPosition(PART_Scroll);
                _dragOriginOffset = PART_Scroll.HorizontalOffset;
                Mouse.Capture((IInputElement)sender);
            }
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragStart is Point start && e.LeftButton == MouseButtonState.Pressed)
            {
                var cur = e.GetPosition(PART_Scroll);
                var dx = cur.X - start.X;
                PART_Scroll.ScrollToHorizontalOffset(Math.Max(0, _dragOriginOffset - dx));
                RedrawTicks(); // tick labels reposition as you pan
            }
        }

        private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _dragStart = null;
                Mouse.Capture(null);
            }
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Zoom around mouse position
            var mouse = e.GetPosition(TicksCanvas);
            var focusTime = ScreenToTime(mouse.X);

            var factor = e.Delta > 0 ? 1.2 : 1 / 1.2;
            var newScale = Math.Clamp(PixelsPerTick * factor, 1e-9, 1e9);

            // Keep focusTime under the cursor after scaling
            var oldX = XOf(focusTime);
            PixelsPerTick = newScale; 
            var newX = XOf(focusTime);
            var deltaX = newX - oldX;

            // Adjust scroll so the focus stays under mouse
            PART_Scroll.ScrollToHorizontalOffset(Math.Max(0, PART_Scroll.HorizontalOffset + deltaX));
            RedrawTicks();
        }
    }
}
