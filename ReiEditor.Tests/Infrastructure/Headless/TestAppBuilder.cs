using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(ReiEditor.Tests.Infrastructure.Headless.TestAppBuilder))]

namespace ReiEditor.Tests.Infrastructure.Headless;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
