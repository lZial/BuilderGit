using System.Text.Json.Serialization;

namespace RogueTrader.Core.Models;

/// <summary>
/// Психический приём (глава VI «Психические приёмы»).
///
/// В Rogue Trader приёмы НЕ имеют уровней 1–5 и не покупаются за кубики:
/// они разбиты на дисциплины и древа приёмов, а «цена» каждого указана
/// в очках опыта (100 / 200 / 300 / 400 / 500). Уровни 1–5 и шкала отчаяния —
/// механики Dark Heresy, в RT их нет.
/// </summary>
public sealed class PsyAbilityDef : RuleDef
{
    public string NameEn { get; set; } = "";
    /// <summary>Дисциплина: Телепатия, Прорицания, Телекинез, Теософамия.</summary>
    public string Discipline { get; set; } = "";
    /// <summary>Древо приёмов внутри дисциплины.</summary>
    public string Tree { get; set; } = "";
    /// <summary>Цена в очках опыта; null — базовый приём дисциплины.</summary>
    public int? Cost { get; set; }
    /// <summary>Стоимость действия: ПД, ЧД, СД, полное, свободное, ЧЛ.</summary>
    [JsonConverter(typeof(Data.LenientStringConverter))]
    public string Action { get; set; } = "";
    /// <summary>«да» / «нет» — можно ли поддерживать приём.</summary>
    [JsonConverter(typeof(Data.LenientStringConverter))]
    public string Sustain { get; set; } = "";
    [JsonConverter(typeof(Data.LenientStringConverter))]
    public string Range { get; set; } = "";
    /// <summary>Какой тест требуется для применения.</summary>
    [JsonConverter(typeof(Data.LenientStringConverter))]
    public string Test { get; set; } = "";
    [JsonConverter(typeof(Data.LenientStringConverter))]
    public string Requirements { get; set; } = "";
    [JsonConverter(typeof(Data.LenientStringConverter))]
    public string Effect { get; set; } = "";
    [JsonConverter(typeof(Data.LenientStringConverter))]
    public string Notes { get; set; } = "";

    public string CostText => Cost is null ? "базовый приём дисциплины" : $"{Cost} ОО";
    public bool IsBase => Cost is null;
}

/// <summary>
/// Сила навигатора (глава VII «Силы навигаторов» и дополнение «Сквозь шторм»).
///
/// У каждой силы три уровня владения — Послушник, Адепт, Мастер; бонус за них
/// +0 / +10 / +20. Навигаторы не бросают тесты сотворения психосилы и не могут
/// вызвать психический феномен.
/// </summary>
public sealed class NavigatorPowerDef : RuleDef
{
    public string NameEn { get; set; } = "";
    [JsonConverter(typeof(Data.LenientStringConverter))]
    public string Test { get; set; } = "";
    [JsonConverter(typeof(Data.LenientStringConverter))]
    public string Effect { get; set; } = "";
    [JsonConverter(typeof(Data.LenientStringConverter))]
    public string Notes { get; set; } = "";
}

/// <summary>Три уровня владения силой навигатора.</summary>
public static class NavigatorProficiency
{
    public const string Novice = "novice";
    public const string Adept = "adept";
    public const string Master = "master";

    public static readonly (string Id, string Title, int Bonus)[] All =
    [
        (Novice, "Послушник", 0),
        (Adept, "Адепт", 10),
        (Master, "Мастер", 20),
    ];

    public static string Title(string id) =>
        All.FirstOrDefault(a => a.Id == id).Title ?? "Послушник";

    public static int Bonus(string id) =>
        All.FirstOrDefault(a => a.Id == id).Bonus;

    public static string Next(string id) => id switch
    {
        Novice => Adept,
        Adept => Master,
        _ => Master,
    };

    public static string Prev(string id) => id switch
    {
        Master => Adept,
        Adept => Novice,
        _ => Novice,
    };

    public static bool CanLevelUp(string id) => id != Master;
    public static bool CanLevelDown(string id) => id != Novice;
}
