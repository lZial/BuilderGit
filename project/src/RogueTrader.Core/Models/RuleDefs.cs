namespace RogueTrader.Core.Models;

/// <summary>Базовый тип для всех определений из книг правил.</summary>
public abstract class RuleDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Источник: core | darkmarkets | stormstout | abhuman | tau | homebrew.</summary>
    public string Source { get; set; } = "core";

    /// <summary>true для хоумбрю / правок игрока.</summary>
    public bool IsHomebrew { get; set; }

    /// <summary>Имя пака, в котором определено (для домашних данных).</summary>
    public string? PackId { get; set; }
}

public sealed class CharacteristicDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Abbrev { get; set; } = "";
    public int Column { get; set; }
    public string Description { get; set; } = "";
    public bool IsHomebrew { get; set; }
    public string? PackId { get; set; }
}

/// <summary>Таблица модификаторов/стоимости. Не используется в DH2e, оставлена для совместимости паков.</summary>
[Obsolete("Система DH2e не использует таблицу модификаторов по порогам.")]
public sealed class CharacteristicModifier
{
    public int Threshold { get; set; }
    public string Modifier { get; set; } = "";
    public int RollD100 { get; set; }
    public int RollD10 { get; set; }
    public int Bonus { get; set; }
    public int Points { get; set; }
    public List<string> Special { get; set; } = [];
}

/// <summary>Машинно-читаемое требование (для талантов и проверки доступности).</summary>
public sealed class RequirementDef
{
    public string Type { get; set; } = "characteristic"; // characteristic|talent|skill|species|career|rank|group|any
    public string? Characteristic { get; set; }
    public int Min { get; set; }

    public List<string> Talents { get; set; } = [];
    public List<SkillRequirement> Skills { get; set; } = [];
    public List<string> Species { get; set; } = [];

    /// <summary>Для type=career/rank: имя рода занятий и минимальный ранг.</summary>
    public string? Career { get; set; }
    public int RankAtLeast { get; set; } = 1;

    public string? Text { get; set; }
    /// <summary>Для type=group / any — вложенные условия.</summary>
    public List<RequirementDef> Of { get; set; } = [];

    public string Describe()
    {
        return Type switch
        {
            "characteristic" => $"{Characteristic} ≥ {Min}",
            "talent" => string.Join(" или ", Talents),
            "skill" => string.Join(", ", Skills.Select(s => $"{s.Id} {s.Rank}+")),
            "species" => string.Join("/", Species),
            "career" => $"Род занятий: {Career}",
            "rank" => $"Ранг {Career} ≥ {RankAtLeast}",
            "group" => string.Join(" и ", Of.Select(o => o.Describe())),
            "any" => string.Join(" или ", Of.Select(o => o.Describe())),
            _ => Text ?? Type
        };
    }
}

public sealed class SkillRequirement
{
    public string Id { get; set; } = "";
    public int Rank { get; set; } = 1;
}

public sealed class SkillAdvancement
{
    public int Level { get; set; }
    public string Name { get; set; } = "";
    public string Effect { get; set; } = "";
}

public sealed class ExpertiseDef
{
    public string Name { get; set; } = "";
    public string Effect { get; set; } = "";
}

public sealed class SkillDef : RuleDef
{
    public List<string> Characteristics { get; set; } = [];
    public bool Advanced { get; set; }
    public bool Specialized { get; set; }
    public List<ExpertiseDef> Expertise { get; set; } = [];
    public List<SkillAdvancement> Advancements { get; set; } = [];
    /// <summary>
    /// Специализации из книги: «Запретное знание (Выберите одно)» — игрок
    /// выбирает одно из перечисленных направлений.
    /// </summary>
    public List<string> Specializations { get; set; } = [];

    public string? ParentId { get; set; }
}

public sealed class TalentEffect
{
    /// <summary>passive | simple | one_time | special</summary>
    public string Trigger { get; set; } = "passive";
    public string Text { get; set; } = "";
}

public sealed class TalentDef : RuleDef
{
    public int Ranks { get; set; } = 1;
    public bool Advanced { get; set; }
    public List<string> Prerequisites { get; set; } = [];
    public List<RequirementDef> Requirements { get; set; } = [];
    public List<TalentEffect> Effects { get; set; } = [];
    /// <summary>
    /// Специализации из книги: «Связи (Выберите одни)» — игрок выбирает одно
    /// из перечисленных направлений.
    /// </summary>
    public List<string> Specializations { get; set; } = [];
}

public sealed class SkillAward
{
    public string Id { get; set; } = "";
    public int Points { get; set; }
}

public sealed class CharacteristicBonus
{
    public string Characteristic { get; set; } = "";
    public int Value { get; set; }
}

/// <summary>
/// Улучшение ранга из таблицы улучшений рода занятий.
/// В книге это строка вида «Атлетика+10 | 200 | Умение | Атлетика».
/// </summary>
public sealed class AdvanceOption
{
    /// <summary>Уникальный ключ внутри ранга: careerId#rank#index.</summary>
    public string Id { get; set; } = "";
    /// <summary>Название как напечатано в книге.</summary>
    public string Name { get; set; } = "";
    /// <summary>Умение | Талант | Характеристика | Особенность | Прочее.</summary>
    public string Type { get; set; } = "";
    /// <summary>Стоимость в очках опыта.</summary>
    public int Cost { get; set; }
    /// <summary>Требования дословно из книги.</summary>
    public string Requirements { get; set; } = "";
    /// <summary>Машинно-читаемые требования.</summary>
    public List<RequirementDef> RequirementList { get; set; } = [];

    public string? SkillId { get; set; }
    /// <summary>1 = +10, 2 = +20.</summary>
    public int SkillStep { get; set; }
    public string? TalentId { get; set; }
    public string? CharacteristicId { get; set; }
    /// <summary>Насколько поднимает характеристику (обычно 5).</summary>
    public int CharacteristicPoints { get; set; }
    /// <summary>Уровень улучшения 1..4: Простой / Средний / Отточенный / Предельный.</summary>
    public int CharacteristicLevel { get; set; }
    public string? FeatureId { get; set; }

    /// <summary>Можно ли брать несколько раз (в книге помечено «(×2)»).</summary>
    public bool Repeatable { get; set; }
    public int MaxRepeats { get; set; } = 1;
    /// <summary>Специализация, если талант/умение её требуют.</summary>
    public string? Specialization { get; set; }
    public string? Note { get; set; }

    public bool IsSkill => !string.IsNullOrEmpty(SkillId);
    public bool IsTalent => !string.IsNullOrEmpty(TalentId);
    public bool IsCharacteristic => !string.IsNullOrEmpty(CharacteristicId);
}

public sealed class CareerRankDef
{
    public int Level { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Нижняя/верхняя граница накопленного опыта для ранга (из таблицы 2-2).</summary>
    public int XpMin { get; set; }
    public int XpMax { get; set; }

    public List<AdvanceOption> Advances { get; set; } = [];
    /// <summary>Требования к рангу дословно.</summary>
    public string Requirements { get; set; } = "";
    public string Notes { get; set; } = "";
    /// <summary>true для альтернативных рангов негодяя (Чёрные рынки).</summary>
    public bool Alternate { get; set; }
}

/// <summary>
/// Альтернативный путь внутри рода занятий: «следопыт», «оператор дронов»,
/// «пилот боевого скафандра» у Тау. Такой путь заменяет обычную таблицу
/// улучшений начиная с ранга MinRank.
/// </summary>
public sealed class CareerPathDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>С какого ранга путь заменяет обычные улучшения.</summary>
    public int MinRank { get; set; } = 2;
    /// <summary>Сколько опыта нужно на этот ранг.</summary>
    public int MinXp { get; set; }
    /// <summary>Дословно из книги: «ранг 2 или выше (7 000 ОО)».</summary>
    public string ReplacesRank { get; set; } = "";
    /// <summary>Требования дословно из книги.</summary>
    public string Requirements { get; set; } = "";
    /// <summary>Род занятий, к которому путь относится.</summary>
    public string CareerRequired { get; set; } = "";
    public List<AdvanceOption> Advances { get; set; } = [];
}

public sealed class CareerDef : RuleDef
{
    public string StartingRank { get; set; } = "";
    public string Overview { get; set; } = "";
    public string RankRequirements { get; set; } = "";
    public List<string> StartingEquipment { get; set; } = [];
    /// <summary>Начальные умения, выдаваемые при создании персонажа.</summary>
    public List<string> StartingSkills { get; set; } = [];
    public List<string> StartingTalents { get; set; } = [];
    public List<string> StartingFeatures { get; set; } = [];
    public string? ShipUpgrade { get; set; }
    public int? ShipHullPoints { get; set; }
    public List<CareerRankDef> Ranks { get; set; } = [];
    /// <summary>true для альтернативных рангов (негодяи, кронуские роды занятий).</summary>
    public bool AlternateRanks { get; set; }

    /// <summary>
    /// true, если альтернативный ранг подходит любому роду занятий
    /// («Аугмент», «Ксенограф», «Архиеретех» и подобные).
    /// </summary>
    public bool AnyBaseCareer { get; set; }

    /// <summary>
    /// Роды занятий, которым альтернативный ранг НЕ подходит («любой, кроме
    /// орка и круута»). Пусто — подходит любой.
    /// </summary>
    public List<string> ExcludedBaseCareers { get; set; } = [];

    /// <summary>true, если подходят только человеческие роды занятий.</summary>
    public bool HumanBaseCareerOnly { get; set; }

    /// <summary>
    /// Альтернативные пути развития внутри рода занятий (у Тау: следопыт,
    /// оператор дронов, пилот боевого скафандра). Такой путь заменяет обычную
    /// таблицу улучшений начиная с указанного ранга.
    /// </summary>
    public List<CareerPathDef> Paths { get; set; } = [];

    /// <summary>
    /// Таблица улучшений характеристик для этого рода занятий:
    /// characteristicId → цены 4 уровней (Простой/Средний/Отточенный/Предельный), накопительные.
    /// </summary>
    public Dictionary<string, int[]> CharacteristicAdvancement { get; set; } = [];

    /// <summary>Названия уровней улучшения: Простой, Средний, Отточенный, Предельный.</summary>
    public List<string> AdvancementLevelNames { get; set; } = ["Простой", "Средний", "Отточенный", "Предельный"];

    public string? Epigraph { get; set; }
    public string? StartingSkillsVerbatim { get; set; }
    public string? StartingTalentsVerbatim { get; set; }
    public List<NamedOptionDef> SpecialAbilities { get; set; } = [];
}

public static class ItemCategory
{
    public const string RangedWeapon = "ranged_weapon";
    public const string Launcher = "launcher";
    public const string Grenade = "grenade_rocket";
    public const string MeleeWeapon = "melee_weapon";
    public const string ChainWeapon = "chain_weapon";
    public const string PowerWeapon = "power_weapon";
    public const string ForceWeapon = "force_weapon";
    public const string Armor = "armor";
    public const string ArmorUpgrade = "armor_upgrade";
    public const string Clothing = "clothing";
    public const string Personal = "personal";
    public const string Consumable = "consumable";
    public const string Tool = "tool";
    public const string Ammo = "ammo";
    public const string WeaponUpgrade = "weapon_upgrade";
    public const string Augmetic = "augmetic";
    public const string Cybernetics = "cybernetics";
    public const string General = "general_gear";
    public const string ShipWeapon = "ship_weapon";
    public const string ShipGeneral = "ship_general";
    public const string Drug = "drug";

    public static string Ru(string c) => c switch
    {
        RangedWeapon => "Стрелковое оружие",
        Launcher => "Пусковое оружие",
        Grenade => "Гранаты и ракеты",
        MeleeWeapon => "Холодное оружие",
        ChainWeapon => "Цепное оружие",
        PowerWeapon => "Силовое оружие",
        ForceWeapon => "Психосиловое оружие",
        Armor => "Броня",
        ArmorUpgrade => "Улучшения брони",
        Clothing => "Одежда и амуниция",
        Personal => "Личные вещи",
        Consumable => "Препараты и вещества",
        Tool => "Инструменты",
        Ammo => "Боеприпасы",
        WeaponUpgrade => "Улучшения оружия",
        Augmetic => "Аугметика",
        Cybernetics => "Бионика и имплантаты",
        General => "Снаряжение",
        ShipWeapon => "Корабельное оружие",
        ShipGeneral => "Корабельное оборудование",
        Drug => "Наркотики и яды",
        _ => c
    };
}

public sealed class ItemDef : RuleDef
{
    // Поля главы «Арсенал» читаем мягко: в паке бронебойность и ёмкость
    // магазина приходят числами, а модель описывает их строками. Без
    // LenientStringConverter весь 05-items.json не читался и арсенал был пуст.
    public string Category { get; set; } = ItemCategory.General;
    public string WeaponClass { get; set; } = "";
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringConverter))]
    public string Cost { get; set; } = "";
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringConverter))]
    public string Availability { get; set; } = "";
    public int Encumbrance { get; set; }
    public int Protection { get; set; }
    public int Melee { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringConverter))]
    public string Range { get; set; } = "";
    public int StrengthBonus { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringListConverter))]
    public List<string> Traits { get; set; } = [];
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringListConverter))]
    public List<string> SpecialRules { get; set; } = [];
    public bool IsUpgrade { get; set; }

    // ── Характеристики из таблиц главы «Арсенал» ──
    // Раньше эти поля были в JSON пака, но отсутствовали в модели, и в лист
    // попадали только вес, защита и дальность — половина характеристик терялась.
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringConverter))]
    public string Damage { get; set; } = "";
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringConverter))]
    public string Penetration { get; set; } = "";
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringConverter))]
    public string Magazine { get; set; } = "";
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringConverter))]
    public string Reload { get; set; } = "";
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringConverter))]
    public string FireMode { get; set; } = "";
    /// <summary>Вес в свободной форме («4 кг», «1–2 кг») — у части предметов он дробный.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringConverter))]
    public string Weight { get; set; } = "";
    /// <summary>Примечание к предмету из книги.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringConverter))]
    public string Note { get; set; } = "";
    /// <summary>Эффект — для аугметики, препаратов и бионических систем.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringConverter))]
    public string Effect { get; set; } = "";
    /// <summary>Место установки — для кибернетических улучшений.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(Data.LenientStringConverter))]
    public string Location { get; set; } = "";

    /// <summary>Бонусы к характеристикам/прочим параметрам от предмета (например, аугметики).</summary>
    public Dictionary<string, int> Modifiers { get; set; } = [];

    /// <summary>Автоматически применять модификаторы при надевании/экипировке.</summary>
    public bool AutoModifiers { get; set; } = true;

    public static string WeaponClassRu(string c) => c switch
    {
        "bolter" => "Болтерное",
        "laser" => "Лазерное",
        "stubber" => "Стабберное",
        "melta" => "Мельта",
        "plasma" => "Плазменное",
        "flame" => "Зажигательное",
        "primitive" => "Примитивное",
        "exotic" => "Экзотическое",
        "psychic" => "Психосиловое",
        "impulse" => "Импульсное",
        "ionic" => "Ионное",
        "rail" => "Рельсовое",
        "power_weapon" => "Силовое (Сила)",
        "force_weapon" => "Силовое (Сила воли)",
        "" => "",
        _ => c
    };
}

/// <summary>Верхний раздел снаряжения — как в оглавлении главы «Арсенал».</summary>
public sealed class ItemSectionDef
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Страница книги, откуда взят раздел.</summary>
    public string Page { get; set; } = "";
    public int Order { get; set; }
}

/// <summary>
/// Подраздел снаряжения, повторяющий оглавление главы «Арсенал»
/// (стр. 109–151 базовой книги). По нему предметы раскладываются в интерфейсе.
/// </summary>
public sealed class ItemGroupDef
{
    public string Id { get; set; } = "";
    /// <summary>id верхнего раздела из <see cref="ItemSectionDef"/>.</summary>
    public string SectionId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Page { get; set; } = "";
    public int Order { get; set; }

    /// <summary>Категории предметов, попадающих в подраздел.</summary>
    public List<string> Categories { get; set; } = [];
    /// <summary>Если задано — подраздел берёт только предметы этих классов оружия.</summary>
    public List<string> WeaponClasses { get; set; } = [];
    /// <summary>true для необычных боеприпасов (стр. 137).</summary>
    public bool Uncommon { get; set; }
    /// <summary>true для бионических заменителей частей тела (стр. 147).</summary>
    public bool LimbOnly { get; set; }
}

public sealed class AugmeticDef : RuleDef
{
    public string Cost { get; set; } = "";
    public string Availability { get; set; } = "";
    public string Location { get; set; } = "";
    public string Effect { get; set; } = "";
}

public sealed class DrugDef : RuleDef
{
    public string Cost { get; set; } = "";
    public string Effect { get; set; } = "";
}

public sealed class NamedOptionDef : RuleDef
{
    public string Effect { get; set; } = "";
    public string Prerequisite { get; set; } = "";
    public List<string> Grants { get; set; } = [];
    public string Requirements { get; set; } = "";

    /// <summary>
    /// Варианты раздела — как в «Сквозь шторм»: у уготованной судьбы, пути
    /// в пустоту, испытания, устремления и наследия есть по три варианта
    /// со своей ценой в очках опыта и своими бонусами.
    /// </summary>
    public List<PathVariantDef> Variants { get; set; } = [];

    /// <summary>
    /// Какие варианты раздела этот вариант заменяет («Вместо: …» в книге).
    /// Такие варианты нельзя выбрать вместе с ним.
    /// </summary>
    public List<string> Replaces { get; set; } = [];

    /// <summary>
    /// Начальное число ранений по этому варианту пути («Изначальное количество
    /// ран: … 2 × бонус Выносливости + 1к5+1»).
    /// </summary>
    public StartingWoundsDef? StartingWounds { get; set; }

    /// <summary>Начальные очки судьбы по этому варианту пути (бросок 1к10).</summary>
    public StartingFateDef? StartingFate { get; set; }
}

/// <summary>
/// Вариант раздела предыстории («Сквозь шторм»): своё название, своя цена
/// в очках опыта, свой текст и свои игромеханические бонусы.
/// </summary>
public sealed class PathVariantDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Цена варианта в очках опыта («Цена: 100 ОО»).</summary>
    public int CostXp { get; set; }
    public string Description { get; set; } = "";
    /// <summary>Бонусы варианта — как в книге («Эффект: вы получаете…»).</summary>
    public string Effect { get; set; } = "";
}

public sealed class IncomeRankDef
{
    /// <summary>Результат броска 1к10 по таблице 1-5: «1», «2-3», «4-7», «8-9», «10».</summary>
    public string Roll { get; set; } = "";
    public int Rank { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Уровень дохода группы. В RT2 это одно число — им измеряется могущество династии.</summary>
    public int Income { get; set; }
    /// <summary>Очки корабля — бюджет конструирования судна (глава VIII).</summary>
    public int ShipPoints { get; set; }
    public string HullPoints { get; set; } = "";
    public string PerCrew { get; set; } = "";
    public string Notes { get; set; } = "";
}

/// <summary>Звёздный корабль из главы VIII: корпус и его цена в очках корабля.</summary>
public sealed class ShipHullDef
{
    public string Id { get; set; } = "";
    public string Class { get; set; } = "";
    public string Name { get; set; } = "";
    public string Crew { get; set; } = "";
    public int HullStrength { get; set; }
    public int Armour { get; set; }
    public int Space { get; set; }
    /// <summary>Цена корпуса в очках корабля.</summary>
    public int Cost { get; set; }
    public string Source { get; set; } = "core";
}

public sealed class WoundDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Effect { get; set; } = "";
    public string Healing { get; set; } = "";
}

public sealed class XpLevelDef
{
    public int Level { get; set; }
    public int Xp { get; set; }
    public string Note { get; set; } = "";
}

public sealed class SpeciesDef : RuleDef
{
    public string Book { get; set; } = "";
    public string Lang { get; set; } = "ru";
    public string Characteristics { get; set; } = "";
    public Dictionary<string, int> Bonus { get; set; } = [];
    public List<string> Talents { get; set; } = [];
    public List<string> Skills { get; set; } = [];
    public List<string> StartingEquipment { get; set; } = [];
    public List<string> Careers { get; set; } = [];
    public List<string> SpecialRules { get; set; } = [];
    public List<ElitePackageDef> ElitePackages { get; set; } = [];
    public bool Tau { get; set; }

    /// <summary>
    /// Черты вида — как в книге («Starting Traits»). Например, у долговязых:
    /// «Биология долговязых», «Неимперский», «Не совсем человек», «Размер (Hulking)».
    /// </summary>
    public List<string> Traits { get; set; } = [];

    /// <summary>
    /// Начальное число ранений (порог ранений) — формула вида. У человека
    /// порог = бонус Выносливости × 2, у абхуманов — своя формула с броском.
    /// </summary>
    public StartingWoundsDef? StartingWounds { get; set; }

    /// <summary>
    /// Начальные очки судьбы вида (0 — не заданы: у тау их определяет ведущий).
    /// </summary>
    public int StartingFatePoints { get; set; }

    /// <summary>
    /// Начальные очки судьбы по броску (у круутов и орков число определяется
    /// броском 1к10 — так в «Сквозь шторм»).
    /// </summary>
    public StartingFateDef? StartingFate { get; set; }

    /// <summary>
    /// Размер персонажа этой расы (0 — размер по умолчанию, средний).
    /// Ставится автоматически при выборе вида: у долговязых и скалидов в
    /// «Руководстве по абхуманам» напечатана черта «Размер (Hulking)»,
    /// у крысолов — «Размер (Scrawny)».
    /// </summary>
    public int Size { get; set; }
    /// <summary>Пояснение к размеру — как в книге.</summary>
    public string SizeNote { get; set; } = "";
    /// <summary>
    /// Постоянная часть броска характеристик для этой расы:
    /// id характеристики → «2к10+N». У людей 25, у тау — от 20 до 30.
    /// </summary>
    public Dictionary<string, int> CharacteristicBase { get; set; } = [];
}

public sealed class ElitePackageDef
{
    public string Name { get; set; } = "";
    public string Effect { get; set; } = "";
}

/// <summary>
/// Формула начального числа ранений вида: «1к5 + 2 × бонус Выносливости».
/// У оgryна в формуле стоит «2 × бонус Выносливости без правок вида» —
/// это и есть <see cref="Unmodified"/>. У круутов и орков — свои формулы
/// из «Сквозь шторм», у ксеносов из базовой книги — готовое число ранений.
/// </summary>
public sealed class StartingWoundsDef
{
    /// <summary>Постоянная часть формулы (например +3 у оgryна).</summary>
    public int Const { get; set; }
    /// <summary>Число граней кости (5 — бросок 1к5).</summary>
    public int Dice { get; set; }
    /// <summary>true — в формуле есть «2 × бонус Выносливости».</summary>
    public bool UseBonus { get; set; } = true;
    /// <summary>true — бонус Выносливости берётся без правок вида («Unmodified»).</summary>
    public bool Unmodified { get; set; }
    /// <summary>Готовое число ранений (если больше нуля — формула не используется).</summary>
    public int Fixed { get; set; }
    /// <summary>Формула из книги — для показа игроку.</summary>
    public string Text { get; set; } = "";
}

/// <summary>
/// Начальные очки судьбы вида. У человека их три, у абхуманов — один или два,
/// у круутов и орков число определяется броском 1к10 («на 1-5 … на 6-10 …»).
/// </summary>
public sealed class StartingFateDef
{
    /// <summary>Диапазоны броска: «с 1 по 5 — два очка судьбы, с 6 по 10 — три».</summary>
    public List<FateRangeDef> Ranges { get; set; } = [];
    /// <summary>Число граней кости (10).</summary>
    public int Dice { get; set; } = 10;
    /// <summary>Правило из книги — для показа игроку.</summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Сколько очков судьбы выпадает при броске. В книге у некоторых миров
    /// («Аристократия») между диапазонами есть пропуск («на 1-3 — два, на
    /// 6-9 — три»), поэтому берём ближайший нижний диапазон: значения,
    /// прямо названные в книге, не меняются.
    /// </summary>
    public int PointsFor(int roll)
    {
        var found = 0;
        foreach (var r in Ranges)
            if (roll >= r.From) found = r.Points;
        return found;
    }
}

/// <summary>Диапазон броска и число очков судьбы за него.</summary>
public sealed class FateRangeDef
{
    public int From { get; set; }
    public int To { get; set; }
    public int Points { get; set; }
}

public sealed class CasteDef : RuleDef
{
    public List<string> Talents { get; set; } = [];
    public List<string> Skills { get; set; } = [];
    public List<string> StartingEquipment { get; set; } = [];
    public List<string> SpecialRules { get; set; } = [];
}

public sealed class CombatSuitDef : RuleDef
{
    public string Characteristics { get; set; } = "";
    public string Protection { get; set; } = "";
}

public sealed class TauSystemDef : RuleDef
{
    public string Kind { get; set; } = "";
}