using System.IO;
using System.Text.Json;

namespace RogueTrader.App.Services;

/// <summary>
/// Избранные предметы: то, что ведущий берёт в персонажа чаще всего.
///
/// Список хранится рядом с пакетами (data\settings\favorites.json) и не
/// относится к персонажу: избранное — удобство инструмента, а не часть
/// правил. Файл лежит вне папки userdata, чтобы не попасть в хоумбрю.
/// </summary>
public static class Favorites
{
    private static readonly HashSet<string> Ids = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    /// <summary>Файл со списком избранного.</summary>
    public static string FilePath => Path.Combine(AppPaths.SettingsDirectory, "favorites.json");

    /// <summary>Загрузить список (один раз за запуск).</summary>
    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            var ids = JsonSerializer.Deserialize<List<string>>(json);
            if (ids is null) return;
            foreach (var id in ids)
                if (!string.IsNullOrWhiteSpace(id)) Ids.Add(id.Trim());
        }
        catch
        {
            // Повреждённый файл избранного не должен мешать работать.
        }
    }

    /// <summary>Сохранить список.</summary>
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.SettingsDirectory);
            var json = JsonSerializer.Serialize(Ids.OrderBy(x => x).ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Не смогли сохранить — не повод мешать работать.
        }
    }

    /// <summary>Отмечен ли предмет как избранный.</summary>
    public static bool IsFavorite(string id) => !string.IsNullOrWhiteSpace(id) && Ids.Contains(id);

    /// <summary>Переключить отметку. Возвращает новое состояние.</summary>
    public static bool Toggle(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        Load();
        var now = !Ids.Contains(id);
        if (now) Ids.Add(id); else Ids.Remove(id);
        Save();
        return now;
    }
}
