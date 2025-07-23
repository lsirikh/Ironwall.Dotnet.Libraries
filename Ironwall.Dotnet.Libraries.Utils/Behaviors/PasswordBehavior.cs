using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;

//public class PasswordBehavior : Behavior<PasswordBox>
//{
//    public static readonly DependencyProperty PasswordProperty =
//        DependencyProperty.Register("Password", typeof(string), typeof(PasswordBehavior), new PropertyMetadata(default(string)));

//    private bool _skipUpdate;

//    public string Password
//    {
//        get { return (string)GetValue(PasswordProperty); }
//        set { SetValue(PasswordProperty, value); }
//    }

//    protected override void OnAttached()
//    {
//        AssociatedObject.PasswordChanged += PasswordBox_PasswordChanged;
//    }

//    protected override void OnDetaching()
//    {
//        AssociatedObject.PasswordChanged -= PasswordBox_PasswordChanged;
//    }

//    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
//    {
//        base.OnPropertyChanged(e);

//        if (e.Property == PasswordProperty)
//        {
//            if (!_skipUpdate && (AssociatedObject != null))
//            {
//                _skipUpdate = true;
//                AssociatedObject.Password = e.NewValue as string;
//                _skipUpdate = false;
//            }
//        }
//    }

//    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
//    {
//        _skipUpdate = true;
//        Password = AssociatedObject.Password;
//        _skipUpdate = false;
//    }
//}

public sealed class PasswordBehavior : Behavior<PasswordBox>
{
    #region Password DP (Two-Way 기본)
    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.Register(
            nameof(Password),
            typeof(string),
            typeof(PasswordBehavior),
            new FrameworkPropertyMetadata(string.Empty,       // ★ null 대신 ""
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPasswordFromVm));                          // VM → View 콜백

    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }
    #endregion

    private bool _syncing;   // 재귀 차단용

    #region Attach/Detach
    protected override void OnAttached()
    {
        AssociatedObject.PasswordChanged += OnPwdChanged;    // View → VM
        // 행 재사용 시 VM 값으로 초기화
        AssociatedObject.Password = Password ?? string.Empty;
    }

    protected override void OnDetaching()
        => AssociatedObject.PasswordChanged -= OnPwdChanged;
    #endregion

    /*------------- View → VM -------------*/
    private void OnPwdChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        try
        {
            _syncing = true;
            Password = AssociatedObject.Password;            // DP ⇒ VM
        }
        finally { _syncing = false; }
    }

    /*------------- VM → View -------------*/
    private static void OnPasswordFromVm(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var bh = (PasswordBehavior)d;
        if (bh._syncing) return;                             // 재귀 차단

        bh.AssociatedObject.Password = e.NewValue as string ?? string.Empty;
    }
}
