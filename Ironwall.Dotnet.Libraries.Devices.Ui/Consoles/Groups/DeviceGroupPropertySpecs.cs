using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Groups;

/// <summary>
/// 장비 그룹의 속성 명세 — 그룹은 장비가 아니지만 <b>같은 폼</b>으로 고친다(칸이 둘뿐이라 따로 폼을 짜지 않는다).
/// </summary>
public static class DeviceGroupPropertySpecs
{
    private static readonly IReadOnlyCollection<EnumDeviceCategory> NoCategory = Array.Empty<EnumDeviceCategory>();

    public static IReadOnlyList<DevicePropertySpec> All { get; } = new[]
    {
        new DevicePropertySpec
        {
            Key = "group.name", Label = "그룹 이름", ApiPath = "name",
            Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Text,
            Categories = NoCategory, ViewModelPath = "Name",
            IsRequiredOnCreate = true, AllowMultiEdit = false, MaxLength = 100,
        },
        new DevicePropertySpec
        {
            Key = "group.description", Label = "설명", ApiPath = "description",
            Section = DevicePropertySection.Common, Editor = DevicePropertyEditor.Text,
            Categories = NoCategory, ViewModelPath = "Description", MaxLength = 500,
        },
        new DevicePropertySpec
        {
            Key = "group.device_count", Label = "장비 수", ApiPath = "device_count",
            Section = DevicePropertySection.Groups, Editor = DevicePropertyEditor.ReadOnly,
            Writable = DevicePropertyWritable.No,
            LockReason = "장비를 이 그룹의 칩에 끌어 놓으면 늘어난다",
            Categories = NoCategory, ViewModelPath = "DeviceCount",
        },
    };
}
