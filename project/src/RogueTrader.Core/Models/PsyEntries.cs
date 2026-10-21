namespace RogueTrader.Core.Models;

/// <summary>
/// Выученный психический приём. По книге приём либо базовый (цена не указана),
/// либо покупается за очки опыта — поэтому здесь важен счётчик «выучено».
/// </summary>
public sealed class PsyAbilityEntry
{
    public string AbilityId { get; set; } = "";
    /// <summary>origin: career | path | advance | gm.</summary>
    public string Origin { get; set; } = "gm";
    public string Note { get; set; } = "";

    public PsyAbilityEntry Clone() => new() { AbilityId = AbilityId, Origin = Origin, Note = Note };
}

/// <summary>Сила навигатора с уровнем владения: Послушник / Адепт / Мастер.</summary>
public sealed class NavigatorPowerEntry
{
    public string PowerId { get; set; } = "";
    public string Proficiency { get; set; } = NavigatorProficiency.Novice;
    public string Note { get; set; } = "";

    public NavigatorPowerEntry Clone() => new()
    {
        PowerId = PowerId, Proficiency = Proficiency, Note = Note,
    };
}
