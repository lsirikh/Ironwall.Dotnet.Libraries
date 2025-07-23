using Microsoft.Xaml.Behaviors;
using System;
using System.Reflection;
using System.Windows.Controls;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/18/2025 4:29:12 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*------------------------------------------------------------------
 |  ButtonClickBehavior
 |  • 버튼 Click → ViewModel 메서드 호출
 |  • 특징
 |      ‣ 메서드 시그니처 자유 : () / (object) / (object, RoutedEventArgs)
 |      ‣ 선택적 파라미터 전달 (Parameter DP)
 |      ‣ 예외 발생 시 로그 던지지 않고 Debug 출력 후 무시
 ------------------------------------------------------------------*/
public sealed class ButtonClickBehavior : Behavior<Button>
{
    /*────────────── Dependency-Properties ──────────────*/
    public static readonly DependencyProperty MethodNameProperty =
        DependencyProperty.Register(nameof(MethodName), typeof(string),
                                    typeof(ButtonClickBehavior), new PropertyMetadata(null));

    public static readonly DependencyProperty ParameterProperty =
        DependencyProperty.Register(nameof(Parameter), typeof(object),
                                    typeof(ButtonClickBehavior), new PropertyMetadata(null));

    /*────────────── Public API ──────────────*/
    public string? MethodName
    {
        get => (string?)GetValue(MethodNameProperty);
        set => SetValue(MethodNameProperty, value);
    }

    public object? Parameter
    {
        get => GetValue(ParameterProperty);
        set => SetValue(ParameterProperty, value);
    }

    /*────────────── Attach / Detach ──────────────*/
    protected override void OnAttached()
    {
        AssociatedObject.Click += OnClick;
        base.OnAttached();
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Click -= OnClick;
        base.OnDetaching();
    }

    /*────────────── Core Logic ──────────────*/
    private void OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MethodName)) return;
     
        if (!AssociatedObject.IsEnabled) return;   // 2차 보호

        object? vm = AssociatedObject.DataContext;
        if (vm is null) return;

        /* ① 시그니처 0-param */
        MethodInfo? mi = vm.GetType()
                           .GetMethod(MethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                      null, Type.EmptyTypes, null);

        /* ② 시그니처 1-param (object 혹은 임의) */
        mi ??= vm.GetType()
                 .GetMethod(MethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                            null, new[] { typeof(object) }, null);

        /* ③ 시그니처 2-param (object, RoutedEventArgs) */
        mi ??= vm.GetType()
                 .GetMethod(MethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                            null, new[] { typeof(object), typeof(RoutedEventArgs) }, null);

        if (mi is null) return;   // 메서드 미존재 → 무시

        try
        {
            var paramCnt = mi.GetParameters().Length;
            switch (paramCnt)
            {
                case 0:
                    mi.Invoke(vm, null);
                    break;
                case 1:
                    mi.Invoke(vm, new[] { Parameter ?? sender });
                    break;
                default:    // (sender, e)
                    mi.Invoke(vm, new object[] { sender, e });
                    break;
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"ButtonClickBehavior invoke error : {ex.Message}");
#endif
        }
    }
}