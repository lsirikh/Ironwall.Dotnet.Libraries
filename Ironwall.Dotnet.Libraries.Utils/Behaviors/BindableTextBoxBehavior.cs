using Microsoft.Xaml.Behaviors;
using System;
using System.Windows.Controls;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/15/2025 1:05:46 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public sealed class BindableTextBoxBehavior : Behavior<TextBox>
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string),
            typeof(BindableTextBoxBehavior),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnAttached()
    {
        AssociatedObject.TextChanged += OnTextChanged;
        SyncToControl();
    }
    protected override void OnDetaching()
    {
        AssociatedObject.TextChanged -= OnTextChanged;
    }

    private void OnTextChanged(object? s, TextChangedEventArgs e) =>
        Text = AssociatedObject.Text;

    private void SyncToControl() => AssociatedObject.Text = Text;
}