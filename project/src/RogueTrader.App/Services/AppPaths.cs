using System.IO;
using System.Reflection;

namespace RogueTrader.App.Services;

/// <summary>
/// Пути приложения. Portable-режим: всё живёт рядом с .exe.
/// ВНИМАНИЕ: при сборке в один файл AppContext.BaseDirectory указывает во временную
/// папку распаковки, поэтому путь берём у самого процесса.
/// </summary>
public static class AppPaths
{
    private static string Root = ResolveRoot();

    private static string ResolveRoot()
    {
        // ProcessPath — реальный путь к exe (работает и при single-file, и при обычной сборке)
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            try
            {
                var dir = Path.GetDirectoryName(exe);
                if (!string.IsNullOrEmpty(dir) && File.Exists(exe))
                    return dir!;
            }
            catch { }
        }
        return AppContext.BaseDirectory;
    }

    /// <summary>Папка с exe.</summary>
    public static string AppDirectory => Root;

    /// <summary>Встроенный дата-пак: data\builtin\*.json</summary>
    public static string BuiltinPackDirectory =>
        Path.Combine(FindDataRoot(), "builtin");

    /// <summary>Пользовательские данные (хоумбрю): data\userdata\*.json</summary>
    public static string UserPackDirectory =>
        Path.Combine(FindDataRoot(), "userdata");

    /// <summary>Сохранённые персонажи: data\characters\*.json</summary>
    public static string CharactersDirectory =>
        Path.Combine(FindDataRoot(), "characters");

    /// <summary>Экспорт (PDF/PNG): data\export\</summary>
    public static string ExportDirectory =>
        Path.Combine(FindDataRoot(), "export");

    /// <summary>
    /// Настройки приложения (избранное и прочее): data\settings\.
    /// Отдельно от userdata, чтобы эти файлы не попадали в хоумбрю.
    /// </summary>
    public static string SettingsDirectory =>
        Path.Combine(FindDataRoot(), "settings");

    private static string _dataRoot = "";

    /// <summary>
    /// Ищет папку data: сначала рядом с exe, затем на два уровня выше
    /// (при запуске из bin\Debug\net10.0-windows в режиме разработки).
    /// </summary>
    private static string FindDataRoot()
    {
        if (_dataRoot.Length > 0) return _dataRoot;

        var candidates = new List<string>
        {
            Path.Combine(Root, "data"),
            Path.Combine(Directory.GetParent(Root)?.FullName ?? Root, "data"),
            Path.Combine(Directory.GetParent(Directory.GetParent(Root)?.FullName ?? Root)?.FullName ?? Root, "data"),
            Path.Combine(Root, "..", "..", "..", "..", "data"),
        };

        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (Directory.Exists(full)) { _dataRoot = full; return _dataRoot; }
        }

        // Ничего нет — создадим рядом с exe при первом сохранении
        _dataRoot = Path.Combine(Root, "data");
        return _dataRoot;
    }

    public static void EnsureDirectories()
    {
        try
        {
            foreach (var d in new[] { FindDataRoot(), BuiltinPackDirectory, UserPackDirectory, CharactersDirectory, ExportDirectory })
            {
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Не удалось создать папки данных: {ex.Message}");
        }
    }

    public static string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
}