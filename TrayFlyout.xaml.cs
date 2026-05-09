using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using H.NotifyIcon;

namespace Audiosink;

/// <summary>
/// Win11 风格的托盘弹出窗口
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class TrayFlyout : Window
{
    // Win32 API 声明
    private const int MONITOR_DEFAULTTONEAREST = 2;
    private const int ABM_GETTASKBARPOS = 0x00000005;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    // 任务栏位置枚举
    private enum TaskbarPosition
    {
        Left,
        Top,
        Right,
        Bottom
    }

    private readonly TaskbarIcon? _trayIcon;
    private readonly Storyboard? _fadeInStoryboard;
    private readonly Storyboard? _fadeOutStoryboard;
    private bool _isClosing;
    private bool _isConnected;

    /// <summary>
    /// 当用户请求连接/断开时触发
    /// </summary>
    public event Action<bool>? ConnectionRequested;

    /// <summary>
    /// 当用户点击设置时触发
    /// </summary>
    public event Action? SettingsRequested;

    public TrayFlyout(TaskbarIcon? trayIcon = null)
    {
        InitializeComponent();

        _trayIcon = trayIcon;

        // 初始化动画
        _fadeInStoryboard = CreateFadeInAnimation();
        _fadeOutStoryboard = CreateFadeOutAnimation();

        // 设置窗口属性
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowInTaskbar = false;

        // 监听鼠标点击外部区域
        MouseDown += (s, e) =>
        {
            e.Handled = true;
        };
    }

    /// <summary>
    /// 更新连接状态
    /// </summary>
    public void UpdateConnectionState(bool isConnected, string? deviceName = null)
    {
        _isConnected = isConnected;

        Dispatcher.Invoke(() =>
        {
            if (isConnected)
            {
                // 已连接状态
                DeviceNameText.Text = deviceName ?? "已连接设备";
                DeviceIcon.Text = ""; // 手机图标
                StatusIndicator.Background = FindResource("StatusConnectedBrush") as System.Windows.Media.SolidColorBrush;
                StatusText.Text = "已连接";
                StatusText.Foreground = FindResource("StatusConnectedBrush") as System.Windows.Media.SolidColorBrush;
                ConnectionToggleButton.IsChecked = true;
                ConnectionToggleButton.Content = "断开连接";
            }
            else
            {
                // 未连接状态
                DeviceNameText.Text = "未连接设备";
                DeviceIcon.Text = ""; // 手机图标
                StatusIndicator.Background = FindResource("StatusDisconnectedBrush") as System.Windows.Media.SolidColorBrush;
                StatusText.Text = "未连接";
                StatusText.Foreground = FindResource("StatusDisconnectedBrush") as System.Windows.Media.SolidColorBrush;
                ConnectionToggleButton.IsChecked = false;
                ConnectionToggleButton.Content = "连接设备";
            }
        });
    }

    /// <summary>
    /// 显示弹出窗口并定位到正确位置
    /// </summary>
    public void ShowAtTrayIcon()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        // 获取任务栏位置
        var taskbarPos = GetTaskbarPosition();
        var trayIconRect = GetTrayIconRect();

        // 计算窗口位置
        var windowPos = CalculateWindowPosition(taskbarPos, trayIconRect);

        // 设置窗口位置
        Left = windowPos.X;
        Top = windowPos.Y;

        // 显示窗口并播放动画
        Show();
        Activate();
        Focus();

        // 播放淡入动画
        if (_fadeInStoryboard != null)
        {
            _fadeInStoryboard.Begin(this);
        }
    }

    /// <summary>
    /// 获取任务栏位置
    /// </summary>
    private static TaskbarPosition GetTaskbarPosition()
    {
        var appBarData = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>()
        };

        SHAppBarMessage(ABM_GETTASKBARPOS, ref appBarData);

        return appBarData.uEdge switch
        {
            0 => TaskbarPosition.Left,
            1 => TaskbarPosition.Top,
            2 => TaskbarPosition.Right,
            3 => TaskbarPosition.Bottom,
            _ => TaskbarPosition.Bottom
        };
    }

    /// <summary>
    /// 获取托盘图标区域（简化版，使用鼠标位置）
    /// </summary>
    private static RECT GetTrayIconRect()
    {
        GetCursorPos(out POINT cursorPos);

        // 返回一个以鼠标位置为中心的矩形
        return new RECT
        {
            Left = cursorPos.X - 16,
            Top = cursorPos.Y - 16,
            Right = cursorPos.X + 16,
            Bottom = cursorPos.Y + 16
        };
    }

    /// <summary>
    /// 根据任务栏位置计算窗口显示位置
    /// </summary>
    private Point CalculateWindowPosition(TaskbarPosition taskbarPos, RECT trayIconRect)
    {
        double windowWidth = Width;
        double windowHeight = Height;

        // 获取工作区（排除任务栏）
        var workArea = SystemParameters.WorkArea;

        double x = 0;
        double y = 0;

        switch (taskbarPos)
        {
            case TaskbarPosition.Bottom:
                // 任务栏在底部，窗口显示在托盘图标上方
                x = trayIconRect.Left - (windowWidth / 2) + 16;
                y = trayIconRect.Top - windowHeight - 8;
                break;

            case TaskbarPosition.Top:
                // 任务栏在顶部，窗口显示在托盘图标下方
                x = trayIconRect.Left - (windowWidth / 2) + 16;
                y = trayIconRect.Bottom + 8;
                break;

            case TaskbarPosition.Left:
                // 任务栏在左侧，窗口显示在托盘图标右侧
                x = trayIconRect.Right + 8;
                y = trayIconRect.Top - (windowHeight / 2) + 16;
                break;

            case TaskbarPosition.Right:
                // 任务栏在右侧，窗口显示在托盘图标左侧
                x = trayIconRect.Left - windowWidth - 8;
                y = trayIconRect.Top - (windowHeight / 2) + 16;
                break;
        }

        // 确保窗口在屏幕范围内
        x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - windowWidth));
        y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - windowHeight));

        return new Point(x, y);
    }

    /// <summary>
    /// 创建淡入动画
    /// </summary>
    private static Storyboard CreateFadeInAnimation()
    {
        var storyboard = new Storyboard();

        // 透明度动画
        var opacityAnimation = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(opacityAnimation);

        // 缩放动画（从中心缩放）
        var scaleXAnimation = new DoubleAnimation
        {
            From = 0.95,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.ScaleX"));
        storyboard.Children.Add(scaleXAnimation);

        var scaleYAnimation = new DoubleAnimation
        {
            From = 0.95,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.ScaleY"));
        storyboard.Children.Add(scaleYAnimation);

        return storyboard;
    }

    /// <summary>
    /// 创建淡出动画
    /// </summary>
    private static Storyboard CreateFadeOutAnimation()
    {
        var storyboard = new Storyboard();

        // 透明度动画
        var opacityAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(opacityAnimation);

        return storyboard;
    }

    /// <summary>
    /// 窗口加载完成事件
    /// </summary>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 设置初始变换原点
        var transform = new System.Windows.Media.ScaleTransform(1.0, 1.0);
        RenderTransform = transform;
        RenderTransformOrigin = new Point(0.5, 0.5);
    }

    /// <summary>
    /// 窗口失去焦点时自动隐藏
    /// </summary>
    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (!_isClosing)
        {
            HideWithAnimation();
        }
    }

    /// <summary>
    /// 带动画隐藏窗口
    /// </summary>
    private void HideWithAnimation()
    {
        if (_fadeOutStoryboard != null)
        {
            _fadeOutStoryboard.Completed += (s, e) =>
            {
                Hide();
            };
            _fadeOutStoryboard.Begin(this);
        }
        else
        {
            Hide();
        }
    }

    /// <summary>
    /// 窗口关闭事件
    /// </summary>
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _isClosing = true;
    }

    /// <summary>
    /// 连接/断开按钮点击事件
    /// </summary>
    private void ConnectionToggleButton_Click(object sender, RoutedEventArgs e)
    {
        bool isChecked = ConnectionToggleButton.IsChecked == true;
        ConnectionRequested?.Invoke(isChecked);

        // 更新UI状态
        if (isChecked)
        {
            ConnectionToggleButton.Content = "断开连接";
            StatusText.Text = "连接中...";
            StatusIndicator.Background = FindResource("StatusConnectingBrush") as System.Windows.Media.SolidColorBrush;
        }
        else
        {
            ConnectionToggleButton.Content = "连接设备";
            StatusText.Text = "断开中...";
            StatusIndicator.Background = FindResource("StatusConnectingBrush") as System.Windows.Media.SolidColorBrush;
        }
    }

    /// <summary>
    /// 设置按钮点击事件
    /// </summary>
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke();
        HideWithAnimation();
    }
}