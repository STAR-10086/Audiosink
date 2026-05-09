using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace Audiosink;

/// <summary>
/// 应用程序主入口，管理托盘图标和窗口生命周期
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private TrayFlyout? _flyout;
    private BluetoothManager? _bluetoothManager;
    private bool _isConnected;
    private string? _connectedDeviceName;

    /// <summary>
    /// 应用程序启动事件
    /// </summary>
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        // 初始化蓝牙管理器
        _bluetoothManager = new BluetoothManager();
        bool initialized = await _bluetoothManager.InitializeAsync();

        if (!initialized)
        {
            MessageBox.Show("蓝牙初始化失败，请检查蓝牙适配器是否正常工作。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // 订阅蓝牙事件
        _bluetoothManager.OnDeviceConnected += OnDeviceConnected;
        _bluetoothManager.OnDeviceDisconnected += OnDeviceDisconnected;

        // 创建托盘图标
        CreateTrayIcon();

        // 创建弹出窗口
        _flyout = new TrayFlyout(_trayIcon);
        _flyout.ConnectionRequested += OnConnectionRequested;
        _flyout.SettingsRequested += OnSettingsRequested;

        // 扫描已配对设备
        await _bluetoothManager.DiscoverPairedDevicesAsync();
    }

    /// <summary>
    /// 创建系统托盘图标
    /// </summary>
    private void CreateTrayIcon()
    {
        _trayIcon = new TaskbarIcon();

        // 设置图标（从资源加载或生成默认图标）
        try
        {
            // 尝试从资源加载图标
            var iconUri = new Uri("pack://application:,,,/Audiosink;component/Resources/Audiosink.ico", UriKind.Absolute);
            var resourceInfo = System.Windows.Application.GetResourceStream(iconUri);
            if (resourceInfo != null)
            {
                _trayIcon.Icon = new System.Drawing.Icon(resourceInfo.Stream);
            }
            else
            {
                // 如果资源不存在，生成默认图标
                _trayIcon.Icon = IconGenerator.GenerateDefaultIcon();
            }
        }
        catch
        {
            // 如果加载失败，生成默认图标
            _trayIcon.Icon = IconGenerator.GenerateDefaultIcon();
        }

        // 设置工具提示
        _trayIcon.ToolTipText = "Audiosink - 蓝牙音频接收器";

        // 设置点击事件
        _trayIcon.TrayLeftMouseUp += OnTrayIconClick;

        // 创建右键菜单
        var contextMenu = new ContextMenu();

        var showItem = new MenuItem { Header = "显示主窗口" };
        showItem.Click += (s, e) => ShowFlyout();
        contextMenu.Items.Add(showItem);

        contextMenu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (s, e) => ExitApplication();
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenu = contextMenu;

        // 显示托盘图标
        _trayIcon.UpdateIcon();
    }

    /// <summary>
    /// 托盘图标点击事件
    /// </summary>
    private void OnTrayIconClick(object sender, RoutedEventArgs e)
    {
        ShowFlyout();
    }

    /// <summary>
    /// 显示弹出窗口
    /// </summary>
    private void ShowFlyout()
    {
        if (_flyout != null)
        {
            _flyout.ShowAtTrayIcon();
        }
    }

    /// <summary>
    /// 设备连接成功回调
    /// </summary>
    private void OnDeviceConnected(string deviceName)
    {
        _isConnected = true;
        _connectedDeviceName = deviceName;

        Dispatcher.Invoke(() =>
        {
            _flyout?.UpdateConnectionState(true, deviceName);
            _trayIcon?.ShowNotification("设备已连接", $"已连接到 {deviceName}", NotificationIcon.Info);
        });
    }

    /// <summary>
    /// 设备断开连接回调
    /// </summary>
    private void OnDeviceDisconnected(string deviceName)
    {
        _isConnected = false;
        _connectedDeviceName = null;

        Dispatcher.Invoke(() =>
        {
            _flyout?.UpdateConnectionState(false);
            _trayIcon?.ShowNotification("设备已断开", $"{deviceName} 已断开连接", NotificationIcon.Warning);
        });
    }

    /// <summary>
    /// 连接请求处理
    /// </summary>
    private async void OnConnectionRequested(bool connect)
    {
        if (_bluetoothManager == null) return;

        if (connect)
        {
            // 尝试连接第一个可用设备
            var devices = _bluetoothManager.GetPairedDeviceNames();
            if (devices.Count > 0)
            {
                await _bluetoothManager.ConnectAsSinkAsync(0);
            }
            else
            {
                MessageBox.Show("未找到已配对的蓝牙设备，请先在系统设置中配对设备。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            await _bluetoothManager.DisconnectAsync();
        }
    }

    /// <summary>
    /// 设置请求处理
    /// </summary>
    private void OnSettingsRequested()
    {
        // TODO: 实现设置窗口
        MessageBox.Show("设置功能即将推出", "设置", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// 退出应用程序
    /// </summary>
    private void ExitApplication()
    {
        // 清理资源
        _bluetoothManager?.Dispose();
        _trayIcon?.Dispose();

        Shutdown();
    }

    /// <summary>
    /// 应用程序退出事件
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _bluetoothManager?.Dispose();
        _trayIcon?.Dispose();

        base.OnExit(e);
    }
}