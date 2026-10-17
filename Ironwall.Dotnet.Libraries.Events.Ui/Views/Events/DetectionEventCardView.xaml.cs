using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Events
{
    /// <summary>
    /// Interaction logic for DetectionEventCardView.xaml
    /// </summary>
    public partial class DetectionEventCardView : UserControl
    {
        public DetectionEventCardView()
        {
            InitializeComponent();
            // U-14 — 머리의 뒤집기 버튼은 routed command(Flipper.FlipCommand)라, 버튼이 아직 Flipper 밑에 붙기 전
            // (XAML 구성 중) 에 CanExecute 를 한 번 묻고 false 를 받은 뒤 입력 · 포커스가 생길 때까지 다시 묻지 않는다.
            // 카드는 이벤트가 들어올 때 입력 없이 생기므로, 커널 버튼의 '꺼짐 판'(SurfaceSunken)이 머리 위에 남아 있었다.
            // 트리에 붙은 순간 한 번 다시 묻게 한다(요청은 병합되므로 카드가 많아도 한 번 돈다).
            Loaded += (_, _) => CommandManager.InvalidateRequerySuggested();
        }
    }
}
