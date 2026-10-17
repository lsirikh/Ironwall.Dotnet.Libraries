using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;

public class DeviceAssignItemViewModel : PropertyChangedBase
{
    public int Id { get; set; }
    public string? DeviceName { get; set; }
    public EnumDeviceType DeviceType { get; set; }

    /// <summary>판별자(<c>category_device</c>) 표시 — 소문자 7값. 6.3 응답이면 모델 형에서 유도된 값.</summary>
    public string CategoryLabel { get; set; } = string.Empty;

    /// <summary>종류축(<c>type_&lt;category&gt;</c>) 표시 — 카탈로그 라벨. 값이 없으면(6.3) 옛 <see cref="DeviceType"/> 이름.</summary>
    public string TypeAxisLabel { get; set; } = string.Empty;
    public int DeviceNumber { get; set; }
    public EnumDeviceStatus Status { get; set; }
    public bool IsEnable { get; set; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            _isChecked = value;
            NotifyOfPropertyChange(() => IsChecked);
        }
    }

    public bool IsAlreadyAssigned { get; set; }

    /// <summary>배정 창 목록 항목의 읽히는 이름(UIA · 화면 낭독) — "#번호 이름". 없으면 형식 이름이 읽혔다(WP-2 SC-ASM-024 과 같은 결함).</summary>
    public override string ToString() => $"#{DeviceNumber} {DeviceName}";
}
