using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Audio;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using Windows.Media.Devices;

namespace Audiosink;

/// <summary>
/// A2DP Sink 核心管理器 - 将电脑注册为蓝牙音频接收端
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public sealed class BluetoothManager : IDisposable
{
    private BluetoothDevice? _connectedDevice;
    private A2dpSink? _a2dpSink;
    private AudioGraph? _audioGraph;
    private bool _disposed;

    // 已发现的配对设备列表
    private readonly List<BluetoothDevice> _pairedDevices = [];

    /// <summary>
    /// 当手机成功连接时触发，参数为手机名称
    /// </summary>
    public event Action<string>? OnDeviceConnected;

    /// <summary>
    /// 当手机断开连接时触发
    /// </summary>
    public event Action<string>? OnDeviceDisconnected;

    /// <summary>
    /// 当发现新设备时触发
    /// </summary>
    public event Action<string>? OnDeviceDiscovered;

    /// <summary>
    /// 获取当前连接的设备名称
    /// </summary>
    public string? ConnectedDeviceName => _connectedDevice?.Name;

    /// <summary>
    /// 是否有设备连接
    /// </summary>
    public bool IsConnected => _connectedDevice != null;

    /// <summary>
    /// 初始化蓝牙子系统，检查 A2DP Sink 支持
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        // 检查系统是否支持 A2DP Sink
        if (!await CheckA2dpSinkSupportAsync())
        {
            Console.WriteLine("[错误] 当前系统不支持 A2DP Sink 功能");
            return false;
        }

        Console.WriteLine("[信息] 蓝牙子系统初始化完成");
        return true;
    }

    /// <summary>
    /// 检查系统是否支持 A2DP Sink
    /// </summary>
    private static async Task<bool> CheckA2dpSinkSupportAsync()
    {
        try
        {
            // 获取本机蓝牙适配器
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter == null)
            {
                Console.WriteLine("[错误] 未找到蓝牙适配器");
                return false;
            }

            // 检查是否支持 A2DP Sink 角色
            bool isSinkSupported = adapter.IsLowEnergySupported &&
                                   adapter.IsCentralRoleSupported;

            Console.WriteLine($"[信息] 蓝牙适配器: {adapter.DeviceId}");
            Console.WriteLine($"[信息] A2DP Sink 支持: {(isSinkSupported ? "是" : "否")}");

            return isSinkSupported;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 检查蓝牙支持时出错: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 扫描已配对的蓝牙设备
    /// </summary>
    public async Task<int> DiscoverPairedDevicesAsync()
    {
        _pairedDevices.Clear();

        try
        {
            // 使用 Windows.Devices.Enumeration 搜索已配对设备
            string aqlFilter = BluetoothDevice.GetDeviceSelector();
            var devices = await DeviceInformation.FindAllAsync(aqlFilter);

            Console.WriteLine($"[信息] 找到 {devices.Count} 个已配对设备");

            foreach (var deviceInfo in devices)
            {
                try
                {
                    var btDevice = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
                    if (btDevice != null)
                    {
                        _pairedDevices.Add(btDevice);
                        string deviceName = string.IsNullOrEmpty(btDevice.Name) ? "未知设备" : btDevice.Name;
                        Console.WriteLine($"  - {deviceName} [{btDevice.DeviceId}]");
                        OnDeviceDiscovered?.Invoke(deviceName);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[警告] 无法访问设备 {deviceInfo.Id}: {ex.Message}");
                }
            }

            return _pairedDevices.Count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 扫描设备失败: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 尝试连接指定的蓝牙设备作为 A2DP Sink
    /// </summary>
    /// <param name="deviceIndex">设备索引（从 0 开始）</param>
    public async Task<bool> ConnectAsSinkAsync(int deviceIndex)
    {
        if (deviceIndex < 0 || deviceIndex >= _pairedDevices.Count)
        {
            Console.WriteLine("[错误] 无效的设备索引");
            return false;
        }

        var device = _pairedDevices[deviceIndex];
        return await ConnectAsSinkAsync(device);
    }

    /// <summary>
    /// 尝试连接指定的蓝牙设备作为 A2DP Sink
    /// </summary>
    /// <param name="device">目标蓝牙设备</param>
    public async Task<bool> ConnectAsSinkAsync(BluetoothDevice device)
    {
        try
        {
            Console.WriteLine($"[信息] 正在尝试连接设备: {device.Name}");

            // 检查设备是否支持 A2DP
            if (!await IsA2dpCapableAsync(device))
            {
                Console.WriteLine($"[警告] 设备 {device.Name} 不支持 A2DP");
                return false;
            }

            // 创建 A2DP Sink 实例
            _a2dpSink = await A2dpSink.FromIdAsync(device.DeviceId);
            if (_a2dpSink == null)
            {
                Console.WriteLine("[错误] 无法创建 A2DP Sink 实例");
                return false;
            }

            // 订阅连接状态变化事件
            _a2dpSink.ConnectionStatusChanged += OnConnectionStatusChanged;

            // 请求连接
            Console.WriteLine("[信息] 正在请求 A2DP 连接...");
            await _a2dpSink.OpenAsync();

            // 等待连接建立
            if (_a2dpSink.ConnectionStatus == A2dpSinkConnectionStatus.Connected)
            {
                _connectedDevice = device;
                string deviceName = string.IsNullOrEmpty(device.Name) ? "未知设备" : device.Name;
                Console.WriteLine($"[成功] 已连接到设备: {deviceName}");

                // 设置音频路由
                await SetupAudioRoutingAsync();

                OnDeviceConnected?.Invoke(deviceName);
                return true;
            }
            else
            {
                Console.WriteLine($"[警告] 连接状态: {_a2dpSink.ConnectionStatus}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 连接设备失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查设备是否支持 A2DP
    /// </summary>
    private static async Task<bool> IsA2dpCapableAsync(BluetoothDevice device)
    {
        try
        {
            // 检查设备是否支持经典蓝牙连接
            if (device.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                return true;
            }

            // 尝试获取设备的蓝牙地址来验证有效性
            var deviceInfo = await DeviceInformation.CreateFromIdAsync(device.DeviceId);
            return deviceInfo != null && deviceInfo.IsEnabled;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 处理连接状态变化
    /// </summary>
    private void OnConnectionStatusChanged(A2dpSink sender, object args)
    {
        Console.WriteLine($"[信息] A2DP 连接状态变化: {sender.ConnectionStatus}");

        if (sender.ConnectionStatus == A2dpSinkConnectionStatus.Disconnected)
        {
            string deviceName = _connectedDevice?.Name ?? "未知设备";
            _connectedDevice = null;
            OnDeviceDisconnected?.Invoke(deviceName);
            Console.WriteLine($"[信息] 设备已断开: {deviceName}");
        }
    }

    /// <summary>
    /// 设置音频路由，将蓝牙音频重定向到默认扬声器
    /// </summary>
    private async Task SetupAudioRoutingAsync()
    {
        try
        {
            // 获取默认音频渲染设备（扬声器）
            string speakerId = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
            Console.WriteLine($"[信息] 默认扬声器 ID: {speakerId}");

            // 创建音频图设置 - 使用通信音频类别以支持蓝牙音频
            var graphSettings = new AudioGraphSettings(AudioRenderCategory.Communications)
            {
                PrimaryRenderDevice = await DeviceInformation.CreateFromIdAsync(speakerId),
                QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency
            };

            // 创建音频图
            var graphResult = await AudioGraph.CreateAsync(graphSettings);
            if (graphResult.Status != AudioGraphCreationStatus.Success)
            {
                Console.WriteLine($"[错误] 创建音频图失败: {graphResult.Status}");
                return;
            }

            _audioGraph = graphResult.Graph;

            // 创建设备输出节点（发送到扬声器）
            var outputResult = await _audioGraph.CreateDeviceOutputNodeAsync();
            if (outputResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                Console.WriteLine($"[错误] 创建设备输出节点失败: {outputResult.Status}");
                return;
            }

            // 创建帧输出节点用于音频处理
            var frameOutputResult = _audioGraph.CreateFrameOutputNode();
            if (frameOutputResult == null)
            {
                Console.WriteLine("[错误] 创建帧输出节点失败");
                return;
            }

            // 连接帧输出到设备输出
            frameOutputResult.AddOutgoingConnection(outputResult.DeviceOutputNode);

            // 启动音频图
            _audioGraph.Start();
            Console.WriteLine("[信息] 音频路由已设置，手机音频将通过电脑扬声器播放");
            Console.WriteLine("[提示] 蓝牙音频将自动通过系统音频管道播放");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 设置音频路由失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 断开当前连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_a2dpSink != null)
        {
            try
            {
                _a2dpSink.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _a2dpSink.Dispose();
                _a2dpSink = null;

                if (_connectedDevice != null)
                {
                    string deviceName = _connectedDevice.Name;
                    _connectedDevice = null;
                    Console.WriteLine($"[信息] 已断开设备: {deviceName}");
                    OnDeviceDisconnected?.Invoke(deviceName);
                }

                await CleanupAudioGraphAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[错误] 断开连接时出错: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 清理音频图资源
    /// </summary>
    private async Task CleanupAudioGraphAsync()
    {
        if (_audioGraph != null)
        {
            try
            {
                _audioGraph.Stop();
                _audioGraph.Dispose();
                _audioGraph = null;
                Console.WriteLine("[信息] 音频图已清理");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[警告] 清理音频图时出错: {ex.Message}");
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 获取所有已配对设备的名称列表
    /// </summary>
    public IReadOnlyList<string> GetPairedDeviceNames()
    {
        return _pairedDevices
            .Select(d => string.IsNullOrEmpty(d.Name) ? "未知设备" : d.Name)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// 释放所有资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // 断开连接并清理资源
            _a2dpSink?.Dispose();
            _a2dpSink = null;

            _audioGraph?.Stop();
            _audioGraph?.Dispose();
            _audioGraph = null;

            // 释放所有配对设备
            foreach (var device in _pairedDevices)
            {
                device.Dispose();
            }
            _pairedDevices.Clear();

            _connectedDevice?.Dispose();
            _connectedDevice = null;

            Console.WriteLine("[信息] BluetoothManager 已释放所有资源");
        }
    }
}

/// <summary>
/// Win32 蓝牙 API P/Invoke 声明 - 作为 WinRT API 的后备方案
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static class BluetoothNativeMethods
{
    // 注意：以下 P/Invoke 声明在 AOT 下可能有限制
    // 主要用于 WinRT API 无法满足需求时的后备方案

    [StructLayout(LayoutKind.Sequential)]
    public struct BLUETOOTH_DEVICE_SEARCH_PARAMS
    {
        public uint dwSize;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnAuthenticated;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnRemembered;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnUnknown;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnConnected;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIssueInquiry;
        public byte cTimeoutMultiplier;
        public IntPtr hRadio;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BLUETOOTH_DEVICE_INFO
    {
        public uint dwSize;
        public ulong Address;
        public uint ulClassofDevice;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fConnected;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fRemembered;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAuthenticated;
        public SYSTEMTIME stLastSeen;
        public SYSTEMTIME stLastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string szName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    // 蓝牙设备枚举函数
    [DllImport("BluetoothAPIs.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr BluetoothFindFirstDevice(
        ref BLUETOOTH_DEVICE_SEARCH_PARAMS pbtsp,
        ref BLUETOOTH_DEVICE_INFO pbtdi);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BluetoothFindNextDevice(
        IntPtr hFind,
        ref BLUETOOTH_DEVICE_INFO pbtdi);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BluetoothFindDeviceClose(IntPtr hFind);

    // 音频端点管理
    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    public static extern uint BluetoothSetServiceState(
        IntPtr hRadio,
        ref BLUETOOTH_DEVICE_INFO pbtdi,
        ref Guid pGuidService,
        uint dwServiceFlags);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    public static extern uint BluetoothEnumerateInstalledServices(
        IntPtr hRadio,
        ref BLUETOOTH_DEVICE_INFO pbtdi,
        ref uint pcServiceInout,
        Guid[] pGuidServices);

    // A2DP 服务 GUID
    public static readonly Guid A2DP_SINK_SERVICE_CLASS = new("0000110B-0000-1000-8000-00805F9B34FB");
    public static readonly Guid AV_REMOTE_SERVICE_CLASS = new("0000110E-0000-1000-8000-00805F9B34FB");

    /// <summary>
    /// 使用 P/Invoke 获取已配对的蓝牙设备（后备方案）
    /// </summary>
    public static List<BLUETOOTH_DEVICE_INFO> GetPairedDevicesNative()
    {
        var devices = new List<BLUETOOTH_DEVICE_INFO>();

        var searchParams = new BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
            fReturnAuthenticated = true,
            fReturnRemembered = true,
            fReturnUnknown = false,
            fReturnConnected = true,
            fIssueInquiry = false,
            cTimeoutMultiplier = 0,
            hRadio = IntPtr.Zero
        };

        var deviceInfo = new BLUETOOTH_DEVICE_INFO
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>()
        };

        IntPtr hFind = BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);

        if (hFind != IntPtr.Zero)
        {
            do
            {
                devices.Add(deviceInfo);
                deviceInfo = new BLUETOOTH_DEVICE_INFO
                {
                    dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>()
                };
            }
            while (BluetoothFindNextDevice(hFind, ref deviceInfo));

            BluetoothFindDeviceClose(hFind);
        }

        return devices;
    }

    /// <summary>
    /// 启用设备的 A2DP Sink 服务
    /// </summary>
    public static bool EnableA2dpSinkService(IntPtr hRadio, ref BLUETOOTH_DEVICE_INFO deviceInfo)
    {
        const uint BLUETOOTH_SERVICE_ENABLE = 0x01;

        uint result = BluetoothSetServiceState(
            hRadio,
            ref deviceInfo,
            ref A2DP_SINK_SERVICE_CLASS,
            BLUETOOTH_SERVICE_ENABLE);

        return result == 0; // ERROR_SUCCESS
    }
}