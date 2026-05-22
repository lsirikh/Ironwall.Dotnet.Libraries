using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/// <summary>
/// TextBox 인라인 이름 편집 Attached Behavior.
/// IsRenaming=true 시 자동 SelectAll + Focus, Enter/Escape 키 처리.
/// _isCommitting 플래그로 Escape→LostFocus 이중 실행 방지.
/// </summary>
public static class TextBoxRenameHelper
{
    private static bool _isCommitting;

    #region IsRenaming

    public static readonly DependencyProperty IsRenamingProperty =
        DependencyProperty.RegisterAttached("IsRenaming", typeof(bool), typeof(TextBoxRenameHelper),
            new PropertyMetadata(false, OnIsRenamingChanged));

    public static bool GetIsRenaming(DependencyObject obj) => (bool)obj.GetValue(IsRenamingProperty);
    public static void SetIsRenaming(DependencyObject obj, bool value) => obj.SetValue(IsRenamingProperty, value);

    #endregion

    #region CommitCommand

    public static readonly DependencyProperty CommitCommandProperty =
        DependencyProperty.RegisterAttached("CommitCommand", typeof(ICommand), typeof(TextBoxRenameHelper));

    public static ICommand GetCommitCommand(DependencyObject obj) => (ICommand)obj.GetValue(CommitCommandProperty);
    public static void SetCommitCommand(DependencyObject obj, ICommand value) => obj.SetValue(CommitCommandProperty, value);

    #endregion

    #region CancelCommand

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.RegisterAttached("CancelCommand", typeof(ICommand), typeof(TextBoxRenameHelper));

    public static ICommand GetCancelCommand(DependencyObject obj) => (ICommand)obj.GetValue(CancelCommandProperty);
    public static void SetCancelCommand(DependencyObject obj, ICommand value) => obj.SetValue(CancelCommandProperty, value);

    #endregion

    private static void OnIsRenamingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox) return;

        if ((bool)e.NewValue)
        {
            _isCommitting = false;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            textBox.LostFocus += OnLostFocus;

            textBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.SelectAll();
                textBox.Focus();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
        else
        {
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            textBox.LostFocus -= OnLostFocus;
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        switch (e.Key)
        {
            case Key.Enter:
                _isCommitting = true;
                var commitCmd = GetCommitCommand(textBox);
                if (commitCmd?.CanExecute(null) == true)
                    commitCmd.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                _isCommitting = true;
                var cancelCmd = GetCancelCommand(textBox);
                if (cancelCmd?.CanExecute(null) == true)
                    cancelCmd.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private static void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (_isCommitting) return;
        if (sender is not TextBox textBox) return;

        var isRenaming = GetIsRenaming(textBox);
        if (!isRenaming) return;

        _isCommitting = true;
        var commitCmd = GetCommitCommand(textBox);
        if (commitCmd?.CanExecute(null) == true)
            commitCmd.Execute(null);
    }
}
