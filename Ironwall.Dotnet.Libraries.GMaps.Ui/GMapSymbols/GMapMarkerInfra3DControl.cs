using System.Windows;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

public sealed class GMapMarkerInfra3DControl : GMapMarkerInfraControl
{
    private HousingVisual? _housing;
    public override bool RotatesIn2D => false;
    /// <summary>3D 는 빌보드가 아니다 — 회전을 yaw 로 표현(FR-10: Infra 2D 의 true 상속 차단).</summary>
    public override bool IsBillboard => false;
    protected override bool WritesBackRenderSize => false;
    static GMapMarkerInfra3DControl() => DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarkerInfra3DControl), new FrameworkPropertyMetadata(typeof(GMapMarkerInfra3DControl)));
    public GMapMarkerInfra3DControl() { }
    public GMapMarkerInfra3DControl(GMapInfraMarker marker) : base(marker) { }
    // 3D 는 건물 종류/운영 상태 색을 MarkerFill 에 쓰지 않는다. 사용자 채우기색(FillColor)이 BodyBrush 의 유일 채널이고,
    // base.UpdateMarkerAppearance() 의 상태색 로컬 대입은 OneWay MarkerFill 바인딩을 영구 제거해 이후 색 변경이 무시된다(D-12).
    // 상태 표현은 Housing3DMarkerStyle 의 MarkerState 트리거(Opacity·ERROR 스토리보드)가 담당한다. PIDS 3D 와 같은 선례.
    protected override void UpdateInfraAppearance() { }
    protected override void UpdateMarkerAppearance() { }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate(); _housing = GetTemplateChild("PART_Housing3D") as HousingVisual;
        UpdateHousing(); ApplyDisplayAngle(CurrentDisplayAngle);
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == BuildingTypeProperty || e.Property == BuildingUsageProperty) UpdateHousing();
    }
    private void UpdateHousing()
    {
        if (_housing == null) return;
        _housing.ModelKey = "infra." + BuildingType.ToString().ToLowerInvariant();
        _housing.RoofBrush = new SolidColorBrush(BuildingUsage.ToString() switch
        {
            "Medical" => Color.FromRgb(205, 85, 88),
            "Storage" => Color.FromRgb(211, 164, 90),
            "Manufacturing" => Color.FromRgb(122, 151, 175),
            "Public" => Color.FromRgb(106, 158, 130),
            "Education" => Color.FromRgb(104, 137, 184),
            "Retail" => Color.FromRgb(191, 131, 96),
            "Residential" => Color.FromRgb(161, 143, 119),
            "Mixed" => Color.FromRgb(141, 124, 175),
            _ => Color.FromRgb(67, 120, 155)
        });
    }
    protected override void ApplyDisplayAngle(double angle)
    { base.ApplyDisplayAngle(0); if (_housing != null) _housing.Yaw = angle; }
}
