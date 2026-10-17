using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Dialogs
{
    /// <summary>
    /// 탐지 조치보고 창(B5). 모양 · 키 · 첫 포커스는 커널 틀(<c>ConsoleDialogFrame</c>)이 정한다.
    /// </summary>
    public partial class DetectionReportDialogView : UserControl
    {
        public DetectionReportDialogView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 틀의 취소(머리 ✕ · ESC)를 [취소] 버튼(<c>x:Name="ClickCancel"</c>)의 Click 으로 넘긴다 — 닫는 길이 그 버튼 하나라
        /// 두 번 닫히지 않고, Caliburn 액션(<c>ClickCancel()</c>)까지 예전과 같은 길을 탄다. 꺼진 버튼은 누르지 않는다. 호출 스레드: UI.
        /// </summary>
        private void OnSecondaryInvoked(object sender, RoutedEventArgs e)
        {
            if (!ClickCancel.IsEnabled || ClickCancel.Visibility != Visibility.Visible) return;
            ClickCancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, ClickCancel));
        }

        #region - 사진 크게 보기 -
        /// <summary>
        /// 사진을 눌렀다 — 같은 칸(Grid)의 확대 팝업을 창 가운데에 연다. 크기는 창의 85% 안에서 사진 비율 그대로
        /// (<see cref="ThumbnailZoomRules.Fit"/>). 사진을 아직 못 받았으면 열지 않는다. 팝업은 x:Name 없이 형제로 찾는다
        /// (x:Name 은 Caliburn 지시자라 새로 달지 않는다). 호출 스레드: UI.
        /// </summary>
        private void OnThumbnailClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is not FrameworkElement { DataContext: DetectionSelectionViewModel { Thumbnail: BitmapSource image } }) return;
            if ((sender as FrameworkElement)?.Parent is not Panel panel) return;
            var popup = panel.Children.OfType<Popup>().FirstOrDefault();
            if (popup?.Child is not FrameworkElement large) return;

            // 조치보고 창이 따로 뜬 작은 창이면(비모달 소유 창) 그 주인 창(앱 본창) 기준으로 키운다.
            var own = Window.GetWindow(this);
            var host = own?.Owner ?? own;
            var size = ThumbnailZoomRules.Fit(image.Width, image.Height,
                host?.ActualWidth ?? double.NaN, host?.ActualHeight ?? double.NaN);
            if (large is Border { Child: Image picture })
            {
                picture.Width = size.Width;
                picture.Height = size.Height;
            }

            popup.PlacementTarget = host?.Content as UIElement ?? this;
            popup.IsOpen = true;
            _zoom = popup;
            large.Focus();
        }

        /// <summary>확대된 사진을 눌렀다 — 닫는다.</summary>
        private void OnZoomedImageClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            CloseZoom();
        }

        /// <summary>확대 팝업에 포커스가 있을 때의 ESC — 확대만 닫는다.</summary>
        private void OnZoomedKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            CloseZoom();
        }

        /// <summary>
        /// 포커스가 창에 남아 있을 때의 ESC — 확대가 열려 있으면 확대만 닫고 틀(취소)로 넘기지 않는다.
        /// 터널 단계라 틀의 PreviewKeyDown(ESC=취소)보다 먼저 온다.
        /// </summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _zoom is { IsOpen: true })
            {
                e.Handled = true;
                CloseZoom();
                return;
            }
            base.OnPreviewKeyDown(e);
        }

        private void CloseZoom()
        {
            if (_zoom != null) _zoom.IsOpen = false;
            _zoom = null;
        }

        private Popup? _zoom;
        #endregion
    }
}
