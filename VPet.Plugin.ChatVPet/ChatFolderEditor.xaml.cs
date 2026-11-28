#nullable enable
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace VPet.Plugin.ChatVPet;

public partial class ChatFolderEditor : UserControl
{
    public ChatFolderEditor()
    {
        InitializeComponent();
        Folders.SelectionChanged += (_, _) => UpdateButtons();
        UpdateButtons();
    }

    internal IEnumerable<string> Paths => Folders.Items.Cast<string>();

    internal void LoadFolders(IEnumerable<string> paths)
    {
        Folders.Items.Clear();
        foreach (string path in paths) Folders.Items.Add(path);
        if (Folders.Items.Count > 0) Folders.SelectedIndex = 0;
    }

    private void AddFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Multiselect = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        foreach (string path in dialog.FolderNames)
        {
            string? existing = Paths.FirstOrDefault(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
            if (existing == null) Folders.Items.Add(path);
            Folders.SelectedItem = existing ?? path;
        }
        Folders.ScrollIntoView(Folders.SelectedItem);
    }

    private void SetDefault(object sender, RoutedEventArgs e)
    {
        var path = Folders.SelectedItem;
        Folders.Items.Remove(path);
        Folders.Items.Insert(0, path);
        Folders.Items.Refresh();
        Folders.SelectedIndex = 0;
        Folders.ScrollIntoView(path);
    }

    private void RemoveFolder(object sender, RoutedEventArgs e)
    {
        int index = Folders.SelectedIndex;
        Folders.Items.RemoveAt(index);
        Folders.Items.Refresh();
        Folders.SelectedIndex = Math.Min(index, Folders.Items.Count - 1);
    }

    private void UpdateButtons()
    {
        DefaultButton.IsEnabled = Folders.SelectedIndex > 0;
        RemoveButton.IsEnabled = Folders.SelectedIndex >= 0;
    }
}
