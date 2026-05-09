using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Audiosink;

/// <summary>
/// 简单的图标生成器，用于在没有外部图标文件时生成默认图标
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static class IconGenerator
{
    /// <summary>
    /// 生成一个简单的蓝牙音频图标
    /// </summary>
    public static Icon GenerateDefaultIcon()
    {
        // 创建一个 256x256 的位图
        using var bitmap = new Bitmap(256, 256);
        using var graphics = Graphics.FromImage(bitmap);

        // 设置高质量渲染
        graphics.SmoothingMode = SmoothingMode.AntiAliasing;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // 清除背景（透明）
        graphics.Clear(Color.Transparent);

        // 绘制圆形背景
        using var backgroundBrush = new SolidBrush(Color.FromArgb(0, 95, 184)); // #005FB8
        graphics.FillEllipse(backgroundBrush, 8, 8, 240, 240);

        // 绘制蓝牙图标符号
        using var iconPen = new Pen(Color.White, 12);
        iconPen.StartCap = LineCap.Round;
        iconPen.EndCap = LineCap.Round;

        // 绘制简化的蓝牙图标
        // 中心竖线
        graphics.DrawLine(iconPen, 128, 70, 128, 186);

        // 上方三角形
        graphics.DrawLine(iconPen, 128, 70, 180, 120);
        graphics.DrawLine(iconPen, 128, 120, 180, 120);

        // 下方三角形
        graphics.DrawLine(iconPen, 128, 186, 180, 136);
        graphics.DrawLine(iconPen, 128, 136, 180, 136);

        // 音频波纹效果
        using var wavePen = new Pen(Color.FromArgb(200, Color.White), 6);
        wavePen.StartCap = LineCap.Round;
        wavePen.EndCap = LineCap.Round;

        // 第一道波纹
        graphics.DrawArc(wavePen, 190, 100, 40, 56, -45, 90);

        // 第二道波纹
        graphics.DrawArc(wavePen, 210, 85, 50, 86, -45, 90);

        // 将位图转换为图标
        IntPtr hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon);

        // 复制图标以便释放原始句柄
        var clonedIcon = (Icon)icon.Clone();
        icon.Dispose();
        DestroyIcon(hIcon);

        return clonedIcon;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}