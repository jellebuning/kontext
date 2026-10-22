using System.Reflection;
using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

public sealed partial class SettingsView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ShellViewModel),
        typeof(SettingsView),
        new PropertyMetadata(null, (view, _) => ((SettingsView)view).Bindings?.Update()));

    public SettingsView() => InitializeComponent();

    public ShellViewModel? ViewModel
    {
        get => (ShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public string AppVersion { get; } = ReadAppVersion();

    private static string ReadAppVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(SettingsView).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip the source revision suffix ("+<hash>") that the SDK appends.
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            var version = plus >= 0 ? informational[..plus] : informational;
            return $"Version {version}";
        }

        var fallback = assembly.GetName().Version;
        return fallback is null ? "Version unknown" : $"Version {fallback}";
    }
}
