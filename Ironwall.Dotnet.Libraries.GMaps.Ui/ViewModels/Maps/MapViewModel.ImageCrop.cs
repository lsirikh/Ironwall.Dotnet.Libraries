using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 오버레이 이미지 다각형 자르기(2026-10-19) — 발전소(보안 구역)만 새 위성 사진으로 3D 를 만들 때 사진을 울타리 모양으로 잘라 투명 PNG 로 넣는다.
   Note         : ★ 처음 불러올 때: 파일을 고른 직후(복사 전) 「이미지 자르기」 판 → 적용 = 원본 복사본 옆에 자른 PNG(+ .crop.json 기록), 그대로 = 예전대로, 취소 = 추가 안 함.
                  ★ 이미 놓인 그림: 우클릭 「다각형으로 자르기」 → 남은 부분이 같은 자리에 머물게 경계를 옮긴다(회전 고려, ImageCropMath.CropBounds) · DB 저장 · Undo 1단위.
                    「자르기 되돌리기」 = 기록의 자르기 전 그림 · 상자로 경계를 넓힌다(ImageCropMath.UncropBounds) · Undo 1단위.
                  ★ 그림 파일이 바뀌므로 MapLayers(OverlayImage).FilePath 도 함께 바꾼다(레이어 패널이 경로로 그림을 찾는다).
                  ★ 3D 는 투명 PNG 의 불투명 부분 모양을 범위로 쓴다(map-3d) — GIS 는 투명 픽셀이 있으면 PNG 로 보낸다(Map3DImageCache.Encode).
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private ImageCropViewModel? _imageCrop;
    private ImageCropPanelControl? _imageCropPanel;
    /// <summary>저장 경로 → 절대 경로(기본 = IImageFileService.GetAbsolutePath — 시험은 임시 폴더로 바꾼다).</summary>
    private Func<string, string>? _imageCropResolve;
    private string ResolveImagePath(string path) => (_imageCropResolve ?? _imageFileService.GetAbsolutePath)(path);

    /// <summary>열린 「이미지 자르기」 판 VM(없으면 null).</summary>
    public ImageCropViewModel? ImageCrop => _imageCrop;
    /// <summary>「이미지 자르기」 판(OverlayWindow 패턴 Control) — DataContext = ImageCrop, 머리 띠 ✕ = 취소.</summary>
    public ImageCropPanelControl? ImageCropPanel => _imageCropPanel;
    public bool IsImageCropPanelOpen => _imageCrop != null;

    /// <summary>판을 열고 결과를 기다린다(닫히면 판은 사라진다). 이미 열려 있으면 취소로 본다.</summary>
    internal async Task<(ImageCropOutcome Outcome, ImageCropViewModel Crop)> PromptImageCropAsync(BitmapSource image, string title, bool allowSkip)
    {
        var vm = new ImageCropViewModel(image, title, allowSkip);
        if (_imageCrop != null) return (ImageCropOutcome.Cancel, vm);
        _imageCrop = vm;
        _imageCropPanel = new ImageCropPanelControl { DataContext = vm, CloseCommand = vm.CancelCommand };
        RaiseImageCropPanel();
        try { return (await vm.Completion, vm); }
        finally
        {
            _imageCrop = null;
            _imageCropPanel = null;
            RaiseImageCropPanel();
        }
    }

    private void RaiseImageCropPanel()
    {
        NotifyOfPropertyChange(nameof(ImageCrop));
        NotifyOfPropertyChange(nameof(ImageCropPanel));
        NotifyOfPropertyChange(nameof(IsImageCropPanelOpen));
    }

    /// <summary>판이 떠 있으면 또 불러오지 않는다 — 안내하고 true(파일 고르기 창도 열지 않는다).</summary>
    private bool RejectWhileImageCropPanelOpen()
    {
        if (!IsImageCropPanelOpen) return false;
        _log?.Info("[ImageCrop] 자르기 판이 떠 있어 이미지 불러오기 막음");
        SetAimStatus("자르기 판을 먼저 닫으세요", autoHide: true);
        return true;
    }

    /// <summary>열린 판을 취소로 닫는다(편집 모드 끄기) — 처음 불러오기 판이면 추가하지 않는다.</summary>
    private void CancelImageCropPanel()
    {
        if (_imageCrop is not { } crop) return;
        _log?.Info("[ImageCrop] 편집 모드가 꺼져 자르기 판 취소");
        crop.CancelCommand.Execute(null);
    }

    /// <summary>키가 「이미지 자르기」 판 안(포커스 또는 키 원본)에서 왔는가 — 지도 키 처리기(Delete · Ctrl+Z/Y · 방향키)가 손대지 않게.</summary>
    private bool IsImageCropPanelKey(System.Windows.Input.KeyEventArgs e)
        => _imageCropPanel is { } panel
           && (IsInImageCropPanel(panel, System.Windows.Input.Keyboard.FocusedElement) || IsInImageCropPanel(panel, e.OriginalSource));

    private static bool IsInImageCropPanel(System.Windows.Media.Visual panel, object? element)
        => element is System.Windows.DependencyObject d
           && (ReferenceEquals(panel, d) || ((d is System.Windows.Media.Visual || d is System.Windows.Media.Media3D.Visual3D) && panel.IsAncestorOf(d)));

    /// <summary>
    /// 오버레이 이미지 추가 — 고른 파일로 「이미지 자르기」 판을 띄우고, 취소가 아니면 저장 폴더에 복사한다.
    /// 돌려주는 값 = 지도에 놓을 그림의 저장 경로(자르기 적용이면 자른 PNG), 취소면 null.
    /// </summary>
    internal async Task<string?> CopyOverlayImageWithCropAsync(string originalPath, string title)
    {
        if (RejectWhileImageCropPanelOpen()) return null;
        BitmapSource? source = null;
        try { source = ImageCropStore.Load(originalPath); }
        catch (Exception ex) { _log?.Warning($"[ImageCrop] 그림을 읽지 못해 자르기 없이 추가: {ex.Message}"); }

        ImageCropOutcome outcome = ImageCropOutcome.Skip;
        ImageCropViewModel? crop = null;
        if (source != null) (outcome, crop) = await PromptImageCropAsync(source, "이미지 자르기", allowSkip: true);
        if (outcome == ImageCropOutcome.Cancel)
        {
            _log?.Info($"[ImageCrop] 오버레이 이미지 추가 취소: {title}");
            return null;
        }

        try
        {
            var copied = _imageFileService.CopyImageToLocal(originalPath);
            if (outcome != ImageCropOutcome.Apply || source == null || crop == null) return copied;

            var (image, box) = ImageCropMath.Crop(source, crop.Points.ToList());
            var cropped = ImageCropStore.Save(ResolveImagePath, copied, image, box, source.PixelWidth, source.PixelHeight);
            _log?.Info($"[ImageCrop] 처음 불러올 때 자름: {copied} → {cropped} 상자=({box.X},{box.Y},{box.Width}x{box.Height}) 꼭짓점={crop.PointCount}");
            return cropped;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 원본이 사라졌거나 저장 폴더에 쓸 수 없음 — 추가하지 않고 알린다(예외를 위로 올리지 않는다)
            _log?.Error($"[ImageCrop] 저장 폴더에 쓰지 못해 추가 안 함: {ex.Message}");
            SetAimStatus("그림을 저장하지 못해 추가하지 않았습니다 — 저장 폴더를 확인하세요", autoHide: true);
            return null;
        }
    }

    /// <summary>우클릭 「다각형으로 자르기」 — 이미 놓인 그림을 잘라 같은 자리에 둔다(Undo 1단위).</summary>
    internal async Task RecropImageAsync(GMapImageMarker marker)
    {
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (IsImageCropPanelOpen || string.IsNullOrEmpty(marker.FilePath)) return;
        var path = marker.FilePath!;
        BitmapSource source;
        try { source = ImageCropStore.Load(ResolveImagePath(path)); }
        catch (Exception ex)
        {
            _log?.Warning($"[ImageCrop] 그림 읽기 실패 {path}: {ex.Message}");
            SetAimStatus("그림 파일을 읽을 수 없어 자를 수 없습니다", autoHide: true);
            return;
        }

        var (outcome, crop) = await PromptImageCropAsync(source, $"다각형으로 자르기 — {marker.Title}", allowSkip: false);
        if (outcome != ImageCropOutcome.Apply || marker.IsDisposed) return;
        // 판이 떠 있는 동안 그림이 바뀜(이전 자르기 Undo 등) — 연 때의 그림 · 크기와 지금 경계가 섞이지 않게 적용하지 않는다
        if (!string.Equals(marker.FilePath, path, StringComparison.Ordinal))
        {
            _log?.Info($"[ImageCrop] 판이 떠 있는 동안 그림이 바뀌어 자르기 취소 id={marker.Id} {path} → {marker.FilePath}");
            SetAimStatus("그림이 바뀌어 자르기를 취소했습니다", autoHide: true);
            return;
        }

        int w = source.PixelWidth, h = source.PixelHeight;
        var (image, box) = ImageCropMath.Crop(source, crop.Points.ToList());
        string newPath;
        try { newPath = ImageCropStore.Save(ResolveImagePath, path, image, box, w, h); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Error($"[ImageCrop] 자른 그림 저장 실패 id={marker.Id}: {ex.Message}");
            SetAimStatus("자른 그림을 저장하지 못했습니다 — 저장 폴더를 확인하세요", autoHide: true);
            return;
        }
        var before = CaptureImageCropState(marker);
        var after = new ImageCropState(newPath, ImageCropMath.CropBounds(marker.ImageBounds, marker.Bearing, w, h, box),
                                       marker.Width * box.Width / w, marker.Height * box.Height / h);
        if (!await TryApplyImageCropStateAsync(marker.Id, after, before))
        {
            SetAimStatus("저장하지 못해 자르지 않았습니다", autoHide: true);
            return;
        }
        _undoService?.Push(new ImageCropCommand("다각형으로 자르기", marker.Id, before, after, ApplyImageCropStateAsync));
        _log?.Info($"[ImageCrop] 다시 자름 id={marker.Id} {path} → {newPath} 상자=({box.X},{box.Y},{box.Width}x{box.Height}) 회전={marker.Bearing:F1}");
        SetAimStatus("잘랐습니다 — 되돌리려면 Ctrl+Z 또는 우클릭 「자르기 되돌리기」", autoHide: true);
    }

    /// <summary>자른 그림인가(옆 기록이 있는가) — 「자르기 되돌리기」 보이기.</summary>
    internal bool IsCroppedImage(GMapImageMarker marker) => ImageCropStore.Read(ResolveImagePath, marker.FilePath) != null;

    /// <summary>우클릭 「자르기 되돌리기」 — 자르기 전 그림으로, 남은 부분 자리는 그대로(Undo 1단위). 여러 번 자른 그림은 한 번에 한 단계씩.</summary>
    internal async Task UncropImageAsync(GMapImageMarker marker)
    {
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (ImageCropStore.Read(ResolveImagePath, marker.FilePath) is not { } record)
        {
            SetAimStatus("자른 그림이 아니라 되돌릴 것이 없습니다", autoHide: true);
            return;
        }
        if (!File.Exists(ResolveImagePath(record.Source)))
        {
            SetAimStatus("자르기 전 그림 파일이 없어 되돌릴 수 없습니다", autoHide: true);
            return;
        }
        var box = record.Box;
        var before = CaptureImageCropState(marker);
        var after = new ImageCropState(record.Source,
                                       ImageCropMath.UncropBounds(marker.ImageBounds, marker.Bearing, record.SourceWidth, record.SourceHeight, box),
                                       marker.Width * record.SourceWidth / box.Width, marker.Height * record.SourceHeight / box.Height);
        if (!await TryApplyImageCropStateAsync(marker.Id, after, before))
        {
            SetAimStatus("저장하지 못해 되돌리지 않았습니다", autoHide: true);
            return;
        }
        _undoService?.Push(new ImageCropCommand("자르기 되돌리기", marker.Id, before, after, ApplyImageCropStateAsync));
        _log?.Info($"[ImageCrop] 자르기 되돌림 id={marker.Id} {marker.FilePath} ← {record.Source}");
        SetAimStatus("자르기 전 그림으로 되돌렸습니다", autoHide: true);
    }

    private static ImageCropState CaptureImageCropState(GMapImageMarker marker)
        => new(marker.FilePath ?? string.Empty, marker.ImageBounds, marker.Width, marker.Height);

    /// <summary>자르기 상태 적용(실행 · Undo · Redo 공용) — 그림 파일 · 크기 · 경계 → DB · MapLayers 경로 · 화면.</summary>
    internal Task ApplyImageCropStateAsync(int imageId, ImageCropState state, CancellationToken ct = default)
        => TryApplyImageCropStateAsync(imageId, state, restoreOnFailure: null);

    /// <summary>
    /// 자르기 상태 적용 — 저장(DB)했으면 true. 저장에 실패하고 restoreOnFailure 가 있으면 화면 · 모델을 그 상태로 되돌린다
    /// (DB 는 바뀌지 않았으므로 — 자르기 · 되돌리기는 Undo 를 쌓지 않는다).
    /// </summary>
    private Task<bool> TryApplyImageCropStateAsync(int imageId, ImageCropState state, ImageCropState? restoreOnFailure)
        => RunOnUiAsync(async () =>
        {
            if (FindMarkerById(imageId, isImage: true) is not GMapImageMarker img) return false;
            var oldPath = img.FilePath;
            SetImageCropState(img, state);
            bool saved;
            try { await DbUpdateProcess(img); saved = true; }
            catch (Exception ex) { saved = false; _log?.Error($"[ImageCrop] 저장 실패 id={imageId}: {ex.Message}"); }
            if (saved)
            {
                // 그림 행은 저장됐다 — MapLayers 경로만 실패하면 로그만(그림은 바뀐 것)
                try { await UpdateOverlayImageLayerPathAsync(oldPath, state.FilePath); }
                catch (Exception ex) { _log?.Error($"[ImageCrop] 저장 실패 id={imageId}: {ex.Message}"); }
            }
            else if (restoreOnFailure is { } back) SetImageCropState(img, back);
            if (MainMap != null)
            {
                img.UpdateScreenPosition(MainMap);
                img.ForceUpdateLocalPosition(MainMap);
                MainMap.InvalidateVisual();
            }
            return saved;
        });

    private static void SetImageCropState(GMapImageMarker img, ImageCropState state)
    {
        img.SuppressBoundsUpdate = true;   // 크기(px)만 바꾸고 경계는 아래에서 직접
        try { img.Width = state.Width; img.Height = state.Height; }
        finally { img.SuppressBoundsUpdate = false; }
        img.FilePath = state.FilePath;
        img.ImageBounds = state.Bounds;
    }

    /// <summary>MapLayers(OverlayImage) 의 그림 경로를 바꾼다(레이어 패널은 경로로 그림을 찾는다).</summary>
    private async Task UpdateOverlayImageLayerPathAsync(string? oldPath, string newPath)
    {
        if (string.IsNullOrEmpty(oldPath) || string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) || _gMapDbService == null) return;
        var layers = await _gMapDbService.FetchMapLayersAsync();
        var layer = layers?.FirstOrDefault(l => l.LayerType == "OverlayImage" && string.Equals(l.FilePath, oldPath, StringComparison.OrdinalIgnoreCase));
        if (layer == null) return;
        layer.FilePath = newPath;
        await _gMapDbService.UpdateMapLayerAsync(layer);
        await LoadLayersFromDbAsync();
    }
}
