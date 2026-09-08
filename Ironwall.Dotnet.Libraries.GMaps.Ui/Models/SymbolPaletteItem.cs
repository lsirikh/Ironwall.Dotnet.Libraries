using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

public sealed record SymbolPaletteItem(string Title, EnumMarkerCategory Category, object Type, string? ModelKey = null, string? ModelVariant = null)
{
    public string Detail => ModelVariant is not null ? ModelVariant.ToUpperInvariant() : ModelKey is not null ? "3D" : "2D";
    public static IReadOnlyList<SymbolPaletteItem> All { get; } = Build();
    private static IReadOnlyList<SymbolPaletteItem> Build()
    {
        var pids = EnumMarkerCategory.PIDS_EQUIPMENT; var infra = EnumMarkerCategory.INFRASTRUCTURE;
        var items = new List<SymbolPaletteItem> {
            new("고정형 카메라",pids,EnumDeviceType.IpCamera,"camera","Fixed"),
            new("돔형 카메라",pids,EnumDeviceType.IpCamera,"camera.dome","Dome"),
            new("PTZ 카메라",pids,EnumDeviceType.IpCamera,"camera.ptz","Ptz") };
        var names = new Dictionary<EnumDeviceType, string>
        {
            [EnumDeviceType.Controller] = "제어기",
            [EnumDeviceType.Multi] = "다중 센서",
            [EnumDeviceType.Fence] = "펜스 센서",
            [EnumDeviceType.Underground] = "지중 센서",
            [EnumDeviceType.Contact] = "접점 센서",
            [EnumDeviceType.PIR] = "PIR 센서",
            [EnumDeviceType.IoController] = "I/O 제어기",
            [EnumDeviceType.Laser] = "레이저 센서",
            [EnumDeviceType.SmartSensor] = "스마트 센서",
            [EnumDeviceType.SmartSensor2] = "스마트 센서 2",
            [EnumDeviceType.SmartCompound] = "복합 센서",
            [EnumDeviceType.IpSpeaker] = "IP 스피커",
            [EnumDeviceType.Radar] = "레이더",
            [EnumDeviceType.Lamp] = "조명",
            [EnumDeviceType.Enclosure] = "함체",
            [EnumDeviceType.SmartMultisensor2] = "스마트 다중 센서",
            [EnumDeviceType.Gate] = "통문"   // D2(2026-09-07) — 3D 키 fencegate, 개폐 형태 DoorState
        };
        foreach (var (type, title) in names) items.Add(new(title, pids, type, Symbols3D.HousingModels.DeviceKey(type)));
        // 표시명은 enum 의 [Display(Name)] 에서 읽는다. 병렬 배열은 enum 에 값이 끼어들면 라벨이 어긋나거나
        // IndexOutOfRange 로 정적 초기화가 죽어 팔레트 전체가 사라진다(R-4).
        foreach (var type in Enum.GetValues<EnumBuildingType>())
            items.Add(new(DisplayName(type), infra, type.ToString(), "infra." + type.ToString().ToLowerInvariant()));
        foreach (var type in Enum.GetValues<EnumShapeType>()) items.Add(new(Helpers.SymbolTypeHelper.ShapeTypeDisplayNames.GetValueOrDefault(type, type.ToString()), EnumMarkerCategory.GEOMETRICS, type));
        items.Add(new("위치 핀", EnumMarkerCategory.BASIC_SHAPES, "Pin"));
        return items.AsReadOnly();
    }

    private static string DisplayName(Enum value)
        => value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.Name ?? value.ToString();
}
