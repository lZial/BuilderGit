using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RogueTrader.Core.Data;
using RogueTrader.Core.Models;
using RogueTrader.Core.Rules;

namespace RogueTrader.App.Services;

/// <summary>Генерация печатного листа персонажа в PDF.</summary>
public static class PdfExportService
{
    private static string Hex(string hex) => hex;

    private static readonly string Ink = "#1A1A1A";
    private static readonly string Accent = "#7A1E14";
    private static readonly string MutedHex = "#666666";
    private static readonly string RuleHex = "#BBBBBB";

    private static void Configure()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public static void Export(Character c, DataPackService svc, string path)
    {
        Configure();
        var pack = svc.Pack;
        var engine = new RulesEngine(pack);
        var d = engine.Compute(c);
        var career = pack.Career(c.CareerId);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(t => t.FontFamily("Segoe UI").FontSize(9).FontColor(Ink));
                page.Header().Element(e => Header(e, c, career, d));
                page.Content().Element(e => Body(e, c, pack, d));
                page.Footer().Element(Footer);
            });
        })
        .GeneratePdf(path);
    }

    private static void Header(IContainer e, Character c, CareerDef? career, DerivedStats d)
    {
        e.Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c1 =>
                {
                    c1.Item().Text(string.IsNullOrWhiteSpace(c.Meta.Name) ? "Безымянный" : c.Meta.Name)
                        .FontSize(21).Bold().FontColor(Accent);
                    c1.Item().Text($"Род занятий: {career?.Name ?? "—"} · ранг {d.Rank}")
                        .FontSize(10).FontColor(MutedHex);
                });
                r.RelativeItem().AlignRight().Column(c2 =>
                {
                    c2.Item().Text($"Всего опыта: {d.TotalXp} ОО").FontSize(9);
                    c2.Item().Text($"Диапазон ранга: {d.RankMin}–{d.RankMax}").FontSize(9).FontColor(MutedHex);
                });
            });
            col.Item().PaddingVertical(6f).LineHorizontal(1).LineColor(Accent);
        });
    }

    private static void Body(IContainer e, Character c, GamePack pack, DerivedStats d)
    {
        e.Column(col =>
        {
            col.Spacing(9);

            // ── Характеристики ──
            col.Item().Text("ХАРАКТЕРИСТИКИ").FontSize(11).Bold().FontColor(Accent);
            col.Item().Row(r =>
            {
                foreach (var cv in d.Characteristics)
                    r.RelativeItem().BorderBottom(1).BorderColor(RuleHex).PaddingVertical(3f).Column(cc =>
                    {
                        cc.Item().Text(cv.Abbrev).FontSize(7.5f).FontColor(MutedHex);
                        cc.Item().Text(cv.Total.ToString()).FontSize(15).Bold();
                        cc.Item().Text($"+{cv.CharBonus}").FontSize(8).FontColor(MutedHex);
                    });
            });

            // ── Сводка ──
            col.Item().Text("СВОДКА").FontSize(11).Bold().FontColor(Accent);
            col.Item().Row(r =>
            {
                r.RelativeItem().Element(ct => Kv(ct, "Порог ранений", d.WoundThreshold.ToString()));
                r.RelativeItem().Element(ct => Kv(ct, "Ран сейчас", c.Wounds.ToString()));
                r.RelativeItem().Element(ct => Kv(ct, "Очки судьбы", c.FatePoints.ToString()));
                r.RelativeItem().Element(ct => Kv(ct, "Размер", c.Size.ToString()));
                r.RelativeItem().Element(ct => Kv(ct, "Скорость", $"{d.Movement} м"));
                r.RelativeItem().Element(ct => Kv(ct, "Защита", d.ArmorProtection.ToString()));
            });
            if (c.PsyRating > 0 || c.Insanity > 0 || c.Corruption > 0)
                col.Item().Row(r =>
                {
                    r.RelativeItem().Element(ct => Kv(ct, "Пси-рейтинг", c.PsyRating.ToString()));
                    r.RelativeItem().Element(ct => Kv(ct, "Очки безумия", c.Insanity.ToString()));
                    r.RelativeItem().Element(ct => Kv(ct, "Очки порчи", c.Corruption.ToString()));
                    r.RelativeItem().PaddingVertical(1f);
                    r.RelativeItem().PaddingVertical(1f);
                });

            // ── Умения ──
            // В колонке — итог листа умений (как в книге), а не «сколько ступеней
            // куплено»: у стартового умения это 0, хотя применяется оно на +10/+20.
            var skills = c.Skills
                .Select(x =>
                {
                    var def = pack.Skill(x.SkillId);
                    return (Def: def, Total: Core.Rules.SkillValues.Total(def, x, d));
                })
                .ToList();
            if (skills.Count > 0)
            {
                col.Item().Text("УМЕНИЯ").FontSize(11).Bold().FontColor(Accent);
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.ConstantColumn(46); cd.RelativeColumn(); });
                    foreach (var (def, total) in skills)
                    {
                        t.Cell().Element(cell => cell.PaddingVertical(2f).Text(def?.Name ?? "—").FontSize(8.5f));
                        t.Cell().Element(cell => cell.PaddingVertical(2f).AlignRight().Text($"{total}").Bold().FontSize(8.5f));
                        t.Cell().Element(cell => cell.PaddingVertical(2f).Text(
                            def is null || def.Characteristics.Count == 0 ? ""
                            : "· " + string.Join(", ", def.Characteristics.Select(RulesEngine.CharacteristicRu)))
                            .FontSize(7.5f).FontColor(MutedHex));
                    }
                });
            }

            // ── Таланты ──
            if (c.Talents.Count > 0)
            {
                col.Item().Text("ТАЛАНТЫ").FontSize(11).Bold().FontColor(Accent);
                col.Item().Column(cc =>
                {
                    foreach (var t in c.Talents)
                    {
                        var def = pack.Talent(t.TalentId);
                        // Ранг таланта в книге считается с единицы, а в модели он
                        // с нуля: стартовый талант — первый ранг.
                        var rank = t.Rank > 0 ? $" (ранг {t.Rank + 1})" : "";
                        cc.Item().Text((def?.Name ?? t.TalentId) + rank)
                            .FontSize(8.5f).SemiBold();
                        if (!string.IsNullOrWhiteSpace(def?.Description))
                            cc.Item().PaddingBottom(3f).Text(def.Description).FontSize(7.5f).FontColor(MutedHex);
                    }
                });
            }

            // ── Снаряжение ──
            if (c.Items.Count > 0)
            {
                col.Item().Text("СНАРЯЖЕНИЕ").FontSize(11).Bold().FontColor(Accent);
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(4); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                    foreach (var it in c.Items)
                    {
                        var def = pack.ItemOf(it);
                        if (def is null) continue;
                        // Качество оружия — по книге: «высшее», «хорошее» и т. п.
                        var quality = Core.Models.WeaponQualities.ByName(it.Quality);
                        var title = def.Name;
                        if (quality is not null) title += $" ({quality.Name.ToLowerInvariant()} качество)";
                        if (it.Quantity > 1) title += $" ×{it.Quantity}";
                        t.Cell().Element(cell => cell.PaddingVertical(2f).Text(title).FontSize(8.5f));
                        t.Cell().Element(cell => cell.PaddingVertical(2f).Text(ItemCategory.Ru(def.Category)).FontSize(7.5f).FontColor(MutedHex));
                        t.Cell().Element(cell => cell.PaddingVertical(2f).Text($"вес {def.Encumbrance}").FontSize(7.5f).FontColor(MutedHex));
                        // Доступность по книге — вместо выдуманной цены.
                        t.Cell().Element(cell => cell.PaddingVertical(2f).Text(string.IsNullOrWhiteSpace(def.Availability) ? "—" : def.Availability).FontSize(7.5f).FontColor(MutedHex));
                    }
                });
            }

            // ── Жизненный путь ──
            var hw = GamePack.Find(pack.Homeworlds, c.HomeworldIds.FirstOrDefault());
            var dest = GamePack.Find(pack.Destiny, c.ForetoldDestinyId);
            var asp = GamePack.Find(pack.Aspiration, c.AspirationId);
            if (hw is not null || dest is not null || asp is not null)
            {
                col.Item().Text("ЖИЗНЕННЫЙ ПУТЬ").FontSize(11).Bold().FontColor(Accent);
                if (hw is not null) col.Item().Text($"Родной мир: {hw.Name}").FontSize(8.5f);
                if (dest is not null) col.Item().Text($"Уготованная судьба: {dest.Name}").FontSize(8.5f);
                if (asp is not null) col.Item().Text($"Устремление: {asp.Name}").FontSize(8.5f);
                // Выбранные варианты разделов («Сквозь шторм»): название и цена.
                foreach (var (section, key, option) in new (string, string, Core.Models.NamedOptionDef?)[]
                         {
                             ("Уготованная судьба", "destiny", dest),
                             ("Путь в пустоту", "void", GamePack.Find(pack.VoidPath, c.VoidPathId)),
                             ("Испытание", "hardship", GamePack.Find(pack.Hardship, c.HardshipIds.FirstOrDefault())),
                             ("Устремление", "aspiration", asp),
                             ("Наследие", "legacy", GamePack.Find(pack.Legacy, c.LegacyIds.FirstOrDefault())),
                         })
                {
                    if (option is null) continue;
                    var vid = c.PathVariantIds.GetValueOrDefault(key) ?? "";
                    if (vid.Length == 0) continue;
                    var v = option.Variants.FirstOrDefault(x => x.Id == vid);
                    if (v is null) continue;
                    col.Item().Text($"{section} — вариант: {v.Name}" + (v.CostXp > 0 ? $" ({v.CostXp} ОО)" : "")).FontSize(8.5f);
                }
            }

            // ── Особенности ──
            if (!string.IsNullOrEmpty(c.Meta.Traits))
            {
                col.Item().Text("ОСОБЕННОСТИ").FontSize(11).Bold().FontColor(Accent);
                col.Item().Text(string.Join(" · ", c.Meta.Traits.Split(", ", StringSplitOptions.RemoveEmptyEntries))).FontSize(8);
            }

            // ── Замечания ──
            if (d.Warnings.Count > 0)
            {
                col.Item().Text("ЗАМЕЧАНИЯ").FontSize(11).Bold().FontColor("#8A5A00");
                foreach (var w in d.Warnings) col.Item().Text("• " + w).FontSize(7.5f);
            }

            // ── Журнал правок мастера ──
            if (c.Adjustments.Count > 0)
            {
                col.Item().Text("ЖУРНАЛ ПРАВОК").FontSize(11).Bold().FontColor(Accent);
                col.Item().Text("Ручные правки ведущего: опыт за них не списывается.").FontSize(7.5f).FontColor(MutedHex);
                foreach (var a in c.Adjustments.OrderByDescending(x => x.When))
                    col.Item().Text($"{a.When:dd.MM.yyyy HH:mm} — {a.What}: было {a.Before}, стало {a.After}").FontSize(7.5f);
            }
        });
    }

    private static void Kv(IContainer c, string key, string val)
    {
        c.BorderBottom(1).BorderColor(RuleHex).PaddingVertical(3f).Column(cc =>
        {
            cc.Item().Text(key).FontSize(7.5f).FontColor(MutedHex);
            cc.Item().Text(val).FontSize(13).Bold();
        });
    }

    private static void Footer(IContainer e)
    {
        e.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(RuleHex);
            col.Item().PaddingTop(4).Row(r =>
            {
                r.RelativeItem().Text("Rogue Trader Builder").FontSize(7.5f).FontColor(MutedHex);
                r.RelativeItem().AlignRight().Text(t => t.CurrentPageNumber().FontSize(7.5f).FontColor(MutedHex));
            });
        });
    }
}