using ChatVPet.ChatProcess;
using LinePutScript;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VPet_Simulator.Core;
using VPet_Simulator.Windows.Interface;
using static VPet_Simulator.Core.GraphInfo;

namespace VPet.Plugin.ChatVPet;

public partial class CVPPlugin
{
    private HostReceipt ObserveScreenshot(ChatTurn turn)
    {
        if (OpenAIConfig?.ScreenshotEnabled != true || toolApi == null) return new HostReceipt { Status = "skipped", Facts = "未启用截图或未配置视觉操作 API。" };
        byte[] bytes;
        if (OpenAIConfig.ScreenshotScope == "screen")
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            using var bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); bytes = stream.ToArray();
        }
        else
        {
            bytes = MW.Dispatcher.Invoke(() =>
            {
                var target = MW.PetGrid;
                var bitmap = new RenderTargetBitmap((int)target.ActualWidth, (int)target.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(target);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
            });
        }
        string observation = toolApi.Observe(turn.Original, bytes, "image/png");
        return new HostReceipt { Status = "observed", Facts = observation };
    }

    private HostReceipt FinishQuit(GameHost host)
    {
        if (OpenAIConfig?.AllowQuit != true) return new HostReceipt { Status = "skipped", Facts = "玩家未允许桌宠自行退出。" };
        if (TalkAPI?.LastSpeechCompleted != true) return new HostReceipt { Status = "failed", Facts = "本句尚未确认播报结束，保留会话。" };
        if (!host.IsCurrentSave) return new HostReceipt { Status = "skipped", Facts = "已切换存档。" };
        MW.Save();
        var window = (Window)MW;
        var closed = new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        void OnClosed(object? sender, EventArgs args) => closed.TrySetResult(true);
        MW.Dispatcher.Invoke(() => window.Closed += OnClosed);
        try
        {
            MW.Dispatcher.Invoke(MW.Close);
            return closed.Task.Wait(TimeSpan.FromSeconds(20))
                ? new HostReceipt { Status = "applied", Facts = "宿主已保存并关闭。" }
                : new HostReceipt { Status = "unknown", Facts = "已请求正常退出，但尚未收到关闭完成通知。" };
        }
        finally
        {
            if (!MW.Dispatcher.HasShutdownStarted) MW.Dispatcher.Invoke(() => window.Closed -= OnClosed);
        }
    }
}
