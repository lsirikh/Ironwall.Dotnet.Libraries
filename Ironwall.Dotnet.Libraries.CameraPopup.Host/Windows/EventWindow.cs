using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Windows;

/// <summary>
/// 이벤트 창 골격(T-00). 머리 한 줄 + 타일 격자(영상 이미지 + 상태 글자).
/// 실제 모양(ConsoleWindowChrome · 토큰 · 우클릭 · 📌 · 꼬리)은 T-05 에서 바꾼다 — 여기서는 격리 경로 검증용.
/// </summary>
internal sealed class EventWindow : Window
{
    private readonly List<(Image Image, TextBlock Label)> _tiles = new();

    public EventWindow(OpenEventWindow msg)
    {
        Title = string.IsNullOrWhiteSpace(msg.Title) ? $"이벤트 {msg.EventKey}" : msg.Title;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = msg.Window.X;
        Top = msg.Window.Y;
        Width = Math.Max(240, msg.Window.Width);
        Height = Math.Max(160, msg.Window.Height);
        Topmost = msg.AlwaysOnTop;
        ShowActivated = false;
        Background = Brushes.Black;
        AutomationProperties.SetAutomationId(this, "CameraPopupHost.EventWindow");

        int count = Math.Clamp(msg.Cameras.Count, 1, TileGrid.MaxCameras);
        var (columns, rows) = TileGrid.Resolve(msg.GridColumns, msg.GridRows, count);
        var grid = new UniformGrid { Columns = columns, Rows = rows };
        for (int i = 0; i < count; i++)
        {
            var camera = i < msg.Cameras.Count ? msg.Cameras[i] : new EventWindowCamera();
            var image = new Image { Stretch = Stretch.Uniform };
            var label = new TextBlock
            {
                Text = $"{camera.Name ?? camera.CameraId} · 연결 중",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(0xA0, 0, 0, 0)),
                Margin = new Thickness(4),
                Padding = new Thickness(4, 1, 4, 1),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            AutomationProperties.SetAutomationId(label, $"CameraPopupHost.EventWindow.Tile.{i}.State");
            var cell = new Grid();
            cell.Children.Add(image);
            cell.Children.Add(label);
            grid.Children.Add(new Border { BorderBrush = Brushes.DimGray, BorderThickness = new Thickness(1), Child = cell });
            _tiles.Add((image, label));
        }

        var header = new TextBlock
        {
            Text = Title,
            Foreground = Brushes.White,
            Background = Brushes.DarkRed,
            Padding = new Thickness(8, 4, 8, 4),
        };
        DockPanel.SetDock(header, Dock.Top);
        var root = new DockPanel();
        root.Children.Add(header);
        root.Children.Add(grid);
        Content = root;
    }

    public int TileCount => _tiles.Count;

    public void SetTileSource(int index, ImageSource source) => _tiles[index].Image.Source = source;

    public void SetTileState(int index, string text) => _tiles[index].Label.Text = text;
}
