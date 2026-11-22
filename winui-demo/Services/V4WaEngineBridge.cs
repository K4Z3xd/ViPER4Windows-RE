using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace V4Wa.Demo.Services;

public sealed class DspParamsModel
{
    public uint Sequence { get; set; }
    public bool MasterEnabled { get; set; } = true;
    public float MasterGainDb { get; set; } = 0f;
    public float OutputPan { get; set; } = 0f;
    public bool EqEnabled { get; set; } = false;
    public float[] EqBandsDb { get; set; } = new float[10];
    public bool BassEnabled { get; set; } = false;
    public uint BassMode { get; set; } = 0;
    public float BassFrequencyHz { get; set; } = 60f;
    public float BassGainDb { get; set; } = 3.5f;
    public bool ClarityEnabled { get; set; } = false;
    public uint ClarityMode { get; set; } = 0;
    public float ClarityGainDb { get; set; } = 3.5f;
    public bool TubeEnabled { get; set; } = false;
    public float TubeDrive { get; set; } = 0.25f;
    public bool SurroundEnabled { get; set; } = false;
    public float SurroundDelayMs { get; set; } = 6.0f;
    public float StereoWidth { get; set; } = 1.0f;
    public bool LimiterEnabled { get; set; } = true;
    public float LimiterThresholdDb { get; set; } = -0.3f;

    // V2
    public bool DynamicEnabled { get; set; } = false;
    public uint DynamicDevice { get; set; } = 0;
    public float DynamicBass { get; set; } = 25f;
    public bool ReverbEnabled { get; set; } = false;
    public float ReverbRoomSize { get; set; } = 0.35f;
    public float ReverbWidth { get; set; } = 0.50f;
    public float ReverbDamping { get; set; } = 0.25f;
    public float ReverbWet { get; set; } = 0.20f;
    public float ReverbDry { get; set; } = 0.85f;
    public bool CompEnabled { get; set; } = false;
    public float CompThresholdDb { get; set; } = -18f;
    public float CompRatio { get; set; } = 3f;
    public float CompAttackMs { get; set; } = 10f;
    public float CompReleaseMs { get; set; } = 120f;
    public float CompMakeupDb { get; set; } = 3f;
    public bool CureEnabled { get; set; } = false;
    public uint CureLevel { get; set; } = 0;

    // V3 Convolver
    public bool ConvolverEnabled { get; set; } = false;
    public float ConvolverCrossChannel { get; set; } = 0f;
    public float ConvolverWetMix { get; set; } = 1.0f;
    public uint ConvolverRevision { get; set; } = 0;

    // V4 Multi-Band EQ
    public uint EqMode { get; set; } = 0; // 0 = 10-Band, 1 = 18-Band
    public float[] EqBands18Db { get; set; } = new float[18];

    // V5 V4A Parity
    public bool DdcEnabled { get; set; } = false;
    public uint DdcRevision { get; set; } = 0;
    public bool VseEnabled { get; set; } = false;
    public float VseStrength { get; set; } = 0.50f;
    public bool AnalogXEnabled { get; set; } = false;
    public uint AnalogXLevel { get; set; } = 0;
    public bool FieldSurroundEnabled { get; set; } = false;
    public float FieldSurroundStrength { get; set; } = 0.40f;
    public float FieldSurroundMidImage { get; set; } = 0.50f;
    public bool VhsEnabled { get; set; } = false;
    public uint VhsLevel { get; set; } = 2;
    public bool AgcEnabled { get; set; } = false;
    public float AgcStrength { get; set; } = 0.50f;
    public float AgcMaxGainDb { get; set; } = 12f;
    public float AgcOutputThreshDb { get; set; } = -1f;

    public DspParamsModel Clone()
    {
        var c = (DspParamsModel)MemberwiseClone();
        c.EqBandsDb = (float[])EqBandsDb.Clone();
        c.EqBands18Db = (float[])EqBands18Db.Clone();
        return c;
    }
}

public sealed class PresetModel
{
    public string Name { get; set; } = "Custom Preset";
    public string Subtitle { get; set; } = "Custom saved preset";
    public DspParamsModel Params { get; set; } = new();
    public string? ActiveIrs { get; set; }
    public string? ActiveDdc { get; set; }
    [JsonIgnore] public bool IsBuiltin { get; set; }
}

public sealed class EqPresetModel
{
    public string Name { get; set; } = "Custom";
    public int EqMode { get; set; } = 0;
    public float[]? EqBandsDb { get; set; }
    public float[]? EqBands18Db { get; set; }
    [JsonIgnore] public bool IsBuiltin { get; set; }
}

public sealed class AudioDeviceModel
{
    public string Id { get; set; } = "";
    public string Guid { get; set; } = "";
    public string Name { get; set; } = "";
    public int SampleRate { get; set; } = 48000;
    public int Channels { get; set; } = 2;
    public int BitsPerSample { get; set; } = 32;
    public bool IsDefault { get; set; }
}

public sealed class AudioSessionModel
{
    public string AppName { get; set; } = "";
    public string ExeName { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public double Peak { get; set; }
}

public sealed class LiveSourceStatusModel
{
    public bool Active { get; set; }
    public string Source { get; set; } = "Windows Audio APO";
    public string Mode { get; set; } = "Standby";
    public int SampleRate { get; set; } = 48000;
    public int Channels { get; set; } = 2;
    public int BitDepth { get; set; } = 32;
    public int BitrateKbps { get; set; }
    public string Codec { get; set; } = "32-BIT FLOAT";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string DeviceName { get; set; } = "Windows Default Audio";
    public bool IsHiRes { get; set; }
    public bool ZeroResample { get; set; }
    public List<string> ExtraApps { get; set; } = new();
}

public sealed class UiSettingsModel
{
    public string SelectedPresetName { get; set; } = "Flat Reference";
    public string SelectedEqPresetName { get; set; } = "Flat";
    public List<string> PinnedPresets { get; set; } = new() { "Flat Reference", "Hi-Res Audiophile", "Deep Bass Boost", "Warm Tube Studio" };
    public List<string> PinnedModules { get; set; } = new();
    public string Theme { get; set; } = "Acrylic";
    public bool AutoAccent { get; set; } = false;
    public int AccentColorCount { get; set; } = 1; // 1 = Solid, 2 = 2-Color Gradient, 3 = 3-Color Gradient
    public string AccentColorHex { get; set; } = "#10B981";
    public string? AccentGradientMidHex { get; set; } = null;
    public string? AccentGradientEndHex { get; set; } = null;
}

public sealed class V4WaEngineBridge
{
    private const uint MagicVphr = 0x56504852; // 'VPHR'
    private const uint ParamsVersion = 5;
    private const int BlockSize = 348; // 4-byte seqlock + 344-byte DspParams

    public string ProgramDataDir { get; } = @"C:\ProgramData\ViPERHiRes";
    public string LiveParamsPath => Path.Combine(ProgramDataDir, "live_params.bin");
    public string FoobarStatusPath => Path.Combine(ProgramDataDir, "foobar_status.json");
    public string ApoDiagPath => Path.Combine(ProgramDataDir, "apo_diag.txt");
    public string ActiveIrPath => Path.Combine(ProgramDataDir, "active_ir.txt");
    public string ActiveDdcPath => Path.Combine(ProgramDataDir, "active_ddc.txt");
    public string UiSettingsPath => Path.Combine(ProgramDataDir, "v4wa_ui_settings.json");

    public string IrsDir { get; }
    public string DdcDir { get; }
    public string PresetsDir { get; }
    public string EqPresetsDir { get; }

    public string RepoRoot { get; }
    public string ViperCtlPath { get; }
    public string InstallScriptPath { get; }
    public string UninstallScriptPath { get; }

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly object _ipcLock = new();

    public V4WaEngineBridge()
    {
        RepoRoot = FindRepoRoot();

        string localCtl = Path.Combine(AppContext.BaseDirectory, "viper_ctl.exe");
        ViperCtlPath = File.Exists(localCtl) ? localCtl : Path.Combine(RepoRoot, "build", "viper_ctl.exe");

        string localInstall = Path.Combine(AppContext.BaseDirectory, "scripts", "install_apo.ps1");
        InstallScriptPath = File.Exists(localInstall) ? localInstall : Path.Combine(RepoRoot, "scripts", "install_apo.ps1");

        string localUninstall = Path.Combine(AppContext.BaseDirectory, "scripts", "uninstall_apo.ps1");
        UninstallScriptPath = File.Exists(localUninstall) ? localUninstall : Path.Combine(RepoRoot, "scripts", "uninstall_apo.ps1");

        // When installed in "<InstallRoot>\bin\", use "<InstallRoot>\irs", "<InstallRoot>\vdc", "<InstallRoot>\preset" directly!
        string? installRoot = Directory.GetParent(AppContext.BaseDirectory.TrimEnd('\\', '/'))?.FullName;
        bool hasInstalledAssetFolders = !string.IsNullOrEmpty(installRoot) &&
            (Directory.Exists(Path.Combine(installRoot, "irs")) ||
             Directory.Exists(Path.Combine(installRoot, "vdc")) ||
             Directory.Exists(Path.Combine(installRoot, "preset")));

        if (hasInstalledAssetFolders && installRoot != null)
        {
            IrsDir = Path.Combine(installRoot, "irs");
            DdcDir = Path.Combine(installRoot, "vdc");
            PresetsDir = Path.Combine(installRoot, "preset");
            EqPresetsDir = Path.Combine(installRoot, "preset", "eq");
        }
        else
        {
            IrsDir = Path.Combine(ProgramDataDir, "irs");
            DdcDir = Path.Combine(ProgramDataDir, "ddc");
            PresetsDir = Path.Combine(ProgramDataDir, "presets");
            EqPresetsDir = Path.Combine(ProgramDataDir, "eq_presets");
        }

        Directory.CreateDirectory(ProgramDataDir);
        try { Directory.CreateDirectory(IrsDir); } catch { }
        try { Directory.CreateDirectory(DdcDir); } catch { }
        try { Directory.CreateDirectory(PresetsDir); } catch { }
        try { Directory.CreateDirectory(EqPresetsDir); } catch { }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "viper_ctl.exe")) ||
                File.Exists(Path.Combine(dir.FullName, "build", "viper_ctl.exe")) ||
                File.Exists(Path.Combine(dir.FullName, "scripts", "install_apo.ps1")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }

    public bool IsComDllRegistered()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(
                @"CLSID\{8F4E2B1A-7C3D-4E9A-B12F-6A8C4D9E105A}\InprocServer32");
            var dllPath = key?.GetValue(null)?.ToString();
            return !string.IsNullOrWhiteSpace(dllPath) && File.Exists(dllPath);
        }
        catch { return false; }
    }

    public bool IsApoInstalledOnDevice(string deviceGuid)
    {
        if (string.IsNullOrWhiteSpace(deviceGuid) || !IsComDllRegistered()) return false;
        try
        {
            using var fxKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{deviceGuid}\FxProperties");
            var val = fxKey?.GetValue("{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},6")?.ToString();
            if (!string.Equals(val, "{8F4E2B1A-7C3D-4E9A-B12F-6A8C4D9E105A}", StringComparison.OrdinalIgnoreCase))
                return false;

            // Check Windows Audio Enhancements circuit-breaker (DisableSysFx == 1 means Windows bypassed the APO)
            var disFx = fxKey?.GetValue("{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5");
            if (disFx is int df && df != 0) return false;

            using var propsKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{deviceGuid}\Properties");
            var disPr = propsKey?.GetValue("{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5");
            if (disPr is int dp && dp != 0) return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsApoActiveOnDevice(AudioDeviceModel dev, LiveSourceStatusModel status, bool masterEnabled)
    {
        if (!masterEnabled || !dev.IsDefault || !IsApoInstalledOnDevice(dev.Guid)) return false;
        if (!string.Equals(status.Mode, "Shared Mode", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            if (!File.Exists(ApoDiagPath)) return false;
            string diag = File.ReadAllText(ApoDiagPath);
            return diag.Contains("LockForProcess OK", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // ==================== DIRECT BINARY SEQLOCK IPC (< 0.1 ms) ====================

    public DspParamsModel ReadLiveParams()
    {
        lock (_ipcLock)
        {
            if (!File.Exists(LiveParamsPath))
            {
                var def = new DspParamsModel();
                WriteLiveParams(def);
                return def;
            }

            byte[] buf = new byte[BlockSize];
            using (var fs = new FileStream(LiveParamsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < BlockSize)
                {
                    var def = new DspParamsModel();
                    WriteLiveParams(def);
                    return def;
                }
                fs.ReadExactly(buf, 0, BlockSize);
            }

            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(4, 4));
            if (magic != MagicVphr)
            {
                var def = new DspParamsModel();
                WriteLiveParams(def);
                return def;
            }

            static float R(byte[] b, int offset) => MathF.Round(BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(offset, 4)), 2);
            static uint U(byte[] b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset, 4));

            var p = new DspParamsModel
            {
                Sequence = U(buf, 12),
                MasterEnabled = U(buf, 16) != 0,
                MasterGainDb = R(buf, 20),
                OutputPan = R(buf, 24),
                EqEnabled = U(buf, 28) != 0,
                BassEnabled = U(buf, 72) != 0,
                BassMode = U(buf, 76),
                BassFrequencyHz = R(buf, 80),
                BassGainDb = R(buf, 84),
                ClarityEnabled = U(buf, 88) != 0,
                ClarityMode = U(buf, 92),
                ClarityGainDb = R(buf, 96),
                TubeEnabled = U(buf, 100) != 0,
                TubeDrive = R(buf, 104),
                SurroundEnabled = U(buf, 108) != 0,
                SurroundDelayMs = R(buf, 112),
                StereoWidth = R(buf, 116),
                LimiterEnabled = U(buf, 120) != 0,
                LimiterThresholdDb = R(buf, 124),
                DynamicEnabled = U(buf, 128) != 0,
                DynamicDevice = U(buf, 132),
                DynamicBass = R(buf, 136),
                ReverbEnabled = U(buf, 140) != 0,
                ReverbRoomSize = R(buf, 144),
                ReverbWidth = R(buf, 148),
                ReverbDamping = R(buf, 152),
                ReverbWet = R(buf, 156),
                ReverbDry = R(buf, 160),
                CompEnabled = U(buf, 164) != 0,
                CompThresholdDb = R(buf, 168),
                CompRatio = R(buf, 172),
                CompAttackMs = R(buf, 176),
                CompReleaseMs = R(buf, 180),
                CompMakeupDb = R(buf, 184),
                CureEnabled = U(buf, 188) != 0,
                CureLevel = U(buf, 192),
                ConvolverEnabled = U(buf, 196) != 0,
                ConvolverCrossChannel = R(buf, 200),
                ConvolverWetMix = R(buf, 204),
                ConvolverRevision = U(buf, 208),
                EqMode = U(buf, 212),
                DdcEnabled = U(buf, 288) != 0,
                DdcRevision = U(buf, 292),
                VseEnabled = U(buf, 296) != 0,
                VseStrength = R(buf, 300),
                AnalogXEnabled = U(buf, 304) != 0,
                AnalogXLevel = U(buf, 308),
                FieldSurroundEnabled = U(buf, 312) != 0,
                FieldSurroundStrength = R(buf, 316),
                FieldSurroundMidImage = R(buf, 320),
                VhsEnabled = U(buf, 324) != 0,
                VhsLevel = U(buf, 328),
                AgcEnabled = U(buf, 332) != 0,
                AgcStrength = R(buf, 336),
                AgcMaxGainDb = R(buf, 340),
                AgcOutputThreshDb = R(buf, 344)
            };

            for (int i = 0; i < 10; i++) p.EqBandsDb[i] = R(buf, 32 + i * 4);
            for (int i = 0; i < 18; i++) p.EqBands18Db[i] = R(buf, 216 + i * 4);
            return p;
        }
    }

    public void WriteLiveParams(DspParamsModel p)
    {
        lock (_ipcLock)
        {
            Directory.CreateDirectory(ProgramDataDir);
            if (!File.Exists(LiveParamsPath) || new FileInfo(LiveParamsPath).Length < BlockSize)
            {
                File.WriteAllBytes(LiveParamsPath, new byte[BlockSize]);
            }

            using var fs = new FileStream(LiveParamsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            byte[] seqBytes = new byte[4];
            fs.Position = 0;
            fs.ReadExactly(seqBytes, 0, 4);
            uint seq = BinaryPrimitives.ReadUInt32LittleEndian(seqBytes);
            if ((seq & 1) != 0) seq += 1;
            uint oddSeq = seq + 1;
            uint evenSeq = seq + 2;

            // 1. Mark seqlock odd (write in progress)
            BinaryPrimitives.WriteUInt32LittleEndian(seqBytes, oddSeq);
            fs.Position = 0;
            fs.Write(seqBytes, 0, 4);
            fs.Flush();

            // 2. Write payload [4..348)
            byte[] payload = new byte[BlockSize - 4];
            static void WU(byte[] b, int off, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(off, 4), v);
            static void WF(byte[] b, int off, float v) => BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(off, 4), v);

            WU(payload, 0, MagicVphr);
            WU(payload, 4, ParamsVersion);
            WU(payload, 8, evenSeq >> 1);
            WU(payload, 12, p.MasterEnabled ? 1u : 0u);
            WF(payload, 16, p.MasterGainDb);
            WF(payload, 20, p.OutputPan);
            WU(payload, 24, p.EqEnabled ? 1u : 0u);
            for (int i = 0; i < 10; i++) WF(payload, 28 + i * 4, i < p.EqBandsDb.Length ? p.EqBandsDb[i] : 0f);
            WU(payload, 68, p.BassEnabled ? 1u : 0u);
            WU(payload, 72, p.BassMode);
            WF(payload, 76, p.BassFrequencyHz);
            WF(payload, 80, p.BassGainDb);
            WU(payload, 84, p.ClarityEnabled ? 1u : 0u);
            WU(payload, 88, p.ClarityMode);
            WF(payload, 92, p.ClarityGainDb);
            WU(payload, 96, p.TubeEnabled ? 1u : 0u);
            WF(payload, 100, p.TubeDrive);
            WU(payload, 104, p.SurroundEnabled ? 1u : 0u);
            WF(payload, 108, p.SurroundDelayMs);
            WF(payload, 112, p.StereoWidth);
            WU(payload, 116, p.LimiterEnabled ? 1u : 0u);
            WF(payload, 120, p.LimiterThresholdDb);
            WU(payload, 124, p.DynamicEnabled ? 1u : 0u);
            WU(payload, 128, p.DynamicDevice);
            WF(payload, 132, p.DynamicBass);
            WU(payload, 136, p.ReverbEnabled ? 1u : 0u);
            WF(payload, 140, p.ReverbRoomSize);
            WF(payload, 144, p.ReverbWidth);
            WF(payload, 148, p.ReverbDamping);
            WF(payload, 152, p.ReverbWet);
            WF(payload, 156, p.ReverbDry);
            WU(payload, 160, p.CompEnabled ? 1u : 0u);
            WF(payload, 164, p.CompThresholdDb);
            WF(payload, 168, p.CompRatio);
            WF(payload, 172, p.CompAttackMs);
            WF(payload, 176, p.CompReleaseMs);
            WF(payload, 180, p.CompMakeupDb);
            WU(payload, 184, p.CureEnabled ? 1u : 0u);
            WU(payload, 188, p.CureLevel);
            WU(payload, 192, p.ConvolverEnabled ? 1u : 0u);
            WF(payload, 196, p.ConvolverCrossChannel);
            WF(payload, 200, p.ConvolverWetMix);
            WU(payload, 204, p.ConvolverRevision);
            WU(payload, 208, p.EqMode);
            for (int i = 0; i < 18; i++) WF(payload, 212 + i * 4, i < p.EqBands18Db.Length ? p.EqBands18Db[i] : 0f);
            WU(payload, 284, p.DdcEnabled ? 1u : 0u);
            WU(payload, 288, p.DdcRevision);
            WU(payload, 292, p.VseEnabled ? 1u : 0u);
            WF(payload, 296, p.VseStrength);
            WU(payload, 300, p.AnalogXEnabled ? 1u : 0u);
            WU(payload, 304, p.AnalogXLevel);
            WU(payload, 308, p.FieldSurroundEnabled ? 1u : 0u);
            WF(payload, 312, p.FieldSurroundStrength);
            WF(payload, 316, p.FieldSurroundMidImage);
            WU(payload, 320, p.VhsEnabled ? 1u : 0u);
            WU(payload, 324, p.VhsLevel);
            WU(payload, 328, p.AgcEnabled ? 1u : 0u);
            WF(payload, 332, p.AgcStrength);
            WF(payload, 336, p.AgcMaxGainDb);
            WF(payload, 340, p.AgcOutputThreshDb);

            fs.Position = 4;
            fs.Write(payload, 0, payload.Length);
            fs.Flush();

            // 3. Mark seqlock even (write committed)
            BinaryPrimitives.WriteUInt32LittleEndian(seqBytes, evenSeq);
            fs.Position = 0;
            fs.Write(seqBytes, 0, 4);
            fs.Flush();
        }
    }

    // ==================== LIVE TELEMETRY & DEVICES ====================

    private async Task<string> RunViperCtlAsync(string arg)
    {
        if (!File.Exists(ViperCtlPath)) return "[]";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ViperCtlPath,
                Arguments = arg,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return "[]";
            string output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return string.IsNullOrWhiteSpace(output) ? "[]" : output.Trim();
        }
        catch
        {
            return "[]";
        }
    }

    public static bool IsHandsFreeDevice(AudioDeviceModel dev)
    {
        string name = dev.Name ?? "";
        if (name.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Hands Free", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("AG Audio", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("(HFP)", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (dev.Channels == 1 && dev.SampleRate > 0 && dev.SampleRate <= 24000)
        {
            return true;
        }
        return false;
    }

    public async Task<List<AudioDeviceModel>> GetDevicesAsync()
    {
        try
        {
            string json = await RunViperCtlAsync("devices");
            var all = JsonSerializer.Deserialize<List<AudioDeviceModel>>(json, JsonOpts) ?? new();
            return all.Where(d => !IsHandsFreeDevice(d)).ToList();
        }
        catch { return new(); }
    }

    public async Task<List<AudioSessionModel>> GetSessionsAsync()
    {
        try
        {
            string json = await RunViperCtlAsync("sessions");
            return JsonSerializer.Deserialize<List<AudioSessionModel>>(json, JsonOpts) ?? new();
        }
        catch { return new(); }
    }

    public async Task<(LiveSourceStatusModel Status, List<AudioDeviceModel> Devices)> PollLiveTelemetryAsync()
    {
        var devTask = GetDevicesAsync();
        var sessTask = GetSessionsAsync();
        await Task.WhenAll(devTask, sessTask);

        var devices = devTask.Result;
        var sessions = sessTask.Result;
        var defaultDev = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();

        // 1. Check foobar2000 live telemetry (1.8s freshness + playing == true)
        try
        {
            if (File.Exists(FoobarStatusPath))
            {
                var info = new FileInfo(FoobarStatusPath);
                double ageMs = (DateTime.UtcNow - info.LastWriteTimeUtc).TotalMilliseconds;
                if (ageMs < 1800)
                {
                    using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(FoobarStatusPath));
                    var root = doc.RootElement;
                    bool playing = !root.TryGetProperty("playing", out var plEl) || plEl.GetBoolean();
                    int sr = root.TryGetProperty("sampleRate", out var srEl) ? srEl.GetInt32() : 0;
                    if (playing && sr > 0)
                    {
                        bool foobarHasShared = sessions.Any(s =>
                            s.ExeName.Contains("foobar", StringComparison.OrdinalIgnoreCase) ||
                            s.AppName.Contains("foobar", StringComparison.OrdinalIgnoreCase));

                        int bd = root.TryGetProperty("bitDepth", out var bdEl) ? bdEl.GetInt32() : 16;
                        int br = root.TryGetProperty("bitrateKbps", out var brEl) ? brEl.GetInt32() : 0;
                        int ch = root.TryGetProperty("channels", out var chEl) ? chEl.GetInt32() : 2;
                        string codec = root.TryGetProperty("codec", out var cEl) ? (cEl.GetString() ?? "PCM") : "PCM";
                        string title = root.TryGetProperty("title", out var tEl) ? (tEl.GetString() ?? "") : "";
                        string artist = root.TryGetProperty("artist", out var aEl) ? (aEl.GetString() ?? "") : "";

                        var extraApps = sessions
                            .Where(s => !s.ExeName.Contains("foobar", StringComparison.OrdinalIgnoreCase))
                            .Select(s => s.AppName)
                            .Distinct()
                            .ToList();

                        return (new LiveSourceStatusModel
                        {
                            Active = true,
                            Source = "foobar2000",
                            Mode = foobarHasShared ? "Shared Mode" : "Exclusive Mode",
                            SampleRate = sr,
                            Channels = ch,
                            BitDepth = bd,
                            BitrateKbps = br,
                            Codec = codec,
                            Title = title,
                            Artist = artist,
                            DeviceName = defaultDev?.Name ?? "Windows Default Audio",
                            IsHiRes = sr > 48000 || bd > 16,
                            ZeroResample = !foobarHasShared,
                            ExtraApps = extraApps
                        }, devices);
                    }
                }
            }
        }
        catch { }

        // 2. Fallback: Windows APO + Active Audio Sessions
        int apoSr = defaultDev?.SampleRate ?? 48000;
        int apoCh = defaultDev?.Channels ?? 2;
        bool apoLocked = false;
        try
        {
            if (File.Exists(ApoDiagPath))
            {
                string txt = await File.ReadAllTextAsync(ApoDiagPath);
                var mSr = Regex.Match(txt, @"SR=(\d+)");
                var mCh = Regex.Match(txt, @"Ch=(\d+)");
                if (mSr.Success) { apoSr = int.Parse(mSr.Groups[1].Value); apoLocked = true; }
                if (mCh.Success) apoCh = int.Parse(mCh.Groups[1].Value);
            }
        }
        catch { }

        var primary = sessions.FirstOrDefault();
        var extras = sessions.Skip(1).Select(s => s.AppName).Distinct().ToList();

        return (new LiveSourceStatusModel
        {
            Active = primary != null || apoLocked,
            Source = primary != null ? primary.AppName : "Windows Audio APO",
            Mode = primary != null ? "Shared Mode" : "Standby",
            SampleRate = apoSr,
            Channels = apoCh,
            BitDepth = defaultDev?.BitsPerSample ?? 32,
            BitrateKbps = 0,
            Codec = "32-BIT FLOAT",
            Title = primary?.WindowTitle ?? "",
            Artist = "",
            DeviceName = defaultDev?.Name ?? "Windows Default Audio",
            IsHiRes = false,
            ZeroResample = false,
            ExtraApps = extras
        }, devices);
    }

    // ==================== CONVOLVER (.irs/.wav) & VIPER-DDC (.vdc) ====================

    public (List<string> Files, string Active) ListIrsFiles()
    {
        Directory.CreateDirectory(IrsDir);
        var files = Directory.GetFiles(IrsDir)
            .Select(Path.GetFileName)
            .Where(f => f != null && (f.EndsWith(".irs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)))
            .Cast<string>()
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string active = "";
        try { if (File.Exists(ActiveIrPath)) active = Path.GetFileName(File.ReadAllText(ActiveIrPath).Trim()); } catch { }
        return (files, active);
    }

    public uint SelectIrsFile(string filename, DspParamsModel curParams)
    {
        string full = Path.Combine(IrsDir, Path.GetFileName(filename));
        if (!File.Exists(full)) return curParams.ConvolverRevision;
        File.WriteAllText(ActiveIrPath, full);
        curParams.ConvolverRevision += 1;
        WriteLiveParams(curParams);
        return curParams.ConvolverRevision;
    }

    public (List<string> Files, string Active) ImportIrsFile(string sourceFilePath, DspParamsModel curParams)
    {
        Directory.CreateDirectory(IrsDir);
        string safe = Path.GetFileName(sourceFilePath);
        string dest = Path.Combine(IrsDir, safe);
        File.Copy(sourceFilePath, dest, overwrite: true);
        File.WriteAllText(ActiveIrPath, dest);
        curParams.ConvolverEnabled = true;
        curParams.ConvolverRevision += 1;
        WriteLiveParams(curParams);
        return ListIrsFiles();
    }

    public (List<string> Files, string Active) ListDdcFiles()
    {
        Directory.CreateDirectory(DdcDir);
        var files = Directory.GetFiles(DdcDir)
            .Select(Path.GetFileName)
            .Where(f => f != null && f.EndsWith(".vdc", StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string active = "";
        try { if (File.Exists(ActiveDdcPath)) active = Path.GetFileName(File.ReadAllText(ActiveDdcPath).Trim()); } catch { }
        return (files, active);
    }

    public uint SelectDdcFile(string filename, DspParamsModel curParams)
    {
        string full = Path.Combine(DdcDir, Path.GetFileName(filename));
        if (!File.Exists(full)) return curParams.DdcRevision;
        File.WriteAllText(ActiveDdcPath, full);
        curParams.DdcRevision += 1;
        WriteLiveParams(curParams);
        return curParams.DdcRevision;
    }

    public (List<string> Files, string Active) ImportDdcFile(string sourceFilePath, DspParamsModel curParams)
    {
        Directory.CreateDirectory(DdcDir);
        string safe = Path.GetFileName(sourceFilePath);
        string dest = Path.Combine(DdcDir, safe);
        File.Copy(sourceFilePath, dest, overwrite: true);
        File.WriteAllText(ActiveDdcPath, dest);
        curParams.DdcEnabled = true;
        curParams.DdcRevision += 1;
        WriteLiveParams(curParams);
        return ListDdcFiles();
    }

    // ==================== PRESETS & EQ PRESETS ====================

    private static string SanitizeName(string name)
    {
        string s = Regex.Replace(name.Trim(), @"[\\/:*?""<>|]", "_");
        return string.IsNullOrWhiteSpace(s) ? "Custom Preset" : s;
    }

    public List<PresetModel> ListCustomPresets()
    {
        Directory.CreateDirectory(PresetsDir);
        var list = new List<PresetModel>();
        foreach (var file in Directory.GetFiles(PresetsDir, "*.json").OrderBy(f => f))
        {
            try
            {
                var p = JsonSerializer.Deserialize<PresetModel>(File.ReadAllText(file), JsonOpts);
                if (p != null)
                {
                    if (string.IsNullOrWhiteSpace(p.Name)) p.Name = Path.GetFileNameWithoutExtension(file);
                    p.Params ??= new DspParamsModel();
                    list.Add(p);
                }
            }
            catch { }
        }
        return list;
    }

    public string SaveCustomPreset(PresetModel preset)
    {
        Directory.CreateDirectory(PresetsDir);
        string safe = SanitizeName(preset.Name);
        preset.Name = safe;
        File.WriteAllText(Path.Combine(PresetsDir, $"{safe}.json"), JsonSerializer.Serialize(preset, JsonOpts));
        return safe;
    }

    public List<EqPresetModel> ListCustomEqPresets()
    {
        Directory.CreateDirectory(EqPresetsDir);
        var list = new List<EqPresetModel>();
        foreach (var file in Directory.GetFiles(EqPresetsDir, "*.json").OrderBy(f => f))
        {
            try
            {
                var ep = JsonSerializer.Deserialize<EqPresetModel>(File.ReadAllText(file), JsonOpts);
                if (ep != null)
                {
                    if (string.IsNullOrWhiteSpace(ep.Name)) ep.Name = Path.GetFileNameWithoutExtension(file);
                    list.Add(ep);
                }
            }
            catch { }
        }
        return list;
    }

    public string SaveCustomEqPreset(EqPresetModel preset)
    {
        Directory.CreateDirectory(EqPresetsDir);
        string safe = SanitizeName(preset.Name);
        preset.Name = safe;
        File.WriteAllText(Path.Combine(EqPresetsDir, $"{safe}.json"), JsonSerializer.Serialize(preset, JsonOpts));
        return safe;
    }

    // ==================== UI SETTINGS & APO SCRIPTS ====================

    public UiSettingsModel LoadUiSettings()
    {
        try
        {
            if (File.Exists(UiSettingsPath))
            {
                return JsonSerializer.Deserialize<UiSettingsModel>(File.ReadAllText(UiSettingsPath), JsonOpts) ?? new();
            }
        }
        catch { }
        return new();
    }

    public void SaveUiSettings(UiSettingsModel settings)
    {
        try
        {
            Directory.CreateDirectory(ProgramDataDir);
            File.WriteAllText(UiSettingsPath, JsonSerializer.Serialize(settings, JsonOpts));
        }
        catch { }
    }

    public bool IsFoobarPluginInstalled()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "foobar2000-v2", "user-components-x64", "foo_dsp_viperhires", "foo_dsp_viperhires.dll");
        return File.Exists(path);
    }

    public async Task<(bool Launched, int ExitCode)> RunApoScriptElevatedAsync(bool install, string endpointGuid)
    {
        string script = install ? InstallScriptPath : UninstallScriptPath;
        if (!File.Exists(script)) return (false, -1);
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\" -EndpointGuid \"{endpointGuid}\"",
                WorkingDirectory = Environment.SystemDirectory,
                Verb = "runas",
                UseShellExecute = true
            });
            if (proc == null) return (false, -1);
            await proc.WaitForExitAsync();
            return (true, proc.ExitCode);
        }
        catch
        {
            return (false, -1);
        }
    }

    public async Task<bool> RestartAudioServiceElevatedAsync()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -WindowStyle Hidden -Command \"Start-Service AudioEndpointBuilder -EA SilentlyContinue; Restart-Service Audiosrv -Force -EA SilentlyContinue; Start-Service Audiosrv -EA SilentlyContinue\"",
                WorkingDirectory = Environment.SystemDirectory,
                Verb = "runas",
                UseShellExecute = true
            });
            if (proc == null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
