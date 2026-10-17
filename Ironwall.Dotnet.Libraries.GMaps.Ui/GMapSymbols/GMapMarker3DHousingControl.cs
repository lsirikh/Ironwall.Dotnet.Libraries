using System.Windows;
using System.Windows.Data;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

public sealed class GMapMarker3DHousingControl : GMapMarkerPidsControl
{
    public static readonly DependencyProperty ModelVariantProperty = DependencyProperty.Register(nameof(ModelVariant), typeof(string), typeof(GMapMarker3DHousingControl), new PropertyMetadata(null));
    public static readonly DependencyProperty BaseBearingProperty = DependencyProperty.Register(nameof(BaseBearing), typeof(double), typeof(GMapMarker3DHousingControl), new PropertyMetadata(0d));
    public string? ModelVariant { get => (string?)GetValue(ModelVariantProperty); set => SetValue(ModelVariantProperty, value); }
    public double BaseBearing { get => (double)GetValue(BaseBearingProperty); set => SetValue(BaseBearingProperty, value); }
    private HousingVisual? _housing;
    public override bool RotatesIn2D => false;
    /// <summary>3D 는 빌보드가 아니다 — 회전을 yaw 로 표현하므로 회전 핸들·속성 회전 행을 유지(FR-10: Pids 2D 의 true 상속 차단).</summary>
    public override bool IsBillboard => false;
    protected override bool WritesBackRenderSize => false;
    protected override bool AnimateFovChanges => false;
    /// <summary>3D 문짝이 열림/닫힘을 직접 그린다 — 문 표시는 구동 중(RUNNING)만 낸다.</summary>
    protected override bool DoorLeavesShowPosition => true;
    protected override Point RotationPivot => new(.5, .5);
    static GMapMarker3DHousingControl() => DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapMarker3DHousingControl), new FrameworkPropertyMetadata(typeof(GMapMarker3DHousingControl)));
    public GMapMarker3DHousingControl() { }
    public GMapMarker3DHousingControl(GMapPidsMarker marker) : base(marker) { }

    protected override void SetupSpecificBindings()
    {
        base.SetupSpecificBindings();
        SetupPropertyBinding(ModelVariantProperty, nameof(GMapPidsMarker.ModelVariant), BindingMode.OneWay);
        SetupPropertyBinding(BaseBearingProperty, nameof(GMapPidsMarker.BaseBearing), BindingMode.OneWay);
    }
    public override void OnApplyTemplate()
    {
        if (_housing != null) _housing.ProjectionChanged -= OnProjectionChanged;
        base.OnApplyTemplate();
        _housing = GetTemplateChild("PART_Housing3D") as HousingVisual;
        if (_housing != null) _housing.ProjectionChanged += OnProjectionChanged;
        UpdateHousing(); ApplyDisplayAngle(CurrentDisplayAngle);
    }
    private void OnProjectionChanged(object? sender, EventArgs e) => UpdateFOVPath();
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == DeviceTypeProperty || e.Property == ModelVariantProperty || e.Property == BaseBearingProperty || e.Property == DetectionBearingProperty)
            UpdateHousing();
    }
    private void UpdateHousing()
    {
        if (_housing == null) return;
        _housing.ModelKey = HousingModels.DeviceKey(DeviceType, ModelVariant) ?? "sensor";
        _housing.Yaw = CurrentDisplayAngle + BaseBearing;
        _housing.HeadYaw = DeviceType == Enums.EnumDeviceType.IpCamera ? DetectionBearing - RotationAngle - BaseBearing : 0;
    }
    protected override void ApplyDisplayAngle(double angle)
    {
        base.ApplyDisplayAngle(0);
        UpdateHousing(); UpdateFOVPath();
    }
    protected override Point GetFovOrigin() => _housing?.LensPoint ?? new Point(ActualWidth / 2, ActualHeight / 2);
    protected override double GetFovBearing() => base.GetFovBearing() + CurrentDisplayAngle;
}
