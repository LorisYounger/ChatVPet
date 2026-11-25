using System.IO;
using LinePutScript;

namespace ChatVPet.Workbench;

public sealed class WorkbenchDefaults
{
    public string Config { get; set; } = "ChatVPetProcess.Qw35-v1.Settings.json";
    public string ModDirectory { get; set; } = "";
    public string SaveFile { get; set; } = "";
}

internal static class WorkbenchData
{
    public static void Import(string directory, SessionOptions options, string? branchSource)
    {
        if (branchSource != null)
        {
            foreach (string name in new[] { "catalog.lps", "pet.lps", "imported-save.lps" })
            {
                string file = Path.Combine(branchSource, name);
                if (File.Exists(file)) File.Copy(file, Path.Combine(directory, name));
            }
            return;
        }
        if (!string.IsNullOrEmpty(options.SaveFile))
        {
            if (!string.Equals(Path.GetExtension(options.SaveFile), ".lps", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("请选择 .lps 格式的 VPet 存档。");
            string save = File.ReadAllText(options.SaveFile);
            if (new LpsDocument(save).FindLine("vpet") == null)
                throw new InvalidDataException("所选存档缺少 vpet 记录，请选择 VPet 的 Save*.lps 文件。");
            File.WriteAllText(Path.Combine(directory, "imported-save.lps"), save);
        }
        if (string.IsNullOrEmpty(options.ModDirectory)) return;
        var root = new DirectoryInfo(options.ModDirectory);
        if (!root.Exists) throw new DirectoryNotFoundException("找不到 MOD 目录：" + root.FullName);
        var catalog = new LpsDocument();
        foreach (var file in Definitions(root, "food"))
            foreach (var line in new LpsDocument(File.ReadAllText(file)).FindAllLine("food"))
            {
                if (string.IsNullOrWhiteSpace(line.GetString("name")))
                    throw new InvalidDataException("食物缺少 name：" + file);
                catalog.Add(line);
            }
        // A MOD can contain several pet definitions. Use the default VPet when present.
        var pets = Definitions(root, "pet").Select(file => new LpsDocument(File.ReadAllText(file)))
            .Where(doc => doc.FirstOrDefault()?.Name.Equals("pet", StringComparison.OrdinalIgnoreCase) == true).ToList();
        var pet = pets.FirstOrDefault(doc => doc.First()?.Info is "vup" or "默认虚拟桌宠") ?? pets.FirstOrDefault();
        if (pet == null && catalog.FirstOrDefault() == null)
            throw new InvalidDataException("此目录没有可导入的 food/*.lps 商品或 pet/*.lps 桌宠配置。请选择具体的 MOD 目录。");
        File.WriteAllText(Path.Combine(directory, "catalog.lps"), catalog.ToString());
        if (pet != null) File.WriteAllText(Path.Combine(directory, "pet.lps"), pet.ToString());
    }

    private static IEnumerable<string> Definitions(DirectoryInfo root, string kind)
    {
        string path = Path.Combine(root.FullName, kind);
        return Directory.Exists(path) ? Directory.EnumerateFiles(path, "*.lps").Order(StringComparer.OrdinalIgnoreCase) : [];
    }
}
