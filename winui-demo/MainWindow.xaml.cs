using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;

namespace V4Wa.Demo;

public sealed partial class MainWindow : Window
{
    private static readonly string[] EqLabels10 = { "31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k" };
    private static readonly string[] EqLabels18 =
    {
        "31", "45", "62", "90", "125", "180",
        "250", "360", "500", "750", "1k", "1.5k",
        "2k", "3k", "4k", "6k", "8k", "16k"
    };

    private double[] _eq10 = { 1.5, 2.0, 0.5, 0, -0.5, 0, 0.5, 1.5, 2.5, 2.0 };
    private double[] _eq18 = new double[18];
    private int _eqMode = 10;
    private readonly List<Slider> _eqSliders = new();
    private readonly List<TextBlock> _eqValueLabels = new();

    private string _selectedPreset = "Hi-Res Audiophile";
    private int _bassMode = 1;
    private int _clarityMode = 2;
    private int _dynDevice = 0;
    private int _analogMode = 0;
    private bool _isUpdating = true;

    private readonly SolidColorBrush EmeraldBrush;
    private readonly SolidColorBrush EmeraldLightBrush;
    private readonly SolidColorBrush EmeraldTextBrush;
    private readonly SolidColorBrush EmeraldBgBrush;
    private readonly SolidColorBrush EmeraldBorderBrush;
    private readonly SolidColorBrush MutedBrush;
    private readonly SolidColorBrush MutedBgBrush;
    private readonly SolidColorBrush MutedBorderBrush;
    private readonly SolidColorBrush CardBgBrush;
    private readonly SolidColorBrush CardBorderBrush;
    private readonly SolidColorBrush TransparentBrush;
    private readonly SolidColorBrush TextSecondaryBrush;
    private readonly SolidColorBrush TextDimBrush;

    public MainWindow()
    {
        InitializeComponent();

        // Create WinRT brushes on the UI thread after XAML initialization
        EmeraldBrush = new(Color.FromArgb(255, 16, 185, 129));
        EmeraldLightBrush = new(Color.FromArgb(255, 52, 211, 153));
        EmeraldTextBrush = new(Color.FromArgb(255, 167, 243, 208));
        EmeraldBgBrush = new(Color.FromArgb(46, 16, 185, 129));
        EmeraldBorderBrush = new(Color.FromArgb(140, 52, 211, 153));
        MutedBrush = new(Color.FromArgb(255, 113, 113, 122));
        MutedBgBrush = new(Color.FromArgb(255, 39, 39, 42));
        MutedBorderBrush = new(Color.FromArgb(255, 63, 63, 70));
        CardBgBrush = new(Color.FromArgb(204, 18, 18, 22));
        CardBorderBrush = new(Color.FromArgb(255, 46, 46, 54));
        TransparentBrush = new(Colors.Transparent);
        TextSecondaryBrush = new(Color.FromArgb(255, 161, 161, 170));
        TextDimBrush = new(Color.FromArgb(255, 212, 212, 216));

        // Safe Windows 11 Mica backdrop
        try
        {
            if (MicaController.IsSupported())
            {
                SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
            }
        }
        catch { }

        // Native Windows 11 custom title bar + window sizing
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new SizeInt32(1240, 860));

        _eq18 = Interpolate10To18(_eq10);
        RebuildEqSliders();
        ApplyPreset("Hi-Res Audiophile");
    }

    private static double[] Interpolate10To18(double[] b10)
    {
        double Mid(double a, double b) => Math.Round((a + b) / 2.0 * 2.0) / 2.0;
        return new[]
        {
            b10[0], Mid(b10[0], b10[1]), b10[1], Mid(b10[1], b10[2]),
            b10[2], Mid(b10[2], b10[3]), b10[3], Mid(b10[3], b10[4]),
            b10[4], Mid(b10[4], b10[5]), b10[5], Mid(b10[5], b10[6]),
            b10[6], Mid(b10[6], b10[7]), b10[7], Mid(b10[7], b10[8]),
            b10[8], b10[9]
        };
    }

    private static double[] Downsample18To10(double[] b18)
    {
        int[] idx = { 0, 2, 4, 6, 8, 10, 12, 14, 16, 17 };
        var res = new double[10];
        for (int i = 0; i < 10; i++) res[i] = b18[idx[i]];
        return res;
    }

    private void RebuildEqSliders()
    {
        EqSlidersGrid.Children.Clear();
        EqSlidersGrid.ColumnDefinitions.Clear();
        _eqSliders.Clear();
        _eqValueLabels.Clear();

        var labels = _eqMode == 18 ? EqLabels18 : EqLabels10;
        var values = _eqMode == 18 ? _eq18 : _eq10;
        EqTitleText.Text = $"{_eqMode}-Band Hi-Res Equalizer";

        for (int i = 0; i < labels.Length; i++)
        {
            EqSlidersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var col = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 6
            };

            var valText = new TextBlock
            {
                Text = FormatDb(values[i]),
                FontFamily = new FontFamily("Consolas"),
                FontSize = _eqMode == 18 ? 10.5 : 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = values[i] != 0 ? EmeraldLightBrush : TextSecondaryBrush
            };

            int bandIdx = i;
            var slider = new Slider
            {
                Orientation = Orientation.Vertical,
                Height = 132,
                Minimum = -12,
                Maximum = 12,
                StepFrequency = 0.5,
                Value = values[i],
                HorizontalAlignment = HorizontalAlignment.Center
            };
            slider.ValueChanged += (_, e) =>
            {
                if (_isUpdating) return;
                if (_eqMode == 18)
                {
                    _eq18[bandIdx] = e.NewValue;
                    _eq10 = Downsample18To10(_eq18);
                }
                else
                {
                    _eq10[bandIdx] = e.NewValue;
                    _eq18 = Interpolate10To18(_eq10);
                }
                valText.Text = FormatDb(e.NewValue);
                bool eqActive = MasterToggle.IsOn && EqToggle.IsOn;
                valText.Foreground = Math.Abs(e.NewValue) > 0.01 && eqActive ? EmeraldLightBrush : TextSecondaryBrush;
                _isUpdating = true;
                EqPresetCombo.SelectedIndex = 0; // Custom
                _isUpdating = false;
                DrawEqCurve();
            };

            var freqText = new TextBlock
            {
                Text = labels[i],
                FontFamily = new FontFamily("Consolas"),
                FontSize = _eqMode == 18 ? 10 : 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = TextSecondaryBrush
            };

            col.Children.Add(valText);
            col.Children.Add(slider);
            col.Children.Add(freqText);
            Grid.SetColumn(col, i);
            EqSlidersGrid.Children.Add(col);
            _eqSliders.Add(slider);
            _eqValueLabels.Add(valText);
        }

        UpdateEqModeButtons();
        DrawEqCurve();
    }

    private static string FormatDb(double db) => db > 0.01 ? $"+{db:0.0}" : $"{db:0.0}";

    private void DrawEqCurve()
    {
        EqCurveCanvas.Children.Clear();
        double w = EqCurveCanvas.ActualWidth;
        double h = EqCurveCanvas.ActualHeight;
        if (w < 20 || h < 20) return;

        bool active = MasterToggle.IsOn && EqToggle.IsOn;
        var values = _eqMode == 18 ? _eq18 : _eq10;
        double padX = 16;
        double padY = 10;
        double usableW = w - padX * 2;
        double usableH = h - padY * 2;
        double midY = padY + usableH / 2.0;

        // 0 dB center line
        EqCurveCanvas.Children.Add(new Line
        {
            X1 = padX,
            Y1 = midY,
            X2 = w - padX,
            Y2 = midY,
            Stroke = MutedBorderBrush,
            StrokeDashArray = new DoubleCollection { 4, 4 },
            StrokeThickness = 1
        });

        var polyline = new Polyline
        {
            Stroke = active ? EmeraldLightBrush : MutedBrush,
            StrokeThickness = 2.4,
            StrokeLineJoin = PenLineJoin.Round
        };

        var fillPoints = new PointCollection();
        fillPoints.Add(new Point(padX, h - padY));

        for (int i = 0; i < values.Length; i++)
        {
            double x = padX + (double)i / (values.Length - 1) * usableW;
            double norm = (values[i] + 12.0) / 24.0;
            double y = padY + (1.0 - norm) * usableH;
            polyline.Points.Add(new Point(x, y));
            fillPoints.Add(new Point(x, y));
        }

        fillPoints.Add(new Point(w - padX, h - padY));

        var grad = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        grad.GradientStops.Add(new GradientStop
        {
            Color = active ? Color.FromArgb(65, 52, 211, 153) : Color.FromArgb(35, 113, 113, 122),
            Offset = 0
        });
        grad.GradientStops.Add(new GradientStop
        {
            Color = Color.FromArgb(0, 16, 185, 129),
            Offset = 1
        });

        EqCurveCanvas.Children.Add(new Polygon { Points = fillPoints, Fill = grad });
        EqCurveCanvas.Children.Add(polyline);

        // Band nodes
        foreach (var pt in polyline.Points)
        {
            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = active ? new SolidColorBrush(Colors.White) : MutedBrush,
                Stroke = active ? EmeraldBrush : MutedBorderBrush,
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(dot, pt.X - 3.5);
            Canvas.SetTop(dot, pt.Y - 3.5);
            EqCurveCanvas.Children.Add(dot);
        }
    }

    private void ApplyPreset(string name)
    {
        _isUpdating = true;
        _selectedPreset = name;

        // Reset all modules cleanly first (just like our real V4Wa app)
        EqToggle.IsOn = false;
        _eq10 = new double[10];
        BassToggle.IsOn = false;
        _bassMode = 0;
        BassFreqSlider.Value = 60;
        BassGainSlider.Value = 3.5;
        ClarityToggle.IsOn = false;
        _clarityMode = 0;
        ClarityGainSlider.Value = 3.5;
        DynamicToggle.IsOn = false;
        _dynDevice = 0;
        DynBassSlider.Value = 25;
        AnalogToggle.IsOn = false;
        _analogMode = 0;
        TubeToggle.IsOn = false;
        TubeDriveSlider.Value = 25;
        SurroundToggle.IsOn = false;
        SurroundDelaySlider.Value = 6.0;
        StereoWidthSlider.Value = 100;
        ConvolverToggle.IsOn = false;
        MasterGainSlider.Value = 0;
        LimiterToggle.IsOn = true;
        LimiterSlider.Value = -0.3;

        switch (name)
        {
            case "Hi-Res Audiophile":
                EqToggle.IsOn = true;
                _eq10 = new[] { 1.5, 2.0, 0.5, 0, -0.5, 0, 0.5, 1.5, 2.5, 2.0 };
                BassToggle.IsOn = true;
                _bassMode = 1;
                BassFreqSlider.Value = 55;
                BassGainSlider.Value = 3.5;
                ClarityToggle.IsOn = true;
                _clarityMode = 2;
                ClarityGainSlider.Value = 3.0;
                SurroundToggle.IsOn = true;
                SurroundDelaySlider.Value = 3.5;
                StereoWidthSlider.Value = 115;
                PresetCombo.SelectedIndex = 1;
                EqPresetCombo.SelectedIndex = 0;
                break;

            case "Deep Bass Boost":
                EqToggle.IsOn = true;
                _eq10 = new[] { 4.5, 5.0, 2.5, 0.5, -1.0, -0.5, 0.5, 1.5, 2.0, 1.5 };
                BassToggle.IsOn = true;
                _bassMode = 2;
                BassFreqSlider.Value = 65;
                BassGainSlider.Value = 7.5;
                ClarityToggle.IsOn = true;
                _clarityMode = 0;
                ClarityGainSlider.Value = 2.5;
                PresetCombo.SelectedIndex = 2;
                EqPresetCombo.SelectedIndex = 0;
                break;

            case "Warm Tube Studio":
                EqToggle.IsOn = true;
                _eq10 = new[] { 1.0, 1.5, 1.0, 0.5, 0.5, 1.0, 1.5, 1.0, 0.5, 0 };
                BassToggle.IsOn = true;
                _bassMode = 0;
                BassFreqSlider.Value = 60;
                BassGainSlider.Value = 2.5;
                ClarityToggle.IsOn = true;
                _clarityMode = 1;
                ClarityGainSlider.Value = 2.0;
                TubeToggle.IsOn = true;
                TubeDriveSlider.Value = 42;
                SurroundToggle.IsOn = true;
                SurroundDelaySlider.Value = 5.0;
                StereoWidthSlider.Value = 110;
                PresetCombo.SelectedIndex = 3;
                EqPresetCombo.SelectedIndex = 0;
                break;

            default: // Flat Reference
                PresetCombo.SelectedIndex = 0;
                EqPresetCombo.SelectedIndex = 1;
                break;
        }

        _eq18 = Interpolate10To18(_eq10);
        SyncEqSliderValues();
        _isUpdating = false;
        RefreshVisualStates();
    }

    private void SyncEqSliderValues()
    {
        var values = _eqMode == 18 ? _eq18 : _eq10;
        for (int i = 0; i < _eqSliders.Count && i < values.Length; i++)
        {
            _eqSliders[i].Value = values[i];
            _eqValueLabels[i].Text = FormatDb(values[i]);
        }
    }

    private void RefreshVisualStates()
    {
        bool master = MasterToggle.IsOn;

        // Status Pill
        StatusPill.Background = master ? EmeraldBgBrush : CardBgBrush;
        StatusPill.BorderBrush = master ? EmeraldBorderBrush : CardBorderBrush;
        StatusDot.Fill = master ? EmeraldLightBrush : MutedBrush;
        StatusText.Text = master ? "WORKING" : "NOT WORKING";
        StatusText.Foreground = master ? EmeraldTextBrush : TextSecondaryBrush;

        // Preset card highlights
        StylePresetCard(PresetFlatBtn, _selectedPreset == "Flat Reference");
        StylePresetCard(PresetHiResBtn, _selectedPreset == "Hi-Res Audiophile");
        StylePresetCard(PresetBassBtn, _selectedPreset == "Deep Bass Boost");
        StylePresetCard(PresetTubeBtn, _selectedPreset == "Warm Tube Studio");

        // Module Icons & ON/OFF Accent Dimming
        EqIcon.Foreground = master && EqToggle.IsOn ? EmeraldLightBrush : MutedBrush;
        BassIcon.Foreground = master && BassToggle.IsOn ? EmeraldLightBrush : MutedBrush;
        ClarityIcon.Foreground = master && ClarityToggle.IsOn ? EmeraldLightBrush : MutedBrush;
        DynamicIcon.Foreground = master && DynamicToggle.IsOn ? EmeraldLightBrush : MutedBrush;
        AnalogIcon.Foreground = master && AnalogToggle.IsOn ? EmeraldLightBrush : MutedBrush;
        TubeIcon.Foreground = master && TubeToggle.IsOn ? EmeraldLightBrush : MutedBrush;
        SurroundIcon.Foreground = master && SurroundToggle.IsOn ? EmeraldLightBrush : MutedBrush;
        ConvolverIcon.Foreground = master && ConvolverToggle.IsOn ? EmeraldLightBrush : MutedBrush;
        LimiterIcon.Foreground = master && LimiterToggle.IsOn ? EmeraldLightBrush : MutedBrush;

        // Mode buttons (active emerald when ON, neutral gray when OFF)
        StyleModeButtons(new[] { BassMode0, BassMode1, BassMode2 }, _bassMode, master && BassToggle.IsOn);
        StyleModeButtons(new[] { ClarityMode0, ClarityMode1, ClarityMode2 }, _clarityMode, master && ClarityToggle.IsOn);
        StyleModeButtons(new[] { DynDev0, DynDev1, DynDev2, DynDev3 }, _dynDevice, master && DynamicToggle.IsOn);
        StyleModeButtons(new[] { AnalogMode0, AnalogMode1, AnalogMode2 }, _analogMode, master && AnalogToggle.IsOn);

        // Dim slider opacity slightly when module is OFF
        BassFreqSlider.Opacity = BassGainSlider.Opacity = master && BassToggle.IsOn ? 1.0 : 0.45;
        ClarityGainSlider.Opacity = master && ClarityToggle.IsOn ? 1.0 : 0.45;
        DynBassSlider.Opacity = master && DynamicToggle.IsOn ? 1.0 : 0.45;
        TubeDriveSlider.Opacity = master && TubeToggle.IsOn ? 1.0 : 0.45;
        SurroundDelaySlider.Opacity = StereoWidthSlider.Opacity = master && SurroundToggle.IsOn ? 1.0 : 0.45;
        LimiterSlider.Opacity = master && LimiterToggle.IsOn ? 1.0 : 0.45;
        EqSlidersGrid.Opacity = master && EqToggle.IsOn ? 1.0 : 0.5;

        // Update readouts
        BassFreqVal.Text = $"{BassFreqSlider.Value:0} Hz";
        BassGainVal.Text = $"+{BassGainSlider.Value:0.0} dB";
        ClarityGainVal.Text = $"+{ClarityGainSlider.Value:0.0} dB";
        DynBassVal.Text = $"{DynBassSlider.Value:0}%";
        TubeDriveVal.Text = $"{TubeDriveSlider.Value:0}%";
        SurroundDelayVal.Text = $"{SurroundDelaySlider.Value:0.0} ms";
        StereoWidthVal.Text = $"{StereoWidthSlider.Value:0}%";
        MasterGainVal.Text = MasterGainSlider.Value > 0 ? $"+{MasterGainSlider.Value:0.0} dB" : $"{MasterGainSlider.Value:0.0} dB";
        LimiterVal.Text = $"{LimiterSlider.Value:0.0} dBFS";

        bool eqActive = master && EqToggle.IsOn;
        var eqVals = _eqMode == 18 ? _eq18 : _eq10;
        for (int i = 0; i < _eqValueLabels.Count && i < eqVals.Length; i++)
        {
            _eqValueLabels[i].Foreground = Math.Abs(eqVals[i]) > 0.01 && eqActive ? EmeraldLightBrush : TextSecondaryBrush;
        }

        DrawEqCurve();
    }

    private void StylePresetCard(Button btn, bool active)
    {
        btn.Background = active ? EmeraldBgBrush : CardBgBrush;
        btn.BorderBrush = active ? EmeraldBorderBrush : CardBorderBrush;
    }

    private void StyleModeButtons(Button[] buttons, int selectedIdx, bool moduleActive)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            bool selected = i == selectedIdx;
            if (!selected)
            {
                buttons[i].Background = TransparentBrush;
                buttons[i].BorderBrush = TransparentBrush;
                buttons[i].Foreground = TextSecondaryBrush;
            }
            else if (!moduleActive)
            {
                buttons[i].Background = MutedBgBrush;
                buttons[i].BorderBrush = MutedBorderBrush;
                buttons[i].Foreground = TextDimBrush;
            }
            else
            {
                buttons[i].Background = EmeraldBgBrush;
                buttons[i].BorderBrush = EmeraldBorderBrush;
                buttons[i].Foreground = EmeraldTextBrush;
            }
        }
    }

    private void UpdateEqModeButtons()
    {
        Eq10Btn.Background = _eqMode == 10 ? MutedBgBrush : TransparentBrush;
        Eq10Btn.Foreground = _eqMode == 10 ? EmeraldLightBrush : TextSecondaryBrush;
        Eq18Btn.Background = _eqMode == 18 ? MutedBgBrush : TransparentBrush;
        Eq18Btn.Foreground = _eqMode == 18 ? EmeraldLightBrush : TextSecondaryBrush;
    }

    private void V4WaInfoBtn_Click(object sender, RoutedEventArgs e) => V4WaTeachingTip.IsOpen = true;

    private void TabDspBtn_Click(object sender, RoutedEventArgs e)
    {
        DspConsolePanel.Visibility = Visibility.Visible;
        DevicesPanel.Visibility = Visibility.Collapsed;
        TabDspBtn.Background = MutedBgBrush;
        TabDspBtn.Foreground = new SolidColorBrush(Colors.White);
        TabDevicesBtn.Background = TransparentBrush;
        TabDevicesBtn.Foreground = TextSecondaryBrush;
    }

    private void TabDevicesBtn_Click(object sender, RoutedEventArgs e)
    {
        DspConsolePanel.Visibility = Visibility.Collapsed;
        DevicesPanel.Visibility = Visibility.Visible;
        TabDevicesBtn.Background = MutedBgBrush;
        TabDevicesBtn.Foreground = new SolidColorBrush(Colors.White);
        TabDspBtn.Background = TransparentBrush;
        TabDspBtn.Foreground = TextSecondaryBrush;
    }

    private void MasterToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isUpdating) RefreshVisualStates();
    }

    private void ModuleToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isUpdating) RefreshVisualStates();
    }

    private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isUpdating) RefreshVisualStates();
    }

    private void PresetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
        {
            ApplyPreset(name);
        }
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (PresetCombo.SelectedItem is ComboBoxItem item && item.Content is string name)
        {
            ApplyPreset(name);
        }
    }

    private void EqModeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int mode) && mode != _eqMode)
        {
            _eqMode = mode;
            _isUpdating = true;
            RebuildEqSliders();
            _isUpdating = false;
            RefreshVisualStates();
        }
    }

    private void EqPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (EqPresetCombo.SelectedItem is ComboBoxItem item && item.Content is string name)
        {
            double[]? target = name switch
            {
                "Flat" => new double[10],
                "V-Shape Fun" => new[] { 3.5, 4.0, 2.0, 0, -1.5, -1.0, 0.5, 2.0, 3.5, 3.0 },
                "Vocal Focus" => new[] { -1.0, -0.5, 0, 1.0, 2.0, 3.0, 3.5, 2.0, 0.5, 0 },
                "Deep Basshead" => new[] { 5.5, 6.0, 3.5, 1.0, -1.0, -0.5, 0.5, 1.5, 2.0, 1.5 },
                _ => null
            };
            if (target != null)
            {
                _isUpdating = true;
                _eq10 = target;
                _eq18 = Interpolate10To18(_eq10);
                EqToggle.IsOn = true;
                SyncEqSliderValues();
                _isUpdating = false;
                RefreshVisualStates();
            }
        }
    }

    private void ResetEqBtn_Click(object sender, RoutedEventArgs e)
    {
        _isUpdating = true;
        _eq10 = new double[10];
        _eq18 = new double[18];
        EqPresetCombo.SelectedIndex = 1; // Flat
        SyncEqSliderValues();
        _isUpdating = false;
        RefreshVisualStates();
    }

    private void EqCurveCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawEqCurve();

    private void BassMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int idx))
        {
            _bassMode = idx;
            RefreshVisualStates();
        }
    }

    private void ClarityMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int idx))
        {
            _clarityMode = idx;
            RefreshVisualStates();
        }
    }

    private void DynDev_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int idx))
        {
            _dynDevice = idx;
            RefreshVisualStates();
        }
    }

    private void AnalogMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int idx))
        {
            _analogMode = idx;
            RefreshVisualStates();
        }
    }

    private async void DummyAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string msg)
        {
            ToastBar.Title = "V4Wa Demo";
            ToastBar.Message = msg;
            ToastBar.IsOpen = true;
            await Task.Delay(2600);
            ToastBar.IsOpen = false;
        }
    }
}
