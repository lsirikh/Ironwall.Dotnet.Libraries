using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapImages;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// 카탈로그 §1 등록(편집 스트립) — 타일 지도 불러오기(MAP-074) · 이미지 불러오기(MAP-075) · 사용자 지도 만들기(MAP-076) 헤드리스 짝.
/// 두 불러오기는 처리기 안에서 Win32 OpenFileDialog 를 직접 만든다(대체 지점 없음) — 그래서 창 앞부분(단추 · 단축키 배선 · 실행 가능 · 권한 게이트)만
/// 창 없이 본다. 권한 게이트는 파일창보다 먼저 돌아 나오므로 파일창이 뜨지 않는다. 파일 고른 뒤(타일 · 이미지 생성)는 실창 몫.
/// 사용자 지도 만들기는 파일창 없이 등록 패널(MapRegistrationControl)을 띄우므로 패널이 뜨고 · 내려가는 데까지 본다(타일 생성은 실창 몫).
/// </summary>
public class MapRegistrationCommandScenarioTests
{
    private static string MapViewXaml()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.GMaps.Ui", "Views", "Maps", "MapView.xaml"));
        return Regex.Replace(text, "<!--.*?-->", string.Empty, RegexOptions.Singleline);   // 주석 처리된 옛 단축키(라인 그리기 Ctrl+L)는 빼고 본다
    }

    /// <summary>편집 스트립 단추가 그 명령에 묶였고, 같은 명령에 Ctrl+키 단축키가 딱 하나 걸렸는지.</summary>
    private static void AssertWired(string automationId, string command, string key)
    {
        var xaml = MapViewXaml();
        Assert.Matches(new Regex($"AutomationProperties\\.AutomationId=\"{Regex.Escape(automationId)}\"\\s+Command=\"\\{{Binding {command}\\}}\"[^>]*ToolTip=\"[^\"]*\\(Ctrl\\+{key}\\)\""), xaml);
        var bindings = Regex.Matches(xaml, $"<KeyBinding\\s+Key=\"{key}\"\\s+Modifiers=\"Ctrl\"[^>]*/>").Select(m => m.Value).ToList();
        var binding = Assert.Single(bindings);                        // 같은 단축키가 둘이면 어느 것이 먹을지 모른다
        Assert.Contains($"Command=\"{{Binding {command}}}\"", binding);
    }

    /// <summary>임시 폴더에 작은 진짜 TIFF 를 쓴다 — 깨진 파일이면 이미지 읽기가 실패하며 파일을 쥐고 있어 지울 수 없다.</summary>
    private static string TempTiff()
    {
        var path = Path.Combine(Path.GetTempPath(), $"maplayer-076-{Guid.NewGuid():N}.tif");
        var pixels = new byte[4 * 3 * 4];
        var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(4, 3, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, 4 * 4);
        var encoder = new System.Windows.Media.Imaging.TiffBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
        return path;
    }

    private static MapVm WithFileCommands(bool canEdit)
    {
        var f = new MapVm(canEdit);
        Scenario.Call(f.Vm, "InitializeFileCommands");                // 생성자가 부르는 명령 초기화(파일 명령 묶음)
        return f;
    }

    // ── MAP-074 — 타일 지도 불러오기(GMaps.EditStrip.LoadMap · Ctrl+L) ──

    [Trait("Scenario", "MAP-074")]
    [Fact]
    public void should_wire_load_map_button_and_ctrl_l_to_one_command_and_stop_before_file_dialog_without_edit_permission() => Scenario.Sta(() =>
    {
        AssertWired("GMaps.EditStrip.LoadMap", "LoadMapImageCommand", "L");
        var f = WithFileCommands(canEdit: false);

        Assert.True(f.Vm.LoadMapImageCommand!.CanExecute(null));      // 단추 · 단축키는 늘 켜져 있다
        f.Vm.LoadMapImageCommand.Execute(null);                      // 비편집 계정 — 파일창 앞에서 돌아 나온다

        var info = Assert.Single(f.Of<OpenInfoPopupMessageModel>());
        Assert.Equal("권한 없음", info.Title);
        Assert.Contains("맵 편집 권한", info.Explain);
        Assert.Contains(f.Log.Lines, l => l.Contains("맵파일 추가 차단"));
        Assert.DoesNotContain(f.Log.Lines, l => l.Contains("커스텀 맵 불러오기 시작"));   // 파일창 단계에 들어가지 않았다
    });

    // ── MAP-075 — 이미지 불러오기(GMaps.EditStrip.LoadImage · Ctrl+I) ──

    [Trait("Scenario", "MAP-075")]
    [Fact]
    public void should_wire_load_image_button_and_ctrl_i_to_one_command_and_stop_before_file_dialog_without_edit_permission() => Scenario.Sta(() =>
    {
        AssertWired("GMaps.EditStrip.LoadImage", "LoadImageOverlayCommand", "I");
        var f = WithFileCommands(canEdit: false);

        Assert.True(f.Vm.LoadImageOverlayCommand!.CanExecute(null));  // 지도가 있으면 켜짐
        f.Vm.LoadImageOverlayCommand.Execute(null);

        Assert.Equal("권한 없음", Assert.Single(f.Of<OpenInfoPopupMessageModel>()).Title);
        Assert.Contains(f.Log.Lines, l => l.Contains("이미지 오버레이 추가 차단"));
        Assert.DoesNotContain(f.Log.Lines, l => l.Contains("이미지 오버레이 불러오기 시작"));
        Assert.Equal(0, f.Map.Markers.Count);                         // 아무것도 올라가지 않는다
    });

    [Trait("Scenario", "MAP-075")]
    [Fact]
    public void should_disable_load_image_when_there_is_no_map_yet() => Scenario.Sta(() =>
    {
        var f = WithFileCommands(canEdit: true);
        Scenario.SetField(f.Vm, "<MainMap>k__BackingField", null);    // 지도가 아직 붙기 전

        Assert.False(f.Vm.LoadImageOverlayCommand!.CanExecute(null)); // 단추 · Ctrl+I 꺼짐(파일창을 열 곳이 없다)
    });

    // ── MAP-076 — 사용자 지도 만들기(GMaps.EditStrip.CreateCustomMap · Ctrl+N) ──

    [Trait("Scenario", "MAP-076")]
    [Fact]
    public void should_open_registration_panel_for_selected_image_and_close_it_on_cancel_when_create_custom_map_runs() => Scenario.Sta(() =>
    {
        AssertWired("GMaps.EditStrip.CreateCustomMap", "CreateCustomMapCommand", "N");
        var f = WithFileCommands(canEdit: true);
        var path = TempTiff();
        var length = new FileInfo(path).Length;
        try
        {
            Assert.False(f.Vm.CreateCustomMapCommand!.CanExecute(null));   // 고른 이미지가 없으면 꺼짐
            var image = new GMapCustomImage(new FakeLog(), new ImageModel
            {
                Title = "안양 정사영상", FilePath = path, Width = 640, Height = 480,
                Left = 126.90, Right = 126.92, Top = 37.40, Bottom = 37.38, Latitude = 37.39, Longitude = 126.91, Opacity = 1, Visibility = true,
            });
            f.Vm.SelectedImage = image;
            Assert.True(f.Vm.CreateCustomMapCommand.CanExecute(null));

            f.Vm.CreateCustomMapCommand.Execute(null);                 // 단추 · Ctrl+N

            // 등록 패널(생성 흐름 1단계) — 파일 이름 · 크기 · 해상도 · 레이어 이름 · 줌 범위
            Assert.True(f.Vm.IsMapRegistrationPanelVisible);
            var panel = f.Vm.MapRegistrationPanel!;
            Assert.Equal(RegistrationPhase.Register, panel.Phase);
            Assert.Equal(Path.GetFileName(path), panel.FileName);
            Assert.True(length < 1024, $"전제: 작은 파일({length} B)");
            Assert.Equal($"{length} B", panel.FileSize);
            Assert.Equal("640 x 480 px", panel.ImageResolution);
            Assert.Equal("안양 정사영상", panel.LayerName);
            Assert.Equal((10, 15), (panel.MinZoom, panel.MaxZoom));   // 최대 줌 = 지금 지도 줌

            Scenario.Call(panel, "RaiseCancelRequested");              // 「취소」
            Assert.False(f.Vm.IsMapRegistrationPanelVisible);
            Assert.Null(f.Vm.MapRegistrationPanel);
            Assert.Same(image, f.Vm.SelectedImage);                    // 고른 이미지는 그대로(다시 만들 수 있다)
        }
        finally { File.Delete(path); }
    });

    [Trait("Scenario", "MAP-076")]
    [Fact]
    public void should_block_registration_panel_with_reason_when_map_edit_permission_is_missing() => Scenario.Sta(() =>
    {
        var f = WithFileCommands(canEdit: false);
        var path = TempTiff();
        try
        {
            f.Vm.SelectedImage = new GMapCustomImage(new FakeLog(), new ImageModel { Title = "안양", FilePath = path, Width = 10, Height = 10, Left = 126.9, Right = 127.0, Top = 37.4, Bottom = 37.3 });

            f.Vm.CreateCustomMapCommand!.Execute(null);

            Assert.False(f.Vm.IsMapRegistrationPanelVisible);
            Assert.Null(f.Vm.MapRegistrationPanel);
            Assert.Equal("권한 없음", Assert.Single(f.Of<OpenInfoPopupMessageModel>()).Title);
        }
        finally { File.Delete(path); }
    });

    [Trait("Scenario", "MAP-076")]
    [Fact]
    public void should_not_open_registration_panel_and_log_error_when_selected_image_file_is_gone() => Scenario.Sta(() =>
    {
        var f = WithFileCommands(canEdit: true);
        var missing = Path.Combine(Path.GetTempPath(), $"maplayer-076-missing-{Guid.NewGuid():N}.tif");
        f.Vm.SelectedImage = new GMapCustomImage(new FakeLog(), new ImageModel { Title = "사라진 원본", FilePath = missing, Width = 10, Height = 10, Left = 126.9, Right = 127.0, Top = 37.4, Bottom = 37.3 });

        f.Vm.CreateCustomMapCommand!.Execute(null);

        Assert.False(f.Vm.IsMapRegistrationPanelVisible);
        Assert.Contains(f.Log.Lines, l => l.StartsWith("E ") && l.Contains("이미지 파일을 찾을 수 없습니다"));
    });

    private static string RepoRoot([CallerFilePath] string? path = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
}
