﻿using System.Diagnostics;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using RyTuneX.Helpers;

namespace RyTuneX.Views;

public sealed partial class OptimizeSystemPage : Page
{
    private const string RegistryBaseKey = @"SOFTWARE\RyTuneX\Optimizations";

    public OptimizeSystemPage()
    {
        InitializeComponent();
        LogHelper.Log("Initializing OptimizeSystemPage");
        this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        Loaded += (sender, e) => InitializeToggleSwitchesAsync();
    }

    private async void InitializeToggleSwitchesAsync()
    {
        await LogHelper.Log("Initializing Toggle Switches");
        try
        {
            foreach (var toggleSwitch in FindVisualChildren<ToggleSwitch>(this))
            {
                if (toggleSwitch.Tag is string tagName)
                {
                    // Retrieve the state from the 64-bit registry with 32-bit app
                    using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                        Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                            ? RegistryView.Registry64
                            : RegistryView.Default).CreateSubKey(RegistryBaseKey);
                    if (key != null && key.GetValue(tagName) is int state)
                    {
                        toggleSwitch.IsOn = state == 1;
                    }

                    // Subscribe to the Toggled event
                    toggleSwitch.Toggled += ToggleSwitch_Toggled;
                }
            }
        }
        catch (Exception ex)
        {
            await LogHelper.LogError($"Error initializing toggle switches: {ex.Message}\nStack Trace: {ex.StackTrace}");
        }
    }
    // Helper method to find all children of a specific type in the visual tree
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj != null)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }
                if (child is SettingsCard settingsCard)
                {
                    foreach (var childOfSettingsCard in FindVisualChildren<T>(settingsCard))
                    {
                        yield return childOfSettingsCard;
                    }
                }
                else
                {
                    foreach (var childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
    private async void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            var toggleSwitch = (ToggleSwitch)sender;
            Debug.WriteLine($"ToggleSwitch Tag: {toggleSwitch.Tag}, IsOn: {toggleSwitch.IsOn}");

            // Save the state to the 64-bit registry with 32-bit app
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                    ? RegistryView.Registry64
                    : RegistryView.Default).CreateSubKey(RegistryBaseKey);
            key?.SetValue((string)toggleSwitch.Tag, toggleSwitch.IsOn ? 1 : 0, RegistryValueKind.DWord);

            await OptimizationOptions.XamlSwitchesAsync(toggleSwitch);
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex.Message);
        }
    }
    private async Task<string> StartTask(string command)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                            ? Path.Combine(Environment.GetEnvironmentVariable("windir"), @"SysNative\cmd.exe")
                            : Path.Combine(Environment.GetEnvironmentVariable("windir"), @"System32\cmd.exe"),
                Arguments = $"/C \"{command}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        // Read output asynchronously
        var output = await process.StandardOutput.ReadToEndAsync();

        // Wait for the process to exit asynchronously
        await process.WaitForExitAsync();

        return output;
    }

    private async void CompressOSButton_Click(object sender, RoutedEventArgs e)
    {
        // Get the current compression status
        var status = await StartTask("compact.exe /compactos:query");

        // Create a dialog to show the compression status and options
        var compressDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
            PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
            SecondaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
            Title = "SystemCompressionTitle".GetLocalized(),
            Content = status,
            PrimaryButtonText = "Compress".GetLocalized(),
            SecondaryButtonText = "Decompress".GetLocalized(),
            CloseButtonText = "Cancel".GetLocalized()
        };

        // Handle the Compress button click by setting UI elements and running the command
        compressDialog.PrimaryButtonClick += async (sender, args) =>
        {
            CompressOSButton.Visibility = Visibility.Collapsed;
            CompressOSProgressRing.Visibility = Visibility.Visible;
            CompressOSProgressText.Text = "Compressing".GetLocalized();
            var result = await StartTask("compact.exe /compactos:always");
            App.ShowNotification("SystemCompressionTitle".GetLocalized(), result, InfoBarSeverity.Success, 5000);
            CompressOSButton.Visibility = Visibility.Visible;
            CompressOSProgressRing.Visibility = Visibility.Collapsed;
            CompressOSProgressText.Text = string.Empty;
        };

        // Handle the Decompress button click by setting UI elements and running the command
        compressDialog.SecondaryButtonClick += async (sender, args) =>
        {
            CompressOSButton.Visibility = Visibility.Collapsed;
            CompressOSProgressRing.Visibility = Visibility.Visible;
            CompressOSProgressText.Text = "Decompressing".GetLocalized();
            var result = await StartTask("compact.exe /compactos:never");
            App.ShowNotification("SystemCompressionTitle".GetLocalized(), result, InfoBarSeverity.Success, 5000);
            CompressOSButton.Visibility = Visibility.Visible;
            CompressOSProgressRing.Visibility = Visibility.Collapsed;
            CompressOSProgressText.Text = string.Empty;
        };
        await compressDialog.ShowAsync();
    }
}