using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;

public partial class DevicePropertyFormView : UserControl
{
    /// <summary>같은 검증 패스에서 여러 칸이 걸리면 첫 칸만 데려간다(마지막이 이겨 맨 아래로 튀지 않게).</summary>
    private FrameworkElement? _pendingError;

    public DevicePropertyFormView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 까닭 한 줄이 떴는데 화면 밖이면 아무 말도 하지 않은 것과 같다 — 스크롤 영역 아래로 잘려
    /// 마지막 글자만 보이던 자리다(D-11 실측). 떠오르는 즉시 그 칸을 보이는 곳으로 끌어올린다.
    /// </summary>
    private void OnErrorVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true || sender is not FrameworkElement element) return;
        if (_pendingError is not null) return;

        _pendingError = element;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var target = _pendingError;
            _pendingError = null;
            target?.BringIntoView();
        }));
    }
}
