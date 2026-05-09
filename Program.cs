using System.Runtime.Versioning;

namespace Audiosink;

/// <summary>
/// 应用程序入口点
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}