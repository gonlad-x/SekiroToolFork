using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace SekiroTool.Utilities;

public class SettingsManager
{
    private static SettingsManager _default;
    public static SettingsManager Default => _default ??= Load();
    
    public string HotkeyActionIds { get; set; } = "";
    public bool ActivateOnLaunchEnabled { get; set; }
    public string ActivateOnLaunchActionIds { get; set; } = "";
    public bool EnableHotkeys { get; set; }
    public bool NoLogo { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool NoTutorials { get; set; }
    public bool NoCameraSpin { get; set; }
    public bool DisableMenuMusic { get; set; }
    public bool DefaultSoundChangeEnabled { get; set; }
    public int DefaultSoundVolume { get; set; } = 3;
    public bool DisableCutscenes { get; set; }
    public double WindowLeft { get; set; }
    public double WindowTop { get; set; }
    public bool EnableUpdateChecks { get; set; } = true;
    public bool HotkeyReminder { get; set; }

    [DefaultValue(true)] public bool TargetOverlayShowDetails { get; set; }
    [DefaultValue(1.0)] public double TargetOverlayScaleX { get; set; }
    [DefaultValue(0.85)] public double TargetOverlayOpacity { get; set; }
    public double TargetOverlayLeft { get; set; } = double.NaN;
    public double TargetOverlayTop { get; set; } = double.NaN;
    public bool BrowserOverlayEnabled { get; set; }

    public string BrowserOverlayLabelFontFamily { get; set; } = "Segoe UI";
    public double BrowserOverlayLabelFontSize { get; set; } = 14;
    public string BrowserOverlayLabelColor { get; set; } = "#CDD6F4";

    public string BrowserOverlayValueFontFamily { get; set; } = "Segoe UI";
    public double BrowserOverlayValueFontSize { get; set; } = 14;
    public string BrowserOverlayValueColor { get; set; } = "#04A5E5";

    public string BrowserOverlayHitCountFontFamily { get; set; } = "Segoe UI";
    public double BrowserOverlayHitCountFontSize { get; set; } = 16;
    public string BrowserOverlayHitCountColor { get; set; } = "#04A5E5";

    public string BrowserOverlayStaggerThresholdFontFamily { get; set; } = "Segoe UI";
    public double BrowserOverlayStaggerThresholdFontSize { get; set; } = 14;
    public string BrowserOverlayStaggerThresholdColor { get; set; } = "#CDD6F4";

    public string BrowserOverlayBarColor { get; set; } = "#F2CDCD";
    public double BrowserOverlayBackgroundOpacity { get; set; } = 0.0;
    public double BrowserOverlayRowHeight { get; set; } = 24;

    [DefaultValue(true)] public bool BrowserOverlayShowLabels { get; set; }
    [DefaultValue(true)] public bool BrowserOverlayShowHitsRow { get; set; }
    [DefaultValue(true)] public bool BrowserOverlayShowPoiseBar { get; set; }
    [DefaultValue(true)] public bool BrowserOverlayShowHp { get; set; }
    [DefaultValue(true)] public bool BrowserOverlayShowPosture { get; set; }
    [DefaultValue(true)] public bool BrowserOverlayShowPoiseTimer { get; set; }
    [DefaultValue(true)] public bool BrowserOverlayShowActKengeki { get; set; }
    public string BrowserOverlayHitsLabelText { get; set; } = "Hits to stagger:";
    public string BrowserOverlayHpLabelText { get; set; } = "HP:";
    public string BrowserOverlayPostureLabelText { get; set; } = "Posture:";
    public string BrowserOverlayPoiseTimerLabelText { get; set; } = "Poise timer:";
    public string BrowserOverlayActLabelText { get; set; } = "Act:";
    public string BrowserOverlayKengekiLabelText { get; set; } = "Kengeki:";



    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SekiroTool",
        "settings.txt");
    
    
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var lines = new List<string>();

            foreach (var prop in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = prop.GetValue(this);
                var stringValue = value switch
                {
                    double d => d.ToString(CultureInfo.InvariantCulture),
                    float f => f.ToString(CultureInfo.InvariantCulture),
                    _ => value?.ToString() ?? ""
                };
                lines.Add($"{prop.Name}={stringValue}");
            }

            File.WriteAllLines(SettingsPath, lines);
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"Error saving settings: {ex.Message}");
        }
    }


    private static SettingsManager Load()
    {
        var settings = new SettingsManager();

        foreach (var prop in typeof(SettingsManager).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var defaultAttr = prop.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultAttr != null)
                prop.SetValue(settings, defaultAttr.Value);
        }

        if (!File.Exists(SettingsPath))
            return settings;

        try
        {
            var props = new Dictionary<string, PropertyInfo>();
            foreach (var prop in typeof(SettingsManager).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                props[prop.Name] = prop;

            foreach (var line in File.ReadAllLines(SettingsPath))
            {
                var parts = line.Split(['='], 2);
                if (parts.Length != 2) continue;

                var key = parts[0];
                var value = parts[1];

                if (!props.TryGetValue(key, out var prop)) continue;

                object parsed = (prop.PropertyType switch
                {
                    { } t when t == typeof(double) =>
                        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0.0,
                    { } t when t == typeof(float) =>
                        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f,
                    { } t when t == typeof(bool) =>
                        bool.TryParse(value, out var b) && b,
                    { } t when t == typeof(string) => value,
                    _ => null
                })!;

                prop.SetValue(settings, parsed);
            }
        }
        catch
        {
            // Return default settings on error
        }

        return settings;
    }
}