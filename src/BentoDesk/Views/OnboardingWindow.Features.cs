using CommunityToolkit.WinUI.Animations;
using BentoDesk.Helpers;
using BentoDesk.Models;
using BentoDesk.Services;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using WinRT.Interop;

namespace BentoDesk.Views;

public sealed partial class OnboardingWindow
{
    private void SetupStep2Features()
    {
        Step2MusicToggle.Toggled -= Step2Toggle_Toggled;

        Step2MusicToggle.IsOn = FeatureWidgetSettings.IsEnabled(_settingsService.Settings, WidgetKind.Music);

        Step2MusicToggle.Toggled += Step2Toggle_Toggled;

        UpdateFeatureCardHighlight(Step2MusicCard, Step2MusicToggle.IsOn);
    }

    private void Step2Toggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle)
        {
            return;
        }

        Border card;
        if (toggle == Step2MusicToggle)
        {
            FeatureWidgetSettings.SetEnabled(_settingsService.Settings, WidgetKind.Music, toggle.IsOn);
            card = Step2MusicCard;
        }
        else
        {
            return;
        }

        _settingsService.SaveDebounced();
        UpdateFeatureCardHighlight(card, toggle.IsOn);

        // Play a subtle scale bounce on the card
        if (toggle.IsOn)
        {
            PlayCardBounce(card);
        }
    }

    private void PlayCardBounce(Border card)
    {
        try
        {
            var transform = GetElementTransform(card);
            var storyboard = new Storyboard();
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            var scaleXUp = new DoubleAnimation
            {
                From = 1,
                To = 1.03,
                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                EasingFunction = easing
            };
            Storyboard.SetTarget(scaleXUp, transform);
            Storyboard.SetTargetProperty(scaleXUp, "ScaleX");
            storyboard.Children.Add(scaleXUp);

            var scaleYUp = new DoubleAnimation
            {
                From = 1,
                To = 1.03,
                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                EasingFunction = easing
            };
            Storyboard.SetTarget(scaleYUp, transform);
            Storyboard.SetTargetProperty(scaleYUp, "ScaleY");
            storyboard.Children.Add(scaleYUp);

            var scaleXDown = new DoubleAnimation
            {
                From = 1.03,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(280)),
                BeginTime = TimeSpan.FromMilliseconds(150),
                EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleXDown, transform);
            Storyboard.SetTargetProperty(scaleXDown, "ScaleX");
            storyboard.Children.Add(scaleXDown);

            var scaleYDown = new DoubleAnimation
            {
                From = 1.03,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(280)),
                BeginTime = TimeSpan.FromMilliseconds(150),
                EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleYDown, transform);
            Storyboard.SetTargetProperty(scaleYDown, "ScaleY");
            storyboard.Children.Add(scaleYDown);

            storyboard.Begin();
        }
        catch { }
    }

    private void UpdateFeatureCardHighlight(Border card, bool isOn)
    {
        if (isOn)
        {
            card.BorderBrush = AccentBrush();
            card.BorderThickness = new Thickness(1.5);
        }
        else
        {
            card.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
            card.BorderThickness = new Thickness(1);
        }
    }
}
