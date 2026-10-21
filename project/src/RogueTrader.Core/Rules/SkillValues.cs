using RogueTrader.Core.Models;

namespace RogueTrader.Core.Rules;

/// <summary>
/// Правила листа умений (базовая книга, глава II): вклад характеристики в
/// умение и итог умения на листе.
///
/// Изученное умение даёт +10, а если бонус характеристики +3 и выше — +20.
/// Неизученное базовое умение применяется по половине характеристики
/// (округлять вниз): половина 30 и больше — 20, иначе 10.
/// Продвинутое неизученное умение не применимо вовсе.
///
/// Правило живёт в ядре, чтобы лист умений в окне, PDF и Markdown
/// показывали одно и то же число.
/// </summary>
public static class SkillValues
{
    /// <summary>Вклад одной характеристики в умение.</summary>
    public static int CharacteristicValue(SkillDef? def, SkillEntry? entry, string characteristicId,
        int characteristicTotal, int characteristicBonus)
    {
        if (def is null || !def.Characteristics.Contains(characteristicId)) return 0;
        // Изученное умение: +10, при бонусе +3 и выше — +20.
        if (entry is not null) return characteristicBonus >= 3 ? 20 : 10;
        // Продвинутое неизученное умение применить нельзя.
        if (def.Advanced) return 0;
        // Базовое неизученное умение — по половине характеристики.
        var half = characteristicTotal / 2;
        return half >= 30 ? 20 : 10;
    }

    /// <summary>
    /// Вклад характеристики в умение.
    ///
    /// Умение проверяется ОДНОЙ характеристикой — так в книге: «Бдительность
    /// (Вос)», «Акробатика (Лов)». Если в данных (например, в хоумбрю)
    /// указано несколько, берётся лучшая: игрок сам выбирает, какой тест
    /// выгоднее. Складывать вклады нельзя — в лист попадала удвоенная сумма.
    /// </summary>
    public static int CharacteristicContribution(SkillDef? def, SkillEntry? entry, DerivedStats d)
    {
        if (def is null || def.Characteristics.Count == 0) return 0;
        var best = 0;
        foreach (var id in def.Characteristics.Distinct())
        {
            var cv = d.Get(id);
            if (cv is null) continue;
            best = Math.Max(best, CharacteristicValue(def, entry, id, cv.Total, cv.CharBonus));
        }
        return best;
    }

    /// <summary>
    /// Итог умения на листе: база + вклад характеристик + улучшения за опыт
    /// + «прочее» + правки мастера. Неизученное умение — по половине
    /// характеристики, продвинутое — ноль.
    /// </summary>
    public static int Total(SkillDef? def, SkillEntry? entry, DerivedStats d)
    {
        if (def is null) return 0;
        if (entry is null)
            return def.Advanced ? 0 : CharacteristicContribution(def, null, d);
        return entry.Base
             + CharacteristicContribution(def, entry, d)
             + entry.Rank * 10
             + entry.Other
             + entry.Adjustment;
    }
}
