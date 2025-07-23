using Microsoft.Xaml.Behaviors;
using System;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/15/2025 1:19:55 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public sealed class NumericInputBehavior : Behavior<TextBox>
{
    private static readonly Regex _regex = new("[^0-9]+");

    protected override void OnAttached()
    {
        AssociatedObject.PreviewTextInput += BlockNonDigit;
        DataObject.AddPastingHandler(AssociatedObject, OnPaste);
        InjectRule();
    }
    protected override void OnDetaching()
    {
        AssociatedObject.PreviewTextInput -= BlockNonDigit;
        DataObject.RemovePastingHandler(AssociatedObject, OnPaste);
    }

    void BlockNonDigit(object s, TextCompositionEventArgs e) => e.Handled = _regex.IsMatch(e.Text);

    void OnPaste(object s, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.Text) &&
            _regex.IsMatch((string)e.DataObject.GetData(DataFormats.Text)!))
            e.CancelCommand();
    }

    void InjectRule()
    {
        var origin = BindingOperations.GetBinding(AssociatedObject, TextBox.TextProperty);
        if (origin is null || HasNumericRule(origin)) return;

        var clone = origin.Clone();                      // BindingExtension 메서드 (아래 참고)
        clone.ValidationRules.Add(new NumericValidationRule());
        BindingOperations.SetBinding(AssociatedObject, TextBox.TextProperty, clone);
    }
    static bool HasNumericRule(Binding b) =>
        b.ValidationRules.Any(r => r is NumericValidationRule);
}
