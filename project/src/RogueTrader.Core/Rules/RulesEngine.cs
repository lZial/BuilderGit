using RogueTrader.Core.Data;
using RogueTrader.Core.Models;

namespace RogueTrader.Core.Rules;

/// <summary>Кэш глобальных правил, чтобы модель не тянула GamePack.</summary>
public static class RulesLookup
{
    public static List<CharacteristicDef> Characteristics { get; set; } = [];
}

/// <summary>Результат проверки доступности.</summary>
public sealed class RequirementCheck
{
    public bool Ok { get; set; } = true;
    public List<string> Missing { get; set; } = [];
    public List<string> Met { get; set; } = [];

    public string Summary => Ok
        ? (Met.Count > 0 ? string.Join("; ", Met) : "Требования выполнены")
        : string.Join("; ", Missing);
}

public sealed class RulesOptions
{
    /// <summary>Разрешить брать что угодно без проверки требований (режим мастера).</summary>
    public bool FreeAllocation { get; set; }
    public bool ItemsApplyModifiers { get; set; } = true;
    /// <summary>Максимальный бонус умения по книге.</summary>
    public int MaxSkillBonus { get; set; } = 20;
    /// <summary>Стартовые «врождённые» очки опыта, которые нельзя тратить.</summary>
    public int StartingXp { get; set; } = 4500;
    /// <summary>Свободные очки опыта на создании персонажа (500 по книге).</summary>
    public int StartingSpendable { get; set; } = 500;
    /// <summary>Сколько очков улучшения характеристики можно купить всего (+20).</summary>
    public int MaxCharacteristicPoints { get; set; } = 20;
    /// <summary>Шаг покупки улучшения характеристики.</summary>
    public int CharacteristicStep { get; set; } = 5;
}

/// <summary>
/// Движок правил той системы, которая реально записана в приложенных книгах:
/// прогресс идёт через <b>улучшения рангов</b> (таблицы «Улучшения … на ранге N»),
/// ранг определяется накопленным опытом (таблица 2-2), характеристика улучшается
/// на +5 за четыре уровня (Простой / Средний / Отточенный / Предельный),
/// умение — максимум до +20.
/// </summary>
public sealed class RulesEngine
{
    private readonly GamePack _pack;
    private readonly RulesOptions _opt;

    public RulesEngine(GamePack pack, RulesOptions? options = null)
    {
        _pack = pack;
        _opt = options ?? new RulesOptions();
    }

    public RulesOptions Options => _opt;
    public GamePack Pack => _pack;

    // ═════════════════════ Ранги и опыт ═════════════════════

    /// <summary>
    /// Таблица 2-2 «Ранги»: ранг → [мин, макс] общих очков опыта.
    /// Ранг поднимается, когда общий опыт персонажа достигает минимума
    /// следующего ранга, поэтому верхняя граница нужна для показа, а не
    /// для расчёта. Обратите внимание: книга оставляет «дыру» между
    /// рангами 7 и 8 (29 001 – 29 999 не принадлежит ни одному рангу).
    /// </summary>
    public static readonly (int Rank, int Min, int Max)[] RankThresholds =
    [
        (1, 5000, 6900), (2, 7000, 9999), (3, 10000, 12999), (4, 13000, 16999),
        (5, 17000, 20999), (6, 21000, 24999), (7, 25000, 29000), (8, 30000, 34999),
    ];

    public static int RankFor(int totalXp)
    {
        var rank = 1;
        foreach (var (r, min, _) in RankThresholds)
            if (totalXp >= min) rank = r;
        return rank;
    }

    public static (int Min, int Max) RangeFor(int rank)
    {
        foreach (var (r, min, max) in RankThresholds)
            if (r == rank) return (min, max);
        return (0, 0);
    }

    /// <summary>Опыт, потраченный на купленные улучшения.</summary>
    public int XpFromAdvances(Character ch) => ch.Advances.Sum(a => a.Cost);

    /// <summary>
    /// Общий опыт персонажа: 4500 врождённых + 500 стартовых + начисленный в игре.
    /// Покупки улучшений на эту сумму не влияют — они списываются из свободного остатка.
    /// </summary>
    public int TotalXp(Character ch) => _opt.StartingXp + _opt.StartingSpendable + ch.XpEarned;

    /// <summary>Стоимость улучшения характеристики по таблице рода занятий (накопительная).</summary>
    public int CharacteristicAdvanceCost(string? careerId, string characteristicId, int level)
    {
        if (level is < 1 or > 4) return 0;
        var career = _pack.Career(careerId);
        if (career is not null && career.CharacteristicAdvancement.TryGetValue(characteristicId, out var costs))
            return level <= costs.Length ? costs[level - 1] : 0;
        // Запасной вариант: если таблицы для рода занятий нет — усреднённая цена по книге
        return level switch { 1 => 250, 2 => 500, 3 => 750, _ => 1000 };
    }

    /// <summary>Суммарная цена n уровней улучшения характеристики (накопительная).</summary>
    public int CharacteristicXpSpent(string? careerId, string characteristicId, int levels)
    {
        var sum = 0;
        for (var l = 1; l <= levels; l++) sum += CharacteristicAdvanceCost(careerId, characteristicId, l);
        return sum;
    }

    // ═════════════════════ Пересчёт ═════════════════════

    /// <summary>
    /// Длина одной из дистанций (половинная, полная, натиск, бег) по базовой книге:
    /// бонус Ловкости × коэффициент, но не меньше минимума из таблицы 9-30.
    /// </summary>
    private static int MovementFor(Core.Data.MovementRulesDef mr, int speedFactor, string kind)
    {
        var value = speedFactor * mr.Coefficient(kind);
        if (value < mr.Minimum(kind)) value = mr.Minimum(kind);
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Порог ранений персонажа. У человека — бонус Выносливости × 2;
    /// у видов с собственной формулой («Руководство по абхуманам») она
    /// учитывается по книге, вместе с броском 1к5 и правкой «без правок вида».
    /// Формула жизненного пути («Изначальное количество ран») действует, если
    /// у вида своей нет: у абхуманов путь происхождения не используется.
    /// </summary>
    private int WoundThreshold(Character ch, SpeciesDef? species, DerivedStats d)
    {
        var tou = d.Get("tou");
        var total = tou?.Total ?? 25;
        var sw = species?.StartingWounds ?? PathStartingOption(ch)?.StartingWounds;
        if (sw is null) return (tou?.CharBonus ?? 0) * 2;
        if (sw.Fixed > 0) return sw.Fixed;

        // «Unmodified Toughness Bonus» — бонус без правок вида (у оgryна +5 к Выносливости,
        // у орков — без «Сверхъестественной Выносливости»).
        var speciesTou = species?.Bonus.GetValueOrDefault("tou") ?? 0;
        var bonus = sw.Unmodified ? (total - speciesTou) / 10 : total / 10;
        var roll = sw.Dice > 0
            ? (species?.StartingWounds is not null ? ch.SpeciesWoundsRoll : ch.PathWoundsRoll)
            : 0;
        return sw.Const + roll + (sw.UseBonus ? bonus * 2 : 0);
    }

    /// <summary>
    /// Вариант жизненного пути, задающий начальные ранения и очки судьбы:
    /// в книге это первая строка таблицы жизненных путей — родной мир.
    /// </summary>
    private NamedOptionDef? PathStartingOption(Character ch)
    {
        foreach (var id in ch.HomeworldIds)
        {
            var o = _pack.Homeworlds.FirstOrDefault(x => x.Id == id);
            if (o is not null && (o.StartingWounds is not null || o.StartingFate is not null)) return o;
        }
        foreach (var id in ch.LegacyIds)
        {
            var o = _pack.Legacy.FirstOrDefault(x => x.Id == id);
            if (o is not null && (o.StartingWounds is not null || o.StartingFate is not null)) return o;
        }
        return null;
    }

    public DerivedStats Compute(Character ch)
    {
        var d = new DerivedStats();
        var career = _pack.Career(ch.CareerId);
        var species = _pack.SpeciesOf(ch.SpeciesId);
        var itemBonus = ItemBonuses(ch);

        foreach (var def in _pack.OrderedCharacteristics)
        {
            var entry = ch.FindCharacteristic(def.Id);
            var fromSpecies = species?.Bonus.GetValueOrDefault(def.Id) ?? 0;
            var fromItems = itemBonus.Characteristics.GetValueOrDefault(def.Id);
            var fromPath = entry?.PathPoints ?? 0;
            var cv = new CharacteristicValue
            {
                Id = def.Id,
                Name = def.Name,
                Abbrev = def.Abbrev,
                Column = def.Column,
                Base = entry?.Base ?? 25,
                AdvancePoints = Math.Clamp(entry?.AdvancePoints ?? 0, 0, 20),
                // «Прочее» — то, что заложено игрой: вид, снаряжение, жизненный путь.
                Other = fromSpecies + fromItems + fromPath,
                Adjustment = entry?.Adjustment ?? 0,
                Path = fromPath,
                FromSpecies = fromSpecies,
                FromItems = fromItems,
            };
            var levels = cv.AdvanceLevels;
            cv.NextAdvanceCost = levels < 4
                ? CharacteristicAdvanceCost(ch.CareerId, def.Id, levels + 1)
                : 0;
            cv.NextAdvanceName = levels < 4
                ? (career?.AdvancementLevelNames.ElementAtOrDefault(levels) ?? $"Уровень {levels + 1}")
                : "";
            d.Characteristics.Add(cv);
        }

        // Боевое состояние.
        // Число ранений (порог ранений): у человека — бонус Выносливости × 2.
        // У абхуманов своя формула («1к5 + 2 × бонус Выносливости»), а у
        // оgryна — «1к5 + 3 + 2 × бонус Выносливости без правок вида».
        d.WoundThreshold = WoundThreshold(ch, species, d);
        d.CurrentWounds = ch.Wounds;
        d.Size = ch.Size;

        // Скорость передвижения по базовой книге (стр. 251, таблица 9-30):
        // длина дистанций зависит от бонуса Ловкости, а размер этот бонус
        // сдвигает (таблица 14-2 «Размеры», колонка «Скорость»: БЛ−3 … БЛ+3).
        var mr = _pack.MovementRules;
        var dexBonus = d.Get("dex")?.CharBonus ?? 0;
        var speedFactor = dexBonus + mr.ModifierForSize(ch.Size);
        d.SpeedFactor = speedFactor;
        d.HalfMovement = MovementFor(mr, speedFactor, "half");
        d.FullMovement = MovementFor(mr, speedFactor, "full");
        d.ChargeMovement = MovementFor(mr, speedFactor, "charge");
        d.RunMovement = MovementFor(mr, speedFactor, "run");
        d.MovementFromItems = itemBonus.Move;
        d.FatePoints = ch.FatePoints;
        d.PsyRating = ch.PsyRating;
        d.Insanity = ch.Insanity;
        d.Corruption = ch.Corruption;

        // ── Уровень дохода и очки корабля (таблица 1-5, шаг 5 создания) ──
        var rollRow = ch.IncomeRoll > 0
            ? _pack.IncomeRanks.FirstOrDefault(r => RollMatches(r.Roll, ch.IncomeRoll))
            : null;
        d.IncomeFromRoll = rollRow?.Income ?? 0;
        d.ShipPointsFromRoll = rollRow?.ShipPoints ?? 0;
        d.IncomeRollRow = rollRow;
        d.IncomeLevel = Math.Max(0, d.IncomeFromRoll + ch.IncomeModifier + ch.IncomeBonus);
        d.ShipPoints = Math.Max(0, ch.ShipPoints > 0 ? ch.ShipPoints : d.ShipPointsFromRoll);
        var hull = _pack.ShipHulls.FirstOrDefault(h => h.Id == ch.ShipHullId);
        d.ShipHull = hull;
        d.ShipPointsSpent = hull?.Cost ?? 0;
        // «Все не потраченные очки корабля прибавляются к уровню дохода»
        d.ShipPointsLeft = Math.Max(0, d.ShipPoints - d.ShipPointsSpent);

        foreach (var e in ch.Items)
        {
            var it = _pack.ItemOf(e);
            if (it is null) continue;
            d.TotalEncumbrance += it.Encumbrance * e.Quantity;
            if (it.Category == ItemCategory.Armor)
            {
                d.ArmorProtection += it.Protection * e.Quantity;
                d.ArmorEncumbrance += it.Encumbrance * e.Quantity;
            }
        }

        // Опыт и ранг
        d.XpLocked = _opt.StartingXp;                       // 4500 — врождённые, повторно не тратятся
        d.XpSpendableTotal = _opt.StartingSpendable + ch.XpEarned;  // 500 при создании + заработанное
        d.XpSpentOnAdvances = XpFromAdvances(ch);
        d.XpSpentOnPathVariants = ch.PathVariantXp;
        d.XpSpentOnCharacteristics = ch.Characteristics.Sum(e =>
            CharacteristicXpSpent(ch.CareerId, e.CharacteristicId, e.AdvanceLevels));
        d.XpSpendableLeft = d.XpSpendableTotal - d.XpSpent;
        if (d.XpSpendableLeft < 0)
            d.Warnings.Add($"Свободный опыт ушёл в минус: не хватает {(-d.XpSpendableLeft)} ОО. "
                           + "Покупки сверх бюджета нужно отменить или добавить опыта.");
        if (d.XpSpendableLeft < 0)
            d.Warnings.Add($"Цена выбранных вариантов предыстории ({ch.PathVariantXp} ОО) превышает свободный "
                           + $"опыт ({d.XpSpendableTotal} ОО): не хватает {(-d.XpSpendableLeft)} ОО.");

        // Общий опыт — всё, что персонаж получил: 4500 врождённых (они уже
        // потрачены на стартовые улучшения рода занятий), 500 свободных на
        // создание и весь опыт, добавленный по ходу игры. Ранг считается
        // от этой суммы по таблице 2-2, а непотраченная часть остаётся
        // в «Свободном опыте» и доступна для улучшений.
        d.TotalXp = d.XpLocked + d.XpSpendableTotal;
        d.Rank = RankFor(d.TotalXp);
        var (min, max) = RangeFor(d.Rank);
        d.RankMin = min;
        d.RankMax = max;
        var next = RankThresholds.FirstOrDefault(x => x.Rank == d.Rank + 1);
        d.XpToNextRank = next.Min > 0 ? Math.Max(0, next.Min - d.TotalXp) : 0;

        d.Warnings.AddRange(ValidateCore(ch, d));
        return d;
    }

    private static bool RollMatches(string? rollSpec, int roll)
    {
        if (string.IsNullOrWhiteSpace(rollSpec)) return false;
        var parts = rollSpec.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;
        if (!int.TryParse(parts[0], out var from)) return false;
        // В таблице 1-5 диапазоны записаны как «4-7», это именно диапазон
        if (parts.Length >= 2 && int.TryParse(parts[1], out var to)) return roll >= from && roll <= to;
        return roll == from;
    }

    private (Dictionary<string, int> Characteristics, int Move) ItemBonuses(Character ch)
    {
        var chars = new Dictionary<string, int>();
        var move = 0;
        if (!_opt.ItemsApplyModifiers) return (chars, move);
        foreach (var e in ch.Items)
        {
            var it = _pack.ItemOf(e);
            if (it is null || !it.AutoModifiers) continue;
            foreach (var (k, v) in it.Modifiers)
            {
                var key = Normalize(k);
                if (key == "movement") move += v * e.Quantity;
                else chars[key] = chars.GetValueOrDefault(key) + v * e.Quantity;
            }
        }
        return (chars, move);
    }

    // ═════════════════════ Применение улучшений ═════════════════════

    public List<AdvanceOption> AdvancesFor(Character ch, int rank)
    {
        var career = _pack.Career(ch.CareerId);
        if (career is null) return [];

        // Альтернативный путь (следопыт, оператор дронов, пилот скафандра)
        // заменяет обычную таблицу улучшений начиная со своего ранга.
        var path = PathFor(ch, career);
        if (path is not null && rank >= path.MinRank) return path.Advances;

        var def = career.Ranks.FirstOrDefault(r => r.Level == rank);
        return def?.Advances ?? [];
    }

    /// <summary>Выбранный альтернативный путь внутри рода занятий (если есть).</summary>
    public CareerPathDef? PathFor(Character ch, CareerDef? career = null)
    {
        career ??= _pack.Career(ch.CareerId);
        if (career is null || string.IsNullOrEmpty(ch.AlternateRankId)) return null;
        return career.Paths.FirstOrDefault(p => p.Id == ch.AlternateRankId);
    }

    public AdvanceOption? FindOption(Character ch, string optionId)
    {
        var parts = optionId.Split('#');
        if (parts.Length < 3) return null;
        var career = _pack.Career(parts[0]);
        if (career is null) return null;

        // «род занятий#путь#номер» — улучшения альтернативного пути
        if (!int.TryParse(parts[1], out var rank))
        {
            var path = career.Paths.FirstOrDefault(p => p.Id == parts[1]);
            if (path is null) return null;
            var pidx = int.TryParse(parts[2], out var pi) ? pi : -1;
            return path.Advances.ElementAtOrDefault(pidx);
        }

        var idx = int.TryParse(parts[2], out var x) ? x : -1;
        return career.Ranks.FirstOrDefault(rr => rr.Level == rank)?.Advances
            .ElementAtOrDefault(idx);
    }

    /// <summary>Можно ли купить это улучшение (требования + лимиты).</summary>
    public RequirementCheck CheckAdvance(Character ch, AdvanceOption opt) =>
        CheckAdvance(ch, opt, Compute(ch), checkRepeatLimit: true);

    /// <summary>
    /// Проверка с уже посчитанной сводкой. Обязательна для ValidateCore:
    /// иначе Compute → ValidateCore → CheckAdvance → Compute зациклится.
    /// </summary>
    /// <param name="checkRepeatLimit">
    /// false при проверке уже купленных улучшений — «уже куплено» это норма,
    /// а не нарушение.
    /// </param>
    public RequirementCheck CheckAdvance(Character ch, AdvanceOption opt, DerivedStats d, bool checkRepeatLimit = true)
    {
        var res = new RequirementCheck();
        if (_opt.FreeAllocation) { res.Met.Add("Режим мастера: проверка отключена"); return res; }

        if (checkRepeatLimit && ch.AdvanceCount(opt.Id) >= Math.Max(1, opt.MaxRepeats))
        {
            res.Ok = false;
            res.Missing.Add(opt.Repeatable
                ? $"Уже взят максимум ({opt.MaxRepeats})"
                : "Уже куплено");
        }

        if (checkRepeatLimit && opt.Cost > 0 && d.XpSpendableLeft < opt.Cost)
        {
            res.Ok = false;
            res.Missing.Add($"Не хватает свободного опыта: нужно {opt.Cost} ОО, доступно {d.XpSpendableLeft} ОО");
        }

        foreach (var r in opt.RequirementList) Evaluate(ch, r, d, res);

        // Требования, которые остались только текстом — пробуем разобрать
        if (!string.IsNullOrWhiteSpace(opt.Requirements) && opt.Requirements != "-")
            foreach (var part in opt.Requirements.Split(',', ';'))
            {
                var p = part.Trim();
                if (p.Length == 0) continue;
                var cid = Normalize(p.Split(' ')[0]);
                var cv = d.Get(cid);
                if (cv is null || !int.TryParse(new string(p.Where(char.IsDigit).ToArray()), out var need)) continue;
                if (cv.Total < need) { res.Ok = false; res.Missing.Add($"{cv.Name} ≥ {need} (сейчас {cv.Total})"); }
                else res.Met.Add($"{cv.Name} ≥ {need}");
            }

        // Умение должно быть изучено, чтобы брать его улучшение.
        // Бонус зашит в название опции («… +10», «… +20»): skillStep в данных
        // только помечает, что это не «просто выучить умение».
        if (opt.IsSkill && !string.IsNullOrEmpty(opt.SkillId))
        {
            var need = SkillBonusOf(opt.Name);
            var have = ch.FindSkill(opt.SkillId!)?.Rank ?? 0;
            if (have < need)
            {
                res.Ok = false;
                res.Missing.Add(need == 0
                    ? "Это умение ещё не изучено"
                    : $"Сначала доведите «{_pack.Skill(opt.SkillId)?.Name}» до {need * 10}");
            }
        }
        return res;
    }

    /// <summary>
    /// Какой бонус даёт улучшение умения: 0 — просто изучить, 1 — +10, 2 — +20.
    /// В книге это записано прямо в названии строки таблицы улучшений.
    /// </summary>
    public static int SkillBonusOf(string? optionName)
    {
        if (string.IsNullOrWhiteSpace(optionName)) return 0;
        if (optionName.Contains("+20", StringComparison.Ordinal)) return 2;
        if (optionName.Contains("+10", StringComparison.Ordinal)) return 1;
        return 0;
    }

    /// <summary>Покупка улучшения: применяет эффекты и добавляет его в список.</summary>
    public bool ApplyAdvance(Character ch, AdvanceOption opt, string? specialization = null)
    {
        if (ch.Advances.Any(a => a.OptionId == opt.Id)) return false;

        if (opt.IsSkill && !string.IsNullOrEmpty(opt.SkillId))
        {
            var e = ch.FindSkill(opt.SkillId!);
            if (e is null)
            {
                e = new SkillEntry { SkillId = opt.SkillId!, Origin = "career" };
                ch.Skills.Add(e);
            }
            // Бонус берём из названия опции: «+10» / «+20», иначе — просто изучено
            e.Rank = Math.Max(e.Rank, SkillBonusOf(opt.Name));
        }
        else if (opt.IsTalent && !string.IsNullOrEmpty(opt.TalentId))
        {
            var e = ch.FindTalent(opt.TalentId!);
            if (e is null) { e = new TalentEntry { TalentId = opt.TalentId!, Origin = "career" }; ch.Talents.Add(e); }
            e.Rank += 1;
        }
        else if (opt.IsCharacteristic && !string.IsNullOrEmpty(opt.CharacteristicId))
        {
            var e = ch.FindCharacteristic(opt.CharacteristicId);
            if (e is null)
            {
                e = new CharacteristicEntry { CharacteristicId = opt.CharacteristicId, Base = 25 };
                ch.Characteristics.Add(e);
            }
            e.AdvanceLevels = e.AdvanceLevels + 1;
            e.Origin = "career";
        }

        var rank = ParseRank(opt.Id);
        ch.Advances.Add(new AppliedAdvance
        {
            OptionId = opt.Id,
            CareerId = ch.CareerId ?? "",
            // У улучшений альтернативного пути ранга в ключе нет — записываем текущий.
            Rank = rank > 0 ? rank : Compute(ch).Rank,
            Cost = opt.Cost,
            Specialization = specialization,
        });
        return true;
    }

    /// <summary>Ранг из ключа улучшения; 0 — если это альтернативный путь.</summary>
    private static int ParseRank(string optionId)
    {
        var parts = optionId.Split('#');
        return parts.Length >= 2 && int.TryParse(parts[1], out var r) ? r : 0;
    }

    public bool RemoveAdvance(Character ch, string optionId)
    {
        var applied = ch.Advances.FirstOrDefault(a => a.OptionId == optionId);
        if (applied is null) return false;
        ch.Advances.Remove(applied);

        var opt = FindOption(ch, optionId);
        if (opt is null) return true;

        if (opt.IsTalent && !string.IsNullOrEmpty(opt.TalentId))
        {
            var e = ch.FindTalent(opt.TalentId!);
            if (e is not null) { e.Rank -= 1; if (e.Rank <= 0) ch.Talents.Remove(e); }
        }
        else if (opt.IsSkill && !string.IsNullOrEmpty(opt.SkillId))
        {
            var e = ch.FindSkill(opt.SkillId!);
            if (e is not null)
            {
                // Понижаем на ступень: откат «+20» оставляет «+10», откат «+10» — изучено.
                var stillHave = ch.Advances.Any(a => a.OptionId != optionId
                    && (FindOption(ch, a.OptionId)) is { IsSkill: true, SkillId: var sid } o
                    && sid == opt.SkillId && SkillBonusOf(o.Name) >= e.Rank);
                e.Rank = stillHave ? e.Rank : e.Rank - 1;
                if (e.Rank < 0) e.Rank = 0;
                if (e.Rank == 0 && ch.Advances.All(a =>
                        FindOption(ch, a.OptionId) is not { IsSkill: true, SkillId: var s2 } so || s2 != opt.SkillId))
                    ch.Skills.Remove(e);
            }
        }
        else if (opt.IsCharacteristic && !string.IsNullOrEmpty(opt.CharacteristicId))
        {
            var e = ch.FindCharacteristic(opt.CharacteristicId);
            if (e is not null) e.AdvanceLevels = Math.Max(0, e.AdvanceLevels - 1);
        }
        return true;
    }

    // ═════════════════════ Таланты: проверка требований ═════════════════════

    public RequirementCheck CheckTalent(Character ch, string talentId)
    {
        var t = _pack.Talent(talentId);
        var res = new RequirementCheck();
        if (t is null) { res.Ok = false; res.Missing.Add("Талант не найден в дата-паке."); return res; }
        var d = Compute(ch);
        foreach (var r in t.Requirements) Evaluate(ch, r, d, res);
        return res;
    }

    public RequirementCheck CheckSkill(Character ch, string skillId)
    {
        var s = _pack.Skill(skillId);
        var res = new RequirementCheck();
        if (s is null) { res.Ok = false; res.Missing.Add("Умение не найдено в дата-паке."); return res; }
        if (!string.IsNullOrWhiteSpace(s.ParentId))
        {
            var e = ch.FindSkill(s.ParentId);
            var name = _pack.Skill(s.ParentId)?.Name ?? s.ParentId;
            if (e is null || e.Rank <= 0) { res.Ok = false; res.Missing.Add($"Требуется умение «{name}»"); }
            else res.Met.Add($"Требуется умение «{name}»");
        }
        return res;
    }

    private void Evaluate(Character ch, RequirementDef r, DerivedStats d, RequirementCheck res)
    {
        switch (r.Type)
        {
            case "characteristic":
            {
                var cv = d.Get(Normalize(r.Characteristic ?? ""));
                if (cv is null) { res.Ok = false; res.Missing.Add($"Неизвестная характеристика «{r.Characteristic}»."); return; }
                if (cv.Total < r.Min) { res.Ok = false; res.Missing.Add($"{cv.Name} ≥ {r.Min} (сейчас {cv.Total})"); }
                else res.Met.Add($"{cv.Name} ≥ {r.Min} ({cv.Total})");
                return;
            }
            case "talent":
            {
                var label = string.Join(" / ", r.Talents.Select(id => _pack.Talent(id)?.Name ?? id));
                if (r.Talents.All(id => (ch.FindTalent(id)?.Rank ?? 0) <= 0))
                { res.Ok = false; res.Missing.Add($"Требуется талант: {label}"); }
                else res.Met.Add($"Требуется талант: {label}");
                return;
            }
            case "skill":
            {
                foreach (var s in r.Skills)
                {
                    var e = ch.FindSkill(s.Id);
                    var name = _pack.Skill(s.Id)?.Name ?? s.Id;
                    if (e is null || e.Rank < s.Rank) { res.Ok = false; res.Missing.Add($"{name} +{s.Rank * 10} (сейчас +{(e?.Rank ?? 0) * 10})"); }
                    else res.Met.Add($"{name} +{s.Rank * 10}");
                }
                return;
            }
            case "species":
            {
                if (!r.Species.Contains(ch.SpeciesId))
                { res.Ok = false; res.Missing.Add($"Требуется вид: {string.Join(" / ", r.Species.Select(s => _pack.SpeciesOf(s)?.Name ?? s))}"); }
                else res.Met.Add($"Вид: {_pack.SpeciesOf(ch.SpeciesId)?.Name ?? ch.SpeciesId}");
                return;
            }
            case "career":
            {
                if (ch.CareerId != r.Career) { res.Ok = false; res.Missing.Add($"Требуется род занятий: {r.Career}"); }
                else res.Met.Add($"Род занятий: {_pack.Career(r.Career)?.Name ?? r.Career}");
                return;
            }
            case "rank":
            {
                var d0 = d;
                if (ch.CareerId != r.Career || d0.Rank < r.RankAtLeast)
                { res.Ok = false; res.Missing.Add($"{r.Career}: ранг ≥ {r.RankAtLeast} (сейчас {d0.Rank})"); }
                else res.Met.Add($"Ранг ≥ {r.RankAtLeast}");
                return;
            }
            case "any":
            {
                if (!r.Of.Any(sub => { var t = new RequirementCheck(); Evaluate(ch, sub, d, t); return t.Ok; }))
                { res.Ok = false; res.Missing.Add($"Любое из: {string.Join(" / ", r.Of.Select(o => o.Describe()))}"); }
                else res.Met.Add("Одно из условий выполнено");
                return;
            }
            case "group":
                foreach (var sub in r.Of) Evaluate(ch, sub, d, res);
                return;
        }
        if (!string.IsNullOrWhiteSpace(r.Text)) res.Met.Add(r.Text);
    }

    // ═════════════════════ Валидация ═════════════════════

    public List<string> Validate(Character ch)
    {
        return ValidateCore(ch, Compute(ch));
    }

    /// <summary>Проверки без повторного пересчёта — принимает уже готовую сводку.</summary>
    private List<string> ValidateCore(Character ch, DerivedStats d)
    {
        var w = new List<string>();
        var career = _pack.Career(ch.CareerId);

        if (career is null)
        {
            if (!string.IsNullOrWhiteSpace(ch.CareerId))
                w.Add($"Род занятий «{_pack.Career(ch.CareerId)?.Name ?? ch.CareerId}» не найден в дата-паке.");
        }
        else
        {
            foreach (var a in ch.Advances)
            {
                var opt = FindOption(ch, a.OptionId);
                if (opt is null) { w.Add($"Улучшение «{a.OptionId}» больше не существует в паке (возможно, переопределено)."); continue; }
                if (!_opt.FreeAllocation)
                {
                    var chk = CheckAdvance(ch, opt, d, checkRepeatLimit: false);
                    if (!chk.Ok) w.Add($"{opt.Name}: {string.Join("; ", chk.Missing)}");
                }
            }
        }

        foreach (var t in ch.Talents)
        {
            if (_pack.Talent(t.TalentId) is null) { w.Add($"Талант «{t.TalentId}» не найден."); continue; }
            var def = _pack.Talent(t.TalentId)!;
            if (t.Rank > 3) w.Add($"Талант «{def.Name}»: ранг {t.Rank} выше 3 (максимум по книге).");
        }

        foreach (var s in ch.Skills)
        {
            var def = _pack.Skill(s.SkillId);
            if (def is null) { w.Add($"Умение «{s.SkillId}» не найдено."); continue; }
            if (s.Rank * 10 > _opt.MaxSkillBonus)
                w.Add($"Умение «{def.Name}»: +{s.Rank * 10} выше максимума +{_opt.MaxSkillBonus}.");
        }

        foreach (var e in ch.Items)
            if (_pack.ItemOf(e) is null) w.Add($"Предмет «{e.ItemId}» не найден в дата-паке.");

        // Снаряжение: в ближнем бою нужна характеристика «Ближний бой»,
        // для огнестрельного оружия — «Дальний бой» (базовая книга, глава II).
        {
            var ws = d.Get("ws")?.Total ?? 0;
            var bs = d.Get("bs")?.Total ?? 0;
            foreach (var e in ch.Items)
            {
                var def = _pack.ItemOf(e);
                if (def is null) continue;
                if (def.Category == ItemCategory.MeleeWeapon && ws <= 0)
                    w.Add($"Оружие «{def.Name}»: нет характеристики «Ближний бой» — в ближнем бою с ним не сразиться.");
                if (def.Category is ItemCategory.RangedWeapon or ItemCategory.Launcher && bs <= 0)
                    w.Add($"Оружие «{def.Name}»: нет характеристики «Дальний бой» — стрелять из него не выйдет.");
            }
        }

        foreach (var e in ch.Characteristics)
            if (e.AdvancePoints > _opt.MaxCharacteristicPoints)
                w.Add($"{CharacteristicRu(e.CharacteristicId)}: улучшений +{e.AdvancePoints} при максимуме +{_opt.MaxCharacteristicPoints}.");

        if (ch.Wounds < 0) w.Add("Количество ран не может быть отрицательным.");

        foreach (var c in d.Characteristics.Where(x => x.Total < 0))
            w.Add($"{c.Name}: отрицательное значение ({c.Total}).");

        return w.Distinct().ToList();
    }

    // ═════════════════════ Утилиты ═════════════════════

    public static readonly Dictionary<string, string> NormalizeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ws"] = "ws", ["weapon skill"] = "ws", ["ближний бой"] = "ws", ["ближний"] = "ws", ["бб"] = "ws", ["close combat"] = "ws",
        ["bs"] = "bs", ["ballistic skill"] = "bs", ["дальний бой"] = "bs", ["дальний"] = "bs", ["дб"] = "bs",
        ["str"] = "str", ["strength"] = "str", ["сила"] = "str", ["с"] = "str", ["сил"] = "str",
        ["tou"] = "tou", ["toughness"] = "tou", ["выносливость"] = "tou", ["вын"] = "tou",
        ["dex"] = "dex", ["agility"] = "dex", ["dexterity"] = "dex", ["ловкость"] = "dex", ["лов"] = "dex",
        ["int"] = "int", ["intelligence"] = "int", ["интеллект"] = "int", ["инт"] = "int",
        ["per"] = "per", ["perception"] = "per", ["восприятие"] = "per", ["вос"] = "per",
        ["wil"] = "wil", ["willpower"] = "wil", ["сила воли"] = "wil", ["св"] = "wil",
        ["fel"] = "fel", ["fellowship"] = "fel", ["товарищество"] = "fel", ["тов"] = "fel",
        ["movement"] = "movement", ["скорость"] = "movement",
        ["size"] = "size", ["размер"] = "size",
        ["psy"] = "psy", ["пси"] = "psy", ["пси-рейтинг"] = "psy", ["psy rating"] = "psy", ["psy_rating"] = "psy",
        ["wounds"] = "wounds", ["wt"] = "wounds", ["порог ранений"] = "wounds", ["wound threshold"] = "wounds",
        ["fate"] = "fate", ["fate points"] = "fate", ["очки судьбы"] = "fate",
    };

    public static string Normalize(string s)
    {
        var k = (s ?? "").Trim().Replace('_', ' ').ToLowerInvariant().Trim(' ', '.', ',', ':', ';');
        if (NormalizeMap.TryGetValue(k, out var v)) return v;
        foreach (var (alias, cid) in NormalizeMap)
            if (alias.Length > 2 && (k.StartsWith(alias) || k.EndsWith(alias))) return cid;
        return k.Replace(' ', '_');
    }

    public static string CharacteristicRu(string id) => id switch
    {
        "ws" => "Ближний бой",
        "bs" => "Дальний бой",
        "str" => "Сила",
        "tou" => "Выносливость",
        "dex" => "Ловкость",
        "int" => "Интеллект",
        "per" => "Восприятие",
        "wil" => "Сила воли",
        "fel" => "Товарищество",
        "movement" => "Скорость",
        "wounds" => "Порог ранений",
        "fate" => "Очки судьбы",
        "psy" => "Пси-рейтинг",
        _ => id
    };

    /// <summary>Названия размеров — как в таблице «Размер» базовой книги.</summary>
    public static string SizeRu(int size) => size switch
    {
        1 => "Крошечный (1)",
        2 => "Маленький (2)",
        3 => "Небольшой (3)",
        4 => "Средний (4)",
        5 => "Крупный (5)",
        6 => "Большой (6)",
        7 => "Огромный (7)",
        _ => $"Размер {size}"
    };
}