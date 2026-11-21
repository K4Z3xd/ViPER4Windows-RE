using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace V4Wa.Demo;

public sealed partial class MainWindow : Window
{
    private sealed class DspCardEntry
    {
        public string Id { get; init; } = "";
        public int DefaultOrder { get; init; }
        public Border Root { get; init; } = null!;
        public Border IconBox { get; init; } = null!;
        public FontIcon Icon { get; init; } = null!;
        public Button PinBtn { get; init; } = null!;
        public ToggleSwitch? Toggle { get; init; }
        public Func<bool> IsActive { get; set; } = () => false;
        public Action<bool> RefreshVisual { get; set; } = _ => { };
    }

    private static readonly string[] EqLabels10 = { "31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k" };
    private static readonly string[] EqLabels18 =
    {
        "31", "45", "62", "90", "125", "180",
        "250", "360", "500", "750", "1k", "1.5k",
        "2k", "3k", "4k", "6k", "8k", "16k"
    };

    private static readonly (string Name, string Subtitle)[] BuiltinPresets =
    {
        ("Flat Reference", "Transparent bit-clean studio baseline"),
        ("Hi-Res Audiophile", "Extended sub-bass + XHiFi micro-detail"),
        ("Deep Bass Boost", "Subwoofer harmonic punch for IEMs"),
        ("Warm Tube Studio", "6N1J analog warmth + vocal focus")
    };

    private double[] _eq10 = { 1.5, 2.0, 0.5, 0, -0.5, 0, 0.5, 1.5, 2.5, 2.0 };
    private double[] _eq18 = new double[18];
    private int _eqMode = 10;
    private readonly List<Slider> _eqSliders = new();
    private readonly List<TextBlock> _eqValueLabels = new();

    private string _selectedPreset = "Hi-Res Audiophile";
    private readonly List<string> _pinnedPresets = new() { "Flat Reference", "Hi-Res Audiophile", "Deep Bass Boost", "Warm Tube Studio" };
    private readonly List<string> _pinnedModules = new();
    private readonly List<DspCardEntry> _cards = new();
    private bool _isUpdating = true;
    private bool _settingsOpen = false;
    private string _activeSettingsCategory = "Appearance";
    private double _currentWindowWidth = 1240;
    private int _lastModuleCols = 2;
    private int _lastPresetCols = 4;

    // Theme Engine state (Acrylic default + DarkOLED)
    private string _activeTheme = "Acrylic";
    private Color _accentBase = Color.FromArgb(255, 16, 185, 129);
    private readonly UISettings _uiSettings = new();

    // Module state handles
    private ToggleSwitch _limiterToggle = null!, _bassToggle = null!, _clarityToggle = null!, _dynToggle = null!;
    private ToggleSwitch _tubeToggle = null!, _analogToggle = null!, _ddcToggle = null!, _convToggle = null!;
    private ToggleSwitch _vseToggle = null!, _surroundToggle = null!, _fieldToggle = null!, _vhsToggle = null!;
    private ToggleSwitch _reverbToggle = null!, _compToggle = null!, _agcToggle = null!, _cureToggle = null!;

    private Slider _masterGainSlider = null!, _panSlider = null!, _limiterSlider = null!;
    private Slider _bassFreqSlider = null!, _bassGainSlider = null!, _clarityGainSlider = null!;
    private Slider _dynBassSlider = null!, _tubeDriveSlider = null!;
    private Slider _convCrossSlider = null!, _convWetSlider = null!, _vseSlider = null!;
    private Slider _surroundDelaySlider = null!, _stereoWidthSlider = null!;
    private Slider _fieldStrengthSlider = null!, _fieldMidSlider = null!;
    private Slider _revRoomSlider = null!, _revWidthSlider = null!, _revDampSlider = null!, _revWetSlider = null!, _revDrySlider = null!;
    private Slider _compThreshSlider = null!, _compRatioSlider = null!, _compAtkSlider = null!, _compRelSlider = null!, _compMakeupSlider = null!;
    private Slider _agcStrengthSlider = null!, _agcMaxGainSlider = null!, _agcThreshSlider = null!;

    private int _bassMode = 1, _clarityMode = 2, _dynDevice = 0, _analogMode = 0, _vhsLevel = 2, _cureLevel = 0;

    // Shared mutable Theme Brushes (resolved from App.xaml so XAML & C# stay 100% synced)
    private readonly SolidColorBrush AccentBrush;
    private readonly SolidColorBrush AccentLightBrush;
    private readonly SolidColorBrush AccentTextBrush;
    private readonly SolidColorBrush AccentBgBrush;
    private readonly SolidColorBrush AccentBorderBrush;
    private readonly SolidColorBrush CardBgBrush;
    private readonly SolidColorBrush CardBorderBrush;
    private readonly SolidColorBrush SubtleBgBrush;
    private readonly SolidColorBrush HeaderBgBrush;
    private readonly SolidColorBrush MutedBrush;
    private readonly SolidColorBrush MutedBgBrush;
    private readonly SolidColorBrush MutedBorderBrush;
    private readonly SolidColorBrush TransparentBrush;
    private readonly SolidColorBrush TextPrimaryBrush;
    private readonly SolidColorBrush TextSecondaryBrush;
    private readonly SolidColorBrush TextDimBrush;

    public MainWindow()
    {
        InitializeComponent();

        var res = Application.Current.Resources;
        AccentBrush = (SolidColorBrush)res["V4WaAccentBrush"];
        AccentLightBrush = (SolidColorBrush)res["V4WaAccentLightBrush"];
        AccentTextBrush = (SolidColorBrush)res["V4WaAccentTextBrush"];
        AccentBgBrush = (SolidColorBrush)res["V4WaAccentBgBrush"];
        AccentBorderBrush = (SolidColorBrush)res["V4WaAccentBorderBrush"];
        CardBgBrush = (SolidColorBrush)res["V4WaCardBgBrush"];
        CardBorderBrush = (SolidColorBrush)res["V4WaCardBorderBrush"];
        SubtleBgBrush = (SolidColorBrush)res["V4WaSubtleBgBrush"];
        HeaderBgBrush = (SolidColorBrush)res["V4WaHeaderBgBrush"];

        MutedBrush = new(Color.FromArgb(255, 113, 113, 122));
        MutedBgBrush = new(Color.FromArgb(255, 39, 39, 42));
        MutedBorderBrush = new(Color.FromArgb(255, 63, 63, 70));
        TransparentBrush = new(Colors.Transparent);
        TextPrimaryBrush = new(Colors.White);
        TextSecondaryBrush = new(Color.FromArgb(255, 161, 161, 170));
        TextDimBrush = new(Color.FromArgb(255, 212, 212, 216));

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new SizeInt32(1260, 880));

        // Listen for live Windows Accent Color changes when Auto Sync is ON
        _uiSettings.ColorValuesChanged += (sender, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (AutoAccentToggle.IsOn)
                {
                    ApplyAccentColor(_uiSettings.GetColorValue(UIColorType.Accent), syncPicker: true);
                }
            });
        };

        _eq18 = Interpolate10To18(_eq10);
        RebuildPinnedPresets();
        RebuildEqSliders();
        BuildAllDspCards();
        LayoutDspCards();
        ApplyAccentColor(_accentBase, syncPicker: true);
        ApplySurfaceTheme("Acrylic");
        SelectSettingsCategory("Appearance");
        ApplyPreset("Hi-Res Audiophile");
    }

    // ==================== APPEARANCE / THEME ENGINE ====================

    private static Color BlendColor(Color a, Color b, double t)
    {
        byte lerp(byte x, byte y) => (byte)Math.Clamp(Math.Round(x + (y - x) * t), 0, 255);
        return Color.FromArgb(255, lerp(a.R, b.R), lerp(a.G, b.G), lerp(a.B, b.B));
    }

    private void ApplyAccentColor(Color baseColor, bool syncPicker)
    {
        _accentBase = Color.FromArgb(255, baseColor.R, baseColor.G, baseColor.B);
        var light = BlendColor(_accentBase, Colors.White, 0.28);
        var text = BlendColor(_accentBase, Colors.White, 0.62);
        var dark = BlendColor(_accentBase, Colors.Black, 0.22);

        AccentBrush.Color = _accentBase;
        AccentLightBrush.Color = light;
        AccentTextBrush.Color = text;
        AccentBgBrush.Color = Color.FromArgb(48, _accentBase.R, _accentBase.G, _accentBase.B);
        AccentBorderBrush.Color = Color.FromArgb(145, light.R, light.G, light.B);

        // Mutate native WinUI 3 ToggleSwitch & Slider brushes live
        var res = Application.Current.Resources;
        if (res["ToggleSwitchFillOn"] is SolidColorBrush t1) t1.Color = _accentBase;
        if (res["ToggleSwitchFillOnPointerOver"] is SolidColorBrush t2) t2.Color = light;
        if (res["ToggleSwitchFillOnPressed"] is SolidColorBrush t3) t3.Color = dark;
        if (res["SliderTrackValueFill"] is SolidColorBrush s1) s1.Color = _accentBase;
        if (res["SliderTrackValueFillPointerOver"] is SolidColorBrush s2) s2.Color = light;
        if (res["SliderTrackValueFillPressed"] is SolidColorBrush s3) s3.Color = dark;
        if (res["SliderThumbBackgroundPressed"] is SolidColorBrush s4) s4.Color = light;

        ActiveAccentHexText.Text = $"#{_accentBase.R:X2}{_accentBase.G:X2}{_accentBase.B:X2} · RGB({_accentBase.R}, {_accentBase.G}, {_accentBase.B})";

        if (syncPicker)
        {
            bool prev = _isUpdating;
            _isUpdating = true;
            AccentColorPicker.Color = _accentBase;
            _isUpdating = prev;
        }

        if (!_isUpdating) RefreshVisualStates();
    }

    private void ApplySurfaceTheme(string theme)
    {
        _activeTheme = theme;

        try
        {
            if (theme == "Acrylic" && DesktopAcrylicController.IsSupported())
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }
            else
            {
                SystemBackdrop = null;
            }
        }
        catch
        {
            SystemBackdrop = null;
        }

        if (theme == "DarkOLED")
        {
            RootBackdropGrid.Background = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
            HeaderBgBrush.Color = Color.FromArgb(255, 6, 6, 8);
            CardBgBrush.Color = Color.FromArgb(255, 14, 14, 18);
            CardBorderBrush.Color = Color.FromArgb(255, 52, 52, 64);
            SubtleBgBrush.Color = Color.FromArgb(255, 8, 8, 11);
        }
        else // Acrylic (Default — high contrast, crisp continuous borders)
        {
            RootBackdropGrid.Background = new SolidColorBrush(Color.FromArgb(84, 10, 12, 18));
            HeaderBgBrush.Color = Color.FromArgb(172, 16, 18, 24);
            CardBgBrush.Color = Color.FromArgb(188, 24, 26, 33);
            CardBorderBrush.Color = Color.FromArgb(96, 255, 255, 255);
            SubtleBgBrush.Color = Color.FromArgb(176, 14, 16, 22);
        }

        UpdateThemePresetButtons();
        if (!_isUpdating) RefreshVisualStates();
    }

    private void UpdateThemePresetButtons()
    {
        var map = new (Button Btn, string Key)[]
        {
            (ThemeAcrylicBtn, "Acrylic"),
            (ThemeOledBtn, "DarkOLED")
        };

        foreach (var (btn, key) in map)
        {
            bool selected = _activeTheme == key;
            btn.Background = selected ? AccentBgBrush : SubtleBgBrush;
            btn.BorderBrush = selected ? AccentBorderBrush : CardBorderBrush;
        }
    }

    private void SelectSettingsCategory(string category)
    {
        _activeSettingsCategory = category;

        SettingsAppearancePage.Visibility = category == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        SettingsDevicesPage.Visibility = category == "Devices" ? Visibility.Visible : Visibility.Collapsed;
        SettingsAboutPage.Visibility = category == "About" ? Visibility.Visible : Visibility.Collapsed;

        var items = new (Button Btn, Border Pill, FontIcon Icon, string Key)[]
        {
            (NavAppearanceBtn, NavAppearancePill, NavAppearanceIcon, "Appearance"),
            (NavDevicesBtn, NavDevicesPill, NavDevicesIcon, "Devices"),
            (NavAboutBtn, NavAboutPill, NavAboutIcon, "About")
        };

        foreach (var (btn, pill, icon, key) in items)
        {
            bool active = category == key;
            btn.Background = active ? CardBgBrush : TransparentBrush;
            pill.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            icon.Foreground = active ? AccentLightBrush : TextSecondaryBrush;
        }
    }

    // ==================== HELPER BUILDERS ====================

    private (StackPanel Panel, Slider Slider, TextBlock ValueText) CreateSliderRow(
        string label, string tooltip, double min, double max, double val, double step, Func<double, string> formatter)
    {
        var panel = new StackPanel { Spacing = 3 };
        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(new TextBlock { Text = label, FontSize = 13, Foreground = TextDimBrush, TextTrimming = TextTrimming.CharacterEllipsis });
        if (!string.IsNullOrEmpty(tooltip))
        {
            var tipIcon = new FontIcon { Glyph = "\uE946", FontSize = 12, Foreground = MutedBrush };
            ToolTipService.SetToolTip(tipIcon, tooltip);
            left.Children.Add(tipIcon);
        }

        var valText = new TextBlock
        {
            Text = formatter(val),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetColumn(left, 0);
        Grid.SetColumn(valText, 1);
        header.Children.Add(left);
        header.Children.Add(valText);

        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = val,
            StepFrequency = step
        };
        slider.ValueChanged += (_, e) =>
        {
            valText.Text = formatter(e.NewValue);
            if (!_isUpdating) RefreshVisualStates();
        };

        panel.Children.Add(header);
        panel.Children.Add(slider);
        return (panel, slider, valText);
    }

    private (Border Container, Button[] Buttons) CreateSegmentedButtons(string[] labels, Func<int> getter, Action<int> setter)
    {
        var border = new Border
        {
            Background = SubtleBgBrush,
            BorderBrush = CardBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(3)
        };
        var grid = new Grid { ColumnSpacing = 4 };
        var buttons = new Button[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int idx = i;
            var btn = new Button
            {
                Content = labels[i],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 32,
                CornerRadius = new CornerRadius(4),
                FontSize = 12,
                Padding = new Thickness(6, 0, 6, 0),
                BorderThickness = new Thickness(1)
            };
            btn.Click += (_, _) =>
            {
                setter(idx);
                RefreshVisualStates();
            };
            Grid.SetColumn(btn, i);
            grid.Children.Add(btn);
            buttons[i] = btn;
        }

        border.Child = grid;
        return (border, buttons);
    }

    private (Border Container, Button[] Buttons) CreateStackedRowButtons(string[] labels, Func<int> getter, Action<int> setter)
    {
        var border = new Border
        {
            Background = SubtleBgBrush,
            BorderBrush = CardBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4)
        };
        var stack = new StackPanel { Spacing = 4 };
        var buttons = new Button[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            var btn = new Button
            {
                Content = labels[i],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Height = 34,
                CornerRadius = new CornerRadius(4),
                FontSize = 12.5,
                Padding = new Thickness(12, 0, 12, 0),
                BorderThickness = new Thickness(1)
            };
            btn.Click += (_, _) =>
            {
                setter(idx);
                RefreshVisualStates();
            };
            stack.Children.Add(btn);
            buttons[i] = btn;
        }

        border.Child = stack;
        return (border, buttons);
    }

    private DspCardEntry RegisterCard(
        string id, int order, string glyph, string title, string tooltip,
        bool hasToggle, Action<StackPanel, DspCardEntry> buildBody)
    {
        var icon = new FontIcon { Glyph = glyph, FontSize = 15, Foreground = AccentLightBrush };
        var iconBox = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(6),
            Background = AccentBgBrush,
            BorderBrush = AccentBorderBrush,
            BorderThickness = new Thickness(1),
            Child = icon
        };
        var titleBlock = new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = TextPrimaryBrush, VerticalAlignment = VerticalAlignment.Center };

        var leftHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        leftHeader.Children.Add(iconBox);
        leftHeader.Children.Add(titleBlock);
        if (!string.IsNullOrEmpty(tooltip))
        {
            var tipIcon = new FontIcon { Glyph = "\uE946", FontSize = 13, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center };
            ToolTipService.SetToolTip(tipIcon, tooltip);
            leftHeader.Children.Add(tipIcon);
        }

        var pinBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE718", FontSize = 12 },
            Padding = new Thickness(0),
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(4),
            Background = SubtleBgBrush,
            BorderBrush = CardBorderBrush,
            BorderThickness = new Thickness(1),
            Foreground = MutedBrush
        };
        ToolTipService.SetToolTip(pinBtn, "Pin module to top");
        pinBtn.Click += (_, _) =>
        {
            if (_pinnedModules.Contains(id)) _pinnedModules.Remove(id);
            else _pinnedModules.Add(id);
            LayoutDspCards();
            RefreshVisualStates();
        };

        ToggleSwitch? toggle = null;
        var rightHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        rightHeader.Children.Add(pinBtn);
        if (hasToggle)
        {
            toggle = new ToggleSwitch { IsOn = false, OnContent = "", OffContent = "", MinWidth = 0 };
            toggle.Toggled += (_, _) => { if (!_isUpdating) RefreshVisualStates(); };
            rightHeader.Children.Add(toggle);
        }

        var headerGrid = new Grid { ColumnSpacing = 10 };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(leftHeader, 0);
        Grid.SetColumn(rightHeader, 1);
        headerGrid.Children.Add(leftHeader);
        headerGrid.Children.Add(rightHeader);

        var bodyStack = new StackPanel { Spacing = 12 };
        bodyStack.Children.Add(headerGrid);

        var root = new Border
        {
            Background = CardBgBrush,
            BorderBrush = CardBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = bodyStack
        };

        var entry = new DspCardEntry
        {
            Id = id,
            DefaultOrder = order,
            Root = root,
            IconBox = iconBox,
            Icon = icon,
            PinBtn = pinBtn,
            Toggle = toggle,
            IsActive = () => toggle == null ? MasterToggle.IsOn : (MasterToggle.IsOn && toggle.IsOn)
        };

        buildBody(bodyStack, entry);
        _cards.Add(entry);
        return entry;
    }

    // ==================== BUILD ALL 16 DSP CARDS (+ EQ = 17) ====================

    private void BuildAllDspCards()
    {
        // 1. ViPER Bass
        RegisterCard("bass", 1, "\uE7F5", "ViPER Bass",
            "Enhances low-end weight and punch.\n• Natural: clean sub-bass shelf\n• PureBass+: tight punch with 2nd harmonics\n• Subwoofer: deep sub-harmonic rumble",
            true, (body, card) =>
            {
                _bassToggle = card.Toggle!;
                var (seg, btns) = CreateSegmentedButtons(new[] { "Natural", "PureBass+", "Subwoofer" }, () => _bassMode, v => _bassMode = v);
                var freq = CreateSliderRow("Bass Frequency", "Cutoff frequency (30–120 Hz) for bass enhancement.", 30, 120, 60, 5, v => $"{v:0} Hz");
                var gain = CreateSliderRow("Bass Gain", "Boost intensity in dB.", 0, 15, 3.5, 0.5, v => $"+{v:0.0} dB");
                _bassFreqSlider = freq.Slider;
                _bassGainSlider = gain.Slider;
                body.Children.Add(seg);
                body.Children.Add(freq.Panel);
                body.Children.Add(gain.Panel);
                card.RefreshVisual = active =>
                {
                    StyleModeButtons(btns, _bassMode, active);
                    freq.Slider.Opacity = gain.Slider.Opacity = active ? 1.0 : 0.45;
                };
            });

        // 2. ViPER Clarity
        RegisterCard("clarity", 2, "\uE767", "ViPER Clarity",
            "Restores vocal presence and micro-detail.\n• Natural: transparent upper-mid lift\n• OZone+: sparkling vocal definition\n• XHiFi: crisp high-frequency air",
            true, (body, card) =>
            {
                _clarityToggle = card.Toggle!;
                var (seg, btns) = CreateSegmentedButtons(new[] { "Natural", "OZone+", "XHiFi" }, () => _clarityMode, v => _clarityMode = v);
                var gain = CreateSliderRow("Clarity Gain", "Detail restoration strength (0–15 dB).", 0, 15, 3.5, 0.5, v => $"+{v:0.0} dB");
                _clarityGainSlider = gain.Slider;
                body.Children.Add(seg);
                body.Children.Add(gain.Panel);
                body.Children.Add(new TextBlock { Text = "Restores micro-detail and upper-harmonic air without harsh sibilance.", FontSize = 12, Foreground = MutedBrush, TextWrapping = TextWrapping.Wrap });
                card.RefreshVisual = active =>
                {
                    StyleModeButtons(btns, _clarityMode, active);
                    gain.Slider.Opacity = active ? 1.0 : 0.45;
                };
            });

        // 3. Dynamic System (HP removed!)
        RegisterCard("dynamic", 3, "\uE9D2", "Dynamic System",
            "Optimizes dynamic excursion and bass response for your listening hardware.",
            true, (body, card) =>
            {
                _dynToggle = card.Toggle!;
                var (stacked, btns) = CreateStackedRowButtons(new[] { "Common", "Extreme", "High-End", "Studio" }, () => _dynDevice, v => _dynDevice = v);
                var bass = CreateSliderRow("Dynamic Bass", "Transient bass punch percentage (0–100%).", 0, 100, 25, 5, v => $"{v:0}%");
                _dynBassSlider = bass.Slider;
                body.Children.Add(stacked);
                body.Children.Add(bass.Panel);
                card.RefreshVisual = active =>
                {
                    StyleModeButtons(btns, _dynDevice, active);
                    bass.Slider.Opacity = active ? 1.0 : 0.45;
                };
            });

        // 4. Output Gain, Pan & Limiter
        RegisterCard("master", 4, "\uE83D", "Output Gain, Pan & Limiter",
            "Controls master output volume, L/R channel balance, and zero-clipping brickwall limiter.",
            false, (body, card) =>
            {
                var gain = CreateSliderRow("Master Gain", "Pre-limiter output level (-24 to +12 dB).", -24, 12, 0, 0.5, v => v > 0 ? $"+{v:0.0} dB" : $"{v:0.0} dB");
                var pan = CreateSliderRow("Output Pan", "Stereo channel balance (-100% Left to +100% Right).", -100, 100, 0, 5, v => Math.Abs(v) < 1 ? "Center" : (v < 0 ? $"L {Math.Abs(v):0}%" : $"R {v:0}%"));
                _masterGainSlider = gain.Slider;
                _panSlider = pan.Slider;

                var limRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
                limRow.Children.Add(new TextBlock { Text = "Brickwall Limiter", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TextPrimaryBrush, VerticalAlignment = VerticalAlignment.Center });
                _limiterToggle = new ToggleSwitch { IsOn = true, OnContent = "", OffContent = "", MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Right };
                _limiterToggle.Toggled += (_, _) => { if (!_isUpdating) RefreshVisualStates(); };
                limRow.Children.Add(_limiterToggle);

                var limThresh = CreateSliderRow("Limiter Threshold", "Hard ceiling preventing digital clipping (-6.0 to 0.0 dBFS).", -6, 0, -0.3, 0.1, v => $"{v:0.0} dBFS");
                _limiterSlider = limThresh.Slider;

                body.Children.Add(gain.Panel);
                body.Children.Add(pan.Panel);
                body.Children.Add(limRow);
                body.Children.Add(limThresh.Panel);
                card.RefreshVisual = active =>
                {
                    gain.Slider.Opacity = pan.Slider.Opacity = active ? 1.0 : 0.45;
                    limThresh.Slider.Opacity = (active && _limiterToggle.IsOn) ? 1.0 : 0.45;
                };
            });

        // 5. AnalogX
        RegisterCard("analogx", 5, "\uE950", "AnalogX",
            "Models dense, smooth solid-state harmonic saturation—tighter and more balanced than 6N1J tube warmth.",
            true, (body, card) =>
            {
                _analogToggle = card.Toggle!;
                body.Children.Add(new TextBlock { Text = "Solid-state harmonic signature for richer body and analog smoothness.", FontSize = 12, Foreground = TextSecondaryBrush, TextWrapping = TextWrapping.Wrap });
                var (seg, btns) = CreateSegmentedButtons(new[] { "Slight", "Moderate", "Extreme" }, () => _analogMode, v => _analogMode = v);
                body.Children.Add(seg);
                card.RefreshVisual = active => StyleModeButtons(btns, _analogMode, active);
            });

        // 6. Tube Simulator (6N1J) — keep (6N1J) per user request
        RegisterCard("tube", 6, "\uEA80", "Tube Simulator (6N1J)",
            "Simulates even-order vacuum tube harmonics of a classic 6N1J preamp for lush, warm vocals.",
            true, (body, card) =>
            {
                _tubeToggle = card.Toggle!;
                var drive = CreateSliderRow("Harmonic Drive", "Even-order tube saturation intensity (0–100%).", 0, 100, 25, 1, v => $"{v:0}%");
                _tubeDriveSlider = drive.Slider;
                body.Children.Add(drive.Panel);
                body.Children.Add(new TextBlock { Text = "Softens digital edge with warm vacuum-tube harmonics.", FontSize = 12, Foreground = MutedBrush, TextWrapping = TextWrapping.Wrap });
                card.RefreshVisual = active => drive.Slider.Opacity = active ? 1.0 : 0.45;
            });

        // 7. ViPER-DDC
        RegisterCard("ddc", 7, "\uE7F6", "ViPER-DDC",
            "Loads ViPER4Android / AutoEQ .vdc biquad profiles to correct headphone frequency response up to 384 kHz.",
            true, (body, card) =>
            {
                _ddcToggle = card.Toggle!;
                var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, Height = 34, MinHeight = 34, CornerRadius = new CornerRadius(4), Background = SubtleBgBrush, BorderBrush = CardBorderBrush, SelectedIndex = 0 };
                combo.Items.Add(new ComboBoxItem { Content = "Moondrop Blessing 3 Harman.vdc (Demo)" });
                combo.Items.Add(new ComboBoxItem { Content = "Sennheiser HD 600 AutoEQ.vdc (Demo)" });
                combo.Items.Add(new ComboBoxItem { Content = "Sony IER-M9 Reference.vdc (Demo)" });
                var uploadBtn = new Button { Content = "Upload .vdc Profile", HorizontalAlignment = HorizontalAlignment.Stretch, Height = 34, CornerRadius = new CornerRadius(4), Background = SubtleBgBrush, BorderBrush = CardBorderBrush, Tag = "VDC file picker opened (dummy preview)" };
                uploadBtn.Click += DummyAction_Click;
                body.Children.Add(combo);
                body.Children.Add(uploadBtn);
                card.RefreshVisual = active => combo.Opacity = uploadBtn.Opacity = active ? 1.0 : 0.5;
            });

        // 8. Convolver
        RegisterCard("convolver", 8, "\uE9F5", "Convolver",
            "Partitioned FFT convolution engine for .irs and .wav impulse responses with automatic Hi-Res resampling.",
            true, (body, card) =>
            {
                _convToggle = card.Toggle!;
                var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, Height = 34, MinHeight = 34, CornerRadius = new CornerRadius(4), Background = SubtleBgBrush, BorderBrush = CardBorderBrush, SelectedIndex = 0 };
                combo.Items.Add(new ComboBoxItem { Content = "Dolby Pro Logic II Studio.irs (Demo)" });
                combo.Items.Add(new ComboBoxItem { Content = "Sony ClearAudio+ Hi-Res.irs (Demo)" });
                combo.Items.Add(new ComboBoxItem { Content = "BBE Sonic Maximizer.irs (Demo)" });
                var uploadBtn = new Button { Content = "Upload .irs / .wav File", HorizontalAlignment = HorizontalAlignment.Stretch, Height = 34, CornerRadius = new CornerRadius(4), Background = SubtleBgBrush, BorderBrush = CardBorderBrush, Tag = "IRS/WAV file picker opened (dummy preview)" };
                uploadBtn.Click += DummyAction_Click;
                var cross = CreateSliderRow("Cross Channel", "Blends L/R channels across the impulse response (0–100%).", 0, 100, 0, 5, v => $"{v:0}%");
                var wet = CreateSliderRow("Wet Mix", "Convolver dry/wet balance (0–100%).", 0, 100, 100, 5, v => $"{v:0}%");
                _convCrossSlider = cross.Slider;
                _convWetSlider = wet.Slider;
                body.Children.Add(combo);
                body.Children.Add(uploadBtn);
                body.Children.Add(cross.Panel);
                body.Children.Add(wet.Panel);
                card.RefreshVisual = active =>
                {
                    combo.Opacity = uploadBtn.Opacity = active ? 1.0 : 0.5;
                    cross.Slider.Opacity = wet.Slider.Opacity = active ? 1.0 : 0.45;
                };
            });

        // 9. Spectrum Extension (no parenthetical)
        RegisterCard("vse", 9, "\uE9D9", "Spectrum Extension",
            "Synthesizes missing upper-harmonic air above 8 kHz for compressed streams (MP3, AAC, YouTube, Spotify).",
            true, (body, card) =>
            {
                _vseToggle = card.Toggle!;
                var str = CreateSliderRow("Effect Strength", "High-frequency harmonic exciter strength (0–100%).", 0, 100, 50, 5, v => $"{v:0}%");
                _vseSlider = str.Slider;
                body.Children.Add(str.Panel);
                body.Children.Add(new TextBlock { Text = "Reconstructs lost high-frequency sparkle in lossy audio.", FontSize = 12, Foreground = MutedBrush, TextWrapping = TextWrapping.Wrap });
                card.RefreshVisual = active => str.Slider.Opacity = active ? 1.0 : 0.45;
            });

        // 10. Differential Surround
        RegisterCard("surround", 10, "\uE8D6", "Differential Surround",
            "Expands stereo soundstage using subtle Haas inter-aural delay and Mid/Side stereo widening.",
            true, (body, card) =>
            {
                _surroundToggle = card.Toggle!;
                var delay = CreateSliderRow("Haas Delay", "Inter-channel delay (0–20 ms).", 0, 20, 6.0, 0.5, v => $"{v:0.0} ms");
                var width = CreateSliderRow("Stereo Width", "Mid/Side expansion (0% Mono, 100% Normal, 200% Wide).", 0, 200, 100, 5, v => $"{v:0}%");
                _surroundDelaySlider = delay.Slider;
                _stereoWidthSlider = width.Slider;
                body.Children.Add(delay.Panel);
                body.Children.Add(width.Panel);
                card.RefreshVisual = active => delay.Slider.Opacity = width.Slider.Opacity = active ? 1.0 : 0.45;
            });

        // 11. Field Surround
        RegisterCard("field", 11, "\uE774", "Field Surround",
            "Psychoacoustic soundstage enhancer with independent center vocal image control.",
            true, (body, card) =>
            {
                _fieldToggle = card.Toggle!;
                var str = CreateSliderRow("Surround Strength", "Ambient spatial width (0–100%).", 0, 100, 40, 5, v => $"{v:0}%");
                var mid = CreateSliderRow("Mid Image", "Center vocal focus and stability (0–100%).", 0, 100, 50, 5, v => $"{v:0}%");
                _fieldStrengthSlider = str.Slider;
                _fieldMidSlider = mid.Slider;
                body.Children.Add(str.Panel);
                body.Children.Add(mid.Panel);
                card.RefreshVisual = active => str.Slider.Opacity = mid.Slider.Opacity = active ? 1.0 : 0.45;
            });

        // 12. Headphone Surround+ (no parenthetical)
        RegisterCard("vhs", 12, "\uE7F6", "Headphone Surround+",
            "5-level early-reflection virtualizer simulating out-of-head loudspeaker staging on headphones.",
            true, (body, card) =>
            {
                _vhsToggle = card.Toggle!;
                body.Children.Add(new TextBlock { Text = "Select room reflection depth from intimate studio (Lv 1) to concert hall (Lv 5).", FontSize = 12, Foreground = TextSecondaryBrush, TextWrapping = TextWrapping.Wrap });
                var (seg, btns) = CreateSegmentedButtons(new[] { "Lv 1", "Lv 2", "Lv 3", "Lv 4", "Lv 5" }, () => _vhsLevel - 1, v => _vhsLevel = v + 1);
                body.Children.Add(seg);
                card.RefreshVisual = active => StyleModeButtons(btns, _vhsLevel - 1, active);
            });

        // 13. Reverberation
        RegisterCard("reverb", 13, "\uE8F1", "Reverberation",
            "Sample-rate invariant 8-comb + 4-allpass stereo room simulator.",
            true, (body, card) =>
            {
                _reverbToggle = card.Toggle!;
                var room = CreateSliderRow("Room Size", "Virtual acoustic chamber size.", 0, 100, 35, 5, v => $"{v:0}%");
                var width = CreateSliderRow("Sound Field Width", "Stereo spread of reverb tails.", 0, 100, 50, 5, v => $"{v:0}%");
                var damp = CreateSliderRow("Damping", "High-frequency wall absorption.", 0, 100, 25, 5, v => $"{v:0}%");
                var wet = CreateSliderRow("Wet Signal", "Reverb reflection mix level.", 0, 100, 20, 5, v => $"{v:0}%");
                var dry = CreateSliderRow("Dry Signal", "Direct original signal level.", 0, 100, 85, 5, v => $"{v:0}%");
                _revRoomSlider = room.Slider;
                _revWidthSlider = width.Slider;
                _revDampSlider = damp.Slider;
                _revWetSlider = wet.Slider;
                _revDrySlider = dry.Slider;
                body.Children.Add(room.Panel);
                body.Children.Add(width.Panel);
                body.Children.Add(damp.Panel);
                body.Children.Add(wet.Panel);
                body.Children.Add(dry.Panel);
                card.RefreshVisual = active =>
                    room.Slider.Opacity = width.Slider.Opacity = damp.Slider.Opacity = wet.Slider.Opacity = dry.Slider.Opacity = active ? 1.0 : 0.45;
            });

        // 14. FET Compressor
        RegisterCard("comp", 14, "\uE9D5", "FET Compressor",
            "Analog-style FET dynamic range compressor for punchy, consistent loudness.",
            true, (body, card) =>
            {
                _compToggle = card.Toggle!;
                var thresh = CreateSliderRow("Threshold", "Level above which compression engages.", -40, 0, -18, 1, v => $"{v:0} dB");
                var ratio = CreateSliderRow("Ratio", "Compression ratio (1:1 to 20:1).", 1, 20, 3, 0.5, v => $"{v:0.0}:1");
                var atk = CreateSliderRow("Attack", "Response time to transients.", 1, 100, 10, 1, v => $"{v:0} ms");
                var rel = CreateSliderRow("Release", "Recovery time after peak.", 20, 500, 120, 10, v => $"{v:0} ms");
                var makeup = CreateSliderRow("Makeup Gain", "Post-compression level boost.", 0, 18, 3, 0.5, v => $"+{v:0.0} dB");
                _compThreshSlider = thresh.Slider;
                _compRatioSlider = ratio.Slider;
                _compAtkSlider = atk.Slider;
                _compRelSlider = rel.Slider;
                _compMakeupSlider = makeup.Slider;
                body.Children.Add(thresh.Panel);
                body.Children.Add(ratio.Panel);
                body.Children.Add(atk.Panel);
                body.Children.Add(rel.Panel);
                body.Children.Add(makeup.Panel);
                card.RefreshVisual = active =>
                    thresh.Slider.Opacity = ratio.Slider.Opacity = atk.Slider.Opacity = rel.Slider.Opacity = makeup.Slider.Opacity = active ? 1.0 : 0.45;
            });

        // 15. Playback Gain Control (no parenthetical)
        RegisterCard("agc", 15, "\uE767", "Playback Gain Control",
            "Automatically levels quiet and loud tracks to a comfortable target loudness.",
            true, (body, card) =>
            {
                _agcToggle = card.Toggle!;
                var str = CreateSliderRow("Effect Strength", "Leveling responsiveness (0–100%).", 0, 100, 50, 5, v => $"{v:0}%");
                var maxG = CreateSliderRow("Max Gain", "Maximum boost applied to quiet tracks.", 0, 24, 12, 1, v => $"+{v:0} dB");
                var thresh = CreateSliderRow("Output Threshold", "Target output loudness ceiling.", -12, 0, -1, 0.5, v => $"{v:0.0} dBFS");
                _agcStrengthSlider = str.Slider;
                _agcMaxGainSlider = maxG.Slider;
                _agcThreshSlider = thresh.Slider;
                body.Children.Add(str.Panel);
                body.Children.Add(maxG.Panel);
                body.Children.Add(thresh.Panel);
                card.RefreshVisual = active => str.Slider.Opacity = maxG.Slider.Opacity = thresh.Slider.Opacity = active ? 1.0 : 0.45;
            });

        // 16. Cure Tech+ (no parenthetical)
        RegisterCard("cure", 16, "\uE95E", "Cure Tech+",
            "Binaural 0.26 ms ITD crossfeed that reduces long-session headphone listening fatigue.",
            true, (body, card) =>
            {
                _cureToggle = card.Toggle!;
                body.Children.Add(new TextBlock { Text = "Blends subtle opposite-channel cues for natural loudspeaker-like comfort.", FontSize = 12, Foreground = TextSecondaryBrush, TextWrapping = TextWrapping.Wrap });
                var (seg, btns) = CreateSegmentedButtons(new[] { "Slight", "Moderate", "Extreme" }, () => _cureLevel, v => _cureLevel = v);
                body.Children.Add(seg);
                card.RefreshVisual = active => StyleModeButtons(btns, _cureLevel, active);
            });
    }

    private void LayoutDspCards()
    {
        int cols = _currentWindowWidth < 820 ? 1 : 2;
        _lastModuleCols = cols;

        ModulesGrid.Children.Clear();
        ModulesGrid.ColumnDefinitions.Clear();
        ModulesGrid.RowDefinitions.Clear();

        for (int c = 0; c < cols; c++)
            ModulesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var ordered = _cards
            .OrderBy(c =>
            {
                int pinIdx = _pinnedModules.IndexOf(c.Id);
                return pinIdx >= 0 ? pinIdx : 100 + c.DefaultOrder;
            })
            .ToList();

        int rows = (ordered.Count + cols - 1) / cols;
        for (int r = 0; r < rows; r++)
            ModulesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < ordered.Count; i++)
        {
            var card = ordered[i];
            Grid.SetRow(card.Root, i / cols);
            Grid.SetColumn(card.Root, i % cols);
            ModulesGrid.Children.Add(card.Root);
        }
    }

    // ==================== PRESETS & EQUALIZER ====================

    private void RebuildPinnedPresets()
    {
        PinnedPresetsGrid.Children.Clear();
        PinnedPresetsGrid.ColumnDefinitions.Clear();
        PinnedPresetsGrid.RowDefinitions.Clear();

        if (_pinnedPresets.Count == 0)
        {
            PinnedPresetsGrid.Visibility = Visibility.Collapsed;
            return;
        }

        PinnedPresetsGrid.Visibility = Visibility.Visible;
        int maxCols = _currentWindowWidth >= 960 ? 4 : (_currentWindowWidth >= 560 ? 2 : 1);
        int cols = Math.Min(maxCols, _pinnedPresets.Count);
        _lastPresetCols = maxCols;

        for (int c = 0; c < cols; c++)
            PinnedPresetsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int rows = (_pinnedPresets.Count + cols - 1) / cols;
        for (int r = 0; r < rows; r++)
            PinnedPresetsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < _pinnedPresets.Count; i++)
        {
            string name = _pinnedPresets[i];
            var meta = BuiltinPresets.FirstOrDefault(p => p.Name == name);

            bool active = _selectedPreset == name;
            var btn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(14, 10, 14, 10),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Background = active ? AccentBgBrush : CardBgBrush,
                BorderBrush = active ? AccentBorderBrush : CardBorderBrush
            };

            var stack = new StackPanel { Spacing = 2 };
            stack.Children.Add(new TextBlock { Text = name, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = TextPrimaryBrush });
            stack.Children.Add(new TextBlock { Text = meta.Subtitle ?? "Custom preset", FontSize = 11.5, Foreground = TextSecondaryBrush, TextTrimming = TextTrimming.CharacterEllipsis });
            btn.Content = stack;
            btn.Click += (_, _) => ApplyPreset(name);

            Grid.SetRow(btn, i / cols);
            Grid.SetColumn(btn, i % cols);
            PinnedPresetsGrid.Children.Add(btn);
        }
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
        EqTitleText.Text = "Equalizer";

        for (int i = 0; i < labels.Length; i++)
        {
            EqSlidersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 6 };
            var valText = new TextBlock
            {
                Text = FormatDb(values[i]),
                FontFamily = new FontFamily("Consolas"),
                FontSize = _eqMode == 18 ? 10.5 : 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = values[i] != 0 ? AccentLightBrush : TextSecondaryBrush
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
                valText.Foreground = Math.Abs(e.NewValue) > 0.01 && eqActive ? AccentLightBrush : TextSecondaryBrush;
                _isUpdating = true;
                EqPresetCombo.SelectedIndex = 0;
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
        double padX = 16, padY = 10;
        double usableW = w - padX * 2, usableH = h - padY * 2;
        double midY = padY + usableH / 2.0;

        EqCurveCanvas.Children.Add(new Line
        {
            X1 = padX, Y1 = midY, X2 = w - padX, Y2 = midY,
            Stroke = MutedBorderBrush,
            StrokeDashArray = new DoubleCollection { 4, 4 },
            StrokeThickness = 1
        });

        var polyline = new Polyline
        {
            Stroke = active ? AccentLightBrush : MutedBrush,
            StrokeThickness = 2.4,
            StrokeLineJoin = PenLineJoin.Round
        };

        var fillPoints = new PointCollection { new Point(padX, h - padY) };
        for (int i = 0; i < values.Length; i++)
        {
            double x = padX + (double)i / (values.Length - 1) * usableW;
            double norm = (values[i] + 12.0) / 24.0;
            double y = padY + (1.0 - norm) * usableH;
            polyline.Points.Add(new Point(x, y));
            fillPoints.Add(new Point(x, y));
        }
        fillPoints.Add(new Point(w - padX, h - padY));

        var light = AccentLightBrush.Color;
        var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        grad.GradientStops.Add(new GradientStop { Color = active ? Color.FromArgb(70, light.R, light.G, light.B) : Color.FromArgb(35, 113, 113, 122), Offset = 0 });
        grad.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, _accentBase.R, _accentBase.G, _accentBase.B), Offset = 1 });

        EqCurveCanvas.Children.Add(new Polygon { Points = fillPoints, Fill = grad });
        EqCurveCanvas.Children.Add(polyline);

        foreach (var pt in polyline.Points)
        {
            var dot = new Ellipse
            {
                Width = 7, Height = 7,
                Fill = active ? TextPrimaryBrush : MutedBrush,
                Stroke = active ? AccentBrush : MutedBorderBrush,
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

        EqToggle.IsOn = false;
        _eq10 = new double[10];
        _masterGainSlider.Value = 0;
        _panSlider.Value = 0;
        _limiterToggle.IsOn = true;
        _limiterSlider.Value = -0.3;
        _bassToggle.IsOn = false; _bassMode = 0; _bassFreqSlider.Value = 60; _bassGainSlider.Value = 3.5;
        _clarityToggle.IsOn = false; _clarityMode = 0; _clarityGainSlider.Value = 3.5;
        _dynToggle.IsOn = false; _dynDevice = 0; _dynBassSlider.Value = 25;
        _tubeToggle.IsOn = false; _tubeDriveSlider.Value = 25;
        _analogToggle.IsOn = false; _analogMode = 0;
        _ddcToggle.IsOn = false;
        _convToggle.IsOn = false; _convCrossSlider.Value = 0; _convWetSlider.Value = 100;
        _vseToggle.IsOn = false; _vseSlider.Value = 50;
        _surroundToggle.IsOn = false; _surroundDelaySlider.Value = 6; _stereoWidthSlider.Value = 100;
        _fieldToggle.IsOn = false; _fieldStrengthSlider.Value = 40; _fieldMidSlider.Value = 50;
        _vhsToggle.IsOn = false; _vhsLevel = 2;
        _reverbToggle.IsOn = false; _revRoomSlider.Value = 35; _revWidthSlider.Value = 50; _revDampSlider.Value = 25; _revWetSlider.Value = 20; _revDrySlider.Value = 85;
        _compToggle.IsOn = false; _compThreshSlider.Value = -18; _compRatioSlider.Value = 3; _compAtkSlider.Value = 10; _compRelSlider.Value = 120; _compMakeupSlider.Value = 3;
        _agcToggle.IsOn = false; _agcStrengthSlider.Value = 50; _agcMaxGainSlider.Value = 12; _agcThreshSlider.Value = -1;
        _cureToggle.IsOn = false; _cureLevel = 0;

        switch (name)
        {
            case "Hi-Res Audiophile":
                EqToggle.IsOn = true;
                _eq10 = new[] { 1.5, 2.0, 0.5, 0, -0.5, 0, 0.5, 1.5, 2.5, 2.0 };
                _bassToggle.IsOn = true; _bassMode = 1; _bassFreqSlider.Value = 55; _bassGainSlider.Value = 3.5;
                _clarityToggle.IsOn = true; _clarityMode = 2; _clarityGainSlider.Value = 3.0;
                _surroundToggle.IsOn = true; _surroundDelaySlider.Value = 3.5; _stereoWidthSlider.Value = 115;
                PresetCombo.SelectedIndex = 1;
                EqPresetCombo.SelectedIndex = 0;
                break;

            case "Deep Bass Boost":
                EqToggle.IsOn = true;
                _eq10 = new[] { 4.5, 5.0, 2.5, 0.5, -1.0, -0.5, 0.5, 1.5, 2.0, 1.5 };
                _bassToggle.IsOn = true; _bassMode = 2; _bassFreqSlider.Value = 65; _bassGainSlider.Value = 7.5;
                _clarityToggle.IsOn = true; _clarityMode = 0; _clarityGainSlider.Value = 2.5;
                PresetCombo.SelectedIndex = 2;
                EqPresetCombo.SelectedIndex = 0;
                break;

            case "Warm Tube Studio":
                EqToggle.IsOn = true;
                _eq10 = new[] { 1.0, 1.5, 1.0, 0.5, 0.5, 1.0, 1.5, 1.0, 0.5, 0 };
                _bassToggle.IsOn = true; _bassMode = 0; _bassFreqSlider.Value = 60; _bassGainSlider.Value = 2.5;
                _clarityToggle.IsOn = true; _clarityMode = 1; _clarityGainSlider.Value = 2.0;
                _tubeToggle.IsOn = true; _tubeDriveSlider.Value = 42;
                _surroundToggle.IsOn = true; _surroundDelaySlider.Value = 5.0; _stereoWidthSlider.Value = 110;
                PresetCombo.SelectedIndex = 3;
                EqPresetCombo.SelectedIndex = 0;
                break;

            default:
                PresetCombo.SelectedIndex = 0;
                EqPresetCombo.SelectedIndex = 1;
                break;
        }

        _eq18 = Interpolate10To18(_eq10);
        SyncEqSliderValues();
        _isUpdating = false;
        RebuildPinnedPresets();
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

        StatusPill.Background = master ? AccentBgBrush : CardBgBrush;
        StatusPill.BorderBrush = master ? AccentBorderBrush : CardBorderBrush;
        StatusDot.Fill = master ? AccentLightBrush : MutedBrush;
        StatusText.Text = master ? "WORKING" : "NOT WORKING";
        StatusText.Foreground = master ? AccentTextBrush : TextSecondaryBrush;

        SettingsBtn.Background = _settingsOpen ? AccentBgBrush : CardBgBrush;
        SettingsBtn.BorderBrush = _settingsOpen ? AccentBorderBrush : CardBorderBrush;
        SettingsGearIcon.Foreground = _settingsOpen ? AccentLightBrush : TextDimBrush;

        bool isPinnedPreset = _pinnedPresets.Contains(_selectedPreset);
        PinPresetBtn.Content = isPinnedPreset ? "Pinned" : "Pin";
        PinPresetBtn.Background = isPinnedPreset ? AccentBgBrush : SubtleBgBrush;
        PinPresetBtn.BorderBrush = isPinnedPreset ? AccentBorderBrush : CardBorderBrush;
        PinPresetBtn.Foreground = isPinnedPreset ? AccentTextBrush : TextDimBrush;

        bool eqActive = master && EqToggle.IsOn;
        EqIcon.Foreground = eqActive ? AccentLightBrush : MutedBrush;
        EqIconBox.Background = eqActive ? AccentBgBrush : SubtleBgBrush;
        EqIconBox.BorderBrush = eqActive ? AccentBorderBrush : CardBorderBrush;
        EqSlidersGrid.Opacity = eqActive ? 1.0 : 0.5;

        var eqVals = _eqMode == 18 ? _eq18 : _eq10;
        for (int i = 0; i < _eqValueLabels.Count && i < eqVals.Length; i++)
            _eqValueLabels[i].Foreground = Math.Abs(eqVals[i]) > 0.01 && eqActive ? AccentLightBrush : TextSecondaryBrush;

        foreach (var card in _cards)
        {
            bool active = card.IsActive();
            card.Icon.Foreground = active ? AccentLightBrush : MutedBrush;
            card.IconBox.Background = active ? AccentBgBrush : SubtleBgBrush;
            card.IconBox.BorderBrush = active ? AccentBorderBrush : CardBorderBrush;
            bool pinned = _pinnedModules.Contains(card.Id);
            card.PinBtn.Background = pinned ? AccentBgBrush : SubtleBgBrush;
            card.PinBtn.BorderBrush = pinned ? AccentBorderBrush : CardBorderBrush;
            card.PinBtn.Foreground = pinned ? AccentLightBrush : MutedBrush;
            card.RefreshVisual(active);
        }

        UpdateThemePresetButtons();
        SelectSettingsCategory(_activeSettingsCategory);
        UpdateEqModeButtons();
        DrawEqCurve();
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
                buttons[i].Background = AccentBgBrush;
                buttons[i].BorderBrush = AccentBorderBrush;
                buttons[i].Foreground = AccentTextBrush;
            }
        }
    }

    private void UpdateEqModeButtons()
    {
        Eq10Btn.Background = _eqMode == 10 ? MutedBgBrush : TransparentBrush;
        Eq10Btn.Foreground = _eqMode == 10 ? AccentLightBrush : TextSecondaryBrush;
        Eq18Btn.Background = _eqMode == 18 ? MutedBgBrush : TransparentBrush;
        Eq18Btn.Foreground = _eqMode == 18 ? AccentLightBrush : TextSecondaryBrush;
    }

    // ==================== EVENT HANDLERS ====================

    private static void SetResponsiveWrap(FrameworkElement element, ColumnDefinition col2, bool wrapToNextRow, bool equalStarWhenWide = false)
    {
        if (wrapToNextRow)
        {
            Grid.SetRow(element, 1);
            Grid.SetColumn(element, 0);
            col2.Width = new GridLength(0);
        }
        else
        {
            Grid.SetRow(element, 0);
            Grid.SetColumn(element, 1);
            col2.Width = equalStarWhenWide ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        }
    }

    private void RootBackdropGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double w = e.NewSize.Width;
        if (w <= 0) return;
        _currentWindowWidth = w;

        // 1. Responsive DSP Modules Grid (2 cols >= 820px, 1 col < 820px)
        int targetModuleCols = w < 820 ? 1 : 2;
        if (targetModuleCols != _lastModuleCols && _cards.Count > 0)
        {
            LayoutDspCards();
        }

        // 2. Responsive Pinned Presets Strip (4 / 2 / 1 cols)
        int targetPresetCols = w >= 960 ? 4 : (w >= 560 ? 2 : 1);
        if (targetPresetCols != _lastPresetCols)
        {
            RebuildPinnedPresets();
        }

        // 3. Responsive Toolbars & Signal Path (CSS flex-wrap style)
        SetResponsiveWrap(SignalBadgesPanel, SignalBadgesCol, w < 760);
        SetResponsiveWrap(PresetActionsPanel, PresetActionsCol, w < 620);
        SetResponsiveWrap(EqActionsPanel, EqActionsCol, w < 900);

        // 4. Settings Sidebar: auto-collapse to compact Icon-Only Rail when width < 760px
        bool compactSidebar = w < 760;
        SettingsSidebarCol.Width = new GridLength(compactSidebar ? 54 : 220);
        var labelVis = compactSidebar ? Visibility.Collapsed : Visibility.Visible;
        NavAppearanceLabel.Visibility = labelVis;
        NavDevicesLabel.Visibility = labelVis;
        NavAboutLabel.Visibility = labelVis;
        SettingsSidebarPanel.Margin = compactSidebar ? new Thickness(0, 4, 10, 0) : new Thickness(0, 4, 16, 0);

        // 5. Settings Pages internal wrapping (Appearance Theme cards, ColorPicker, Devices & APO)
        SetResponsiveWrap(ThemeOledBtn, ThemeOledCol, w < 640, equalStarWhenWide: true);
        SetResponsiveWrap(AccentPickerPanel, AccentPickerCol, w < 920);
        SetResponsiveWrap(DevicesActionsPanel, DevicesActionsCol, w < 820);
        SetResponsiveWrap(DevicesCmd2Border, DevicesCmd2Col, w < 820, equalStarWhenWide: true);
    }

    private void SetSettingsOpen(bool open)
    {
        _settingsOpen = open;
        HeaderAudioInfoPanel.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        HeaderSettingsPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        DspScrollViewer.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        SettingsSplitGrid.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        RefreshVisualStates();
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e) => SetSettingsOpen(!_settingsOpen);

    private void BackToDspBtn_Click(object sender, RoutedEventArgs e) => SetSettingsOpen(false);

    private void SettingsNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string category)
        {
            SelectSettingsCategory(category);
        }
    }

    private void ThemePresetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string theme)
        {
            ApplySurfaceTheme(theme);
            RebuildPinnedPresets();
        }
    }

    private void AutoAccentToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        AccentColorPicker.IsEnabled = !AutoAccentToggle.IsOn;
        if (AutoAccentToggle.IsOn)
        {
            var winAccent = _uiSettings.GetColorValue(UIColorType.Accent);
            ApplyAccentColor(winAccent, syncPicker: true);
        }
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex && hex.Length == 7)
        {
            _isUpdating = true;
            AutoAccentToggle.IsOn = false;
            AccentColorPicker.IsEnabled = true;
            _isUpdating = false;
            byte r = Convert.ToByte(hex.Substring(1, 2), 16);
            byte g = Convert.ToByte(hex.Substring(3, 2), 16);
            byte b = Convert.ToByte(hex.Substring(5, 2), 16);
            ApplyAccentColor(Color.FromArgb(255, r, g, b), syncPicker: true);
        }
    }

    private void AccentColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isUpdating) return;
        if (AutoAccentToggle.IsOn)
        {
            _isUpdating = true;
            AutoAccentToggle.IsOn = false;
            _isUpdating = false;
        }
        ApplyAccentColor(args.NewColor, syncPicker: false);
    }

    private void MasterToggle_Toggled(object sender, RoutedEventArgs e) { if (!_isUpdating) RefreshVisualStates(); }
    private void ModuleToggle_Toggled(object sender, RoutedEventArgs e) { if (!_isUpdating) RefreshVisualStates(); }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (PresetCombo.SelectedItem is ComboBoxItem item && item.Content is string name) ApplyPreset(name);
    }

    private void PinPresetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_pinnedPresets.Contains(_selectedPreset)) _pinnedPresets.Remove(_selectedPreset);
        else _pinnedPresets.Add(_selectedPreset);
        RebuildPinnedPresets();
        RefreshVisualStates();
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
                "Acoustic & Jazz" => new[] { 1.0, 1.5, 1.0, 0.5, 0, 0.5, 1.5, 2.0, 2.5, 2.0 },
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
        EqPresetCombo.SelectedIndex = 1;
        SyncEqSliderValues();
        _isUpdating = false;
        RefreshVisualStates();
    }

    private void EqCurveCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawEqCurve();

    private async void DummyAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string msg)
        {
            ToastBar.Title = "V4Wa";
            ToastBar.Message = msg;
            ToastBar.IsOpen = true;
            await Task.Delay(2400);
            ToastBar.IsOpen = false;
        }
    }
}
