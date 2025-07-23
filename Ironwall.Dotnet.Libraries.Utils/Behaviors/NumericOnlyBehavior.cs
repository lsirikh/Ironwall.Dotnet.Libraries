using Microsoft.Xaml.Behaviors;
using System;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/15/2025 1:05:00 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public sealed class NumericOnlyBehavior : Behavior<TextBox>
{
    private static readonly Regex _regex = new("[^0-9]+");

    protected override void OnAttached()
    {
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
        DataObject.AddPastingHandler(AssociatedObject, OnPaste);
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown; 
    }
    protected override void OnDetaching()
    {
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
        DataObject.RemovePastingHandler(AssociatedObject, OnPaste);
        AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown; 
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = _regex.IsMatch(e.Text);
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.Text) &&
            _regex.IsMatch((string)e.DataObject.GetData(DataFormats.Text)!))
            e.CancelCommand();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 숫자 키, 백스페이스, 삭제, 화살표, 탭만 허용
        bool isNumber =
            (e.Key >= Key.D0 && e.Key <= Key.D9) ||
            (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9);
        bool isControl =
            e.Key == Key.Back ||
            e.Key == Key.Delete ||
            e.Key == Key.Left ||
            e.Key == Key.Right ||
            e.Key == Key.Tab;

        if (!isNumber && !isControl)
            e.Handled = true;
    }
}
