using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;

/// <summary>폼의 절 하나 — 절의 순서와 제목은 <see cref="DevicePropertyCatalog"/> 가 정한다.</summary>
public sealed class PropertySectionViewModel
{
    public PropertySectionViewModel(DevicePropertySection section, IReadOnlyList<PropertyFieldViewModel> fields, bool isNotReceived)
    {
        Section = section;
        Title = DevicePropertyCatalog.SectionTitle(section);
        AxisName = DevicePropertyCatalog.SectionAxisName(section);
        Fields = fields;
        IsNotReceived = isNotReceived;
    }

    public DevicePropertySection Section { get; }
    public string Title { get; }
    public string? AxisName { get; }
    public IReadOnlyList<PropertyFieldViewModel> Fields { get; }

    /// <summary>이 절의 모든 축 칸이 "미수신"이다 — 칸 대신 미수신 상자 하나를 보인다.</summary>
    public bool IsNotReceived { get; }
}

/// <summary>[적용] · [등록] 을 눌렀을 때 폼이 한 일.</summary>
/// <param name="IsWritten">행에 값을 썼다 — 이제 패널의 저장을 부르면 된다.</param>
/// <param name="RowCount">값을 쓴 행 수.</param>
/// <param name="FieldCount">쓴 칸 수.</param>
/// <param name="Message">못 썼을 때의 한 줄.</param>
public sealed record PropertyFormCommit(bool IsWritten, int RowCount, int FieldCount, string? Message);

/// <summary>
/// 정규화된 속성 명세에서 <b>만들어지는</b> 상세 폼. 장비 종류마다 폼을 손으로 짜지 않는다.
/// </summary>
/// <remarks>
/// <para>흐름: 목록 선택 → <see cref="Load"/> → 칸 입력(칸 안에만 머문다) → <see cref="Commit"/> 가 검증하고 손댄 칸만 행에 쓴다
/// → 콘솔이 패널의 기존 저장 경로를 부른다(전송은 폼의 일이 아니다).</para>
/// <para>검증에 하나라도 걸리면 <b>아무 행에도 쓰지 않는다</b> — 절반만 고친 행이 저장 경로로 새지 않게.</para>
/// </remarks>
public sealed class DevicePropertyFormViewModel : PropertyChangedBase
{
    private readonly IDevicePropertyOptions? _options;
    private IReadOnlyList<object> _rows = Array.Empty<object>();
    private IReadOnlyList<PropertySectionViewModel> _sections = Array.Empty<PropertySectionViewModel>();

    public DevicePropertyFormViewModel(ConsoleDetailPresenter presenter, IDevicePropertyOptions? options = null)
    {
        Presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _options = options;
    }

    /// <summary>상세 칸의 여섯 상태 · 바닥 막대 — 손댄 칸 수는 여기의 추적기가 센다.</summary>
    public ConsoleDetailPresenter Presenter { get; }

    public IReadOnlyList<PropertySectionViewModel> Sections
    {
        get => _sections;
        private set { _sections = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(Fields)); }
    }

    public IEnumerable<PropertyFieldViewModel> Fields => _sections.SelectMany(s => s.Fields);

    public IReadOnlyList<object> Rows => _rows;
    public EnumDeviceCategory Category { get; private set; }
    public bool IsCreating { get; private set; }

    /// <summary>
    /// 고른 행들로 폼을 만든다. 손댄 칸은 비운다 — 미적용 변경을 버려도 되는지는 부르는 쪽이 먼저 확인한다(<see cref="NavigationGuard"/>).
    /// </summary>
    public void Load(IReadOnlyList<object> rows, EnumDeviceCategory category, bool isAxisContract, bool isCreating, bool isReadOnly)
        => Load(rows, DevicePropertyCatalog.For(category, isAxisContract), category, isCreating, isReadOnly);

    /// <summary>명세 목록을 직접 받아 폼을 만든다 — 장비가 아닌 것(그룹)도 같은 폼을 쓴다.</summary>
    public void Load(IReadOnlyList<object> rows, IReadOnlyList<DevicePropertySpec> specs, EnumDeviceCategory category, bool isCreating, bool isReadOnly)
    {
        _rows = rows ?? Array.Empty<object>();
        Category = category;
        IsCreating = isCreating;

        Presenter.Tracker.Clear();

        if (_rows.Count == 0)
        {
            Sections = Array.Empty<PropertySectionViewModel>();
            return;
        }

        Presenter.Tracker.MarkIdentity(specs.Where(s => !s.AllowMultiEdit).Select(s => s.Key).ToArray());

        var sections = new List<PropertySectionViewModel>();
        foreach (var section in DevicePropertyCatalog.SectionOrder)
        {
            var fields = new List<PropertyFieldViewModel>();
            foreach (var spec in specs.Where(s => s.Section == section))
            {
                var field = new PropertyFieldViewModel(spec, Presenter.Tracker);
                var options = ResolveOptions(spec, category);
                field.Load(_rows, options, isCreating, isReadOnly, IsNotReceived(spec, isCreating));
                fields.Add(field);
            }

            if (fields.Count == 0) continue;

            var axisFields = fields.Where(f => f.Spec.AxisSection is not null).ToList();
            var allMissing = axisFields.Count == fields.Count && axisFields.All(f => f.IsNotReceived);
            sections.Add(new PropertySectionViewModel(section, fields, allMissing));
        }

        Sections = sections;
    }

    /// <summary>폼을 비운다(선택 없음 · 창 닫힘).</summary>
    public void Clear()
    {
        _rows = Array.Empty<object>();
        IsCreating = false;
        Presenter.Tracker.Clear();
        Sections = Array.Empty<PropertySectionViewModel>();
    }

    /// <summary>모든 칸을 원래 글로 돌려놓는다. 행은 건드린 적이 없으므로 복원할 것이 없다.</summary>
    public void Revert()
    {
        // 추적기를 먼저 비운다 — 칸이 다시 그릴 때 "손댄 칸" 표지를 추적기에서 읽는다.
        Presenter.Tracker.Clear();
        foreach (var field in Fields) field.Revert();
    }

    /// <summary>
    /// 검증하고, 통과하면 <b>손댄 칸만</b> 고른 행 전부에 쓴다. 등록이면 필수 칸은 손대지 않았어도 검증한다.
    /// </summary>
    public PropertyFormCommit Commit()
    {
        if (_rows.Count == 0) return new PropertyFormCommit(false, 0, 0, "고른 장비가 없다");

        var touchedKeys = Presenter.Tracker.ChangesFor(_rows.Count).Select(change => change.Key).ToHashSet(StringComparer.Ordinal);
        var touched = Fields.Where(f => !f.IsLocked && touchedKeys.Contains(f.Key)).ToList();

        // 1) 검증 — 하나라도 걸리면 아무것도 쓰지 않는다.
        var toValidate = IsCreating ? Fields.Where(f => !f.IsLocked && (f.IsRequired || touchedKeys.Contains(f.Key))).ToList() : touched;
        var invalid = toValidate.Where(f => !f.Validate(IsCreating)).ToList();
        if (invalid.Count > 0)
            return new PropertyFormCommit(false, 0, 0, $"{invalid[0].Label}: {invalid[0].Error}" + (invalid.Count > 1 ? $" 외 {invalid.Count - 1}건" : string.Empty));

        if (touched.Count == 0 && !IsCreating) return new PropertyFormCommit(false, 0, 0, "바꾼 칸이 없다");

        // 2) 쓰기 — 검증을 지난 값이 여기서 거절되면 명세와 뷰모델이 어긋난 것이다. 그 칸에 까닭을 남기고 멈춘다.
        foreach (var row in _rows)
        {
            foreach (var field in touched)
            {
                if (!field.WriteTo(row))
                    return new PropertyFormCommit(false, 0, 0, $"{field.Label}: {field.Error}");
            }
        }

        return new PropertyFormCommit(true, _rows.Count, touched.Count, null);
    }

    private IReadOnlyList<PropertyOption> ResolveOptions(DevicePropertySpec spec, EnumDeviceCategory category)
    {
        switch (spec.OptionSource)
        {
            case DevicePropertyOptionSource.None:
                return Array.Empty<PropertyOption>();

            case DevicePropertyOptionSource.ClrEnum when spec.EnumType is { IsEnum: true } enumType:
                return Enum.GetNames(enumType).Select(name => new PropertyOption(name, name)).ToList();

            default:
                return _options?.OptionsFor(spec, category) ?? Array.Empty<PropertyOption>();
        }
    }

    private bool IsNotReceived(DevicePropertySpec spec, bool isCreating)
    {
        if (spec.AxisSection is null || isCreating) return false;

        // 축 묶음 자체가 없으면(6.3 계약) 미수신이 아니라 "그런 축이 없는" 것이다.
        // 고른 행 전부가 그 축을 못 받았을 때만 "미수신" — 일부만 못 받았으면 받은 값이 보이는 편이 낫다.
        return _rows.All(row => ModelOf(row)?.Axes is { } axes && !axes.IsSectionReceived(spec.AxisSection));
    }

    private static IBaseDeviceModel? ModelOf(object row)
        => row.GetType().GetProperty("Model")?.GetValue(row) as IBaseDeviceModel;
}
