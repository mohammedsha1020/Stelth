using Stealth.Shared;

namespace Stealth.Mobile;

public partial class SettingsPage : ContentPage
{
    private TrustedCodeManager? _trustedCodeManager;

    public SettingsPage()
    {
        InitializeComponent();
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        try
        {
            _trustedCodeManager = new TrustedCodeManager();
            var deviceId = _trustedCodeManager.GetDeviceId();
            DeviceIdLabel.Text = $"Device ID: {deviceId}";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to initialize settings: {ex.Message}", "OK");
        }
    }

    private async void OnGenerateCodeClicked(object sender, EventArgs e)
    {
        try
        {
            var newCode = _trustedCodeManager?.GenerateNewTrustedCode();
            await DisplayAlert("New Trusted Code", $"Generated: {newCode}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to generate code: {ex.Message}", "OK");
        }
    }
}
