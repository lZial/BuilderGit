using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using RogueTrader.App.Services;
using RogueTrader.App.ViewModels;
using RogueTrader.Core.Models;
using RogueTrader.Core.Rules;

namespace RogueTrader.App;

/// <summary>
/// Пошаговое создание персонажа прямо через модель представления —
/// ровно теми действиями, которые делает игрок в окне программы.
/// Нужен, чтобы находить несостыковки правил и недод��лки интерфейса.
/// Запуск: <c>RogueTraderBuilder.exe --walkthrough</c>.
/// </summary>
internal static class Walkthrough
{
    private static readonly StringBuilder Log = new();
    private static int _failures;

    private static void Line(string s = "") => Log.AppendLine(s);
    private static void Ok(string s) => Log.AppendLine($"  [ок]   {s}");
    private static void Bad(string s) { _failures++; Log.AppendLine($"  [ОШИБКА] {s}"); }
    private static void Note(string s) => Log.AppendLine($"  [·]    {s}");
    private static void Step(string s) => Log.AppendLine($"{Environment.NewLine}═══ {s} ═══");

    private static void Try(string what, Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            Bad($"{what}: {ex.GetType().Name}: {ex.Message}");
            Log.AppendLine("        " + (ex.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim());
        }
    }

    public static int Run()
    {
        _failures = 0;
        var vm = new MainViewModel();
        vm.NoDialogs = true;
        Line("ПОШАГОВОЕ СОЗДАНИЕ ПЕРСОНАЖА (Rogue Trader 2nd Edition)");
        Line("=====================================================");
        Line($"Дата-пак: {vm.Svc.Report.Summary}");

        var pack = vm.Svc.Pack;
        var ch = vm.Character;

        // ── Шаг 1. Определение характеристик ──
        Step("Шаг 1. Определение характеристик");
        Try("бросок характеристик", vm.RollCharacteristics);
        foreach (var row in vm.Characteristics)
            Note($"{row.Abbrev,-4} {row.Name,-16} база {row.BaseText,-4} итог {row.Total,-3} бонус +{row.CharBonus}");

        var bad = vm.Characteristics.Where(r => r.Total is < 25 or > 45).ToList();
        if (bad.Count > 0) Bad($"итог вне 25…45: {string.Join(", ", bad.Select(b => b.Name))}");
        else Ok("все итоги в диапазоне 25…45");

        // ── Шаг 2. Жизненный путь ──
        Step("Шаг 2. Жизненный путь");

        Try("выбор рода занятий", () => vm.SelectedCareer =
            pack.Careers.Values.FirstOrDefault(c => c.Id == "free_trader") ?? pack.Careers.Values.FirstOrDefault());
        Note($"род занятий: {vm.SelectedCareer?.Name}");

        var homeworld = pack.Homeworlds.FirstOrDefault(h => h.Id == "death_world");
        Try("родной мир", () => vm.SelectedHomeworld = homeworld);
        Note($"родной мир: {vm.SelectedHomeworld?.Name}");

        var destiny = pack.Destiny.FirstOrDefault(d => d.Id == "pauper");
        Try("уготованная судьба", () => vm.SelectedDestiny = destiny);
        Note($"судьба: {vm.SelectedDestiny?.Name}");

        Try("путь в пустоту", () => vm.SelectedVoidPath = pack.VoidPath.FirstOrDefault(v => v.Id == "taint"));
        Note($"путь в пустоту: {vm.SelectedVoidPath?.Name}");

        Try("испытание", () => vm.SelectedHardship = pack.Hardship.FirstOrDefault(h => h.Id == "mark_of_war"));
        Note($"испытание: {vm.SelectedHardship?.Name}");

        Try("устремление", () => vm.SelectedAspiration = pack.Aspiration.FirstOrDefault(a => a.Id == "greed"));
        Note($"устремление: {vm.SelectedAspiration?.Name}");

        Try("наследие", () => vm.SelectedLegacy = pack.Legacy.FirstOrDefault(l => l.Id == "cursed_family"));
        Note($"наследие: {vm.SelectedLegacy?.Name}");

        Line();
        Note($"наград от пути распознано: {vm.PathGrants.Count}");
        foreach (var g in vm.PathGrants)
            Note($"   [{g.Grant.Kind}] {g.Label}   ← {Trim(g.SourceText, 70)}");
        if (vm.PathGrants.Count == 0)
            Bad("ни одна награда жизненного пути не распознана — проверь разбор текста");

        var pathChars = vm.Characteristics.Where(r => r.PathPart != 0).ToList();
        foreach (var r in pathChars)
            Note($"характеристика {r.Name}: {r.PathText}");

        Line();
        Note($"умений получено: {ch.Skills.Count} (род занятий {ch.Skills.Count(s => s.Origin == "career")}, путь {ch.Skills.Count(s => s.Origin != "career" && s.Origin != "gm")})");
        foreach (var s in ch.Skills.OrderBy(s => s.Origin).ThenBy(s => pack.Skill(s.SkillId)?.Name))
            Note($"   умение {pack.Skill(s.SkillId)?.Name,-38} +{s.Rank * 10,-3} [{Origin(s.Origin)}]");
        if (ch.Skills.Count == 0) Bad("род занятий не дал ни одного умения — startingSkillsVerbatim не разобран");

        Note($"талантов получено: {ch.Talents.Count}");
        foreach (var t in ch.Talents.Take(20))
            Note($"   талант {pack.Talent(t.TalentId)?.Name,-40} ранг {t.Rank} [{Origin(t.Origin)}]");

        // ── Шаг 2б. «Сквозь шторм»: снаряжение, таланты и варианты разделов ──
        Step("Шаг 2б. «Сквозь шторм»: снаряжение и варианты разделов");

        {
            var stormItems = pack.Items.Values.Where(i => i.Source == "stormstout").ToList();
            Note($"предметов из «Сквозь шторм»: {stormItems.Count}");
            if (stormItems.Count < 150) Bad($"снаряжения «Сквозь шторм» мало: {stormItems.Count} (ожидалось 195)");
            else Ok($"снаряжение «Сквозь шторм» в паке: {stormItems.Count} предметов");

            var lasgun = pack.Items.Values.FirstOrDefault(i => i.Name.Contains("Штурмовой лазган"));
            if (lasgun is null) Bad("в паке нет штурмового лазгана из «Сквозь шторм»");
            else Ok($"штурмовой лазган на месте: {lasgun.Name}, урон {lasgun.Damage}, доступность {lasgun.Availability}");

            var stormTalents = pack.Talents.Values.Where(t => t.Source == "stormstout").ToList();
            Note($"талантов из «Сквозь шторм»: {stormTalents.Count}");
            foreach (var t in stormTalents) Note($"   {t.Name}");
            if (!stormTalents.Any(t => t.Id == "mercenary")) Bad("нет таланта «Наёмник» из «Сквозь шторм»");
            else Ok("таланты круутов и орков на месте");

            // Ссылки родов занятий: все стартовые умения и таланты должны находиться в паке.
            var dangling = new List<string>();
            foreach (var c in pack.Careers.Values)
            {
                foreach (var s in c.StartingSkills)
                    if (pack.Skill(s) is null) dangling.Add($"{c.Name}: умение {s}");
                foreach (var t in c.StartingTalents)
                    if (pack.Talent(t) is null) dangling.Add($"{c.Name}: талант {t}");
            }
            if (dangling.Count > 0) Bad($"неразрешённые ссылки родов занятий: {string.Join("; ", dangling.Take(8))}");
            else Ok("стартовые умения и таланты всех родов занятий разрешаются");

            // Варианты разделов предыстории («Сквозь шторм»).
            var stormy = pack.Destiny.FirstOrDefault(d => d.Variants.Count > 0);
            if (stormy is null) Bad("нет разделов предыстории с вариантами («Сквозь шторм»)");
            else
            {
                Note($"раздел с вариантами: {stormy.Name} — вариантов {stormy.Variants.Count}");
                foreach (var v in stormy.Variants) Note($"   {v.Name} — {v.CostXp} ОО: {Trim(v.Effect, 60)}");
                Try("выбор раздела с вариантами", () => vm.SelectedDestiny = stormy);
                if (vm.SelectedDestinyVariant is not null && vm.SelectedDestinyVariant.Id.Length > 0)
                    Bad("вариант раздела выбрался сам — книга оставляет выбор игроку");
                else Ok("вариант раздела не выбран автоматически — выбор за игроком");

                var before = vm.Character.PathVariantXp;
                Try("выбор варианта раздела", () => vm.SelectedDestinyVariant = stormy.Variants[0]);
                Note($"выбран вариант: {vm.SelectedDestinyVariant?.Name}, цена {vm.SelectedDestinyVariant?.CostXp} ОО");
                var delta = vm.Character.PathVariantXp - before;
                if (vm.SelectedDestinyVariant is null || vm.SelectedDestinyVariant.Id.Length == 0) Bad("вариант раздела не выбран");
                else if (delta != vm.SelectedDestinyVariant.CostXp)
                    Bad($"цена варианта не учтена в опыте: +{delta} вместо {vm.SelectedDestinyVariant.CostXp}");
                else Ok($"цена варианта учтена: {vm.SelectedDestinyVariant.Name}, +{delta} ОО из свободного опыта");
            }

            // Разделы с выбором варианта («Испытания и страдания», «Путь в
            // пустоту») перечисляют варианты — их должно быть видно в меню.
            var hardship = pack.Hardship.FirstOrDefault(h => h.Variants.Count > 0);
            if (hardship is null) Bad("у разделов «Испытания и страдания» нет вариантов выбора");
            else
            {
                Ok($"варианты раздела «{hardship.Name}» на месте: "
                   + string.Join("; ", hardship.Variants.Take(2).Select(v => v.Name)));
                Try("выбор испытания", () => vm.SelectedHardship = hardship);
                Note($"наград от испытания: {vm.PathGrants.Count}");
            }

            // Альтернативные ранги «любому роду занятий»: у них должен быть
            // список подходящих родов занятий, и его можно выбрать.
            var anyRanks = pack.Careers.Values.Where(c => c.AnyBaseCareer).ToList();
            if (anyRanks.Count == 0) Bad("нет альтернативных рангов «любому роду занятий»");
            else
            {
                Note($"альтернативных рангов «любому роду занятий»: {anyRanks.Count}");
                foreach (var c in anyRanks)
                {
                    var allowed = c.ExcludedBaseCareers.Count == 0
                        ? pack.Careers.Values.Count(x => x.Id != c.Id && !x.AlternateRanks)
                        : pack.Careers.Values.Count(x => x.Id != c.Id && !x.AlternateRanks
                                                        && !c.ExcludedBaseCareers.Contains(x.Id));
                    if (allowed == 0) Bad($"у альтернативного ранга «{c.Name}» нет подходящих родов занятий");
                }
                Ok($"у всех альтернативных рангов «любому роду занятий» есть список подходящих родов занятий");
                var keepCareer = vm.SelectedCareer;
                Try("выбор основного рода занятий", () =>
                {
                    vm.SelectedCareer = anyRanks.First();
                    var base_ = vm.BaseCareerChoices.FirstOrDefault()?.Value;
                    if (base_ is null) Bad("для альтернативного ранга не предложены основные роды занятий");
                    else
                    {
                        vm.SelectedBaseCareer = base_;
                        Note($"основной род занятий: {base_.Name}; умений выдано: {vm.Character.Skills.Count}");
                        if (vm.Character.Skills.Count == 0)
                            Bad("после выбора основного рода занятий умения не выдались");
                    }
                    // Возвращаем прежний род занятий — дальше проверки идут как обычно.
                    vm.SelectedBaseCareer = null;
                    vm.SelectedCareer = keepCareer;
                });
            }

            // Выбор варианта не должен удваивать список вариантов.
            {
                var withVariants = pack.Hardship.FirstOrDefault(h => h.Variants.Count > 0);
                if (withVariants is null) Bad("нет разделов с вариантами выбора");
                else
                {
                    vm.SelectedHardship = null;
                    vm.SelectedHardship = withVariants;
                    var list = vm.HardshipVariantChoices;
                    var once = list.Count;
                    var pick = list.FirstOrDefault(c => c.Value is { Id.Length: > 0 });
                    if (pick?.Value is null) Bad("в списке вариантов нет ни одного варианта");
                    else
                    {
                        vm.SelectedHardshipVariant = pick.Value;
                        if (list.Count != once)
                            Bad($"после выбора варианта список изменился: было {once}, стало {list.Count}");
                        else Ok($"после выбора варианта список не изменился ({once} пунктов)");
                    }
                }
            }

            // «Вместо …»: новые варианты испытаний «Сквозь шторм» заменяют
            // старые — выбрать оба нельзя.
            var replaces = pack.Hardship.Where(h => h.Replaces.Count > 0).ToList();
            if (replaces.Count == 0) Bad("в паке нет вариантов «вместо …» для испытаний");
            else
            {
                foreach (var h in replaces)
                    foreach (var id in h.Replaces)
                        if (pack.Hardship.All(x => x.Id != id))
                            Bad($"«{h.Name}» заменяет несуществующий вариант «{id}»");
                Ok($"вариантов «вместо …»: {replaces.Count} — {string.Join("; ", replaces.Select(h => h.Name + " → " + string.Join(", ", h.Replaces)))}");
                // Испытание выбирается одно, поэтому книжное «вместо …»
                // выполняется само собой: важно, чтобы все варианты были
                // в списке и выбор работал.
                var keepHardship = vm.SelectedHardship;
                Try("выбор испытания работает", () =>
                {
                    var h = replaces.First();
                    vm.SelectedHardship = h;
                    var shown = vm.HardshipChoices.Select(c => c.Value?.Id ?? "").ToHashSet();
                    if (vm.HardshipChoices.Count != pack.Hardship.Count)
                        Bad($"в списке испытаний {vm.HardshipChoices.Count} пунктов вместо {pack.Hardship.Count}");
                    else if (vm.SelectedHardship?.Id != h.Id)
                        Bad($"выбранное испытание «{h.Name}» не отображается");
                    else if (pack.Hardship.Any(x => !shown.Contains(x.Id)))
                        Bad("в списке испытаний пропущены варианты");
                    else Ok($"испытание выбирается, все {pack.Hardship.Count} вариантов в списке");
                });
                vm.SelectedHardship = keepHardship;

                // Смена испытания не должна трогать остальные списки выбора пути.
                var otherBefore = (vm.HomeworldChoices.Count, vm.DestinyChoices.Count,
                                   vm.VoidPathChoices.Count, vm.AspirationChoices.Count,
                                   vm.LegacyChoices.Count);
                vm.SelectedHardship = pack.Hardship.FirstOrDefault(x => x.Variants.Count > 0);
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { });
                var otherAfter = (vm.HomeworldChoices.Count, vm.DestinyChoices.Count,
                                  vm.VoidPathChoices.Count, vm.AspirationChoices.Count,
                                  vm.LegacyChoices.Count);
                if (otherBefore != otherAfter)
                    Bad("смена испытания изменила остальные списки выбора пути");
                else Ok("смена испытания не тронула остальные списки выбора пути");
            }

            var voidWithVariants = pack.VoidPath.Count(v => v.Variants.Count > 0);
            if (voidWithVariants == 0) Bad("у разделов «Путь в пустоту» нет вариантов выбора");
            else Ok($"варианты выбора есть у {voidWithVariants} разделов «Путь в пустоту»");

            // «Или-или» в предыстории: выбор должен быть распознан, а названные
            // умения и таланты — быть в паке.
            {
                // Настоящий выбор — «или» между двумя названными умениями
                // или талантами либо «выберите одно».
                var choiceRegex = new Regex(
                    @"[«""][^»""]{2,60}[»""]\s+или\s+|или\s+[«""][^»""]{2,60}[»""]|выберите\s+одн",
                    RegexOptions.IgnoreCase);
                var talentCtx = new Regex(@"(талант\w*|способност\w*)\s*$", RegexOptions.IgnoreCase);
                var skillCtx = new Regex(@"(умени\w*|навык\w*)\s*$", RegexOptions.IgnoreCase);
                int choiceCases = 0, badChoice = 0;
                var problems = new List<string>();

                void CheckNames(string text, string where)
                {
                    foreach (Match m in Regex.Matches(text, "[«\"]([^»\"]{3,70})[»\"]"))
                    {
                        var nm = m.Groups[1].Value.Trim();
                        var ctx = text[Math.Max(0, m.Index - 40)..m.Index];
                        if (!talentCtx.IsMatch(ctx) && !skillCtx.IsMatch(ctx)) continue;
                        if (Core.Rules.GrantParser.ResolveSkill(pack, nm) is not null) continue;
                        if (Core.Rules.GrantParser.ResolveTalent(pack, nm) is not null) continue;
                        badChoice++;
                        problems.Add($"{where}: «{nm}» нет в паке");
                    }
                }

                foreach (var opt in pack.Homeworlds.Concat(pack.Destiny).Concat(pack.VoidPath)
                                   .Concat(pack.Hardship).Concat(pack.Aspiration).Concat(pack.Legacy))
                {
                    var grants = Core.Rules.GrantParser.Parse(opt, pack);
                    foreach (var g in opt.Grants)
                    {
                        if (!choiceRegex.IsMatch(g)) continue;
                        choiceCases++;
                        var mine = grants.Where(x => x.SourceText == g).ToList();
                        if (mine.Count == 0 || !mine.Any(x => x.Optional))
                        {
                            badChoice++;
                            problems.Add($"{opt.Name}: выбор не распознан — «{g[..Math.Min(50, g.Length)]}»");
                        }
                        CheckNames(g, opt.Name);
                    }
                    foreach (var v in opt.Variants)
                    {
                        var text = v.Effect ?? "";
                        if (!choiceRegex.IsMatch(text)) continue;
                        choiceCases++;
                        var def = new Core.Models.NamedOptionDef
                        {
                            Id = $"{opt.Id}.{v.Id}", Name = v.Name, Effect = text,
                        };
                        var vg = Core.Rules.GrantParser.Parse(def, pack);
                        if (!vg.Any(x => x.Optional))
                        {
                            badChoice++;
                            problems.Add($"{opt.Name} — {v.Name}: выбор не распознан");
                        }
                        CheckNames(text, $"{opt.Name} — {v.Name}");
                    }
                }
                Note($"выборов «или-или» в предыстории: {choiceCases}");
                if (badChoice == 0)
                    Ok($"выбор «или-или» во всех {choiceCases} местах распознан, умения и таланты на месте");
                else
                    Bad($"проблем с выбором «или-или»: {badChoice} — {string.Join("; ", problems.Distinct().Take(6))}");
            }
        }

        // ── Описания и бонусы в меню выбора ──
        {
            var careerDesc = vm.CareerChoices.Count(c => !string.IsNullOrWhiteSpace(c.Description));
            var careerBonus = vm.CareerChoices.Count(c => !string.IsNullOrWhiteSpace(c.Bonuses));
            if (careerDesc != vm.CareerChoices.Count) Bad($"у родов занятий нет описания в меню выбора: {careerDesc} из {vm.CareerChoices.Count}");
            else Ok($"у всех родов занятий есть описание в меню выбора ({vm.CareerChoices.Count})");
            if (careerBonus == 0) Bad("у родов занятий нет бонусов в меню выбора");
            else Ok($"бонусы в меню выбора есть у {careerBonus} родов занятий");

            var pathWithBonus = vm.DestinyChoices.Count(c => !string.IsNullOrWhiteSpace(c.Bonuses));
            if (pathWithBonus != vm.DestinyChoices.Count) Bad($"у судеб нет бонусов в меню выбора: {pathWithBonus} из {vm.DestinyChoices.Count}");
            else Ok($"у всех судеб есть бонусы в меню выбора ({vm.DestinyChoices.Count})");
        }

        // Лист умений: неизученное базовое умение — по половине характеристики
        {
            var basic = vm.Skills.FirstOrDefault(r => !r.Learned && !r.Advanced && r.CharacteristicSum > 0);
            var advanced = vm.Skills.FirstOrDefault(r => !r.Learned && r.Advanced);
            if (basic is null) Bad("не нашлось неизученного базового умения для проверки");
            else
            {
                Note($"«{basic.Name}» не изучено: вклад {basic.CharacteristicSum}, итог {basic.Total}");
                Note($"   {basic.CharacteristicTip}");
                if (basic.Total <= 0) Bad("неизученное базовое умение должно считаться по половине характеристики");
                else Ok($"неизученное базовое умение считается по половине характеристики: {basic.Total}");
            }
            if (advanced is not null)
            {
                Note($"«{advanced.Name}» (продвинутое) не изучено: итог {advanced.Total}");
                if (advanced.Total != 0) Bad("неизученное продвинутое умение должно давать 0");
                else Ok("неизученное продвинутое умение не применяется");
            }
            // В строке только свои характеристики — без лишних нулей
            var withCells = vm.Skills.Where(r => r.Cells.Count > 0).ToList();
            if (withCells.Count > 0 && withCells.Any(r => r.Cells.Count > 4))
                Bad("в строке умения больше четырёх характеристик — фильтр не работает");
            else if (withCells.Count > 0)
                Ok($"в строке умения только свои характеристики: " +
                   string.Join(", ", withCells.Take(3).Select(r => $"{r.Name} ({r.Cells.Count})")));
        }

        // ── Шаг 3. Трата опыта ──
        Step("Шаг 3. Трата опыта (500 свободных очков)");
        var xpStart = vm.Stats.XpSpendableLeft;
        Note($"свободных очков опыта: {xpStart}");

        // 3а. улучшение характеристик — единственное, что тратит опыт при записи числа
        var ws = vm.Characteristics.FirstOrDefault(c => c.Id == "ws");
        if (ws is not null)
        {
            var before0 = vm.Stats.XpSpendableLeft;
            Try("улучшение характеристики Ближний бой на +5", () => vm.SetCharacteristicAdvance(ws, 5));
            var spent0 = before0 - vm.Stats.XpSpendableLeft;
            Note($"потрачено на характеристику: {spent0} ОО (осталось {vm.Stats.XpSpendableLeft})");
            if (spent0 <= 0) Bad("улучшение характеристики не стоило опыта");
            else Ok($"+5 к «{ws.Name}» стоит {spent0} ОО — как в таблице рода занятий");
        }

        // 3б. покупки из таблицы улучшений ранга 1
        var rank1 = vm.AvailableAdvances.Where(a => a.Ok).ToList();
        int bought = 0;
        foreach (var row in rank1)
        {
            if (vm.Stats.XpSpendableLeft <= 0) break;
            var before = vm.Stats.XpSpendableLeft;
            Try($"покупка улучшения «{row.Name}»", () => vm.BuyAdvance(row));
            if (vm.Stats.XpSpendableLeft == before) Bad($"покупка «{row.Name}» не списала опыт");
            bought++;
            if (bought >= 5) break;
        }
        Note($"куплено улучшений: {bought}, осталось опыта {vm.Stats.XpSpendableLeft}");

        // Описания умений и талантов: игрок должен видеть, что покупает
        var withDesc = vm.AvailableAdvances.Count(r => r.HasShortDescription);
        var withDetails = vm.AvailableAdvances.Count(r => r.HasDetails);
        Note($"улучшений с описанием: {withDesc} из {vm.AvailableAdvances.Count}, с подробностями: {withDetails}");
        if (vm.AvailableAdvances.Count > 0 && withDesc == 0)
            Bad("ни у одного улучшения нет описания — игроку не из чего выбирать");
        else if (withDesc > 0)
            Ok($"описания подтянуты из пака: {withDesc} из {vm.AvailableAdvances.Count}");

        // 3в. база и правки мастера опыта не должны тратить
        {
            var before = vm.Stats.XpSpendableLeft;
            var row = vm.Characteristics.First();
            Try("правка «База»", () => vm.SetCharacteristicBase(row, 33));
            if (vm.Stats.XpSpendableLeft != before) Bad("правка «База» списала опыт — по книге она бесплатна");
            else Ok("правка «База» опыта не тратит");

            before = vm.Stats.XpSpendableLeft;
            Try("правка «Правки»", () => vm.SetCharacteristicAdjustment(row, 2));
            if (vm.Stats.XpSpendableLeft != before) Bad("правка «Правки» списала опыт — по книге она бесплатна");
            else Ok("правка «Правки» опыта не тратит");
            if (row.AdjustmentValue != 2) Bad($"правка «Правки» не записалась: в колонке {row.AdjustmentValue}");
            else Ok("правка «Правки» записана в свою колонку");
        }

        // 3в-2. «База» ограничена 20 очками сверх начального значения
        {
            var row = vm.Characteristics.First(r => r.Id == "ws");
            var def = vm.DefaultBaseFor("ws");

            // Кнопки «−» и «+» меняют значение на очко
            vm.SetCharacteristicBase(row, def);
            var before = row.BaseValue;
            vm.AdjustCharacteristicBase(row, 1);
            if (row.BaseValue != before + 1) Bad("кнопка «+» у «Базы» не увеличила значение на очко");
            else Ok("кнопка «+» у «Базы» работает");
            vm.AdjustCharacteristicBase(row, -1);
            if (row.BaseValue != before) Bad("кнопка «−» у «Базы» не вернула значение");
            else Ok("кнопка «−» у «Базы» работает");

            // Больше 20 сверх начального — нельзя
            Try("ввод 99 в «Базу»", () => vm.SetCharacteristicBase(row, 99));
            if (row.BaseValue > def + 20)
                Bad($"«База» превысила предел: {row.BaseValue} при начальном {def}");
            else
                Ok($"«База» ограничена: введено 99, осталось {row.BaseValue} (начальное {def}, предел +{MainViewModel.MaxManualBasePoints})");
            Note($"{vm.BasePointsSummary}");

            Try("возврат базы", () => vm.SetCharacteristicBase(row, def));
        }

        // 3г. «Прочее» и «Улучш.» считает система — вручную не меняются
        {
            var strBefore = vm.Characteristics.FirstOrDefault(r => r.Id == "str");
            var otherBefore = strBefore?.OtherValue ?? 0;
            var totalBefore = strBefore?.Total ?? 0;

            // Вид меняет «Прочее» само: у долговязых Силы −5.
            Try("смена вида на долговязых", () => vm.SelectedSpecies =
                vm.Svc.AllSpecies.FirstOrDefault(s => s.Id == "longshanks"));
            var strRow = vm.Characteristics.FirstOrDefault(r => r.Id == "str");
            if (strRow is null) Bad("нет строки «Сила» для проверки вида");
            else if (strRow.OtherValue == otherBefore)
                Bad("вклад вида не появился в колонке «Прочее»");
            else
                Ok($"вклад вида записан в «Прочее»: было {otherBefore}, стало {strRow.OtherValue} ({strRow.OtherBreakdown})");
            if (strRow is not null && strRow.Total == totalBefore)
                Bad("итог не пересчитан после смены вида");
            else
                Ok($"итог пересчитан: {totalBefore} → {strRow?.Total}");

            Try("возврат вида к человеку", () => vm.SelectedSpecies =
                vm.Svc.AllSpecies.FirstOrDefault(s => s.Id == "human"));
        }

        Note($"итого: ранг {vm.Stats.Rank}, всего ОО {vm.Stats.TotalXp}, потрачено {vm.Stats.XpSpent}");

        // ── Снаряжение по главе «Арсенал» ──
        Step("Глава «Арсенал»: раскладка снаряжения");
        Note($"предметов в паке: {pack.Items.Count} (включая аугметику и препараты)");
        Note($"разделов: {pack.ItemSections.Count}, подразделов: {pack.ItemGroups.Count}");
        var orphan = pack.Items.Values.Count(i => Core.Data.ItemGroups.Find(i, pack.ItemGroups) is null);
        if (orphan == 0) Ok("каждый предмет попал в раздел и подраздел главы «Арсенал»");
        else Bad($"{orphan} предметов не попали ни в один раздел «Арсенала»");
        foreach (var sec in pack.ItemSections.OrderBy(s => s.Order))
        {
            var groups = pack.ItemGroups.Where(g => g.SectionId == sec.Id).ToList();
            var inSec = pack.Items.Values.Count(i =>
                Core.Data.ItemGroups.Find(i, pack.ItemGroups) is { } g && g.SectionId == sec.Id);
            Note($"{sec.Title} (стр. {sec.Page}): {groups.Count} подразделов, {inSec} предметов");
        }
        foreach (var row in vm.Items.Take(6))
            Note($"   {row.Name,-38} {row.SectionTitle} → {row.GroupTitle} · {row.SourceText}");

        // ── Психические приёмы и силы навигатора ──
        Step("Психические приёмы и силы навигатора");
        Note($"приёмов: {pack.PsyAbilities.Count}, сил навигатора: {pack.NavigatorPowers.Count}");
        if (pack.PsyAbilities.Count == 0) Bad("ни один психический приём не загружен");
        if (pack.NavigatorPowers.Count == 0) Bad("ни одна сила навигатора не загружена");
        foreach (var d in pack.PsyAbilities.GroupBy(a => a.Discipline))
            Note($"дисциплина {d.Key}: {d.Count()} приёмов");
        foreach (var g in pack.PsyAbilities.GroupBy(a => a.Discipline))
        {
            if (g.Count() < 8) Note($"   в «{g.Key}» меньше восьми приёмов — постижение ещё невозможно");
            else Ok($"дисциплина «{g.Key}» постижима: {g.Count()} приёмов (нужно 8)");
        }
        foreach (var d in pack.PsyAbilities.Where(a => string.IsNullOrWhiteSpace(a.Effect)).Take(3))
            Bad($"у приёма «{d.Name}» пустой эффект");
        foreach (var w in pack.NavigatorPowers.Where(p => string.IsNullOrWhiteSpace(p.Effect)).Take(3))
            Bad($"у силы «{w.Name}» пустой эффект");

        Try("изучить психический приём", () =>
        {
            var row = vm.PsyAbilities.FirstOrDefault(r => r.Discipline == "Телепатия");
            if (row is null) return;
            vm.LearnPsy(row);
        });
        Note($"выучено психических приёмов: {vm.PsyLearnedCount}");
        Note(vm.MasteredText);

        Try("получить силу навигатора и поднять уровень", () =>
        {
            var row = vm.NavPowers.FirstOrDefault(r => !r.Learned);
            if (row is null) return;
            vm.LearnNav(row);
            row.Up();
        });
        foreach (var row in vm.NavPowers.Where(r => r.Learned).Take(4))
            Note($"сила «{row.Name}»: {row.Proficiency} ({row.BonusText})");

        // ── Таблица 2-2 «Ранги» ──
        Step("Таблица 2-2 «Ранги»: сверка с книгой");
        // Значения переписаны из таблицы 2-2 (стр. 30 базовой книги)
        var expected = new (int Rank, int Min, int Max)[]
        {
            (1, 5000, 6900), (2, 7000, 9999), (3, 10000, 12999), (4, 13000, 16999),
            (5, 17000, 20999), (6, 21000, 24999), (7, 25000, 29000), (8, 30000, 34999),
        };
        var actual = RulesEngine.RankThresholds;
        if (actual.Length != expected.Length)
            Bad($"в таблице {actual.Length} рангов, а в книге {expected.Length}");
        for (var i = 0; i < Math.Min(actual.Length, expected.Length); i++)
        {
            var a = actual[i]; var e = expected[i];
            var ok = a == e;
            Note($"ранг {a.Rank}: {a.Min} – {a.Max} ОО  {(ok ? "совпадает" : "≠ книга: " + e.Min + " – " + e.Max + " ОО")}");
            if (!ok) Bad($"ранг {a.Rank}: в движке {a.Min} – {a.Max}, в книге {e.Min} – {e.Max}");
        }

        // Границы: ранг = последний, чей минимум ≤ потраченного опыта
        foreach (var (rank, min, _) in expected)
        {
            var atMin = RulesEngine.RankFor(min);
            if (atMin != rank)
                Bad($"при {min} ОО движок даёт ранг {atMin}, а по книге {rank}");
            // Ранга 0 в книге нет: новый персонаж с 4500 врождённых ОО — уже ранг 1,
            // поэтому нижнюю границу проверяем только начиная со второго ранга.
            if (rank > 1 && RulesEngine.RankFor(min - 1) >= rank)
                Bad($"при {min - 1} ОО ранг не должен быть {rank}, а движок даёт {RulesEngine.RankFor(min - 1)}");
            // Пропуск между рангами в книге (6901–6999 и 29001–29999) персонаж
            // проводит на нижнем ранге: ранг растёт только при достижении
            // минимума следующего, поэтому резкого скачка быть не должно.
            if (rank > 1)
            {
                var prevMax = expected[rank - 2].Max;
                var justAfter = RulesEngine.RankFor(prevMax + 1);
                if (justAfter > rank)
                    Bad($"при {prevMax + 1} ОО ранг не должен быть {justAfter} — книга не пропускает ранги");
                else if (prevMax + 1 < min)
                    Note($"пропуск книги: {prevMax + 1} – {min - 1} ОО → ранг {justAfter}"
                         + $" (следующий ранг только с {min} ОО)");
            }
        }
        Ok("все восемь границ рангов сходятся с книгой");
        Note(vm.RankRuleText);
        Note($"текущий ранг: {vm.Stats.Rank}, диапазон {vm.RankRangeText}, "
             + $"до следующего {vm.Stats.XpToNextRank} ОО");

        // Пример из книги: Джонас потратил 10 050 ОО → ранг 3
        if (RulesEngine.RankFor(10050) != 3)
            Bad("пример из книги не сходится: 10 050 ОО должно быть ранг 3");
        else
            Ok("пример из книги сходится: 10 050 ОО → ранг 3");

        // Новый персонаж: 4500 врождённых → ранг 1
        if (RulesEngine.RankFor(4500) != 1)
            Bad("новый персонаж (4500 ОО) должен быть ранга 1");
        else
            Ok("новый персонаж с 4500 врождёнными ОО — ранг 1, как в книге");

        // ── Начисление опыта ──
        Step("Начисление опыта: общий опыт, ранг и трата");
        var xpBefore = vm.Stats.TotalXp;
        var rankBefore = vm.Stats.Rank;
        var freeBefore = vm.Stats.XpSpendableLeft;
        vm.XpToAddText = "2000";
        Try("начисление 2000 ОО", vm.AddXp);
        if (vm.Stats.TotalXp != xpBefore + 2000)
            Bad($"после начисления 2000 ОО общий опыт {vm.Stats.TotalXp}, а ожидался {xpBefore + 2000}");
        else Ok($"начисленные 2000 ОО вошли в общий опыт ({xpBefore} → {vm.Stats.TotalXp})");
        if (vm.Stats.XpSpendableLeft != freeBefore + 2000)
            Bad($"свободный опыт {vm.Stats.XpSpendableLeft} вместо {freeBefore + 2000}");
        else Ok("начисленный опыт доступен для улучшений");
        if (vm.Stats.Rank <= rankBefore)
            Bad($"начисление 2000 ОО не подняло ранг: было {rankBefore}, стало {vm.Stats.Rank}");
        else Ok($"ранг вырос с {rankBefore} до {vm.Stats.Rank} — начисленный опыт учтён");
        vm.ResetEarnedXp();
        if (vm.Stats.TotalXp != xpBefore || vm.Stats.XpSpendableLeft != freeBefore)
            Bad("сброс начисленного опыта не вернул прежние значения");
        else
            Ok($"сброс начисленного опыта вернул ранг {vm.Stats.Rank} и бюджет {freeBefore} ОО");

        // ── Тау, абхуманы и размеры ──
        Step("Вид, размер персонажа и род занятий «Воин Огня»");

        // Долговязые: в «Руководстве по абхуманам» у породы напечатана
        // черта «Размер (Hulking)» — значит, размер Крупный (5).
        Try("выбор вида «Долговязые»", () => vm.SelectedSpecies =
            vm.Svc.AllSpecies.FirstOrDefault(s => s.Id == "longshanks"));
        Note($"вид: {vm.SelectedSpecies?.Name}, размер {vm.Stats.Size} ({RulesEngine.SizeRu(vm.Stats.Size)})");
        if (vm.Stats.Size != 5) Bad($"размер долговязых должен быть 5, а получился {vm.Stats.Size}");
        else Ok("размер взят из черты породы «Размер (Hulking)» — Крупный (5)");
        Note($"подсказка: {vm.SizeHint}");

        // Крысоловы: «Размер (Scrawny)» — Небольшой (3).
        Try("выбор вида «Крысоловы»", () => vm.SelectedSpecies =
            vm.Svc.AllSpecies.FirstOrDefault(s => s.Id == "ratlings"));
        if (vm.Stats.Size != 3) Bad($"размер крысоловов должен быть 3, а получился {vm.Stats.Size}");
        else Ok("размер крысоловов — «Размер (Scrawny)», Небольшой (3)");

        // Вид задаёт черты, начальные ранения и очки судьбы — по «Руководству
        // по абхуманам» (например, у долговязых: 1к5 + 2 × бонус Выносливости
        // ранений и два очка судьбы).
        Try("черты и начальные ранения вида", () =>
        {
            vm.SelectedSpecies = vm.Svc.AllSpecies.FirstOrDefault(s => s.Id == "longshanks");
            var traits = ch.SpeciesTraits;
            Note($"черты вида: {string.Join(", ", traits)}");
            Note($"ранения: {vm.StartingWoundsText}; {vm.StartingFatePointsText}");
            if (!traits.Contains("Longshank Biology")) Bad("черта «Longshank Biology» не выдана персонажу");
            else if (!traits.Contains("Not Quite Human")) Bad("черта «Not Quite Human» не выдана персонажу");
            else Ok("черты долговязых выданы: " + string.Join(", ", traits));

            if (ch.FatePoints != 2) Bad($"у долговязых должно быть 2 очка судьбы, а получилось {ch.FatePoints}");
            else Ok("очки судьбы долговязых — 2");

            // Порог ранений: 1к5 + 2 × бонус Выносливости.
            var tou = vm.Characteristics.FirstOrDefault(r => r.Id == "tou");
            var bonus = tou?.CharBonus ?? 0;
            var expect = ch.SpeciesWoundsRoll + bonus * 2;
            if (vm.Stats.WoundThreshold != expect)
                Bad($"порог ранений {vm.Stats.WoundThreshold}, а по книге {ch.SpeciesWoundsRoll} + {bonus}×2 = {expect}");
            else Ok($"порог ранений по книге: 1к5 ({ch.SpeciesWoundsRoll}) + {bonus}×2 = {vm.Stats.WoundThreshold}");

            // Таланты и умения вида выдаются сразу.
            if (ch.FindTalent("technical_knock") is null) Bad("талант «Ритуал освобождения» (Technical Knock) не выдан");
            else Ok("талант вида выдан");
            if (ch.FindSkill("common_lore_tech") is null) Bad("умение «Обыденное знание (Техника)» не выдано");
            else Ok("умения вида выданы");
        });

        // Скорость передвижения: по базовой книге длина дистанций зависит от
        // бонуса Ловкости, а размер этот бонус сдвигает (таблица 14-2).
        {
            var dex = vm.Characteristics.FirstOrDefault(r => r.Id == "dex");
            var rules = vm.Svc.Pack.MovementRules;
            void Check(int size, string who)
            {
                // Бонус Ловкости пересчитывается при смене вида — читаем его заново.
                var bonus = dex?.CharBonus ?? 0;
                var mod = rules.ModifierForSize(size);
                var factor = Math.Max(0, bonus + mod);
                int Exp(string kind) => (int)Math.Round(Math.Max(rules.Minimum(kind), factor * rules.Coefficient(kind)),
                    MidpointRounding.AwayFromZero);
                Note($"{who}: Ловкость {dex?.Total} (бонус {bonus}), размер {size} ({mod:+#;-#}) → скорость ×{factor}");
                if (vm.Stats.Size != size) { Bad($"размер не применился: {vm.Stats.Size} вместо {size}"); return; }
                if (vm.Stats.HalfMovement != Exp("half")) Bad($"пол. скорость {vm.Stats.HalfMovement}, а должно {Exp("half")}");
                else if (vm.Stats.FullMovement != Exp("full")) Bad($"полная скорость {vm.Stats.FullMovement}, а должно {Exp("full")}");
                else if (vm.Stats.ChargeMovement != Exp("charge")) Bad($"натиск {vm.Stats.ChargeMovement}, а должно {Exp("charge")}");
                else if (vm.Stats.RunMovement != Exp("run")) Bad($"бег {vm.Stats.RunMovement}, а должно {Exp("run")}");
                else Ok($"скорость по книге: {vm.Stats.HalfMovement} / {vm.Stats.FullMovement} / {vm.Stats.ChargeMovement} / {vm.Stats.RunMovement} м");
            }
            Check(vm.Stats.Size, "текущий вид");
            vm.SelectedSpecies = vm.Svc.AllSpecies.FirstOrDefault(s => s.Id == "longshanks");
            Check(vm.Stats.Size, "долговязые (размер 5 → +1 к бонусу Ловкости)");
            vm.SelectedSpecies = vm.Svc.AllSpecies.FirstOrDefault(s => s.Id == "human");
        }

        // Жизненный путь задаёт начальные ранения и очки судьбы
        // («Изначальное количество ран…», «Изначальное количество очков судьбы…»).
        Try("начальные ранения и очки судьбы по родному миру", () =>
        {
            vm.SelectedSpecies = vm.Svc.AllSpecies.FirstOrDefault(s => s.Id == "human");
            vm.SelectedHomeworld = vm.Svc.Pack.Homeworlds.FirstOrDefault(h => h.Id == "death_world");
            Note($"ранения: {vm.StartingWoundsText}; очки судьбы: {vm.StartingFatePointsText}");
            var tou = vm.Characteristics.FirstOrDefault(r => r.Id == "tou");
            var bonus = tou?.CharBonus ?? 0;
            var expect = ch.PathWoundsRoll + 1 + bonus * 2;
            if (vm.Stats.WoundThreshold != expect)
                Bad($"порог ранений {vm.Stats.WoundThreshold}, а по книге {ch.PathWoundsRoll} + 1 + {bonus}×2 = {expect}");
            else Ok($"порог ранений по книге: 1к5 ({ch.PathWoundsRoll}) + 1 + {bonus}×2 = {vm.Stats.WoundThreshold}");

            var roll = ch.PathFateRoll;
            var expectFate = roll <= 5 ? 2 : 3;
            if (ch.FatePoints != expectFate)
                Bad($"очки судьбы {ch.FatePoints}, а по броску {roll} должно быть {expectFate}");
            else Ok($"очки судьбы по броску 1к10 ({roll}) — {ch.FatePoints}");
        });

        // Тау: своя раса со своими бросками характеристик.
        Try("выбор вида «Тау»", () => vm.SelectedSpecies =
            vm.Svc.AllSpecies.FirstOrDefault(s => s.Id == "tau"));
        Note($"вид: {vm.SelectedSpecies?.Name}");
        if (vm.SelectedSpecies is null) Bad("вид «Тау» не загрузился в дата-пак");
        else
        {
            var wsRow = vm.Characteristics.FirstOrDefault(r => r.Id == "ws");
            Note($"формула броска: {vm.RollHintText}");
            if (wsRow is not null && wsRow.BaseValue != 20)
                Bad($"база Ближнего боя у тау должна быть 20 (2к10+20), а получилась {wsRow.BaseValue}");
            else Ok("постоянная броска у тау — своя для каждой характеристики");
            if (ch.FindSkill("common_lore_tau_empire") is not null)
                Ok("тау получают умение «Обыденное знание (Империя Тау)»");
            else Bad("тау не получили умение «Обыденное знание (Империя Тау)»");
        }

        // Род занятий «Воин Огня».
        Try("выбор рода занятий «Воин Огня»", () => vm.SelectedCareer = pack.Career("fire_warrior"));
        var fw = pack.Career("fire_warrior");
        if (fw is null) Bad("род занятий «Воин Огня» не загрузился в дата-пак");
        else
        {
            Note($"рангов: {fw.Ranks.Count}, улучшений на рангах: " +
                 string.Join(", ", fw.Ranks.Select(r => $"{r.Level} — {r.Advances.Count}")));
            if (fw.Ranks.Count != 8) Bad($"у воина Огня должно быть 8 рангов, а в паке {fw.Ranks.Count}");
            else Ok("таблицы улучшений воина Огня на ранге 1–8 загружены");
            if (fw.CharacteristicAdvancement.Count == 0) Bad("нет таблицы улучшений характеристик воина Огня");
            else Ok("таблица улучшений характеристик воина Огня есть");
            Note($"альтернативные пути: {string.Join(", ", fw.Paths.Select(p => p.Name))}");
            if (fw.Paths.Count != 3) Bad($"ожидалось 3 альтернативных пути, а в паке {fw.Paths.Count}");
            else Ok("три альтернативных пути: следопыт, оператор дронов, пилот боевого скафандра");

            // Стартовое снаряжение воина Огня: оружие тау есть в паке.
            var fwRows = vm.StartingEquipment.ToList();
            Note($"строк стартового снаряжения: {fwRows.Count}, сопоставлено: {fwRows.Count(r => r.HasOptions)}");
            foreach (var r in fwRows) Note($"   {r.Text}");
            var impulse = fwRows.FirstOrDefault(r => r.Options.Any(o => o.Item.Name.Contains("Импульсн", StringComparison.CurrentCulture)));
            if (impulse is null) Bad("оружие тау не сопоставилось со стартовым снаряжением воина Огня");
            else
            {
                Ok($"сопоставлено оружие тау: {string.Join(" или ", impulse.Options.Select(o => o.Text))}");
                var pick = impulse.Options.First();
                impulse.Choose(0);
                var granted = vm.Items.FirstOrDefault(r => r.Entry.ItemId == pick.Item.Id);
                if (granted is null) Bad($"предмет «{pick.Item.Name}» не выдан");
                else Ok($"выдано: {pick.Text} — качество {granted.QualityName}, урон {granted.EffectiveDamage}");
            }
        }

        // Альтернативный путь заменяет таблицу улучшений.
        Try("выбор пути «Следопыт»", () => vm.SelectedPathId = "scout");
        Note($"путь: {vm.SelectedPathName}");
        Note($"требования: {vm.PathHint}");
        if (vm.SelectedPathId != "scout") Bad("путь «Следопыт» не выбрался");
        else
        {
            var path = vm.Engine.PathFor(ch);
            var rank2 = vm.Engine.AdvancesFor(ch, 2);
            if (path is null) Bad("движок не видит выбранный путь");
            else if (rank2.Count != path.Advances.Count)
                Bad($"на ранге 2 показывается {rank2.Count} улучшений вместо {path.Advances.Count} из пути");
            else Ok($"на ранге 2 доступны улучшения пути «{path.Name}» ({rank2.Count})");
        }

        // Возвращаем обычного персонажа, чтобы не сбить остальные шаги.
        Try("возврат к человеку и вольному торговцу", () =>
        {
            vm.SelectedPathId = null;
            vm.SelectedCareer = pack.Career("free_trader");
            vm.SelectedSpecies = vm.Svc.AllSpecies.FirstOrDefault(s => s.Id == "human");
        });
        vm.RollCharacteristics();

        // ── Шаг 4. Оживление персонажа ──
        Step("Шаг 4. Оживление персонажа");
        Try("бросок очков судьбы 1к10", () =>
        {
            ch.FatePoints = Random.Shared.Next(1, 11);
            vm.MarkDirty();
        });
        Note($"очки судьбы: {vm.Stats.FatePoints}");

        // ── Шаг 5. Уровень дохода и очки корабля ──
        Step("Шаг 5. Уровень дохода и очки корабля");
        for (var attempt = 1; attempt <= 3; attempt++) Try("бросок 1к10", vm.RollIncome);
        Note(vm.IncomeText);
        Note(vm.IncomeExampleText);
        if (vm.Stats.IncomeLevel <= 0) Bad("уровень дохода не посчитан");
        else Ok($"уровень дохода {vm.Stats.IncomeLevel}, очков корабля {vm.Stats.ShipPoints}");

        Try("выбор корпуса", () => vm.SetShipHull(pack.ShipHulls.FirstOrDefault()));
        Note(vm.ShipHullText);
        Note($"осталось очков корабля: {vm.Stats.ShipPointsLeft}");

        // ── Шаг 6. Снаряжение ──
        Step("Шаг 6. Выбор снаряжения");
        foreach (var cat in new[] { "ranged_weapon", "melee_weapon", "armor", "clothing" })
        {
            var it = pack.ItemsByCategory(cat).FirstOrDefault();
            if (it is null) { Note($"категория {cat}: пусто"); continue; }
            Try($"добавить предмет «{it.Name}»", () =>
            {
                ch.Items.Add(new ItemEntry { ItemId = it.Id });
                vm.MarkDirty();
            });
        }
        Note($"предметов: {ch.Items.Count}, вес {vm.Stats.TotalEncumbrance} кг, защита {vm.Stats.ArmorProtection}");
        foreach (var row in vm.Items) Note($"   {row.Name,-44} {row.Encumbrance,-10} {row.Protection}");

        // ── Качество оружия ──
        Step("Качество оружия");
        Try("качество записывается в предмет", () =>
        {
            var melee = pack.ItemsByCategory("melee_weapon").FirstOrDefault();
            if (melee is null) { Note("холодное оружие в паке не найдено"); return; }
            ch.Items.Add(new ItemEntry { ItemId = melee.Id, Quality = "high" });
            vm.MarkDirty();
            var row = vm.Items.FirstOrDefault(r => r.Entry.ItemId == melee.Id && r.Entry.Quality == "high");
            if (row is null) { Bad("предмет с качеством не появился в списке снаряжения"); return; }
            if (row.QualityName != "Высшее") Bad($"качество показано как «{row.QualityName}»");
            else Ok($"«{row.Name}» — качество {row.QualityName}");

            // Высшее холодное оружие: +10 к тестам атаки и +1 к урону.
            var m = System.Text.RegularExpressions.Regex.Match(melee.Damage, @"\+(\d+)");
            if (m.Success)
            {
                var expect = int.Parse(m.Groups[1].Value) + 1;
                if (!row.EffectiveDamage.Contains("+" + expect))
                    Bad($"урон с учётом качества: «{row.EffectiveDamage}», ожидалось +{expect}");
                else Ok($"урон высшего качества: {row.EffectiveDamage}");
            }
        });
        Try("качество по умолчанию — среднее", () =>
        {
            var ranged = pack.ItemsByCategory("ranged_weapon").FirstOrDefault();
            if (ranged is null) { Note("стрелковое оружие в паке не найдено"); return; }
            ch.Items.Add(new ItemEntry { ItemId = ranged.Id });
            vm.MarkDirty();
            var row = vm.Items.FirstOrDefault(r => r.Entry.ItemId == ranged.Id && r.Entry.Quality.Length == 0);
            if (row is null) { Bad("предмет без качества не появился в списке"); return; }
            if (row.QualityName != "Среднее") Bad($"качество по умолчанию: «{row.QualityName}»");
            else Ok($"«{row.Name}» — качество {row.QualityName} (как в таблице)");
        });

        // ── Стартовое снаряжение ──
        Step("Стартовое снаряжение");
        Try("строки стартового снаряжения", () =>
        {
            vm.SelectedSpecies = pack.SpeciesOf("human");
            vm.SelectedCareer = pack.Career("free_trader");
            var rows = vm.StartingEquipment.ToList();
            Note($"строк: {rows.Count}, сопоставлено с предметами: {rows.Count(r => r.HasOptions)}, вручную: {rows.Count(r => r.HasMissing)}");
            foreach (var r in rows.Take(8))
                Note($"   {r.OriginText,-28} {r.Text}");
            var withItems = rows.FirstOrDefault(r => r.HasOptions);
            if (withItems is null) { Bad("ни одна строка стартового снаряжения не сопоставлена с предметом пака"); return; }

            // Строки без выбора («или» нет) выдаются сразу.
            var auto = rows.FirstOrDefault(r => r.HasOptions && !r.IsChoice);
            if (auto is not null)
            {
                var first = auto.Options.First();
                if (ch.Items.Any(i => i.ItemId == first.Item.Id))
                    Ok($"выдано автоматически (без выбора): {first.Text}");
                else
                    Bad($"предмет «{first.Text}» без выбора не выдан автоматически");
            }

            var opt = withItems.Options.First();
            withItems.Choose(0);
            if (!ch.Items.Any(i => i.ItemId == opt.Item.Id))
                Bad($"предмет «{opt.Item.Name}» не выдан в снаряжение");
            else
                Ok($"выдано по старту: {opt.Text}");
        });
        Try("выбор стартового снаряжения сохраняется", () =>
        {
            if (ch.StartingEquipmentChoices.Count == 0) { Bad("выбор стартового снаряжения не записан"); return; }
            var key = ch.StartingEquipmentChoices.Keys.First();
            Ok($"выборов: {ch.StartingEquipmentChoices.Count} (например {key})");
        });

        // ── Проверка листа умений ──
        Step("Проверка листа умений");
        Note($"строк в листе: {vm.Skills.Count}");
        var learned = vm.Skills.Count(r => r.Learned);
        Note($"изучено: {learned}");
        foreach (var row in vm.Skills.Where(r => r.Learned).Take(12))
            Note($"   {row.Name,-40} база {row.BaseText,-4} хар {row.CharacteristicSum,-4} улучш {row.AdvanceText,-4} итого {row.Total}");
        if (vm.Skills.Count != pack.Skills.Count)
            Note($"внимание: в листе {vm.Skills.Count} строк, а в дата-паке {pack.Skills.Count} умений");
        if (learned == 0) Bad("ни одно умение не отмечено как изученное");
        foreach (var row in vm.Skills.Where(r => r.Learned && r.Total <= 0))
            Bad($"изученное умение «{row.Name}» имеет итог {row.Total}");

        // Итог умения в листе и в экспорте должен совпадать: в PDF и Markdown
        // раньше печаталось «+0» вместо настоящего значения.
        {
            var stats = vm.Stats;
            var mismatch = 0;
            foreach (var e in ch.Skills)
            {
                var def = pack.Skill(e.SkillId);
                if (def is null) continue;
                var expectedValue = Core.Rules.SkillValues.Total(def, e, stats);
                var sheet = vm.Skills.FirstOrDefault(r => r.Id == e.SkillId)?.Total ?? -1;
                if (expectedValue != sheet) { mismatch++; Note($"   расхождение: {def.Name} — лист {sheet}, правило {expectedValue}"); }
            }
            if (mismatch > 0) Bad($"итог умения в листе и по правилу разошёлся в {mismatch} случаях");
            else Ok($"итог умения в листе и по правилу совпадает ({ch.Skills.Count} умений)");

            var md = CharacterRepository.ToMarkdown(ch, vm.Svc);
            var plus0 = md.Split('\n').Count(l => l.Contains("** +0"));
            if (plus0 > 0) Bad($"в Markdown осталось «+0» у {plus0} умений");
            else Ok("в Markdown умения печатаются настоящим значением, а не «+0»");
        }

        // Стартовое снаряжение у всех родов занятий: сколько строк удалось
        // сопоставить с предметами пака. Остальные — «вкусные» предметы из
        // книги (ряса, кадило, мантия Нобилите), которых в арсенале нет.
        {
            var keep = vm.SelectedCareer;
            int rows = 0, matched = 0, missing = 0;
            var misses = new List<string>();
            foreach (var c in pack.Careers.Values)
            {
                vm.SelectedCareer = c;
                foreach (var r in vm.StartingEquipment)
                {
                    rows++;
                    if (r.HasOptions) matched++;
                    if (r.HasMissing) { missing++; misses.Add($"{c.Name}: {r.Text}"); }
                }
            }
            vm.SelectedCareer = keep;
            Note($"строк стартового снаряжения у всех родов занятий: {rows}, "
               + $"сопоставлено: {matched}, строк с частичным совпадением: {missing}");
            foreach (var m in misses.Take(20)) Note($"   не найдено: {m}");
            if (rows > 0 && matched < rows * 0.7)
                Bad($"сопоставлено меньше 70% строк стартового снаряжения: {matched} из {rows}");
            else Ok($"сопоставлено не менее 70% строк стартового снаряжения ({matched} из {rows})");
        }

        // ── Журнал ручных правок мастера ──
        Step("Журнал правок");
        {
            var row = vm.Characteristics.FirstOrDefault(r => r.Id == "wil");
            if (row is null) Note("нет строки «Воля» — проверка журнала правок пропущена");
            else
            {
                var before = vm.AdjustmentLog.Count;
                vm.AdjustCharacteristicAdjustment(row, 5);
                var after = vm.AdjustmentLog.Count;
                if (after <= before) Bad("ручная правка характеристики не попала в журнал");
                else
                {
                    var entry = vm.AdjustmentLog.First();
                    Ok($"правка записана в журнал: {entry.When:dd.MM.yyyy HH:mm} — {entry.What}: было {entry.Before}, стало {entry.After}");
                    vm.AdjustCharacteristicAdjustment(row, -5);   // возвращаем как было
                    if (vm.AdjustmentLog.Count <= after) Bad("откат правки не записан в журнал");
                    else Ok("откат правки тоже записан в журнал");
                }
            }

            // Журнал должен попадать в экспорт.
            var md = CharacterRepository.ToMarkdown(ch, vm.Svc);
            if (md.Contains("## Журнал правок")) Ok("в экспорте есть раздел «Журнал правок»");
            else Bad("в экспорте нет раздела «Журнал правок»");
        }

        // ── Проверка талантов ──
        Step("Проверка талантов");
        foreach (var row in vm.Talents.Take(10))
            Note($"   {row.Name,-44} требования выполнены: {row.Ok} {row.Status}");
        if (vm.Talents.Count == 0) Bad("ни одного таланта не получено");

        // Ранг таланта: в книге таланты нумеруются с единицы, стартовый —
        // первый ранг. Проверяем, что в интерфейсе и в экспорте так и есть.
        {
            var startTalent = vm.Talents.FirstOrDefault(r => r.Entry.Rank == 0);
            if (startTalent is null) Note("таланта первого ранга нет — проверка ранга пропущена");
            else if (startTalent.RankText != "")
                Bad($"у стартового таланта «{startTalent.Name}» показан {startTalent.RankText}, а он первого ранга");
            else Ok("у стартового таланта ранг не показан (первый ранг по книге)");

            var boughtTalent = vm.Talents.FirstOrDefault(r => r.Entry.Rank > 0);
            if (boughtTalent is null) Note("купленного таланта нет — проверка ранга пропущена");
            else if (boughtTalent.RankText != $"ранг {boughtTalent.Entry.Rank + 1}")
                Bad($"у купленного таланта «{boughtTalent.Name}» показан {boughtTalent.RankText} вместо ранга {boughtTalent.Entry.Rank + 1}");
            else Ok($"ранг купленного таланта показан по книге: {boughtTalent.RankText}");
        }

        // Бюджет опыта: покупка сверх свободного опыта невозможна.
        {
            var opts = vm.Engine.AdvancesFor(ch, 2).Where(o => o.Cost > 0).ToList();
            var over = opts.FirstOrDefault(o => o.Cost > vm.Stats.XpSpendableLeft);
            if (over is null) Note("нет улучшения дороже свободного опыта — проверка бюджета пропущена");
            else
            {
                var chk = vm.Engine.CheckAdvance(ch, over);
                if (chk.Ok) Bad($"покупка «{over.Name}» ({over.Cost} ОО) разрешена при свободных {vm.Stats.XpSpendableLeft} ОО");
                else Ok($"покупка сверх бюджета отклоняется: {string.Join("; ", chk.Missing)}");
            }
        }

        // ── Сохранение и загрузка ──
        Step("Сохранение и загрузка персонажа");
        var tmp = Path.Combine(Path.GetTempPath(), "rt-walkthrough.json");
        Try("сохранить в JSON", () =>
        {
            ch.Meta.Name = "Тестовый Авантюрист";
            CharacterRepository.Save(ch, tmp);
        });
        Try("загрузить из JSON", () =>
        {
            var back = CharacterRepository.Load(tmp);
            var vm2 = new MainViewModel();
            vm2.Character = back;
            Note($"после загрузки: ранг {vm2.Stats.Rank}, умений {back.Skills.Count}, талантов {back.Talents.Count}, предметов {back.Items.Count}");
            foreach (var row in vm.Characteristics)
            {
                var b = back.FindCharacteristic(row.Id);
                if (b is null) { Bad($"характеристика {row.Name} потерялась при сохранении"); continue; }
                if (b.Base != row.BaseValue || b.AdvancePoints != row.AdvancePoints
                    || b.Adjustment != row.AdjustmentValue || b.PathPoints != row.PathPart)
                    Bad($"{row.Name}: после загрузки база {b.Base}/{row.BaseValue}, улучш. {b.AdvancePoints}/{row.AdvancePoints}, правки {b.Adjustment}/{row.AdjustmentValue}, путь {b.PathPoints}/{row.PathPart}");
            }
            Ok("JSON сохраняется и читается без расхождений");
        });

        // Старые сохранения содержат «infamy» — характеристику из Dark Heresy 1e,
        // которой нет в базовой книге. Такое поле должно просто игнорироваться.
        Try("загрузка старого файла с полем «infamy»", () =>
        {
            var old = Path.Combine(Path.GetTempPath(), "rt-walkthrough-old.json");
            var json = File.ReadAllText(tmp);
            File.WriteAllText(old, json.TrimEnd().TrimEnd('}').TrimEnd() + ", \"infamy\": 7 }");
            var back = CharacterRepository.Load(old);
            if (back.Meta.Name != ch.Meta.Name) Bad("старый файл загрузился, но персонаж отличается");
            else Ok("старое поле «infamy» проигнорировано, персонаж загружен как обычно");
        });

        // Имя персонажа: должно переживать сохранение, загрузку и правку на лету
        {
            var metaNotified = false;
            ch.Meta.PropertyChanged += (_, e) => { if (e.PropertyName == "Name") metaNotified = true; };
            ch.Meta.Name = "Жак";
            if (!metaNotified) Bad("имя персонажа не сообщает об изменении — шапка окна не обновится");
            else Ok("правка имени сразу видна в шапке");

            CharacterRepository.Save(ch, tmp);
            var changed = new List<string>();
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");
            vm.Character = CharacterRepository.Load(tmp);
            if (vm.Character.Meta.Name != "Жак") Bad($"после загрузки имя пропало: «{vm.Character.Meta.Name}»");
            else Ok("имя персонажа сохраняется и появляется после загрузки");
            if (!changed.Contains("Character"))
                Bad("после загрузки нет уведомления о смене персонажа — форма не перечитывает поля");
            else
                Ok("после загрузки форма перечитывает поля нового персонажа");

            // Живая привязка: такой же текст стоит в шапке окна
            var probe = new System.Windows.Controls.TextBlock { DataContext = vm };
            probe.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding("Character.Meta.Name"));
            probe.Measure(new System.Windows.Size(400, 30));
            vm.Character.Meta.Name = "Жак";
            probe.UpdateLayout();
            if (probe.Text != "Жак") Bad($"привязка на имя не обновилась: «{probe.Text}»");
            else Ok("привязка на имя обновляется сразу — как в шапке окна");
        }

        // ── Окно: проверка разметки ──
        // Ошибки в XAML (например, привязка к свойству без сеттера) всплывают
        // только при построении шаблонов — поэтому окно создаём и считаем макет.
        Step("Окно: разметка и привязки");
        Try("создание окна и расчёт макета", () =>
        {
            var w = new MainWindow { DataContext = vm, Width = 1280, Height = 820 };
            w.Measure(new Size(1280, 820));
            w.Arrange(new Rect(0, 0, 1280, 820));
            w.UpdateLayout();
        });
        Note($"окон в приложении: {System.Windows.Application.Current.Windows.Count}, " +
             $"несохранённые изменения: {vm.HasUnsavedChanges}");
        Ok("разметка окна строится без ошибок");

        // ── Экспорт ──
        Step("Экспорт");        AppPaths.EnsureDirectories();
        var pdf = Path.Combine(Path.GetTempPath(), "rt-walkthrough.pdf");
        Try("экспорт в PDF", () => PdfExportService.Export(ch, vm.Svc, pdf));
        if (File.Exists(pdf)) Ok($"PDF {new FileInfo(pdf).Length / 1024} КБ");
        else Bad("PDF не создался");
        Try("экспорт в Markdown", () =>
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "rt-walkthrough.md"),
                CharacterRepository.ToMarkdown(ch, vm.Svc), new System.Text.UTF8Encoding(false)));

        // ── Свой предмет: лут, которого нет в официальном снаряжении ──
        Step("Свой предмет");
        {
            // После загрузки файла персонаж в представлении — новый объект,
            // поэтому берём текущий, а не ссылку, сохранённую в начале.
            var cur = vm.Character;
            var before = vm.Stats.TotalEncumbrance;
            var talisman = new Core.Models.ItemDef
            {
                Id = "",
                Name = "Талисман с чужой руки",
                Description = "Костяной амулет, снятый с убитого. Никаких особых свойств — просто вещь.",
                Note = "Символ упыря, найденный в лазуре.",
                Category = Core.Models.ItemCategory.Personal,
                Source = "custom",
                Encumbrance = 2,
            };
            cur.Items.Add(new ItemEntry { ItemId = "", Custom = talisman });
            vm.Refresh();
            var row = vm.Items.FirstOrDefault(r => r.Entry.Custom is not null);
            if (row is null) Bad("свой предмет не появился в списке снаряжения");
            else
            {
                Ok($"свой предмет в списке снаряжения: «{row.Name}» ({row.GroupTitle})");
                if (row.SourceText != "Свой предмет") Bad($"неверная подпись источника: «{row.SourceText}»");
                else Ok("подпись источника: «Свой предмет»");
            }
            var after = vm.Stats.TotalEncumbrance;
            if (after != before + 2) Bad($"вес своего предмета не учтён: было {before}, стало {after}");
            else Ok($"вес своего предмета учтён: +{after - before} кг");

            // Количество: две штуки — вдвое больше груза.
            var entry = cur.Items.First(e => e.Custom is not null);
            entry.Quantity = 2;
            vm.Refresh();
            if (vm.Stats.TotalEncumbrance != after + 2) Bad("количество своего предмета не учтено в весе");
            else Ok("количество своего предмета учтено в весе: +2 кг");

            if (vm.Stats.Warnings.Any(w => w.Contains("не найден в дата-паке")))
                Bad("свой предмет помечен как не найденный в дата-паке");
            else Ok("свой предмет не считается потерянным");
        }

        // ── Сохранение и загрузка: выбранные варианты пути должны сохраниться ──
        Step("Сохранение и загрузка");
        {
            var file = Path.Combine(Path.GetTempPath(), "rt-walkthrough-char.json");
            var cur2 = vm.Character;
            Try("сохранение персонажа", () => CharacterRepository.Save(cur2, file));
            Character? loaded = null;
            Try("загрузка персонажа", () => loaded = CharacterRepository.Load(file));
            if (loaded is null) Bad("персонаж не загрузился");
            else
            {
                if (cur2.PathVariantIds.Count == 0) Ok("вариантов пути не выбрано — сохранять нечего");
                else if (loaded.PathVariantIds.Count != cur2.PathVariantIds.Count
                         || loaded.PathVariantIds.Values.Any(v => !cur2.PathVariantIds.Values.Contains(v)))
                    Bad($"варианты пути не сохранились: было {string.Join(",", cur2.PathVariantIds.Values)}, стало {string.Join(",", loaded.PathVariantIds.Values)}");
                else Ok($"варианты пути сохранились: {string.Join(", ", loaded.PathVariantIds.Select(kv => kv.Value))}");
                if (loaded.PathVariantXp != cur2.PathVariantXp)
                    Bad($"цена вариантов не сохранилась: {loaded.PathVariantXp} вместо {cur2.PathVariantXp}");
                else Ok($"цена вариантов сохранилась: {cur2.PathVariantXp} ОО");

                // Свой предмет должен сохраниться вместе с описанием.
                var mine = cur2.Items.FirstOrDefault(e => e.Custom is not null);
                var their = loaded.Items.FirstOrDefault(e => e.Custom is not null);
                if (mine is null) Ok("своих предметов в персонаже нет");
                else if (their is null || their.Custom!.Name != mine.Custom!.Name)
                    Bad($"свой предмет не сохранился: «{mine.Custom!.Name}»");
                else Ok($"свой предмет сохранился: «{their.Custom!.Name}» (вес {their.Custom!.Encumbrance} кг)");
            }
        }

        // ── Этап 5: полировка ──
        Step("Шаг 9. Полировка интерфейса");

        // Избранное: отметка пишется в файл и читается обратно.
        Try("избранное сохраняется", () =>
        {
            var probe = "__проверка_избранного__";
            Favorites.Load();
            var before = Favorites.IsFavorite(probe);
            Favorites.Toggle(probe);
            if (!Favorites.IsFavorite(probe)) Bad("предмет не помечен как избранный");
            else if (!File.Exists(Favorites.FilePath)) Bad("файл избранного не создан");
            else Ok($"избранное пишется в {Path.GetFileName(Favorites.FilePath)}");
            if (Favorites.IsFavorite(probe) != before) Favorites.Toggle(probe);
        });

        // Масштаб интерфейса: значение записывается в настройки и читается
        // обратно — при следующем запуске окно открывается с тем же масштабом.
        Try("масштаб интерфейса сохраняется", () =>
        {
            var old = UiSettings.LoadZoom();
            try
            {
                UiSettings.SaveZoom(0.8);
                var z = UiSettings.LoadZoom();
                if (Math.Abs(z - 0.8) > 0.001) Bad($"масштаб не восстановился: {z}");
                else Ok($"масштаб пишется в {Path.GetFileName(UiSettings.FilePath)} и читается обратно");
            }
            finally
            {
                // Возвращаем прежний масштаб: проверка не должна менять
                // настройки, которыми пользуется игрок.
                UiSettings.SaveZoom(old);
            }
        });

        // Плавная прокрутка списков: смещение должно считаться в пикселях.
        // При смещении по элементам колесо прыгает по целым карточкам.
        Try("списки прокручиваются по пикселям", () =>
        {
            var style = Application.Current.Resources[typeof(System.Windows.Controls.ListBox)] as Style;
            var setter = style?.Setters.OfType<Setter>()
                .FirstOrDefault(s => s.Property == System.Windows.Controls.VirtualizingPanel.ScrollUnitProperty);
            if (setter is null) Bad("у списков не задан VirtualizingPanel.ScrollUnit");
            else if (!Equals(setter.Value, System.Windows.Controls.ScrollUnit.Pixel))
                Bad($"списки считают смещение не в пикселях: {setter.Value}");
            else Ok("списки считают смещение в пикселях — колесо катит плавно");
        });

        // У умения ровно одна характеристика: «Бдительность (Вос)», «Акробатика
        // (Лов)». Если их две, в лист попадала удвоенная сумма вклада.
        Try("у умения одна характеристика", () =>
        {
            var multi = vm.Svc.Pack.Skills.Values
                .Where(sk => sk.Characteristics.Count != 1)
                .Select(sk => $"{sk.Name} ({string.Join(",", sk.Characteristics)})")
                .ToList();
            if (multi.Count > 0)
                Bad("характеристик у умений не одна: " + string.Join("; ", multi.Take(6)));
            else Ok("у каждого умения ровно одна характеристика");
        });

        // Психические приёмы: у каждого есть проверка и описание из книги.
        Try("у психического приёма есть проверка и описание", () =>
        {
            var bad = vm.Svc.Pack.PsyAbilities
                .Where(a => string.IsNullOrWhiteSpace(a.Test) || string.IsNullOrWhiteSpace(a.Effect))
                .Select(a => a.Name)
                .ToList();
            if (bad.Count > 0)
                Bad("у приёмов нет проверки или описания: " + string.Join("; ", bad.Take(6)));
            else Ok($"у всех {vm.Svc.Pack.PsyAbilities.Count} приёмов есть проверка и описание");
        });

        // Сортировка «по весу»: вес в паке записан свободно («4 кг», «0,5 кг»).
        Try("вес для сортировки", () =>
        {
            if (Math.Abs(MainViewModel.WeightNumber("0,5 кг") - 0.5) > 0.001) Bad("вес «0,5 кг» разобран неверно");
            else if (MainViewModel.WeightNumber("4 кг") != 4) Bad("вес «4 кг» разобран неверно");
            else if (MainViewModel.WeightNumber("1–2 кг") != 1) Bad("вес «1–2 кг» разобран неверно");
            else if (MainViewModel.WeightNumber("") != 0) Bad("пустой вес должен быть нулём");
            else Ok("вес разбирается верно: 0,5 / 4 / 1–2 / пусто");
        });

        // Свой предмет — на отдельной вкладке и не в книжном списке.
        Try("свой предмет только на своей вкладке", () =>
        {
            var talisman = new ItemDef { Id = "", Name = "Проверка: талисман", Category = ItemCategory.Personal };
            ch.Items.Add(new ItemEntry { ItemId = "", Custom = talisman });
            vm.Refresh();
            var onOwn = vm.CustomItemView.Cast<MainViewModel.ItemRow>().Any(r => r.Entry.Custom is not null);
            var inArsenal = vm.ItemView.Cast<MainViewModel.ItemRow>().Any(r => r.Entry.Custom is not null);
            if (!onOwn) Bad("свой предмет не попал на вкладку «Свои предметы»");
            else if (inArsenal) Bad("свой предмет попал в список книжного снаряжения");
            else Ok("свой предмет только на вкладке «Свои предметы»");
        });

        // Холодное оружие без «Ближнего боя» — должно быть предупреждение.
        // Проверяем движком напрямую: так видно правило само по себе, без
        // влияния правок и бросков демонстрационного персонажа.
        Try("предупреждение о «Ближнем бою»", () =>
        {
            var melee = pack.ItemsByCategory("melee_weapon").FirstOrDefault();
            if (melee is null) { Note("холодное оружие в паке не найдено"); return; }
            var engine = new RulesEngine(pack);
            var test = new Character { SpeciesId = "human", CareerId = "free_trader" };
            // Минус в базе: к «Ближнему бою» прибавляется бонус вида (у
            // человека это +5), и при нулевой базе итог всё равно выше нуля.
            test.Characteristics.Add(new CharacteristicEntry { CharacteristicId = "ws", Base = -10 });
            test.Items.Add(new ItemEntry { ItemId = melee.Id });
            var warnings = engine.Validate(test);
            if (!warnings.Any(w => w.Contains("Ближний бой")))
                Bad($"нет предупреждения: холодное оружие, а «Ближний бой» = {warnings.Count} предупреждений");
            else Ok("предупреждение о «Ближнем бою» есть");

            // И наоборот: с нормальной характеристикой предупреждения нет.
            test.Characteristics[0].Base = 30;
            var clean = engine.Validate(test);
            if (clean.Any(w => w.Contains("Ближний бой"))) Bad("предупреждение есть, хотя «Ближний бой» = 35");
            else Ok("при «Ближнем бою» = 35 предупреждения нет");
        });

        // Журнал правок: мастерские изменения попадают в журнал.
        Try("журнал правок: умения, таланты, снаряжение", () =>
        {
            var before = vm.AdjustmentLog.Count;

            // Умение: мастер выучил его и поправил «Правки» вручную.
            var skillRow = vm.Skills.FirstOrDefault(r => !r.Learned);
            if (skillRow is null) { Note("неизученных умений нет"); return; }
            var skillName = skillRow.Name;
            vm.LearnSkill(skillRow);
            vm.Refresh();
            var row2 = vm.Skills.FirstOrDefault(r => r.Id == skillRow.Id);
            if (row2 is null) { Bad("умение не появилось в листе"); return; }
            vm.SetSkillAdjustment(row2, 5);
            var entry = vm.Character.FindSkill(skillRow.Id);
            if (entry is null || entry.Adjustment != 5) Bad($"правка умения не сохранилась: {entry?.Adjustment}");
            else if (!vm.AdjustmentLog.Any(a => a.What.Contains("Умение") && a.What.Contains(skillName)))
                Bad("в журнале нет записи о правке умения");
            else Ok($"правка умения «{skillName}» попала в журнал");

            // Талант: мастер убрал.
            var talent = vm.Talents.FirstOrDefault();
            if (talent is null) Note("талантов нет — удаление не проверяем");
            else
            {
                var talentName = talent.Name;
                vm.RemoveTalent(talent.Entry);
                if (!vm.AdjustmentLog.Any(a => a.What.Contains("Талант") && a.What.Contains(talentName)))
                    Bad("в журнале нет записи об удалении таланта");
                else Ok($"удаление таланта «{talentName}» попало в журнал");
            }

            // Снаряжение: мастер убрал предмет.
            var item = vm.Items.FirstOrDefault();
            if (item is null) Note("снаряжения нет — удаление не проверяем");
            else
            {
                vm.RemoveItem(item.Entry);
                if (!vm.AdjustmentLog.Any(a => a.What.Contains("Снаряжение")))
                    Bad("в журнале нет записи об удалении снаряжения");
                else Ok("удаление снаряжения попало в журнал");
            }

            if (vm.AdjustmentLog.Count <= before) Bad("журнал не вырос");
        });

        // ── Кнопка «Новый»: предыстория должна обнуляться ──
        Step("Новый персонаж");
        Try("«Новый» очищает выборы предыстории", () =>
        {
            vm.SelectedHomeworld = pack.Homeworlds.FirstOrDefault();
            vm.SelectedDestiny = pack.Destiny.FirstOrDefault();
            vm.SelectedVoidPath = pack.VoidPath.FirstOrDefault();
            vm.SelectedHardship = pack.Hardship.FirstOrDefault();
            vm.SelectedAspiration = pack.Aspiration.FirstOrDefault();
            vm.SelectedLegacy = pack.Legacy.FirstOrDefault();
            if (vm.SelectedHomeworld is null) { Note("предысторию не выбрали — сброс не проверяем"); return; }

            var notified = new List<string>();
            vm.PropertyChanged += (s, e) => { if (e.PropertyName is not null) notified.Add(e.PropertyName); };
            vm.NewCommand.Execute(null);

            var left = new List<string>();
            if (vm.SelectedHomeworld is not null) left.Add("родной мир");
            if (vm.SelectedDestiny is not null) left.Add("уготованная судьба");
            if (vm.SelectedVoidPath is not null) left.Add("путь в пустоту");
            if (vm.SelectedHardship is not null) left.Add("испытание");
            if (vm.SelectedAspiration is not null) left.Add("устремление");
            if (vm.SelectedLegacy is not null) left.Add("наследие");
            if (left.Count > 0)
                Bad($"после «Новый» остались выборы предыстории: {string.Join(", ", left)}");
            else if (!notified.Contains(nameof(vm.SelectedHomeworld))
                  || !notified.Contains(nameof(vm.SelectedHardship)))
                Bad("интерфейс не уведомлён о сбросе предыстории");
            else Ok("«Новый» очистил предысторию и сообщил интерфейсу");
        });

        // Поиск по умениям не должен пересобирать остальные списки:
        // раньше каждое нажатие клавиши пересоздавало весь лист персонажа.
        Try("поиск по умениям не трогает остальные списки", () =>
        {
            var skillsBefore = vm.Skills.Count;
            var talentsBefore = vm.Talents.Count;
            var psyBefore = vm.PsyAbilities.Count;
            var itemsBefore = vm.Items.Count;

            vm.SkillFilter = "взрыв";
            var filtered = vm.Skills.Count;
            vm.SkillFilter = "";

            if (vm.Talents.Count != talentsBefore)
                Bad("поиск по умениям пересобрал список талантов");
            else if (vm.PsyAbilities.Count != psyBefore)
                Bad("поиск по умениям пересобрал список психических приёмов");
            else if (vm.Items.Count != itemsBefore)
                Bad("поиск по умениям пересобрал список снаряжения");
            else if (filtered > skillsBefore)
                Bad($"поиск по умениям не отфильтровал: было {skillsBefore}, стало {filtered}");
            else
                Ok($"поиск по умениям меняет только свой список (было {skillsBefore}, стало {filtered})");
        });

        // ── Итог ──
        Step("Итог");
        foreach (var w in vm.Warnings) Note($"предупреждение: {w}");
        if (_failures == 0) Ok("все проверки пройдены");
        else Bad($"проблем найдено: {_failures}");

        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(Log.ToString());
        return _failures;
    }

    private static string Origin(string o) => o switch
    {
        "career" => "род занятий", "homeworld" => "родной мир", "destiny" => "судьба",
        "void" => "путь", "hardship" => "испытание", "aspiration" => "устремление",
        "legacy" => "наследие", "species" => "вид", "item" => "снаряжение", "gm" => "вручную",
        _ => o,
    };

    private static string Trim(string s, int n)
    {
        s = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length <= n ? s : s[..n] + "…";
    }
}