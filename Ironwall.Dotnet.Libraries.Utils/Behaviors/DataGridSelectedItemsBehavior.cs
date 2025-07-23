using System;
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
        => AssociatedObject.SelectionChanged -= OnSelectionChanged;

    private void OnSelectionChanged(object? s, SelectionChangedEventArgs e)
        => SelectedItems = AssociatedObject.SelectedItems
                           .OfType<TItemVm>()
                           .ToList();
}
