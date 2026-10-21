using RogueTrader.Core.Models;

namespace RogueTrader.Core.Data;

/// <summary>
/// Правила скорости передвижения по базовой книге (стр. 251 «Скорость передвижения»,
/// таблица 9-30 «Передвижение в структурированном времени» и таблица 14-2 «Размеры»).
/// </summary>
public sealed class MovementRulesDef
{
    /// <summary>Коэффициенты от бонуса Ловкости: половинная, полная, натиск, бег.</summary>
    public Dictionary<string, double> SpeedByDexBonus { get; set; } = new()
    {
        ["half"] = 1, ["full"] = 2, ["charge"] = 3, ["run"] = 6,
    };
    /// <summary>Минимумы для бонуса Ловкости 0.</summary>
    public Dictionary<string, double> MinByDexBonus { get; set; } = new()
    {
        ["half"] = 0.5, ["full"] = 1, ["charge"] = 2, ["run"] = 3,
    };
    /// <summary>Сдвиг бонуса Ловкости от размера (таблица 14-2, колонка «Скорость»).</summary>
    public Dictionary<int, int> SizeSpeedModifier { get; set; } = new()
    {
        [1] = -3, [2] = -2, [3] = -1, [4] = 0, [5] = 1, [6] = 2, [7] = 3,
    };
    /// <summary>Прыжки: метров на единицу бонуса Силы (лист персонажа: БС×1м и БС×20см).</summary>
    public Dictionary<string, double> Jumps { get; set; } = new()
    {
        ["longPerStr"] = 1, ["highPerStr"] = 0.2,
    };

    /// <summary>Сдвиг скорости от размера персонажа.</summary>
    public int ModifierForSize(int size) => SizeSpeedModifier.GetValueOrDefault(size);

    /// <summary>Коэффициент для одного из четырёх движений.</summary>
    public double Coefficient(string kind) => SpeedByDexBonus.GetValueOrDefault(kind, 1);

    /// <summary>Минимум для одного из четырёх движений.</summary>
    public double Minimum(string kind) => MinByDexBonus.GetValueOrDefault(kind, 0);
}

/// <summary>
/// Полный игровой дата-пак: встроенные данные книг + наложенные пользовательские (хоумбрю).
/// Все коллекции — словари по Id, чтобы быстро резолвить ссылки.
/// </summary>
public sealed class GamePack
{
    public string Name { get; set; } = "Rogue Trader (DH2e)";
    public string Version { get; set; } = "1.0";

    public List<CharacteristicDef> Characteristics { get; set; } = [];
    public List<string> CharacteristicsOrder { get; set; } = [];

    /// <summary>Стоимость шагов улучшения характеристики: 100 / 200 / 300 / 400 / 500.</summary>
    public List<int> AdvancementCosts { get; set; } = [100, 200, 300, 400, 500];
    /// <summary>Стоимость повышения ранга рода занятий (базовая / продвинутая).</summary>
    public List<int> CareerRankCosts { get; set; } = [500, 1000];
    /// <summary>Стоимость рангов таланта: 100 / 200 / 300.</summary>
    public List<int> TalentRankCosts { get; set; } = [100, 200, 300];
    /// <summary>Стоимость +10 к умению: базовое / продвинутое.</summary>
    public List<int> SkillCosts { get; set; } = [100, 200];
    public int XpPerSession { get; set; } = 500;

    /// <summary>
    /// Правила скорости по базовой книге (стр. 251, таблица 9-30 и таблица 14-2):
    /// длина дистанций зависит от бонуса Ловкости, размер этот бонус сдвигает.
    /// </summary>
    public MovementRulesDef MovementRules { get; set; } = new();

    public Dictionary<string, SkillDef> Skills { get; set; } = [];
    public Dictionary<string, TalentDef> Talents { get; set; } = [];
    public Dictionary<string, CareerDef> Careers { get; set; } = [];
    public Dictionary<string, ItemDef> Items { get; set; } = [];
    public Dictionary<string, AugmeticDef> Augmetics { get; set; } = [];
    public Dictionary<string, DrugDef> Drugs { get; set; } = [];
    public Dictionary<string, SpeciesDef> Species { get; set; } = [];
    public Dictionary<string, CasteDef> Castes { get; set; } = [];
    public Dictionary<string, CombatSuitDef> CombatSuits { get; set; } = [];

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

    public List<WoundDef> Wounds { get; set; } = [];
    public List<IncomeRankDef> IncomeRanks { get; set; } = [];
    /// <summary>Корпуса звёздных кораблей (глава VIII) с ценой в очках корабля.</summary>
    public List<ShipHullDef> ShipHulls { get; set; } = [];

    /// <summary>Психические приёмы (глава VI) — из дисциплин и древев приёмов.</summary>
    public List<PsyAbilityDef> PsyAbilities { get; set; } = [];
    /// <summary>Силы навигатора (глава VII и дополнение «Сквозь шторм»).</summary>
    public List<NavigatorPowerDef> NavigatorPowers { get; set; } = [];

    /// <summary>Разделы снаряжения — верхний уровень главы «Арсенал».</summary>
    public List<ItemSectionDef> ItemSections { get; set; } = [];
    /// <summary>Подразделы снаряжения (лазерное оружие, доспехи, аугметика…).</summary>
    public List<ItemGroupDef> ItemGroups { get; set; } = [];
    public List<NamedOptionDef> CareerRankQualifications { get; set; } = [];

    /// <summary>Справочные тексты правил: ключ → текст из книги.</summary>
    public Dictionary<string, string> Rules { get; set; } = [];

    public List<string> LoadedPacks { get; set; } = [];

    // ── удобные резолвы ──
    public SkillDef? Skill(string? id) => id is not null && Skills.TryGetValue(id, out var s) ? s : null;
    public TalentDef? Talent(string? id) => id is not null && Talents.TryGetValue(id, out var t) ? t : null;
    public CareerDef? Career(string? id) => id is not null && Careers.TryGetValue(id, out var c) ? c : null;
    public ItemDef? Item(string? id) => id is not null && Items.TryGetValue(id, out var i) ? i : null;

    /// <summary>
    /// Описание предмета персонажа: из пака — либо свой предмет, добавленный
    /// игроком (лут, талисман, подарок).
    /// </summary>
    public ItemDef? ItemOf(ItemEntry? entry)
    {
        if (entry is null) return null;
        if (!string.IsNullOrEmpty(entry.ItemId))
        {
            var def = Item(entry.ItemId);
            if (def is not null) return def;
        }
        return entry.Custom;
    }
    public AugmeticDef? Augmetic(string? id) => id is not null && Augmetics.TryGetValue(id, out var a) ? a : null;
    public SpeciesDef? SpeciesOf(string? id) => id is not null && Species.TryGetValue(id, out var s) ? s : null;
    public CasteDef? Caste(string? id) => id is not null && Castes.TryGetValue(id, out var c) ? c : null;
    public CharacteristicDef? Characteristic(string id) => Characteristics.FirstOrDefault(c => c.Id == id);

    public List<CharacteristicDef> OrderedCharacteristics =>
        CharacteristicsOrder.Count > 0
            ? CharacteristicsOrder.Select(id => Characteristics.FirstOrDefault(c => c.Id == id))
                                  .Where(c => c is not null).Select(c => c!).ToList()
            : Characteristics.OrderBy(c => c.Column).ToList();

    public List<ItemDef> ItemsByCategory(string cat) =>
        Items.Values.Where(i => i.Category == cat).OrderBy(i => i.Name, StringComparer.CurrentCulture).ToList();

    public static NamedOptionDef? Find(List<NamedOptionDef> list, string? id) =>
        id is null ? null : list.FirstOrDefault(o => o.Id == id);
}