using BentoDesk.Models;

namespace BentoDesk.Services;

public sealed class WidgetChromeModeResolver
{
    public WidgetChromeMode Resolve(WidgetConfig config, WidgetContentDescriptor descriptor)
    {
        // 标题样式不再提供配置；按盒子类型固定默认值
        _ = config;
        return descriptor.DefaultChromeMode;
    }
}
