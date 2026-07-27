namespace BentoDesk.Controls;

/// <summary>
/// How a widget presents itself while collapsed.
/// </summary>
public enum WidgetCollapseChromeMode
{
    /// <summary>
    /// Keep the original title bar and hide only the content area (file widgets).
    /// </summary>
    TitleBarOnly,

    /// <summary>
    /// Replace the title bar with the dedicated capsule chrome (music / overlay widgets).
    /// </summary>
    CapsulePresentation
}
