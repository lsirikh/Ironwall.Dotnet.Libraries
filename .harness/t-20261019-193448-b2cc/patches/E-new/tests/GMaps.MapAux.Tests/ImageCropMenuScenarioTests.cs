using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 오버레이 이미지 우클릭 「다각형으로 자르기…」 · 「자르기 되돌리기」(t-20261019-193448-b2cc 영역 E · E-36 · D-7) —
/// 실제 ShowMarkerContextMenu 를 부르고 메뉴를 열지 않고 잡는다(<see cref="MenuTrap"/>). 편집 모드에서만 · 자른 그림에만 「자르기 되돌리기」 ·
/// 헤디드용 AutomationId · 누르면 판이 뜬다. 이미지 불러오기 명령의 판 가드 순서(파일 고르기 창보다 먼저)는 소스로 본다(창을 띄우지 않으려고).
/// </summary>
public class ImageCropMenuScenarioTests
{
    private const string CropHeader = "다각형으로 자르기…";
    private const string UncropHeader = "자르기 되돌리기";

    private sealed class Files : IDisposable
    {
        public string Base { get; } = Path.Combine(Path.GetTempPath(), $"imgcrop-menu-{Guid.NewGuid():N}");
        public string Abs(string relative) => Path.IsPathRooted(relative) ? relative : Path.Combine(Base, relative);

        public string Png(string relative, int w, int h)
        {
            var px = new byte[w * h * 4];
            for (int i = 3; i < px.Length; i += 4) px[i] = 255;
            Directory.CreateDirectory(Path.GetDirectoryName(Abs(relative))!);
            File.WriteAllBytes(Abs(relative), ImageCropMath.EncodePng(BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4)));
            return relative;
        }

        public void Dispose() { try { Directory.Delete(Base, recursive: true); } catch (IOException) { } }
    }

    private static GMapImageMarker Image(string relative)
        => new(Mock.Of<ILogService>(), new ImageModel
        {
            Id = 91, Title = "발전소", FilePath = relative, Width = 200, Height = 100, Opacity = 1, Visibility = true,
            Top = 37.501, Left = 127.0, Right = 127.004, Bottom = 37.499,
        });

    private static MapVmRig Rig(Files files, bool editMode)
    {
        var rig = MapVmRig.Create(editMode: editMode);
        Scenario.SetField(rig.Vm, "_imageCropResolve", (Func<string, string>)files.Abs);
        return rig;
    }

    [Fact]
    [Trait("Scenario", "E-36")]
    [Trait("Defect", "D-7")]
    public void should_show_crop_items_with_automation_ids_only_in_edit_mode_and_revert_only_for_cropped_image() => OneSta.Sta(() =>
    {
        using var files = new Files();
        var plain = files.Png("Images/Overlays/발전소_1.png", 200, 100);
        using var marker = Image(plain);

        // 운영 모드 — 자르기 항목 없음(메뉴 자체가 없거나 자르기 항목이 없다)
        var view = Rig(files, editMode: false);
        try { Assert.DoesNotContain(MenuTrap.Headers(view.RightClick(marker)), h => h == CropHeader || h == UncropHeader); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("메뉴가 만들어지지 않았다")) { }

        // 편집 모드 · 자르지 않은 그림 — 「다각형으로 자르기…」만
        var edit = Rig(files, editMode: true);
        var menu = edit.RightClick(marker);
        Assert.Contains(CropHeader, MenuTrap.Headers(menu));
        Assert.DoesNotContain(UncropHeader, MenuTrap.Headers(menu));
        Assert.Equal("GMaps.Symbol.ContextMenu.ImageCrop", AutomationProperties.GetAutomationId(MenuTrap.Item(menu, CropHeader)));

        // 자른 그림(옆 기록 있음) — 「자르기 되돌리기」도
        var crop = ImageCropMath.Crop(ImageCropStore.Load(files.Abs(plain)), new[] { new Point(100, 0), new Point(200, 0), new Point(200, 100), new Point(100, 100) });
        var cropped = ImageCropStore.Save(files.Abs, plain, crop.Image, crop.Box, 200, 100);
        marker.FilePath = cropped;
        var menu2 = edit.RightClick(marker);
        var headers = MenuTrap.Headers(menu2);
        Assert.True(headers.IndexOf(CropHeader) >= 0 && headers.IndexOf(UncropHeader) == headers.IndexOf(CropHeader) + 1, string.Join(" | ", headers));
        Assert.Equal("GMaps.Symbol.ContextMenu.ImageUncrop", AutomationProperties.GetAutomationId(MenuTrap.Item(menu2, UncropHeader)));
        Assert.Equal("GMaps.Symbol.ContextMenu.ImageCrop", AutomationProperties.GetAutomationId(MenuTrap.Item(menu2, CropHeader)));
    });

    [Fact]
    [Trait("Scenario", "E-36")]
    public void should_open_crop_panel_for_that_image_when_crop_menu_item_is_pressed() => OneSta.Sta(() =>
    {
        using var files = new Files();
        var plain = files.Png("Images/Overlays/발전소_2.png", 120, 60);
        using var marker = Image(plain);
        var rig = Rig(files, editMode: true);
        rig.Map.Markers.Add(marker);

        Parts.Press(MenuTrap.Item(rig.RightClick(marker), CropHeader));
        for (int i = 0; i < 20 && !rig.Vm.IsImageCropPanelOpen; i++) Scenario.Pump();
        Assert.True(rig.Vm.IsImageCropPanelOpen);
        Assert.Equal((120, 60), (rig.Vm.ImageCrop!.PixelWidth, rig.Vm.ImageCrop.PixelHeight));
        Assert.False(rig.Vm.ImageCrop.AllowSkip);
        Assert.Equal("다각형으로 자르기 — 발전소", rig.Vm.ImageCrop.Title);
        rig.Vm.ImageCrop.CancelCommand.Execute(null);
        for (int i = 0; i < 20 && rig.Vm.IsImageCropPanelOpen; i++) Scenario.Pump();
        Assert.False(rig.Vm.IsImageCropPanelOpen);
        Assert.Equal(plain, marker.FilePath);
    });

    [Fact]
    [Trait("Scenario", "E-16")]
    [Trait("Defect", "D-6")]
    public void should_check_open_crop_panel_before_opening_file_picker_when_image_overlay_is_loaded()
    {
        // 실행하면 파일 고르기 창(모달)이 뜨므로 소스 순서로 본다 — 권한 확인 → 판 가드 → 파일 고르기 창
        var src = File.ReadAllText(Path.Combine(RepoPaths.GMapsUi, "ViewModels", "Maps", "MapViewModel.cs"));
        int start = src.IndexOf("private async void ExecuteLoadImageOverlay(object obj)", StringComparison.Ordinal);
        Assert.True(start > 0, "ExecuteLoadImageOverlay 없음");
        int dialog = src.IndexOf("new Microsoft.Win32.OpenFileDialog", start, StringComparison.Ordinal);
        int guard = src.IndexOf("RejectWhileImageCropPanelOpen()", start, StringComparison.Ordinal);
        Assert.True(dialog > start);
        Assert.True(guard > start && guard < dialog, "자르기 판 가드가 파일 고르기 창보다 앞에 없다");
        Assert.Contains("private bool CanExecuteLoadImageOverlay(object arg) => MainMap != null;", src);   // CanExecute 는 그대로(단축키 무반응 방지)
    }
}
