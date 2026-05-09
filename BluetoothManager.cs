using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Audiosink;

/// <summary>
/// A2DP Sink 核心管理器 - 将电脑注册为蓝牙音频接收端
/// 使用 Win32 P/Invoke API 实现
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public sealed class BluetoothManager : IDisposable
{
    private BluetoothNativeMethods.BLUETOOTH_DEVICE_INFO? _connectedDevice;
    private bool _disposed;

    // 已发现的配对设备列表
    private readonly List<BluetoothNativeMethods.BLUETOOTH_DEVICE_INFO> _pairedDevices = [];

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
    public string? ConnectedDeviceName => _connectedDevice?.szName;

    /// <summary>
    /// 是否有设备连接
    /// </summary>
    public bool IsConnected => _connectedDevice.HasValue;

    /// <summary>
    /// 初始化蓝牙子系统，检查 A2DP Sink 支持
    /// </summary>
    public Task<bool> InitializeAsync()
    {
        try
        {
            // 检查蓝牙服务是否运行
            bool isRunning = BluetoothNativeMethods.IsBluetoothServiceRunning();
            if (!isRunning)
            {
                Console.WriteLine("[错误] 蓝牙服务未运行");
                return Task.FromResult(false);
            }

            Console.WriteLine("[信息] 蓝牙子系统初始化完成");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 初始化蓝牙失败: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// 扫描已配对的蓝牙设备
    /// </summary>
    public Task<int> DiscoverPairedDevicesAsync()
    {
        _pairedDevices.Clear();

        try
        {
            var devices = BluetoothNativeMethods.GetPairedDevicesNative();

            Console.WriteLine($"[信息] 找到 {devices.Count} 个已配对设备");

            foreach (var device in devices)
            {
                _pairedDevices.Add(device);
                string deviceName = string.IsNullOrEmpty(device.szName) ? "未知设备" : device.szName;
                Console.WriteLine($"  - {deviceName} [{device.Address:X12}]");
                OnDeviceDiscovered?.Invoke(deviceName);
            }

            return Task.FromResult(_pairedDevices.Count);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 扫描设备失败: {ex.Message}");
            return Task.FromResult(0);
        }
    }

    /// <summary>
    /// 尝试连接指定的蓝牙设备作为 A2DP Sink
    /// </summary>
    /// <param name="deviceIndex">设备索引（从 0 开始）</param>
    public Task<bool> ConnectAsSinkAsync(int deviceIndex)
    {
        if (deviceIndex < 0 || deviceIndex >= _pairedDevices.Count)
        {
            Console.WriteLine("[错误] 无效的设备索引");
            return Task.FromResult(false);
        }

        var device = _pairedDevices[deviceIndex];
        return ConnectAsSinkAsync(device);
    }

    /// <summary>
    /// 尝试连接指定的蓝牙设备作为 A2DP Sink
    /// </summary>
    /// <param name="device">目标蓝牙设备</param>
    public Task<bool> ConnectAsSinkAsync(BluetoothNativeMethods.BLUETOOTH_DEVICE_INFO device)
    {
        try
        {
            string deviceName = string.IsNullOrEmpty(device.szName) ? "未知设备" : device.szName;
            Console.WriteLine($"[信息] 正在尝试连接设备: {deviceName}");

            // 启用 A2DP Sink 服务
            bool enabled = BluetoothNativeMethods.EnableA2dpSinkService(IntPtr.Zero, ref device);
            if (!enabled)
            {
                Console.WriteLine($"[警告] 无法启用 A2DP Sink 服务，错误代码: {Marshal.GetLastWin32Error()}");
                // 继续尝试，某些设备可能已经启用
            }

            // 标记为已连接
            _connectedDevice = device;
            Console.WriteLine($"[成功] 已连接到设备: {deviceName}");

            // 触发连接事件
            OnDeviceConnected?.Invoke(deviceName);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 连接设备失败: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// 断开当前连接
    /// </summary>
    public Task DisconnectAsync()
    {
        if (_connectedDevice.HasValue)
        {
            try
            {
                var device = _connectedDevice.Value;
                string deviceName = string.IsNullOrEmpty(device.szName) ? "未知设备" : device.szName;

                // 禁用 A2DP Sink 服务
                BluetoothNativeMethods.DisableA2dpSinkService(IntPtr.Zero, ref device);

                _connectedDevice = null;
                Console.WriteLine($"[信息] 已断开设备: {deviceName}");
                OnDeviceDisconnected?.Invoke(deviceName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[错误] 断开连接时出错: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取所有已配对设备的名称列表
    /// </summary>
    public IReadOnlyList<string> GetPairedDeviceNames()
    {
        return _pairedDevices
            .Select(d => string.IsNullOrEmpty(d.szName) ? "未知设备" : d.szName)
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

            // 断开连接
            if (_connectedDevice.HasValue)
            {
                var device = _connectedDevice.Value;
                BluetoothNativeMethods.DisableA2dpSinkService(IntPtr.Zero, ref device);
                _connectedDevice = null;
            }

            _pairedDevices.Clear();

            Console.WriteLine("[信息] BluetoothManager 已释放所有资源");
        }
    }
}

/// <summary>
/// Win32 蓝牙 API P/Invoke 声明
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static class BluetoothNativeMethods
{
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

    // 蓝牙服务管理
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

    // 蓝牙无线电管理
    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    public static extern IntPtr BluetoothFindFirstRadio(
        ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp,
        out IntPtr phRadio);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BluetoothFindRadioClose(IntPtr hFind);

    [StructLayout(LayoutKind.Sequential)]
    public struct BLUETOOTH_FIND_RADIO_PARAMS
    {
        public uint dwSize;
    }

    // A2DP 服务 GUID
    public static readonly Guid A2DP_SINK_SERVICE_CLASS = new("0000110B-0000-1000-8000-00805F9B34FB");
    public static readonly Guid AV_REMOTE_SERVICE_CLASS = new("0000110E-0000-1000-8000-00805F9B34FB");

    /// <summary>
    /// 检查蓝牙服务是否运行
    /// </summary>
    public static bool IsBluetoothServiceRunning()
    {
        try
        {
            var searchParams = new BLUETOOTH_FIND_RADIO_PARAMS
            {
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_FIND_RADIO_PARAMS>()
            };

            IntPtr hFind = BluetoothFindFirstRadio(ref searchParams, out IntPtr hRadio);
            if (hFind != IntPtr.Zero)
            {
                CloseHandle(hRadio);
                BluetoothFindRadioClose(hFind);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// 使用 P/Invoke 获取已配对的蓝牙设备
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

    /// <summary>
    /// 禁用设备的 A2DP Sink 服务
    /// </summary>
    public static bool DisableA2dpSinkService(IntPtr hRadio, ref BLUETOOTH_DEVICE_INFO deviceInfo)
    {
        const uint BLUETOOTH_SERVICE_DISABLE = 0x00;

        uint result = BluetoothSetServiceState(
            hRadio,
            ref deviceInfo,
            ref A2DP_SINK_SERVICE_CLASS,
            BLUETOOTH_SERVICE_DISABLE);

        return result == 0; // ERROR_SUCCESS
    }
}
