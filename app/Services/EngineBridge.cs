using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ViPER4WindowsRE.Services;

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

public sealed class UpdateCheckResult
{
    public bool Success { get; set; }
    public bool HasUpdate { get; set; }
    public string CurrentVersion { get; set; } = EngineBridge.AppVersion;
    public string LatestVersion { get; set; } = EngineBridge.AppVersion;
    public string DownloadUrl { get; set; } = "";
    public string AssetName { get; set; } = "";
    public long AssetSizeBytes { get; set; }
    public string ReleaseNotes { get; set; } = "";
    public string ReleasePageUrl { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}

public sealed class UiSettingsModel
{
    public string SelectedPresetName { get; set; } = "Flat";
    public string SelectedEqPresetName { get; set; } = "Flat";
    public List<string> PinnedPresets { get; set; } = new() { "Flat", "Hi-Res", "Bass Boost" };
    public List<string> PinnedModules { get; set; } = new();
    public string Theme { get; set; } = "Acrylic";
    public bool AutoAccent { get; set; } = false;
    public int AccentColorCount { get; set; } = 1; // 1 = Solid, 2 = 2-Color Gradient, 3 = 3-Color Gradient
    public string AccentColorHex { get; set; } = "#10B981";
    public string? AccentGradientMidHex { get; set; } = null;
    public string? AccentGradientEndHex { get; set; } = null;
}

public sealed class EngineBridge
{
    public const string AppVersion = "1.1.0";
    public const string GitHubRepo = "K4Z3xd/ViPER4Windows-RE";
    private const uint MagicVphr = 0x56504852; // 'VPHR'
    private const uint ParamsVersion = 5;
    private const int BlockSize = 348; // 4-byte seqlock + 344-byte DspParams

    public string ProgramDataDir { get; } = @"C:\ProgramData\ViPERHiRes";
    public string LiveParamsPath => Path.Combine(ProgramDataDir, "live_params.bin");
    public string FoobarStatusPath => Path.Combine(ProgramDataDir, "foobar_status.json");
    public string ApoDiagPath => Path.Combine(LogsDir, "apo_diag.txt");
    public string ActiveIrPath => Path.Combine(ProgramDataDir, "active_ir.txt");
    public string ActiveDdcPath => Path.Combine(ProgramDataDir, "active_ddc.txt");
    public string UiSettingsPath => Path.Combine(ProgramDataDir, "v4wa_ui_settings.json");

    public string IrsDir { get; }
    public string DdcDir { get; }
    public string PresetsDir { get; }
    public string EqPresetsDir { get; }
    public string LogsDir { get; }

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

    public EngineBridge()
    {
        RepoRoot = FindRepoRoot();

        string localCtl = Path.Combine(AppContext.BaseDirectory, "viper_ctl.exe");
        ViperCtlPath = File.Exists(localCtl) ? localCtl : Path.Combine(RepoRoot, "build", "viper_ctl.exe");

        string localInstall = Path.Combine(AppContext.BaseDirectory, "scripts", "install_apo.ps1");
        InstallScriptPath = File.Exists(localInstall) ? localInstall : Path.Combine(RepoRoot, "scripts", "install_apo.ps1");

        string localUninstall = Path.Combine(AppContext.BaseDirectory, "scripts", "uninstall_apo.ps1");
        UninstallScriptPath = File.Exists(localUninstall) ? localUninstall : Path.Combine(RepoRoot, "scripts", "uninstall_apo.ps1");

        // When installed in "<InstallRoot>\bin\", use "<InstallRoot>\irs", "vdc", "preset", "logs" directly!
        string? installRoot = Directory.GetParent(AppContext.BaseDirectory.TrimEnd('\\', '/'))?.FullName;
        bool hasInstalledAssetFolders = !string.IsNullOrEmpty(installRoot) &&
            (Directory.Exists(Path.Combine(installRoot, "irs")) ||
             Directory.Exists(Path.Combine(installRoot, "vdc")) ||
             Directory.Exists(Path.Combine(installRoot, "preset")) ||
             Directory.Exists(Path.Combine(installRoot, "logs")));

        if (hasInstalledAssetFolders && installRoot != null)
        {
            IrsDir = Path.Combine(installRoot, "irs");
            DdcDir = Path.Combine(installRoot, "vdc");
            PresetsDir = Path.Combine(installRoot, "preset");
            EqPresetsDir = Path.Combine(installRoot, "preset", "eq");
            LogsDir = Path.Combine(installRoot, "logs");
        }
        else
        {
            IrsDir = Path.Combine(ProgramDataDir, "irs");
            DdcDir = Path.Combine(ProgramDataDir, "ddc");
            PresetsDir = Path.Combine(ProgramDataDir, "presets");
            EqPresetsDir = Path.Combine(ProgramDataDir, "eq_presets");
            LogsDir = ProgramDataDir;
        }

        Directory.CreateDirectory(ProgramDataDir);
        try { Directory.CreateDirectory(IrsDir); } catch { }
        try { Directory.CreateDirectory(DdcDir); } catch { }
        try { Directory.CreateDirectory(PresetsDir); } catch { }
        try { Directory.CreateDirectory(EqPresetsDir); } catch { }
        try { Directory.CreateDirectory(LogsDir); } catch { }
        if (!string.Equals(LogsDir, ProgramDataDir, StringComparison.OrdinalIgnoreCase))
        {
            try { File.WriteAllText(Path.Combine(ProgramDataDir, "logs_dir.txt"), LogsDir, new UTF8Encoding(false)); } catch { }
        }
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
            string diagFile = File.Exists(ApoDiagPath) ? ApoDiagPath : Path.Combine(ProgramDataDir, "apo_diag.txt");
            if (File.Exists(diagFile))
            {
                string txt = await File.ReadAllTextAsync(diagFile);
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

    // ==================== PRESETS & COMMUNITY IMPORTER (Native JSON, V4A JSON v1/v2, XML) ====================

    private static string SanitizeName(string name)
    {
        string s = Regex.Replace(name.Trim(), @"[\\/:*?""<>|]", "_");
        return string.IsNullOrWhiteSpace(s) ? "Custom Preset" : s;
    }

    public static float[] ResampleEqBands(float[] src, int targetLen)
    {
        var dst = new float[targetLen];
        if (src == null || src.Length == 0) return dst;
        if (src.Length == targetLen)
        {
            for (int i = 0; i < targetLen; i++) dst[i] = Math.Clamp(src[i], -12f, 12f);
            return dst;
        }
        if (src.Length == 1)
        {
            Array.Fill(dst, Math.Clamp(src[0], -12f, 12f));
            return dst;
        }
        for (int i = 0; i < targetLen; i++)
        {
            double pos = (double)i * (src.Length - 1) / (targetLen - 1);
            int lo = (int)Math.Floor(pos);
            int hi = Math.Min(src.Length - 1, lo + 1);
            double frac = pos - lo;
            float val = (float)(src[lo] * (1.0 - frac) + src[hi] * frac);
            dst[i] = MathF.Round(Math.Clamp(val, -12f, 12f) * 2f) / 2f;
        }
        return dst;
    }

    private static float[] ParseSemicolonFloats(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Array.Empty<float>();
        var list = new List<float>();
        foreach (var tok in s.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (float.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                list.Add(v);
        }
        return list.ToArray();
    }

    private static float V4aVolumeToDb(double pct) =>
        pct <= 1 ? -24f : (float)Math.Clamp(20.0 * Math.Log10(pct / 100.0), -24.0, 12.0);

    private static float V4aLimiterToDb(double pct) =>
        pct >= 100 ? -0.3f : (float)Math.Clamp(20.0 * Math.Log10(Math.Max(pct, 1.0) / 100.0), -6.0, 0.0);

    private static float V4aBassGainToDb(double val) =>
        val <= 15.0 ? (float)Math.Clamp(val, 0.0, 15.0) : (float)Math.Clamp(val * 0.03, 0.0, 15.0);

    private static float V4aClarityGainToDb(double val) =>
        val <= 15.0 ? (float)Math.Clamp(val, 0.0, 15.0) : (float)Math.Clamp(val * 0.045, 0.0, 15.0);

    public PresetModel? ParsePresetFile(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        string ext = Path.GetExtension(filePath);
        string fallbackName = Path.GetFileNameWithoutExtension(filePath);
        string content = File.ReadAllText(filePath);

        if (ext.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            return ParseV4aXmlPreset(content, fallbackName);

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;

        // 1. Native ViPER4Windows RE format (has "params" object)
        if (root.TryGetProperty("params", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Object)
        {
            var native = JsonSerializer.Deserialize<PresetModel>(content, JsonOpts);
            if (native != null)
            {
                if (string.IsNullOrWhiteSpace(native.Name)) native.Name = fallbackName;
                native.Params ??= new DspParamsModel();
                return native;
            }
        }

        // 2. ViPER4Android Community JSON v2 ("schemaVersion": 2)
        if (root.TryGetProperty("schemaVersion", out var sv) && sv.ValueKind == JsonValueKind.Number && sv.GetInt32() >= 2)
            return ParseV4aJsonV2(root, fallbackName);

        // 3. ViPER4Android Community JSON v1 (flat keys — headset/bluetooth or spk* speaker keys)
        if (root.TryGetProperty("eqBands", out _) || root.TryGetProperty("spkEqBands", out _) ||
            root.TryGetProperty("bassEnabled", out _) || root.TryGetProperty("spkBassEnabled", out _) ||
            root.TryGetProperty("convolverKernel", out _) || root.TryGetProperty("spkConvolverKernel", out _) ||
            root.TryGetProperty("ddcDevice", out _) || root.TryGetProperty("masterEnabled", out _) ||
            root.TryGetProperty("spkMasterEnabled", out _))
            return ParseV4aJsonV1(root, fallbackName);

        return null;
    }

    private static bool TryGetV1Prop(JsonElement obj, string prop, out JsonElement el)
    {
        if (obj.TryGetProperty(prop, out el)) return true;
        if (prop.Length > 0)
        {
            string spkProp = "spk" + char.ToUpperInvariant(prop[0]) + prop[1..];
            if (obj.TryGetProperty(spkProp, out el)) return true;
        }
        el = default;
        return false;
    }

    private static bool GetBool(JsonElement obj, string prop, bool def = false) =>
        TryGetV1Prop(obj, prop, out var el) && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False)
            ? el.GetBoolean() : def;

    private static double GetNum(JsonElement obj, string prop, double def = 0)
    {
        if (!TryGetV1Prop(obj, prop, out var el)) return def;
        if (el.ValueKind == JsonValueKind.Number) return el.GetDouble();
        if (el.ValueKind == JsonValueKind.String && double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return v;
        return def;
    }

    private static string? GetStr(JsonElement obj, string prop) =>
        TryGetV1Prop(obj, prop, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static PresetModel ParseV4aJsonV1(JsonElement r, string name)
    {
        var p = new DspParamsModel
        {
            MasterEnabled = GetBool(r, "masterEnabled", true),
            MasterGainDb = V4aVolumeToDb(GetNum(r, "outputVolume", 100)),
            OutputPan = (float)Math.Clamp(GetNum(r, "channelPan", 0) / 100.0, -1.0, 1.0),
            LimiterEnabled = true,
            LimiterThresholdDb = V4aLimiterToDb(GetNum(r, "limiter", 100)),

            EqEnabled = GetBool(r, "eqEnabled"),
            BassEnabled = GetBool(r, "bassEnabled"),
            BassMode = (uint)Math.Clamp((int)GetNum(r, "bassMode", 0), 0, 2),
            BassFrequencyHz = (float)Math.Clamp(GetNum(r, "bassFrequency", 60), 30, 120),
            BassGainDb = V4aBassGainToDb(GetNum(r, "bassGain", 100)),

            ClarityEnabled = GetBool(r, "clarityEnabled"),
            ClarityMode = (uint)Math.Clamp((int)GetNum(r, "clarityMode", 0), 0, 2),
            ClarityGainDb = V4aClarityGainToDb(GetNum(r, "clarityGain", 100)),

            TubeEnabled = GetBool(r, "tubeSimulatorEnabled"),
            TubeDrive = 0.35f,
            AnalogXEnabled = GetBool(r, "analogxEnabled"),
            AnalogXLevel = (uint)Math.Clamp((int)GetNum(r, "analogxMode", 0), 0, 2),

            DdcEnabled = GetBool(r, "ddcEnabled"),
            ConvolverEnabled = GetBool(r, "convolverEnabled"),
            ConvolverCrossChannel = (float)Math.Clamp(GetNum(r, "convolverCrossChannel", 0) / 100.0, 0.0, 1.0),
            ConvolverWetMix = 1.0f,

            VseEnabled = GetBool(r, "vseEnabled"),
            VseStrength = (float)Math.Clamp(GetNum(r, "vseStrength", 5000) / (GetNum(r, "vseStrength", 5000) > 100 ? 10000.0 : 100.0), 0.0, 1.0),

            DynamicEnabled = GetBool(r, "dynamicSystemEnabled"),
            DynamicDevice = (uint)Math.Clamp((int)GetNum(r, "dynamicSystemDevice", 0), 0, 3),
            DynamicBass = (float)Math.Clamp(GetNum(r, "dynamicSystemStrength", 25), 0, 100),

            FieldSurroundEnabled = GetBool(r, "fieldSurroundEnabled"),
            FieldSurroundStrength = (float)Math.Clamp(GetNum(r, "fieldSurroundWidening", 3) / 8.0, 0.0, 1.0),
            FieldSurroundMidImage = (float)Math.Clamp(GetNum(r, "fieldSurroundMidImage", 5) / 10.0, 0.0, 1.0),

            SurroundEnabled = GetBool(r, "diffSurroundEnabled"),
            SurroundDelayMs = (float)Math.Clamp(GetNum(r, "diffSurroundDelay", 6), 0, 20),
            StereoWidth = 1.0f,

            VhsEnabled = GetBool(r, "vheEnabled"),
            VhsLevel = (uint)Math.Clamp((int)GetNum(r, "vheQuality", 1) + 1, 1, 5),

            ReverbEnabled = GetBool(r, "reverbEnabled"),
            ReverbRoomSize = (float)Math.Clamp(GetNum(r, "reverbRoomSize", 35) / 100.0, 0.0, 1.0),
            ReverbWidth = (float)Math.Clamp(GetNum(r, "reverbWidth", 50) / 100.0, 0.0, 1.0),
            ReverbDamping = (float)Math.Clamp(GetNum(r, "reverbDampening", 25) / 100.0, 0.0, 1.0),
            ReverbWet = (float)Math.Clamp(GetNum(r, "reverbWet", 20) / 100.0, 0.0, 1.0),
            ReverbDry = (float)Math.Clamp(GetNum(r, "reverbDry", 85) / 100.0, 0.0, 1.0),

            CompEnabled = GetBool(r, "fetEnabled"),
            CompThresholdDb = (float)Math.Clamp(GetNum(r, "fetThreshold", -18), -40, 0),
            CompRatio = (float)Math.Clamp(Math.Max(1.0, GetNum(r, "fetRatio", 30) / 10.0), 1, 20),
            CompAttackMs = (float)Math.Clamp(GetNum(r, "fetAttack", 10), 1, 100),
            CompReleaseMs = (float)Math.Clamp(GetNum(r, "fetRelease", 120), 20, 500),
            CompMakeupDb = (float)Math.Clamp(GetNum(r, "fetGain", 0), 0, 18),

            AgcEnabled = GetBool(r, "agcEnabled"),
            AgcStrength = (float)Math.Clamp(GetNum(r, "agcStrength", 50) / 100.0, 0.0, 1.0),
            AgcMaxGainDb = (float)Math.Clamp(GetNum(r, "agcMaxGain", 100) * 0.12, 0, 24),
            AgcOutputThreshDb = V4aLimiterToDb(GetNum(r, "agcOutputThreshold", 100)),

            CureEnabled = GetBool(r, "cureEnabled"),
            CureLevel = (uint)Math.Clamp((int)GetNum(r, "cureStrength", 0), 0, 2)
        };

        var eqArr = ParseSemicolonFloats(GetStr(r, "eqBands"));
        if (eqArr.Length > 0)
        {
            p.EqMode = eqArr.Length > 12 ? 1u : 0u;
            p.EqBandsDb = ResampleEqBands(eqArr, 10);
            p.EqBands18Db = ResampleEqBands(eqArr, 18);
        }

        string? irs = GetStr(r, "convolverKernel");
        string? ddc = GetStr(r, "ddcDevice");
        return new PresetModel
        {
            Name = name,
            Subtitle = "Imported V4A JSON v1 preset",
            Params = p,
            ActiveIrs = string.IsNullOrWhiteSpace(irs) ? null : Path.GetFileName(irs),
            ActiveDdc = string.IsNullOrWhiteSpace(ddc) ? null : Path.GetFileName(ddc)
        };
    }

    private static PresetModel ParseV4aJsonV2(JsonElement r, string name)
    {
        var p = new DspParamsModel { MasterEnabled = GetBool(r, "masterEnable", true), LimiterEnabled = true };
        string? irs = null, ddc = null;

        if (r.TryGetProperty("masterLimiter", out var ml) && ml.ValueKind == JsonValueKind.Object)
        {
            p.MasterGainDb = V4aVolumeToDb(GetNum(ml, "outputVolume", 100));
            p.OutputPan = (float)Math.Clamp(GetNum(ml, "channelPan", 0) / 100.0, -1.0, 1.0);
            p.LimiterThresholdDb = V4aLimiterToDb(GetNum(ml, "threshold", 100));
        }
        if (r.TryGetProperty("equalizer", out var eq) && eq.ValueKind == JsonValueKind.Object)
        {
            p.EqEnabled = GetBool(eq, "enable");
            if (eq.TryGetProperty("bands", out var bEl) && bEl.ValueKind == JsonValueKind.Array)
            {
                var arr = bEl.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => (float)x.GetDouble()).ToArray();
                if (arr.Length > 0)
                {
                    p.EqMode = arr.Length > 12 ? 1u : 0u;
                    p.EqBandsDb = ResampleEqBands(arr, 10);
                    p.EqBands18Db = ResampleEqBands(arr, 18);
                }
            }
        }
        if (r.TryGetProperty("ddc", out var dEl) && dEl.ValueKind == JsonValueKind.Object)
        {
            p.DdcEnabled = GetBool(dEl, "enable");
            ddc = GetStr(dEl, "device");
        }
        if (r.TryGetProperty("convolver", out var cEl) && cEl.ValueKind == JsonValueKind.Object)
        {
            p.ConvolverEnabled = GetBool(cEl, "enable");
            p.ConvolverCrossChannel = (float)Math.Clamp(GetNum(cEl, "crossChannel", 0) / 100.0, 0.0, 1.0);
            irs = GetStr(cEl, "kernelFile");
        }
        if (r.TryGetProperty("bass", out var b) && b.ValueKind == JsonValueKind.Object)
        {
            p.BassEnabled = GetBool(b, "enable");
            p.BassMode = (uint)Math.Clamp((int)GetNum(b, "mode", 0), 0, 2);
            p.BassFrequencyHz = (float)Math.Clamp(GetNum(b, "frequency", 60), 30, 120);
            p.BassGainDb = V4aBassGainToDb(GetNum(b, "gain", 100));
        }
        if (r.TryGetProperty("clarity", out var cl) && cl.ValueKind == JsonValueKind.Object)
        {
            p.ClarityEnabled = GetBool(cl, "enable");
            p.ClarityMode = (uint)Math.Clamp((int)GetNum(cl, "mode", 0), 0, 2);
            p.ClarityGainDb = V4aClarityGainToDb(GetNum(cl, "gain", 100));
        }
        if (r.TryGetProperty("spectrumExtension", out var vse) && vse.ValueKind == JsonValueKind.Object)
        {
            p.VseEnabled = GetBool(vse, "enable");
            double s = GetNum(vse, "strength", 5000);
            p.VseStrength = (float)Math.Clamp(s / (s > 100 ? 10000.0 : 100.0), 0.0, 1.0);
        }
        if (r.TryGetProperty("tubeSimulator", out var tb) && tb.ValueKind == JsonValueKind.Object)
        {
            p.TubeEnabled = GetBool(tb, "enable");
            p.TubeDrive = 0.35f;
        }
        if (r.TryGetProperty("analogX", out var ax) && ax.ValueKind == JsonValueKind.Object)
        {
            p.AnalogXEnabled = GetBool(ax, "enable");
            p.AnalogXLevel = (uint)Math.Clamp((int)GetNum(ax, "mode", 0), 0, 2);
        }
        if (r.TryGetProperty("dynamicSystem", out var ds) && ds.ValueKind == JsonValueKind.Object)
        {
            p.DynamicEnabled = GetBool(ds, "enable");
            p.DynamicDevice = (uint)Math.Clamp((int)GetNum(ds, "device", 0), 0, 3);
            p.DynamicBass = (float)Math.Clamp(GetNum(ds, "strength", 25), 0, 100);
        }
        if (r.TryGetProperty("fieldSurround", out var fs) && fs.ValueKind == JsonValueKind.Object)
        {
            p.FieldSurroundEnabled = GetBool(fs, "enable");
            p.FieldSurroundStrength = (float)Math.Clamp(GetNum(fs, "widening", 3) / 8.0, 0.0, 1.0);
            p.FieldSurroundMidImage = (float)Math.Clamp(GetNum(fs, "midImage", 5) / 10.0, 0.0, 1.0);
        }
        if (r.TryGetProperty("diffSurround", out var df) && df.ValueKind == JsonValueKind.Object)
        {
            p.SurroundEnabled = GetBool(df, "enable");
            p.SurroundDelayMs = (float)Math.Clamp(GetNum(df, "delay", 6), 0, 20);
        }
        if (r.TryGetProperty("headphoneSurround", out var vhs) && vhs.ValueKind == JsonValueKind.Object)
        {
            p.VhsEnabled = GetBool(vhs, "enable");
            p.VhsLevel = (uint)Math.Clamp((int)GetNum(vhs, "quality", 1) + 1, 1, 5);
        }
        if (r.TryGetProperty("reverb", out var rv) && rv.ValueKind == JsonValueKind.Object)
        {
            p.ReverbEnabled = GetBool(rv, "enable");
            p.ReverbRoomSize = (float)Math.Clamp(GetNum(rv, "roomSize", 35) / 100.0, 0.0, 1.0);
            p.ReverbWidth = (float)Math.Clamp(GetNum(rv, "width", 50) / 100.0, 0.0, 1.0);
            p.ReverbDamping = (float)Math.Clamp(GetNum(rv, "damp", 25) / 100.0, 0.0, 1.0);
            p.ReverbWet = (float)Math.Clamp(GetNum(rv, "wet", 20) / 100.0, 0.0, 1.0);
            p.ReverbDry = (float)Math.Clamp(GetNum(rv, "dry", 85) / 100.0, 0.0, 1.0);
        }
        if (r.TryGetProperty("fetCompressor", out var fc) && fc.ValueKind == JsonValueKind.Object)
        {
            p.CompEnabled = GetBool(fc, "enable");
            p.CompThresholdDb = (float)Math.Clamp(GetNum(fc, "threshold", -18), -40, 0);
            p.CompRatio = (float)Math.Clamp(Math.Max(1.0, GetNum(fc, "ratio", 30) / 10.0), 1, 20);
            p.CompAttackMs = (float)Math.Clamp(GetNum(fc, "attack", 10), 1, 100);
            p.CompReleaseMs = (float)Math.Clamp(GetNum(fc, "release", 120), 20, 500);
            p.CompMakeupDb = (float)Math.Clamp(GetNum(fc, "gain", 0), 0, 18);
        }
        if (r.TryGetProperty("playbackGainControl", out var agc) && agc.ValueKind == JsonValueKind.Object)
        {
            p.AgcEnabled = GetBool(agc, "enable");
            p.AgcStrength = (float)Math.Clamp(GetNum(agc, "strength", 50) / 100.0, 0.0, 1.0);
            p.AgcMaxGainDb = (float)Math.Clamp(GetNum(agc, "maxGain", 100) * 0.12, 0, 24);
            p.AgcOutputThreshDb = V4aLimiterToDb(GetNum(agc, "outputThreshold", 100));
        }
        if (r.TryGetProperty("cure", out var cu) && cu.ValueKind == JsonValueKind.Object)
        {
            p.CureEnabled = GetBool(cu, "enable");
            p.CureLevel = (uint)Math.Clamp((int)GetNum(cu, "crossfeedPreset", 0), 0, 2);
        }

        return new PresetModel
        {
            Name = name,
            Subtitle = "Imported V4A JSON v2 preset",
            Params = p,
            ActiveIrs = string.IsNullOrWhiteSpace(irs) ? null : Path.GetFileName(irs),
            ActiveDdc = string.IsNullOrWhiteSpace(ddc) ? null : Path.GetFileName(ddc)
        };
    }

    private static PresetModel? ParseV4aXmlPreset(string xmlContent, string name)
    {
        var doc = XDocument.Parse(xmlContent);
        var map = doc.Root;
        if (map == null || map.Name.LocalName != "map") return null;

        var bools = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var ints = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var strs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var el in map.Elements())
        {
            string? k = el.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(k)) continue;
            string tag = el.Name.LocalName;
            if (tag == "boolean" && bool.TryParse(el.Attribute("value")?.Value, out bool bv)) bools[k] = bv;
            else if ((tag == "int" || tag == "long" || tag == "float") && double.TryParse(el.Attribute("value")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double nv)) ints[k] = nv;
            else if (tag == "string") strs[k] = el.Value.Trim();
        }

        bool B(string numKey, string namedKey, bool def = false) =>
            bools.TryGetValue(numKey, out bool v1) ? v1 : (bools.TryGetValue(namedKey, out bool v2) ? v2 : def);
        double N(string numKey, string namedKey, double def = 0)
        {
            if (ints.TryGetValue(numKey, out double v1)) return v1;
            if (strs.TryGetValue(numKey, out string? s1) && double.TryParse(s1, NumberStyles.Float, CultureInfo.InvariantCulture, out double sv1)) return sv1;
            if (ints.TryGetValue(namedKey, out double v2)) return v2;
            if (strs.TryGetValue(namedKey, out string? s2) && double.TryParse(s2, NumberStyles.Float, CultureInfo.InvariantCulture, out double sv2)) return sv2;
            return def;
        }
        string? S(string numKey, string namedKey) =>
            strs.TryGetValue(numKey, out string? v1) ? v1 : (strs.TryGetValue(namedKey, out string? v2) ? v2 : null);

        var p = new DspParamsModel
        {
            MasterEnabled = B("36868", "viper4android.headphonefx.enable", true),
            LimiterEnabled = true,
            EqEnabled = B("65551", "viper4android.headphonefx.fireq.enable"),
            DdcEnabled = B("65546", "viper4android.headphonefx.viperddc.enable"),
            VseEnabled = B("65548", "viper4android.headphonefx.vse.enable"),
            VseStrength = (float)Math.Clamp(N("65549;65550", "viper4android.headphonefx.vse.value", 10) / 100.0, 0.0, 1.0),
            ConvolverEnabled = B("65538", "viper4android.headphonefx.convolver.enable"),
            ConvolverCrossChannel = (float)Math.Clamp(N("65543", "viper4android.headphonefx.convolver.crosschannel", 0) / 100.0, 0.0, 1.0),
            FieldSurroundEnabled = B("65553", "viper4android.headphonefx.colorfulmusic.enable"),
            FieldSurroundStrength = (float)Math.Clamp(N("65554;65556", "viper4android.headphonefx.colorfulmusic.coeffs", 3) / 8.0, 0.0, 1.0),
            FieldSurroundMidImage = (float)Math.Clamp(N("65555", "viper4android.headphonefx.colorfulmusic.midimage", 5) / 10.0, 0.0, 1.0),
            SurroundEnabled = B("65557", "viper4android.headphonefx.diffsurr.enable"),
            SurroundDelayMs = (float)Math.Clamp(N("65558", "viper4android.headphonefx.diffsurr.delay", 4) * 2.0, 0.0, 20.0),
            VhsEnabled = B("65544", "viper4android.headphonefx.vhs.enable"),
            VhsLevel = (uint)Math.Clamp((int)N("65545", "viper4android.headphonefx.vhs.qual", 1) + 1, 1, 5),
            ReverbEnabled = B("65559", "viper4android.headphonefx.reverb.enable"),
            ReverbRoomSize = (float)Math.Clamp(N("65560", "viper4android.headphonefx.reverb.roomsize", 35) / 100.0, 0.0, 1.0),
            ReverbWidth = (float)Math.Clamp(N("65561", "viper4android.headphonefx.reverb.roomwidth", 50) / 100.0, 0.0, 1.0),
            ReverbDamping = (float)Math.Clamp(N("65562", "viper4android.headphonefx.reverb.damp", 25) / 100.0, 0.0, 1.0),
            ReverbWet = (float)Math.Clamp(N("65563", "viper4android.headphonefx.reverb.wet", 20) / 100.0, 0.0, 1.0),
            ReverbDry = (float)Math.Clamp(N("65564", "viper4android.headphonefx.reverb.dry", 85) / 100.0, 0.0, 1.0),
            DynamicEnabled = B("65569", "viper4android.headphonefx.dynamicsystem.enable"),
            DynamicBass = (float)Math.Clamp(N("65573", "viper4android.headphonefx.dynamicsystem.bass", 25), 0, 100),
            TubeEnabled = B("65583", "viper4android.headphonefx.tube.enable"),
            TubeDrive = 0.35f,
            BassEnabled = B("65574", "viper4android.headphonefx.fidelity.bass.enable"),
            BassMode = (uint)Math.Clamp((int)N("65575", "viper4android.headphonefx.fidelity.bass.mode", 0), 0, 2),
            BassFrequencyHz = (float)Math.Clamp(N("65576", "viper4android.headphonefx.fidelity.bass.freq", 60), 30, 120),
            BassGainDb = Math.Clamp((float)N("65577", "viper4android.headphonefx.fidelity.bass.gain", 2) * 1.5f + 2.0f, 0f, 15f),
            ClarityEnabled = B("65578", "viper4android.headphonefx.fidelity.clarity.enable"),
            ClarityMode = (uint)Math.Clamp((int)N("65579", "viper4android.headphonefx.fidelity.clarity.mode", 0), 0, 2),
            ClarityGainDb = Math.Clamp((float)N("65580", "viper4android.headphonefx.fidelity.clarity.gain", 2) * 1.5f + 2.0f, 0f, 15f),
            CureEnabled = B("65581", "viper4android.headphonefx.cure.enable"),
            CureLevel = (uint)Math.Clamp((int)N("65582", "viper4android.headphonefx.cure.crossfeed", 0), 0, 2),
            AnalogXEnabled = B("65584", "viper4android.headphonefx.analogx.enable"),
            AnalogXLevel = (uint)Math.Clamp((int)N("65585", "viper4android.headphonefx.analogx.mode", 0), 0, 2),
            AgcEnabled = B("65565", "viper4android.headphonefx.playbackgain.enable"),
            CompEnabled = B("65610", "viper4android.headphonefx.fetcompressor.enable")
        };

        var eqArr = ParseSemicolonFloats(S("65552", "viper4android.headphonefx.fireq.custom"));
        if (eqArr.Length > 0)
        {
            p.EqMode = eqArr.Length > 12 ? 1u : 0u;
            p.EqBandsDb = ResampleEqBands(eqArr, 10);
            p.EqBands18Db = ResampleEqBands(eqArr, 18);
        }

        string? irs = S("65540;65541;65542", "viper4android.headphonefx.convolver.kernel");
        string? ddc = S("65547", "viper4android.headphonefx.viperddc.device");
        return new PresetModel
        {
            Name = name,
            Subtitle = "Imported V4A XML preset",
            Params = p,
            ActiveIrs = string.IsNullOrWhiteSpace(irs) ? null : Path.GetFileName(irs),
            ActiveDdc = string.IsNullOrWhiteSpace(ddc) ? null : Path.GetFileName(ddc)
        };
    }

    public void EnsureDefaultPresetsSeeded(IEnumerable<PresetModel> defaults, IEnumerable<EqPresetModel> eqDefaults)
    {
        try
        {
            Directory.CreateDirectory(PresetsDir);
            if (!Directory.EnumerateFiles(PresetsDir, "*.json").Any())
            {
                foreach (var p in defaults) SaveCustomPreset(p);
            }
            Directory.CreateDirectory(EqPresetsDir);
            if (!Directory.EnumerateFiles(EqPresetsDir, "*.json").Any())
            {
                foreach (var ep in eqDefaults) SaveCustomEqPreset(ep);
            }
        }
        catch { }
    }

    public List<PresetModel> ListCustomPresets()
    {
        Directory.CreateDirectory(PresetsDir);
        var list = new List<PresetModel>();
        var files = Directory.GetFiles(PresetsDir, "*.json")
            .Concat(Directory.GetFiles(PresetsDir, "*.xml"))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            try
            {
                var p = ParsePresetFile(file);
                if (p != null) list.Add(p);
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
        foreach (var file in Directory.GetFiles(EqPresetsDir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
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

    // ==================== IN-APP GITHUB UPDATE CHECKER & INSTALLER ====================

    private static readonly HttpClient UpdateHttp = CreateUpdateHttpClient();

    private static HttpClient CreateUpdateHttpClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"ViPER4Windows-RE/{AppVersion}");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        var res = new UpdateCheckResult { CurrentVersion = AppVersion };
        try
        {
            string url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
            using var resp = await UpdateHttp.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                res.ErrorMessage = $"GitHub API returned HTTP {(int)resp.StatusCode}";
                return res;
            }

            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tag = (root.TryGetProperty("tag_name", out var tEl) ? tEl.GetString() : "") ?? "";
            string cleanVer = tag.Trim().TrimStart('v', 'V');
            res.LatestVersion = string.IsNullOrWhiteSpace(cleanVer) ? AppVersion : cleanVer;
            res.ReleasePageUrl = (root.TryGetProperty("html_url", out var hEl) ? hEl.GetString() : "") ?? $"https://github.com/{GitHubRepo}/releases";
            res.ReleaseNotes = (root.TryGetProperty("body", out var bEl) ? bEl.GetString() : "") ?? "";

            if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assetsEl.EnumerateArray())
                {
                    string aName = (a.TryGetProperty("name", out var nEl) ? nEl.GetString() : "") ?? "";
                    if (aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        res.AssetName = aName;
                        res.DownloadUrl = (a.TryGetProperty("browser_download_url", out var uEl) ? uEl.GetString() : "") ?? "";
                        res.AssetSizeBytes = a.TryGetProperty("size", out var sEl) ? sEl.GetInt64() : 0;
                        break;
                    }
                }
            }

            if (Version.TryParse(AppVersion, out var curV) && Version.TryParse(res.LatestVersion, out var latV))
                res.HasUpdate = latV > curV;
            else
                res.HasUpdate = !string.Equals(AppVersion, res.LatestVersion, StringComparison.OrdinalIgnoreCase);

            res.Success = true;
        }
        catch (Exception ex)
        {
            res.ErrorMessage = ex.Message;
        }
        return res;
    }

    public async Task<(bool Ok, string Message)> DownloadAndLaunchUpdateAsync(string downloadUrl, IProgress<int>? progressPercent = null)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
            return (false, "No installer asset found in this release.");

        try
        {
            string tempInstaller = Path.Combine(Path.GetTempPath(), "ViPER4Windows-RE-Update-Setup.exe");
            using var dlClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            dlClient.DefaultRequestHeaders.UserAgent.ParseAdd($"ViPER4Windows-RE/{AppVersion}");

            using var resp = await dlClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength ?? -1L;
            await using var netStream = await resp.Content.ReadAsStreamAsync();
            await using (var fs = File.Create(tempInstaller))
            {
                var buffer = new byte[81920];
                long readTotal = 0;
                int lastPct = -1;
                int read;
                while ((read = await netStream.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read));
                    readTotal += read;
                    if (total > 0)
                    {
                        int pct = (int)Math.Clamp(readTotal * 100 / total, 0, 100);
                        if (pct != lastPct)
                        {
                            lastPct = pct;
                            progressPercent?.Report(pct);
                        }
                    }
                }
            }

            // Launch downloaded installer in seamless in-place upgrade mode
            Process.Start(new ProcessStartInfo
            {
                FileName = tempInstaller,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /NORESTART",
                Verb = "runas",
                UseShellExecute = true
            });
            return (true, "Installer launched — updating ViPER4Windows RE...");
        }
        catch (Exception ex)
        {
            return (false, $"Update failed: {ex.Message}");
        }
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
