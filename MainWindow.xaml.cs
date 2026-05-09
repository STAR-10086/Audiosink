using System.Runtime.Versioning;
using System.Windows;

namespace Audiosink;

/// <summary>
/// 主窗口（隐藏状态，仅作为应用程序容器）
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}