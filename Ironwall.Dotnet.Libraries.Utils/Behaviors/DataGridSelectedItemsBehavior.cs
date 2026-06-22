using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :
   Created By   : GHLee
   Created On   : 6/10/2025 4:51:23 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DataGridSelectedItemsBehavior<TItemVm> : Behavior<DataGrid>
{
    public IList<TItemVm>? SelectedItems
    {
        get => (IList<TItemVm>?)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.Register(
            nameof(SelectedItems),
            typeof(IList<TItemVm>),
            typeof(DataGridSelectedItemsBehavior<TItemVm>));

    protected override void OnAttached()
    {
        // (fix) 빈 set으로 시작. SelectionChanged는 DataGrid가 선택을 '갱신한 뒤' 발생하므로,
        //   set을 AssociatedObject.SelectedItems로 초기화하면 첫 클릭의 AddedItems가 이미 set에 들어있어
        //   흡수(changed=false)되어 첫 선택이 전파되지 않던 버그를 유발했음.
        _selectedItemsSet = new HashSet<TItemVm>();
        AssociatedObject.SelectionChanged += OnSelectionChanged;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.SelectionChanged -= OnSelectionChanged;
        _selectedItemsSet = null;
    }

    protected virtual void OnSelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        // 델타 기반 처리 — 추가/제거된 항목만 반영(O(1) per change). set은 OnAttached에서 빈 상태로 시작.
        // (주의) AssociatedObject.SelectedItems 로 lazy-init 금지 — SelectionChanged 시점의 post-change
        //   선택을 캡처해 첫 AddedItems를 흡수(첫 선택 누락)하므로 빈 set 폴백만 둔다.
        _selectedItemsSet ??= new HashSet<TItemVm>();

        bool changed = false;
        foreach (var item in e.AddedItems.OfType<TItemVm>())
            changed |= _selectedItemsSet.Add(item);
        foreach (var item in e.RemovedItems.OfType<TItemVm>())
            changed |= _selectedItemsSet.Remove(item);

        if (changed)
            SelectedItems = new List<TItemVm>(_selectedItemsSet);
    }

    private HashSet<TItemVm>? _selectedItemsSet;
}
