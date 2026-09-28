using System.Text.Json;
using Microsoft.Win32;

namespace DisplayPilot;

public sealed class DeviceRule
{
    public string Name { get; set; } = "Input device";
    public string Identifier { get; set; } = "";
    public string Target { get; set; } = "Ignore";
    public string Kind { get; set; } = "Keyboard / mouse";
}
public sealed class Settings
{
    public string DisplayMagicianPath { get; set; } = @"C:\Program Files\DisplayMagician\DisplayMagicianConsole.exe";
    public List<string> Profiles { get; set; } = ["PC", "TV"];
    public string PcProfile { get; set; } = "PC";
    public string TvProfile { get; set; } = "TV";
    public int CooldownSeconds { get; set; } = 5;
    public bool StartMinimized { get; set; }
    public bool StartWithWindows { get; set; }
    public bool AutomaticSwitching { get; set; } = true;
    public List<DeviceRule> Devices { get; set; } = [];
    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DisplayPilot", "settings.json");
    public static Settings Load(out string? warning)
    {
        warning = null;
        if (!File.Exists(FilePath)) return new();
        try { var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? throw new InvalidDataException("Empty settings"); s.Validate(); return s; }
        catch (Exception e) { warning = "Settings could not be loaded. Defaults are active. " + e.Message; File.Copy(FilePath, FilePath + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); return new(); }
    }
    public void Validate()
    {
        if (Profiles == null || Profiles.Count == 0 || Profiles.Any(string.IsNullOrWhiteSpace) || Profiles.Any(p => p.Equals("Ignore", StringComparison.OrdinalIgnoreCase)) || Profiles.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Profiles.Count || !Profiles.Contains(PcProfile) || !Profiles.Contains(TvProfile)) throw new InvalidDataException("Profiles must be unique and include the PC and TV button profiles.");
        if (string.IsNullOrWhiteSpace(PcProfile) || string.IsNullOrWhiteSpace(TvProfile)) throw new InvalidDataException("Both profile names are required.");
        if (CooldownSeconds < 0 || CooldownSeconds > 120) throw new InvalidDataException("Cooldown must be 0–120 seconds.");
        if (Devices == null || Devices.Any(d => string.IsNullOrWhiteSpace(d.Identifier) || string.IsNullOrWhiteSpace(d.Name) || d.Identifier.Split('|').Any(string.IsNullOrWhiteSpace) || (d.Target != "Ignore" && !Profiles.Contains(d.Target)))) throw new InvalidDataException("Each device needs a name, identifier, and a valid profile or Ignore target.");
    }
    public void Save()
    {
        Validate(); Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
    public DeviceRule? Match(string path) => Devices.FirstOrDefault(d => d.Identifier.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(id => path.Contains(id, StringComparison.OrdinalIgnoreCase)));
}
public sealed class SwitchEngine(Settings settings, Func<string, Task> change)
{
    public string Mode { get; private set; } = settings.PcProfile;
    public bool Busy { get; private set; }
    public DateTime LastAttempt { get; private set; } = DateTime.MinValue;
    public async Task<bool> Request(string target, bool manual = false)
    {
        if (!settings.Profiles.Contains(target) || Busy || string.Equals(target, Mode, StringComparison.OrdinalIgnoreCase) || (!manual && (!settings.AutomaticSwitching || DateTime.UtcNow - LastAttempt < TimeSpan.FromSeconds(settings.CooldownSeconds)))) return false;
        Busy = true; LastAttempt = DateTime.UtcNow;
        try { await change(target); Mode = target; return true; }
        finally { LastAttempt = DateTime.UtcNow; Busy = false; }
    }
}
public static class Startup
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled { get { using var k = Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue("DisplayPilot") != null; } }
    public static void Set(bool enabled) { using var k = Registry.CurrentUser.CreateSubKey(Key); if (enabled) k.SetValue("DisplayPilot", "\"" + Environment.ProcessPath + "\" --minimized"); else k.DeleteValue("DisplayPilot", false); }
}


