# Audiosink - 蓝牙音频接收器

一个 Windows 11 风格的蓝牙音频接收器应用程序，可以将电脑变成蓝牙音箱。

## 功能特性

- **A2DP Sink 支持**：将电脑注册为蓝牙音频接收端
- **Win11 风格 UI**：Mica 材质背景、圆角设计、Segoe Fluent Icons
- **系统托盘集成**：最小化到托盘，点击弹出控制窗口
- **自动设备发现**：扫描已配对的蓝牙设备
- **音频路由**：自动将蓝牙音频路由到电脑扬声器

## 项目结构

```
Audiosink/
├── App.xaml                    # 应用程序入口
├── App.xaml.cs                 # 应用程序逻辑
├── Audiosink.csproj           # 项目文件
├── BluetoothManager.cs        # 蓝牙管理核心逻辑
├── IconGenerator.cs           # 图标生成器
├── MainWindow.xaml            # 主窗口（隐藏）
├── MainWindow.xaml.cs         # 主窗口逻辑
├── Program.cs                 # 程序入口点
├── Resources/                 # 资源文件目录
│   └── README.md             # 资源说明
├── TrayFlyout.xaml            # 托盘弹出窗口
└── TrayFlyout.xaml.cs         # 弹出窗口逻辑
```

## 技术栈

- **.NET 9.0** + **WPF**
- **Native AOT** 编译优化
- **H.NotifyIcon.Wpf** 系统托盘支持
- **Windows.Devices.Bluetooth.Audio** WinRT API

## 编译要求

- Visual Studio 2022 或更高版本
- .NET 9.0 SDK
- Windows 10 版本 19041 或更高

## 编译步骤

1. 克隆或下载项目
2. 在 Visual Studio 中打开 `Audiosink.csproj`
3. 还原 NuGet 包
4. 编译项目

## 使用说明

1. **启动应用程序**：运行编译后的可执行文件
2. **系统托盘**：应用程序启动后会最小化到系统托盘
3. **打开控制窗口**：点击托盘图标打开控制窗口
4. **连接设备**：点击"连接设备"按钮连接第一个已配对的蓝牙设备
5. **断开连接**：点击"断开连接"按钮断开当前连接

## 配置说明

### 蓝牙设置

确保您的电脑满足以下要求：

- 蓝牙适配器支持 A2DP Sink 功能
- Windows 10 版本 19041 或更高
- 已安装最新的蓝牙驱动程序

### 音频设置

应用程序会自动使用系统默认的音频输出设备（扬声器）。如需更改音频输出：

1. 打开 Windows 设置
2. 进入"系统" > "声音"
3. 选择"输出设备"

## 开发说明

### Native AOT 兼容性

本项目设计为 Native AOT 兼容：

- 避免使用反射（Reflection）
- 避免使用动态类型（dynamic）
- 使用直接的 P/Invoke 声明
- 所有类型在编译时静态已知

### 添加新功能

如需添加新功能：

1. 在 `BluetoothManager.cs` 中添加蓝牙相关逻辑
2. 在 `TrayFlyout.xaml` 中添加 UI 元素
3. 在 `TrayFlyout.xaml.cs` 中添加交互逻辑
4. 在 `App.xaml.cs` 中集成新功能

## 故障排除

### 蓝牙连接失败

- 检查蓝牙适配器是否正常工作
- 确认设备已配对且在范围内
- 检查 Windows 蓝牙设置

### 音频播放问题

- 检查默认音频设备是否正确
- 确认音量未静音
- 尝试重新连接设备

### 应用程序崩溃

- 检查事件查看器中的错误日志
- 确认 .NET 9.0 运行时已安装
- 尝试以管理员身份运行

## 许可证

MIT License

## 贡献

欢迎提交 Issue 和 Pull Request！