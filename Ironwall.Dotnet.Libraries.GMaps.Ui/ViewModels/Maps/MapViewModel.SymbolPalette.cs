using GMap.NET;
using System.Windows;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

public partial class MapViewModel
{
    private bool _isSymbolPaletteVisible;
    private string? _placeVariant;
    public bool IsSymbolPaletteVisible
    {
        get => _isSymbolPaletteVisible;
        set { _isSymbolPaletteVisible = value && IsEditModeEnabled; NotifyOfPropertyChange(nameof(IsSymbolPaletteVisible)); }
    }
    public void ToggleSymbolPalette() => IsSymbolPaletteVisible = !IsSymbolPaletteVisible;
    public void CloseSymbolPalette() => IsSymbolPaletteVisible = false;

    public bool BeginPalettePlacement(SymbolPaletteItem item)
    {
        if (MainMap == null || !IsEditModeEnabled || !CanEditMap()) return false;
        if (MainMap.IsHomePlacementMode) ExitHomePlacementMode();
        EnterSymbolPlacementMode(item.Category, item.Type, item.Title);
        _placeVariant = item.ModelVariant;
        return true;
    }
    public void CancelPalettePlacement() => ExitSymbolPlacementMode();

    /// <summary>
    /// 팔레트 목록에서 사용자가 Esc 로 배치를 취소 — 지도 Esc 경로(OnMapPreviewKeyDownForAim)와 같은 「배치를 취소했습니다.」 안내(EDT-S2).
    /// 배치 중이 아니면 조용히 둔다. 끌어 놓기 뒤 정리 · 다른 도구로 넘어갈 때는 안내 없는 <see cref="CancelPalettePlacement"/> 를 쓴다.
    /// </summary>
    public void EscapePalettePlacement()
    {
        bool wasPlacing = MainMap?.IsSymbolPlacementMode ?? false;
        ExitSymbolPlacementMode();
        if (wasPlacing) SetAimStatus("배치를 취소했습니다.", autoHide: true);
    }

    public async Task PlacePaletteSymbolAsync(SymbolPaletteItem item, PointLatLng geo)
    {
        if (MainMap == null || !IsEditModeEnabled || !CanEditMap() || !MainMap.IsSymbolPlacementMode) return;
        ExitSymbolPlacementMode();
        await PlaceSymbolAsync(item.Category, item.Type, item.Title, item.ModelVariant, geo);
    }

    private async Task PlaceSymbolAsync(EnumMarkerCategory category, object type, string title, string? variant, PointLatLng geo)
    {
        if (!IsEditModeEnabled || !CanEditMap()) return;
        switch (category)
        {
            case EnumMarkerCategory.BASIC_SHAPES when type is string basic: await AddBasicShapeMarker(geo, basic, title); break;
            case EnumMarkerCategory.GEOMETRICS when type is EnumShapeType shape: await AddGeometricMarker(geo, shape, title); break;
            case EnumMarkerCategory.PIDS_EQUIPMENT when type is EnumDeviceType device: await AddPidsMarker(geo, device, title, variant); break;
            case EnumMarkerCategory.INFRASTRUCTURE when type is string building: await AddInfraMarker(geo, building, title); break;
        }
    }
}
