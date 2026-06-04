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
        => AssociatedObject.SelectionChanged += OnSelectionChanged;

    protected override void OnDetaching()
    {
        AssociatedObject.SelectionChanged -= OnSelectionChanged;
        _selectedItemsSet = null;
    }

    protected virtual void OnSelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        // 델타 기반 처리 — 전체 SelectedItems 재생성 대신 추가/제거된 항목만 반영
        // AS-IS: OfType<T>().ToList() 매번 전체 재생성 → 1000개 선택 시 1000번 반복
        // TO-BE: AddedItems/RemovedItems 델타만 처리 → O(1) per change
        _selectedItemsSet ??= new HashSet<TItemVm>(
            AssociatedObject.SelectedItems.OfType<TItemVm>());

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
