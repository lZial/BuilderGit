using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using RogueTrader.App.Services;
using RogueTrader.App.Views;
using RogueTrader.Core.Data;
using RogueTrader.Core.Models;
using RogueTrader.Core.Rules;

namespace RogueTrader.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly DataPackService _svc = new();
    private RulesEngine _engine = null!;
    private Character _ch = new();
    private DerivedStats _stats = new();
    private string? _filePath;

    // ── публичное состояние ──
    public Character Character
    {
        get => _ch;
        set
        {
            _ch = value;
            // Модель сменилась (загрузка файла или «Новый»): все привязки вида
            // Character.* должны перечитать новый объект, а закешированные
            // выборы — сброситься, чтобы не остались от прежнего персонажа.
            OnPropertyChanged(nameof(Character));
            ResetSelections();
            RefreshStartingEquipment();
            Refresh();
        }
    }
    public DerivedStats Stats => _stats;
    public DataPackService Svc => _svc;
    public RulesEngine Engine => _engine;

    public string FilePath => _filePath ?? "(не сохранён)";
    public string FileName => _filePath is null ? "" : Path.GetFileName(_filePath);
    public bool HasUnsavedChanges { get; private set; }

    public ObservableCollection<CharacteristicRow> Characteristics { get; } = [];
    public ObservableCollection<SkillSheetRow> Skills { get; } = [];

    /// <summary>Награды от вариантов жизненного пути с переключателями включения.</summary>
    public ObservableCollection<PathGrantRow> PathGrants { get; } = [];

    /// <summary>Стартовое снаряжение вида и рода занятий — с выбором вариантов.</summary>
    public ObservableCollection<StartingEquipmentRow> StartingEquipment { get; } = [];

    /// <summary>Показывать в листе умений только те, что персонаж уже изучил.</summary>
    public bool SkillsOnlyLearned
    {
        get => _skillsOnlyLearned;
        set { if (Set(ref _skillsOnlyLearned, value)) Refresh(); }
    }
    private bool _skillsOnlyLearned;

    /// <summary>Фильтр по названию умения.</summary>
    public string SkillFilter
    {
        get => _skillFilter;
        // Пересобираем только лист умений: полный Refresh() на каждое нажатие
        // клавиши пересобирал все списки приложения и подвисал интерфейс.
        set { if (Set(ref _skillFilter, value)) SyncSkillRows(); }
    }
    private string _skillFilter = "";

    /// <summary>Все умения дата-пака — лист персонажа показывает их целиком.</summary>
    public IEnumerable<SkillDef> AllSkillDefs => _svc.AllSkills;

    /// <summary>Все таланты дата-пака.</summary>
    public IEnumerable<TalentDef> AllTalentDefs => _svc.AllTalents;

    /// <summary>Заголовки колонок характеристик для листа умений.</summary>
    public IReadOnlyList<CharacteristicHeader> CharacteristicHeaders { get; private set; } = [];

    /// <summary>Откуда получено умение или талант.</summary>
    public static string OriginRu(string origin) => origin switch
    {
        "career" => "род занятий",
        "homeworld" => "родной мир",
        "destiny" => "уготованная судьба",
        "void" => "путь в пустоту",
        "hardship" => "испытание",
        "aspiration" => "устремление",
        "legacy" => "наследие",
        "species" => "вид",
        "item" => "снаряжение",
        "gm" => "мастер",
        _ => "вручную",
    };
    public ObservableCollection<TalentRow> Talents { get; } = [];
    public ObservableCollection<ItemRow> Items { get; } = [];

    /// <summary>
    /// Список снаряжения, разложенный по главе «Арсенал»: сначала раздел
    /// (оружие, боеприпасы, доспехи, снаряжение, кибернетика), внутри —
    /// подраздел книги (лазерное, стабберное, болтерное…) и уже в нём предметы.
    /// </summary>
    public ICollectionView ItemView { get; }

    /// <summary>
    /// Свои предметы — лут, талисманы, подарки. Они лежат в персонаже, а не
    /// в паке, и показаны на отдельной вкладке, чтобы не мешать в списке
    /// снаряжения из книги.
    /// </summary>
    public ICollectionView CustomItemView { get; }

    /// <summary>Поиск по названию и описанию предмета.</summary>
    public string ItemFilter
    {
        get => _itemFilter;
        set
        {
            if (Set(ref _itemFilter, value)) ItemView.Refresh();
        }
    }
    private string _itemFilter = "";

    public string ItemFilterHint => string.IsNullOrWhiteSpace(_itemFilter)
        ? "Всего предметов: {0}"
        : "Найдено: {0}";

    public int VisibleItemCount => ItemView.IsEmpty ? 0 : ItemView.Cast<object>().Count();
    public int TotalItemCount => Character.Items.Count(e => e.Custom is null);
    /// <summary>Сколько своих предметов у персонажа.</summary>
    public int CustomItemCount => Character.Items.Count(e => e.Custom is not null);
    /// <summary>Есть ли свои предметы — иначе показываем подсказку.</summary>
    public bool HasCustomItems => CustomItemCount > 0;
    public ObservableCollection<AdvanceRow> AvailableAdvances { get; } = [];
    public ObservableCollection<AdvanceRow> BoughtAdvances { get; } = [];

    /// <summary>Есть ли купленные улучшения — иначе колонка с ними скрыта.</summary>
    public bool HasBoughtAdvances => BoughtAdvances.Count > 0;
    public ObservableCollection<string> Warnings { get; } = [];
    public ObservableCollection<Choice<SpeciesDef?>> SpeciesChoices { get; } = [];
    public ObservableCollection<Choice<CareerDef?>> CareerChoices { get; } = [];
    public ObservableCollection<Choice<NamedOptionDef?>> HomeworldChoices { get; } = [];
    public ObservableCollection<Choice<NamedOptionDef?>> DestinyChoices { get; } = [];
    public ObservableCollection<Choice<NamedOptionDef?>> VoidPathChoices { get; } = [];
    public ObservableCollection<Choice<NamedOptionDef?>> HardshipChoices { get; } = [];
    public ObservableCollection<Choice<NamedOptionDef?>> AspirationChoices { get; } = [];
    public ObservableCollection<Choice<NamedOptionDef?>> LegacyChoices { get; } = [];

    public string PackSummary => _svc.Report.Summary;

    /// <summary>Версия программы — из свойств сборки.</summary>
    public string VersionText
    {
        get
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var v = asm.GetName().Version;
            return $"Rogue Trader Builder {v?.Major}.{v?.Minor}.{v?.Build}";
        }
    }

    // ── двусторонние выборы, к которым привязан UI ──
    private CareerDef? _selectedCareer;
    public CareerDef? SelectedCareer
    {
        get => _selectedCareer ?? _svc.Pack.Career(_ch.CareerId);
        set
        {
            if (!Set(ref _selectedCareer, value)) return;
            OnPropertyChanged(nameof(SelectedCareerBonuses));
            OnPropertyChanged(nameof(HasSelectedCareer));
            SetCareer(value);
        }
    }

    /// <summary>Выбран ли род занятий — без него список улучшений пуст.</summary>
    public bool HasSelectedCareer => SelectedCareer is not null;

    /// <summary>
    /// Основной род занятий для альтернативного ранга «любому роду занятий»
    /// («Аугмент», «Ксенограф», «Архиеретех»): от него берутся начальные
    /// умения, таланты и снаряжение.
    /// </summary>
    private CareerDef? _selectedBaseCareer;
    public CareerDef? SelectedBaseCareer
    {
        get => _selectedBaseCareer ?? _svc.Pack.Career(_ch.BaseCareerId);
        set
        {
            if (!Set(ref _selectedBaseCareer, value)) return;
            _ch.BaseCareerId = value?.Id ?? "";
            if (SelectedCareer is { AnyBaseCareer: true })
            {
                ApplyStarting(SelectedCareer);
                RefreshStartingEquipment();
                Refresh();
            }
            OnPropertyChanged(nameof(SelectedBaseCareer));
        }
    }

    /// <summary>Подходящие роды занятий для выбранного альтернативного ранга.</summary>
    public ObservableCollection<Choice<CareerDef?>> BaseCareerChoices { get; } = [];

    /// <summary>Нужен ли выбор основного рода занятий.</summary>
    public bool HasBaseCareerChoice =>
        SelectedCareer is { AnyBaseCareer: true } && BaseCareerChoices.Count > 0;

    /// <summary>
    /// Список подходящих родов занятий — по книге: «любой, кроме орка и
    /// круута» и подобные.
    /// </summary>
    private void SyncBaseCareerChoices()
    {
        BaseCareerChoices.Clear();
        var career = SelectedCareer;
        if (career is not { AnyBaseCareer: true }) return;
        foreach (var c in _svc.AllCareers)
        {
            if (c.Id == career.Id || c.AlternateRanks) continue;
            if (career.ExcludedBaseCareers.Contains(c.Id)) continue;
            if (career.HumanBaseCareerOnly && c.Source != "core") continue;
            BaseCareerChoices.Add(new Choice<CareerDef?>
            {
                Value = c,
                Label = c.Name,
                Description = c.Description,
                Bonuses = CareerBonuses(c),
            });
        }
        if (_selectedBaseCareer is not null && BaseCareerChoices.All(x => x.Value != _selectedBaseCareer))
            _selectedBaseCareer = null;
        OnPropertyChanged(nameof(HasBaseCareerChoice));
        OnPropertyChanged(nameof(SelectedBaseCareer));
    }

    /// <summary>Выбран ли родной мир — иначе предыстория не показана.</summary>
    public bool HasSelectedHomeworld => SelectedHomeworld is not null;

    /// <summary>Бонусы выбранного рода занятий — как в книге.</summary>
    public string SelectedCareerBonuses => SelectedCareer is null ? "" : CareerBonuses(SelectedCareer);

    private SpeciesDef? _selectedSpecies;
    public SpeciesDef? SelectedSpecies
    {
        get => _selectedSpecies ?? _svc.Pack.SpeciesOf(_ch.SpeciesId);
        set
        {
            if (!Set(ref _selectedSpecies, value)) return;
            OnPropertyChanged(nameof(SelectedSpeciesBonuses));
            _ch.SpeciesId = value?.Id ?? "human";
            ApplySpecies(value);
            MarkDirty();
        }
    }

    /// <summary>Бонусы выбранного вида — характеристики, умения, таланты, черты.</summary>
    public string SelectedSpeciesBonuses => SelectedSpecies is null ? "" : SpeciesBonuses(SelectedSpecies);

    /// <summary>
    /// Вид задаёт размер персонажа и форму броска характеристик.
    /// Например, у долговязых в «Руководстве по абхуманам» напечатана черта
    /// «Размер (Hulking)» — значит, размер Крупный (5), а не Средний (4).
    /// </summary>
    private void ApplySpecies(SpeciesDef? species)
    {
        if (species is null) return;

        // Размер: как в книге, а если черты нет — обычный средний.
        var size = species.Size > 0 ? species.Size : 4;
        _ch.Size = size;
        _selectedSize = size;

        // Постоянная часть броска: у тау своя для каждой характеристики.
        foreach (var e in Character.Characteristics)
        {
            var b = species.CharacteristicBase.GetValueOrDefault(e.CharacteristicId);
            if (b > 0) e.Base = b;
        }

        // Умения и таланты, что даёт сама раса.
        Character.Skills.RemoveAll(s => s.Origin == "species");
        Character.Talents.RemoveAll(t => t.Origin == "species");
        foreach (var id in species.Skills)
            if (Character.FindSkill(id) is null)
                Character.Skills.Add(new SkillEntry { SkillId = id, Origin = "species" });
        foreach (var id in species.Talents)
            if (Character.FindTalent(id) is null)
                Character.Talents.Add(new TalentEntry { TalentId = id, Origin = "species" });

        // Черты вида («Starting Traits»): у долговязых — «Биология долговязых»,
        // «Неимперский», «Не совсем человек», «Размер (Hulking)», «Привычный к пустоте».
        Character.SpeciesTraits.Clear();
        foreach (var t in species.Traits)
        {
            var name = t.Trim('«', '»', '"', ' ', '.');
            if (name.Length > 0) Character.SpeciesTraits.Add(name);
        }

        // Очки судьбы вида: у абхуманов их меньше, чем у человека, а у круутов
        // и орков число определяется броском 1к10.
        if (species.StartingFate is { } sf && sf.Dice > 0)
        {
            Character.SpeciesFateRoll = Random.Shared.Next(1, sf.Dice + 1);
            Character.FatePoints = sf.PointsFor(Character.SpeciesFateRoll);
        }
        else if (species.StartingFatePoints > 0) Character.FatePoints = species.StartingFatePoints;
        else Character.SpeciesFateRoll = 0;

        // Начальные ранения: бросок вида (1к5 и т. п.) — один раз, при выборе вида.
        Character.SpeciesWoundsRoll = species.StartingWounds is { Dice: > 0 } sw
            ? Random.Shared.Next(1, sw.Dice + 1)
            : 0;

        RefreshStartingEquipment();
    }

    /// <summary>Пояснение к размеру: как он задан у выбранного вида.</summary>
    public string SizeHint
    {
        get
        {
            var sp = SelectedSpecies;
            if (sp is null) return "";
            var size = sp.Size > 0 ? sp.Size : 4;
            var head = $"Размер по виду: {Core.Rules.RulesEngine.SizeRu(size)}";
            if (string.IsNullOrWhiteSpace(sp.SizeNote)) return head;
            return $"{head} — {sp.SizeNote}";
        }
    }

    // ═════════════ Скорость — как в листе персонажа ═════════════

    /// <summary>Бонус Силы: от него считаются прыжки в длину и высоту.</summary>
    public int StrBonus => _stats.Get("str")?.CharBonus ?? 0;

    /// <summary>Движение с половинной скоростью — по базовой книге (бонус Ловкости × 1).</summary>
    public string HalfMoveText => $"{Stats.HalfMovement} м";
    /// <summary>Движение с полной скоростью (бонус Ловкости × 2).</summary>
    public string FullMoveText => $"{Stats.FullMovement} м";
    /// <summary>Натиск (бонус Ловкости × 3).</summary>
    public string ChargeMoveText => $"{Stats.ChargeMovement} м";
    /// <summary>Бег (бонус Ловкости × 6).</summary>
    public string RunMoveText => $"{Stats.RunMovement} м";
    /// <summary>Прыжок в длину: столько метров — бонус Силы (лист персонажа: БС × 1 м).</summary>
    public string LongJumpText => $"{StrBonus} м";
    /// <summary>Прыжок в высоту: бонус Силы × 20 см (лист персонажа: БС × 20 см).</summary>
    public string HighJumpText => Metres(StrBonus * 0.2);

    /// <summary>Метры с запятой вместо точки: 0,6 м.</summary>
    private static string Metres(double value) =>
        $"{value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')} м";

    /// <summary>Как считается скорость — для подсказки над панелью.</summary>
    public string SpeedRuleText =>
        $"Скорость по базовой книге: длина дистанций зависит от бонуса Ловкости "
        + $"(сейчас +{Stats.SpeedFactor} с учётом размера). Половинная — ×1, полная — ×2, натиск — ×3, бег — ×6.";

    public string SpeedNote =>
        "Скорость берётся по размеру персонажа; натиск — полная скорость плюс 3 м, бег — двойная скорость. "
        + "Подсказки в скобках — как напечатаны в листе персонажа: БЛ — бонус Ловкости, БС — бонус Силы.";

    public string LongJumpTip =>
        "Прыжок в длину: на расстояние, равное бонусу Силы, плюс по 0,5 м за каждую дополнительную ступень успеха "
        + "(базовая книга, стр. 248). В листе персонажа подсказка такая же: (БС × 1 м).";

    public string HighJumpTip =>
        "Прыжок в высоту с места — не больше 0,5 м; с разбега — половина бонуса Силы плюс по 0,5 м за ступень успеха "
        + "(базовая книга, стр. 248). В листе персонажа: (БС × 20 см).";

    /// <summary>Особенности выбранного вида — из книги (у тау это «Во имя Высшего блага!» и другие).</summary>
    public ObservableCollection<string> SpeciesRules { get; } = [];

    /// <summary>Есть ли у вида особенности, которые стоит показать.</summary>
    public bool HasSpeciesInfo => SpeciesRules.Count > 0;

    /// <summary>Черты вида («Starting Traits») — выдаются персонажу при выборе вида.</summary>
    public ObservableCollection<string> SpeciesTraits { get; } = [];

    public bool HasSpeciesTraits => SpeciesTraits.Count > 0;

    /// <summary>
    /// Начальные ранения вида: формула из книги и результат. У человека —
    /// бонус Выносливости × 2, у долговязых — «1к5 + 2 × бонус Выносливости».
    /// </summary>
    public string StartingWoundsText
    {
        get
        {
            var sp = SelectedSpecies;
            if (sp is null) return "";
            var sw = sp.StartingWounds;
            if (sw is null)
            {
                // У человека формулу задаёт родной мир: «2 × бонус Выносливости
                // + 1к5+1» («Изначальное количество ран» базовой книги).
                var pw = PathStartingOption()?.StartingWounds;
                if (pw is not null)
                    return $"Начальные ранения: {pw.Text} → {Stats.WoundThreshold}";
                if (sp.Id == "human")
                    return "Начальные ранения: 2 × бонус Выносливости + 1к5 (по родному миру)";
                // У зверей общих бонусов нет: всё даёт подраса вида.
                return "Начальные ранения: по подрасе вида";
            }
            var roll = Character.SpeciesWoundsRoll;
            var formula = string.IsNullOrWhiteSpace(sw.Text) ? "по книге" : sw.Text;
            return $"Начальные ранения: {formula} → {Stats.WoundThreshold}";
        }
    }

    /// <summary>Начальные очки судьбы вида (у тау их определяет ведущий).</summary>
    public string StartingFatePointsText
    {
        get
        {
            var sp = SelectedSpecies;
            if (sp is null) return "";
            if (sp.StartingFate is { } sf)
            {
                var text = string.IsNullOrWhiteSpace(sf.Text) ? "бросок 1к10" : sf.Text;
                return $"Очки судьбы при создании: {text} → {Character.FatePoints}";
            }
            if (sp.StartingFatePoints > 0) return $"Очки судьбы при создании: {sp.StartingFatePoints}";
            // У человека (и большинства видов) очки судьбы на создание не заданы
            // видом: их задаёт родной мир («Изначальное количество очков судьбы»).
            var pf = PathStartingOption()?.StartingFate;
            if (pf is not null)
            {
                var text = string.IsNullOrWhiteSpace(pf.Text) ? "бросок 1к10" : pf.Text;
                return $"Очки судьбы при создании: {text} → {Character.FatePoints}";
            }
            return $"Очки судьбы при создании: {Character.FatePoints} (по умолчанию)";
        }
    }

    /// <summary>
    /// Вариант жизненного пути, задающий начальные ранения и очки судьбы:
    /// в книге это первая строка таблицы путей — родной мир.
    /// </summary>
    private NamedOptionDef? PathStartingOption() =>
        PathSources().Select(s => s.Def)
            .FirstOrDefault(d => d is not null && (d.StartingWounds is not null || d.StartingFate is not null));

    public bool HasStartingInfo =>
        !string.IsNullOrWhiteSpace(StartingWoundsText) || !string.IsNullOrWhiteSpace(StartingFatePointsText);

    private void SyncSpeciesRules()
    {
        SpeciesRules.Clear();
        var sp = SelectedSpecies;
        if (sp is not null)
        {
            if (!string.IsNullOrWhiteSpace(sp.Description)) SpeciesRules.Add(sp.Description);
            foreach (var r in sp.SpecialRules) SpeciesRules.Add(r);
        }
        OnPropertyChanged(nameof(HasSpeciesInfo));

        SpeciesTraits.Clear();
        if (sp is not null)
            foreach (var t in sp.Traits) SpeciesTraits.Add(t.Trim('«', '»', '"', ' ', '.'));
        OnPropertyChanged(nameof(HasSpeciesTraits));
        OnPropertyChanged(nameof(StartingWoundsText));
        OnPropertyChanged(nameof(StartingFatePointsText));
        OnPropertyChanged(nameof(HasStartingInfo));
    }

    private int _selectedSize = 4;
    public int SelectedSize
    {
        get => _selectedSize;
        set { if (Set(ref _selectedSize, value)) { _ch.Size = value; MarkDirty(); } }
    }

    public ObservableCollection<Choice<int>> SizeChoices { get; } = [];
    private void FillSizeChoices()
    {
        SizeChoices.Clear();
        for (var i = 3; i <= 6; i++)
        {
            // Размер сдвигает бонус Ловкости, а значит и скорость (таблица 14-2).
            var mod = _svc.Pack.MovementRules.ModifierForSize(i);
            var hint = mod == 0 ? "без сдвига скорости" : $"{mod:+#;-#} к скорости";
            SizeChoices.Add(new Choice<int> { Value = i, Label = Core.Rules.RulesEngine.SizeRu(i), Hint = hint });
        }
    }

    private NamedOptionDef? _selectedHomeworld;
    public NamedOptionDef? SelectedHomeworld
    {
        get => _selectedHomeworld ?? FindIn(_svc.Pack.Homeworlds, _ch.HomeworldIds);
        set
        {
            if (!Set(ref _selectedHomeworld, value)) return;
            _ch.HomeworldIds = value is null ? [] : [value.Id];
            ApplyPathGrants();
            MarkDirty();
        }
    }

    private NamedOptionDef? _selectedDestiny;
    public NamedOptionDef? SelectedDestiny
    {
        get => _selectedDestiny ?? GamePack.Find(_svc.Pack.Destiny, _ch.ForetoldDestinyId);
        set
        {
            if (!Set(ref _selectedDestiny, value)) return;
            _ch.ForetoldDestinyId = value?.Id;
            SyncVariants();
            ApplyPathGrants();
            MarkDirty();
        }
    }

    private NamedOptionDef? _selectedVoidPath;
    public NamedOptionDef? SelectedVoidPath
    {
        get => _selectedVoidPath ?? GamePack.Find(_svc.Pack.VoidPath, _ch.VoidPathId);
        set
        {
            if (!Set(ref _selectedVoidPath, value)) return;
            _ch.VoidPathId = value?.Id;
            SyncVariants();
            ApplyPathGrants();
            MarkDirty();
        }
    }

    private NamedOptionDef? _selectedHardship;
    public NamedOptionDef? SelectedHardship
    {
        get => _selectedHardship ?? FindIn(_svc.Pack.Hardship, _ch.HardshipIds);
        set
        {
            if (!Set(ref _selectedHardship, value)) return;
            _ch.HardshipIds = value is null ? [] : [value.Id];
            SyncVariants();
            ApplyPathGrants();
            MarkDirty();
        }
    }

    private NamedOptionDef? _selectedAspiration;
    public NamedOptionDef? SelectedAspiration
    {
        get => _selectedAspiration ?? GamePack.Find(_svc.Pack.Aspiration, _ch.AspirationId);
        set
        {
            if (!Set(ref _selectedAspiration, value)) return;
            _ch.AspirationId = value?.Id;
            SyncVariants();
            ApplyPathGrants();
            MarkDirty();
        }
    }

    private NamedOptionDef? _selectedLegacy;
    public NamedOptionDef? SelectedLegacy
    {
        get => _selectedLegacy ?? FindIn(_svc.Pack.Legacy, _ch.LegacyIds);
        set
        {
            if (!Set(ref _selectedLegacy, value)) return;
            _ch.LegacyIds = value is null ? [] : [value.Id];
            SyncVariants();
            ApplyPathGrants();
            MarkDirty();
        }
    }

    // ── Варианты разделов предыстории («Сквозь шторм») ──
    //
    // В книге у уготованной судьбы, пути в пустоту, испытаний, устремлений и
    // наследий есть по три варианта: свой текст, своя цена в очках опыта и
    // свои бонусы. Выбирается один вариант на раздел.

    public ObservableCollection<Choice<PathVariantDef?>> DestinyVariantChoices { get; } = [];
    public ObservableCollection<Choice<PathVariantDef?>> VoidPathVariantChoices { get; } = [];
    public ObservableCollection<Choice<PathVariantDef?>> HardshipVariantChoices { get; } = [];
    public ObservableCollection<Choice<PathVariantDef?>> AspirationVariantChoices { get; } = [];
    public ObservableCollection<Choice<PathVariantDef?>> LegacyVariantChoices { get; } = [];

    private readonly Dictionary<string, PathVariantDef?> _variants = [];

    /// <summary>Пустой вариант: в книге он не выбран, игрок решает сам.</summary>
    private static readonly PathVariantDef NoVariant = new() { Id = "", Name = "— вариант не выбран —" };

    public PathVariantDef? SelectedDestinyVariant
    {
        get => GetVariant("destiny");
        set => SetVariant("destiny", value, nameof(SelectedDestinyVariant));
    }

    public PathVariantDef? SelectedVoidPathVariant
    {
        get => GetVariant("void");
        set => SetVariant("void", value, nameof(SelectedVoidPathVariant));
    }

    public PathVariantDef? SelectedHardshipVariant
    {
        get => GetVariant("hardship");
        set => SetVariant("hardship", value, nameof(SelectedHardshipVariant));
    }

    public PathVariantDef? SelectedAspirationVariant
    {
        get => GetVariant("aspiration");
        set => SetVariant("aspiration", value, nameof(SelectedAspirationVariant));
    }

    public PathVariantDef? SelectedLegacyVariant
    {
        get => GetVariant("legacy");
        set => SetVariant("legacy", value, nameof(SelectedLegacyVariant));
    }

    /// <summary>Есть ли у выбранного раздела варианты («Сквозь шторм»).</summary>
    public bool HasDestinyVariants => SelectedDestiny?.Variants.Count > 0;
    public bool HasVoidPathVariants => SelectedVoidPath?.Variants.Count > 0;
    public bool HasHardshipVariants => SelectedHardship?.Variants.Count > 0;
    public bool HasAspirationVariants => SelectedAspiration?.Variants.Count > 0;
    public bool HasLegacyVariants => SelectedLegacy?.Variants.Count > 0;

    private PathVariantDef? GetVariant(string key) =>
        _variants.TryGetValue(key, out var v) ? v : NoVariant;

    private void SetVariant(string key, PathVariantDef? value, string prop)
    {
        var v = value is null || value.Id.Length == 0 ? NoVariant : value;
        if (ReferenceEquals(GetVariant(key), v)) return;
        _variants[key] = v;
        if (v.Id.Length == 0) _ch.PathVariantIds.Remove(key);
        else _ch.PathVariantIds[key] = v.Id;
        OnPropertyChanged(prop);
        UpdateVariantXp();
        ApplyPathGrants();
        MarkDirty();
    }

    /// <summary>true, пока идёт пересборка списков вариантов.</summary>
    private bool _syncingVariants;

    /// <summary>Пересобирает списки вариантов под выбранные разделы пути.</summary>
    private void SyncVariants()
    {
        // Пересборка вызывает событие выбора, а оно снова зовёт пересборку:
        // без защиты варианты попадают в список дважды.
        if (_syncingVariants) return;
        _syncingVariants = true;
        try
        {
            FillVariantsSync();
        }
        finally
        {
            _syncingVariants = false;
        }
    }

    private void FillVariantsSync()
    {
        FillVariants("destiny", DestinyVariantChoices, SelectedDestiny);
        FillVariants("void", VoidPathVariantChoices, SelectedVoidPath);
        FillVariants("hardship", HardshipVariantChoices, SelectedHardship);
        FillVariants("aspiration", AspirationVariantChoices, SelectedAspiration);
        FillVariants("legacy", LegacyVariantChoices, SelectedLegacy);
        UpdateVariantXp();
        OnPropertyChanged(nameof(HasDestinyVariants));
        OnPropertyChanged(nameof(HasVoidPathVariants));
        OnPropertyChanged(nameof(HasHardshipVariants));
        OnPropertyChanged(nameof(HasAspirationVariants));
        OnPropertyChanged(nameof(HasLegacyVariants));
    }

    private void FillVariants(string key, ObservableCollection<Choice<PathVariantDef?>> list, NamedOptionDef? option)
    {
        // Сохранённый id заранее: очистка списка сбрасывает и выбор в списке,
        // а значит и запись в персонаже.
        var saved = _ch.PathVariantIds.GetValueOrDefault(key) ?? "";
        list.Clear();
        // Пустой пункт: вариант выбирает игрок, книга его не назначает.
        list.Add(new Choice<PathVariantDef?> { Value = NoVariant, Label = NoVariant.Name });
        foreach (var v in option?.Variants ?? [])
            list.Add(new Choice<PathVariantDef?>
            {
                Value = v,
                Label = v.CostXp > 0 ? $"{v.Name} — {v.CostXp} ОО" : v.Name,
                Description = v.Description,
                Bonuses = v.Effect,
            });
        // Автовыбора нет: берётся только тот вариант, что сохранён у персонажа.
        var chosen = list.FirstOrDefault(c => c.Value?.Id == saved && saved.Length > 0)?.Value ?? NoVariant;
        _variants[key] = chosen;
        if (chosen.Id.Length == 0) _ch.PathVariantIds.Remove(key);
        else _ch.PathVariantIds[key] = chosen.Id;
        OnPropertyChanged(VariantProperty(key));
    }

    private static string VariantProperty(string key) => key switch
    {
        "destiny" => nameof(SelectedDestinyVariant),
        "void" => nameof(SelectedVoidPathVariant),
        "hardship" => nameof(SelectedHardshipVariant),
        "aspiration" => nameof(SelectedAspirationVariant),
        _ => nameof(SelectedLegacyVariant),
    };

    /// <summary>Цена выбранных вариантов в очках опыта — вычитается из свободных.</summary>
    private void UpdateVariantXp() =>
        _ch.PathVariantXp = _variants.Values
            .Where(v => v is not null && v.Id.Length > 0)
            .Sum(v => v!.CostXp);

    private static NamedOptionDef? FindIn(List<NamedOptionDef> list, List<string> ids) =>
        ids.Count > 0 ? list.FirstOrDefault(o => o.Id == ids[0]) : null;

    /// <summary>Сброс кэшированных выборов после перезагрузки персонажа.</summary>
    public void ResetSelections()
    {
        _selectedCareer = null; _selectedSpecies = null; _selectedHomeworld = null;
        _selectedDestiny = null; _selectedVoidPath = null; _selectedHardship = null;
        _selectedAspiration = null; _selectedLegacy = null;
        _variants.Clear();
        // Плашки выбора должны показать пустое значение, а не прежнее:
        // без этого выпадающие списки остаются от прошлого персонажа.
        OnPropertyChanged(nameof(SelectedCareer));
        OnPropertyChanged(nameof(SelectedSpecies));
        OnPropertyChanged(nameof(SelectedHomeworld));
        OnPropertyChanged(nameof(SelectedDestiny));
        OnPropertyChanged(nameof(SelectedVoidPath));
        OnPropertyChanged(nameof(SelectedHardship));
        OnPropertyChanged(nameof(SelectedAspiration));
        OnPropertyChanged(nameof(SelectedLegacy));
        OnPropertyChanged(nameof(SelectedBaseCareer));
        OnPropertyChanged(nameof(SelectedInclination));
        OnPropertyChanged(nameof(SelectedPathId));
        OnPropertyChanged(nameof(SelectedDestinyVariant));
        OnPropertyChanged(nameof(SelectedVoidPathVariant));
        OnPropertyChanged(nameof(SelectedHardshipVariant));
        OnPropertyChanged(nameof(SelectedAspirationVariant));
        OnPropertyChanged(nameof(SelectedLegacyVariant));
    }

    // ── команды ──
    public RelayCommand NewCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsCommand { get; }
    public RelayCommand ExportPdfCommand { get; }
    public RelayCommand ExportMarkdownCommand { get; }
    public RelayCommand ReloadPackCommand { get; }
    public RelayCommand AddSkillCommand { get; }
    public RelayCommand AddTalentCommand { get; }
    public RelayCommand AddItemCommand { get; }
    public RelayCommand AddCustomItemCommand { get; }
    public RelayCommand RollCharacteristicsCommand { get; }
    public RelayCommand RollIncomeCommand { get; }
    public RelayCommand OpenDataFolderCommand { get; }

    public MainViewModel()
    {
        _svc.Load();
        _engine = new RulesEngine(_svc.Pack);

        // Группировка по главе «Арсенал»: раздел → подраздел → предмет.
        // WPF строит вложенность сама, по порядку описаний.
        ItemView = CollectionViewSource.GetDefaultView(Items);
        ItemView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ItemRow.SectionTitle)));
        ItemView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ItemRow.GroupTitle)));
        // Свои предметы — на отдельной вкладке, в книжном списке их нет.
        ItemView.Filter = o => o is ItemRow row ? row.Entry.Custom is null : true;

        // Для своей вкладки нужен ОТДЕЛЬНЫЙ объект просмотра:
        // CollectionViewSource.GetDefaultView для одного и того же списка
        // возвращает один и тот же вид, и второй фильтр затирал бы первый.
        CustomItemView = new System.Windows.Data.ListCollectionView(Items)
        {
            Filter = o => o is ItemRow row2 && row2.Entry.Custom is not null,
        };

        // Психические приёмы: группируются по дисциплинам и древам приёмов
        PsyView = CollectionViewSource.GetDefaultView(PsyAbilities);
        // Группируем только по дисциплине: заголовок дерева между приёмами
        // путал — при прокрутке казалось, что «появилась следующая дисциплина».
        // Дерево приёмов теперь подписано в самой карточке.
        PsyView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PsyRow.Discipline)));

        NewCommand = new RelayCommand(() => { _filePath = null; Character = CreateNew(); });
        OpenCommand = new RelayCommand(DoOpen);
        SaveCommand = new RelayCommand(DoSave);
        SaveAsCommand = new RelayCommand(DoSaveAs);
        ExportPdfCommand = new RelayCommand(DoExportPdf);
        ExportMarkdownCommand = new RelayCommand(DoExportMarkdown);
        ReloadPackCommand = new RelayCommand(() => { _svc.Load(); _engine = new RulesEngine(_svc.Pack); FillChoices(); Refresh(); });
        AddSkillCommand = new RelayCommand(() => PickSkill());
        AddTalentCommand = new RelayCommand(() => PickTalent());
        AddItemCommand = new RelayCommand(() => PickItem());
        AddCustomItemCommand = new RelayCommand(() => AddCustomItem());
        RollCharacteristicsCommand = new RelayCommand(RollCharacteristics);
        RollIncomeCommand = new RelayCommand(RollIncome);
        OpenDataFolderCommand = new RelayCommand(() =>
        {
            AppPaths.EnsureDirectories();
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppPaths.UserPackDirectory) { UseShellExecute = true }); } catch { }
        });

        FillChoices();
        FillSizeChoices();
        Character = CreateNew();
    }

    // ═════════════ Создание персонажа ═════════════

    private Character CreateNew()
    {
        var c = new Character
        {
            Meta = new CharacterMeta { CreatedUtc = DateTime.UtcNow, AppVersion = AppPaths.AppVersion },
            SpeciesId = "human",
            Wounds = 0,
            FatePoints = 3,
            Size = 4,
        };
        foreach (var def in _svc.Pack.OrderedCharacteristics)
            c.Characteristics.Add(new CharacteristicEntry { CharacteristicId = def.Id, Base = 25 });
        return c;
    }

    // ── характеристики ──
    /// <summary>
    /// Строки характеристик создаются один раз и обновляются на месте: если
    /// пересоздавать их при каждом пересчёте, TextBox теряет фокус и мешает печатать.
    /// </summary>
    private void SyncCharacteristicRows()
    {
        foreach (var cv in _stats.Characteristics)
        {
            var row = Characteristics.FirstOrDefault(r => r.Id == cv.Id);
            if (row is null) Characteristics.Add(new CharacteristicRow(this, cv));
            else row.Update(cv);
        }
        for (var i = Characteristics.Count - 1; i >= 0; i--)
            if (!_stats.Characteristics.Any(c => c.Id == Characteristics[i].Id))
                Characteristics.RemoveAt(i);
    }

    /// <summary>
    /// «База» — бросок 2к10+25 либо начальное значение вида. По правилам за неё
    /// опыт НЕ платится. Поверх начального значения можно вписать не больше
    /// 20 очков на характеристику («Распределение очков» базовой книги).
    /// </summary>
    public void SetCharacteristicBase(CharacteristicValue cv, int value)
    {
        var e = Character.FindCharacteristic(cv.Id);
        if (e is null) return;
        var def = DefaultBaseFor(cv.Id);
        e.Base = Math.Clamp(value, def, def + MaxManualBasePoints);
        MarkDirty();
    }

    /// <summary>Предел очков, вписываемых сверх начального значения (базовая книга).</summary>
    public const int MaxManualBasePoints = 20;

    /// <summary>Начальное значение характеристики: 25 у людей, своё у вида (у тау 20–30).</summary>
    public int DefaultBaseFor(string characteristicId)
    {
        var b = SelectedSpecies?.CharacteristicBase.GetValueOrDefault(characteristicId) ?? 0;
        return b > 0 ? b : 25;
    }

    /// <summary>Изменить «Базу» на шаг — кнопки «−» и «+» в строке листа.</summary>
    public void AdjustCharacteristicBase(CharacteristicRow row, int delta)
    {
        var def = DefaultBaseFor(row.Id);
        var next = Math.Clamp(row.BaseValue + delta, def, def + MaxManualBasePoints);
        if (next == row.BaseValue) return;
        SetCharacteristicBase(row.Value, next);
    }

    /// <summary>Изменить «Правки» на шаг — кнопки «−» и «+» в строке листа.</summary>
    public void AdjustCharacteristicAdjustment(CharacteristicRow row, int delta) =>
        SetCharacteristicAdjustment(row.Value, row.AdjustmentValue + delta);

    /// <summary>Сколько очков внесено сверх начальных значений — по всем характеристикам.</summary>
    public int BasePointsTotal => Characteristics.Sum(r => r.BaseExtra);

    /// <summary>Строка под таблицей: сколько очков внесено и по каким характеристикам.</summary>
    public string BasePointsSummary
    {
        get
        {
            var total = BasePointsTotal;
            var word = total % 10 == 1 && total % 100 != 11 ? "очко"
                     : total % 10 is 2 or 3 or 4 && (total % 100 < 12 || total % 100 > 14) ? "очка"
                     : "очков";
            var head = $"Сверх начальных значений внесено: {total} {word}";            var parts = Characteristics.Where(r => r.BaseExtra > 0)
                .Select(r => $"{r.Name} +{r.BaseExtra}").ToList();
            return parts.Count > 0 ? head + " (" + string.Join(", ", parts) + ")" : head;
        }
    }

    // Перегрузки для строк листа: удобно и в UI, и в пошаговой проверке
    public void SetCharacteristicBase(CharacteristicRow row, int value) =>
        SetCharacteristicBase(row.Value, value);

    /// <summary>
    /// «Улучш. (из магазина)» — единственное поле, которое тратит опыт.
    /// Шаг 5, максимум +20 на характеристику.
    /// </summary>
    public void SetCharacteristicAdvance(CharacteristicValue cv, int points)
    {
        var e = Character.FindCharacteristic(cv.Id);
        if (e is null) return;

        var step = _engine.Options.CharacteristicStep;
        var max = _engine.Options.MaxCharacteristicPoints;
        var target = Math.Clamp((int)Math.Round(points / (double)step) * step, 0, max);
        if (target == e.AdvancePoints) { Refresh(); return; }

        var currentCost = _engine.CharacteristicXpSpent(Character.CareerId, e.CharacteristicId, e.AdvanceLevels);
        var newCost = _engine.CharacteristicXpSpent(Character.CareerId, e.CharacteristicId, target / step);
        var delta = newCost - currentCost;

        if (delta > Stats.XpSpendableLeft)
        {
            Warn($"Не хватает свободного опыта: покупка +{step} в «{cv.Name}» стоит {delta} ОО, доступно {Stats.XpSpendableLeft}.");
            Refresh();
            return;
        }

        e.AdvancePoints = target;
        MarkDirty();
    }

    public void SetCharacteristicAdvance(CharacteristicRow row, int points) =>
        SetCharacteristicAdvance(row.Value, points);

    /// <summary>
    /// «Правки» — ручная правка мастера. Опыт не тратится.
    /// Колонка «Прочее» (вид, аугметика, жизненный путь) считается системой
    /// и вручную не меняется.
    /// </summary>
    public void SetCharacteristicAdjustment(CharacteristicValue cv, int value)
    {
        var e = Character.FindCharacteristic(cv.Id);
        if (e is null) return;
        var next = Math.Clamp(value, -100, 100);
        if (e.Adjustment == next) return;
        var before = e.Adjustment;
        e.Adjustment = next;
        LogAdjustment($"Характеристика «{cv.Name}»: правки",
            before.ToString(), next.ToString());
        MarkDirty();
    }

    public void SetCharacteristicAdjustment(CharacteristicRow row, int value) =>
        SetCharacteristicAdjustment(row.Value, value);

    public void RollCharacteristics()
    {
        var species = SelectedSpecies;
        var bases = species?.CharacteristicBase ?? [];
        var formula = bases.Count > 0
            ? "2к10 + " + string.Join(" / ", bases.Values.Distinct().OrderBy(x => x).Select(x => x.ToString()))
            : "2к10 + 25";
        if (!NoDialogs)
        {
            var msg = $"Перебросить базу характеристик ({formula} для каждой)?\n"
                    + "Вложенные в улучшения очки и «прочее» будут обнулены.";
            if (MessageBox.Show(msg, "Бросок характеристик", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;
        }
        var rnd = new Random();
        foreach (var e in Character.Characteristics)
        {
            var b = bases.GetValueOrDefault(e.CharacteristicId);
            if (b <= 0) b = 25;
            e.Base = b + rnd.Next(2, 11) + rnd.Next(2, 11);
            e.AdvancePoints = 0;
            e.Other = 0;
        }
        MarkDirty();
    }

    /// <summary>Формула броска характеристики для выбранного вида («2к10+25»).</summary>
    public string RollFormula(string characteristicId)
    {
        var b = SelectedSpecies?.CharacteristicBase.GetValueOrDefault(characteristicId) ?? 0;
        return b > 0 ? $"2к10+{b}" : "2к10+25";
    }

    // ═════════════ Пересчёт и заполнение UI ═════════════

    public void Refresh()
    {
        // Награды жизненного пути пересчитываются первыми: от них зависят
        // «Прочее» в характеристиках и итоговые значения.
        SyncVariants();
        ApplyPathGrants();
        _stats = _engine.Compute(Character);

        SyncCharacteristicRows();
        foreach (var r in Characteristics) r.RaiseAll();
        OnPropertyChanged(nameof(BasePointsTotal));
        OnPropertyChanged(nameof(BasePointsSummary));

        SyncSkillRows();
        foreach (var r in Skills) r.RaiseAll();

        Talents.Clear();
        foreach (var e in Character.Talents.OrderBy(x => _svc.Pack.Talent(x.TalentId)?.Name ?? x.TalentId))
        {
            var def = _svc.Pack.Talent(e.TalentId);
            var check = _engine.CheckTalent(Character, e.TalentId);
            Talents.Add(new TalentRow(def, e, check.Ok, check.Summary));
        }

        Items.Clear();
        var seen = new HashSet<string>();
        foreach (var e in Character.Items)
        {
            // Свой предмет лежит в самом персонаже, а не в паке: у него нет
            // id в дата-паке, и искать его надо по самому определению.
            var def = e.Custom ?? _svc.Pack.ItemOf(e);
            if (def is null) continue;
            // Свои предметы — в отдельной группе, а не среди книжных.
            var group = e.Custom is null && _svc.Pack.ItemGroups.Count > 0
                ? ItemGroups.Find(def, _svc.Pack.ItemGroups)
                : null;
            var section = group is null
                ? null
                : _svc.Pack.ItemSections.FirstOrDefault(s => s.Id == group.SectionId);
            Items.Add(new ItemRow(def, e, section, group));
            if (!string.IsNullOrEmpty(def.Id)) seen.Add(def.Id);
        }
        ItemView.Refresh();
        OnPropertyChanged(nameof(ItemFilter));
        OnPropertyChanged(nameof(VisibleItemCount));
        OnPropertyChanged(nameof(TotalItemCount));
        OnPropertyChanged(nameof(CustomItemCount));
        OnPropertyChanged(nameof(HasCustomItems));

        // Психические приёмы: по дисциплинам и древам приёмов, как в главе VI
        PsyAbilities.Clear();
        var q = _psyFilter.Trim();
        foreach (var def in _svc.Pack.PsyAbilities
                     .Where(d => !_psyOnlyLearned || Character.PsyAbilities.Any(e => e.AbilityId == d.Id))
                     .Where(d => q.Length == 0
                                 || d.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                                 || d.Discipline.Contains(q, StringComparison.OrdinalIgnoreCase)
                                 || d.Effect.Contains(q, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(d => d.Discipline, StringComparer.CurrentCulture)
                     .ThenBy(d => d.Tree, StringComparer.CurrentCulture)
                     .ThenBy(d => d.Cost ?? 0)
                     .ThenBy(d => d.Name, StringComparer.CurrentCulture))
        {
            PsyAbilities.Add(new PsyRow(def, Character.PsyAbilities.Any(e => e.AbilityId == def.Id)));
        }
        // Счётчики по дисциплинам: изучено сколько и какие постигнуты.
        PsyDisciplines.Clear();
        foreach (var group in _svc.Pack.PsyAbilities
                     .GroupBy(a => a.Discipline)
                     .OrderBy(g => g.Key, StringComparer.CurrentCulture))
        {
            var learned = group.Count(a => Character.PsyAbilities.Any(e => e.AbilityId == a.Id));
            PsyDisciplines.Add(new PsyDisciplineStat(group.Key, learned, group.Count()));
        }

        PsyView.Refresh();
        OnPropertyChanged(nameof(PsyLearnedCount));
        OnPropertyChanged(nameof(MasteredText));

        // Силы навигатора: сначала имеющиеся
        NavPowers.Clear();
        foreach (var def in _svc.Pack.NavigatorPowers
                     .OrderBy(d => Character.NavigatorPowers.Any(e => e.PowerId == d.Id) ? 0 : 1)
                     .ThenBy(d => d.Name, StringComparer.CurrentCulture))
        {
            NavPowers.Add(new NavPowerRow(this, def)
            {
                Entry = Character.NavigatorPowers.FirstOrDefault(e => e.PowerId == def.Id),
            });
        }
        OnPropertyChanged(nameof(NavRuleText));
        OnPropertyChanged(nameof(NavLevelsText));
        OnPropertyChanged(nameof(NavStartText));
        OnPropertyChanged(nameof(NavNoPhenomena));

        // Улучшения доступные / купленные
        var rank = _stats.Rank;
        AvailableAdvances.Clear();
        foreach (var opt in _engine.AdvancesFor(Character, rank))
        {
            var check = _engine.CheckAdvance(Character, opt);
            var taken = Character.AdvanceCount(opt.Id);
            if (taken >= Math.Max(1, opt.MaxRepeats) && !check.Ok) continue;
            AvailableAdvances.Add(new AdvanceRow(opt, check.Ok, check.Summary, taken, null, _svc.Pack));
        }
        // Показываем и уже купленные на текущем ранге
        foreach (var a in Character.Advances.Where(x => x.Rank == rank))
            if (!AvailableAdvances.Any(x => x.Option?.Id == a.OptionId))
            {
                var opt = _engine.FindOption(Character, a.OptionId);
                if (opt is not null)
                    AvailableAdvances.Add(new AdvanceRow(opt, true, "Уже куплено", Character.AdvanceCount(a.OptionId), null, _svc.Pack));
            }

        BoughtAdvances.Clear();
        foreach (var a in Character.Advances.OrderBy(x => x.Rank))
        {
            var opt = _engine.FindOption(Character, a.OptionId);
            BoughtAdvances.Add(new AdvanceRow(opt, true, "", Character.AdvanceCount(a.OptionId), a, _svc.Pack));
        }
        // Пока купленных улучшений нет, вторая колонка вкладки не нужна.
        OnPropertyChanged(nameof(HasBoughtAdvances));

        Warnings.Clear();
        foreach (var w in _stats.Warnings) Warnings.Add(w);

        SyncChoices();
        SyncRankTable();
        OnPropertyChanged(nameof(Stats));
        OnPropertyChanged(nameof(EarnedXpText));
        OnPropertyChanged(nameof(HasEarnedXp));
        OnPropertyChanged(nameof(XpCompositionText));
        OnPropertyChanged(nameof(IncomeText));
        OnPropertyChanged(nameof(IncomeExampleText));
        OnPropertyChanged(nameof(IncomeLevelText));
        OnPropertyChanged(nameof(IncomeHint));
        OnPropertyChanged(nameof(ShipHullText));
        OnPropertyChanged(nameof(SelectedHull));
        OnPropertyChanged(nameof(PackSummary));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// Лист умений: показываем ВСЕ умения дата-пака, как на листе персонажа.
    /// Изученные отмечены, невыученные можно добавить прямо из строки.
    /// Строки переиспользуются, чтобы поля ввода не теряли фокус.
    /// </summary>
    private void SyncSkillRows()
    {
        CharacteristicHeaders = _stats.Characteristics
            .Select(cv => new CharacteristicHeader
            {
                Id = cv.Id, Abbrev = cv.Abbrev, Name = cv.Name, Bonus = cv.CharBonus,
                Value = cv.Total,
            }).ToList();

        var q = _skillFilter.Trim();
        var visible = _svc.AllSkills
            .Where(def => !_skillsOnlyLearned || Character.FindSkill(def.Id) is not null)
            .Where(def => q.Length == 0
                || def.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || def.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(def => Character.FindSkill(def.Id) is null)
            .ThenBy(def => def.Name, StringComparer.CurrentCulture)
            .ToList();

        var seen = new HashSet<string>();
        var keep = new List<SkillSheetRow>(visible.Count);
        foreach (var def in visible)
        {
            seen.Add(def.Id);
            var row = Skills.FirstOrDefault(r => r.Id == def.Id);
            if (row is null)
            {
                row = new SkillSheetRow(this, def, Character.FindSkill(def.Id));
                Skills.Add(row);
            }
            else row.Update(def, Character.FindSkill(def.Id));
            keep.Add(row);
        }
        for (var i = Skills.Count - 1; i >= 0; i--)
            if (!seen.Contains(Skills[i].Id)) Skills.RemoveAt(i);

        // Переставляем строки в порядок фильтра
        var order = keep.ToDictionary(r => r.Id);
        for (var i = 0; i < keep.Count; i++)
            if (!ReferenceEquals(Skills[i], keep[i])) Skills.Move(Skills.IndexOf(keep[i]), i);

        foreach (var row in Skills) row.Rebuild();
        OnPropertyChanged(nameof(VisibleSkillCount));
        _ = order;
    }

    public int VisibleSkillCount => Skills.Count;

    /// <summary>Добавить умение прямо из листа (учится, но без бонуса).</summary>
    public void LearnSkill(SkillSheetRow row)
    {
        var def = row.Def;
        if (def is null) return;
        if (Character.FindSkill(def.Id) is null)
        {
            Character.Skills.Add(new SkillEntry { SkillId = def.Id, Origin = "gm" });
            LogAdjustment($"Умение «{def.Name}»: добавлено мастером", "не изучено", "изучено");
        }
        MarkDirty();
    }

    public void UnlearnSkill(SkillSheetRow row)
    {
        var e = Character.FindSkill(row.Id);
        if (e is null) return;
        if (e.Origin is "career" or "homeworld" or "destiny" or "void" or "hardship" or "aspiration")
        {
            Warn($"«{row.Name}» персонаж получает от рода занятий или жизненного пути — убрать его можно только сменой соответствующего выбора.");
            return;
        }
        Character.Skills.Remove(e);
        LogAdjustment($"Умение «{row.Name}»: убрано мастером", "было в листе", "убрано");
        MarkDirty();
    }

    private NamedOptionDef? _selectedInclination;
    public NamedOptionDef? SelectedInclination
    {
        get => _selectedInclination
            ?? _svc.Pack.Inclinations.FirstOrDefault(i => i.Id == _ch.InclinationId);
        set
        {
            if (!Set(ref _selectedInclination, value)) return;
            _ch.InclinationId = value?.Id;
            MarkDirty();
        }
    }

    public ObservableCollection<Choice<NamedOptionDef?>> InclinationChoices { get; } = [];

    /// <summary>Выявленная склонность — выбор тау вместо родного мира.</summary>
    public bool HasInclinations => SelectedSpecies?.Tau == true && InclinationChoices.Count > 0;

    private void SyncChoices()
    {
        OnPropertyChanged(nameof(SelectedCareer));
        OnPropertyChanged(nameof(HasSelectedCareer));
        OnPropertyChanged(nameof(HasSelectedHomeworld));
        OnPropertyChanged(nameof(SelectedSpecies));
        OnPropertyChanged(nameof(SelectedSize));
        OnPropertyChanged(nameof(SizeHint));
        OnPropertyChanged(nameof(HasInclinations));
        SyncSpeciesRules();
        SyncPathChoices();
        SyncBaseCareerChoices();
    }

    // ═════════════ Альтернативные пути внутри рода занятий ═════════════

    public ObservableCollection<Choice<string?>> PathChoices { get; } = [];

    private string? _selectedPathId;
    public string? SelectedPathId
    {
        get => _selectedPathId;
        set
        {
            if (!Set(ref _selectedPathId, value)) return;
            Character.AlternateRankId = value;
            MarkDirty();
        }
    }

    /// <summary>Есть ли у рода занятий альтернативные пути (следопыт и прочие).</summary>
    public bool HasPaths => PathChoices.Count > 1;

    /// <summary>Требования выбранного пути — дословно из книги.</summary>
    public string PathHint
    {
        get
        {
            var path = _engine.PathFor(Character);
            if (path is null) return "";
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(path.CareerRequired)) parts.Add($"Требуемый род занятий: {path.CareerRequired}");
            if (!string.IsNullOrWhiteSpace(path.ReplacesRank)) parts.Add($"Заменяет ранг: {path.ReplacesRank}");
            if (!string.IsNullOrWhiteSpace(path.Requirements)) parts.Add($"Требования: {path.Requirements}");
            return string.Join(" · ", parts);
        }
    }

    /// <summary>Название выбранного пути — для заголовка вкладки улучшений.</summary>
    public string SelectedPathName =>
        PathChoices.FirstOrDefault(c => c.Value == _selectedPathId)?.Label ?? "Обычный путь";

    /// <summary>Подсказка о броске характеристик — по виду персонажа.</summary>
    public string RollHintText
    {
        get
        {
            var sp = SelectedSpecies;
            var b = sp?.CharacteristicBase;
            if (sp is null || b is null || b.Count == 0)
                return "Бросок 2к10+25. Опыт за это не платится";
            var vals = b.Values.Distinct().OrderBy(x => x).Select(x => x.ToString());
            return $"Бросок 2к10+{string.Join(" / ", vals)} — по таблице «{sp.Name}». Опыт за это не платится";
        }
    }

    private void SyncPathChoices()
    {
        var career = _svc.Pack.Career(_ch.CareerId);
        PathChoices.Clear();
        PathChoices.Add(new Choice<string?> { Value = null, Label = "Обычный путь" });
        if (career is not null)
            foreach (var p in career.Paths)
                PathChoices.Add(new Choice<string?> { Value = p.Id, Label = p.Name, Hint = p.ReplacesRank });

        // Путь, которого больше нет в паке, — сбрасываем.
        if (_selectedPathId is not null && PathChoices.All(c => c.Value != _selectedPathId))
            _selectedPathId = null;
        Character.AlternateRankId = _selectedPathId;
        OnPropertyChanged(nameof(SelectedPathId));
        OnPropertyChanged(nameof(HasPaths));
        OnPropertyChanged(nameof(PathHint));
        OnPropertyChanged(nameof(SelectedPathName));
        OnPropertyChanged(nameof(RollHintText));
    }

    // ═════════════ Выборы ═════════════

    private void FillChoices()
    {
        SpeciesChoices.Clear();
        foreach (var s in _svc.AllSpecies)
            SpeciesChoices.Add(new Choice<SpeciesDef?>
            {
                Value = s,
                Label = s.Name,
                Hint = s.Book,
                Description = s.Description,
                Bonuses = SpeciesBonuses(s),
            });

        CareerChoices.Clear();
        foreach (var c in _svc.AllCareers)
            CareerChoices.Add(new Choice<CareerDef?>
            {
                Value = c,
                Label = $"{c.Name} ({c.Source})",
                Description = c.Description,
                Bonuses = CareerBonuses(c),
            });

        static List<NamedOptionDef> Opts(IEnumerable<NamedOptionDef> src) =>
            src.OrderBy(x => x.Name, StringComparer.CurrentCulture).ToList();

        void AddPath(ObservableCollection<Choice<NamedOptionDef?>> list, IEnumerable<NamedOptionDef> src)
        {
            foreach (var o in Opts(src))
                list.Add(new Choice<NamedOptionDef?>
                {
                    Value = o,
                    Label = o.Name,
                    Description = o.Description,
                    Bonuses = PathBonuses(o),
                });
        }

        HomeworldChoices.Clear();
        AddPath(HomeworldChoices, _svc.Pack.Homeworlds);
        DestinyChoices.Clear();
        AddPath(DestinyChoices, _svc.Pack.Destiny);
        VoidPathChoices.Clear();
        AddPath(VoidPathChoices, _svc.Pack.VoidPath);
        HardshipChoices.Clear();
        AddPath(HardshipChoices, _svc.Pack.Hardship);
        AspirationChoices.Clear();
        AddPath(AspirationChoices, _svc.Pack.Aspiration);
        LegacyChoices.Clear();
        AddPath(LegacyChoices, _svc.Pack.Legacy);

        ShipHulls.Clear();
        foreach (var h in _svc.Pack.ShipHulls.OrderBy(h => h.Cost).ThenBy(h => h.Name, StringComparer.CurrentCulture))
            ShipHulls.Add(h);

        InclinationChoices.Clear();
        foreach (var o in _svc.Pack.Inclinations)
            InclinationChoices.Add(new Choice<NamedOptionDef?>
            {
                Value = o,
                Label = o.Name,
                Description = o.Description,
                Bonuses = PathBonuses(o),
            });
    }

    /// <summary>Бонусы варианта жизненного пути — строками из книги.</summary>
    private string PathBonuses(NamedOptionDef o)
    {
        var parts = new List<string>();
        // Что удалось разобрать из текста книги: «+5 к Силе», «талант «…»».
        var parsed = Core.Rules.GrantParser.Parse(o, _svc.Pack)
            .Select(g => g.Label).Distinct().ToList();
        if (parsed.Count > 0) parts.Add(string.Join(", ", parsed));
        else if (!string.IsNullOrWhiteSpace(o.Effect)) parts.Add(o.Effect);
        if (o.Variants.Count > 0)
            parts.Add("Варианты: " + string.Join(", ", o.Variants.Select(v =>
                v.CostXp > 0 ? $"{v.Name} ({v.CostXp} ОО)" : v.Name)));
        return string.Join(" • ", parts);
    }

    /// <summary>Бонусы вида — характеристики, умения, таланты, черты.</summary>
    private string SpeciesBonuses(SpeciesDef s)
    {
        var parts = new List<string>();
        var chars = s.Bonus.Where(b => b.Value != 0)
            .Select(b => $"{(b.Value > 0 ? "+" : "")}{b.Value} {CharacteristicName(b.Key)}");
        if (chars.Any()) parts.Add("Характеристики: " + string.Join(", ", chars));
        if (s.Skills.Count > 0) parts.Add("Умения: " + string.Join(", ", s.Skills.Select(SkillName)));
        if (s.Talents.Count > 0) parts.Add("Таланты: " + string.Join(", ", s.Talents.Select(TalentName)));
        if (s.Traits.Count > 0) parts.Add("Черты: " + string.Join(", ", s.Traits));
        if (s.StartingWounds is not null) parts.Add("Ранения: " + s.StartingWounds.Text);
        if (s.StartingFatePoints > 0) parts.Add($"Очки судьбы: {s.StartingFatePoints}");
        return string.Join(" • ", parts);
    }

    /// <summary>Бонусы рода занятий — как в книге.</summary>
    private string CareerBonuses(CareerDef c)
    {
        var parts = new List<string>();
        var skills = c.StartingSkills.Count > 0
            ? string.Join(", ", c.StartingSkills.Select(SkillName))
            : CleanVerbatim(c.StartingSkillsVerbatim);
        var talents = c.StartingTalents.Count > 0
            ? string.Join(", ", c.StartingTalents.Select(TalentName))
            : CleanVerbatim(c.StartingTalentsVerbatim);
        if (skills.Length > 0) parts.Add("Умения: " + skills);
        if (talents.Length > 0) parts.Add("Таланты: " + talents);
        // Альтернативный ранг («Генетор», «Аугмент») — это тот же род занятий
        // с заменой ранга. Если он подходит любому роду занятий, свои
        // начальные умения он не перечисляет.
        if (c.AlternateRanks && skills.Length == 0 && talents.Length == 0)
            parts.Add("Альтернативный ранг: умения, таланты и снаряжение — от выбранного рода занятий");
        if (c.StartingFeatures.Count > 0) parts.Add("Особенности: " + string.Join(", ", c.StartingFeatures));
        if (c.StartingEquipment.Count > 0) parts.Add("Снаряжение: " + string.Join(", ", c.StartingEquipment));
        return string.Join(" • ", parts);
    }

    /// <summary>Список в книге целиком («Грамотность (Инт)», «Оценка (Инт)») — в одну строку.</summary>
    private static string CleanVerbatim(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var t = text.Trim().TrimEnd('.', ';');
        if (t.StartsWith('«') && t.EndsWith('»')) t = t[1..^1];
        return t.Replace("», «", ", ").Replace('»', ' ').Replace('«', ' ').Trim();
    }

    private string SkillName(string id) => _svc.Pack.Skill(id)?.Name ?? id;
    private string TalentName(string id) => _svc.Pack.Talent(id)?.Name ?? id;
    private static string CharacteristicName(string id) => Core.Rules.GrantParser.CharacteristicName(id);

    // ═════════════ Психические приёмы (глава VI) ═════════════

    public ObservableCollection<PsyRow> PsyAbilities { get; } = [];
    public ICollectionView PsyView { get; }

    /// <summary>Дисциплины: сколько приёмов изучено и какие постигнуты.</summary>
    public ObservableCollection<PsyDisciplineStat> PsyDisciplines { get; } = [];

    public string PsyLearnedCount => $"{Character.PsyAbilities.Count} из {_svc.Pack.PsyAbilities.Count}";

    /// <summary>Дисциплины, по которым выучено уже восемь приёмов (постигнуты).</summary>
    public string MasteredText
    {
        get
        {
            var names = _svc.Pack.PsyAbilities
                .Where(a => Character.PsyAbilities.Any(e => e.AbilityId == a.Id))
                .GroupBy(a => a.Discipline)
                .Where(g => g.Count() >= 8)
                .Select(g => $"{g.Key} ({g.Count()})")
                .ToList();
            return names.Count == 0
                ? "Ни одна дисциплина ещё не постигнута (нужно восемь приёмов)"
                : "Постигнуты: " + string.Join(", ", names);
        }
    }

    public string PsyFilter
    {
        get => _psyFilter;
        set { if (Set(ref _psyFilter, value)) PsyView.Refresh(); }
    }
    private string _psyFilter = "";

    public bool PsyOnlyLearned
    {
        get => _psyOnlyLearned;
        set { if (Set(ref _psyOnlyLearned, value)) PsyView.Refresh(); }
    }
    private bool _psyOnlyLearned;

    public void LearnPsy(PsyRow row)
    {
        if (Character.PsyAbilities.Any(e => e.AbilityId == row.Id)) return;
        Character.PsyAbilities.Add(new PsyAbilityEntry { AbilityId = row.Id, Origin = "gm" });
        MarkDirty();
    }

    public void ForgetPsy(PsyRow row)
    {
        var e = Character.PsyAbilities.FirstOrDefault(x => x.AbilityId == row.Id);
        if (e is null) return;
        if (e.Origin is "career" or "path")
        {
            Warn($"«{row.Name}» персонаж получает от рода занятий или жизненного пути — убрать его можно только сменой соответствующего выбора.");
            return;
        }
        Character.PsyAbilities.Remove(e);
        MarkDirty();
    }

    // ═════════════ Силы навигатора (глава VII) ═════════════

    public ObservableCollection<NavPowerRow> NavPowers { get; } = [];

    public string NavRuleText => _svc.Pack.Rules.GetValueOrDefault("psi_nav_test") ?? "";
    public string NavLevelsText => _svc.Pack.Rules.GetValueOrDefault("psi_nav_levels") ?? "";
    public string NavStartText => _svc.Pack.Rules.GetValueOrDefault("psi_nav_starting") ?? "";
    public string NavNoPhenomena => _svc.Pack.Rules.GetValueOrDefault("psi_nav_no_phenomena") ?? "";

    public void LearnNav(NavPowerRow row)
    {
        if (Character.NavigatorPowers.Any(e => e.PowerId == row.Id)) return;
        Character.NavigatorPowers.Add(new NavigatorPowerEntry
        {
            PowerId = row.Id, Proficiency = NavigatorProficiency.Novice,
        });
        MarkDirty();
    }

    public void RemoveNav(NavPowerRow row)
    {
        var e = Character.NavigatorPowers.FirstOrDefault(x => x.PowerId == row.Id);
        if (e is null) return;
        Character.NavigatorPowers.Remove(e);
        MarkDirty();
    }

    /// <summary>Повысить уровень владения силой: Послушник → Адепт → Мастер.</summary>
    public void RaiseNavProficiency(string powerId)
    {
        var e = Character.NavigatorPowers.FirstOrDefault(x => x.PowerId == powerId);
        if (e is null || !NavigatorProficiency.CanLevelUp(e.Proficiency)) return;
        e.Proficiency = NavigatorProficiency.Next(e.Proficiency);
        MarkDirty();
    }

    /// <summary>Понизить уровень владения силой.</summary>
    public void LowerNavProficiency(string powerId)
    {
        var e = Character.NavigatorPowers.FirstOrDefault(x => x.PowerId == powerId);
        if (e is null || !NavigatorProficiency.CanLevelDown(e.Proficiency)) return;
        e.Proficiency = NavigatorProficiency.Prev(e.Proficiency);
        MarkDirty();
    }

    // ═════════════ Таблица 2-2 «Ранги» ═════════════

    /// <summary>Все восемь рангов из таблицы 2-2 — чтобы видеть соотношение опыта и ранга.</summary>
    public ObservableCollection<RankBandRow> RankTable { get; } = [];

    /// <summary>Диапазон текущего ранга, как он записан в книге.</summary>
    public string RankRangeText => _stats.RankMax > 0
        ? $"{_stats.RankMin} – {_stats.RankMax} ОО"
        : $"{_stats.RankMin} ОО";

    /// <summary>Номер следующего ранга (раньше него уже некуда расти).</summary>
    public int NextRank => Math.Min(8, _stats.Rank + 1);
    public string NextRankLabel => _stats.Rank >= 8
        ? "Высший ранг"
        : $"До ранга {NextRank}";

    /// <summary>Пояснение, откуда берётся общий опыт и ранг.</summary>
    public string RankRuleText =>
        $"Всего {_stats.TotalXp} ОО: {_stats.XpLocked} врождённых, "
        + $"{_engine.Options.StartingSpendable} стартовых и {Character.XpEarned} начисленных. "
        + "Ранг считается по таблице 2-2 от общего опыта, а непотраченная часть "
        + "остаётся в «Свободном опыте» и тратится на улучшения.";

    private void SyncRankTable()
    {
        if (RankTable.Count == 0)
            foreach (var (rank, min, max) in RulesEngine.RankThresholds)
                RankTable.Add(new RankBandRow(rank, min, max));

        // Установка IsCurrent сама рассылает уведомление
        foreach (var row in RankTable)
            row.IsCurrent = row.Rank == _stats.Rank;
        OnPropertyChanged(nameof(RankRangeText));
        OnPropertyChanged(nameof(RankRuleText));
        OnPropertyChanged(nameof(NextRank));
        OnPropertyChanged(nameof(NextRankLabel));
    }

    /// <summary>Строка таблицы рангов.</summary>
    public sealed class RankBandRow : INotifyPropertyChanged
    {
        private bool _isCurrent;

        public RankBandRow(int rank, int min, int max) { Rank = rank; Min = min; Max = max; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public int Rank { get; }
        public int Min { get; }
        public int Max { get; }
        public bool IsCurrent { get => _isCurrent; set { _isCurrent = value; Raise(); } }

        public string RankText => $"Ранг {Rank}";
        public string RangeText => $"{Min} – {Max} ОО";
        public string BarText => _isCurrent
            ? $"{Min} – {Max} ОО  ← вы здесь"
            : $"{Min} – {Max} ОО";

        /// <summary>Ширина полоски для наглядности: ранг тем «длиннее», тем дороже.</summary>
        public double BarWidth => Math.Max(6, (Max - Min + 1) / 35000.0 * 420);
    }

    // ═════════════ Уровень дохода и корабль (шаг 5) ═════════════

    /// <summary>Бросок 1к10 по таблице 1-5.</summary>
    public void RollIncome()
    {
        Character.IncomeRoll = Random.Shared.Next(1, 11);
        MarkDirty();
    }

    public void SetIncomeRoll(int roll) { if (roll is < 1 or > 10) return; Character.IncomeRoll = roll; MarkDirty(); }

    /// <summary>
    /// Уровень дохода для шапки. Пока бросок 1к10 не сделан, показываем «—»:
    /// шаг 5 создания персонажа ещё не пройден.
    /// </summary>
    public string IncomeLevelText =>
        Character.IncomeRoll is > 0 and <= 10 ? Stats.IncomeLevel.ToString() : "—";

    /// <summary>Подсказка к доходу в шапке: бросок, уровень и очки корабля.</summary>
    public string IncomeHint =>
        Character.IncomeRoll > 0
            ? $"Бросок 1к10: {Character.IncomeRoll}. Уровень дохода: {Stats.IncomeLevel}. "
              + $"Очки корабля: {Character.ShipPoints}. Нажмите, чтобы бросить заново."
            : "Бросок 1к10 по таблице 1-5 — начальный уровень дохода (шаг 5 создания персонажа). "
              + "Нажмите, чтобы бросить.";

    public void SetIncomeModifier(int value) { Character.IncomeModifier = value; MarkDirty(); }    public void SetIncomeBonus(int value)
    {
        if (Character.IncomeBonus == value) return;
        var before = Character.IncomeBonus;
        Character.IncomeBonus = value;
        LogAdjustment("Уровень дохода: правка мастера", before.ToString(), value.ToString());
        MarkDirty();
    }

    // ═════════════ Журнал ручных правок ═════════════

    /// <summary>
    /// Журнал правок мастера: сначала свежие. Правки бесплатны, но их видно
    /// на листе и в экспорте — чтобы было понятно, что менял ведущий.
    /// </summary>
    public IReadOnlyList<AdjustmentLogEntry> AdjustmentLog =>
        Character.Adjustments.OrderByDescending(a => a.When).ToList();

    /// <summary>Есть ли записи в журнале правок.</summary>
    public bool HasAdjustmentLog => Character.Adjustments.Count > 0;

    /// <summary>Записать правку в журнал.</summary>
    private void LogAdjustment(string what, string before, string after)
    {
        Character.Adjustments.Add(new AdjustmentLogEntry
        {
            When = DateTime.Now,
            What = what,
            Before = before,
            After = after,
        });
        // Больше 200 записей не нужно: журнал показывается как памятка ведущему.
        if (Character.Adjustments.Count > 200)
            Character.Adjustments.RemoveRange(0, Character.Adjustments.Count - 200);
        OnPropertyChanged(nameof(AdjustmentLog));
        OnPropertyChanged(nameof(HasAdjustmentLog));
    }

    /// <summary>
    /// Сколько опыта начислить по кнопке «Добавить». Держим строкой, а не числом:
    /// пустое поле или опечатка не должны ронять привязку — разбираем при нажатии.
    /// </summary>
    public string XpToAddText
    {
        get => _xpToAddText;
        set => Set(ref _xpToAddText, value);
    }
    private string _xpToAddText = "500";

    /// <summary>
    /// Опыт, начисленный персонажу по ходу игры. Входит в общий опыт (и в ранг),
    /// а его непотраченная часть доступна для улучшений.
    /// </summary>
    public int EarnedXp
    {
        get => Character.XpEarned;
        set
        {
            var v = Math.Max(0, value);
            if (Character.XpEarned == v) return;
            Character.XpEarned = v;
            MarkDirty();
        }
    }

    /// <summary>Начислить опыт из поля ввода.</summary>
    public void AddXp()
    {
        var text = (XpToAddText ?? "").Trim().Replace(" ", "").Replace("\u00A0", "");
        if (text.StartsWith('+')) text = text[1..];
        if (text.EndsWith("ОО", StringComparison.OrdinalIgnoreCase)) text = text[..^2];

        if (!int.TryParse(text, out var amount) || amount <= 0)
        {
            Warn("Введите целое число очков опыта больше нуля, например 500.");
            return;
        }
        if (amount > 100_000)
        {
            Warn("За один раз можно добавить не больше 100 000 ОО — проверьте число.");
            return;
        }

        EarnedXp += amount;
    }

    /// <summary>Сбросить весь начисленный опыт (если ошиблись с суммой).</summary>
    public void ResetEarnedXp()
    {
        if (EarnedXp != 0) EarnedXp = 0;
    }

    /// <summary>Сколько опыта начислено — подпись рядом с полем ввода.</summary>
    public string EarnedXpText => Character.XpEarned > 0
        ? $"Начислено за игру: {Character.XpEarned} ОО"
        : "Опыт ещё не начислялся";

    /// <summary>Показывать ли кнопку сброса начисленного опыта.</summary>
    public bool HasEarnedXp => Character.XpEarned > 0;

    /// <summary>Из чего складывается общий опыт — для пояснения под цифрой.</summary>
    public string XpCompositionText =>
        $"{_stats.XpLocked} врождённых + {_engine.Options.StartingSpendable} стартовых + {Character.XpEarned} добавленных";

    public void SetShipPoints(int value) { Character.ShipPoints = Math.Max(0, value); MarkDirty(); }

    public void SetShipHull(ShipHullDef? hull)
    {
        Character.ShipHullId = hull?.Id;
        MarkDirty();
    }

    public ObservableCollection<ShipHullDef> ShipHulls { get; } = [];
    public ShipHullDef? SelectedHull
    {
        get => ShipHulls.FirstOrDefault(h => h.Id == Character.ShipHullId);
        set { if (Set(ref _selectedHull, value)) SetShipHull(value); }
    }
    private ShipHullDef? _selectedHull;

    public string IncomeText => _stats.IncomeRollRow is null
        ? "бросок ещё не сделан"
        : $"1к10 = {Character.IncomeRoll} → «{_stats.IncomeRollRow.Roll}»: уровень дохода {_stats.IncomeFromRoll}, очков корабля {_stats.ShipPointsFromRoll}";

    public string IncomeExampleText => _stats.IncomeRollRow?.Name ?? "";

    public string ShipHullText => _stats.ShipHull is null ? "судно не выбран"
        : $"{_stats.ShipHull.Name} — {_stats.ShipHull.Cost} ОК, экипаж {_stats.ShipHull.Crew}, прочность {_stats.ShipHull.HullStrength}";

    // ═════════════ Действия ═════════════

    public void MarkDirty()
    {
        HasUnsavedChanges = true;
        Character.Meta.ModifiedUtc = DateTime.UtcNow;
        Refresh();
    }

    public void SetCareer(CareerDef? career)
    {
        Character.CareerId = career?.Id;
        Character.Advances.RemoveAll(a => a.CareerId != career?.Id);
        SyncBaseCareerChoices();
        if (career is not null)
        {
            ApplyStarting(career);
        }
        ApplyPathGrants();
        RefreshStartingEquipment();
        MarkDirty();
    }

    /// <summary>
    /// Награды, которые персонаж получает от вариантов жизненного пути
    /// (родной мир, уготованная судьба, путь в пустоту, испытания, устремление).
    ///
    /// В книге они записаны обычным текстом, поэтому берём и разбираем строки
    /// «grants» и «effect». Всё, что распозналось, показываем игроку
    /// переключателями: правило «получают один из двух талантов» решает он сам.
    /// </summary>
    private void ApplyPathGrants()
    {
        foreach (var origin in PathOrigins)
        {
            Character.Skills.RemoveAll(s => s.Origin == origin);
            Character.Talents.RemoveAll(t => t.Origin == origin);
        }
        foreach (var e in Character.Characteristics) e.PathPoints = 0;

        PathGrants.Clear();
        ApplyPathStartingValues();
        foreach (var (origin, def) in PathSources())
        {
            if (def is null) continue;
            ApplyGrants(origin, def);

            // Вариант раздела («Сквозь шторм»): свои бонусы и своя цена.
            var variant = GetVariant(origin);
            if (variant is null || variant.Id.Length == 0 || string.IsNullOrWhiteSpace(variant.Effect)) continue;
            ApplyGrants(origin, new NamedOptionDef
            {
                Id = $"{def.Id}.{variant.Id}",
                Name = variant.Name,
                Effect = variant.Effect,
            });
        }
    }

    /// <summary>Применяет разобранные награды раздела к персонажу.</summary>
    private void ApplyGrants(string origin, NamedOptionDef def)
    {
        foreach (var g in GrantParser.Parse(def, _svc.Pack))
        {
            var key = $"{def.Id}:{g.Key}";
            // Книга требует выбора («один из двух», «Ловкость или Сила воли») —
            // такие награды по умолчанию выключены, решает игрок.
            var on = g.Optional ? false : !Character.GrantToggles.TryGetValue(key, out var off) || off;
            var row = new PathGrantRow(this, origin, g, key, on);
            PathGrants.Add(row);
            if (!on) continue;

            switch (g.Kind)
            {
                case GrantKind.Characteristic:
                    var ce = Character.FindCharacteristic(g.TargetId);
                    if (ce is not null) ce.PathPoints += g.Delta;
                    break;
                case GrantKind.Skill:
                    if (Character.FindSkill(g.TargetId) is null)
                        Character.Skills.Add(new SkillEntry { SkillId = g.TargetId, Origin = origin });
                    break;
                case GrantKind.Talent:
                    if (Character.FindTalent(g.TargetId) is null)
                        Character.Talents.Add(new TalentEntry { TalentId = g.TargetId, Origin = origin });
                    break;
            }
        }
    }

    private static readonly string[] PathOrigins = ["homeworld", "destiny", "void", "hardship", "aspiration", "legacy"];

    /// <summary>
    /// Начальные ранения и очки судьбы по жизненному пути. В книге это первая
    /// строка таблицы жизненных путей — родной мир: «Изначальное количество ран:
    /// … 2 × бонус Выносливости + 1к5+1», «Изначальное количество очков судьбы:
    /// бросьте к10…».
    /// </summary>
    private void ApplyPathStartingValues()
    {
        Character.PathWoundsRoll = 0;
        Character.PathFateRoll = 0;

        var option = PathSources().Select(s => s.Def)
            .FirstOrDefault(d => d is not null && (d.StartingWounds is not null || d.StartingFate is not null));
        if (option is null) return;

        if (option.StartingWounds is { Dice: > 0 } sw)
            Character.PathWoundsRoll = Random.Shared.Next(1, sw.Dice + 1);

        if (option.StartingFate is { Dice: > 0 } sf)
        {
            Character.PathFateRoll = Random.Shared.Next(1, sf.Dice + 1);
            // У вида своя формула очков судьбы — она главнее (у абхуманов
            // жизненный путь вообще не используется).
            var species = SelectedSpecies;
            var speciesHasFate = species is not null
                && (species.StartingFate is not null || species.StartingFatePoints > 0);
            if (!speciesHasFate) Character.FatePoints = sf.PointsFor(Character.PathFateRoll);
        }
    }

    private (string Origin, NamedOptionDef? Def)[] PathSources() =>
    [
        ("homeworld", SelectedHomeworld),
        ("destiny", SelectedDestiny),
        ("void", SelectedVoidPath),
        ("hardship", SelectedHardship),
        ("aspiration", SelectedAspiration),
        ("legacy", SelectedLegacy),
    ];

    /// <summary>Игрок включил/выключил конкретную награду жизненного пути.</summary>
    public void TogglePathGrant(PathGrantRow row)
    {
        Character.GrantToggles[row.Key] = !row.Enabled;
        ApplyPathGrants();
        MarkDirty();
    }

    /// <summary>Включить награду (для тех, что по умолчанию выключены).</summary>
    public void EnablePathGrant(PathGrantRow row)
    {
        Character.GrantToggles[row.Key] = true;
        ApplyPathGrants();
        MarkDirty();
    }

    /// <summary>Начальные умения, таланты и особенности рода занятий.</summary>
    private void ApplyStarting(CareerDef career)
    {
        // Альтернативный ранг («Генетор», «Аугмент») — это тот же род занятий
        // с заменой ранга: свои начальные умения он не перечисляет, поэтому
        // выданные ранее умения и таланты рода занятий сохраняются.
        // Альтернативный ранг «любому роду занятий» берёт начальное от
        // выбранного основного рода занятий.
        bool hasBase = career is { AnyBaseCareer: true } && SelectedBaseCareer is not null;
        bool inherits = career.AlternateRanks && !hasBase
            && career.StartingSkills.Count == 0 && career.StartingTalents.Count == 0
            && string.IsNullOrWhiteSpace(career.StartingSkillsVerbatim)
            && string.IsNullOrWhiteSpace(career.StartingTalentsVerbatim);
        if (!inherits)
        {
            Character.Skills.RemoveAll(s => s.Origin == "career");
            Character.Talents.RemoveAll(t => t.Origin == "career");
        }
        var source = hasBase ? SelectedBaseCareer! : career;

        foreach (var raw in source.StartingSkills.Concat(Quoted(source.StartingSkillsVerbatim)))
        {
            var id = ResolveSkillId(raw);
            if (id is null) continue;
            var e = Character.FindSkill(id);
            if (e is null) { e = new SkillEntry { SkillId = id, Origin = "career" }; Character.Skills.Add(e); }
            // Стартовое умение рода занятий просто изучено: +10/+20 покупаются
            // отдельно, в таблице «Улучшения … на ранге N».
            e.Rank = 0;
        }
        foreach (var raw in source.StartingTalents.Concat(Quoted(source.StartingTalentsVerbatim)))
        {
            var id = ResolveTalentId(raw);
            if (id is null) continue;
            if (Character.FindTalent(id) is null)
                Character.Talents.Add(new TalentEntry { TalentId = id, Origin = "career" });
        }
        foreach (var f in source.StartingFeatures)
            AddTrait(f.Trim('«', '»', '"', ' '));
    }

    /// <summary>Все фрагменты в «ёлочках» из строки — так в книге перечислены умения и таланты.</summary>
    private static IEnumerable<string> Quoted(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var i = 0;
        while (i < text.Length)
        {
            var open = text.IndexOf('«', i);
            if (open < 0) yield break;
            var close = text.IndexOf('»', open + 1);
            if (close < 0) yield break;
            var inner = text[(open + 1)..close].Trim();
            if (inner.Length > 0) yield return inner;
            i = close + 1;
        }
    }

    private string? ResolveSkillId(string raw) =>
        GrantParser.ResolveSkill(_svc.Pack, raw.Split('(').FirstOrDefault()?.Trim() is { Length: > 0 } s ? s : raw)
        ?? GrantParser.ResolveSkill(_svc.Pack, raw);

    private string? ResolveTalentId(string raw) => GrantParser.ResolveTalent(_svc.Pack, raw);

    public void AddTrait(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (!Character.Traits.Contains(name)) Character.Traits.Add(name);
        MarkDirty();
    }

    public void RemoveTrait(string name)
    {
        Character.Traits.Remove(name);
        MarkDirty();
    }

    // ── улучшения рангов ──
    public void BuyAdvance(AdvanceRow row)
    {
        if (row.Option is null) return;
        var check = _engine.CheckAdvance(Character, row.Option);
        if (!check.Ok) { Warn(check.Summary); return; }
        _engine.ApplyAdvance(Character, row.Option);
        MarkDirty();
    }

    public void UnbuyAdvance(AdvanceRow row)
    {
        if (row.Option?.Id is null) return;
        _engine.RemoveAdvance(Character, row.Option.Id);
        MarkDirty();
    }

    // ── умения / таланты ──
    public void RemoveSkill(SkillEntry e)
    {
        var name = _svc.Pack.Skill(e.SkillId)?.Name ?? e.SkillId;
        Character.Skills.Remove(e);
        LogAdjustment($"Умение «{name}»: убрано мастером", "было в листе", "убрано");
        MarkDirty();
    }

    public void SetSkillRank(SkillEntry e, int rank)
    {
        var next = Math.Clamp(rank, 0, 2);
        if (e.Rank == next) return;
        var before = e.Rank;
        e.Rank = next;
        var name = _svc.Pack.Skill(e.SkillId)?.Name ?? e.SkillId;
        LogAdjustment($"Умение «{name}»: ранг", before.ToString(), next.ToString());
        MarkDirty();
    }

    /// <summary>
    /// «Правки» умения — ручная правка мастера. Идёт через модель, чтобы
    /// изменение попало в журнал правок.
    /// </summary>
    public void SetSkillAdjustment(SkillSheetRow row, int value)
    {
        var e = row.Entry;
        if (e is null) return;
        var next = Math.Clamp(value, -100, 100);
        if (e.Adjustment == next) return;
        var before = e.Adjustment;
        e.Adjustment = next;
        LogAdjustment($"Умение «{row.Name}»: правки", before.ToString(), next.ToString());
        MarkDirty();
    }

    public void RemoveTalent(TalentEntry e)
    {
        var name = _svc.Pack.Talent(e.TalentId)?.Name ?? e.TalentId;
        Character.Talents.Remove(e);
        LogAdjustment($"Талант «{name}»: убран мастером", "был в персонаже", "убран");
        MarkDirty();
    }

    public void RemoveItem(ItemEntry e)
    {
        var name = e.Custom?.Name ?? _svc.Pack.ItemOf(e)?.Name ?? e.ItemId;
        Character.Items.Remove(e);
        LogAdjustment($"Снаряжение «{name}»: убрано мастером", "было в снаряжении", "убрано");
        // Предмет, выданный по старту, игрок убрал — больше не выдаём его
        // автоматически (иначе он вернётся при следующем пересчёте).
        if (e.Note.StartsWith(GrantNotePrefix, StringComparison.Ordinal))
        {
            var key = e.Note[GrantNotePrefix.Length..].Trim();
            if (key.Length > 0 && !Character.StartingEquipmentRemoved.Contains(key))
                Character.StartingEquipmentRemoved.Add(key);
        }
        MarkDirty();
    }

    private void PickSkill()
    {
        var list = _svc.AllSkills
            .Where(s => !Character.Skills.Any(x => x.SkillId == s.Id))
            .Select(s => new PickerItem
            {
                Value = s,
                Title = s.Name,
                Subtitle = $"{s.Characteristics.Select(RulesEngine.CharacteristicRu).Join(", ")} · {DescribeType(s)}",
                Description = s.Description,
                Advanced = s.Advanced,
                Available = false,
            })
            .ToList();
        if (!PickerDialog.Show("Добавить умение", list, out var picked)) return;
        if (picked is SkillDef sk)
        {
            Character.Skills.Add(new SkillEntry { SkillId = sk.Id, Origin = "homebrew" });
            MarkDirty();
        }
    }

    private void PickTalent()
    {
        var list = _svc.AllTalents
            .Where(t => !Character.Talents.Any(x => x.TalentId == t.Id))
            .Select(t =>
            {
                var check = _engine.CheckTalent(Character, t.Id);
                return new PickerItem
                {
                    Value = t,
                    Title = t.Name,
                    Subtitle = t.Prerequisites.Count > 0 ? t.Prerequisites[0] : "без требований",
                    Description = t.Description,
                    Advanced = t.Advanced,
                    Available = check.Ok,
                };
            })
            .ToList();
        if (!PickerDialog.Show("Добавить талант", list, out var picked)) return;
        if (picked is TalentDef t)
        {
            Character.Talents.Add(new TalentEntry { TalentId = t.Id, Origin = "homebrew" });
            LogAdjustment($"Талант «{t.Name}»: добавлен мастером", "не было", "есть");
            MarkDirty();
        }
    }

    /// <summary>Вид снаряжения: стрелковое, пусковое, холодное, цепное, сильное, психосиловое.</summary>
    public static bool IsWeaponCategory(string c) => c is ItemCategory.RangedWeapon or ItemCategory.Launcher
        or ItemCategory.MeleeWeapon or ItemCategory.ChainWeapon or ItemCategory.PowerWeapon
        or ItemCategory.ForceWeapon;

    /// <summary>Раздел главы «Арсенал», к которому относится предмет — для сортировки.</summary>
    private string SectionNameOf(ItemDef def)
    {
        if (_svc.Pack.ItemGroups.Count == 0) return "";
        var group = Core.Data.ItemGroups.Find(def, _svc.Pack.ItemGroups);
        if (group is null) return "";
        return _svc.Pack.ItemSections.FirstOrDefault(s => s.Id == group.SectionId)?.Title ?? "";
    }

    /// <summary>
    /// Вес предмета числом — для сортировки. В паке вес записан свободно
    /// («4 кг», «0,5 кг», «1–2 кг»), поэтому берём первое число.
    /// </summary>
    /// <summary>Вес числом — для сортировки списка предметов.</summary>
    public static double WeightNumber(string? weight)
    {
        if (string.IsNullOrWhiteSpace(weight)) return 0;
        var digits = new string(weight.TakeWhile(c => char.IsDigit(c) || c == ',' || c == '.').ToArray());
        if (digits.Length == 0) return 0;
        if (double.TryParse(digits.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
            return v;
        return 0;
    }

    /// <summary>Стрелковое оружие — для него правила качества свои (стр. 114).</summary>
    private static bool IsRangedCategory(string c) => c is ItemCategory.RangedWeapon or ItemCategory.Launcher;

    private void PickItem()
    {
        try
        {
            var list = _svc.AllItems
                .Where(i => !Character.Items.Any(x => x.ItemId == i.Id))
                .Select(i => new PickerItem
                {
                    Value = i,
                    Id = i.Id,
                    Title = i.Name,
                    Subtitle = ItemSubtitle(i),
                    Description = i.Description,
                    Advanced = i.IsUpgrade,
                    Available = false,
                    IsFavorite = Services.Favorites.IsFavorite(i.Id),
                    SortGroup = SectionNameOf(i),
                    SortNumber = WeightNumber(i.Weight),
                })
                .ToList();
            var filters = BuildItemFilters();
            var ok = PickerDialog.Show("Добавить снаряжение", list, out var picked, filters);
            if (!ok) return;
            if (picked is not ItemDef it) return;

            // Оружию положено качество: спрашиваем его отдельно (базовая книга, стр. 114).
            var quality = "";
            if (IsWeaponCategory(it.Category))
            {
                var qList = WeaponQualities.All.Select(q => new PickerItem
                {
                    Value = q,
                    Title = q.Name,
                    Subtitle = q.Id == "standard" ? "как в таблице снаряжения" : "уровень качества",
                    Description = QualityRuleText(q, it),
                }).ToList();
                if (PickerDialog.Show("Качество оружия", qList, out var qpicked)
                    && qpicked is WeaponQualityDef qd)
                {
                    quality = qd.Id;
                }
            }

            Character.Items.Add(new ItemEntry { ItemId = it.Id, Quality = quality });
            // Если поиск по снаряжению не показывает новый предмет — сбросить его,
            // иначе предмет будет добавлен, но не виден.
            if (!string.IsNullOrWhiteSpace(ItemFilter)
                && !it.Name.Contains(ItemFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                ItemFilter = "";
            MarkDirty();
            // Показать добавленный предмет: список снаряжения сгруппирован по
            // главе «Арсенал», и новый предмет часто оказывается внизу.
            ItemAddedToArsenal?.Invoke(Character.Items[^1]);
        }
        catch (Exception ex)
        {
            // Окно выбора не открылось — говорим об этом прямо, а не молча.
            System.Windows.MessageBox.Show(
                $"Не удалось открыть список снаряжения.\n\n{ex.Message}",
                "Снаряжение", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    /// <summary>Предмет добавлен в снаряжение — окно должно показать его.</summary>
    public event Action<ItemEntry>? ItemAddedToArsenal;

    /// <summary>
    /// Свой предмет: лут, талисман, подарок — того, чего нет в официальном
    /// снаряжении. Хранится в персонаже, а не в паке.
    /// </summary>
    private void AddCustomItem()
    {
        if (NoDialogs) return;
        var def = Views.CustomItemDialog.Ask();
        if (def is null) return;

        Character.Items.Add(new ItemEntry { ItemId = "", Custom = def });
        // Если поиск по снаряжению не показывает новый предмет — сбросить его.
        if (!string.IsNullOrWhiteSpace(ItemFilter)
            && !def.Name.Contains(ItemFilter.Trim(), StringComparison.OrdinalIgnoreCase))
            ItemFilter = "";
        MarkDirty();
        Refresh();
        ItemAddedToArsenal?.Invoke(Character.Items[^1]);
    }

    /// <summary>Строка под названием предмета: вид, класс, ключевые характеристики, доступность.</summary>
    private static string ItemSubtitle(ItemDef i)
    {
        var parts = new List<string> { ItemCategoryRu(i.Category) };
        if (!string.IsNullOrWhiteSpace(i.WeaponClass)) parts.Add(ItemDef.WeaponClassRu(i.WeaponClass));
        if (!string.IsNullOrWhiteSpace(i.Damage)) parts.Add("урон " + i.Damage);
        if (!string.IsNullOrWhiteSpace(i.Penetration)) parts.Add("ББ " + i.Penetration);
        if (!string.IsNullOrWhiteSpace(i.Range)) parts.Add(i.Range);
        parts.Add(string.IsNullOrWhiteSpace(i.Availability) ? "доступность не указана" : "доступность: " + i.Availability);
        if (i.Encumbrance > 0) parts.Add($"вес {i.Encumbrance} кг");
        return string.Join(" · ", parts);
    }

    /// <summary>Фильтры окна подбора: вид снаряжения и класс оружия.</summary>
    private List<PickerFilter> BuildItemFilters()
    {
        var filters = new List<PickerFilter>();

        var cats = _svc.AllItems.Select(i => i.Category).Distinct()
            .Select(c => (Name: ItemCategoryRu(c), Id: c))
            .OrderBy(x => x.Name, StringComparer.CurrentCulture).ToList();
        filters.Add(new PickerFilter
        {
            Label = "Вид снаряжения:",
            Options = cats.Select(c => (c.Name, (Func<PickerItem, bool>)(it =>
                it.Value is ItemDef d && d.Category == c.Id))).ToList(),
        });

        var classes = _svc.AllItems.Where(i => !string.IsNullOrWhiteSpace(i.WeaponClass))
            .Select(i => i.WeaponClass).Distinct()
            .Select(c => (Name: ItemDef.WeaponClassRu(c), Id: c))
            .OrderBy(x => x.Name, StringComparer.CurrentCulture).ToList();
        filters.Add(new PickerFilter
        {
            Label = "Класс оружия:",
            Options = classes.Select(c => (c.Name, (Func<PickerItem, bool>)(it =>
                it.Value is ItemDef d && d.WeaponClass == c.Id))).ToList(),
        });

        return filters;
    }

    /// <summary>Правило качества для конкретного предмета — как в базовой книге, стр. 114.</summary>
    private static string QualityRuleText(WeaponQualityDef q, ItemDef it)
    {
        var ranged = IsRangedCategory(it.Category);
        var sb = new System.Text.StringBuilder();
        sb.Append(q.Effect);
        if (ranged)
        {
            if (q.Id == "low")
                sb.Append(" Стрелковое оружие низкого качества обладает свойством «Ненадёжное»: если оно уже имеет такое свойство, осечка будет при каждом промахе.");
            else if (q.Id == "good")
                sb.Append(" Стрелковое оружие хорошего качества получает свойство «Надёжное»: если оно и так его имело, игромеханических изменений нет.");
            else if (q.Id == "high")
                sb.Append(" Стрелковое оружие высшего качества никогда не даёт осечек и не перегревается — такие результаты считаются обычными промахами.");
        }
        else
        {
            if (q.MeleeAttackPenalty > 0)
                sb.Append($" Холодное оружие низкого качества получает штраф −{q.MeleeAttackPenalty} к тестам, предпринятым при атаках и парировании.");
            if (q.MeleeAttackBonus > 0)
                sb.Append($" Холодное оружие получает бонус +{q.MeleeAttackBonus} к тестам, предпринятым при атаке.");
            if (q.MeleeDamageBonus > 0)
                sb.Append($" И +{q.MeleeDamageBonus} к наносимому урону.");
        }
        return sb.ToString();
    }

    // ── Выдача стартового снаряжения ──

    /// <summary>Пометка в примечании предмета: снаряжение выдано по старту.</summary>
    public const string GrantNotePrefix = "старт: ";

    /// <summary>
    /// Разбирает стартовое снаряжение вида, рода занятий и вариантов пути:
    /// строки книги сопоставляются с предметами пака. Если в строке нет
    /// «или» — предметы выдаются сразу; если «или» есть — выбирает игрок.
    /// </summary>
    private void BuildStartingEquipment()
    {
        StartingEquipment.Clear();
        foreach (var (origin, name, lines) in StartingEquipmentSources())
        {
            for (var i = 0; i < lines.Count; i++)
            {
                var text = lines[i];
                var key = $"{origin}:{i}";
                var (variants, missing) = MatchVariants(text);
                var chosen = Character.StartingEquipmentChoices.TryGetValue(key, out var val) ? val : "";
                StartingEquipment.Add(new StartingEquipmentRow(this, key, origin, name, text, variants, chosen, missing));
            }
        }
    }

    /// <summary>
    /// Источники стартового снаряжения: вид, род занятий и выбранные
    /// варианты жизненного пути (у них есть строка «Снаряжение: …»).
    /// </summary>
    private IEnumerable<(string Origin, string Name, List<string> Lines)> StartingEquipmentSources()
    {
        if (SelectedSpecies is not null)
            yield return ("species", SelectedSpecies.Name, SelectedSpecies.StartingEquipment);
        if (SelectedCareer is not null)
        {
            var baseCareer = SelectedCareer is { AnyBaseCareer: true } ? SelectedBaseCareer : null;
            yield return ("career",
                baseCareer?.Name ?? SelectedCareer.Name,
                baseCareer?.StartingEquipment ?? SelectedCareer.StartingEquipment);
        }
        foreach (var (origin, def) in PathSources())
        {
            if (def is null) continue;
            var lines = GearLines(def);
            if (lines.Count > 0) yield return (origin, def.Name, lines);
        }
    }

    /// <summary>Строки «Снаряжение: …» из варианта жизненного пути.</summary>
    private static List<string> GearLines(NamedOptionDef def)
    {
        var result = new List<string>();
        foreach (var raw in def.Grants)
        {
            var text = raw.Trim();
            foreach (var prefix in new[] { "Снаряжение:", "Оружие:", "Броня:", "Экипировка:" })
            {
                if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var value = text[prefix.Length..].Trim();
                if (value.Length > 0) result.Add(value);
                break;
            }
        }
        return result;
    }

    /// <summary>Пересобрать строки стартового снаряжения и выдать положенное.</summary>
    private void RefreshStartingEquipment()
    {
        BuildStartingEquipment();
        ApplyStartingEquipment();
    }

    /// <summary>Выбрать вариант снаряжения (в книге «или»).</summary>
    public void ChooseStartingEquipment(StartingEquipmentRow row, int variantIndex)
    {
        Character.StartingEquipmentChoices[row.Key] = variantIndex.ToString();
        Character.StartingEquipmentRemoved.Remove(row.Key);
        // Сначала пересобираем строки (в них уже будет новый выбор),
        // потом выдаём снаряжение по ним.
        BuildStartingEquipment();
        ApplyStartingEquipment();
        MarkDirty();
    }

    /// <summary>Убрать снаряжение, выданное по этой строке.</summary>
    public void UnchooseStartingEquipment(StartingEquipmentRow row)
    {
        Character.StartingEquipmentChoices.Remove(row.Key);
        BuildStartingEquipment();
        ApplyStartingEquipment();
        MarkDirty();
    }

    /// <summary>
    /// Подобрать предмет вручную для строки, которой нет в паке
    /// (например, «изысканный гардероб» или «трофейный шлем»).
    /// </summary>
    public void PickStartingEquipmentManually(StartingEquipmentRow row)
    {
        var list = _svc.AllItems.Select(i => new PickerItem
        {
            Value = i,
            Title = i.Name,
            Subtitle = ItemSubtitle(i),
            Description = i.Description,
            Advanced = i.IsUpgrade,
            Available = false,
        }).ToList();
        if (!PickerDialog.Show("Подобрать снаряжение", list, out var picked, BuildItemFilters(), row.Text)) return;
        if (picked is not ItemDef it) return;

        var quality = "";
        if (IsWeaponCategory(it.Category))
        {
            var qList = WeaponQualities.All.Select(q => new PickerItem
            {
                Value = q,
                Title = q.Name,
                Subtitle = q.Id == "standard" ? "как в таблице снаряжения" : "уровень качества",
                Description = QualityRuleText(q, it),
            }).ToList();
            if (PickerDialog.Show("Качество оружия", qList, out var qpicked)
                && qpicked is WeaponQualityDef qd)
            {
                quality = qd.Id;
            }
        }

        var qty = ParseQuantity(row.Text);
        Character.Items.Add(new ItemEntry { ItemId = it.Id, Quantity = qty, Quality = quality });
        MarkDirty();
    }

    /// <summary>
    /// Синхронизирует инвентарь со стартовым снаряжением.
    ///
    /// В книге предметы, между которыми нет выбора, выдаются сразу: «лёгкий
    /// панцирь силовика», «мультиключ». Там, где стоит «или», выбирает игрок.
    /// </summary>
    private void ApplyStartingEquipment()
    {
        // Сначала убираем ранее выданное по старту, потом выдаём заново.
        Character.Items.RemoveAll(i => i.Note.StartsWith(GrantNotePrefix, StringComparison.Ordinal));

        foreach (var row in StartingEquipment)
        {
            if (row.Variants.Count == 0) continue;
            // Игрок убрал этот предмет из инвентаря — не выдаём снова.
            if (Character.StartingEquipmentRemoved.Contains(row.Key)) continue;

            List<StartingEquipmentOption>? granted = null;
            if (!row.IsChoice)
            {
                // Выбора в книге нет — выдаём всё, что перечислено строкой.
                granted = row.Variants.FirstOrDefault();
            }
            else if (row.ChosenVariant is not null)
            {
                // Выбор игрока.
                granted = row.ChosenVariant;
            }
            if (granted is null) continue;

            foreach (var o in granted)
            {
                Character.Items.Add(new ItemEntry
                {
                    ItemId = o.Item.Id,
                    Quantity = o.Quantity,
                    Quality = o.QualityId,
                    Note = GrantNotePrefix + row.Key,
                });
            }
        }
    }

    /// <summary>Ключ выбора: предмет + качество + количество.</summary>
    public static string OptionKeyOf(StartingEquipmentOption o) =>
        $"{o.Item.Id}|{o.QualityId}|{o.Quantity}";

    private static string QualityRu(string id) => WeaponQualities.ByName(id)?.Name ?? id;

    /// <summary>
    /// Разбирает строку стартового снаряжения на группы вариантов.
    ///
    /// «или» отделяет варианты (между ними выбирает игрок), запятая — предметы
    /// одной группы (выдаются все). Например: «лазпистолет высшего качества или
    /// ручная пушка хорошего качества» — два варианта, а «меч, плащ,
    /// перчатки» — один вариант из трёх предметов. Части, которых нет в паке,
    /// возвращаются отдельно — их покажем игроку.
    /// </summary>
    private (List<List<StartingEquipmentOption>> Variants, List<string> Missing) MatchVariants(string line)
    {
        var variants = new List<List<StartingEquipmentOption>>();
        var missing = new List<string>();
        foreach (var part in line.Split(" или ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var group = new List<StartingEquipmentOption>();
            foreach (var piece in part.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var opt = MatchItem(piece);
                if (opt is not null) group.Add(opt);
                else missing.Add(piece.Trim());
            }
            if (group.Count > 0) variants.Add(group);
        }
        return (variants, missing);
    }

    /// <summary>
    /// Ищет предмет пака в куске строки: самый длинный подходящий.
    /// Качество и количество берутся из текста.
    /// </summary>
    private StartingEquipmentOption? MatchItem(string text)
    {
        var normText = Norm(text);
        if (normText.Length == 0) return null;

        ItemDef? best = null;
        var bestScore = 0;
        var bestExtra = int.MaxValue;
        // Запасной вариант: начало названия предмета без уточнений
        // вроде «модели «Риза»». Например, «плазменный пистолет среднего
        // качества» — это «Плазменный пистолет модели «Риза»».
        var stem = StemWords(text);
        foreach (var it in _svc.AllItems)
        {
            var n = Norm(it.Name);
            // Короткие названия («Ряса») пропускаем: они слишком легко
            // совпадают с чужими словами. Исключение — точное совпадение.
            if (n.Length < 3 && normText != n) continue;
            int score, extra;
            if (normText.Contains(n))
            {
                score = n.Length;
                extra = normText.Length - score;
            }
            else if (stem.Length >= 6 && n.Contains(stem, StringComparison.Ordinal))
            {
                score = stem.Length;
                extra = n.Length - score;
            }
            else if (WordsMatch(text, it.Name))
            {
                // Совпадение по словам: «фотонные гранаты» находят
                // «Фотонная граната», «языкознание (круутский, …)» —
                // «Языкознание (язык тау)».
                score = 6;
                extra = n.Length;
            }
            else continue;

            // Длинное совпадение важнее; при равной длине — ближе по объёму.
            if (score > bestScore || (score == bestScore && extra < bestExtra))
            {
                best = it; bestScore = score; bestExtra = extra;
            }
        }
        if (best is null) return null;

        // Качество относится к оружию: для брони и снаряжения оно не применяется.
        var quality = IsWeaponCategory(best.Category) ? WeaponQualities.FromText(text)?.Id ?? "" : "";
        var qty = ParseQuantity(text);
        return new StartingEquipmentOption(best, quality, qty);
    }

    /// <summary>
    /// Начало названия по первым значимым словам (без указания качества),
    /// каждое слово обрезано до основы: «фотонные гранаты тау» → «фотоннгранат».
    /// Нужно, чтобы «плазменный пистолет среднего качества» нашёлся среди
    /// предметов «Плазменный пистолет модели «Риза»».
    /// </summary>
    private static string StemWords(string text)
    {
        var t = text.ToLowerInvariant();
        // Указания качества и количество в основу названия не входят.
        foreach (var w in new[]
                 {
                     "высшего", "высокого", "хорошего", "среднего", "низкого", "качества",
                     "два", "две", "три", "четыре", "пять", "шесть", "одно", "одна",
                     "пару", "несколько",
                 })
        {
            t = t.Replace(w, " ");
        }
        var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && char.IsLetter(w[0]))
            .Take(2)
            .Select(w => w.Length > 6 ? w[..6] : w);
        return Norm(string.Join(" ", words));
    }

    /// <summary>
    /// Совпадение названия предмета с книжной строкой по словам: каждое
    /// слово названия должно найтись в строке по началу слова. Это устойчиво
    /// к формам слов: «фотонные гранаты» находят «Фотонная граната».
    ///
    /// Формы сравниваем по основе из трёх букв — так «зубы» находят
    /// «зубов». Слова короче четырёх букв («нож», «меч») сравниваем как
    /// есть: иначе «нож» совпал бы с «ножницы».
    /// </summary>
    private static bool WordsMatch(string text, string itemName)
    {
        var t = Words(text);
        var n = Words(itemName);
        if (t.Count == 0 || n.Count == 0) return false;
        var used = new bool[t.Count];
        foreach (var w in n)
        {
            if (w.Length < 3) continue;
            var key = WordStem(w);
            var found = false;
            for (var i = 0; i < t.Count; i++)
            {
                if (used[i]) continue;
                var tk = WordStem(t[i]);
                if (tk.StartsWith(key, StringComparison.Ordinal) || key.StartsWith(tk, StringComparison.Ordinal))
                {
                    used[i] = true; found = true; break;
                }
            }
            if (!found) return false;
        }
        return true;
    }

    /// <summary>
    /// Основа слова для сравнения: у слов от четырёх букв берём первые три
    /// («зубов» → «зуб»), короткие слова не трогаем.
    /// </summary>
    private static string WordStem(string w) => w.Length > 3 ? w[..3] : w;

    /// <summary>Слова текста без знаков и пробелов.</summary>
    private static List<string> Words(string s) =>
        s.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string(w.Where(char.IsLetterOrDigit).ToArray()))
            .Where(w => w.Length > 0)
            .ToList();

    /// <summary>Количество из текста: «два магазина», «три гранаты», «1к5 зубов».</summary>
    private static int ParseQuantity(string s)
    {
        var t = s.ToLowerInvariant().TrimStart();
        if (t.StartsWith("два ") || t.StartsWith("две ") || t.StartsWith("пару ")) return 2;
        if (t.StartsWith("три ")) return 3;
        if (t.StartsWith("четыре ")) return 4;
        if (t.StartsWith("пять ")) return 5;
        if (t.StartsWith("шесть ")) return 6;
        var m = System.Text.RegularExpressions.Regex.Match(t, @"^(\d+)к(\d+)");
        if (m.Success && int.TryParse(m.Groups[2].Value, out var dice)) return dice;
        var m2 = System.Text.RegularExpressions.Regex.Match(t, @"^(\d+)\s");
        if (m2.Success && int.TryParse(m2.Groups[1].Value, out var num)) return num;
        return 1;
    }

    /// <summary>Нормализация названия для сопоставления: только буквы и цифры, без пробелов.</summary>
    private static string Norm(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant())
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    private static string DescribeType(SkillDef s) =>
        s.Advanced ? (s.Specialized ? "специализированное" : "продвинутое") : "базовое";

    public static string ItemCategoryRu(string c) => ItemCategory.Ru(c);

    private void Warn(string msg)
    {
        if (NoDialogs) { _warnings.Add(msg); return; }
        MessageBox.Show(msg, "Нельзя", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>Тихий режим: вместо окон вопросов писать в список. Нужен автопроверке.</summary>
    public bool NoDialogs { get; set; }
    public IReadOnlyList<string> DialogLog => _warnings;
    private readonly List<string> _warnings = [];

    /// <summary>Показать предупреждение из строки листа.</summary>
    public void ShowMessage(string msg) => Warn(msg);

    // ═════════════ Файл ═════════════

    private void DoOpen()
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Персонаж RogueTrader (*.rtchar.json)|*.rtchar.json|JSON (*.json)|*.json",
            InitialDirectory = AppPaths.CharactersDirectory,
        };
        if (ofd.ShowDialog() != true) return;
        try
        {
            Character = CharacterRepository.Load(ofd.FileName);
            _filePath = ofd.FileName;
            HasUnsavedChanges = false;
            Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть файл:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DoSave()
    {
        if (_filePath is null) { DoSaveAs(); return; }
        SaveTo(_filePath);
    }

    private void DoSaveAs()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Персонаж RogueTrader (*.rtchar.json)|*.rtchar.json",
            InitialDirectory = AppPaths.CharactersDirectory,
            FileName = CharacterRepository.SafeFileName(Character.Meta.Name),
        };
        if (sfd.ShowDialog() != true) return;
        _filePath = sfd.FileName;
        SaveTo(_filePath);
    }

    private void SaveTo(string path)
    {
        try
        {
            CharacterRepository.Save(Character, path);
            HasUnsavedChanges = false;
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось сохранить:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DoExportMarkdown()
    {
        AppPaths.EnsureDirectories();
        var path = Path.Combine(AppPaths.ExportDirectory,
            CharacterRepository.SafeFileName(Character.Meta.Name) + ".md");
        try
        {
            File.WriteAllText(path, CharacterRepository.ToMarkdown(Character, _svc), new System.Text.UTF8Encoding(false));
            MessageBox.Show($"Сохранено:\n{path}", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка экспорта:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DoExportPdf()
    {
        AppPaths.EnsureDirectories();
        var path = Path.Combine(AppPaths.ExportDirectory,
            CharacterRepository.SafeFileName(Character.Meta.Name) + ".pdf");
        try
        {
            PdfExportService.Export(Character, _svc, path);
            var res = MessageBox.Show($"PDF сохранён:\n{path}\n\nОткрыть папку?", "Экспорт",
                MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (res == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppPaths.ExportDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка экспорта PDF:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Собирает наполненного персонажа для демонстрации (флаг --demo).</summary>
public static void BuildDemo(MainViewModel vm)
    {
        var ch = vm.Character;
        ch.Meta.Name = "Иоганн Вальгар";
        ch.Meta.Age = "38";
        ch.Meta.Build = "жилистый";
        ch.Meta.Eyes = "серые, в шрамах";
        ch.Meta.Personality = "Холоден и расчётлив, верит в букву закона и в свою удачу. Не доверяет Инквизиции, но умеет с ней говорить.";

        // Род занятий выбираем через модель представления: так применяются
        // начальные умения и таланты и строятся строки стартового снаряжения.
        vm.SelectedCareer = vm.Svc.Pack.Career("free_trader");
        var rnd = new Random(20261019);
        foreach (var e in ch.Characteristics)
            e.Base = 25 + rnd.Next(2, 11) + rnd.Next(2, 11);

        var engine = vm.Engine;

        // Демо показывает персонажа «после нескольких сессий»: 500 ОО при создании
        // плюс заработанные. Покупки идут строго в пределах бюджета, иначе на листе
        // появилась бы отрицательная сумма, которой по книге быть не может.
        ch.XpEarned = 500 * 5;
        var budget = 500 + ch.XpEarned;
        void TryBuy(AdvanceOption opt)
        {
            if (opt.Cost > budget) return;
            budget -= opt.Cost;
            engine.ApplyAdvance(ch, opt);
        }

        // Заполняем ранг 1 целиком, а от ранга 2 — сколько хватит
        foreach (var opt in engine.AdvancesFor(ch, 1)) TryBuy(opt);
        foreach (var opt in engine.AdvancesFor(ch, 2)) TryBuy(opt);

        // Характеристики: по одному уровню улучшения, пока хватает опыта
        foreach (var id in new[] { "ws", "fel", "wil" })
        {
            var e = ch.FindCharacteristic(id);
            if (e is null || e.AdvancePoints != 0) continue;
            var cost = engine.CharacteristicAdvanceCost(ch.CareerId, id, 1);
            if (cost > budget) continue;
            budget -= cost;
            e.AdvancePoints = 5;
        }

        // Жизненный путь
        vm.SelectedHomeworld = _pack_Homeworld(vm);
        vm.SelectedDestiny = vm.Svc.Pack.Destiny.FirstOrDefault();
        vm.SelectedAspiration = vm.Svc.Pack.Aspiration.FirstOrDefault();

        ch.Traits.Add("Хромой (левая нога)");
        ch.Traits.Add("Знаток имперских законов");

        // Снаряжение: по одному представителю каждой категории.
        // Оружию задаём качество — по книге у оружия оно бывает разным.
        var demoQualities = new Dictionary<string, string>
        {
            [Core.Models.ItemCategory.RangedWeapon] = "good",
            [Core.Models.ItemCategory.MeleeWeapon] = "high",
        };
        foreach (var cat in new[]
                 {
                     Core.Models.ItemCategory.RangedWeapon,
                     Core.Models.ItemCategory.MeleeWeapon,
                     Core.Models.ItemCategory.Armor,
                     Core.Models.ItemCategory.Clothing,
                     Core.Models.ItemCategory.General,
                 })
        {
            var it = _pack_Items(vm).FirstOrDefault(x => x.Category == cat && x.Encumbrance > 0);
            if (it is not null)
                ch.Items.Add(new ItemEntry
                {
                    ItemId = it.Id,
                    Quality = demoQualities.TryGetValue(cat, out var q) ? q : "",
                });
        }

        ch.Wounds = 2;
        ch.FatePoints = 4;
        vm.Refresh();
    }

    private static NamedOptionDef? _pack_Homeworld(MainViewModel vm) =>
        vm.Svc.Pack.Homeworlds.FirstOrDefault(h => h.Name.Contains("смерти", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Демонстрация тау (флаг --demo=tau): воин Огня из дополнения
    /// «Руководство по Тау» с выявленной склонностью.
    /// </summary>
    public static void BuildTauDemo(MainViewModel vm)
    {
        var ch = vm.Character;
        ch.Meta.Name = "Ка'то Ау'ву";
        ch.Meta.Age = "34";
        ch.Meta.Build = "жилистый, ниже человеческого роста";
        ch.Meta.Eyes = "янтарные, без зрачка";
        ch.Meta.Personality = "Говорит коротко и по делу, считает каждое слово частью Высшего блага.";

        // Вид и род занятий задают форму броска характеристик и размер.
        vm.SelectedSpecies = vm.Svc.AllSpecies.FirstOrDefault(s => s.Id == "tau");
        vm.SelectedCareer = vm.Svc.Pack.Career("fire_warrior");

        var rnd = new Random(20261020);
        var species = vm.Svc.Pack.SpeciesOf("tau");
        foreach (var e in ch.Characteristics)
        {
            var b = species?.CharacteristicBase.GetValueOrDefault(e.CharacteristicId) ?? 25;
            e.Base = b + rnd.Next(2, 11) + rnd.Next(2, 11);
        }

        // Выявленная склонность — вместо родного мира.
        vm.SelectedInclination = vm.Svc.Pack.Inclinations.FirstOrDefault(i => i.Id == "pathfinder");

        var engine = vm.Engine;
        ch.XpEarned = 500 * 5;
        var budget = 500 + ch.XpEarned;
        void TryBuy(AdvanceOption opt)
        {
            if (opt.Cost > budget) return;
            budget -= opt.Cost;
            engine.ApplyAdvance(ch, opt);
        }

        foreach (var opt in engine.AdvancesFor(ch, 1)) TryBuy(opt);
        foreach (var opt in engine.AdvancesFor(ch, 2)) TryBuy(opt);

        foreach (var id in new[] { "bs", "fel", "wil" })
        {
            var e = ch.FindCharacteristic(id);
            if (e is null || e.AdvancePoints != 0) continue;
            var cost = engine.CharacteristicAdvanceCost(ch.CareerId, id, 1);
            if (cost > budget) continue;
            budget -= cost;
            e.AdvancePoints = 5;
        }

        ch.Traits.Add("Чуждый Империуму");
        ch.Wounds = 1;
        ch.FatePoints = 3;
        vm.Refresh();
    }

    private static IEnumerable<ItemDef> _pack_Items(MainViewModel vm) => vm.Svc.AllItems;

    // ═════════════ Строки для UI ═════════════

/// <summary>
/// Строка характеристики в виде «листа персонажа».
/// Три независимых поля: База · Улучш. (из магазина) · Прочее.
/// Только «Улучш.» стоит опыта — База и Прочее вводятся бесплатно.
/// </summary>
public sealed class CharacteristicRow : INotifyPropertyChanged
{
    private readonly MainViewModel _vm;
    private CharacteristicValue _cv;

    public CharacteristicRow(MainViewModel vm, CharacteristicValue cv)
    {
        _vm = vm;
        _cv = cv;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public CharacteristicValue Value => _cv;

    public void Update(CharacteristicValue fresh)
    {
        _cv = fresh;
        RaiseAll();
    }

    public void RaiseAll() => Raise(null);

    public string Id => _cv.Id;
    public string Name => _cv.Name;
    public string Abbrev => _cv.Abbrev;

    /// <summary>Формула броска базы — по виду персонажа («2к10+25», у тау иначе).</summary>
    public string RollText => _vm.RollFormula(_cv.Id);

    // ── колонки листа ──
    public string BaseText
    {
        get => _cv.Base.ToString();
        set { if (int.TryParse(value, out var v)) _vm.SetCharacteristicBase(_cv, v); else Raise(); }
    }

    /// <summary>
    /// «Улучш.» — покупается за опыт по таблице рода занятий. Вручную не меняется:
    /// значение считает система (покупки в разделе «Улучшения»).
    /// </summary>
    public string AdvanceText => _cv.AdvancePoints.ToString();

    /// <summary>
    /// «Прочее» — что заложено самой игрой: вид, род занятий, аугметика, вещи,
    /// награды жизненного пути. Считается системой, вручную не меняется.
    /// </summary>
    public string OtherText => _cv.Other.ToString();

    /// <summary>«Правки» — ручная правка мастера. Бесплатно.</summary>
    public string AdjustmentText
    {
        get => _cv.Adjustment.ToString();
        set
        {
            if (int.TryParse(value, out var v)) _vm.SetCharacteristicAdjustment(_cv, v);
            else Raise();
        }
    }

    /// <summary>Сколько в «Прочем» пришло от родного мира, судьбы и прочих выборов пути.</summary>
    public int PathPart => _cv.Path;
    public string PathText => _cv.Path == 0 ? "" : $"{(_cv.Path > 0 ? "+" : "")}{_cv.Path} от жизненного пути";

    /// <summary>Из чего сложилось «Прочее» — для подсказки под колонкой.</summary>
    public string OtherBreakdown
    {
        get
        {
            var parts = new List<string>();
            if (_cv.FromSpecies != 0) parts.Add($"{(_cv.FromSpecies > 0 ? "+" : "")}{_cv.FromSpecies} от вида");
            if (_cv.FromItems != 0) parts.Add($"{(_cv.FromItems > 0 ? "+" : "")}{_cv.FromItems} от снаряжения");
            if (_cv.Path != 0) parts.Add($"{(_cv.Path > 0 ? "+" : "")}{_cv.Path} от жизненного пути");
            return parts.Count == 0 ? "" : string.Join(", ", parts);
        }
    }

    public int Total => _cv.Total;
    public int CharBonus => _cv.CharBonus;
    public int BaseValue => _cv.Base;
    public int AdvancePoints => _cv.AdvancePoints;
    public int OtherValue => _cv.Other;
    public int AdjustmentValue => _cv.Adjustment;

    /// <summary>Начальное значение характеристики по виду (25 у людей, 20–30 у тау).</summary>
    public int DefaultBase => _vm.DefaultBaseFor(_cv.Id);

    /// <summary>Сколько очков внесено сверх начального значения.</summary>
    public int BaseExtra => Math.Max(0, _cv.Base - DefaultBase);

    /// <summary>Подпись под «Базой»: внесённые сверх начального очки.</summary>
    public string BaseExtraText => BaseExtra > 0 ? $"+{BaseExtra}" : "";

    public string BaseToolTip =>
        $"Начальное значение: {DefaultBase}. Внесено сверх него: {BaseExtra} очков " +
        $"(по книге — не больше {MainViewModel.MaxManualBasePoints} на характеристику).";

    /// <summary>Бонусы, приходящие со стороны (вид, снаряжение) — показываем в подсказке.</summary>
    public int ExternalBonus => _cv.FromSpecies + _cv.FromItems;
    public string ExternalText => ExternalBonus == 0 ? "" : $"+{ExternalBonus} от вида/снаряжения";

    public int MaxAdvance => _vm.Engine.Options.MaxCharacteristicPoints;
    public int Step => _vm.Engine.Options.CharacteristicStep;

    /// <summary>Цена следующих 5 очков улучшения по таблице рода занятий.</summary>
    public int NextCost => _cv.NextAdvanceCost;
    public string CostText => _cv.AdvancePoints >= MaxAdvance
        ? "предел +20"
        : $"{NextCost} ОО за +{Step}";

    public string AdvanceToolTip =>
        $"Купается за опыт: максимум +{MaxAdvance} шагом {Step}.\n" +
        $"Осталось улучшений: +{MaxAdvance - _cv.AdvancePoints}\n" +
        "Покупается кнопками «+» и «−» по таблице улучшений рода занятий.";

    public string OtherToolTip =>
        "Вид, род занятий, аугметика, снаряжение, награды жизненного пути.\n" +
        (OtherBreakdown.Length > 0 ? OtherBreakdown + "\n" : "") +
        "Считается системой, вручную не меняется.";

    public string AdjustmentToolTip =>
        "Ручная правка мастера: недостаток, излишек, ошибка броска.\n" +
        "Опыт за правку не тратится.";

    public string TotalToolTip =>
        $"Итог = База {_cv.Base} + Улучш. {_cv.AdvancePoints} + Прочее {_cv.Other} + Правки {_cv.Adjustment}" +
        $"\nБонус характеристики: +{CharBonus}";

    public void Inc() => _vm.SetCharacteristicAdvance(_cv, _cv.AdvancePoints + Step);
    public void Dec() => _vm.SetCharacteristicAdvance(_cv, _cv.AdvancePoints - Step);
}

    /// <summary>
    /// Умение в виде строки «листа персонажа»: слева направо
    /// База · девять колонок характеристик · Улучш. · Прочее · Итог.
    ///
    /// Вклад характеристик считается автоматически: +10, если бонус
    /// характеристики меньше +3 (значение до 29), и +20, если больше.
    /// Итог = База + вклад характеристик + Улучш. + Прочее.
    /// </summary>
    public sealed class SkillSheetRow : INotifyPropertyChanged
    {
        private readonly MainViewModel _vm;
        private SkillDef? _def;
        private SkillEntry? _entry;

        public SkillSheetRow(MainViewModel vm, SkillDef? def, SkillEntry? entry)
        {
            _vm = vm;
            _def = def;
            _entry = entry;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public SkillDef? Def => _def;
        public SkillEntry? Entry => _entry;
        public string Id => _def?.Id ?? "";

        public void Update(SkillDef def, SkillEntry? entry)
        {
            _def = def;
            _entry = entry;
        }

        public bool Learned => _entry is not null;
        public bool CanRemove => _entry is not null;

        public string Name => _def?.Name ?? "";
        public string Description => _def?.Description ?? "";
        public bool Advanced => _def?.Advanced ?? false;
        public bool Specialized => _def?.Specialized ?? false;
        public string TypeText => _def is null ? ""
            : (_def.Advanced ? (_def.Specialized ? "специализация" : "продвинутое") : "базовое");
        public string OriginText => _entry is null ? "не изучено" : OriginRu(_entry.Origin);
        public string ToggleGlyph => _entry is null ? "+" : "×";
        public string ToggleTip => _entry is null ? "Изучить умение" : "Убрать умение";

        /// <summary>Какие характеристики дают в это умение.</summary>
        public string Characteristics => _def is null || _def.Characteristics.Count == 0
            ? "без характеристики"
            : string.Join(", ", _def.Characteristics.Distinct().Select(RulesEngine.CharacteristicRu));

        public IReadOnlyList<SkillCell> Cells { get; private set; } = [];

        public void Rebuild()
        {
            // Показываем только те характеристики, от которых зависит умение:
            // девять колонок с нулями были лишним шумом.
            var cells = new List<SkillCell>();
            foreach (var ch in _vm.CharacteristicHeaders)
            {
                if (_def is null || !_def.Characteristics.Contains(ch.Id)) continue;
                cells.Add(new SkillCell
                {
                    CharacteristicId = ch.Id,
                    Abbrev = ch.Abbrev,
                    Name = ch.Name,
                    Value = ValueFrom(ch),
                    Half = !Learned && _def is not null && !_def.Advanced,
                });
            }
            Cells = cells;
            // Умение проверяется одной характеристикой: если в данных их
            // несколько (хоумбрю), вклад считается по лучшей, а не суммой.
            CharacteristicSum = cells.Count == 0 ? 0 : cells.Max(c => c.Value);
            RaiseAll();
        }

        /// <summary>
        /// Вклад характеристики в умение.
        ///
        /// Изученное умение: +10, если бонус характеристики меньше +3
        /// (значение до 29), и +20, если +3 и выше.
        ///
        /// Базовое не изученное умение применяется по половине связанной
        /// характеристики (округлять вниз) — так в базовой книге.
        /// Продвинутое не изученное умение применить нельзя: вклад нулевой.
        /// </summary>
        private int ValueFrom(CharacteristicHeader ch) =>
            Core.Rules.SkillValues.CharacteristicValue(_def, _entry, ch.Id, ch.Value, ch.Bonus);

        /// <summary>Умение берётся по половине характеристики (не изучено, базовое).</summary>
        public bool HalfCharacteristic =>
            !Learned && _def is not null && !_def.Advanced && CharacteristicSum > 0;

        /// <summary>Пояснение к вкладу характеристики.</summary>
        public string CharacteristicTip
        {
            get
            {
                if (_def is null) return "";
                var names = string.Join(", ", _def.Characteristics.Distinct().Select(RulesEngine.CharacteristicRu));
                if (Learned)
                    return $"{names}: +10 при бонусе меньше +3, +20 при +3 и выше.";
                if (_def.Advanced)
                    return $"{names}: продвинутое умение не изучено — применить нельзя.";
                var half = string.Join(", ", _def.Characteristics.Distinct()
                    .Select(id => _vm.CharacteristicHeaders.FirstOrDefault(h => h.Id == id))
                    .Where(h => h is not null)
                    .Select(h => $"{h!.Name} {h.Value} → половина {h.Value / 2}"));
                return $"Базовое умение не изучено — применяется по половине характеристики: {half}.";
            }
        }

        public string BaseText
        {
            get => (_entry?.Base ?? 0).ToString();
            set
            {
                if (!Learned) return;
                if (int.TryParse(value, out var v)) { _entry!.Base = Math.Clamp(v, 0, 200); _vm.MarkDirty(); }
                else Raise();
            }
        }

        /// <summary>
        /// «Улучш.» — ступени, купленные за опыт (0 → +0, 1 → +10, 2 → +20).
        /// Вручную не меняется: покупается в разделе «Улучшения».
        /// </summary>
        public string AdvanceText => ((_entry?.Rank ?? 0) * 10).ToString();

        /// <summary>«Прочее» — надбавки, заложенные игрой (таланты, снаряжение, путь).</summary>
        public string OtherText => (_entry?.Other ?? 0).ToString();

        /// <summary>«Правки» — ручная правка мастера. Бесплатно.</summary>
        public string AdjustmentText
        {
            get => (_entry?.Adjustment ?? 0).ToString();
            set
            {
                if (!Learned) return;
                if (int.TryParse(value, out var v)) _vm.SetSkillAdjustment(this, v);
                else Raise();
            }
        }

        public int CharacteristicSum { get; private set; }

        /// <summary>
        /// Итог листа умений. Изученное умение: База + вклад характеристик +
        /// улучшения + прочее + правки. Неизученное базовое умение применимо
        /// по половине характеристики, продвинутое — не применимо вовсе.
        /// </summary>
        public int Total => Core.Rules.SkillValues.Total(_def, _entry, _vm.Stats);

        public string TotalToolTip => !Learned
            ? (_def is not null && _def.Advanced
                ? "Умение не изучено: продвинутое умение применяется только изученным."
                : "Базовое умение не изучено: применяется по половине связанной характеристики (округлять вниз).")
            : $"Итог = База {_entry!.Base} + характеристики {CharacteristicSum}"
              + $" + улучшения {_entry.Rank * 10} + прочее {_entry.Other} + правки {_entry.Adjustment}";

        public void RaiseAll() => Raise(null);
    }

    /// <summary>Награда жизненного пути с переключателем «применять / не применять».</summary>
    public sealed class PathGrantRow
    {
        private readonly MainViewModel _vm;

        public PathGrantRow(MainViewModel vm, string origin, PathGrant grant, string key, bool enabled)
        {
            _vm = vm; Origin = origin; Grant = grant; Key = key; Enabled = enabled;
        }

        public string Origin { get; }
        public PathGrant Grant { get; }
        public string Key { get; }
        public bool Enabled { get; }

        public string OriginText => OriginRu(Origin);
        public string Label => Grant.Label;
        public string SourceText => Grant.SourceText;
        /// <summary>По книге нужно выбрать один из вариантов — отмечаем это.</summary>
        public bool Optional => Grant.Optional;
        public string OptionalText => Grant.Optional ? "выбор по книге" : "";

        public void Toggle() => _vm.TogglePathGrant(this);
    }

    /// <summary>
    /// Вариант стартового снаряжения: предмет пака, качество и количество.
    /// </summary>
    public sealed class StartingEquipmentOption
    {
        public StartingEquipmentOption(ItemDef item, string qualityId, int quantity)
        {
            Item = item; QualityId = qualityId; Quantity = quantity;
        }

        public ItemDef Item { get; }
        public string QualityId { get; }
        public int Quantity { get; }
        /// <summary>Строка, которой принадлежит вариант.</summary>
        public StartingEquipmentRow? Row { get; set; }

        public string QualityName => WeaponQualities.ByName(QualityId)?.Name ?? "";
        public bool HasQuality => QualityId.Length > 0;

        /// <summary>Название с качеством и количеством — как показывать в списке.</summary>
        public string Text
        {
            get
            {
                var s = Item.Name;
                if (HasQuality) s += $" ({QualityName.ToLowerInvariant()} качество)";
                if (Quantity > 1) s += $" ×{Quantity}";
                return s;
            }
        }

        /// <summary>Короткая справка: вид снаряжения и доступность по книге.</summary>
        public string Detail
        {
            get
            {
                var parts = new List<string> { ItemCategory.Ru(Item.Category) };
                if (!string.IsNullOrWhiteSpace(Item.WeaponClass)) parts.Add(ItemDef.WeaponClassRu(Item.WeaponClass));
                if (!string.IsNullOrWhiteSpace(Item.Availability)) parts.Add("доступность: " + Item.Availability);
                return string.Join(" · ", parts);
            }
        }
    }

    /// <summary>
    /// Группа предметов строки стартового снаряжения — то, что выдаётся
    /// вместе. Если групп несколько («или»), игрок выбирает одну.
    /// </summary>
    public sealed class StartingEquipmentVariant
    {
        public StartingEquipmentVariant(StartingEquipmentRow row, int index, List<StartingEquipmentOption> options)
        {
            Row = row; Index = index; Options = options;
        }

        public StartingEquipmentRow Row { get; }
        public int Index { get; }
        public List<StartingEquipmentOption> Options { get; }

        public string Text => StartingEquipmentRow.VariantText(Options);
        public string Detail => Options.Count > 0 ? Options[0].Detail : "";

        /// <summary>Выбор есть — показываем кнопку.</summary>
        public bool IsButton => Row.IsChoice;
        /// <summary>Выбора нет — предметы выдаются автоматически.</summary>
        public bool IsText => !Row.IsChoice;
        public bool IsSelected => Row.IsChoice && Row.Chosen == Index.ToString();

        public string StatusText => Row.IsChoice
            ? (IsSelected ? "выбрано" : "выбрать вариант")
            : "выдаётся автоматически";

        public void Choose() => Row.Choose(Index);
    }

    /// <summary>
    /// Строка стартового снаряжения вида, рода занятий или варианта пути.
    ///
    /// В книге предметы перечисляют двумя способами: списком («меч, плащ,
    /// перчатки») — значит, выдаётся всё; или через «или» («лазпистолет … или
    /// ручная пушка …») — значит, выбирает игрок. Поэтому варианты хранятся
    /// группами: список — одна группа с несколькими предметами, «или» — по
    /// группе на каждый.
    /// </summary>
    public sealed class StartingEquipmentRow
    {
        private readonly MainViewModel _vm;

        public StartingEquipmentRow(MainViewModel vm, string key, string origin, string sourceName,
            string text, List<List<StartingEquipmentOption>> variants, string chosen,
            List<string>? missing = null)
        {
            _vm = vm; Key = key; Origin = origin; SourceName = sourceName;
            Text = text; Variants = variants; Chosen = chosen;
            MissingParts = missing ?? [];
            for (var i = 0; i < variants.Count; i++)
            {
                var v = variants[i];
                foreach (var o in v) o.Row = this;
                VariantRows.Add(new StartingEquipmentVariant(this, i, v));
            }
        }

        /// <summary>Части строки, которых нет в паке (показать игроку).</summary>
        public List<string> MissingParts { get; }

        /// <summary>Группы вариантов — для показа в списке.</summary>
        public List<StartingEquipmentVariant> VariantRows { get; } = [];

        public string Key { get; }
        public string Origin { get; }
        public string SourceName { get; }
        /// <summary>Строка из книги — как она напечатана.</summary>
        public string Text { get; }
        /// <summary>Группы вариантов: одна — выдаётся целиком, несколько — выбор.</summary>
        public List<List<StartingEquipmentOption>> Variants { get; }
        /// <summary>Индекс выбранного варианта (пусто — выбор не сделан).</summary>
        public string Chosen { get; }

        /// <summary>Все предметы строки — для показа.</summary>
        public IEnumerable<StartingEquipmentOption> Options => Variants.SelectMany(v => v);

        public string OriginText => Origin switch
        {
            "species" => $"вид: {SourceName}",
            "career" => $"род занятий: {SourceName}",
            "homeworld" => $"родной мир: {SourceName}",
            "destiny" => $"уготованная судьба: {SourceName}",
            "void" => $"путь в пустоту: {SourceName}",
            "hardship" => $"испытание: {SourceName}",
            "aspiration" => $"устремление: {SourceName}",
            "legacy" => $"наследие: {SourceName}",
            _ => SourceName,
        };

        public bool HasOptions => Variants.Sum(v => v.Count) > 0;
        /// <summary>
        /// В книге есть «или» — выбирает игрок. Смотрим именно на текст:
        /// даже если часть вариантов нет в паке, выбор остаётся за игроком.
        /// </summary>
        public bool IsChoice => Text.Contains(" или ", StringComparison.OrdinalIgnoreCase);
        public string ChoiceText => IsChoice ? "выбор по книге" : "";

        /// <summary>Выбранный вариант (если есть).</summary>
        public List<StartingEquipmentOption>? ChosenVariant =>
            int.TryParse(Chosen, out var idx) && idx >= 0 && idx < Variants.Count ? Variants[idx] : null;

        /// <summary>Выдано ли уже это снаряжение.</summary>
        public bool IsGranted => !IsChoice || ChosenVariant is not null;

        /// <summary>Подсказка для строк, которых нет в паке.</summary>
        public string MissingText => HasOptions ? "" : "в паке нет — подобрать вручную";
        public bool HasMissing => !HasOptions;

        /// <summary>Части строки, которых нет в паке (например «мономеч»).</summary>
        public string MissingPartsText =>
            MissingParts.Count > 0 ? "нет в паке: " + string.Join(", ", MissingParts) : "";
        public bool HasMissingParts => MissingParts.Count > 0;

        /// <summary>Название варианта для кнопки выбора.</summary>
        public static string VariantText(List<StartingEquipmentOption> variant) =>
            string.Join(", ", variant.Select(o => o.Text));

        public void Choose(int index) => _vm.ChooseStartingEquipment(this, index);
        public void Remove() => _vm.UnchooseStartingEquipment(this);
        public void PickManual() => _vm.PickStartingEquipmentManually(this);
    }

    /// <summary>Заголовок колонки характеристики для листа умений.</summary>
    public sealed class CharacteristicHeader
    {
        public string Id { get; init; } = "";
        public string Abbrev { get; init; } = "";
        public string Name { get; init; } = "";
        public int Bonus { get; set; }
        /// <summary>Полное значение характеристики — нужно для правила половины.</summary>
        public int Value { get; set; }
    }

    /// <summary>Ячейка «характеристика → умение».</summary>
    public sealed class SkillCell
    {
        public string CharacteristicId { get; init; } = "";
        public string Abbrev { get; init; } = "";
        public string Name { get; init; } = "";
        public int Value { get; set; }
        /// <summary>true — вклад взят по половине характеристики.</summary>
        public bool Half { get; set; }
    }

    public sealed class TalentRow
    {
        public TalentRow(TalentDef? def, TalentEntry entry, bool ok, string status)
        {
            Def = def; Entry = entry; Ok = ok; Status = status;
        }
        public TalentDef? Def { get; }
        public TalentEntry Entry { get; }
        public bool Ok { get; }
        public string Status { get; }
        public string Name => Def?.Name ?? Entry.TalentId;
        /// <summary>
        /// Ранг таланта по книге. В модели ранг хранится с нуля (0 — стартовый
        /// талант), а в книге таланты нумеруются с единицы: стартовый — первый.
        /// Поэтому показываем ранг только у купленного таланта и на единицу больше.
        /// </summary>
        public string RankText => Entry.Rank > 0 ? $"ранг {Entry.Rank + 1}" : "";
        public string Prerequisites => Def is null || Def.Prerequisites.Count == 0 ? "" : string.Join("; ", Def.Prerequisites);
        /// <summary>Есть ли требования — иначе строка под названием пустая.</summary>
        public bool HasPrerequisites => !string.IsNullOrWhiteSpace(Prerequisites);
        public string Description => Def?.Description ?? "";
    }

    /// <summary>
    /// Строка снаряжения в списке. Показывает всё, что есть в таблице главы
    /// «Арсенал» для этого предмета: класс, дальность, режим огня, урон,
    /// бронебойность, магазин, перезарядку, вес, доступность, особые свойства —
    /// плюс дословное описание из книги и название источника.
    /// </summary>
    public sealed class ItemRow
    {
        public ItemRow(ItemDef def, ItemEntry entry, ItemSectionDef? section, ItemGroupDef? group)
        {
            Def = def; Entry = entry; Section = section; Group = group;
        }

        public ItemDef Def { get; }
        public ItemEntry Entry { get; }
        public ItemSectionDef? Section { get; }
        public ItemGroupDef? Group { get; }

        /// <summary>Заголовок группы верхнего уровня — раздел главы «Арсенал».</summary>
        public string SectionTitle => Section?.Title ?? (Entry.Custom is not null ? "Свои предметы" : "Прочее");
        /// <summary>Заголовок группы второго уровня — подраздел.</summary>
        public string GroupTitle => Group?.Title ?? ItemCategory.Ru(Def.Category);

        public string Name => Def.Name;
        public string Category => ItemCategory.Ru(Def.Category);

        /// <summary>Стр. книги, откуда взят предмет, — так проще искать в оригинале.</summary>
        public string PageText => Section is null ? "" : $"стр. {Section.Page}";
        public string GroupPageText => Group is null ? "" : $"стр. {Group.Page}";

        /// <summary>Из какой книги предмет: базовая или дополнение.</summary>
        public string SourceText => BookSourceRu(Def.Source);

        /// <summary>
        /// Доступность по книге (Редкий, Скудный, Почти уникальный…): в
        /// таблицах «Арсенала» цены нет, она определяется по уровню дохода.
        /// У своего предмета доступности в книге нет — значит, и не пишем.
        /// </summary>
        public string Availability => string.IsNullOrWhiteSpace(Def.Availability)
            ? (Entry.Custom is not null ? "" : "средняя")
            : Def.Availability;

        public string Encumbrance => Def.Encumbrance > 0 ? $"{Def.Encumbrance} кг" : "";
        public string Protection => Def.Protection > 0 ? $"защита {Def.Protection}" : "";
        public string Range => Def.Range;
        public string Traits => string.Join(", ", Def.Traits);
        public string WeaponClass => ItemDef.WeaponClassRu(Def.WeaponClass);

        public string Description => Def.Description;
        public bool HasDescription => !string.IsNullOrWhiteSpace(Def.Description);
        public bool HasTraits => EffectiveTraits.Count > 0;
        public bool HasRules => Def.SpecialRules.Count > 0;
        public bool HasEffect => !string.IsNullOrWhiteSpace(Def.Effect);
        public bool HasNote => !string.IsNullOrWhiteSpace(Def.Note);
        /// <summary>Примечание к предмету в инвентаре (например, «старт: career:0»).</summary>
        public string EntryNote => Entry.Note;
        public bool HasEntryNote => !string.IsNullOrWhiteSpace(Entry.Note);
        public string SpecialRulesText => string.Join("\n", Def.SpecialRules);
        public string EffectText => Def.Effect;
        public string NoteText => Def.Note;
        public string LocationText => Def.Location;

        // ── Характеристики оружия из таблиц главы «Арсенал» ──
        public string Damage => Def.Damage;
        public string Penetration => Def.Penetration;
        public string Magazine => Def.Magazine;
        public string Reload => Def.Reload;
        public string FireMode => Def.FireMode;
        public string RangeText => Def.Range;
        public string Weight => Def.Weight;

        /// <summary>Короткая строка характеристик для списка.</summary>
        public string StatsLine
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Def.WeaponClass)) parts.Add(ItemDef.WeaponClassRu(Def.WeaponClass));
                if (!string.IsNullOrWhiteSpace(Def.Damage)) parts.Add("урон " + EffectiveDamage);
                if (!string.IsNullOrWhiteSpace(Def.Penetration)) parts.Add("ББ " + Def.Penetration);
                if (!string.IsNullOrWhiteSpace(Def.Magazine)) parts.Add("маг. " + Def.Magazine);
                if (!string.IsNullOrWhiteSpace(Def.Reload)) parts.Add("пер. " + Def.Reload);
                if (!string.IsNullOrWhiteSpace(Def.FireMode)) parts.Add("темп " + Def.FireMode);
                if (!string.IsNullOrWhiteSpace(Def.Range)) parts.Add(Def.Range);
                if (Def.Protection > 0) parts.Add("защита " + Def.Protection);
                if (!string.IsNullOrWhiteSpace(Def.Weight)) parts.Add(Def.Weight);
                else if (Def.Encumbrance > 0) parts.Add($"{Def.Encumbrance} кг");
                if (!string.IsNullOrWhiteSpace(Def.Availability)) parts.Add(Def.Availability);
                if (HasQuality) parts.Add("качество " + QualityName.ToLowerInvariant());
                return string.Join(" · ", parts);
            }
        }

        /// <summary>Оружие ли это (для оружия действует правило качества, стр. 114).</summary>
        public bool IsWeapon => Def.Category is "ranged_weapon" or "melee_weapon" or "chain_weapon"
            or "power_weapon" or "force_weapon" or "launcher";

        /// <summary>Стрелковое ли это оружие — для него правила качества свои.</summary>
        public bool IsRanged => Def.Category is "ranged_weapon" or "launcher";

        /// <summary>Качество, записанное в этом предмете. По умолчанию — среднее.</summary>
        public WeaponQualityDef? Quality =>
            WeaponQualities.ByName(Entry.Quality) ?? (IsWeapon ? WeaponQualities.Default : null);

        public bool HasQuality => Quality is not null;
        public string QualityName => Quality?.Name ?? "";

        /// <summary>Правило качества — отдельно для стрелкового и холодного оружия.</summary>
        public string QualityEffect
        {
            get
            {
                var q = Quality;
                if (q is null) return "";
                var sb = new System.Text.StringBuilder(q.Effect);
                if (IsRanged)
                {
                    if (q.Id == "low") sb.Append(" Стрелковое оружие низкого качества получает свойство «Ненадёжное»: осечка при каждом промахе.");
                    else if (q.Id == "good") sb.Append(" Стрелковое оружие хорошего качества получает свойство «Надёжное».");
                    else if (q.Id == "high") sb.Append(" Стрелковое оружие высшего качества никогда не даёт осечек и не перегревается.");
                }
                else
                {
                    if (q.MeleeAttackPenalty > 0)
                        sb.Append($" Холодное оружие низкого качества получает штраф −{q.MeleeAttackPenalty} к тестам, предпринятым при атаках и парировании.");
                    if (q.MeleeAttackBonus > 0)
                        sb.Append($" Холодное оружие получает бонус +{q.MeleeAttackBonus} к тестам, предпринятым при атаке.");
                    if (q.MeleeDamageBonus > 0)
                        sb.Append($" И +{q.MeleeDamageBonus} к наносимому урону.");
                }
                return sb.ToString();
            }
        }

        /// <summary>Свойства предмета с учётом выбранного качества.</summary>
        public IReadOnlyList<string> EffectiveTraits
        {
            get
            {
                var list = new List<string>(Def.Traits);
                var q = Quality;
                if (q is null) return list;
                var add = IsRanged ? q.RangedTraits : q.MeleeTraits;
                foreach (var t in add) if (!list.Contains(t)) list.Add(t);
                return list;
            }
        }

        /// <summary>Урон с учётом качества (высшее холодное оружие бьёт на очко злее).</summary>
        public string EffectiveDamage
        {
            get
            {
                var q = Quality;
                if (q is null || q.MeleeDamageBonus == 0 || string.IsNullOrWhiteSpace(Def.Damage)) return Def.Damage;
                var m = System.Text.RegularExpressions.Regex.Match(Def.Damage, @"\+(\d+)");
                if (!m.Success) return Def.Damage;
                var newVal = int.Parse(m.Groups[1].Value) + q.MeleeDamageBonus;
                return Def.Damage.Remove(m.Index, m.Length).Insert(m.Index, "+" + newVal);
            }
        }

        /// <summary>
        /// Тип урона по базовой книге (стр. 241): взрывной, резаный, ударный,
        /// энергетический. Если тип не указан — он считается ударным.
        /// В листе оружия это колонка «Тип».
        /// </summary>
        public string DamageType
        {
            get
            {
                var dm = (Def.Damage ?? "").Trim();
                if (dm.Length == 0) return "";
                var last = dm.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
                if (last is "-" or "–")
                {
                    var boom = Def.Traits.Concat(Def.SpecialRules)
                        .Any(t => t.StartsWith("Взрыв", StringComparison.CurrentCulture));
                    return boom ? "взрывной" : "";
                }
                if (last.Any(char.IsDigit)) return "ударный (по умолчанию)";
                return last switch
                {
                    "Э" => "энергетический",
                    "Р" => "резаный",
                    "У" => "ударный",
                    "В" => "взрывной",
                    _ => last,
                };
            }
        }

        /// <summary>Одна строка «Поле: значение» для подробной карточки.</summary>
        public IEnumerable<ItemStatLine> StatLines
        {
            get
            {
                var lines = new List<ItemStatLine>();
                void Add(string name, string? value)
                {
                    if (!string.IsNullOrWhiteSpace(value)) lines.Add(new ItemStatLine(name, value!));
                }
                Add("Класс", string.IsNullOrWhiteSpace(Def.WeaponClass) ? "" : ItemDef.WeaponClassRu(Def.WeaponClass));
                Add("Урон", EffectiveDamage);
                Add("Тип урона", DamageType);
                Add("Бронебойность", Def.Penetration);
                Add("Дальность", Def.Range);
                Add("Режим огня", Def.FireMode);
                Add("Магазин", Def.Magazine);
                Add("Перезарядка", Def.Reload);
                Add("Защита", Def.Protection > 0 ? Def.Protection.ToString() : "");
                Add("Вес", !string.IsNullOrWhiteSpace(Def.Weight) ? Def.Weight
                    : Def.Encumbrance > 0 ? $"{Def.Encumbrance} кг" : "");
                Add("Доступность", Def.Availability);
                Add("Качество", QualityName);
                Add("Место установки", Def.Location);
                return lines;
            }
        }
    }

    /// <summary>
    /// Дисциплина психических приёмов: сколько приёмов изучено и постигнута ли она.
    ///
    /// Дисциплина считается постигнутой, когда выучено восемь её приёмов.
    /// </summary>
    public sealed class PsyDisciplineStat
    {
        public PsyDisciplineStat(string name, int learned, int total)
        {
            Name = name; Learned = learned; Total = total;
        }

        public string Name { get; }
        public int Learned { get; }
        public int Total { get; }
        public bool Mastered => Learned >= 8;
        public string CountText => $"изучено {Learned} из {Total}";
        public string Mark => Mastered ? "\u2713" : Learned > 0 ? "\u25D0" : "\u25CB";
    }

    /// <summary>Строка психического приёма в списке.</summary>
    public sealed class PsyRow
    {
        public PsyRow(PsyAbilityDef def, bool learned)
        {
            Def = def; Learned = learned;
        }

        public PsyAbilityDef Def { get; }
        public bool Learned { get; set; }

        public string Id => Def.Id;
        public string Name => Def.Name;
        public string Discipline => Def.Discipline;
        public string CostText => Def.CostText;
        public string Action => Def.Action;
        public string Sustain => Def.Sustain;
        public string Range => Def.Range;
        public string Test => Def.Test;
        public string Requirements => Def.Requirements;
        public string Effect => Def.Effect;
        public string Notes => Def.Notes;
        public string SourceText => BookSourceRu(Def.Source);
        public bool HasNotes => !string.IsNullOrWhiteSpace(Def.Notes);
        public bool HasEffect => !string.IsNullOrWhiteSpace(Def.Effect);
        public string Glyph => Learned ? "×" : "+";
        public string Tip => Learned ? "Забыть приём" : "Изучить приём";

        /// <summary>
        /// Свойства приёма — как в книге: применение, поддержание, дальность,
        /// тест сотворения психосилы, требования.
        /// </summary>
        public IReadOnlyList<StatPair> StatPairs => FormatPairs(
            ("Применение", Def.Action),
            ("Поддержание", Def.Sustain),
            ("Дальность", Def.Range),
            ("Тест соображения психоника", Def.Test),
            ("Требования", Def.Requirements));
    }

    /// <summary>Пара «подпись — значение» в карточке приёма или силы навигатора.</summary>
    public sealed record StatPair(string Name, string Value);

    /// <summary>
    /// Собирает пары «подпись — значение»: убирает пустые значения и «нет»,
    /// слово «тест» из значения проверки (оно уже есть в подписи) и
    /// переводит «да»/«нет» в понятные слова.
    /// </summary>
    internal static IReadOnlyList<StatPair> FormatPairs(params (string Name, string Value)[] pairs)
    {
        var list = new List<StatPair>();
        foreach (var (name, raw) in pairs)
        {
            var v = (raw ?? "").Trim();
            if (v.Length == 0 || v is "нет" or "-" or "—") continue;
            // «Тест …: тест Силы воли» — слово «тест» в значении не нужно:
            // в книге оно есть, но в подписи поля оно и так сказано.
            if ((name.StartsWith("Тест", StringComparison.OrdinalIgnoreCase)
                 || name.Equals("Проверка", StringComparison.OrdinalIgnoreCase))
                && v.StartsWith("тест ", StringComparison.OrdinalIgnoreCase))
                v = v["тест ".Length..].Trim();
            if (v.Length == 0) continue;
            list.Add(new StatPair(name, v));
        }
        return list;
    }

    /// <summary>Строка силы навигатора: уровень владения Послушник → Адепт → Мастер.</summary>
    public sealed class NavPowerRow
    {
        private readonly MainViewModel _vm;

        public NavPowerRow(MainViewModel vm, NavigatorPowerDef def)
        {
            _vm = vm; Def = def;
        }

        public NavigatorPowerDef Def { get; }
        public NavigatorPowerEntry? Entry { get; set; }
        public bool Learned => Entry is not null;

        public string Id => Def.Id;
        public string Name => Def.Name;
        public string Test => Def.Test;
        public string Effect => Def.Effect;
        public string Notes => Def.Notes;
        public string SourceText => BookSourceRu(Def.Source);
        public bool HasNotes => !string.IsNullOrWhiteSpace(Def.Notes);

        public string Proficiency => NavigatorProficiency.Title(Entry?.Proficiency ?? NavigatorProficiency.Novice);
        public string BonusText => $"+{NavigatorProficiency.Bonus(Entry?.Proficiency ?? NavigatorProficiency.Novice)}";
        public bool CanUp => Entry is not null && NavigatorProficiency.CanLevelUp(Entry.Proficiency);
        public bool CanDown => Entry is not null && NavigatorProficiency.CanLevelDown(Entry.Proficiency);
        public string Glyph => Learned ? "×" : "+";

        /// <summary>Свойства силы — отдельными строками «подпись — значение».</summary>
        public IReadOnlyList<StatPair> StatPairs => FormatPairs(("Проверка", Def.Test));

        // Запись ищем по id в персонаже: список строк пересобирается при любом
        // изменении, и ссылка Entry у этой строки успевает устареть.
        public void Up() => _vm.RaiseNavProficiency(Id);
        public void Down() => _vm.LowerNavProficiency(Id);
    }

    /// <summary>Пара «поле — значение» в подробной карточке предмета.</summary>
    public sealed record ItemStatLine(string Name, string Value);

    /// <summary>Откуда взят предмет: базовая книга, дополнение или свой.</summary>
    public static string BookSourceRu(string? source) => source switch
    {
        null or "" or "core" => "Базовая книга правил",
        "darkmarkets" => "Дополнение «Чёрные рынки»",
        "stormstout" => "Дополнение «Сквозь шторм»",
        "abhuman" => "Дополнение «Свиньины Империума»",
        "tau" => "Дополнение «Кодекс Тау»",
        "custom" => "Свой предмет",
        _ => source,
    };

    /// <summary>
    /// Строка улучшения из таблицы ранга. Показывает, что именно игрок покупает:
    /// описание умения или таланта из пака, эффекты, требования.
    /// </summary>
    public sealed class AdvanceRow
    {
        public AdvanceRow(AdvanceOption? opt, bool ok, string status, int taken, AppliedAdvance? applied = null,
                          GamePack? pack = null)
        {
            Option = opt; Ok = ok; Status = status; Taken = taken; Applied = applied; Pack = pack;
        }
        public AdvanceOption? Option { get; }
        public bool Ok { get; }
        public string Status { get; }
        public int Taken { get; }
        public AppliedAdvance? Applied { get; }
        public GamePack? Pack { get; }
        public string Name => Option?.Name ?? "(удалено)";
        public string Type => Option?.Type ?? "";
        public int Cost => Option?.Cost ?? 0;
        public string Requirements => Option is null || string.IsNullOrWhiteSpace(Option.Requirements) || Option.Requirements == "-"
            ? "без требований" : Option.Requirements;
        public bool Bought => Applied is not null;
        public string RequirementStatus => string.IsNullOrWhiteSpace(Status) ? Requirements : Status;

        private SkillDef? Skill => Option?.SkillId is not null ? Pack?.Skill(Option.SkillId) : null;
        private TalentDef? Talent => Option?.TalentId is not null ? Pack?.Talent(Option.TalentId) : null;

        /// <summary>Дословное описание из книги — что игрок получит.</summary>
        public string Description
        {
            get
            {
                if (Option is null) return "";
                var skill = Skill;
                if (skill is not null) return skill.Description;
                var talent = Talent;
                if (talent is not null) return talent.Description;
                if (Option.IsCharacteristic && !string.IsNullOrEmpty(Option.CharacteristicId))
                {
                    var name = Pack?.OrderedCharacteristics
                        .FirstOrDefault(c => c.Id == Option.CharacteristicId)?.Name ?? Option.CharacteristicId;
                    return $"+{Option.CharacteristicPoints} к «{name}» — уровень «{LevelName}».";
                }
                return "";
            }
        }

        /// <summary>Первая строка описания — короткая выжимка для списка.</summary>
        public string ShortDescription
        {
            get
            {
                var d = FlatDescription;
                if (d.Length == 0) return "";
                var cut = d.IndexOf(". ", StringComparison.Ordinal);
                if (cut > 40) d = d[..(cut + 1)];
                if (d.Length > 180) d = d[..177].TrimEnd() + "…";
                return d;
            }
        }

        /// <summary>Продолжение описания — то, что не вошло в выжимку.</summary>
        public string DetailDescription
        {
            get
            {
                var d = FlatDescription;
                if (d.Length == 0) return "";
                var cut = d.IndexOf(". ", StringComparison.Ordinal);
                if (cut > 40) d = d[(cut + 1)..].Trim();
                return d;
            }
        }

        private string FlatDescription =>
            Description.Replace("\r", " ").Replace("\n", " ").Trim();

        public bool HasShortDescription => ShortDescription.Length > 0;

        /// <summary>Путь развития по уровню улучшения — из таблицы рода занятий.</summary>
        private string LevelName
        {
            get
            {
                var id = Option?.Id?.Split('#').FirstOrDefault();
                var names = id is null ? null : Pack?.Career(id)?.AdvancementLevelNames;
                var idx = Math.Clamp((Option?.CharacteristicLevel ?? 1) - 1, 0, 3);
                return names is not null && names.Count > idx ? names[idx] : $"Уровень {idx + 1}";
            }
        }

        /// <summary>
        /// Подробности: характеристики умения, требования таланта, эффекты,
        /// которых нет в описании, и пометка о специализации.
        /// </summary>
        public IReadOnlyList<string> DetailLines
        {
            get
            {
                var lines = new List<string>();
                var skill = Skill;
                var talent = Talent;

                if (skill is not null)
                {
                    var chars = skill.Characteristics
                        .Select(id => Pack?.OrderedCharacteristics.FirstOrDefault(c => c.Id == id)?.Name ?? id)
                        .Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                    if (chars.Count > 0) lines.Add("Проверяется: " + string.Join(", ", chars));
                    if (skill.Advanced) lines.Add("Продвинутое умение — берётся раз в два раза дороже.");
                    if (skill.Specialized) lines.Add("Требует специализации.");
                    foreach (var e in skill.Expertise)
                        if (!string.IsNullOrWhiteSpace(e.Name)) lines.Add("Специализация: " + e.Name);
                }

                if (talent is not null)
                {
                    if (talent.Ranks > 1) lines.Add($"Рангов таланта: {talent.Ranks}.");
                    foreach (var p in talent.Prerequisites)
                        if (!string.IsNullOrWhiteSpace(p)) lines.Add("Требование таланта: " + p);
                    // Эффекты, которых нет в описании: иначе текст задвоится.
                    foreach (var e in talent.Effects)
                    {
                        if (string.IsNullOrWhiteSpace(e.Text)) continue;
                        if (talent.Description.Contains(e.Text, StringComparison.OrdinalIgnoreCase)) continue;
                        lines.Add(EffectLabel(e.Trigger) + ": " + e.Text);
                    }
                }

                if (!string.IsNullOrWhiteSpace(Option?.Specialization))
                    lines.Add("Специализация: " + Option!.Specialization);

                return lines;
            }
        }

        private static string EffectLabel(string trigger) => trigger switch
        {
            "one_time" => "Одноразовый эффект",
            "simple" => "Простое действие",
            "special" => "Особое правило",
            _ => "Пассивный эффект",
        };

        public bool HasDetails => DetailDescription.Length > 0 || DetailLines.Count > 0;
    }
}

internal static class Ext
{
    public static string Join<T>(this IEnumerable<T> src, string sep) => string.Join(sep, src);
    public static void Let(this object? _) { }
}