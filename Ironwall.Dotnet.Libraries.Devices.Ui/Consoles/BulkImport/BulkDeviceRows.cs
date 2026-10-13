using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.BulkImport;
/****************************************************************************
   Purpose      : 장비 대량 등록 — 모델 ↔ 행 뷰모델 · 칸 값 읽기 (device-bulk-import FR-02 · D-3 · D-4)
   Created By   : GHLee
   Created On   : 10/13/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 카탈로그 명세는 <b>행 뷰모델</b>의 속성 이름을 쥔다 — 상세 폼과 같은 접근기(<see cref="DevicePropertyAccessor"/>)로 읽고 쓰려고
/// 모델을 행 뷰모델로 감싼다. 서버 캐시의 모델은 <b>직접 고치지 않는다</b>(<see cref="Clone"/> 사본에 쓴다 — 실패해도 캐시가 더럽혀지지 않는다).
/// </summary>
public static class BulkDeviceRows
{
    public static EnumDeviceCategory CategoryOf(IBaseDeviceModel model) => DeviceAxesMapper.CategoryOf(model);

    /// <summary>새 장비 모델(패널의 [추가]와 같은 빈 모델).</summary>
    public static IBaseDeviceModel NewModel(EnumDeviceCategory category) => category switch
    {
        EnumDeviceCategory.Controller => new ControllerDeviceModel(),
        EnumDeviceCategory.Sensor => new SensorDeviceModel(),
        EnumDeviceCategory.Camera => new CameraDeviceModel(),
        EnumDeviceCategory.Speaker => new SpeakerDeviceModel(),
        EnumDeviceCategory.Enclosure => new EnclosureDeviceModel(),
        EnumDeviceCategory.Lamp => new LampDeviceModel(),
        EnumDeviceCategory.Gate => new GateDeviceModel(),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "대량 등록을 받지 않는 카테고리입니다."),
    };

    /// <summary>모델 → 행 뷰모델(패널 목록의 행과 같은 형식).</summary>
    public static object Wrap(IBaseDeviceModel model) => model switch
    {
        IControllerDeviceModel m => new ControllerDeviceViewModel(m),
        ISensorDeviceModel m => new SensorDeviceViewModel(m),
        ICameraDeviceModel m => new CameraDeviceViewModel(m),
        ISpeakerDeviceModel m => new SpeakerDeviceViewModel(m),
        IEnclosureDeviceModel m => new EnclosureDeviceViewModel(m),
        ILampDeviceModel m => new LampDeviceViewModel(m),
        IGateDeviceModel m => new GateDeviceViewModel(m),
        _ => throw new ArgumentException($"대량 등록이 다루지 않는 장비 형식입니다: {model?.GetType().Name}", nameof(model)),
    };

    /// <summary>
    /// 기존 장비의 사본 — 수정은 사본에 쓰고 사본을 보낸다. 복사 생성자가 있는 형식은 그것을, 없는 형식(센서 · 카메라)은
    /// 패널 저장과 같은 DTO 변환을 한 바퀴 돌린 뒤 축 · 부대 · 종류축 · 소속 제어기를 원본에서 되싣는다.
    /// </summary>
    public static IBaseDeviceModel Clone(IBaseDeviceModel source)
    {
        IBaseDeviceModel copy = source switch
        {
            ControllerDeviceModel m => new ControllerDeviceModel(m),
            SpeakerDeviceModel m => new SpeakerDeviceModel(m),
            EnclosureDeviceModel m => new EnclosureDeviceModel(m),
            LampDeviceModel m => new LampDeviceModel(m),
            GateDeviceModel m => new GateDeviceModel(m),
            SensorDeviceModel m => m.ToSensorDeviceDto().ToSensorDeviceModel(),
            CameraDeviceModel m => m.ToCameraDeviceDto().ToCameraDeviceModel(),
            _ => throw new ArgumentException($"대량 등록이 다루지 않는 장비 형식입니다: {source?.GetType().Name}", nameof(source)),
        };

        copy.Id = source.Id;
        copy.Axes = source.Axes;
        copy.UnitId = source.UnitId;
        copy.TypeAxisCode = source.TypeAxisCode;
        copy.CategoryDevice = source.CategoryDevice;
        if (copy is SensorDeviceModel sensor && source is ISensorDeviceModel original) sensor.Controller = original.Controller;
        // 소속은 고치지 않는다 — 기준선을 찍어 두면 저장 본문이 group_ids 를 싣지 않는다(통째 교체 방지).
        GroupMembershipBaseline.Capture(copy);
        return copy;
    }

    /// <summary>
    /// 열 하나의 지금 값(내보내기 · 바뀐 칸 비교). 비밀번호는 <paramref name="includeSecret"/> 일 때만(비교용 — 파일 · 화면에는 쓰지 않는다).
    /// </summary>
    public static string ReadText(IBaseDeviceModel model, object row, BulkColumn column, bool includeSecret = false)
    {
        switch (column.Kind)
        {
            case BulkColumnKind.Id:
                return model.Id > 0 ? model.Id.ToString(CultureInfo.InvariantCulture) : string.Empty;
            case BulkColumnKind.Unit:
                return model.UnitId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            case BulkColumnKind.Controller:
                return model is ISensorDeviceModel { Controller: { Id: > 0 } controller } ? controller.DeviceName ?? string.Empty : string.Empty;
            default:
                if (column.Spec is null) return string.Empty;
                if (column.IsSecret && !includeSecret) return string.Empty;
                return DevicePropertyAccessor.ReadText(row, column.Spec);
        }
    }

    /// <summary>
    /// 선택 칸의 고를 수 있는 값 — 종류축 · 부가 축은 화면이 쓰는 공급자(<paramref name="provider"/>)에서, 열거형 · 고정 표는 여기서 만든다.
    /// 빈 목록 = 모른다(카탈로그 미수신 · 6.3) — 판정기는 그때 막지 않는다(서버가 최종 판정, TypeAxisPanelSupport 와 같은 규칙).
    /// </summary>
    public static IReadOnlyList<PropertyOption> OptionsFor(BulkColumn column, EnumDeviceCategory category, IDevicePropertyOptions? provider)
    {
        var spec = column.Spec;
        if (spec is null) return Array.Empty<PropertyOption>();
        if (spec.Editor == DevicePropertyEditor.Boolean)
            return new[] { new PropertyOption("예", "true"), new PropertyOption("아니오", "false") };

        switch (spec.OptionSource)
        {
            case DevicePropertyOptionSource.ClrEnum when spec.EnumType is { IsEnum: true } enumType:
                return Enum.GetValues(enumType).Cast<Enum>()
                    .Select(v => new PropertyOption(DeviceEnumDisplay.KoreanOf(v), v.ToString()))
                    .ToList();
            case DevicePropertyOptionSource.Fixed when spec.FixedOptions is { } table:
                return table.Where(o => o.Code.Length > 0).Select(o => new PropertyOption(o.Display, o.Code)).ToList();
            case DevicePropertyOptionSource.TypeAxis:
            case DevicePropertyOptionSource.ExtraAxis:
            case DevicePropertyOptionSource.CatalogVocabulary:
                return provider?.OptionsFor(spec, category).Where(o => o.Text is not null).ToList() ?? (IReadOnlyList<PropertyOption>)Array.Empty<PropertyOption>();
            default:
                return Array.Empty<PropertyOption>();
        }
    }
}
