namespace RogueTrader.Core.Models;

/// <summary>
/// Качество оружия по базовой книге (блок «Качество оружия», стр. 114):
/// «Все перечисленное ниже оружие – среднего качества», а для иных уровней
/// качества применяются описанные изменения. Правила для стрелкового и
/// холодного оружия различаются.
/// </summary>
public sealed class WeaponQualityDef
{
    /// <summary>Идентификатор: low | standard | good | high.</summary>
    public string Id { get; set; } = "";
    /// <summary>Название качества: Низкое, Среднее, Хорошее, Высшее.</summary>
    public string Name { get; set; } = "";
    /// <summary>Правило качества — как в книге, общей частью.</summary>
    public string Effect { get; set; } = "";
    /// <summary>Что добавляется стрелковому оружию (свойства).</summary>
    public List<string> RangedTraits { get; set; } = [];
    /// <summary>Что добавляется холодному оружию (свойства).</summary>
    public List<string> MeleeTraits { get; set; } = [];
    /// <summary>Бонус к тестам атаки для холодного оружия (+5, +10).</summary>
    public int MeleeAttackBonus { get; set; }
    /// <summary>Штраф к тестам атаки и парирования для холодного оружия (−10).</summary>
    public int MeleeAttackPenalty { get; set; }
    /// <summary>Бонус к наносимому урону для холодного оружия (+1 у высшего качества).</summary>
    public int MeleeDamageBonus { get; set; }
}

public static class WeaponQualities
{
    /// <summary>Уровни качества по порядку — от худшего к лучшему.</summary>
    public static readonly List<WeaponQualityDef> All =
    [
        new WeaponQualityDef
        {
            Id = "low",
            Name = "Низкое",
            Effect = "Дешёвое и склонное к осечкам оружие.",
            RangedTraits = ["Ненадёжное"],
            MeleeAttackPenalty = 10,
        },
        new WeaponQualityDef
        {
            Id = "standard",
            Name = "Среднее",
            Effect = "Оружие среднего качества — как указано в таблице, без изменений.",
        },
        new WeaponQualityDef
        {
            Id = "good",
            Name = "Хорошее",
            Effect = "Оружие качественной конструкции, что превосходит обычное надёжностью.",
            RangedTraits = ["Надёжное"],
            MeleeAttackBonus = 5,
        },
        new WeaponQualityDef
        {
            Id = "high",
            Name = "Высшее",
            Effect = "Настоящий шедевр, созданный лучшими мастерами столетия назад.",
            MeleeAttackBonus = 10,
            MeleeDamageBonus = 1,
        },
    ];

    /// <summary>Качество по названию (как в тексте снаряжения) или по id.</summary>
    public static WeaponQualityDef? ByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var n = name.Trim().ToLowerInvariant();
        return All.FirstOrDefault(q => q.Name.ToLowerInvariant() == n || q.Id == n);
    }

    /// <summary>Качество по умолчанию — среднее (так в книге: «всё перечисленное оружие — среднего качества»).</summary>
    public static WeaponQualityDef Default => All[1];

    /// <summary>Разбор качества из свободного текста: «высшего качества» → Высшее.</summary>
    public static WeaponQualityDef? FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.ToLowerInvariant();
        if (t.Contains("высшего качества")) return ByName("high");
        if (t.Contains("высокого качества")) return ByName("high");
        if (t.Contains("хорошего качества")) return ByName("good");
        if (t.Contains("среднего качества")) return ByName("standard");
        if (t.Contains("низкого качества")) return ByName("low");
        return null;
    }
}
