using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace RogueTrader.Core.Models;

/// <summary>
/// Сведения о персонаже. Реализует уведомление об изменениях, чтобы правка
/// имени или кампании сразу была видна в шапке окна.
/// </summary>
/// <summary>
/// Запись журнала ручных правок мастера: что изменилось, когда и на сколько.
/// Правки вносятся вручную (бесплатно), поэтому важно, чтобы их было видно.
/// </summary>
public sealed class AdjustmentLogEntry
{
    /// <summary>Когда сделана правка (локальное время).</summary>
    public DateTime When { get; set; } = DateTime.Now;
    /// <summary>Что именно изменено — например, «Характеристика «Воля»».</summary>
    public string What { get; set; } = "";
    /// <summary>Значение до правки.</summary>
    public string Before { get; set; } = "";
    /// <summary>Значение после правки.</summary>
    public string After { get; set; } = "";
}

public sealed class CharacterMeta : INotifyPropertyChanged
{
    private string _name = "";
    private string _custodianName = "";
    private string _homeworldName = "";
    private string _gender = "";
    private string _age = "";
    private string _build = "";
    private string _eyes = "";
    private string _hair = "";
    private string _distinguishingMark = "";
    private string _personality = "";
    private string _birthright = "";
    private string _quote = "";
    private string _traits = "";

    public string Name { get => _name; set => Set(ref _name, value); }
    public string CustodianName { get => _custodianName; set => Set(ref _custodianName, value); }
    public string HomeworldName { get => _homeworldName; set => Set(ref _homeworldName, value); }
    public string Gender { get => _gender; set => Set(ref _gender, value); }
    public string Age { get => _age; set => Set(ref _age, value); }
    public string Build { get => _build; set => Set(ref _build, value); }
    public string Eyes { get => _eyes; set => Set(ref _eyes, value); }
    public string Hair { get => _hair; set => Set(ref _hair, value); }
    public string DistinguishingMark { get => _distinguishingMark; set => Set(ref _distinguishingMark, value); }
    public string Personality { get => _personality; set => Set(ref _personality, value); }
    public string Birthright { get => _birthright; set => Set(ref _birthright, value); }
    public string Quote { get => _quote; set => Set(ref _quote, value); }
    public string Traits { get => _traits; set => Set(ref _traits, value); }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    public string AppVersion { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set(ref string field, string value, [CallerMemberName] string? name = null)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class SkillEntry
{
    public string SkillId { get; set; } = "";
    /// <summary>Улучшения, купленные за опыт: 0 → +0, 1 → +10, 2 → +20.</summary>
    public int Rank { get; set; }
    /// <summary>
    /// «База» на листе. Для умений без характеристики (личных) — их стартовое
    /// значение (обычно 30). Для умений от характеристики база 0, а вклад даёт
    /// сама характеристика.
    /// </summary>
    public int Base { get; set; }
    /// <summary>
    /// «Прочее» — то, что заложено игрой: надбавки от талантов, снаряжения,
    /// жизненного пути. Считается системой, вручную не меняется.
    /// </summary>
    public int Other { get; set; }
    /// <summary>«Правки» — ручная правка мастера. Бесплатно, опыт не тратится.</summary>
    public int Adjustment { get; set; }
    /// <summary>career | homeworld | destiny | void | hardship | aspiration | xp | homebrew.</summary>
    public string Origin { get; set; } = "xp";
    public string Note { get; set; } = "";
}

public sealed class TalentEntry
{
    public string TalentId { get; set; } = "";
    public int Rank { get; set; }
    public string Origin { get; set; } = "xp";
    public string Note { get; set; } = "";
}

public sealed class ItemEntry
{
    public string ItemId { get; set; } = "";
    public string InstanceId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public int Quantity { get; set; } = 1;
    public Dictionary<string, int> Modifiers { get; set; } = [];
    /// <summary>
    /// Качество оружия: low | standard | good | high (базовая книга, стр. 114).
    /// Пусто — «среднее качество», как и указано в таблице.
    /// </summary>
    public string Quality { get; set; } = "";
    public string Note { get; set; } = "";
    public bool Homebrew { get; set; }

    /// <summary>
    /// Предмет, добавленный игроком: лут, талисман, подарок — того, чего нет
    /// в официальном снаряжении. Хранится в самом персонаже, а не в паке:
    /// id предмета при этом пуст.
    /// </summary>
    public ItemDef? Custom { get; set; }
}

/// <summary>
/// Характеристика персонажа — так же, как в колонках «листа персонажа»:
/// <c>База</c> (бросок 2к10+25) + <c>Улучш.</c> (куплено за опыт) +
/// <c>Прочее</c> (порода, таланты, аугметика, правка мастера) = <c>Итог</c>.
/// </summary>
public sealed class CharacteristicEntry
{
    public string CharacteristicId { get; set; } = "";

    /// <summary>«База» — бросок 2к10 + 25. Опыт за неё не тратится.</summary>
    public int Base { get; set; } = 25;

    /// <summary>
    /// «Улучш. (из магазина)» — ОЧКИ, купленные за опыт (0…20, шаг 5).
    /// Только это поле тратит опыт; «База», «Прочее» и «Правки» — бесплатны.
    /// </summary>
    public int AdvancePoints { get; set; }

    /// <summary>
    /// «Правки» — ручные правки мастера (недостаток, излишек, ошибка броска).
    /// Опыт за них не тратится. В колонке «Прочее» вручную ничего не вводится.
    /// </summary>
    public int Adjustment { get; set; }

    /// <summary>
    /// Устаревшее «прочее» из сохранений прежних версий: при загрузке
    /// переносится в «Правки», иначе потерялось бы.
    /// </summary>
    public int Other { get; set; }

    /// <summary>career | homeworld | species | item | gm.</summary>
    public string Origin { get; set; } = "career";

    /// <summary>
    /// Автоматическая часть «Прочего»: изменения характеристик из родного мира,
    /// уготованной судьбы, пути в пустоту, испытаний и устремления.
    /// Пересчитывается при смене варианта пути.
    /// </summary>
    public int PathPoints { get; set; }

    public string Note { get; set; } = "";

    /// <summary>Число купленных уровней улучшения (по 5 очков каждый, максимум 4).</summary>
    [JsonIgnore]
    public int AdvanceLevels
    {
        get => Math.Clamp(AdvancePoints / 5, 0, 4);
        set => AdvancePoints = Math.Clamp(value, 0, 4) * 5;
    }

    /// <summary>Итог по правилам: база + улучшения + правки + автоматическое «прочее».</summary>
    [JsonIgnore]
    public int Total => Math.Max(0, Base + AdvancePoints + Adjustment + PathPoints);
}

/// <summary>Купленное улучшение из таблицы улучшений ранга.</summary>
public sealed class AppliedAdvance
{
    /// <summary>Id опции (careerId#rank#index) — однозначно идентифицирует покупку.</summary>
    public string OptionId { get; set; } = "";
        public string CareerId { get; set; } = "";


    public int Rank { get; set; }
    public int Cost { get; set; }
    public string Note { get; set; } = "";
    /// <summary>Прогресс/уточнение мастера (специализация таланта, конкретный вариант умения).</summary>
    public string? Specialization { get; set; }
}

public sealed class Character
{
    public string SchemaVersion { get; set; } = "2.0";
    public CharacterMeta Meta { get; set; } = new();

    /// <summary>
    /// Журнал ручных правок мастера. Правки бесплатны, но заметны —
    /// по журналу видно, что и когда ведущий менял в персонаже.
    ///
    /// Список сделан «null-безопасным»: в старых файлах персонажа этого поля
    /// нет, а если встретится явный null — приложение не должно падать.
    /// </summary>
    public List<AdjustmentLogEntry> Adjustments
    {
        get => _adjustments ??= [];
        set => _adjustments = value ?? [];
    }
    private List<AdjustmentLogEntry>? _adjustments;

    /// <summary>core | tau.</summary>
    public string System { get; set; } = "core";

    public string SpeciesId { get; set; } = "human";
    public string? CareerId { get; set; }

    /// <summary>
    /// Основной род занятий для альтернативного ранга «любому роду занятий»
    /// («Аугмент», «Ксенограф», «Архиеретех»): от него берутся начальные
    /// умения, таланты и снаряжение.
    /// </summary>
    public string? BaseCareerId { get; set; }
    public string? AlternateRankId { get; set; }
    public string? CasteId { get; set; }

    public List<CharacteristicEntry> Characteristics { get; set; } = [];
    /// <summary>Умения с бонусом +10/+20 (максимум +20 по книге).</summary>
    public List<SkillEntry> Skills { get; set; } = [];
    public List<TalentEntry> Talents { get; set; } = [];
    /// <summary>Снаряжение персонажа. Список «null-безопасный» (см. Adjustments).</summary>
    public List<ItemEntry> Items
    {
        get => _items ??= [];
        set => _items = value ?? [];
    }
    private List<ItemEntry>? _items;
    /// <summary>
    /// Выбор стартового снаряжения: ключ строки (например «career:0») →
    /// «id предмета|качество|количество». Нужен, чтобы при перезагрузке
    /// файла знать, что именно игрок выбрал из вариантов «… или …».
    /// </summary>
    public Dictionary<string, string> StartingEquipmentChoices { get; set; } = [];

    /// <summary>
    /// Строки стартового снаряжения, которые игрок убрал из инвентаря:
    /// их больше не выдаём автоматически.
    /// </summary>
    public List<string> StartingEquipmentRemoved { get; set; } = [];
    /// <summary>Купленные улучшения из таблиц рангов.</summary>
    public List<AppliedAdvance> Advances { get; set; } = [];

    public List<string> HomeworldIds { get; set; } = [];
    public string? ForetoldDestinyId { get; set; }
    public string? VoidPathId { get; set; }
    public List<string> HardshipIds { get; set; } = [];
    public string? AspirationId { get; set; }
    public string? InclinationId { get; set; }
    public string? AscensionId { get; set; }
    public List<string> LegacyIds { get; set; } = [];
    public List<string> ElitePackageIds { get; set; } = [];
    public List<string> Traits
    {
        get => string.IsNullOrEmpty(Meta.Traits)
            ? []
            : Meta.Traits.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList();
        set => Meta.Traits = string.Join(", ", value ?? []);
    }

    /// <summary>
    /// Выбранные варианты разделов предыстории («Сквозь шторм»):
    /// раздел → id варианта. Вариант стоит очков опыта и даёт свои бонусы.
    /// </summary>
    public Dictionary<string, string> PathVariantIds { get; set; } = [];

    /// <summary>Цена выбранных вариантов в очках опыта (вычитается из свободных).</summary>
    public int PathVariantXp { get; set; }

    /// <summary>
    /// Черты, которые даёт выбранный вид (например «Биология долговязых»).
    /// Хранятся отдельно от прочих черт: при смене вида их нужно снять.
    /// </summary>
    public List<string> SpeciesTraits { get; set; } = [];

    /// <summary>
    /// Бросок начальных ранений вида (1к5 и т. п.). Хранится, чтобы при
    /// пересчёте характеристик число ранений не менялось.
    /// </summary>
    public int SpeciesWoundsRoll { get; set; }

    /// <summary>
    /// Бросок для начальных очков судьбы вида (1к10 у круутов и орков).
    /// </summary>
    public int SpeciesFateRoll { get; set; }

    /// <summary>Бросок начальных ранений по жизненному пути (1к5 и т. п.).</summary>
    public int PathWoundsRoll { get; set; }

    /// <summary>Бросок начальных очков судьбы по жизненному пути (1к10).</summary>
    public int PathFateRoll { get; set; }

    // ── Боевое состояние ──
    public int Wounds { get; set; }
    public int FatePoints { get; set; } = 3;
    /// <summary>Размер: 3=Небольшой, 4=Средний, 5=Крупный, 6=Огромный.</summary>
    public int Size { get; set; } = 4;
    public int PsyRating { get; set; }
    /// <summary>Очки безумия (ОБ) — растут от пережитых ужасов (базовая книга, стр. 279).</summary>
    public int Insanity { get; set; }
    /// <summary>Очки порчи (ОП) — растут от Варпа и запретного знания (базовая книга, стр. 281).</summary>
    public int Corruption { get; set; }

    /// <summary>Опыт, начисленный в ходе игры. Входит в общий опыт и доступен для улучшений.</summary>
    public int XpEarned { get; set; }

    // ── Доход и корабль (шаг 5 создания персонажа, таблица 1-5) ──
    /// <summary>Бросок 1к10 по таблице 1-5 (0 — ещё не бросали).</summary>
    public int IncomeRoll { get; set; }
    /// <summary>Модификаторы к уровню дохода, полученные за варианты жизненного пути.</summary>
    public int IncomeModifier { get; set; }
    /// <summary>Правка мастера к уровню дохода.</summary>
    public int IncomeBonus { get; set; }
    /// <summary>Очки корабля, полученные по таблице (можно изменить при покупке судна).</summary>
    public int ShipPoints { get; set; }
    /// <summary>id корпуса из главы VIII.</summary>
    public string? ShipHullId { get; set; }

    // ── Психика и навигатор ──
    /// <summary>Выученные психические приёмы (глава VI).</summary>
    public List<PsyAbilityEntry> PsyAbilities { get; set; } = [];
    /// <summary>Силы навигатора (глава VII) с уровнем владения.</summary>
    public List<NavigatorPowerEntry> NavigatorPowers { get; set; } = [];

    /// <summary>
    /// Какие награды жизненного пути игрок отключил.
    /// Ключ — <c>idВарианта:вид:цель:дельта</c>. Отсутствующий ключ означает «включено»,
    /// поэтому в файле хранится только то, что игрок сознательно выключил.
    /// </summary>
    public Dictionary<string, bool> GrantToggles { get; set; } = [];

    public Dictionary<string, string> Notes { get; set; } = [];
    public HomebrewPack? Homebrew { get; set; }

    public SkillEntry? FindSkill(string id) => Skills.FirstOrDefault(s => s.SkillId == id);
    public TalentEntry? FindTalent(string id) => Talents.FirstOrDefault(t => t.TalentId == id);
    public CharacteristicEntry? FindCharacteristic(string id) =>
        Characteristics.FirstOrDefault(c => c.CharacteristicId == id);

    public AppliedAdvance? FindAdvance(string optionId) =>
        Advances.FirstOrDefault(a => a.OptionId == optionId);

    /// <summary>Сколько раз куплена опция.</summary>
    public int AdvanceCount(string optionId) => Advances.Count(a => a.OptionId == optionId);
}

// ─────────────────────────── Вычисляемые значения ───────────────────────────

public sealed class CharacteristicValue
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Abbrev { get; set; } = "";
    public int Column { get; set; }

    /// <summary>«База» — бросок 2к10+25 (или 25, если бросок ещё не сделан).</summary>
    public int Base { get; set; } = 25;

    /// <summary>«Улучш.» — очки, купленные за опыт (0…20, шаг 5).</summary>
    public int AdvancePoints { get; set; }

    /// <summary>
    /// «Прочее» — что заложено самой игрой: вид, род занятий, аугметика, вещи,
    /// награды жизненного пути. Считается системой, вручную не меняется.
    /// </summary>
    public int Other { get; set; }

    /// <summary>«Правки» — ручные правки мастера. Бесплатно, но вручную.</summary>
    public int Adjustment { get; set; }

    /// <summary>Часть «Прочего», пришедшая от жизненного пути (родной мир, судьба и т. п.).</summary>
    public int Path { get; set; }

    /// <summary>Вклад вида в характеристику (у человека/орка и т. п.).</summary>
    public int FromSpecies { get; set; }
    /// <summary>Вклад снаряжения.</summary>
    public int FromItems { get; set; }

    /// <summary>Итог = База + Улучш. + Прочее + Правки.</summary>
    [JsonIgnore]
    public int Total => Math.Max(0, Base + AdvancePoints + Other + Adjustment);

    /// <summary>Бонус характеристики = полные десятки.</summary>
    [JsonIgnore]
    public int CharBonus => Total / 10;

    /// <summary>Число купленных уровней улучшения.</summary>
    [JsonIgnore] public int AdvanceLevels => AdvancePoints / 5;

    [JsonIgnore] public int MaxAdvancePoints => 20;
    [JsonIgnore] public int AdvancePointsLeft => Math.Max(0, MaxAdvancePoints - AdvancePoints);

    /// <summary>Цена покупки следующих 5 очков улучшения (0 — улучшать нельзя).</summary>
    public int NextAdvanceCost { get; set; }
    public string NextAdvanceName { get; set; } = "";

    [JsonIgnore] public bool CanAdvance => AdvancePoints < MaxAdvancePoints && NextAdvanceCost > 0;
}

/// <summary>Сводка состояния персонажа — пересчитывается правилами.</summary>
public sealed class DerivedStats
{
    public List<CharacteristicValue> Characteristics { get; set; } = [];

    [JsonIgnore] public CharacteristicValue? Strength => Get("str");
    [JsonIgnore] public CharacteristicValue? Toughness => Get("tou");
    [JsonIgnore] public CharacteristicValue? Dexterity => Get("dex");
    [JsonIgnore] public CharacteristicValue? Perception => Get("per");
    [JsonIgnore] public CharacteristicValue? WeaponSkill => Get("ws");
    [JsonIgnore] public CharacteristicValue? BallisticSkill => Get("bs");

    public CharacteristicValue? Get(string id) =>
        Characteristics.FirstOrDefault(c => c.Id == id);

    /// <summary>Порог ранений = 2 × бонус Выносливости.</summary>
    public int WoundThreshold { get; set; }
    public int CurrentWounds { get; set; }
    /// <summary>Сколько ран персонаж способен получить, прежде чем выбыть из строя.</summary>
    [JsonIgnore] public int WoundsLeft => Math.Max(0, WoundThreshold - CurrentWounds);

    public int Size { get; set; } = 4;

    /// <summary>
    /// Скорость по базовой книге: длина дистанций зависит от бонуса Ловкости
    /// (стр. 251, таблица 9-30), а размер этот бонус сдвигает (таблица 14-2).
    /// </summary>
    public int SpeedFactor { get; set; }
    /// <summary>Движение с половинной скоростью, м.</summary>
    public int HalfMovement { get; set; }
    /// <summary>Движение с полной скоростью, м.</summary>
    public int FullMovement { get; set; }
    /// <summary>Натиск, м.</summary>
    public int ChargeMovement { get; set; }
    /// <summary>Бег, м.</summary>
    public int RunMovement { get; set; }
    /// <summary>Бонус от снаряжения к скорости (в наших данных не встречается).</summary>
    public int MovementFromItems { get; set; }
    /// <summary>Полная скорость — так она подписана в сводке и в печатном листе.</summary>
    [JsonIgnore] public int Movement => Math.Max(0, FullMovement + MovementFromItems);

    public int ArmorProtection { get; set; }
    public int ArmorEncumbrance { get; set; }
    public int TotalEncumbrance { get; set; }
    public int FatePoints { get; set; }
    public int PsyRating { get; set; }
    public int Insanity { get; set; }
    public int Corruption { get; set; }

    // ── Уровень дохода и корабль (таблица 1-5) ──
    /// <summary>Уровень дохода, выпавший по таблице 1-5.</summary>
    public int IncomeFromRoll { get; set; }
    [JsonIgnore] public int IncomeLevel { get; set; }
    /// <summary>Очки корабля по таблице 1-5.</summary>
    public int ShipPointsFromRoll { get; set; }
    [JsonIgnore] public int ShipPoints { get; set; }
    [JsonIgnore] public int ShipPointsSpent { get; set; }
    [JsonIgnore] public int ShipPointsLeft { get; set; }
    [JsonIgnore] public IncomeRankDef? IncomeRollRow { get; set; }
    [JsonIgnore] public ShipHullDef? ShipHull { get; set; }

    // Бюджет опыта
    /// <summary>
    /// Всего очков опыта, включая 4500 «врождённых».
    /// Именно по этой сумме определяется ранг (таблица 2-2: 5000…34999).
    /// </summary>
    public int TotalXp { get; set; }
    /// <summary>Врождённый, уже потраченный опыт — его нельзя тратить повторно.</summary>
    public int XpLocked { get; set; }
    /// <summary>Весь доступный опыт: 500 стартовых на создание + начисленный в игре.</summary>
    public int XpSpendableTotal { get; set; }
    /// <summary>Сколько свободного опыта ещё можно потратить.</summary>
    public int XpSpendableLeft { get; set; }
    /// <summary>Куда пошёл свободный опыт.</summary>
    public int XpSpentOnCharacteristics { get; set; }
    public int XpSpentOnAdvances { get; set; }
    /// <summary>Цена выбранных вариантов предыстории («Сквозь шторм»).</summary>
    public int XpSpentOnPathVariants { get; set; }

    /// <summary>Текущий ранг в роде занятий (по таблице 2-2).</summary>
    public int Rank { get; set; } = 1;
    public int RankMin { get; set; }
    public int RankMax { get; set; }
    /// <summary>Сколько опыта не хватает до следующего ранга.</summary>
    public int XpToNextRank { get; set; }

    /// <summary>Всего потрачено сверх врождённых 4500.</summary>
    public int XpSpent => XpSpentOnCharacteristics + XpSpentOnAdvances + XpSpentOnPathVariants;

    public List<string> Warnings { get; set; } = [];
}

/// <summary>Данные пака: встроенного + пользовательских (хоумбрю).</summary>
public sealed class HomebrewPack
{
    public string Id { get; set; } = "homebrew";
    public string Name { get; set; } = "Хоумбрю";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public List<SkillDef> Skills { get; set; } = [];
    public List<TalentDef> Talents { get; set; } = [];
    public List<CareerDef> Careers { get; set; } = [];
    public List<ItemDef> Items { get; set; } = [];
    public List<AugmeticDef> Augmetics { get; set; } = [];
    public List<SpeciesDef> Species { get; set; } = [];
    public List<CasteDef> Castes { get; set; } = [];
    public List<NamedOptionDef> Homeworlds { get; set; } = [];
    public List<NamedOptionDef> Destiny { get; set; } = [];
    public List<NamedOptionDef> VoidPath { get; set; } = [];
    public List<NamedOptionDef> Hardship { get; set; } = [];
    public List<NamedOptionDef> Aspiration { get; set; } = [];
    public List<NamedOptionDef> Legacy { get; set; } = [];
    public List<NamedOptionDef> Inclinations { get; set; } = [];
    public List<NamedOptionDef> Ascension { get; set; } = [];
    public List<NamedOptionDef> ElitePackages { get; set; } = [];
    public List<WoundDef> Wounds { get; set; } = [];
}