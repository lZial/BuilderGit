using System.Text.Json;
using System.Text.Json.Serialization;
using RogueTrader.Core.Models;
using RogueTrader.Core.Rules;

namespace RogueTrader.Core.Data;

// ───────────────────────────── DTO форматов дата-пака ─────────────────────────────

public sealed class CharacteristicsFile
{
    public List<CharacteristicDef> Characteristics { get; set; } = [];
    public List<string> Order { get; set; } = [];
}

public sealed class SkillsFile
{
    public List<SkillDef> Skills { get; set; } = [];
}

public sealed class TalentsFile
{
    public List<TalentDef> Talents { get; set; } = [];
}

/// <summary>Умения и таланты из дополнений («Руководство по Тау», «Сквозь шторм»).</summary>
public sealed class ExtraSkillsFile
{
    public List<SkillDef> Skills { get; set; } = [];
    public List<TalentDef> Talents { get; set; } = [];
}

public sealed class CareersFile
{
    public List<CareerDef> Careers { get; set; } = [];
}

public sealed class ItemsFile
{
    public List<ItemDef> Items { get; set; } = [];
    public List<AugmeticDef> Augmetics { get; set; } = [];
    /// <summary>Правила книги текстом — чтобы информация не потерялась.</summary>
    public Dictionary<string, string> Rules { get; set; } = [];
    public List<DrugDef> Drugs { get; set; } = [];

    /// <summary>Разделы и подразделы снаряжения. Если пусто — берутся из <see cref="ItemGroups"/>.</summary>
    public List<ItemSectionDef> ItemSections { get; set; } = [];
    public List<ItemGroupDef> ItemGroups { get; set; } = [];
}

public sealed class CreationFile
{
    public List<NamedOptionDef> Homeworlds { get; set; } = [];
    public List<NamedOptionDef> Destiny { get; set; } = [];
    public List<NamedOptionDef> VoidPath { get; set; } = [];
    public List<NamedOptionDef> Hardship { get; set; } = [];
    public List<NamedOptionDef> Aspiration { get; set; } = [];
    public List<NamedOptionDef> Legacy { get; set; } = [];
    public List<NamedOptionDef> Inclinations { get; set; } = [];
    public List<NamedOptionDef> Ascension { get; set; } = [];
    public List<NamedOptionDef> ElitePackages { get; set; } = [];
    public List<NamedOptionDef> Traits { get; set; } = [];
    public Dictionary<string, string> Rules { get; set; } = [];
}

public sealed class ProgressionFile
{
    public List<WoundDef> Wounds { get; set; } = [];
    public List<IncomeRankDef> IncomeRanks { get; set; } = [];
    public List<ShipHullDef> ShipHulls { get; set; } = [];
    public List<int>? AdvancementCosts { get; set; }
    public List<int>? CareerRankCosts { get; set; }
    public List<int>? TalentRankCosts { get; set; }
    public List<int>? SkillCosts { get; set; }
    public int? XpPerSession { get; set; }
    /// <summary>Правила скорости по базовой книге: коэффициенты от бонуса Ловкости и сдвиг от размера.</summary>
    public MovementRulesDef? MovementRules { get; set; }
    public Dictionary<string, string> Rules { get; set; } = [];
}

public sealed class SpeciesFile
{
    public List<SpeciesDef> Species { get; set; } = [];
}

/// <summary>Глава VI «Психические приёмы» и глава VII «Силы навигаторов».</summary>
public sealed class PsiFile
{
    public List<PsyAbilityDef> PsyAbilities { get; set; } = [];
    public List<NavigatorPowerDef> NavigatorPowers { get; set; } = [];
    public Dictionary<string, string> Rules { get; set; } = [];
}

public sealed class TauFile
{
    public List<CasteDef> Castes { get; set; } = [];
    public List<CombatSuitDef> CombatSuits { get; set; } = [];
    public List<TalentDef> Talents { get; set; } = [];
    public List<SkillDef> Skills { get; set; } = [];
    public Dictionary<string, string> Rules { get; set; } = [];
}

/// <summary>Раса «Тау» и род занятий «Воин Огня» — дополнение «Руководство по Тау».</summary>
public sealed class TauCareerFile
{
    public List<SpeciesDef>? Species { get; set; }
    public List<CareerDef>? Careers { get; set; }
}

/// <summary>Единый файл пользовательского пака: может содержать любые разделы.</summary>
public sealed class UserPackFile
{
    public string Id { get; set; } = "homebrew";
    public string Name { get; set; } = "Хоумбрю";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";

    public List<CharacteristicDef>? Characteristics { get; set; }
    public List<SkillDef>? Skills { get; set; }
    public List<TalentDef>? Talents { get; set; }
    public List<CareerDef>? Careers { get; set; }
    public List<ItemDef>? Items { get; set; }
    public List<AugmeticDef>? Augmetics { get; set; }
    public List<DrugDef>? Drugs { get; set; }
    public List<SpeciesDef>? Species { get; set; }
    public List<CasteDef>? Castes { get; set; }
    public List<NamedOptionDef>? Homeworlds { get; set; }
    public List<NamedOptionDef>? Destiny { get; set; }
    public List<NamedOptionDef>? VoidPath { get; set; }
    public List<NamedOptionDef>? Hardship { get; set; }
    public List<NamedOptionDef>? Aspiration { get; set; }
    public List<NamedOptionDef>? Legacy { get; set; }
    public List<NamedOptionDef>? Inclinations { get; set; }
    public List<NamedOptionDef>? Ascension { get; set; }
    public List<NamedOptionDef>? ElitePackages { get; set; }
    public List<NamedOptionDef>? Traits { get; set; }
    public List<WoundDef>? Wounds { get; set; }
    public List<int>? AdvancementCosts { get; set; }
    public List<int>? CareerRankCosts { get; set; }
    public List<int>? TalentRankCosts { get; set; }
    public List<int>? SkillCosts { get; set; }
}

// ───────────────────────────── Загрузка и слияние ─────────────────────────────

public sealed class PackLoadReport
{
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Conflicts { get; } = [];
    public List<string> LoadedPacks { get; } = [];

    public bool HasErrors => Errors.Count > 0;
    public string Summary =>
        $"Паков: {LoadedPacks.Count} · ошибок: {Errors.Count} · предупреждений: {Warnings.Count} · переопределений: {Conflicts.Count}";
}

public static class GamePackLoader
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] BuiltinOrder =
    [
        "01-characteristics.json",
        "02-skills.json",
        "03-talents.json",
        "04-careers.json",
        "05-items.json",
        "06-creation.json",
        "07-progression.json",
        "08-species.json",
        "09-tau.json",
        "10-psi.json",
        "11-tau-career.json",
        "12-tau-items.json",
        "13-extra-skills.json",
        "14-storm-items.json",
    ];

    /// <summary>
    /// Загружает встроенный дата-пак и накладывает пользовательские (хоумбрю).
    /// Встроенный пак лежит в папке <paramref name="builtinDir"/> (для portable — рядом с exe: data\builtin).
    /// </summary>
    public static GamePack Load(string? builtinDir, string? userPackDir, PackLoadReport report)
    {
        var pack = new GamePack();
        LoadBuiltin(pack, builtinDir, report);

        if (!string.IsNullOrWhiteSpace(userPackDir) && Directory.Exists(userPackDir))
        {
            foreach (var file in Directory.GetFiles(userPackDir, "*.json").OrderBy(Path.GetFileName))
            {
                try
                {
                    var up = JsonSerializer.Deserialize<UserPackFile>(File.ReadAllText(file), Json);
                    if (up is null) { report.Errors.Add($"{Path.GetFileName(file)}: файл пуст."); continue; }
                    MergeUserPack(pack, up, Path.GetFileName(file), report);
                    report.LoadedPacks.Add($"Пользовательский: {up.Name} ({Path.GetFileName(file)})");
                }
                catch (Exception ex) { report.Errors.Add($"{Path.GetFileName(file)}: {ex.Message}"); }
            }
        }

        if (pack.CharacteristicsOrder.Count == 0)
            pack.CharacteristicsOrder.AddRange(pack.Characteristics.OrderBy(c => c.Column).Select(c => c.Id));

        RulesLookup.Characteristics = pack.Characteristics;
        pack.LoadedPacks = report.LoadedPacks;
        return pack;
    }

    private static void LoadBuiltin(GamePack pack, string? builtinDir, PackLoadReport report)
    {
        if (string.IsNullOrWhiteSpace(builtinDir) || !Directory.Exists(builtinDir))
        {
            report.Errors.Add(
                "Не найдена папка со встроенным дата-паком. Ожидается: <папка приложения>\\data\\builtin\\*.json. " +
                "Сгенерируйте пак командой: python tools\\build_datapack.py");
            return;
        }

        var found = 0;
        foreach (var name in BuiltinOrder)
        {
            var path = Path.Combine(builtinDir, name);
            if (!File.Exists(path))
            {
                report.Warnings.Add($"Файл данных {name} не найден в «{builtinDir}».");
                continue;
            }
            try
            {
                using var fs = File.OpenRead(path);
                Apply(pack, name, fs, report, false, "builtin");
                found++;
            }
            catch (Exception ex) { report.Errors.Add($"{name}: {ex.Message}"); }
        }

        if (found > 0) report.LoadedPacks.Add($"Встроенный: Базовые книги правил ({found} файлов)");
    }

    private static void Apply(GamePack pack, string name, Stream stream, PackLoadReport report, bool isUser, string packId)
    {
        void Mark<T>(T def) where T : RuleDef
        {
            def.IsHomebrew = isUser;
            if (isUser) def.PackId = packId;
            if (string.IsNullOrWhiteSpace(def.Source)) def.Source = isUser ? "homebrew" : "core";
        }

        switch (Path.GetFileNameWithoutExtension(name))
        {
            case "01-characteristics":
            {
                var f = Read<CharacteristicsFile>(stream, name);
                foreach (var c in f.Characteristics)
                {
                    c.IsHomebrew = isUser;
                    if (isUser) c.PackId = packId;
                    pack.Characteristics.RemoveAll(x => x.Id == c.Id);
                    pack.Characteristics.Add(c);
                }
                if (f.Order.Count > 0) pack.CharacteristicsOrder = f.Order;
                break;
            }
            case "02-skills":
                foreach (var s in Read<SkillsFile>(stream, name).Skills) { Mark(s); Add(pack.Skills, s.Id, s, report, name); }
                break;
            case "03-talents":
                foreach (var t in Read<TalentsFile>(stream, name).Talents) { Mark(t); Add(pack.Talents, t.Id, t, report, name); }
                break;
            case "04-careers":
                foreach (var c in Read<CareersFile>(stream, name).Careers) { Mark(c); Add(pack.Careers, c.Id, c, report, name); }
                break;
            case "05-items":
            {
                var f = Read<ItemsFile>(stream, name);
                foreach (var i in f.Items) { Mark(i); Add(pack.Items, i.Id, i, report, name); }
                foreach (var a in f.Augmetics) { Mark(a); Add(pack.Augmetics, a.Id, a, report, name); }
                foreach (var d in f.Drugs) { Mark(d); Add(pack.Drugs, d.Id, d, report, name); }

                // Кибернетика и препараты лежат в паке отдельными списками,
                // но для снаряжения персонажа это обычные предметы — переносим
                // их в общий каталог, сохранив место установки и эффект.
                foreach (var a in f.Augmetics) Add(pack.Items, "aug_" + a.Id, AsItem(a), report, name);
                foreach (var d in f.Drugs) Add(pack.Items, "drug_" + d.Id, AsItem(d), report, name);

                if (f.ItemSections.Count > 0) pack.ItemSections = f.ItemSections;
                if (f.ItemGroups.Count > 0) pack.ItemGroups = f.ItemGroups;
                // Раскладка по главе «Арсенал» — часть книги, а не данные пака:
                // если пака нет или он старый, берём встроенную раскладку.
                if (pack.ItemSections.Count == 0) pack.ItemSections = ItemGroups.Sections();
                if (pack.ItemGroups.Count == 0) pack.ItemGroups = ItemGroups.Defaults();
                break;
            }
            case "06-creation":
            {
                var f = Read<CreationFile>(stream, name);
                Opt(pack.Homeworlds, f.Homeworlds, isUser, packId, report, name, "родной мир");
                Opt(pack.Destiny, f.Destiny, isUser, packId, report, name, "уготованная судьба");
                Opt(pack.VoidPath, f.VoidPath, isUser, packId, report, name, "путь в пустоту");
                Opt(pack.Hardship, f.Hardship, isUser, packId, report, name, "испытание/страдание");
                Opt(pack.Aspiration, f.Aspiration, isUser, packId, report, name, "устремление");
                Opt(pack.Legacy, f.Legacy, isUser, packId, report, name, "наследие");
                Opt(pack.Inclinations, f.Inclinations, isUser, packId, report, name, "склонность");
                Opt(pack.Ascension, f.Ascension, isUser, packId, report, name, "возвышение");
                Opt(pack.ElitePackages, f.ElitePackages, isUser, packId, report, name, "элитное улучшение");
                Opt(pack.Traits, f.Traits, false, packId, report, name, "особенность");
                foreach (var kv in f.Rules) pack.Rules[kv.Key] = kv.Value;
                break;
            }
            case "07-progression":
            {
                var f = Read<ProgressionFile>(stream, name);
                foreach (var w in f.Wounds) Add(pack.Wounds, w.Id, w, report, name);
                if (f.IncomeRanks.Count > 0) pack.IncomeRanks = f.IncomeRanks;
                if (f.ShipHulls.Count > 0) pack.ShipHulls = f.ShipHulls;
                // Таблицы, которых может не быть в старой сборке пака, — берём из книги
                if (pack.IncomeRanks.Count == 0) pack.IncomeRanks = Defaults.IncomeTable15();
                if (pack.ShipHulls.Count == 0) pack.ShipHulls = Defaults.ShipHulls();
                if (f.AdvancementCosts is { Count: > 0 }) pack.AdvancementCosts = f.AdvancementCosts;
                if (f.CareerRankCosts is { Count: > 0 }) pack.CareerRankCosts = f.CareerRankCosts;
                if (f.TalentRankCosts is { Count: > 0 }) pack.TalentRankCosts = f.TalentRankCosts;
                if (f.SkillCosts is { Count: > 0 }) pack.SkillCosts = f.SkillCosts;
                if (f.XpPerSession > 0) pack.XpPerSession = f.XpPerSession.Value;
                if (f.MovementRules is not null) pack.MovementRules = f.MovementRules;
                foreach (var kv in f.Rules) pack.Rules[kv.Key] = kv.Value;
                break;
            }
            case "08-species":
                foreach (var s in Read<SpeciesFile>(stream, name).Species) { Mark(s); Add(pack.Species, s.Id, s, report, name); }
                break;
            case "10-psi":
            {
                var f = Read<PsiFile>(stream, name);
                foreach (var a in f.PsyAbilities) Mark(a);
                foreach (var w in f.NavigatorPowers) Mark(w);
                if (f.PsyAbilities.Count > 0) pack.PsyAbilities = f.PsyAbilities;
                if (f.NavigatorPowers.Count > 0) pack.NavigatorPowers = f.NavigatorPowers;
                foreach (var kv in f.Rules) pack.Rules["psi_" + kv.Key] = kv.Value;
                break;
            }
            case "09-tau":
            {
                var f = Read<TauFile>(stream, name);
                foreach (var c in f.Castes) { Mark(c); Add(pack.Castes, c.Id, c, report, name); }
                foreach (var c in f.CombatSuits) { Mark(c); Add(pack.CombatSuits, c.Id, c, report, name); }
                foreach (var t in f.Talents) { Mark(t); Add(pack.Talents, t.Id, t, report, name); }
                foreach (var s in f.Skills) { Mark(s); Add(pack.Skills, s.Id, s, report, name); }
                foreach (var kv in f.Rules) pack.Rules[kv.Key] = kv.Value;
                break;
            }
            case "11-tau-career":
            {
                // Раса «Тау» и род занятий «Воин Огня» из дополнения «Руководство по Тау».
                var f = Read<TauCareerFile>(stream, name);
                foreach (var s in f.Species ?? []) { Mark(s); Add(pack.Species, s.Id, s, report, name); }
                foreach (var c in f.Careers ?? []) { Mark(c); Add(pack.Careers, c.Id, c, report, name); }
                break;
            }
            case "12-tau-items":
            {
                // Оружие и броня тау по таблицам «Руководства по Тау» (стр. 27–28).
                var f = Read<ItemsFile>(stream, name);
                foreach (var i in f.Items) { Mark(i); Add(pack.Items, i.Id, i, report, name); }
                // Подразделы «Оружие тау» и «Броня тау» — перед основными,
                // иначе предметы попадут в подразделы базовой книги.
                if (f.ItemGroups.Count > 0)
                    pack.ItemGroups = [.. f.ItemGroups, .. pack.ItemGroups];
                break;
            }
            case "13-extra-skills":
            {
                // Умения и таланты из дополнений: «Руководство по Тау»
                // и «Сквозь шторм» (ксеносы).
                var f = Read<ExtraSkillsFile>(stream, name);
                foreach (var s in f.Skills) { Mark(s); Add(pack.Skills, s.Id, s, report, name); }
                foreach (var t in f.Talents) { Mark(t); Add(pack.Talents, t.Id, t, report, name); }
                break;
            }
            case "14-storm-items":
            {
                // Снаряжение из «Сквозь шторм»: арсенал орков и круутов,
                // новые игрушки и аугметика.
                var f = Read<ItemsFile>(stream, name);
                foreach (var i in f.Items) { Mark(i); Add(pack.Items, i.Id, i, report, name); }
                foreach (var a in f.Augmetics) { Mark(a); Add(pack.Items, "aug_" + a.Id, AsItem(a), report, name); }
                foreach (var kv in f.Rules) pack.Rules[kv.Key] = kv.Value;
                // Подразделы добавляем в конец: сначала должны совпасть
                // более точные подразделы базовой книги.
                foreach (var g in f.ItemGroups) pack.ItemGroups.Add(g);
                break;
            }
        }
    }

    private static void Opt(List<NamedOptionDef> target, List<NamedOptionDef>? src, bool isUser, string packId, PackLoadReport report, string file, string label)
    {
        if (src is null) return;
        foreach (var o in src)
        {
            if (isUser) { o.IsHomebrew = true; o.PackId = packId; }
            var idx = target.FindIndex(x => x.Id == o.Id);
            if (idx >= 0) { report.Conflicts.Add($"{label}: «{o.Name}» переопределён ({file})."); target[idx] = o; }
            else target.Add(o);
        }
    }

    private static void Add<T>(Dictionary<string, T> dict, string id, T value, PackLoadReport report, string file)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(id)) { report.Warnings.Add($"{file}: запись без id пропущена."); return; }
        if (dict.ContainsKey(id)) report.Conflicts.Add($"{file}: дубликат id «{id}» — использован последний.");
        dict[id] = value;
    }

    private static void Add(List<WoundDef> list, string id, WoundDef value, PackLoadReport report, string file)
    {
        if (string.IsNullOrWhiteSpace(id)) { report.Warnings.Add($"{file}: запись ранений без id пропущена."); return; }
        if (list.Any(w => w.Id == id)) report.Conflicts.Add($"{file}: дубликат ран «{id}».");
        list.Add(value);
    }

    /// <summary>
    /// Кибернетическое улучшение как обычный предмет снаряжения: те же название,
    /// описание, цена и доступность, плюс место установки и эффект.
    /// </summary>
    private static ItemDef AsItem(AugmeticDef a) => new()
    {
        Id = a.Id, Name = a.Name, Description = a.Description,
        Source = a.Source, IsHomebrew = a.IsHomebrew, PackId = a.PackId,
        Category = ItemCategory.Augmetic,
        Cost = a.Cost, Availability = a.Availability,
        Location = a.Location, Effect = a.Effect,
    };

    /// <summary>Препарат или яд как обычный предмет снаряжения.</summary>
    private static ItemDef AsItem(DrugDef d) => new()
    {
        Id = d.Id, Name = d.Name, Description = d.Description,
        Source = d.Source, IsHomebrew = d.IsHomebrew, PackId = d.PackId,
        Category = ItemCategory.Drug,
        Cost = d.Cost, Effect = d.Effect,
    };

    private static void MergeUserPack(GamePack pack, UserPackFile up, string file, PackLoadReport report)
    {
        void Mark<T>(T def) where T : RuleDef { def.IsHomebrew = true; def.PackId = up.Id; }

        if (up.Characteristics is { Count: > 0 })
            foreach (var c in up.Characteristics) { c.IsHomebrew = true; c.PackId = up.Id; pack.Characteristics.RemoveAll(x => x.Id == c.Id); pack.Characteristics.Add(c); }
        if (up.Skills is not null) foreach (var s in up.Skills) { Mark(s); Add(pack.Skills, s.Id, s, report, file); }
        if (up.Talents is not null) foreach (var t in up.Talents) { Mark(t); Add(pack.Talents, t.Id, t, report, file); }
        if (up.Careers is not null) foreach (var c in up.Careers) { Mark(c); Add(pack.Careers, c.Id, c, report, file); }
        if (up.Items is not null) foreach (var i in up.Items) { Mark(i); Add(pack.Items, i.Id, i, report, file); }
        if (up.Augmetics is not null) foreach (var a in up.Augmetics) { Mark(a); Add(pack.Augmetics, a.Id, a, report, file); }
        if (up.Drugs is not null) foreach (var d in up.Drugs) { Mark(d); Add(pack.Drugs, d.Id, d, report, file); }
        if (up.Species is not null) foreach (var s in up.Species) { Mark(s); Add(pack.Species, s.Id, s, report, file); }
        if (up.Castes is not null) foreach (var c in up.Castes) { Mark(c); Add(pack.Castes, c.Id, c, report, file); }
        if (up.Wounds is not null) foreach (var w in up.Wounds) Add(pack.Wounds, w.Id, w, report, file);

        Opt(pack.Homeworlds, up.Homeworlds, true, up.Id, report, file, "родной мир");
        Opt(pack.Destiny, up.Destiny, true, up.Id, report, file, "уготованная судьба");
        Opt(pack.VoidPath, up.VoidPath, true, up.Id, report, file, "путь в пустоту");
        Opt(pack.Hardship, up.Hardship, true, up.Id, report, file, "испытание/страдание");
        Opt(pack.Aspiration, up.Aspiration, true, up.Id, report, file, "устремление");
        Opt(pack.Legacy, up.Legacy, true, up.Id, report, file, "наследие");
        Opt(pack.Inclinations, up.Inclinations, true, up.Id, report, file, "склонность");
        Opt(pack.Ascension, up.Ascension, true, up.Id, report, file, "возвышение");
        Opt(pack.ElitePackages, up.ElitePackages, true, up.Id, report, file, "элитное улучшение");
        Opt(pack.Traits, up.Traits, true, up.Id, report, file, "особенность");

        if (up.AdvancementCosts is { Count: > 0 }) pack.AdvancementCosts = up.AdvancementCosts;
        if (up.CareerRankCosts is { Count: > 0 }) pack.CareerRankCosts = up.CareerRankCosts;
        if (up.TalentRankCosts is { Count: > 0 }) pack.TalentRankCosts = up.TalentRankCosts;
        if (up.SkillCosts is { Count: > 0 }) pack.SkillCosts = up.SkillCosts;
    }

    private static T Read<T>(Stream stream, string name)
    {
        using var r = new StreamReader(stream);
        var json = r.ReadToEnd();
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException($"{name}: файл пуст.");
        return JsonSerializer.Deserialize<T>(json, Json)
               ?? throw new InvalidOperationException($"{name}: десериализация вернула null.");
    }
}