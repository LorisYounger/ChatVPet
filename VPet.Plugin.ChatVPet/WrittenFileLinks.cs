#nullable enable
using LinePutScript.Localization.WPF;
using Panuon.WPF.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VPet_Simulator.Core;

namespace VPet.Plugin.ChatVPet;

public sealed class WrittenFileLinks : WrapPanel
{
    internal void AddFile(string path, Button? button = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0
            || Children.OfType<Button>().Any(b => string.Equals(b.ToolTip as string, path, StringComparison.OrdinalIgnoreCase))) return;
        if (button == null)
        {
            button = new Button
            {
                Cursor = Cursors.Hand, FontSize = 13,
                Padding = new Thickness(9, 2, 9, 2), Margin = new Thickness(0, 0, 5, 4), BorderThickness = new Thickness(1),
                Background = Function.ResourcesBrush(Function.BrushType.PrimaryLighter),
                Foreground = Function.ResourcesBrush(Function.BrushType.PrimaryText),
                BorderBrush = Function.ResourcesBrush(Function.BrushType.DARKPrimary)
            };
            ButtonHelper.SetCornerRadius(button, new CornerRadius(6));
        }
        string label = "定位文件：{0}".Translate(Path.GetFileName(path));
        button.Content = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.ToolTip = path;
        button.MaxWidth = 240;
        button.Click += (_, e) =>
        {
            e.Handled = true;
            if (!File.Exists(path))
            {
                MessageBox.Show("找不到文件：{0}".Translate(path));
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                    $"/select,\"{path}\"") { UseShellExecute = false });
            }
            catch (System.ComponentModel.Win32Exception ex) { MessageBox.Show(ex.Message); }
        };
        Children.Add(button);
    }
}
