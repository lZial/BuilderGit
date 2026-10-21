using System;
using System.IO;
using System.Text.Json;

namespace RogueTrader.App.Services;

/// <summary>
/// Настройки интерфейса. Хранятся рядом с программой в data\settings\ui.json —
/// как в браузере, выбранный масштаб не сбрасывается при следующем запуске.
/// Файл лежит в папке settings, а не в userdata, чтобы не попасть в хоумбрю.
/// </summary>
public static class UiSettings
{
    /// <summary>Файл настроек интерфейса.</summary>
    public static string FilePath => Path.Combine(AppPaths.SettingsDirectory, "ui.json");

    /// <summary>Читает сохранённый масштаб. Битый или отсутствующий файл — 100%.</summary>
    public static double LoadZoom()
    {
        try
        {
            if (!File.Exists(FilePath)) return 1.0;
            using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (doc.RootElement.TryGetProperty("zoom", out var z) && z.TryGetDouble(out var v))
                return v;
        }
        catch
        {
            // Настройки — необязательные: нечитаемый файл не должен мешать работе.
        }
        return 1.0;
    }

    /// <summary>Сохраняет масштаб интерфейса.</summary>
    public static void SaveZoom(double zoom)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.SettingsDirectory);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(new { zoom = Math.Round(zoom, 2) }));
        }
        catch
        {
            // Записать настройки не вышло — работаем без сохранения.
        }
    }
}
