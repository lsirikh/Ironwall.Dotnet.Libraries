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
    /// Interaction logic for MalfunctionEventCardView.xaml
    /// </summary>
    public partial class MalfunctionEventCardView : UserControl
    {
        public MalfunctionEventCardView()
        {
            InitializeComponent();
            // U-14 — DetectionEventCardView 와 같은 이유: 뒤집기 버튼(routed command)의 CanExecute 를 트리에 붙은 뒤 다시 묻는다.
            Loaded += (_, _) => CommandManager.InvalidateRequerySuggested();
        }
    }
}
