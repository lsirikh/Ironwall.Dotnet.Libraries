using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;

/****************************************************************************
   Purpose      : 관계도 Enter → 상세 첫 칸 포커스 다리 (unit-relationship-map FR-36)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 관계도 뷰모델의 <see cref="UnitMapFocusTarget.DetailFirstField"/> 요청(<c>Enter</c> — FR-36 "상세 첫 칸으로 포커스")을
/// 부대 콘솔 뷰의 상세 칸으로 잇는다.
/// </summary>
/// <remarks>
/// <para>뷰모델은 상세 칸을 모른다 — 요청만 올린다. 그 요청을 받는 곳이 캔버스뿐이었고 캔버스는 <c>Canvas</c> 요청만 처리해
/// <c>Enter</c> 가 아무 일도 하지 않았다(실창 8회차 SIM-K031: 포커스가 노드에 그대로). 콘솔 뷰가 이 다리를 쥔다.</para>
/// <para>첫 칸 = 보이고 · 켜져 있고 · 포커스를 받을 수 있는 첫 입력 칸 — 등록 중이면 코드 칸, 아니면 이름 칸(코드는 등록 뒤 잠긴다).
/// 폼이 접혀 있으면(선택 없음) 아무 칸도 고르지 않는다. UI 스레드 전용.</para>
/// </remarks>
public sealed class UnitDetailFocusBridge
{
    /// <summary>상세 폼의 입력 칸 — 화면 순서대로.</summary>
    public static readonly string[] FirstFieldIds = { "Units.Detail.CodeField", "Units.Detail.NameBox" };

    private readonly Action _focusDetail;
    private UnitMapViewModel? _map;

    /// <param name="focusDetail">상세 첫 칸으로 포커스를 옮기는 일(뷰가 준다 — 보통 <see cref="FocusFirstField"/> 를 다음 입력 차례에).</param>
    public UnitDetailFocusBridge(Action focusDetail) => _focusDetail = focusDetail ?? throw new ArgumentNullException(nameof(focusDetail));

    /// <summary>듣는 관계도를 바꾼다(콘솔 DataContext 교체) — 옛 관계도의 구독은 푼다. <c>null</c> 이면 듣지 않는다.</summary>
    public void Bind(UnitMapViewModel? map)
    {
        if (ReferenceEquals(_map, map)) return;
        if (_map is not null) _map.FocusRequested -= OnFocusRequested;
        _map = map;
        if (_map is not null) _map.FocusRequested += OnFocusRequested;
    }

    private void OnFocusRequested(object? sender, UnitMapFocusTarget target)
    {
        if (target == UnitMapFocusTarget.DetailFirstField) _focusDetail();
    }

    /// <summary>상세 첫 칸 — 보이고(접힌 조상 없음) · 켜져 있고 · 포커스를 받을 수 있는 첫 칸. 없으면 <c>null</c>.</summary>
    public static Control? FindFirstField(DependencyObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        foreach (var id in FirstFieldIds)
        {
            if (Find(root, id) is Control field && field.IsEnabled && field.Focusable) return field;
        }
        return null;
    }

    /// <summary>상세 첫 칸으로 키보드 포커스(안 되면 논리 포커스) — 옮겼으면 <c>true</c>.</summary>
    public static bool FocusFirstField(DependencyObject root)
    {
        if (FindFirstField(root) is not { } field) return false;
        field.Focus();
        if (!field.IsKeyboardFocused) FocusManager.SetFocusedElement(FocusManager.GetFocusScope(field), field);
        if (field is TextBox box) box.SelectAll();
        return true;
    }

    /// <summary>시각 나무를 앞에서부터 — 접힌(Visibility ≠ Visible) 가지는 통째로 건너뛴다.</summary>
    private static DependencyObject? Find(DependencyObject node, string automationId)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is UIElement element && element.Visibility != Visibility.Visible) continue;
            if (AutomationProperties.GetAutomationId(child) == automationId) return child;
            if (Find(child, automationId) is { } found) return found;
        }
        return null;
    }
}
