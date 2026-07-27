using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BentoDesk.Views.SettingsSections;

public sealed partial class CollapseModeSettingsSection : UserControl
{
    public CollapseModeSettingsSection()
    {
        InitializeComponent();
    }

    public event EventHandler<SettingsSectionNavigationRequestedEventArgs>? NavigationRequested;

    private void NestedSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string sectionTag })
        {
            NavigationRequested?.Invoke(this, new SettingsSectionNavigationRequestedEventArgs(sectionTag));
        }
    }
}
