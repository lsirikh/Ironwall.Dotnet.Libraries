using Microsoft.Xaml.Behaviors;
using System;
using System.Windows.Controls;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/15/2025 1:06:32 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public sealed class PasswordSyncBehavior : Behavior<PasswordBox>
{
    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.Register(nameof(Target), typeof(string),
            typeof(PasswordSyncBehavior), new PropertyMetadata(null));

    public string? Target
    {
        get => (string?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    protected override void OnAttached()
        => AssociatedObject.PasswordChanged += OnPwdChanged;

    protected override void OnDetaching()
        => AssociatedObject.PasswordChanged -= OnPwdChanged;

    void OnPwdChanged(object? _, RoutedEventArgs __)
        => Target = AssociatedObject.Password;
}
