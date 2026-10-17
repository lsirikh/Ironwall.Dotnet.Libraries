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
using System.Windows.Shapes;

namespace Ironwall.Dotnet.Libraries.Streaming.Views
{
    /// <summary>
    /// Interaction logic for PopupWindowView.xaml
    /// </summary>
    public partial class PopupWindowView : Window
    {
        public PopupWindowView()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // disposal은 PopupViewerService.OnWindowClosed가 단독 담당 (중복 호출 방지)
        }
    }
}
