using System.Text.RegularExpressions;
using RogueTrader.Core.Data;
using RogueTrader.Core.Models;

namespace RogueTrader.Core.Rules;

public enum GrantKind { Characteristic, Skill, Talent }

/// <summary>
/// Награда, которую персонаж получает от варианта жизненного пути.
/// В книге такие награды записаны обычным текстом, поэтому они извлекаются
/// из строк «grants» и «effect» и показываются игроку переключателями.
/// </summary>
public sealed class PathGrant
{
    public GrantKind Kind { get; init; }
    /// <summary>Id характеристики / умения / таланта. Для неизвестного — пусто.</summary>
    public string TargetId { get; init; } = "";
    /// <summary>Изменение характеристики (для остальных видов 0).</summary>
    public int Delta { get; init; }
    /// <summary>Что показываем игроку.</summary>
    public string Label { get; init; } = "";
    /// <summary>Фрагмент книги, из которого награда получена.</summary>
    public string SourceText { get; init; } = "";
    /// <summary>
    /// true, если книга требует выбора («один из двух», «+3 к Ловкости или Силе воли»).
    /// Такие награды по умолчанию выключены: решать должен игрок.
    /// </summary>
    public bool Optional { get; init; }
    /// <summary>Ключ для запоминания включён/выключен в персонаже.</summary>
    public string Key => $"{Kind}:{TargetId}:{Delta}";
}

/// <summary>Разбор книжного текста с наградами жизненного пути.</summary>
public static partial class GrantParser
{
    /// <summary>Падежные формы названий характеристик → id.</summary>
    private static readonly (string Id, string[] Forms)[] CharForms =
    [
        ("wil", ["силе воли", "силы воли", "силу воли", "силой воли", "сила воли", "силу волей", "выдержке"]),
        ("fel", ["товариществу", "товарищества", "товариществом", "товарищество"]),
        ("tou", ["выносливости", "выносливость", "выносливостью", "выносливост"]),
        ("per", ["восприятию", "восприятие", "восприятия", "восприятием"]),
        ("int", ["интеллекту", "интеллект", "интеллекта", "интеллектом"]),
        ("dex", ["ловкости", "ловкость", "ловкостью", "ловкост"]),
        ("str", ["силе", "сила", "силы", "силой", "силу"]),
        ("ws",  ["ближнему бою", "ближний бой", "ближнего боя", "ближним боем"]),
        ("bs",  ["дальнему бою", "дальний бой", "дальнего боя", "дальним боем"]),
    ];

    // «+5 к Силе», «-5 к Силе воли», «+3 к Ловкости или Силе воли».
    // Имена характеристик тянем до запятой/точки/конца строки: «или», «и» и
    // начало нового числа со знаком разбираются уже отдельным шагом.
    [GeneratedRegex(@"(?<sign>[+\-−–—])\s*(?<val>\d+)\s*к\s+(?<names>[^,.;]+?)(?=[,.;]|$)", RegexOptions.Multiline)]
    private static partial Regex CharRegex();

    [GeneratedRegex(@"\s+(?:или|и)\s+|,|;|(?=[+\-−–—]\s*\d+\s*к)", RegexOptions.IgnoreCase)]
    private static partial Regex NameSplitRegex();

    [GeneratedRegex(@"[«""]")]
    private static partial Regex QuoteStart();

    /// <summary>Куда указывает текст прямо перед названием в «ёлочках».</summary>
    private static readonly Regex TalentContext =
        new(@"(талант\w*|способност\w*)\s*$", RegexOptions.IgnoreCase);

    private static readonly Regex SkillContext =
        new(@"(умени\w*|навык\w*)\s*$", RegexOptions.IgnoreCase);

    /// <summary>
    /// Извлекает награды из варианта жизненного пути.
    /// Сканируются и <c>grants</c> (заполнен у родных миров), и <c>effect</c> +
    /// хвост описания (у судьбы, пути в пустоту, испытания и устремления
    /// весь эффект записан текстом).
    /// </summary>
    public static List<PathGrant> Parse(NamedOptionDef def, GamePack pack)
    {
        var result = new List<PathGrant>();
        var seen = new HashSet<string>();
        void Add(PathGrant g)
        {
            if (g.TargetId.Length == 0 && g.Kind != GrantKind.Characteristic) return;
            if (!seen.Add(g.Key)) return;
            result.Add(g);
        }

        var blocks = new List<string>();
        blocks.AddRange(def.Grants);
        if (!string.IsNullOrWhiteSpace(def.Effect)) blocks.Add(def.Effect);
        // У части вариантов перечисление эффектов стоит сразу за словом «Эффект:»
        // в самом описании, поэтому description тоже сканируем — но только хвост,
        // чтобы не поймать упоминания в вступлении.
        if (!string.IsNullOrWhiteSpace(def.Description))
        {
            var tail = TailAfterEffect(def.Description);
            if (tail.Length > 0) blocks.Add(tail);
        }

        foreach (var block in blocks)
        {
            // В книге «один из двух», «или», «выберите» — это выбор игрока.
            // Такие награды не применяем молча.
            var optional = OptionalRegex.IsMatch(block);

            foreach (Match m in CharRegex().Matches(block))
            {
                var sign = m.Groups["sign"].Value is "+" ? 1 : -1;
                var val = int.Parse(m.Groups["val"].Value);
                var partOptional = optional && (m.Groups["names"].Value.Contains(" или ", StringComparison.OrdinalIgnoreCase)
                                                || m.Groups["names"].Value.Contains(" или", StringComparison.OrdinalIgnoreCase));
                foreach (var raw in SplitNames(m.Groups["names"].Value))
                {
                    var id = MatchCharacteristic(raw);
                    if (id is null) continue;
                    Add(new PathGrant
                    {
                        Kind = GrantKind.Characteristic,
                        TargetId = id,
                        Delta = sign * val,
                        Optional = partOptional,
                        Label = $"{(sign > 0 ? "+" : "")}{sign * val} к {CharacteristicName(id)}",
                        SourceText = block.Trim(),
                    });
                }
            }

            foreach (var (name, context) in QuotedNames(block))
            {
                var wantTalent = TalentContext.IsMatch(context);
                var wantSkill = SkillContext.IsMatch(context);
                var talent = pack.Talent(ResolveTalent(pack, name));
                var skill = pack.Skill(ResolveSkill(pack, name));

                if (talent is not null && (wantTalent || !wantSkill))
                    Add(new PathGrant
                    {
                        Kind = GrantKind.Talent, TargetId = talent.Id, Optional = optional,
                        Label = $"талант «{talent.Name}»", SourceText = block.Trim(),
                    });
                if (skill is not null && (wantSkill || !wantTalent))
                    Add(new PathGrant
                    {
                        Kind = GrantKind.Skill, TargetId = skill.Id, Optional = optional,
                        Label = $"умение «{skill.Name}»", SourceText = block.Trim(),
                    });
            }
        }

        return result;
    }

    /// <summary>Формулировки, по которым видно, что игрок выбирает один из вариантов.</summary>
    private static readonly Regex OptionalRegex = new(
        @"(один\s+из|выберите|любой\s+из|\s+или\s+талант|\s+или\s+умение|«[^»]{0,60}»\s+или\s|или\s+«)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary>Хвост описания после слов «Эффект:» / «Эффект :».</summary>
    private static string TailAfterEffect(string description)
    {
        var i = description.IndexOf("Эффект", StringComparison.OrdinalIgnoreCase);
        return i < 0 ? "" : description[i..];
    }

    private static IEnumerable<string> SplitNames(string raw)
    {
        foreach (var p in NameSplitRegex().Split(raw))
        {
            var t = p.Trim();
            if (t.Length > 0) yield return t;
        }
    }

    public static string? MatchCharacteristic(string raw)
    {
        var s = Norm(raw);
        foreach (var (id, forms) in CharForms)
            foreach (var f in forms)
                if (s.Contains(f, StringComparison.Ordinal))
                    return id;
        return null;
    }

    public static string CharacteristicName(string id) => id switch
    {
        "ws" => "Ближнему бою", "bs" => "Дальнему бою", "str" => "Силе", "tou" => "Выносливости",
        "dex" => "Ловкости", "int" => "Интеллекту", "per" => "Восприятию",
        "wil" => "Силе воли", "fel" => "Товариществу", _ => id,
    };

    /// <summary>Все фрагменты в «ёлочках» вместе с несколькими словами перед ними.</summary>
    private static IEnumerable<(string Name, string Context)> QuotedNames(string text)
    {
        foreach (Match m in QuoteStart().Matches(text))
        {
            var open = m.Index;
            if (open + 1 >= text.Length) yield break;
            var closeCh = m.Value[0] == '«' ? '»' : '"';
            var close = text.IndexOf(closeCh, open + 1);
            if (close < 0) yield break;
            var name = text[(open + 1)..close].Trim();
            if (name.Length == 0) { continue; }
            var ctxStart = Math.Max(0, open - 40);
            yield return (name, text[ctxStart..open]);
        }
    }

    /// <summary>
    /// Находит умение по названию. В книге у названия стоит характеристика
    /// в скобках: «Грамотность (Инт)», а у специализированных — ещё и область
    /// («Обыденное знание (Империум) (Инт)»), поэтому убираем скобочные
    /// куски справа по одному, пока не найдём совпадение.
    /// </summary>
    public static string? ResolveSkill(GamePack pack, string raw)
    {
        var name = raw.Trim('«', '»', '"', ' ', '.');
        var full = Norm(name);
        var direct = pack.Skills.Values.FirstOrDefault(s => Norm(s.Name) == full);
        if (direct is not null) return direct.Id;

        var work = name;
        while (true)
        {
            var cut = work.LastIndexOf('(');
            if (cut <= 0) break;
            work = work[..cut].Trim();
            var w = Norm(work);
            var hit = pack.Skills.Values.FirstOrDefault(s => Norm(s.Name) == w);
            if (hit is not null) return hit.Id;
        }
        return ByPrefix(pack.Skills.Values, full);
    }

    public static string? ResolveTalent(GamePack pack, string raw)
    {
        var name = raw.Trim('«', '»', '"', ' ', '.');
        var full = Norm(name);
        var direct = pack.Talents.Values.FirstOrDefault(t => Norm(t.Name) == full);
        if (direct is not null) return direct.Id;

        var work = name;
        while (true)
        {
            var cut = work.LastIndexOf('(');
            if (cut <= 0) break;
            work = work[..cut].Trim();
            var w = Norm(work);
            var hit = pack.Talents.Values.FirstOrDefault(t => Norm(t.Name) == w);
            if (hit is not null) return hit.Id;
        }
        return ByPrefix(pack.Talents.Values, full);
    }

    /// <summary>
    /// Книга иногда пишет название сокращённо («Обыденное» вместо «Обыденное
    /// знание»). Подходит, только если такой вариант в паке один.
    /// </summary>
    private static string? ByPrefix<T>(IEnumerable<T> items, string prefix) where T : RuleDef
    {
        if (prefix.Length < 4) return null;
        var hits = items.Where(i => Norm(i.Name).StartsWith(prefix, StringComparison.Ordinal)).ToList();
        if (hits.Count == 0) return null;
        // Среди совпадений берём базовое название (без уточнения в скобках):
        // «Обыденное» — это «Обыденное знание», а не «Обыденное знание (Варп)».
        var basic = hits.FirstOrDefault(i => !i.Name.Contains('('));
        if (basic is not null) return basic.Id;
        return hits.Count == 1 ? hits[0].Id : null;
    }

    private static string Norm(string? s) =>
        (s ?? "").ToLowerInvariant().Replace('ё', 'е').Trim();
}