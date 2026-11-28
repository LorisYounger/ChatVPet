#nullable enable
using ChatVPet.ChatProcess;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.ChatVPet;

internal sealed class ChatFiles(string plugin)
{
    // The game directory itself may be writable. Keep its permission list outside the files the pet can edit.
    private const string SettingsKey = @"HKEY_CURRENT_USER\Software\VPet\ChatFileFolders";

    private static string? DocumentFolder
    {
        get
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return documents.Length == 0 ? null : Path.Combine(documents, "VPet");
        }
    }

    public string[] Folders
    {
        get
        {
            if (Registry.GetValue(SettingsKey, plugin, null) is string[] folders) return folders;
            return DocumentFolder is string folder ? [folder, ExtensionValue.BaseDirectory] : [ExtensionValue.BaseDirectory];
        }
    }

    public void CreateDocumentFolder()
    {
        if (DocumentFolder is string folder && Folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
            Directory.CreateDirectory(folder);
    }

    public bool ReadAnywhere => Registry.GetValue(SettingsKey, plugin + ".ReadAnywhere", null) is 1;

    public void SaveFolders(IEnumerable<string> paths, bool readAnywhere)
    {
        var folders = paths.Select(NormalPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (string folder in folders) Directory.CreateDirectory(folder);
        Registry.SetValue(SettingsKey, plugin, folders, RegistryValueKind.MultiString);
        Registry.SetValue(SettingsKey, plugin + ".ReadAnywhere", readAnywhere ? 1 : 0, RegistryValueKind.DWord);
    }

    private static string NormalPath(string path)
    {
        path = Path.GetFullPath(path);
        if (path.StartsWith(@"\\?\") || path.StartsWith(@"\\.\"))
            throw new ArgumentException("请使用普通文件路径。");
        foreach (string name in path[Path.GetPathRoot(path)!.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' ')
                || Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|(?:COM|LPT)[0-9¹²³]) *(?:\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                throw new ArgumentException("路径中包含无效的文件名。");
        return Path.TrimEndingDirectorySeparator(path);
    }

    private static string FilePath(string path, string baseFolder, string[] folders, bool readAnywhere = false)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path))
            throw new ArgumentException("请指定文件的绝对路径或相对路径。");
        path = NormalPath(Path.Combine(baseFolder, path));
        if (readAnywhere) return path;
        string? root = folders.FirstOrDefault(folder => path.StartsWith(Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));
        if (root == null)
            throw new UnauthorizedAccessException("文件在允许的目录之外。");
        path = root + path[root.Length..];
        // A junction or symbolic link can lead outside the directory selected by the player.
        for (string? current = path; current != null; current = Path.GetDirectoryName(current))
        {
            var attributes = new FileInfo(current).Attributes;
            if (attributes != (FileAttributes)(-1) && attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new UnauthorizedAccessException("文件路径经过链接或重解析点。");
        }
        return path;
    }

    public HostReceipt Read(ChatTurn turn, Dictionary<string, string> args)
    {
        try
        {
            var folders = Folders;
            bool readAnywhere = ReadAnywhere;
            if (!readAnywhere && folders.Length == 0)
                return new() { Status = "skipped", Facts = "玩家未允许访问任何文件夹。" };
            string baseFolder = folders.FirstOrDefault() ?? DocumentFolder ?? ExtensionValue.BaseDirectory;
            if (string.IsNullOrWhiteSpace(args.GetValueOrDefault("path")))
                return new() { Status = "observed", Facts = (readAnywhere ? "可读取当前用户有权访问的文本文件。" : "读取限于下列目录。")
                    + $"相对读取路径的起点：{baseFolder}\n允许写入的目录："
                    + (folders.Length == 0 ? "无" : "\n" + string.Join("\n", folders)) };
            string path = FilePath(args["path"], baseFolder, folders, readAnywhere);
            if (!int.TryParse(args.GetValueOrDefault("offset", "0"), out int offset) || offset < 0)
                return new() { Status = "failed", Facts = "offset 应为非负字符位置。" };
            using var reader = new StreamReader(path, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
            var buffer = new char[4096];
            for (int remaining = offset; remaining > 0;)
            {
                int count = reader.ReadBlock(buffer, 0, Math.Min(remaining, buffer.Length));
                if (count == 0) return new() { Status = "failed", Facts = "offset 超出文件长度。" };
                remaining -= count;
            }
            int length = reader.ReadBlock(buffer, 0, buffer.Length);
            string content = new(buffer, 0, length);
            if (content.Contains('\0')) return new() { Status = "failed", Facts = "文件包含二进制数据，请选择文本文件。" };
            if (length > 0 && char.IsHighSurrogate(content[^1]) && char.IsLowSurrogate((char)reader.Peek()))
                content += (char)reader.Read();
            string more = reader.Peek() < 0 ? "已读到文件末尾" : $"后续内容从 offset={offset + (long)content.Length} 继续";
            return new() { Status = "observed", Facts = $"{path}（{more}）\n{content}" };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { return new() { Status = "failed", Facts = "读取失败：" + ex.Message }; }
    }

    public HostReceipt Write(ChatTurn turn, Dictionary<string, string> args)
    {
        try
        {
            var folders = Folders;
            if (folders.Length == 0) return new() { Status = "skipped", Facts = "玩家未设置允许写入的目录。" };
            if (!args.TryGetValue("content", out string? content)) return new() { Status = "failed", Facts = "缺少要写入的正文。" };
            string path = FilePath(args.GetValueOrDefault("path", ""), folders[0], folders);
            string directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            string temp = Path.Combine(directory, Path.GetRandomFileName());
            var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            try
            {
                using (var writer = new StreamWriter(stream))
                    writer.Write(content);
                File.Move(temp, path, overwrite: true);
            }
            finally { File.Delete(temp); }
            return new() { Status = "applied", WrittenFile = path, Facts = $"已写入 {path}（{content.Length} 个字符）。" };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { return new() { Status = "failed", Facts = "写入失败：" + ex.Message }; }
    }
}
