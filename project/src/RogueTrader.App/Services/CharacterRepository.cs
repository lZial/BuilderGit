using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RogueTrader.Core.Models;

namespace RogueTrader.App.Services;

/// <summary>Сохранение и загрузка персонажей в JSON.</summary>
public sealed class CharacterRepository
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string? CurrentPath { get; private set; }

    public static void Save(Character c, string? path = null)
    {
        path ??= CurrentPath;
        AppPaths.EnsureDirectories();
        if (string.IsNullOrWhiteSpace(path))
        {
            var name = SafeFileName(c.Meta.Name);
            path = Path.Combine(AppPaths.CharactersDirectory, name + ".rtchar.json");
        }
        c.Meta.ModifiedUtc = DateTime.UtcNow;
        c.Meta.AppVersion = AppPaths.AppVersion;
        var json = JsonSerializer.Serialize(c, Options);
        File.WriteAllText(path, json);
        CurrentPath = path;
    }

    public static Character Load(string path)
    {
        var json = File.ReadAllText(path);
        var c = JsonSerializer.Deserialize<Character>(json, Options) ?? new Character();
        c.SchemaVersion = "2.0";
        // В прежних версиях ручная правка мастера лежала в «Прочем».
        // Теперь «Прочее» считает система, а ручная правка живёт в «Правках».
        foreach (var e in c.Characteristics)
        {
            if (e.Other != 0) { e.Adjustment += e.Other; e.Other = 0; }
        }
        foreach (var e in c.Skills)
        {
            if (e.Other != 0) { e.Adjustment += e.Other; e.Other = 0; }
        }
        CurrentPath = path;
        return c;
    }

    public static List<(string Path, string Name, DateTime Modified)> List()
    {
        if (!Directory.Exists(AppPaths.CharactersDirectory)) return [];
        var list = new List<(string, string, DateTime)>();
        foreach (var f in Directory.GetFiles(AppPaths.CharactersDirectory, "*.rtchar.json"))
        {
            try
            {
                var info = new FileInfo(f);
                string name = "(без имени)";
                using var fs = File.OpenRead(f);
                using var doc = JsonDocument.Parse(fs);
                if (doc.RootElement.TryGetProperty("meta", out var meta) &&
                    meta.TryGetProperty("name", out var n))
                    name = string.IsNullOrWhiteSpace(n.GetString()) ? "(без имени)" : n.GetString()!;
                list.Add((f, name, info.LastWriteTime));
            }
            catch { list.Add((f, "(повреждён)", File.GetLastWriteTime(f))); }
        }
        return list.OrderByDescending(x => x.Item3).ToList();
    }

    public static string SafeFileName(string? name)
    {
        var n = string.IsNullOrWhiteSpace(name) ? "Персонаж" : name!;
        var bad = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(n.Length);
        foreach (var ch in n) sb.Append(bad.Contains(ch) ? '_' : ch);
        return sb.ToString().Trim();
    }

    /// <summary>Экспорт в Markdown — удобно для вики и переписки.</summary>
    public static string ToMarkdown(Character c, DataPackService svc)
    {
        var pack = svc.Pack;
        var engine = new Core.Rules.RulesEngine(pack);
        var d = engine.Compute(c);
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"# {c.Meta.Name}");
        var career = pack.Career(c.CareerId);
        sb.AppendLine($"*Род занятий: {career?.Name ?? "—"}, ранг {d.Rank} · Всего опыта: {d.TotalXp} ОО*");
        sb.AppendLine();

        sb.AppendLine("## Характеристики");
        sb.AppendLine("| Характеристика | Значение | Бонус |");
        sb.AppendLine("|---|---|---|");
        foreach (var cv in d.Characteristics) sb.AppendLine($"| {cv.Name} | {cv.Total} | +{cv.CharBonus} |");
        sb.AppendLine($"\n**Порог ранений:** {d.WoundThreshold} · **Скорость:** {d.Movement} м · **Очки судьбы:** {c.FatePoints}");
        sb.AppendLine();

        // ── Жизненный путь ──
        {
            var lines = new List<string>();
            void AddPath(string title, string key, Core.Models.NamedOptionDef? option)
            {
                if (option is null) return;
                var text = $"{title}: {option.Name}";
                var vid = c.PathVariantIds.GetValueOrDefault(key) ?? "";
                if (vid.Length > 0)
                {
                    var v = option.Variants.FirstOrDefault(x => x.Id == vid);
                    if (v is not null) text += $" — вариант «{v.Name}»" + (v.CostXp > 0 ? $" ({v.CostXp} ОО)" : "");
                }
                lines.Add(text);
            }
            AddPath("Родной мир", "homeworld", pack.Homeworlds.FirstOrDefault(h => c.HomeworldIds.Contains(h.Id)));
            AddPath("Уготованная судьба", "destiny", pack.Destiny.FirstOrDefault(x => x.Id == c.ForetoldDestinyId));
            AddPath("Путь в пустоту", "void", pack.VoidPath.FirstOrDefault(x => x.Id == c.VoidPathId));
            AddPath("Испытание", "hardship", pack.Hardship.FirstOrDefault(x => c.HardshipIds.Contains(x.Id)));
            AddPath("Устремление", "aspiration", pack.Aspiration.FirstOrDefault(x => x.Id == c.AspirationId));
            AddPath("Наследие", "legacy", pack.Legacy.FirstOrDefault(x => c.LegacyIds.Contains(x.Id)));
            if (lines.Count > 0)
            {
                sb.AppendLine("## Жизненный путь");
                foreach (var l in lines) sb.AppendLine($"- {l}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Умения");
        foreach (var e in c.Skills)
        {
            var sk = pack.Skill(e.SkillId);
            if (sk is null) continue;
            // Итог листа умений — как в книге (у стартового умения это 20,
            // а не «+0»: ступени, купленные за опыт, идут в счёт итога).
            var total = Core.Rules.SkillValues.Total(sk, e, d);
            sb.AppendLine($"- **{sk.Name}** {total}");
        }
        sb.AppendLine();

        sb.AppendLine("## Таланты");
        foreach (var e in c.Talents)
        {
            var t = pack.Talent(e.TalentId);
            if (t is null) continue;
            // Ранг таланта в книге считается с единицы, в модели — с нуля.
            var rank = e.Rank > 0 ? $" (ранг {e.Rank + 1})" : "";
            sb.AppendLine($"- **{t.Name}**{rank}");
            if (!string.IsNullOrWhiteSpace(t.Description)) sb.AppendLine($"  > {t.Description}");
        }
        sb.AppendLine();

        sb.AppendLine("## Снаряжение");
        foreach (var e in c.Items)
        {
            var it = pack.ItemOf(e);
            if (it is null) continue;
            // Качество оружия — по книге (низкое, среднее, хорошее, высшее).
            var quality = Core.Models.WeaponQualities.ByName(e.Quality);
            var line = $"- {it.Name}";
            if (quality is not null) line += $" ({quality.Name.ToLowerInvariant()} качество)";
            line += $" × {e.Quantity}";
            if (!string.IsNullOrWhiteSpace(it.Availability)) line += $" — доступность: {it.Availability}";
            if (e.Custom is not null) line += " — свой предмет";
            if (!string.IsNullOrWhiteSpace(it.Note)) line += $"\n  > {it.Note}";
            sb.AppendLine(line);
        }

        if (d.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Предупреждения");
            foreach (var w in d.Warnings) sb.AppendLine($"- ⚠ {w}");
        }

        if (c.Adjustments.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Журнал правок");
            foreach (var a in c.Adjustments.OrderByDescending(x => x.When))
                sb.AppendLine($"- {a.When:dd.MM.yyyy HH:mm} — {a.What}: было {a.Before}, стало {a.After}");
        }
        return sb.ToString();
    }
}