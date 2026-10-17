namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;

/****************************************************************************
   Purpose      : 설정 블록의 세그먼트 · 칩 한 칸 (T-03)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 세그먼트 · 칩 하나. 뷰의 <c>RadioButton.IsChecked</c> 가 <see cref="IsSelected"/> 에 묶인다 —
/// 켜지면 <paramref name="onPick"/> 으로 블록에 알린다(끄기는 무시 — 다른 칸이 켜지며 꺼진다).
/// </summary>
public class CameraPopupChoice : CameraPopupObservable
{
    private readonly Action<CameraPopupChoice> _onPick;
    private bool _isSelected;

    public CameraPopupChoice(object value, string label, string automationId, Action<CameraPopupChoice> onPick,
                             bool isEnabled = true, string toolTip = "")
    {
        Value = value;
        Label = label;
        AutomationId = automationId;
        IsEnabled = isEnabled;
        ToolTip = string.IsNullOrEmpty(toolTip) ? label : toolTip;
        _onPick = onPick ?? throw new ArgumentNullException(nameof(onPick));
    }

    public object Value { get; }
    public string Label { get; }
    public string AutomationId { get; }
    public bool IsEnabled { get; }
    public string ToolTip { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!value)
            {
                // 라디오 묶음이 다른 칸을 켜며 이 칸을 끈다 — 블록 값은 켜진 칸이 정한다. 표시만 따른다.
                Set(ref _isSelected, false);
                return;
            }
            if (!IsEnabled) { Raise(nameof(IsSelected)); return; }
            if (Set(ref _isSelected, true)) _onPick(this);
        }
    }

    /// <summary>블록이 값에서 표시를 맞출 때 — 알림(onPick)을 부르지 않는다.</summary>
    internal void Sync(bool selected) => Set(ref _isSelected, selected, nameof(IsSelected));
}

/// <summary>격자 칩 — 미니 격자(가로 × 세로)와 빈 칸 표시.</summary>
public sealed class CameraPopupLayoutChoice : CameraPopupChoice
{
    public CameraPopupLayoutChoice(object value, string label, string automationId, Action<CameraPopupChoice> onPick,
                                   int columns, int rows, int filled)
        : base(value, label, automationId, onPick, toolTip: $"가로 {columns} × 세로 {rows}")
    {
        Columns = columns;
        Rows = rows;
        // 빈 칸은 속이 빈 칸으로 그린다(색이 아니라 모양) — 5대 · 3×2 의 마지막 칸.
        Cells = Enumerable.Range(0, columns * rows).Select(i => i < filled).ToList();
    }

    public int Columns { get; }
    public int Rows { get; }

    /// <summary>칸마다 카메라가 들어가는가.</summary>
    public IReadOnlyList<bool> Cells { get; }
}
