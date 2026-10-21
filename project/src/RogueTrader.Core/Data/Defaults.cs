using RogueTrader.Core.Models;

namespace RogueTrader.Core.Data;

/// <summary>
/// Правила, которые в дата-паке может не оказаться (старые сборки),
/// но без них нельзя пройти шаг 5 создания персонажа.
/// Взяты дословно из базовой книги (Rogue Trader 2nd Edition).
/// </summary>
internal static class Defaults
{
    /// <summary>Таблица 1-5 «Начальный уровень дохода и очки корабля» (стр. 29).</summary>
    public static List<IncomeRankDef> IncomeTable15() =>
    [
        new()
        {
            Roll = "1", Income = 60, ShipPoints = 30,
            Name = "Хотя у династии нет могучего корабля, её богатства велики",
        },
        new()
        {
            Roll = "2-3", Income = 50, ShipPoints = 40,
            Name = "Славный корабль и немалый капитал обещают этой династии блестящее будущее",
        },
        new()
        {
            Roll = "4-7", Income = 40, ShipPoints = 50,
            Name = "Эта династия – новички на галактической арене, и к ним приковано внимание – и благосклонное, и не очень",
        },
        new()
        {
            Roll = "8-9", Income = 30, ShipPoints = 60,
            Name = "Династия находится в упадке, но ещё может воспрянуть",
        },
        new()
        {
            Roll = "10", Income = 20, ShipPoints = 70,
            Name = "Когда-то пламя величия этой династии полыхало ярко, но теперь от него остались лишь тлеющие уголья.",
        },
    ];

    /// <summary>Глава VIII «Звёздные корабли» (стр. 189–192): корпуса и их цена в очках корабля.</summary>
    public static List<ShipHullDef> ShipHulls() =>
    [
        new() { Id = "jericho", Class = "Паломнические суда", Name = "Паломничье судно типа «Иерихон»", Crew = "20 000", HullStrength = 50, Armour = 12, Space = 45, Cost = 20 },
        new() { Id = "trader", Class = "Торговые суда", Name = "Купец типа «Бродяга»", Crew = "18 000", HullStrength = 40, Armour = 13, Space = 40, Cost = 20 },
        new() { Id = "corsair", Class = "Рейдеры", Name = "Капер типа «Асироф»", Crew = "22 000", HullStrength = 32, Armour = 14, Space = 35, Cost = 30 },
        new() { Id = "raider", Class = "Рейдеры", Name = "Коммерческий рейдер типа «Грабитель»", Crew = "24 000", HullStrength = 30, Armour = 16, Space = 40, Cost = 35 },
        new() { Id = "sword", Class = "Фрегаты", Name = "Фрегат типа «Меч»", Crew = "26 000", HullStrength = 35, Armour = 18, Space = 40, Cost = 40 },
        new() { Id = "storm", Class = "Фрегаты", Name = "Ударный фрегат типа «Буря»", Crew = "30 500", HullStrength = 36, Armour = 19, Space = 42, Cost = 40 },
        new() { Id = "unwavering", Class = "Лёгкие крейсеры", Name = "Лёгкий крейсер типа «Неустрашимый»", Crew = "65 000", HullStrength = 60, Armour = 19, Space = 60, Cost = 55 },
        new() { Id = "lunar", Class = "Крейсеры", Name = "Крейсер типа «Лунный»", Crew = "95 000", HullStrength = 70, Armour = 20, Space = 75, Cost = 60 },
    ];
}