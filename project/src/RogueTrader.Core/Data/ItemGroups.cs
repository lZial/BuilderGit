using RogueTrader.Core.Models;

namespace RogueTrader.Core.Data;

/// <summary>
/// Раскладка снаряжения по главе «Арсенал» (глава V базовой книги, стр. 109–151)
/// и по главе «Звёздные корабли» (VIII, узлы — стр. 187).
///
/// Названия подразделов и номера страниц взяты из оглавления книги дословно,
/// чтобы список в программе читался так же, как глава в книге.
/// Категории из пака раскладываются по подразделам через правила ниже.
/// </summary>
public static class ItemGroups
{
    public const string SecWeapons = "weapons";
    public const string SecAmmo = "ammo";
    public const string SecArmor = "armor";
    public const string SecGear = "gear";
    public const string SecCyber = "cyber";
    public const string SecShip = "ship";

    // Подразделы-«универсалы»: в них попадает только то, что не разобрали выше.
    private const string CatchMelee = "melee_common";
    private const string CatchGear = "gear_general";

    public static List<ItemSectionDef> Sections() =>
    [
        new() { Id = SecWeapons, Title = "Оружие", Page = "113", Order = 1 },
        new() { Id = SecAmmo, Title = "Боеприпасы", Page = "135", Order = 2 },
        new() { Id = SecArmor, Title = "Доспехи", Page = "138", Order = 3 },
        new() { Id = SecGear, Title = "Снаряжение", Page = "140", Order = 4 },
        new() { Id = SecCyber, Title = "Кибернетические улучшения", Page = "147", Order = 5 },
        new() { Id = SecShip, Title = "Оборудование корабля", Page = "187", Order = 6 },
    ];

    public static List<ItemGroupDef> Defaults() =>
    [
        // ── Оружие (стр. 113) ──
        G("ranged_laser", SecWeapons, "Лазерное оружие", "116", 10,
            [ItemCategory.RangedWeapon], ["laser"]),
        G("ranged_stubber", SecWeapons, "Стабберное оружие", "117", 11,
            [ItemCategory.RangedWeapon], ["stubber"]),
        G("ranged_bolter", SecWeapons, "Болтерное оружие", "120", 12,
            [ItemCategory.RangedWeapon], ["bolter"]),
        G("ranged_melta", SecWeapons, "Мельта-оружие", "121", 13,
            [ItemCategory.RangedWeapon], ["melta"]),
        G("ranged_plasma", SecWeapons, "Плазменное оружие", "122", 14,
            [ItemCategory.RangedWeapon], ["plasma"]),
        G("ranged_flame", SecWeapons, "Зажигательное оружие", "123", 15,
            [ItemCategory.RangedWeapon], ["flame"]),
        G("ranged_primitive", SecWeapons, "Примитивное оружие (стрелковое)", "124", 16,
            [ItemCategory.RangedWeapon], ["primitive"]),
        G("ranged_exotic", SecWeapons, "Экзотическое стрелковое оружие", "127", 17,
            [ItemCategory.RangedWeapon], ["exotic", "psychic"]),
        G("launcher_grenade", SecWeapons, "Гранаты и ракеты", "125", 18,
            [ItemCategory.Grenade, ItemCategory.Launcher], []),
        G("melee_chain", SecWeapons, "Цепное оружие", "130", 20,
            [ItemCategory.ChainWeapon], []),
        G("melee_exotic", SecWeapons, "Экзотическое холодное оружие", "131", 21,
            [ItemCategory.MeleeWeapon], ["exotic", "psychic"]),
        G("melee_shock", SecWeapons, "Шоковое оружие", "132", 22,
            [ItemCategory.MeleeWeapon], ["force_weapon"]),
        G("melee_primitive", SecWeapons, "Примитивное оружие (холодное)", "132", 23,
            [ItemCategory.MeleeWeapon], ["primitive"]),
        // Сила: отдельная категория из пака и холодное оружие с признаком силы
        G("melee_power", SecWeapons, "Силовое оружие", "130", 24,
            [ItemCategory.PowerWeapon, ItemCategory.ForceWeapon, ItemCategory.MeleeWeapon], []),
        G(CatchMelee, SecWeapons, "Прочее холодное оружие", "130", 25,
            [ItemCategory.MeleeWeapon], []),
        G("weapon_upgrade", SecWeapons, "Улучшения оружия", "134", 26,
            [ItemCategory.WeaponUpgrade], []),

        // ── Боеприпасы (стр. 135 и 137) ──
        G("ammo_common", SecAmmo, "Боеприпасы", "135", 30,
            [ItemCategory.Ammo], []),
        G("ammo_uncommon", SecAmmo, "Необычные боеприпасы", "137", 31,
            [ItemCategory.Ammo], [], uncommon: true),

        // ── Доспехи (стр. 138) ──
        G("armor", SecArmor, "Доспехи", "138", 40,
            [ItemCategory.Armor], []),
        G("armor_upgrade", SecArmor, "Улучшения доспеха", "138", 41,
            [ItemCategory.ArmorUpgrade], []),

        // ── Снаряжение (стр. 140) ──
        G("clothing", SecGear, "Одежда и амуниция", "140", 50,
            [ItemCategory.Clothing, ItemCategory.Personal], []),
        G("consumable", SecGear, "Препараты и вещества", "142", 51,
            [ItemCategory.Consumable, ItemCategory.Drug], []),
        G("tool", SecGear, "Инструменты", "143", 52,
            [ItemCategory.Tool], []),
        G(CatchGear, SecGear, "Прочее снаряжение", "140", 53,
            [ItemCategory.General], []),

        // ── Кибернетические улучшения (стр. 147 и 148) ──
        G("cyber_limb", SecCyber, "Бионические заменители частей тела", "147", 60,
            [ItemCategory.Augmetic], [], limbOnly: true),
        G("cyber_system", SecCyber, "Вживляемые системы", "148", 61,
            [ItemCategory.Augmetic], []),

        // ── Корабль, глава VIII ──
        G("ship_nodes", SecShip, "Узлы корабля", "187", 70,
            [ItemCategory.ShipGeneral, ItemCategory.ShipWeapon], []),
    ];

    private static ItemGroupDef G(string id, string section, string title, string page,
                                  int order, string[] categories, string[] weaponClasses,
                                  bool uncommon = false, bool limbOnly = false) =>
        new()
        {
            Id = id, SectionId = section, Title = title, Page = page, Order = order,
            Categories = [.. categories], WeaponClasses = [.. weaponClasses],
            Uncommon = uncommon, LimbOnly = limbOnly,
        };

    /// <summary>
    /// В какой подраздел попадает предмет. Сначала ищем точный подраздел,
    /// и только потом — «прочее», чтобы ни один предмет не потерялся.
    /// </summary>
    public static ItemGroupDef? Find(ItemDef def, IReadOnlyList<ItemGroupDef> groups)
    {
        foreach (var g in groups)
        {
            if (IsCatchAll(g.Id)) continue;
            if (Matches(def, g)) return g;
        }
        foreach (var g in groups)
        {
            if (!IsCatchAll(g.Id)) continue;
            if (g.Categories.Contains(def.Category)) return g;
        }
        return null;
    }

    private static bool IsCatchAll(string id) => id is CatchMelee or CatchGear;

    private static bool Matches(ItemDef def, ItemGroupDef g)
    {
        if (!g.Categories.Contains(def.Category)) return false;
        if (g.WeaponClasses.Count > 0 && !g.WeaponClasses.Contains(def.WeaponClass)) return false;
        if (g.Uncommon != IsUncommonAmmo(def)) return false;
        if (g.LimbOnly && !IsLimbAugmetic(def)) return false;
        return true;
    }

    /// <summary>Необычные боеприпасы идут отдельным подразделом (стр. 137).</summary>
    private static bool IsUncommonAmmo(ItemDef def)
    {
        if (def.Category != ItemCategory.Ammo) return false;
        return Has(def.Name, "необычн") || Has(def.Name, "экзотическ")
            || Has(def.Description, "необычн") || Has(def.Availability, "необычн");
    }

    /// <summary>
    /// Бионический заменитель ставится на место органа, а вживляемая система —
    /// на «любое место тела» или внутрь. Так они и разделены в книге.
    /// </summary>
    private static bool IsLimbAugmetic(ItemDef def)
    {
        if (def.Category != ItemCategory.Augmetic) return false;
        if (string.IsNullOrWhiteSpace(def.Location)) return false;
        if (Has(def.Location, "любом месте") || Has(def.Location, "любой части")) return false;
        if (Has(def.Location, "под кожей") || Has(def.Location, "вживляем")) return false;
        return true;
    }

    private static bool Has(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack)
        && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
