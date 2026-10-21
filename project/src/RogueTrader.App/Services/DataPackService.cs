using System.Diagnostics;
using System.Text.Json;
using RogueTrader.Core.Data;

namespace RogueTrader.App.Services;

public static class Debug
{
    public static void WriteLine(string msg) => System.Diagnostics.Debug.WriteLine(msg);
}

/// <summary>Загрузка и горячая перезагрузка дата-паков.</summary>
public sealed class DataPackService
{
    public GamePack Pack { get; private set; } = new();
    public PackLoadReport Report { get; private set; } = new();

    public event EventHandler? PackReloaded;

    public void Load()
    {
        AppPaths.EnsureDirectories();
        var report = new PackLoadReport();
        var pack = GamePackLoader.Load(AppPaths.BuiltinPackDirectory, AppPaths.UserPackDirectory, report);
        Pack = pack;
        Report = report;
        foreach (var e in report.Errors) Debug.WriteLine("[pack] ОШИБКА: " + e);
        foreach (var w in report.Warnings) Debug.WriteLine("[pack] " + w);
        PackReloaded?.Invoke(this, EventArgs.Empty);
    }

    public static readonly JsonSerializerOptions Json = GamePackLoader.Json;

    // ── быстрые выборки для UI ──

    public IEnumerable<Core.Models.SkillDef> AllSkills =>
        Pack.Skills.Values.OrderBy(s => s.Name, StringComparer.CurrentCulture);

    public IEnumerable<Core.Models.TalentDef> AllTalents =>
        Pack.Talents.Values.OrderBy(t => t.Name, StringComparer.CurrentCulture);

    public IEnumerable<Core.Models.CareerDef> AllCareers =>
        Pack.Careers.Values.OrderBy(c => c.Name, StringComparer.CurrentCulture);

    public IEnumerable<Core.Models.ItemDef> AllItems =>
        Pack.Items.Values.OrderBy(i => i.Name, StringComparer.CurrentCulture);

    public IEnumerable<Core.Models.SpeciesDef> AllSpecies =>
        Pack.Species.Values.OrderBy(s => s.Name, StringComparer.CurrentCulture);

    /// <summary>Мягкий поиск по русским и английским названиям.</summary>
    public static bool Matches(string? haystack, string? needle)
    {
        if (string.IsNullOrWhiteSpace(needle)) return true;
        if (string.IsNullOrEmpty(haystack)) return false;
        return haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}