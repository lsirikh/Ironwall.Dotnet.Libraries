using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;

/****************************************************************************
   Purpose      : 부대 콘솔의 행 — 트리 노드 · 미배치 장비 · 인접 칩 (N-11 FR-02 · FR-11)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 편제 트리의 행 하나. <b>TreeView 가 아니라 평면 목록</b>이라 깊이를 여기서 여백으로 바꾼다.
/// </summary>
/// <remarks>
/// <para>들여쓰기는 단계당 <b>16 DIU</b>, 행 높이는 <b>30</b>(와이어프레임 L102 · L117).</para>
/// <para>왜 평면 목록인가 — 커널의 <c>CaptureDragBehavior</c> 는 <c>Behavior&lt;ItemsControl&gt;</c> 이고
/// 끌린 항목을 <b>뿌리 목록의 컨테이너 생성기</b>로 되찾는다(<c>ItemFromContainer</c>).
/// <c>TreeView</c> 의 중첩 <c>TreeViewItem</c> 은 그 생성기에 없어 <b>루트 노드만 끌리고</b>,
/// 게다가 <c>TreeView</c> 는 <c>Selector</c> 가 아니라 데드존 미만의 클릭 폴백(<c>SelectOnly</c>)도 죽는다.
/// 목업이 이미 평면 + <c>margin-left</c> 로 그려져 있어(스토리보드 화면 G) 모양도 같다.</para>
/// </remarks>
public sealed class UnitNodeRowViewModel : PropertyChangedBase
{
    public const double INDENT_PER_DEPTH = 16.0;

    private bool _isExpanded = true;
    private int _deviceCount;
    private bool _isMoving;

    public UnitNodeRowViewModel(UnitTreeNode node, bool isMine)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        IsMine = isMine;
    }

    public UnitTreeNode Node { get; }

    public int Id => Node.Id;
    public string Code => Node.Code;
    public string Name => Node.Name;
    public int Depth => Node.Depth;
    public bool IsEnable => Node.IsEnable;
    public bool HasChildren => Node.HasChildren;
    public bool IsOrphan => Node.IsOrphan;
    public EnumUnitEchelon? Echelon => Node.Echelon;
    public string EchelonText => UnitDropRules.EchelonTextOf(Node);
    public int AdjacentCount => Node.AdjacentIds.Count;

    /// <summary>이 앱이 붙어 있는 부대(<c>GroupNats</c> 코드)와 같다 — 트리에서 강조한다(스토리보드 L407).</summary>
    public bool IsMine { get; }

    /// <summary>들여쓰기 — 단계당 16. 컨테이너에 로컬 값을 쓰지 않고 <b>내용</b>의 여백으로만 준다.</summary>
    public Thickness Indent => new(Depth * INDENT_PER_DEPTH, 0, 0, 0);

    /// <summary>이 부대에 직접 매인 장비 수.</summary>
    public int DeviceCount { get => _deviceCount; set { _deviceCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasDeviceCount)); } }
    public bool HasDeviceCount => _deviceCount > 0;

    /// <summary>지금 끌려 다니는 노드 — 목업의 "이동 중" 표시(스토리보드 화면 G L320).</summary>
    public bool IsMoving { get => _isMoving; set { _isMoving = value; NotifyOfPropertyChange(); } }

    /// <summary>자식을 접었는가. 접힘은 <b>화면 표시만</b> 바꾼다(서버 호출 없음).</summary>
    public bool IsExpanded { get => _isExpanded; set { _isExpanded = value; NotifyOfPropertyChange(); } }

    public string Tooltip => $"{EchelonText} · {Name} · {Code}" + (IsEnable ? string.Empty : " · 운용 중지");

    public override string ToString() => Name;
}

/// <summary>미배치 장비 목록의 행.</summary>
public sealed class UnitDeviceRowViewModel : PropertyChangedBase
{
    private string? _pendingUnitName;

    public UnitDeviceRowViewModel(UnitDeviceItem item, string unitText)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        UnitText = unitText;
    }

    public UnitDeviceItem Item { get; }

    public int Id => Item.Id;
    public string Name => Item.Name;
    public string NumberText => $"#{Item.NumberDevice}";
    public string CategoryText => Item.CategoryText;

    /// <summary>지금 소속 — 없으면 "소속 없음".</summary>
    public string UnitText { get; }

    /// <summary>아직 보내지 않은 배치(Draft). 채워져 있으면 행이 앰버 파선으로 뜬다.</summary>
    public string? PendingUnitName
    {
        get => _pendingUnitName;
        set { _pendingUnitName = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsPending)); NotifyOfPropertyChange(nameof(PendingText)); }
    }

    public bool IsPending => !string.IsNullOrEmpty(_pendingUnitName);
    public string PendingText => IsPending ? $"→ {_pendingUnitName}" : string.Empty;

    public override string ToString() => Name;
}

/// <summary>상세 칸의 인접 칩 하나.</summary>
public sealed class UnitAdjacencyChipViewModel
{
    public UnitAdjacencyChipViewModel(int id, string name, string echelonText)
    {
        Id = id;
        Name = name;
        EchelonText = echelonText;
    }

    public int Id { get; }
    public string Name { get; }
    public string EchelonText { get; }
    public override string ToString() => Name;
}

/// <summary>상위 부대 피커 · 인접 후보 콤보가 쓰는 항목(드래그의 키보드 폴백).</summary>
public sealed class UnitOptionViewModel
{
    public UnitOptionViewModel(int? id, string label)
    {
        Id = id;
        Label = label;
    }

    /// <summary><c>null</c> 은 "최상위(루트)".</summary>
    public int? Id { get; }
    public string Label { get; }
    public override string ToString() => Label;
}

/// <summary>툴바의 제대 칩 — 전체 · 사단 · 연대 · 대대 · 중대 · 소초(스토리보드 화면 G L301).</summary>
public sealed class UnitEchelonFilterViewModel : PropertyChangedBase
{
    private bool _isSelected;

    public UnitEchelonFilterViewModel(EnumUnitEchelon? echelon, string label)
    {
        Echelon = echelon;
        Label = label;
    }

    /// <summary><c>null</c> = 전체. 서버에 보낼 때 <b>빈 문자열이 아니라 파라미터 자체를 뺀다</b>(와이어프레임 L315).</summary>
    public EnumUnitEchelon? Echelon { get; }
    public string Label { get; }
    public string Key => Echelon?.ToString() ?? "All";

    public bool IsSelected { get => _isSelected; set { _isSelected = value; NotifyOfPropertyChange(); } }
}
