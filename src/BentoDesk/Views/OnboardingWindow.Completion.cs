using BentoDesk.Services;
using Microsoft.UI.Xaml;

namespace BentoDesk.Views;

public sealed partial class OnboardingWindow
{
    private void SetupStep5()
    {
        // File boxes are always available; music is settings-gated and not part of onboarding.
        Step5WidgetsSummary.Text = _localizationService.T("Onboarding.Step5.SummaryFileWidgets");

        // Appearance summary
        string themeLabel = _settingsService.Settings.Theme switch
        {
            "Light" => _localizationService.T("Onboarding.Step3.ThemeLight"),
            "Dark" => _localizationService.T("Onboarding.Step3.ThemeDark"),
            _ => _localizationService.T("Onboarding.Step3.ThemeSystem")
        };
        string materialLabel = _settingsService.Settings.WidgetMaterialType switch
        {
            "Acrylic" => _localizationService.T("Onboarding.Step3.MaterialAcrylic"),
            "Solid" => _localizationService.T("Onboarding.Step3.MaterialSolid"),
            _ => _localizationService.T("Onboarding.Step3.MaterialMica")
        };
        Step5AppearanceSummary.Text = $"{themeLabel} · {materialLabel}";

        // Daily use summary
        string hotkeySummary = _settingsService.Settings.GlobalHotkeyEnabled
            ? _localizationService.T("Onboarding.Step5.SummaryHotkeyOn")
            : _localizationService.T("Onboarding.Step5.SummaryHotkeyOff");
        string startupSummary = StartupService.IsEnabled()
            ? _localizationService.T("Onboarding.Step5.SummaryStartupOn")
            : _localizationService.T("Onboarding.Step5.SummaryStartupOff");
        Step5DailySummary.Text = $"{hotkeySummary} · {startupSummary}";
    }

    private void OnLanguageChanged()
    {
        Title = _localizationService.T("Onboarding.WindowTitle");
        Localized.RefreshAll(_localizationService);
        PrepareIntroContent();
        SetupStep(animate: false);
        UpdateFooterState();
    }
}
