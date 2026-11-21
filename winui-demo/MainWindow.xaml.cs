using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using V4Wa.Demo.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.ViewManagement;
using WinRT.Interop;

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

    private readonly V4WaEngineBridge _bridge = new();
    private DspParamsModel _liveParams = new();
    private UiSettingsModel _uiSettingsModel = new();
    private LiveSourceStatusModel _liveStatus = new();
    private List<AudioDeviceModel> _devices = new();
    private List<PresetModel> _allPresets = new();
    private List<EqPresetModel> _allEqPresets = new();
    private List<string> _irsFiles = new();
    private string _activeIrs = "";
    private List<string> _ddcFiles = new();
    private string _activeDdc = "";
    private readonly DispatcherTimer _pollTimer = new();
    private bool _pollBusy = false;
    private bool _apoOpBusy = false;

    private readonly List<Slider> _eqSliders = new();
    private readonly List<TextBlock> _eqValueLabels = new();
    private readonly List<DspCardEntry> _cards = new();
    private bool _isUpdating = true;
    private bool _settingsOpen = false;
    private string _activeSettingsCategory = "Appearance";
    private double _currentWindowWidth = 1240;
    private int _lastModuleCols = 2;
    private int _lastPresetCols = 4;

    private Color _accentBase = Color.FromArgb(255, 16, 185, 129);
    private readonly UISettings _osUiSettings = new();

    // Module UI handles
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

    private ComboBox _ddcCombo = null!, _irsCombo = null!;

    // Shared mutable Theme Brushes
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

        MutedBrush = new(Color.FromArgb(255, 138, 138, 150));
        MutedBgBrush = new(Color.FromArgb(255, 39, 39, 44));
        MutedBorderBrush = new(Color.FromArgb(255, 68, 68, 78));
        TransparentBrush = new(Colors.Transparent);
        TextPrimaryBrush = new(Colors.White);
        TextSecondaryBrush = new(Color.FromArgb(255, 180, 180, 190));
        TextDimBrush = new(Color.FromArgb(255, 224, 224, 232));

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new SizeInt32(1260, 880));

        // 1. Load saved UI preferences & real live APO parameters
        _uiSettingsModel = _bridge.LoadUiSettings();
        _liveParams = _bridge.ReadLiveParams();
        ReloadPresetsLists();

        // 2. Build all 17 DSP module cards
        BuildAllDspCards();
        LayoutDspCards();

        // 3. Populate IRS / DDC files from C:\ProgramData\ViPERHiRes
        RefreshIrsAndDdcCombos();

        // 4. Apply saved Appearance (Theme + Accent)
        _osUiSettings.ColorValuesChanged += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_uiSettingsModel.AutoAccent)
                {
                    ApplyAccentColor(_osUiSettings.GetColorValue(UIColorType.Accent), syncPicker: true, persist: false);
                }
            });
        };

        AutoAccentToggle.IsOn = _uiSettingsModel.AutoAccent;
        AccentColorPicker.IsEnabled = !_uiSettingsModel.AutoAccent;
        if (_uiSettingsModel.AutoAccent)
            ApplyAccentColor(_osUiSettings.GetColorValue(UIColorType.Accent), syncPicker: true, persist: false);
        else
            ApplyAccentColor(ParseHexColor(_uiSettingsModel.AccentColorHex, Color.FromArgb(255, 16, 185, 129)), syncPicker: true, persist: false);

        ApplySurfaceTheme(_uiSettingsModel.Theme, persist: false);
        SelectSettingsCategory("Appearance");

        // 5. Hydrate all UI controls from real live_params.bin (without overwriting live engine!)
        HydrateUiFromLiveParams();

        // 6. Check foobar2000 plugin status
        FoobarPluginStatusText.Text = _bridge.IsFoobarPluginInstalled()
            ? "INSTALLED · BIT-TRANSPARENT v0.4.1"
            : "NOT DETECTED";

        // 7. Start live telemetry polling (900 ms)
        _pollTimer.Interval = TimeSpan.FromMilliseconds(900);
        _pollTimer.Tick += async (_, _) => await PollLiveTelemetrySafeAsync();
        _pollTimer.Start();
        _ = PollLiveTelemetrySafeAsync();
    }

    // ==================== BUILT-IN PRESETS ====================

    private static List<PresetModel> CreateBuiltinPresets() => new()
    {
        new PresetModel
        {
            Name = "Flat Reference",
            Subtitle = "Transparent bit-clean studio baseline",
            IsBuiltin = true,
            Params = new DspParamsModel()
        },
        new PresetModel
        {
            Name = "Hi-Res Audiophile",
            Subtitle = "Extended sub-bass + XHiFi micro-detail",
            IsBuiltin = true,
            Params = new DspParamsModel
            {
                EqEnabled = true,
                EqBandsDb = new[] { 1.5f, 2.0f, 0.5f, 0f, -0.5f, 0f, 0.5f, 1.5f, 2.5f, 2.0f },
                EqBands18Db = Interpolate10To18(new[] { 1.5f, 2.0f, 0.5f, 0f, -0.5f, 0f, 0.5f, 1.5f, 2.5f, 2.0f }),
                BassEnabled = true, BassMode = 1, BassFrequencyHz = 55f, BassGainDb = 3.5f,
                ClarityEnabled = true, ClarityMode = 2, ClarityGainDb = 3.0f,
                SurroundEnabled = true, SurroundDelayMs = 3.5f, StereoWidth = 1.15f
            }
        },
        new PresetModel
        {
            Name = "Deep Bass Boost",
            Subtitle = "Subwoofer harmonic punch for IEMs",
            IsBuiltin = true,
            Params = new DspParamsModel
            {
                EqEnabled = true,
                EqBandsDb = new[] { 4.5f, 5.0f, 2.5f, 0.5f, -1.0f, -0.5f, 0.5f, 1.5f, 2.0f, 1.5f },
                EqBands18Db = Interpolate10To18(new[] { 4.5f, 5.0f, 2.5f, 0.5f, -1.0f, -0.5f, 0.5f, 1.5f, 2.0f, 1.5f }),
                BassEnabled = true, BassMode = 2, BassFrequencyHz = 65f, BassGainDb = 7.5f,
                ClarityEnabled = true, ClarityMode = 0, ClarityGainDb = 2.5f
            }
        },
        new PresetModel
        {
            Name = "Warm Tube Studio",
            Subtitle = "6N1J analog warmth + vocal focus",
            IsBuiltin = true,
            Params = new DspParamsModel
            {
                EqEnabled = true,
                EqBandsDb = new[] { 1.0f, 1.5f, 1.0f, 0.5f, 0.5f, 1.0f, 1.5f, 1.0f, 0.5f, 0f },
                EqBands18Db = Interpolate10To18(new[] { 1.0f, 1.5f, 1.0f, 0.5f, 0.5f, 1.0f, 1.5f, 1.0f, 0.5f, 0f }),
                BassEnabled = true, BassMode = 0, BassFrequencyHz = 60f, BassGainDb = 2.5f,
                ClarityEnabled = true, ClarityMode = 1, ClarityGainDb = 2.0f,
                TubeEnabled = true, TubeDrive = 0.42f,
                SurroundEnabled = true, SurroundDelayMs = 5.0f, StereoWidth = 1.10f
            }
        }
    };

    private static List<EqPresetModel> CreateBuiltinEqPresets() => new()
    {
        new EqPresetModel { Name = "Flat", EqMode = 0, IsBuiltin = true, EqBandsDb = new float[10], EqBands18Db = new float[18] },
        new EqPresetModel
        {
            Name = "V-Shape Fun", EqMode = 0, IsBuiltin = true,
            EqBandsDb = new[] { 3.5f, 4.0f, 2.0f, 0f, -1.5f, -1.0f, 0.5f, 2.0f, 3.5f, 3.0f },
            EqBands18Db = Interpolate10To18(new[] { 3.5f, 4.0f, 2.0f, 0f, -1.5f, -1.0f, 0.5f, 2.0f, 3.5f, 3.0f })
        },
        new EqPresetModel
        {
            Name = "Vocal Focus", EqMode = 0, IsBuiltin = true,
            EqBandsDb = new[] { -1.0f, -0.5f, 0f, 1.0f, 2.0f, 3.0f, 3.5f, 2.0f, 0.5f, 0f },
            EqBands18Db = Interpolate10To18(new[] { -1.0f, -0.5f, 0f, 1.0f, 2.0f, 3.0f, 3.5f, 2.0f, 0.5f, 0f })
        },
        new EqPresetModel
        {
            Name = "Deep Basshead", EqMode = 1, IsBuiltin = true,
            EqBandsDb = new[] { 5.5f, 6.0f, 3.5f, 1.0f, -1.0f, -0.5f, 0.5f, 1.5f, 2.0f, 1.5f },
            EqBands18Db = Interpolate10To18(new[] { 5.5f, 6.0f, 3.5f, 1.0f, -1.0f, -0.5f, 0.5f, 1.5f, 2.0f, 1.5f })
        },
        new EqPresetModel
        {
            Name = "Acoustic & Jazz", EqMode = 1, IsBuiltin = true,
            EqBandsDb = new[] { 1.0f, 1.5f, 1.0f, 0.5f, 0f, 0.5f, 1.5f, 2.0f, 2.5f, 2.0f },
            EqBands18Db = Interpolate10To18(new[] { 1.0f, 1.5f, 1.0f, 0.5f, 0f, 0.5f, 1.5f, 2.0f, 2.5f, 2.0f })
        }
    };

    private void ReloadPresetsLists()
    {
        var builtins = CreateBuiltinPresets();
        var customs = _bridge.ListCustomPresets();
        _allPresets = builtins
            .Concat(customs.Where(c => !builtins.Any(b => b.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        var builtinEq = CreateBuiltinEqPresets();
        var customEq = _bridge.ListCustomEqPresets();
        _allEqPresets = builtinEq
            .Concat(customEq.Where(c => !builtinEq.Any(b => b.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        if (!_allPresets.Any(p => p.Name == _uiSettingsModel.SelectedPresetName))
            _uiSettingsModel.SelectedPresetName = "Flat Reference";

        RebuildPresetCombo();
        RebuildEqPresetCombo();
        RebuildPinnedPresets();
    }

    private void RebuildPresetCombo()
    {
        bool prev = _isUpdating;
        _isUpdating = true;
        PresetCombo.Items.Clear();
        int selIdx = 0;
        for (int i = 0; i < _allPresets.Count; i++)
        {
            PresetCombo.Items.Add(new ComboBoxItem { Content = _allPresets[i].Name });
            if (_allPresets[i].Name == _uiSettingsModel.SelectedPresetName) selIdx = i;
        }
        PresetCombo.SelectedIndex = selIdx;
        _isUpdating = prev;
    }

    private void RebuildEqPresetCombo()
    {
        bool prev = _isUpdating;
        _isUpdating = true;
        EqPresetCombo.Items.Clear();
        EqPresetCombo.Items.Add(new ComboBoxItem { Content = "Custom" });
        int selIdx = 0;
        for (int i = 0; i < _allEqPresets.Count; i++)
        {
            EqPresetCombo.Items.Add(new ComboBoxItem { Content = _allEqPresets[i].Name });
            if (_allEqPresets[i].Name == _uiSettingsModel.SelectedEqPresetName) selIdx = i + 1;
        }
        EqPresetCombo.SelectedIndex = selIdx;
        _isUpdating = prev;
    }

    // ==================== LIVE SYNC TO / FROM APO ENGINE ====================

    private void PushLiveParams()
    {
        if (_isUpdating) return;
        _liveParams.MasterEnabled = MasterToggle.IsOn;
        _liveParams.MasterGainDb = (float)_masterGainSlider.Value;
        _liveParams.OutputPan = (float)(_panSlider.Value / 100.0);
        _liveParams.LimiterEnabled = _limiterToggle.IsOn;
        _liveParams.LimiterThresholdDb = (float)_limiterSlider.Value;

        _liveParams.EqEnabled = EqToggle.IsOn;
        _liveParams.BassEnabled = _bassToggle.IsOn;
        _liveParams.BassFrequencyHz = (float)_bassFreqSlider.Value;
        _liveParams.BassGainDb = (float)_bassGainSlider.Value;

        _liveParams.ClarityEnabled = _clarityToggle.IsOn;
        _liveParams.ClarityGainDb = (float)_clarityGainSlider.Value;

        _liveParams.DynamicEnabled = _dynToggle.IsOn;
        _liveParams.DynamicBass = (float)_dynBassSlider.Value;

        _liveParams.TubeEnabled = _tubeToggle.IsOn;
        _liveParams.TubeDrive = (float)(_tubeDriveSlider.Value / 100.0);

        _liveParams.AnalogXEnabled = _analogToggle.IsOn;
        _liveParams.DdcEnabled = _ddcToggle.IsOn;

        _liveParams.ConvolverEnabled = _convToggle.IsOn;
        _liveParams.ConvolverCrossChannel = (float)(_convCrossSlider.Value / 100.0);
        _liveParams.ConvolverWetMix = (float)(_convWetSlider.Value / 100.0);

        _liveParams.VseEnabled = _vseToggle.IsOn;
        _liveParams.VseStrength = (float)(_vseSlider.Value / 100.0);

        _liveParams.SurroundEnabled = _surroundToggle.IsOn;
        _liveParams.SurroundDelayMs = (float)_surroundDelaySlider.Value;
        _liveParams.StereoWidth = (float)(_stereoWidthSlider.Value / 100.0);

        _liveParams.FieldSurroundEnabled = _fieldToggle.IsOn;
        _liveParams.FieldSurroundStrength = (float)(_fieldStrengthSlider.Value / 100.0);
        _liveParams.FieldSurroundMidImage = (float)(_fieldMidSlider.Value / 100.0);

        _liveParams.VhsEnabled = _vhsToggle.IsOn;

        _liveParams.ReverbEnabled = _reverbToggle.IsOn;
        _liveParams.ReverbRoomSize = (float)(_revRoomSlider.Value / 100.0);
        _liveParams.ReverbWidth = (float)(_revWidthSlider.Value / 100.0);
        _liveParams.ReverbDamping = (float)(_revDampSlider.Value / 100.0);
        _liveParams.ReverbWet = (float)(_revWetSlider.Value / 100.0);
        _liveParams.ReverbDry = (float)(_revDrySlider.Value / 100.0);

        _liveParams.CompEnabled = _compToggle.IsOn;
        _liveParams.CompThresholdDb = (float)_compThreshSlider.Value;
        _liveParams.CompRatio = (float)_compRatioSlider.Value;
        _liveParams.CompAttackMs = (float)_compAtkSlider.Value;
        _liveParams.CompReleaseMs = (float)_compRelSlider.Value;
        _liveParams.CompMakeupDb = (float)_compMakeupSlider.Value;

        _liveParams.AgcEnabled = _agcToggle.IsOn;
        _liveParams.AgcStrength = (float)(_agcStrengthSlider.Value / 100.0);
        _liveParams.AgcMaxGainDb = (float)_agcMaxGainSlider.Value;
        _liveParams.AgcOutputThreshDb = (float)_agcThreshSlider.Value;

        _liveParams.CureEnabled = _cureToggle.IsOn;

        try { _bridge.WriteLiveParams(_liveParams); } catch { }
        RefreshVisualStates();
    }

    private void HydrateUiFromLiveParams()
    {
        _isUpdating = true;

        MasterToggle.IsOn = _liveParams.MasterEnabled;
        _masterGainSlider.Value = Math.Clamp(_liveParams.MasterGainDb, -24, 12);
        _panSlider.Value = Math.Clamp(_liveParams.OutputPan * 100.0, -100, 100);
        _limiterToggle.IsOn = _liveParams.LimiterEnabled;
        _limiterSlider.Value = Math.Clamp(_liveParams.LimiterThresholdDb, -6, 0);

        EqToggle.IsOn = _liveParams.EqEnabled;
        _bassToggle.IsOn = _liveParams.BassEnabled;
        _bassFreqSlider.Value = Math.Clamp(_liveParams.BassFrequencyHz, 30, 120);
        _bassGainSlider.Value = Math.Clamp(_liveParams.BassGainDb, 0, 15);

        _clarityToggle.IsOn = _liveParams.ClarityEnabled;
        _clarityGainSlider.Value = Math.Clamp(_liveParams.ClarityGainDb, 0, 15);

        _dynToggle.IsOn = _liveParams.DynamicEnabled;
        _dynBassSlider.Value = Math.Clamp(_liveParams.DynamicBass, 0, 100);

        _tubeToggle.IsOn = _liveParams.TubeEnabled;
        _tubeDriveSlider.Value = Math.Clamp(_liveParams.TubeDrive * 100.0, 0, 100);

        _analogToggle.IsOn = _liveParams.AnalogXEnabled;
        _ddcToggle.IsOn = _liveParams.DdcEnabled;

        _convToggle.IsOn = _liveParams.ConvolverEnabled;
        _convCrossSlider.Value = Math.Clamp(_liveParams.ConvolverCrossChannel * 100.0, 0, 100);
        _convWetSlider.Value = Math.Clamp(_liveParams.ConvolverWetMix * 100.0, 0, 100);

        _vseToggle.IsOn = _liveParams.VseEnabled;
        _vseSlider.Value = Math.Clamp(_liveParams.VseStrength * 100.0, 0, 100);

        _surroundToggle.IsOn = _liveParams.SurroundEnabled;
        _surroundDelaySlider.Value = Math.Clamp(_liveParams.SurroundDelayMs, 0, 20);
        _stereoWidthSlider.Value = Math.Clamp(_liveParams.StereoWidth * 100.0, 0, 200);

        _fieldToggle.IsOn = _liveParams.FieldSurroundEnabled;
        _fieldStrengthSlider.Value = Math.Clamp(_liveParams.FieldSurroundStrength * 100.0, 0, 100);
        _fieldMidSlider.Value = Math.Clamp(_liveParams.FieldSurroundMidImage * 100.0, 0, 100);

        _vhsToggle.IsOn = _liveParams.VhsEnabled;

        _reverbToggle.IsOn = _liveParams.ReverbEnabled;
        _revRoomSlider.Value = Math.Clamp(_liveParams.ReverbRoomSize * 100.0, 0, 100);
        _revWidthSlider.Value = Math.Clamp(_liveParams.ReverbWidth * 100.0, 0, 100);
        _revDampSlider.Value = Math.Clamp(_liveParams.ReverbDamping * 100.0, 0, 100);
        _revWetSlider.Value = Math.Clamp(_liveParams.ReverbWet * 100.0, 0, 100);
        _revDrySlider.Value = Math.Clamp(_liveParams.ReverbDry * 100.0, 0, 100);

        _compToggle.IsOn = _liveParams.CompEnabled;
        _compThreshSlider.Value = Math.Clamp(_liveParams.CompThresholdDb, -40, 0);
        _compRatioSlider.Value = Math.Clamp(_liveParams.CompRatio, 1, 20);
        _compAtkSlider.Value = Math.Clamp(_liveParams.CompAttackMs, 1, 100);
        _compRelSlider.Value = Math.Clamp(_liveParams.CompReleaseMs, 20, 500);
        _compMakeupSlider.Value = Math.Clamp(_liveParams.CompMakeupDb, 0, 18);

        _agcToggle.IsOn = _liveParams.AgcEnabled;
        _agcStrengthSlider.Value = Math.Clamp(_liveParams.AgcStrength * 100.0, 0, 100);
        _agcMaxGainSlider.Value = Math.Clamp(_liveParams.AgcMaxGainDb, 0, 24);
        _agcThreshSlider.Value = Math.Clamp(_liveParams.AgcOutputThreshDb, -12, 0);

        _cureToggle.IsOn = _liveParams.CureEnabled;

        RebuildEqSliders();
        _uiSettingsModel.SelectedEqPresetName = MatchEqPresetName(_liveParams.EqBandsDb, _liveParams.EqBands18Db, (int)_liveParams.EqMode);
        RebuildEqPresetCombo();

        _isUpdating = false;
        RefreshVisualStates();
    }

    // ==================== LIVE TELEMETRY & DEVICES UI ====================

    private async Task PollLiveTelemetrySafeAsync()
    {
        if (_pollBusy) return;
        _pollBusy = true;
        try
        {
            var (status, devices) = await _bridge.PollLiveTelemetryAsync();
            _liveStatus = status;

            string DeviceSig(AudioDeviceModel d) => $"{d.Guid}|{d.IsDefault}|{d.SampleRate}|{_bridge.IsApoInstalledOnDevice(d.Guid)}";
            bool devicesChanged = devices.Count != _devices.Count ||
                                  !devices.Select(DeviceSig).SequenceEqual(_devices.Select(DeviceSig));
            _devices = devices;

            UpdateTelemetryUi();
            if (devicesChanged) RebuildDevicesListUi();
        }
        catch { }
        finally { _pollBusy = false; }
    }

    private void UpdateTelemetryUi()
    {
        var defDev = _devices.FirstOrDefault(d => d.IsDefault) ?? _devices.FirstOrDefault();
        if (defDev != null)
        {
            HeaderDeviceNameText.Text = defDev.Name;
            string chLabel = defDev.Channels == 2 ? "Stereo" : $"{defDev.Channels}ch";
            HeaderDeviceFormatText.Text = $"{defDev.SampleRate / 1000.0:0.0} kHz · {defDev.BitsPerSample}-bit · {chLabel}";
        }

        SignalSourceText.Text = _liveStatus.Source.ToUpperInvariant();
        SignalModeText.Text = _liveStatus.Mode.ToUpperInvariant();

        if (_liveStatus.ExtraApps.Count > 0)
        {
            SignalExtraAppsText.Text = "+ " + string.Join(", ", _liveStatus.ExtraApps);
            SignalExtraAppsBadge.Visibility = Visibility.Visible;
        }
        else
        {
            SignalExtraAppsBadge.Visibility = Visibility.Collapsed;
        }

        SignalTitleText.Text = !string.IsNullOrWhiteSpace(_liveStatus.Title)
            ? (!string.IsNullOrWhiteSpace(_liveStatus.Artist) ? $"{_liveStatus.Artist} — {_liveStatus.Title}" : _liveStatus.Title)
            : _liveStatus.DeviceName;

        SignalHiResBadge.Visibility = _liveStatus.IsHiRes ? Visibility.Visible : Visibility.Collapsed;
        SignalCodecText.Text = _liveStatus.Codec;
        SignalCodecBadge.Visibility = string.IsNullOrWhiteSpace(_liveStatus.Codec) ? Visibility.Collapsed : Visibility.Visible;
        SignalSampleRateText.Text = $"{_liveStatus.SampleRate / 1000.0:0.0} kHz";

        SignalBitDepthBadge.Visibility = _liveStatus.BitDepth > 0 ? Visibility.Visible : Visibility.Collapsed;
        SignalBitDepthText.Text = $"{_liveStatus.BitDepth}-bit";

        SignalBitrateBadge.Visibility = _liveStatus.BitrateKbps > 0 ? Visibility.Visible : Visibility.Collapsed;
        SignalBitrateText.Text = $"{_liveStatus.BitrateKbps:N0} kbps";

        RefreshStatusPill();
    }

    private void RebuildDevicesListUi()
    {
        DevicesListPanel.Children.Clear();
        if (_devices.Count == 0)
        {
            DevicesListPanel.Children.Add(new TextBlock
            {
                Text = "Waiting for Windows Audio service (Audiosrv)... Click Refresh Devices if needed.",
                Foreground = TextSecondaryBrush,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var dev in _devices)
        {
            bool apoActive = _bridge.IsApoInstalledOnDevice(dev.Guid);
            var cardStack = new StackPanel { Spacing = 12 };

            var topGrid = new Grid { ColumnSpacing = 12, RowSpacing = 10 };
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { Spacing = 4 };
            info.Children.Add(new TextBlock { Text = dev.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = TextPrimaryBrush, TextWrapping = TextWrapping.Wrap });

            var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            if (dev.IsDefault)
            {
                badges.Children.Add(new Border
                {
                    Height = 22,
                    Background = SubtleBgBrush,
                    BorderBrush = CardBorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 0, 8, 0),
                    Child = new TextBlock { Text = "DEFAULT", FontFamily = new FontFamily("Consolas"), FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = TextDimBrush, VerticalAlignment = VerticalAlignment.Center }
                });
            }
            badges.Children.Add(new Border
            {
                Height = 22,
                Background = apoActive ? AccentBgBrush : SubtleBgBrush,
                BorderBrush = apoActive ? AccentBorderBrush : CardBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = apoActive ? "MFX APO ACTIVE" : "NOT ATTACHED",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = apoActive ? AccentTextBrush : MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
            info.Children.Add(badges);
            info.Children.Add(new TextBlock
            {
                Text = $"GUID: {dev.Guid} · {dev.SampleRate / 1000.0:0.0} kHz / {dev.BitsPerSample}-bit · {dev.Channels}ch",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                Foreground = TextSecondaryBrush,
                TextWrapping = TextWrapping.Wrap
            });

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            string guid = dev.Guid;
            var installBtn = new Button
            {
                Content = "Install / Repair APO",
                Height = 34,
                Padding = new Thickness(12, 0, 12, 0),
                CornerRadius = new CornerRadius(4),
                Background = AccentBgBrush,
                BorderBrush = AccentBorderBrush,
                Foreground = AccentTextBrush,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12.5
            };
            installBtn.Click += async (_, _) =>
            {
                if (_apoOpBusy) { ShowToast("An APO operation is already in progress..."); return; }
                _apoOpBusy = true;
                try
                {
                    ShowToast("Installing V4Wa MFX APO...");
                    var (launched, exitCode) = await _bridge.RunApoScriptElevatedAsync(true, guid);
                    await Task.Delay(500);
                    await PollLiveTelemetrySafeAsync();
                    RebuildDevicesListUi();
                    if (!launched) ShowToast("UAC cancelled or install script not found.");
                    else if (exitCode == 0) ShowToast("V4Wa MFX APO installed successfully!");
                    else ShowToast($"APO install failed (code {exitCode}). See apo_install.log.");
                }
                finally { _apoOpBusy = false; }
            };

            var restoreBtn = new Button
            {
                Content = "Restore Factory",
                Height = 34,
                Padding = new Thickness(12, 0, 12, 0),
                CornerRadius = new CornerRadius(4),
                Background = SubtleBgBrush,
                BorderBrush = CardBorderBrush,
                FontSize = 12.5
            };
            restoreBtn.Click += async (_, _) =>
            {
                if (_apoOpBusy) { ShowToast("An APO operation is already in progress..."); return; }
                _apoOpBusy = true;
                try
                {
                    ShowToast("Restoring factory audio...");
                    var (launched, exitCode) = await _bridge.RunApoScriptElevatedAsync(false, guid);
                    await Task.Delay(500);
                    await PollLiveTelemetrySafeAsync();
                    RebuildDevicesListUi();
                    if (!launched) ShowToast("UAC cancelled or uninstall script not found.");
                    else if (exitCode == 0) ShowToast("Factory audio restored successfully!");
                    else ShowToast($"Restore failed (code {exitCode}). See apo_install.log.");
                }
                finally { _apoOpBusy = false; }
            };

            actions.Children.Add(installBtn);
            actions.Children.Add(restoreBtn);

            Grid.SetColumn(info, 0);
            Grid.SetColumn(actions, 1);
            topGrid.Children.Add(info);
            topGrid.Children.Add(actions);
            cardStack.Children.Add(topGrid);

            // Command copy box for default device
            if (dev.IsDefault)
            {
                string installCmd = $"powershell -ExecutionPolicy Bypass -File \"{_bridge.InstallScriptPath}\" -EndpointGuid \"{guid}\"";
                var cmdBox = new Border
                {
                    Background = SubtleBgBrush,
                    BorderBrush = CardBorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 8, 12, 8)
                };
                var cmdGrid = new Grid { ColumnSpacing = 10 };
                cmdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                cmdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var cmdText = new TextBlock { Text = installCmd, FontFamily = new FontFamily("Consolas"), FontSize = 11, Foreground = TextDimBrush, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
                var copyBtn = new Button { Content = "Copy Cmd", Height = 30, Padding = new Thickness(10, 0, 10, 0), CornerRadius = new CornerRadius(4), Background = CardBgBrush, BorderBrush = CardBorderBrush, FontSize = 11.5 };
                copyBtn.Click += (_, _) =>
                {
                    var dp = new DataPackage();
                    dp.SetText(installCmd);
                    Clipboard.SetContent(dp);
                    ShowToast("Copied APO install command");
                };
                Grid.SetColumn(cmdText, 0);
                Grid.SetColumn(copyBtn, 1);
                cmdGrid.Children.Add(cmdText);
                cmdGrid.Children.Add(copyBtn);
                cmdBox.Child = cmdGrid;
                cardStack.Children.Add(cmdBox);
            }

            DevicesListPanel.Children.Add(new Border
            {
                Background = CardBgBrush,
                BorderBrush = CardBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Child = cardStack
            });
        }
    }

    // ==================== APPEARANCE / THEME ENGINE ====================

    private static Color ParseHexColor(string hex, Color fallback)
    {
        try
        {
            string s = hex.Trim().TrimStart('#');
            if (s.Length == 6)
                return Color.FromArgb(255, Convert.ToByte(s[..2], 16), Convert.ToByte(s[2..4], 16), Convert.ToByte(s[4..6], 16));
        }
        catch { }
        return fallback;
    }

    private static Color BlendColor(Color a, Color b, double t)
    {
        byte lerp(byte x, byte y) => (byte)Math.Clamp(Math.Round(x + (y - x) * t), 0, 255);
        return Color.FromArgb(255, lerp(a.R, b.R), lerp(a.G, b.G), lerp(a.B, b.B));
    }

    private void ApplyAccentColor(Color baseColor, bool syncPicker, bool persist = true)
    {
        _accentBase = Color.FromArgb(255, baseColor.R, baseColor.G, baseColor.B);
        var light = BlendColor(_accentBase, Colors.White, 0.28);
        var text = BlendColor(_accentBase, Colors.White, 0.62);
        var dark = BlendColor(_accentBase, Colors.Black, 0.22);

        AccentBrush.Color = _accentBase;
        AccentLightBrush.Color = light;
        AccentTextBrush.Color = text;
        AccentBgBrush.Color = Color.FromArgb(52, _accentBase.R, _accentBase.G, _accentBase.B);
        AccentBorderBrush.Color = Color.FromArgb(160, light.R, light.G, light.B);

        var res = Application.Current.Resources;
        if (res["ToggleSwitchFillOn"] is SolidColorBrush t1) t1.Color = _accentBase;
        if (res["ToggleSwitchFillOnPointerOver"] is SolidColorBrush t2) t2.Color = light;
        if (res["ToggleSwitchFillOnPressed"] is SolidColorBrush t3) t3.Color = dark;
        if (res["SliderTrackValueFill"] is SolidColorBrush s1) s1.Color = _accentBase;
        if (res["SliderTrackValueFillPointerOver"] is SolidColorBrush s2) s2.Color = light;
        if (res["SliderTrackValueFillPressed"] is SolidColorBrush s3) s3.Color = dark;
        if (res["SliderThumbBackgroundPressed"] is SolidColorBrush s4) s4.Color = light;

        string hex = $"#{_accentBase.R:X2}{_accentBase.G:X2}{_accentBase.B:X2}";
        ActiveAccentHexText.Text = $"{hex} · RGB({_accentBase.R}, {_accentBase.G}, {_accentBase.B})";

        if (persist)
        {
            _uiSettingsModel.AccentColorHex = hex;
            _bridge.SaveUiSettings(_uiSettingsModel);
        }

        if (syncPicker)
        {
            bool prev = _isUpdating;
            _isUpdating = true;
            AccentColorPicker.Color = _accentBase;
            _isUpdating = prev;
        }

        if (!_isUpdating) RefreshVisualStates();
    }

    private void ApplySurfaceTheme(string theme, bool persist = true)
    {
        _uiSettingsModel.Theme = theme == "DarkOLED" ? "DarkOLED" : "Acrylic";

        try
        {
            if (_uiSettingsModel.Theme == "Acrylic" && DesktopAcrylicController.IsSupported())
                SystemBackdrop = new DesktopAcrylicBackdrop();
            else
                SystemBackdrop = null;
        }
        catch { SystemBackdrop = null; }

        if (_uiSettingsModel.Theme == "DarkOLED")
        {
            RootBackdropGrid.Background = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
            HeaderBgBrush.Color = Color.FromArgb(255, 6, 6, 8);
            CardBgBrush.Color = Color.FromArgb(255, 14, 14, 18);
            CardBorderBrush.Color = Color.FromArgb(255, 52, 52, 64);
            SubtleBgBrush.Color = Color.FromArgb(255, 8, 8, 11);
        }
        else
        {
            RootBackdropGrid.Background = new SolidColorBrush(Color.FromArgb(84, 10, 12, 18));
            HeaderBgBrush.Color = Color.FromArgb(172, 16, 18, 24);
            CardBgBrush.Color = Color.FromArgb(188, 24, 26, 33);
            CardBorderBrush.Color = Color.FromArgb(96, 255, 255, 255);
            SubtleBgBrush.Color = Color.FromArgb(176, 14, 16, 22);
        }

        if (persist) _bridge.SaveUiSettings(_uiSettingsModel);
        UpdateThemePresetButtons();
        if (!_isUpdating) RefreshVisualStates();
    }

    private void UpdateThemePresetButtons()
    {
        bool isAcrylic = _uiSettingsModel.Theme != "DarkOLED";
        ThemeAcrylicBtn.Background = isAcrylic ? AccentBgBrush : SubtleBgBrush;
        ThemeAcrylicBtn.BorderBrush = isAcrylic ? AccentBorderBrush : CardBorderBrush;
        ThemeOledBtn.Background = !isAcrylic ? AccentBgBrush : SubtleBgBrush;
        ThemeOledBtn.BorderBrush = !isAcrylic ? AccentBorderBrush : CardBorderBrush;
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

        var slider = new Slider { Minimum = min, Maximum = max, Value = val, StepFrequency = step };
        slider.ValueChanged += (_, e) =>
        {
            valText.Text = formatter(e.NewValue);
            PushLiveParams();
        };

        panel.Children.Add(header);
        panel.Children.Add(slider);
        return (panel, slider, valText);
    }

    private (Border Container, Button[] Buttons) CreateSegmentedButtons(string[] labels, Action<int> setter)
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
            btn.Click += (_, _) => { setter(idx); PushLiveParams(); };
            Grid.SetColumn(btn, i);
            grid.Children.Add(btn);
            buttons[i] = btn;
        }

        border.Child = grid;
        return (border, buttons);
    }

    private (Border Container, Button[] Buttons) CreateStackedRowButtons(string[] labels, Action<int> setter)
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
            btn.Click += (_, _) => { setter(idx); PushLiveParams(); };
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
        var titleBlock = new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = TextPrimaryBrush, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

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
            if (_uiSettingsModel.PinnedModules.Contains(id)) _uiSettingsModel.PinnedModules.Remove(id);
            else _uiSettingsModel.PinnedModules.Add(id);
            _bridge.SaveUiSettings(_uiSettingsModel);
            LayoutDspCards();
            RefreshVisualStates();
        };

        ToggleSwitch? toggle = null;
        var rightHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        rightHeader.Children.Add(pinBtn);
        if (hasToggle)
        {
            toggle = new ToggleSwitch { IsOn = false, OnContent = "", OffContent = "", MinWidth = 0 };
            toggle.Toggled += (_, _) => PushLiveParams();
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
        RegisterCard("bass", 1, "\uE7F5", "ViPER Bass",
            "Enhances low-end weight and punch.\n• Natural: clean sub-bass shelf\n• PureBass+: tight punch with 2nd harmonics\n• Subwoofer: deep sub-harmonic rumble",
            true, (body, card) =>
            {
                _bassToggle = card.Toggle!;
                var (seg, btns) = CreateSegmentedButtons(new[] { "Natural", "PureBass+", "Subwoofer" }, v => _liveParams.BassMode = (uint)v);
                var freq = CreateSliderRow("Bass Frequency", "Cutoff frequency (30–120 Hz).", 30, 120, 60, 5, v => $"{v:0} Hz");
                var gain = CreateSliderRow("Bass Gain", "Boost intensity in dB.", 0, 15, 3.5, 0.5, v => $"+{v:0.0} dB");
                _bassFreqSlider = freq.Slider;
                _bassGainSlider = gain.Slider;
                body.Children.Add(seg);
                body.Children.Add(freq.Panel);
                body.Children.Add(gain.Panel);
                card.RefreshVisual = active =>
                {
                    StyleModeButtons(btns, (int)_liveParams.BassMode, active);
                    freq.Slider.Opacity = gain.Slider.Opacity = active ? 1.0 : 0.45;
                };
            });

        RegisterCard("clarity", 2, "\uE767", "ViPER Clarity",
            "Restores vocal presence and micro-detail.\n• Natural: transparent upper-mid lift\n• OZone+: sparkling vocal definition\n• XHiFi: crisp high-frequency air",
            true, (body, card) =>
            {
                _clarityToggle = card.Toggle!;
                var (seg, btns) = CreateSegmentedButtons(new[] { "Natural", "OZone+", "XHiFi" }, v => _liveParams.ClarityMode = (uint)v);
                var gain = CreateSliderRow("Clarity Gain", "Detail restoration strength (0–15 dB).", 0, 15, 3.5, 0.5, v => $"+{v:0.0} dB");
                _clarityGainSlider = gain.Slider;
                body.Children.Add(seg);
                body.Children.Add(gain.Panel);
                body.Children.Add(new TextBlock { Text = "Restores micro-detail and upper-harmonic air without harsh sibilance.", FontSize = 12, Foreground = MutedBrush, TextWrapping = TextWrapping.Wrap });
                card.RefreshVisual = active =>
                {
                    StyleModeButtons(btns, (int)_liveParams.ClarityMode, active);
                    gain.Slider.Opacity = active ? 1.0 : 0.45;
                };
            });

        RegisterCard("dynamic", 3, "\uE9D2", "Dynamic System",
            "Optimizes dynamic excursion and bass response for your listening hardware.",
            true, (body, card) =>
            {
                _dynToggle = card.Toggle!;
                var (stacked, btns) = CreateStackedRowButtons(new[] { "Common", "Extreme", "High-End", "Studio" }, v => _liveParams.DynamicDevice = (uint)v);
                var bass = CreateSliderRow("Dynamic Bass", "Transient bass punch percentage (0–100%).", 0, 100, 25, 5, v => $"{v:0}%");
                _dynBassSlider = bass.Slider;
                body.Children.Add(stacked);
                body.Children.Add(bass.Panel);
                card.RefreshVisual = active =>
                {
                    StyleModeButtons(btns, (int)_liveParams.DynamicDevice, active);
                    bass.Slider.Opacity = active ? 1.0 : 0.45;
                };
            });

        RegisterCard("master", 4, "\uE83D", "Output Gain, Pan & Limiter",
            "Controls master output volume, L/R channel balance, and zero-clipping brickwall limiter.",
            false, (body, card) =>
            {
                var gain = CreateSliderRow("Master Gain", "Pre-limiter output level (-24 to +12 dB).", -24, 12, 0, 0.5, v => v > 0 ? $"+{v:0.0} dB" : $"{v:0.0} dB");
                var pan = CreateSliderRow("Output Pan", "Stereo channel balance (-100% Left to +100% Right).", -100, 100, 0, 5, v => Math.Abs(v) < 1 ? "Center" : (v < 0 ? $"L {Math.Abs(v):0}%" : $"R {v:0}%"));
                _masterGainSlider = gain.Slider;
                _panSlider = pan.Slider;

                var limRow = new Grid { Margin = new Thickness(0, 4, 0, 0), ColumnSpacing = 10 };
                limRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                limRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var limLabel = new TextBlock { Text = "Brickwall Limiter", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TextPrimaryBrush, VerticalAlignment = VerticalAlignment.Center };
                _limiterToggle = new ToggleSwitch { IsOn = true, OnContent = "", OffContent = "", MinWidth = 0 };
                _limiterToggle.Toggled += (_, _) => PushLiveParams();
                Grid.SetColumn(limLabel, 0);
                Grid.SetColumn(_limiterToggle, 1);
                limRow.Children.Add(limLabel);
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

        RegisterCard("analogx", 5, "\uE950", "AnalogX",
            "Models dense, smooth solid-state harmonic saturation—tighter and more balanced than 6N1J tube warmth.",
            true, (body, card) =>
            {
                _analogToggle = card.Toggle!;
                body.Children.Add(new TextBlock { Text = "Solid-state harmonic signature for richer body and analog smoothness.", FontSize = 12, Foreground = TextSecondaryBrush, TextWrapping = TextWrapping.Wrap });
                var (seg, btns) = CreateSegmentedButtons(new[] { "Slight", "Moderate", "Extreme" }, v => _liveParams.AnalogXLevel = (uint)v);
                body.Children.Add(seg);
                card.RefreshVisual = active => StyleModeButtons(btns, (int)_liveParams.AnalogXLevel, active);
            });

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

        RegisterCard("ddc", 7, "\uE7F6", "ViPER-DDC",
            "Loads ViPER4Android / AutoEQ .vdc biquad profiles to correct headphone frequency response up to 384 kHz.",
            true, (body, card) =>
            {
                _ddcToggle = card.Toggle!;
                _ddcCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, Height = 34, MinHeight = 34, CornerRadius = new CornerRadius(4), Background = SubtleBgBrush, BorderBrush = CardBorderBrush, PlaceholderText = "Select .vdc profile..." };
                _ddcCombo.SelectionChanged += (_, _) =>
                {
                    if (_isUpdating) return;
                    if (_ddcCombo.SelectedItem is ComboBoxItem item && item.Content is string fn && _ddcFiles.Contains(fn))
                    {
                        _activeDdc = fn;
                        _liveParams.DdcRevision = _bridge.SelectDdcFile(fn, _liveParams);
                        ShowToast($"Loaded DDC: {fn}");
                    }
                };
                var uploadBtn = new Button { Content = "Upload .vdc Profile", HorizontalAlignment = HorizontalAlignment.Stretch, Height = 34, CornerRadius = new CornerRadius(4), Background = SubtleBgBrush, BorderBrush = CardBorderBrush };
                uploadBtn.Click += async (_, _) => await UploadDdcFileAsync();
                body.Children.Add(_ddcCombo);
                body.Children.Add(uploadBtn);
                card.RefreshVisual = active => _ddcCombo.Opacity = uploadBtn.Opacity = active ? 1.0 : 0.5;
            });

        RegisterCard("convolver", 8, "\uE9F5", "Convolver",
            "Partitioned FFT convolution engine for .irs and .wav impulse responses with automatic Hi-Res resampling.",
            true, (body, card) =>
            {
                _convToggle = card.Toggle!;
                _irsCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, Height = 34, MinHeight = 34, CornerRadius = new CornerRadius(4), Background = SubtleBgBrush, BorderBrush = CardBorderBrush, PlaceholderText = "Select .irs / .wav file..." };
                _irsCombo.SelectionChanged += (_, _) =>
                {
                    if (_isUpdating) return;
                    if (_irsCombo.SelectedItem is ComboBoxItem item && item.Content is string fn && _irsFiles.Contains(fn))
                    {
                        _activeIrs = fn;
                        _liveParams.ConvolverRevision = _bridge.SelectIrsFile(fn, _liveParams);
                        ShowToast($"Loaded IR: {fn}");
                    }
                };
                var uploadBtn = new Button { Content = "Upload .irs / .wav File", HorizontalAlignment = HorizontalAlignment.Stretch, Height = 34, CornerRadius = new CornerRadius(4), Background = SubtleBgBrush, BorderBrush = CardBorderBrush };
                uploadBtn.Click += async (_, _) => await UploadIrsFileAsync();
                var cross = CreateSliderRow("Cross Channel", "Blends L/R channels across the impulse response (0–100%).", 0, 100, 0, 5, v => $"{v:0}%");
                var wet = CreateSliderRow("Wet Mix", "Convolver dry/wet balance (0–100%).", 0, 100, 100, 5, v => $"{v:0}%");
                _convCrossSlider = cross.Slider;
                _convWetSlider = wet.Slider;
                body.Children.Add(_irsCombo);
                body.Children.Add(uploadBtn);
                body.Children.Add(cross.Panel);
                body.Children.Add(wet.Panel);
                card.RefreshVisual = active =>
                {
                    _irsCombo.Opacity = uploadBtn.Opacity = active ? 1.0 : 0.5;
                    cross.Slider.Opacity = wet.Slider.Opacity = active ? 1.0 : 0.45;
                };
            });

        RegisterCard("vse", 9, "\uE9D9", "Spectrum Extension",
            "Synthesizes missing upper-harmonic air above 8 kHz for compressed streams.",
            true, (body, card) =>
            {
                _vseToggle = card.Toggle!;
                var str = CreateSliderRow("Effect Strength", "High-frequency harmonic exciter strength (0–100%).", 0, 100, 50, 5, v => $"{v:0}%");
                _vseSlider = str.Slider;
                body.Children.Add(str.Panel);
                body.Children.Add(new TextBlock { Text = "Reconstructs lost high-frequency sparkle in lossy audio.", FontSize = 12, Foreground = MutedBrush, TextWrapping = TextWrapping.Wrap });
                card.RefreshVisual = active => str.Slider.Opacity = active ? 1.0 : 0.45;
            });

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

        RegisterCard("vhs", 12, "\uE7F6", "Headphone Surround+",
            "5-level early-reflection virtualizer simulating out-of-head loudspeaker staging on headphones.",
            true, (body, card) =>
            {
                _vhsToggle = card.Toggle!;
                body.Children.Add(new TextBlock { Text = "Select room reflection depth from intimate studio (Lv 1) to concert hall (Lv 5).", FontSize = 12, Foreground = TextSecondaryBrush, TextWrapping = TextWrapping.Wrap });
                var (seg, btns) = CreateSegmentedButtons(new[] { "Lv 1", "Lv 2", "Lv 3", "Lv 4", "Lv 5" }, v => _liveParams.VhsLevel = (uint)(v + 1));
                body.Children.Add(seg);
                card.RefreshVisual = active => StyleModeButtons(btns, Math.Clamp((int)_liveParams.VhsLevel - 1, 0, 4), active);
            });

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

        RegisterCard("cure", 16, "\uE95E", "Cure Tech+",
            "Binaural 0.26 ms ITD crossfeed that reduces long-session headphone listening fatigue.",
            true, (body, card) =>
            {
                _cureToggle = card.Toggle!;
                body.Children.Add(new TextBlock { Text = "Blends subtle opposite-channel cues for natural loudspeaker-like comfort.", FontSize = 12, Foreground = TextSecondaryBrush, TextWrapping = TextWrapping.Wrap });
                var (seg, btns) = CreateSegmentedButtons(new[] { "Slight", "Moderate", "Extreme" }, v => _liveParams.CureLevel = (uint)v);
                body.Children.Add(seg);
                card.RefreshVisual = active => StyleModeButtons(btns, (int)_liveParams.CureLevel, active);
            });
    }

    private void RefreshIrsAndDdcCombos()
    {
        bool prev = _isUpdating;
        _isUpdating = true;

        (_irsFiles, _activeIrs) = _bridge.ListIrsFiles();
        _irsCombo.Items.Clear();
        int irSel = -1;
        for (int i = 0; i < _irsFiles.Count; i++)
        {
            _irsCombo.Items.Add(new ComboBoxItem { Content = _irsFiles[i] });
            if (_irsFiles[i].Equals(_activeIrs, StringComparison.OrdinalIgnoreCase)) irSel = i;
        }
        _irsCombo.SelectedIndex = irSel;

        (_ddcFiles, _activeDdc) = _bridge.ListDdcFiles();
        _ddcCombo.Items.Clear();
        int ddcSel = -1;
        for (int i = 0; i < _ddcFiles.Count; i++)
        {
            _ddcCombo.Items.Add(new ComboBoxItem { Content = _ddcFiles[i] });
            if (_ddcFiles[i].Equals(_activeDdc, StringComparison.OrdinalIgnoreCase)) ddcSel = i;
        }
        _ddcCombo.SelectedIndex = ddcSel;

        _isUpdating = prev;
    }

    private FileOpenPicker CreateFilePicker(params string[] extensions)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        foreach (var ext in extensions) picker.FileTypeFilter.Add(ext);
        return picker;
    }

    private async Task UploadIrsFileAsync()
    {
        var file = await CreateFilePicker(".irs", ".wav").PickSingleFileAsync();
        if (file == null) return;
        (_irsFiles, _activeIrs) = _bridge.ImportIrsFile(file.Path, _liveParams);
        RefreshIrsAndDdcCombos();
        _isUpdating = true;
        _convToggle.IsOn = true;
        _isUpdating = false;
        RefreshVisualStates();
        ShowToast($"Imported & activated IR: {file.Name}");
    }

    private async Task UploadDdcFileAsync()
    {
        var file = await CreateFilePicker(".vdc").PickSingleFileAsync();
        if (file == null) return;
        (_ddcFiles, _activeDdc) = _bridge.ImportDdcFile(file.Path, _liveParams);
        RefreshIrsAndDdcCombos();
        _isUpdating = true;
        _ddcToggle.IsOn = true;
        _isUpdating = false;
        RefreshVisualStates();
        ShowToast($"Imported & activated DDC: {file.Name}");
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
                int pinIdx = _uiSettingsModel.PinnedModules.IndexOf(c.Id);
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

        var validPinned = _uiSettingsModel.PinnedPresets
            .Where(name => _allPresets.Any(p => p.Name == name))
            .ToList();

        if (validPinned.Count == 0)
        {
            PinnedPresetsGrid.Visibility = Visibility.Collapsed;
            return;
        }

        PinnedPresetsGrid.Visibility = Visibility.Visible;
        int maxCols = _currentWindowWidth >= 960 ? 4 : (_currentWindowWidth >= 560 ? 2 : 1);
        int cols = Math.Min(maxCols, validPinned.Count);
        _lastPresetCols = maxCols;

        for (int c = 0; c < cols; c++)
            PinnedPresetsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int rows = (validPinned.Count + cols - 1) / cols;
        for (int r = 0; r < rows; r++)
            PinnedPresetsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < validPinned.Count; i++)
        {
            string name = validPinned[i];
            var meta = _allPresets.First(p => p.Name == name);
            bool active = _uiSettingsModel.SelectedPresetName == name;

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
            btn.Click += (_, _) => ApplyPresetByName(name);

            Grid.SetRow(btn, i / cols);
            Grid.SetColumn(btn, i % cols);
            PinnedPresetsGrid.Children.Add(btn);
        }
    }

    private static float[] Interpolate10To18(float[] b10)
    {
        float G(int i) => i < b10.Length ? b10[i] : 0f;
        float Mid(float a, float b) => MathF.Round((a + b) / 2f * 2f) / 2f;
        return new[]
        {
            G(0), Mid(G(0), G(1)), G(1), Mid(G(1), G(2)),
            G(2), Mid(G(2), G(3)), G(3), Mid(G(3), G(4)),
            G(4), Mid(G(4), G(5)), G(5), Mid(G(5), G(6)),
            G(6), Mid(G(6), G(7)), G(7), Mid(G(7), G(8)),
            G(8), G(9)
        };
    }

    private static float[] Downsample18To10(float[] b18)
    {
        int[] idx = { 0, 2, 4, 6, 8, 10, 12, 14, 16, 17 };
        var res = new float[10];
        for (int i = 0; i < 10; i++) res[i] = idx[i] < b18.Length ? b18[idx[i]] : 0f;
        return res;
    }

    private string MatchEqPresetName(float[] b10, float[] b18, int eqMode)
    {
        static bool Close(float[] a, float[] b) => a.Length == b.Length && a.Zip(b).All(p => MathF.Abs(p.First - p.Second) < 0.05f);
        foreach (var ep in _allEqPresets)
        {
            if (eqMode == 1)
            {
                var t18 = ep.EqBands18Db is { Length: 18 } ? ep.EqBands18Db : (ep.EqBandsDb is { Length: 10 } ? Interpolate10To18(ep.EqBandsDb) : null);
                if (t18 != null && Close(b18, t18)) return ep.Name;
            }
            else
            {
                var t10 = ep.EqBandsDb is { Length: 10 } ? ep.EqBandsDb : (ep.EqBands18Db is { Length: 18 } ? Downsample18To10(ep.EqBands18Db) : null);
                if (t10 != null && Close(b10, t10)) return ep.Name;
            }
        }
        return "";
    }

    private void ApplyPresetByName(string name)
    {
        var found = _allPresets.FirstOrDefault(p => p.Name == name);
        if (found == null) return;

        bool prevMaster = _liveParams.MasterEnabled;
        uint prevIrRev = _liveParams.ConvolverRevision;
        uint prevDdcRev = _liveParams.DdcRevision;

        _liveParams = found.Params.Clone();
        _liveParams.MasterEnabled = prevMaster;
        _liveParams.ConvolverRevision = prevIrRev;
        _liveParams.DdcRevision = prevDdcRev;

        if (_liveParams.EqBands18Db.Length == 18 && _liveParams.EqBands18Db.All(v => v == 0) && _liveParams.EqBandsDb.Any(v => v != 0))
            _liveParams.EqBands18Db = Interpolate10To18(_liveParams.EqBandsDb);
        else if (_liveParams.EqBandsDb.Length == 10 && _liveParams.EqBandsDb.All(v => v == 0) && _liveParams.EqBands18Db.Any(v => v != 0))
            _liveParams.EqBandsDb = Downsample18To10(_liveParams.EqBands18Db);

        if (!string.IsNullOrWhiteSpace(found.ActiveIrs) && _irsFiles.Contains(found.ActiveIrs))
        {
            _activeIrs = found.ActiveIrs;
            _liveParams.ConvolverRevision = _bridge.SelectIrsFile(found.ActiveIrs, _liveParams);
        }
        if (!string.IsNullOrWhiteSpace(found.ActiveDdc) && _ddcFiles.Contains(found.ActiveDdc))
        {
            _activeDdc = found.ActiveDdc;
            _liveParams.DdcRevision = _bridge.SelectDdcFile(found.ActiveDdc, _liveParams);
        }

        _bridge.WriteLiveParams(_liveParams);
        _uiSettingsModel.SelectedPresetName = name;
        _bridge.SaveUiSettings(_uiSettingsModel);

        RefreshIrsAndDdcCombos();
        RebuildPresetCombo();
        RebuildPinnedPresets();
        HydrateUiFromLiveParams();
    }

    private void RebuildEqSliders()
    {
        EqSlidersGrid.Children.Clear();
        EqSlidersGrid.ColumnDefinitions.Clear();
        _eqSliders.Clear();
        _eqValueLabels.Clear();

        bool is18 = _liveParams.EqMode == 1;
        var labels = is18 ? EqLabels18 : EqLabels10;
        var values = is18 ? _liveParams.EqBands18Db : _liveParams.EqBandsDb;
        EqTitleText.Text = "Equalizer";

        for (int i = 0; i < labels.Length; i++)
        {
            EqSlidersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 6 };
            float curVal = i < values.Length ? values[i] : 0f;
            var valText = new TextBlock
            {
                Text = FormatDb(curVal),
                FontFamily = new FontFamily("Consolas"),
                FontSize = is18 ? 10.5 : 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = MathF.Abs(curVal) > 0.01f ? AccentLightBrush : TextSecondaryBrush
            };

            int bandIdx = i;
            var slider = new Slider
            {
                Orientation = Orientation.Vertical,
                Height = 132,
                Minimum = -12,
                Maximum = 12,
                StepFrequency = 0.5,
                Value = curVal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            slider.ValueChanged += (_, e) =>
            {
                if (_isUpdating) return;
                if (_liveParams.EqMode == 1)
                {
                    _liveParams.EqBands18Db[bandIdx] = (float)e.NewValue;
                    _liveParams.EqBandsDb = Downsample18To10(_liveParams.EqBands18Db);
                }
                else
                {
                    _liveParams.EqBandsDb[bandIdx] = (float)e.NewValue;
                    _liveParams.EqBands18Db = Interpolate10To18(_liveParams.EqBandsDb);
                }
                valText.Text = FormatDb(e.NewValue);
                _uiSettingsModel.SelectedEqPresetName = "";
                _isUpdating = true;
                EqPresetCombo.SelectedIndex = 0;
                _isUpdating = false;
                PushLiveParams();
            };

            var freqText = new TextBlock
            {
                Text = labels[i],
                FontFamily = new FontFamily("Consolas"),
                FontSize = is18 ? 10 : 11.5,
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
        var values = _liveParams.EqMode == 1 ? _liveParams.EqBands18Db : _liveParams.EqBandsDb;
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

    private void RefreshStatusPill()
    {
        bool isExclusive = string.Equals(_liveStatus.Mode, "Exclusive Mode", StringComparison.OrdinalIgnoreCase);
        bool working = MasterToggle.IsOn && !isExclusive;
        StatusPill.Background = working ? AccentBgBrush : CardBgBrush;
        StatusPill.BorderBrush = working ? AccentBorderBrush : CardBorderBrush;
        StatusDot.Fill = working ? AccentLightBrush : MutedBrush;
        StatusText.Text = working ? "WORKING" : "NOT WORKING";
        StatusText.Foreground = working ? AccentTextBrush : TextSecondaryBrush;
    }

    private void RefreshVisualStates()
    {
        bool master = MasterToggle.IsOn;
        RefreshStatusPill();

        SettingsBtn.Background = _settingsOpen ? AccentBgBrush : SubtleBgBrush;
        SettingsBtn.BorderBrush = _settingsOpen ? AccentBorderBrush : CardBorderBrush;
        SettingsGearIcon.Foreground = _settingsOpen ? AccentLightBrush : TextDimBrush;

        bool isPinnedPreset = _uiSettingsModel.PinnedPresets.Contains(_uiSettingsModel.SelectedPresetName);
        PinPresetBtn.Content = isPinnedPreset ? "Pinned" : "Pin";
        PinPresetBtn.Background = isPinnedPreset ? AccentBgBrush : SubtleBgBrush;
        PinPresetBtn.BorderBrush = isPinnedPreset ? AccentBorderBrush : CardBorderBrush;
        PinPresetBtn.Foreground = isPinnedPreset ? AccentTextBrush : TextDimBrush;

        bool eqActive = master && EqToggle.IsOn;
        EqIcon.Foreground = eqActive ? AccentLightBrush : MutedBrush;
        EqIconBox.Background = eqActive ? AccentBgBrush : SubtleBgBrush;
        EqIconBox.BorderBrush = eqActive ? AccentBorderBrush : CardBorderBrush;
        EqSlidersGrid.Opacity = eqActive ? 1.0 : 0.5;

        var eqVals = _liveParams.EqMode == 1 ? _liveParams.EqBands18Db : _liveParams.EqBandsDb;
        for (int i = 0; i < _eqValueLabels.Count && i < eqVals.Length; i++)
            _eqValueLabels[i].Foreground = MathF.Abs(eqVals[i]) > 0.01f && eqActive ? AccentLightBrush : TextSecondaryBrush;

        foreach (var card in _cards)
        {
            bool active = card.IsActive();
            card.Icon.Foreground = active ? AccentLightBrush : MutedBrush;
            card.IconBox.Background = active ? AccentBgBrush : SubtleBgBrush;
            card.IconBox.BorderBrush = active ? AccentBorderBrush : CardBorderBrush;
            bool pinned = _uiSettingsModel.PinnedModules.Contains(card.Id);
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
        bool is18 = _liveParams.EqMode == 1;
        Eq10Btn.Background = !is18 ? MutedBgBrush : TransparentBrush;
        Eq10Btn.Foreground = !is18 ? AccentLightBrush : TextSecondaryBrush;
        Eq18Btn.Background = is18 ? MutedBgBrush : TransparentBrush;
        Eq18Btn.Foreground = is18 ? AccentLightBrush : TextSecondaryBrush;
    }

    // ==================== RESPONSIVE & EVENT HANDLERS ====================

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

        int targetModuleCols = w < 820 ? 1 : 2;
        if (targetModuleCols != _lastModuleCols && _cards.Count > 0) LayoutDspCards();

        int targetPresetCols = w >= 960 ? 4 : (w >= 560 ? 2 : 1);
        if (targetPresetCols != _lastPresetCols) RebuildPinnedPresets();

        SetResponsiveWrap(SignalBadgesPanel, SignalBadgesCol, w < 760);
        SetResponsiveWrap(PresetActionsPanel, PresetActionsCol, w < 620);
        SetResponsiveWrap(EqActionsPanel, EqActionsCol, w < 900);

        bool compactSidebar = w < 760;
        SettingsSidebarCol.Width = new GridLength(compactSidebar ? 54 : 220);
        var labelVis = compactSidebar ? Visibility.Collapsed : Visibility.Visible;
        NavAppearanceLabel.Visibility = labelVis;
        NavDevicesLabel.Visibility = labelVis;
        NavAboutLabel.Visibility = labelVis;
        SettingsSidebarPanel.Margin = compactSidebar ? new Thickness(0, 4, 10, 0) : new Thickness(0, 4, 16, 0);

        SetResponsiveWrap(ThemeOledBtn, ThemeOledCol, w < 640, equalStarWhenWide: true);
        SetResponsiveWrap(AccentPickerPanel, AccentPickerCol, w < 920);
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
        if (sender is Button btn && btn.Tag is string category) SelectSettingsCategory(category);
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
        _uiSettingsModel.AutoAccent = AutoAccentToggle.IsOn;
        _bridge.SaveUiSettings(_uiSettingsModel);
        AccentColorPicker.IsEnabled = !AutoAccentToggle.IsOn;
        if (AutoAccentToggle.IsOn)
            ApplyAccentColor(_osUiSettings.GetColorValue(UIColorType.Accent), syncPicker: true, persist: false);
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            _isUpdating = true;
            AutoAccentToggle.IsOn = false;
            _uiSettingsModel.AutoAccent = false;
            AccentColorPicker.IsEnabled = true;
            _isUpdating = false;
            ApplyAccentColor(ParseHexColor(hex, _accentBase), syncPicker: true);
        }
    }

    private void AccentColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isUpdating) return;
        if (AutoAccentToggle.IsOn)
        {
            _isUpdating = true;
            AutoAccentToggle.IsOn = false;
            _uiSettingsModel.AutoAccent = false;
            _isUpdating = false;
        }
        ApplyAccentColor(args.NewColor, syncPicker: false);
    }

    private void MasterToggle_Toggled(object sender, RoutedEventArgs e) => PushLiveParams();
    private void ModuleToggle_Toggled(object sender, RoutedEventArgs e) => PushLiveParams();

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (PresetCombo.SelectedItem is ComboBoxItem item && item.Content is string name) ApplyPresetByName(name);
    }

    private void PinPresetBtn_Click(object sender, RoutedEventArgs e)
    {
        string cur = _uiSettingsModel.SelectedPresetName;
        if (_uiSettingsModel.PinnedPresets.Contains(cur)) _uiSettingsModel.PinnedPresets.Remove(cur);
        else _uiSettingsModel.PinnedPresets.Add(cur);
        _bridge.SaveUiSettings(_uiSettingsModel);
        RebuildPinnedPresets();
        RefreshVisualStates();
    }

    private async Task<string?> PromptNameAsync(string title, string defaultValue)
    {
        var box = new TextBox { Text = defaultValue, PlaceholderText = "Enter name..." };
        var dlg = new ContentDialog
        {
            Title = title,
            Content = box,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootBackdropGrid.XamlRoot
        };
        var result = await dlg.ShowAsync();
        return result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(box.Text) ? box.Text.Trim() : null;
    }

    private async void SavePresetBtn_Click(object sender, RoutedEventArgs e)
    {
        string? name = await PromptNameAsync("Save Full Preset", _uiSettingsModel.SelectedPresetName);
        if (name == null) return;
        string saved = _bridge.SaveCustomPreset(new PresetModel
        {
            Name = name,
            Subtitle = "Custom saved preset",
            Params = _liveParams.Clone(),
            ActiveIrs = string.IsNullOrWhiteSpace(_activeIrs) ? null : _activeIrs,
            ActiveDdc = string.IsNullOrWhiteSpace(_activeDdc) ? null : _activeDdc
        });
        _uiSettingsModel.SelectedPresetName = saved;
        _bridge.SaveUiSettings(_uiSettingsModel);
        ReloadPresetsLists();
        ShowToast($"Saved preset: {saved}");
    }

    private async void LoadPresetBtn_Click(object sender, RoutedEventArgs e)
    {
        var files = await CreateFilePicker(".json").PickMultipleFilesAsync();
        if (files == null || files.Count == 0) return;
        int imported = 0;
        string? lastImported = null;
        foreach (var f in files)
        {
            try
            {
                string txt = await File.ReadAllTextAsync(f.Path);
                var p = JsonSerializer.Deserialize<PresetModel>(txt, V4WaEngineBridge.JsonOpts);
                if (p != null)
                {
                    if (string.IsNullOrWhiteSpace(p.Name)) p.Name = System.IO.Path.GetFileNameWithoutExtension(f.Name);
                    lastImported = _bridge.SaveCustomPreset(p);
                    imported++;
                }
            }
            catch { }
        }
        if (imported > 0)
        {
            ReloadPresetsLists();
            if (lastImported != null) ApplyPresetByName(lastImported);
            ShowToast($"Imported {imported} preset(s)");
        }
    }

    private async void SaveEqPresetBtn_Click(object sender, RoutedEventArgs e)
    {
        string? name = await PromptNameAsync("Save EQ Preset", string.IsNullOrWhiteSpace(_uiSettingsModel.SelectedEqPresetName) ? "My EQ" : _uiSettingsModel.SelectedEqPresetName);
        if (name == null) return;
        string saved = _bridge.SaveCustomEqPreset(new EqPresetModel
        {
            Name = name,
            EqMode = (int)_liveParams.EqMode,
            EqBandsDb = (float[])_liveParams.EqBandsDb.Clone(),
            EqBands18Db = (float[])_liveParams.EqBands18Db.Clone()
        });
        _uiSettingsModel.SelectedEqPresetName = saved;
        _bridge.SaveUiSettings(_uiSettingsModel);
        ReloadPresetsLists();
        ShowToast($"Saved EQ preset: {saved}");
    }

    private async void LoadEqPresetBtn_Click(object sender, RoutedEventArgs e)
    {
        var files = await CreateFilePicker(".json").PickMultipleFilesAsync();
        if (files == null || files.Count == 0) return;
        int imported = 0;
        foreach (var f in files)
        {
            try
            {
                string txt = await File.ReadAllTextAsync(f.Path);
                var ep = JsonSerializer.Deserialize<EqPresetModel>(txt, V4WaEngineBridge.JsonOpts);
                if (ep != null)
                {
                    if (string.IsNullOrWhiteSpace(ep.Name)) ep.Name = System.IO.Path.GetFileNameWithoutExtension(f.Name);
                    _bridge.SaveCustomEqPreset(ep);
                    imported++;
                }
            }
            catch { }
        }
        if (imported > 0)
        {
            ReloadPresetsLists();
            ShowToast($"Imported {imported} EQ preset(s)");
        }
    }

    private void EqModeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int mode))
        {
            uint targetMode = mode == 18 ? 1u : 0u;
            if (targetMode == _liveParams.EqMode) return;
            _liveParams.EqMode = targetMode;
            if (targetMode == 1) _liveParams.EqBands18Db = Interpolate10To18(_liveParams.EqBandsDb);
            else _liveParams.EqBandsDb = Downsample18To10(_liveParams.EqBands18Db);

            _isUpdating = true;
            RebuildEqSliders();
            _isUpdating = false;
            PushLiveParams();
        }
    }

    private void EqPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (EqPresetCombo.SelectedItem is ComboBoxItem item && item.Content is string name && name != "Custom")
        {
            var ep = _allEqPresets.FirstOrDefault(x => x.Name == name);
            if (ep == null) return;

            if (ep.EqBands18Db is { Length: 18 } && ep.EqBandsDb is { Length: 10 })
            {
                _liveParams.EqBandsDb = (float[])ep.EqBandsDb.Clone();
                _liveParams.EqBands18Db = (float[])ep.EqBands18Db.Clone();
            }
            else if (ep.EqBands18Db is { Length: 18 })
            {
                _liveParams.EqBands18Db = (float[])ep.EqBands18Db.Clone();
                _liveParams.EqBandsDb = Downsample18To10(_liveParams.EqBands18Db);
            }
            else if (ep.EqBandsDb is { Length: 10 })
            {
                _liveParams.EqBandsDb = (float[])ep.EqBandsDb.Clone();
                _liveParams.EqBands18Db = Interpolate10To18(_liveParams.EqBandsDb);
            }

            _uiSettingsModel.SelectedEqPresetName = name;
            _bridge.SaveUiSettings(_uiSettingsModel);

            _isUpdating = true;
            EqToggle.IsOn = true;
            RebuildEqSliders();
            _isUpdating = false;
            PushLiveParams();
        }
    }

    private void ResetEqBtn_Click(object sender, RoutedEventArgs e)
    {
        _liveParams.EqBandsDb = new float[10];
        _liveParams.EqBands18Db = new float[18];
        _uiSettingsModel.SelectedEqPresetName = "Flat";
        _bridge.SaveUiSettings(_uiSettingsModel);
        _isUpdating = true;
        RebuildEqPresetCombo();
        RebuildEqSliders();
        _isUpdating = false;
        PushLiveParams();
    }

    private void EqCurveCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawEqCurve();

    private async void RestartAudioBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_apoOpBusy) { ShowToast("An audio operation is already in progress..."); return; }
        _apoOpBusy = true;
        try
        {
            ShowToast("Restarting Windows Audio service...");
            bool ok = await _bridge.RestartAudioServiceElevatedAsync();
            await Task.Delay(600);
            await PollLiveTelemetrySafeAsync();
            RebuildDevicesListUi();
            ShowToast(ok ? "Windows Audio service restarted" : "Could not restart Audiosrv (UAC cancelled)");
        }
        finally { _apoOpBusy = false; }
    }

    private async void RefreshDevicesBtn_Click(object sender, RoutedEventArgs e)
    {
        await PollLiveTelemetrySafeAsync();
        RebuildDevicesListUi();
        ShowToast("Audio devices refreshed");
    }

    private async void ShowToast(string msg)
    {
        ToastBar.Title = "V4Wa";
        ToastBar.Message = msg;
        ToastBar.IsOpen = true;
        await Task.Delay(2400);
        ToastBar.IsOpen = false;
    }
}
