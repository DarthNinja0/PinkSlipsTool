using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class PinkSlipsWheelPopup : Window
{
    private readonly Random _rng = new();
    private bool _isSpinning;
    private bool _hasSpun;
    private int _selectedIndex = -1;

    private static readonly List<PerkDef> WheelPerks = PerkManager.DefaultPerks;

    // Weight per perk index: higher = bigger slice = more likely to land there
    private static readonly int[] SliceWeights = { 1, 2, 4, 5, 2, 5, 1, 2, 1, 3 };
    private static readonly double[] SliceAngles = ComputeSliceAngles();
    private static readonly int TotalWeight = SliceWeights.Sum();

    private static double[] ComputeSliceAngles()
    {
        var total = SliceWeights.Sum();
        return SliceWeights.Select(w => 360.0 * w / total).ToArray();
    }

    private static readonly Brush[] SliceBrushes =
    {
        new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00)),
        new SolidColorBrush(Color.FromRgb(0x1E, 0x90, 0xFF)),
        new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
        new SolidColorBrush(Color.FromRgb(0x32, 0xCD, 0x32)),
        new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)),
        new SolidColorBrush(Color.FromRgb(0xFF, 0x4F, 0xA3)),
        new SolidColorBrush(Color.FromRgb(0xFF, 0x69, 0xB4)),
        new SolidColorBrush(Color.FromRgb(0x00, 0xCE, 0xD1)),
        new SolidColorBrush(Color.FromRgb(0x93, 0x70, 0xDB)),
        new SolidColorBrush(Color.FromRgb(0xFF, 0x63, 0x47)),
    };

    public PerkDef SelectedPerk { get; private set; }

    public PinkSlipsWheelPopup()
    {
        InitializeComponent();
        DrawWheel();
    }

    private void DrawWheel()
    {
        WheelContent.Children.Clear();
        var cx = 225.0;
        var cy = 225.0;
        var radius = 210.0;
        var startAngle = 0.0;

        for (var i = 0; i < WheelPerks.Count; i++)
        {
            var endAngle = startAngle + SliceAngles[i];
            var slice = CreateSlice(cx, cy, radius, startAngle, endAngle);
            slice.Fill = SliceBrushes[i % SliceBrushes.Length];
            slice.Stroke = Brushes.White;
            slice.StrokeThickness = 1;
            Panel.SetZIndex(slice, 1);
            WheelContent.Children.Add(slice);

            var midAngle = (startAngle + endAngle) / 2.0;
            var labelR = radius * 0.58;
            var rad = midAngle * Math.PI / 180.0;
            var lx = cx + labelR * Math.Cos(rad);
            var ly = cy + labelR * Math.Sin(rad);

            var textAngle = midAngle;
            if (textAngle > 90 && textAngle < 270)
                textAngle += 180;

            var label = new TextBlock
            {
                Text = WheelPerks[i].Name,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeight.FromOpenTypeWeight(700),
                TextAlignment = TextAlignment.Center,
                RenderTransform = new RotateTransform(textAngle),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, lx - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, ly - label.DesiredSize.Height / 2);
            Panel.SetZIndex(label, 2);
            WheelContent.Children.Add(label);

            startAngle = endAngle;
        }

        var hub = new Ellipse
        {
            Width = 52,
            Height = 52,
            Fill = Brushes.White,
            Stroke = Brushes.Black,
            StrokeThickness = 2
        };
        Canvas.SetLeft(hub, cx - 26);
        Canvas.SetTop(hub, cy - 26);
        Panel.SetZIndex(hub, 3);
        HubOverlay.Children.Add(hub);

        var logo = new Image
        {
            Width = 52,
            Height = 52,
            Stretch = Stretch.UniformToFill,
            Source = new BitmapImage(new Uri("pack://application:,,,/Images/C4Logo.png"))
        };
        logo.Clip = new EllipseGeometry(new Point(26, 26), 26, 26);
        Canvas.SetLeft(logo, cx - 26);
        Canvas.SetTop(logo, cy - 26);
        Panel.SetZIndex(logo, 4);
        HubOverlay.Children.Add(logo);
    }

    private static Path CreateSlice(double cx, double cy, double r, double startDeg, double endDeg)
    {
        var startRad = startDeg * Math.PI / 180.0;
        var endRad = endDeg * Math.PI / 180.0;

        var x1 = cx + r * Math.Cos(startRad);
        var y1 = cy + r * Math.Sin(startRad);
        var x2 = cx + r * Math.Cos(endRad);
        var y2 = cy + r * Math.Sin(endRad);

        var largeArc = (endDeg - startDeg) > 180.0;

        var seg = new PathSegmentCollection
        {
            new LineSegment(new Point(x1, y1), false),
            new ArcSegment(new Point(x2, y2), new Size(r, r), 0, largeArc, SweepDirection.Clockwise, true),
            new LineSegment(new Point(cx, cy), false)
        };

        var fig = new PathFigure(new Point(cx, cy), seg, true);
        return new Path { Data = new PathGeometry(new[] { fig }) };
    }

    private void SpinButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isSpinning || _hasSpun) return;
        _isSpinning = true;
        _hasSpun = true;
        SpinButton.IsEnabled = false;
        ResultBanner.Visibility = Visibility.Collapsed;
        ApplyButton.Visibility = Visibility.Collapsed;

        // Weighted random selection
        var roll = _rng.Next(TotalWeight);
        var cumulative = 0;
        for (var i = 0; i < SliceWeights.Length; i++)
        {
            cumulative += SliceWeights[i];
            if (roll < cumulative) { _selectedIndex = i; break; }
        }

        // Compute cumulative start angle of the selected slice
        var sliceStart = 0.0;
        for (var i = 0; i < _selectedIndex; i++)
            sliceStart += SliceAngles[i];

        var fullSpins = 5 + _rng.Next(3);
        // Rotate so the START of the selected slice lands at 12 o'clock (270°)
        var remainder = (270.0 - sliceStart) % 360.0;
        if (remainder < 0) remainder += 360.0;
        var targetAngle = fullSpins * 360.0 + remainder;

        var anim = new DoubleAnimation
        {
            From = 0,
            To = targetAngle,
            Duration = TimeSpan.FromSeconds(4),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        anim.Completed += (s, args) =>
        {
            _isSpinning = false;
            SelectedPerk = WheelPerks[_selectedIndex];
            ResultText.Text = $"🎉 {SelectedPerk.Name} 🎉";
            ResultBanner.Visibility = Visibility.Visible;
            ApplyButton.Visibility = Visibility.Visible;
        };

        var rotate = new RotateTransform { CenterX = 225, CenterY = 225 };
        WheelContent.RenderTransform = rotate;
        rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
