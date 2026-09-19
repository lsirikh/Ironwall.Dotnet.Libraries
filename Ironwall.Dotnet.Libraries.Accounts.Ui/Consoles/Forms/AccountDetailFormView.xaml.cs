using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Forms;

/// <summary>명세에서 만들어지는 사용자 상세 폼. 판단은 전부 뷰모델에 있다.</summary>
public partial class AccountDetailFormView : UserControl
{
    public AccountDetailFormView() => InitializeComponent();
}

/// <summary>칸의 편집기 종류로 템플릿을 고른다 — 잠긴 칸은 무엇이든 읽기 전용으로 그린다.</summary>
public sealed class AccountEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Text { get; set; }
    public DataTemplate? Choice { get; set; }
    public DataTemplate? ReadOnly { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item is not AccountFieldViewModel field
            ? base.SelectTemplate(item, container)
            : field.EffectiveEditor switch
            {
                AccountFieldEditor.Choice => Choice,
                AccountFieldEditor.ReadOnly => ReadOnly,
                _ => Text,
            };
}
