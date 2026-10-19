using System.Linq;
using CoordinateSharp;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 묶음 회전 컨트롤러 판(MapView.xaml "Map.GroupRotation.Panel") — 선택 묶음이 어디를 중심으로 몇 도 도는지
                  보이고, 중심 · 각도를 바꾸고, 확정(저장) · 취소(원위치)한다.
                  판은 회전 세션이 열리면(손잡이 끌기 · ] [ 키 · 편집 도구줄 「회전」) 뜨고 세션이 끝나면 닫힌다.
   Note         : ★ 판의 모든 값은 GroupSelectionService.Rotation(GroupRotationSession) 하나에서 다시 읽는다 —
                    RotationChanged 마다 입력칸 글을 세션 값으로 덮는다(손잡이 · 키 → 판, 판 → 세션 → 지도 미리보기 · 판 양방향).
                  ★ 판 닫기(×) = 취소(원위치). 선택이 바뀌거나 다른 키 · 이동을 시작하면 기존 규칙대로 먼저 확정.
                  입력 검증 · 요약 글은 순수 함수 GroupRotationPanelRules(헤드리스 시험).
   Created On   : 2026-10-18 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private string _groupRotationCenterLat = "";
    private string _groupRotationCenterLon = "";
    private string _groupRotationAngleText = "";
    private string? _groupRotationInputError;
    private string? _groupRotationCenterMgrs;
    private string? _groupRotationCenterUtm;
    private PointLatLng? _groupRotationGridPivot;   // MGRS · UTM 을 마지막으로 계산한 중심(같으면 다시 안 함)
    private bool _groupRotationWasPicking;

    /// <summary>판이 열려 있는가 = 묶음 회전 세션이 열려 있는가.</summary>
    public bool IsGroupRotationPanelOpen => _groupSelection?.IsRotating == true;

    private GroupRotationPanelControl? _groupRotationPanel;
    /// <summary>묶음 회전 컨트롤러 판(OverlayWindow 패턴 Control) — DataContext=this, 머리 띠 ✕ = 취소.</summary>
    public GroupRotationPanelControl GroupRotationPanel => _groupRotationPanel ??= new GroupRotationPanelControl
    {
        DataContext = this,
        CloseCommand = CancelGroupRotationCommand,
    };

    /// <summary>선택 항목 수 · 종류별 요약(돌아가는 것만 — 잠금은 "제외" 로 따로).</summary>
    public string GroupRotationSummary
    {
        get
        {
            var s = _groupSelection?.Rotation;
            if (s == null) return "";
            int markers = 0, lines = 0, shapes = 0, images = 0;
            foreach (var o in s.Originals)
            {
                switch (o.Marker)
                {
                    case ILineEditableMarker: lines++; break;
                    case IImageEditableMarker: images++; break;
                    case IGeoEditableMarker: shapes++; break;
                    default: markers++; break;
                }
            }
            int locked = (_groupSelection?.Selection.Count ?? 0) - s.Originals.Count;
            return GroupRotationPanelRules.KindSummary(markers, lines, shapes, images, locked > 0 ? locked : 0);
        }
    }

    /// <summary>회전 중심 위도 입력칸(십진 도). 넣으면(Enter · 칸 떠남) 중심을 옮긴다.</summary>
    public string GroupRotationCenterLat
    {
        get => _groupRotationCenterLat;
        set
        {
            _groupRotationCenterLat = value ?? "";
            if (!GroupRotationPanelRules.TryParseLatitude(value, out var lat, out var err)) { SetGroupRotationInputError(err); NotifyOfPropertyChange(); return; }
            if (_groupSelection?.Rotation is { } s) _groupSelection.SetRotationPivot(new PointLatLng(lat, s.Pivot.Lng));
        }
    }

    /// <summary>회전 중심 경도 입력칸(십진 도).</summary>
    public string GroupRotationCenterLon
    {
        get => _groupRotationCenterLon;
        set
        {
            _groupRotationCenterLon = value ?? "";
            if (!GroupRotationPanelRules.TryParseLongitude(value, out var lng, out var err)) { SetGroupRotationInputError(err); NotifyOfPropertyChange(); return; }
            if (_groupSelection?.Rotation is { } s) _groupSelection.SetRotationPivot(new PointLatLng(s.Pivot.Lat, lng));
        }
    }

    /// <summary>회전 중심 MGRS(지도 좌표 칩과 같은 글, 좌표 설정 MGRS 켜졌을 때 보임).</summary>
    public string? GroupRotationCenterMgrs => _groupRotationCenterMgrs;
    /// <summary>회전 중심 UTM(좌표 설정 UTM 켜졌을 때 보임).</summary>
    public string? GroupRotationCenterUtm => _groupRotationCenterUtm;

    /// <summary>각도 입력칸(°, 소수 1자리, −180~180). 넣으면 지도 미리보기가 그 각으로.</summary>
    public string GroupRotationAngleText
    {
        get => _groupRotationAngleText;
        set
        {
            _groupRotationAngleText = value ?? "";
            if (!GroupRotationPanelRules.TryParseAngle(value, out var deg, out var err)) { SetGroupRotationInputError(err); NotifyOfPropertyChange(); return; }
            _groupSelection?.PreviewRotation(deg);
        }
    }

    /// <summary>각도 슬라이더(−180~180, 0.1° 단위).</summary>
    public double GroupRotationAngle
    {
        get => _groupSelection?.Rotation?.Angle ?? 0d;
        set => _groupSelection?.PreviewRotation(System.Math.Round(value, 1, System.MidpointRounding.AwayFromZero));
    }

    /// <summary>입력 오류 글(형식 · 범위) — 없으면 null.</summary>
    public string? GroupRotationInputError => _groupRotationInputError;
    public bool HasGroupRotationInputError => _groupRotationInputError != null;

    /// <summary>확정 전 한 줄 확인 — 「n개를 (중심)에서 +15.0° 돌립니다」(확정 단추가 곧 확인).</summary>
    public string GroupRotationConfirmText
    {
        get
        {
            var s = _groupSelection?.Rotation;
            if (s == null) return "";
            var center = GroupRotationPanelRules.CenterText(s.Pivot.Lat, s.Pivot.Lng, _groupRotationCenterMgrs, _groupRotationCenterUtm,
                IsShowWSG84, IsShowMGRS, IsShowUTM);
            return GroupRotationPanelRules.ConfirmSentence(s.Originals.Count, center, s.Angle);
        }
    }

    /// <summary>「지도에서 고르기」 중인가(단추 강조).</summary>
    public bool IsGroupRotationPickingCenter => _groupSelection?.IsPickingPivot == true;

    private RelayCommand? _startGroupRotationCommand;
    /// <summary>편집 도구줄 「회전」 — 선택 2개 이상일 때 회전 세션을 열고 판을 띄운다.</summary>
    public RelayCommand StartGroupRotationCommand => _startGroupRotationCommand ??= new RelayCommand(
        _ => StartGroupRotation(), _ => IsEditModeEnabled && SelectedMarkers.Count >= 2);

    private RelayCommand? _pickGroupRotationCenterCommand;
    /// <summary>「지도에서 고르기」 — 다음 지도 클릭 자리를 중심으로(다시 누르면 그만).</summary>
    public RelayCommand PickGroupRotationCenterCommand => _pickGroupRotationCenterCommand ??= new RelayCommand(_ =>
    {
        if (_groupSelection == null || !_groupSelection.IsRotating) return;
        bool on = !_groupSelection.IsPickingPivot;
        _groupSelection.SetPivotPicking(on);
        if (on) SetAimStatus("지도를 눌러 회전 중심 고르기 — (ESC = 그만)");
    });

    private RelayCommand? _resetGroupRotationCenterCommand;
    /// <summary>「가운데로」 — 중심을 묶음 경계 상자 중심(기본값)으로.</summary>
    public RelayCommand ResetGroupRotationCenterCommand => _resetGroupRotationCenterCommand ??= new RelayCommand(_ => _groupSelection?.ResetRotationPivot());

    private RelayCommand? _stepGroupRotationCommand;
    /// <summary>±1° · ±0.1° 단추 — CommandParameter = 더할 각("1" · "-1" · "0.1" · "-0.1").</summary>
    public RelayCommand StepGroupRotationCommand => _stepGroupRotationCommand ??= new RelayCommand(p =>
    {
        var s = _groupSelection?.Rotation;
        if (s == null || !double.TryParse(p?.ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var step)) return;
        _groupSelection!.PreviewRotation(GroupRotationPanelRules.Step(s.Angle, step));
    });

    private RelayCommand? _confirmGroupRotationCommand;
    /// <summary>확정(저장) — Enter 와 같다.</summary>
    public RelayCommand ConfirmGroupRotationCommand => _confirmGroupRotationCommand ??= new RelayCommand(_ => _groupSelection?.EndRotation(commit: true));

    private RelayCommand? _cancelGroupRotationCommand;
    /// <summary>취소(원위치) · 판 닫기 — Esc 와 같다.</summary>
    public RelayCommand CancelGroupRotationCommand => _cancelGroupRotationCommand ??= new RelayCommand(_ =>
    {
        if (_groupSelection?.IsRotating != true) return;
        _groupSelection.EndRotation(commit: false);
        SetAimStatus("회전 취소", true);
    });

    private void StartGroupRotation()
    {
        if (_groupSelection == null || !IsEditModeEnabled) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (!_groupSelection.BeginRotation())
            SetAimStatus("돌릴 수 있는 항목이 없습니다(잠기지 않은 것이 있어야 합니다).", autoHide: true);
    }

    /// <summary>
    /// 열린 그룹 속성 판(2개 이상)의 칸을 지금 마커 값으로 다시 채운다(EnterGroupMode 재사용) — 판은 열릴 때 한 번만 채우므로
    /// 묶음 회전 확정 · Undo/Redo 뒤 회전(MarkerBearing) · 감시 방향(DetectionBearing) 칸이 낡지 않게.
    /// </summary>
    private void RefreshGroupPropertyPanelValues()
    {
        if (PropertyPanel is { IsGroupEditing: true, GroupMarkers: { Count: >= 2 } group } panel)
            panel.EnterGroupMode(group);
    }

    private void SetGroupRotationInputError(string? error)
    {
        _groupRotationInputError = error;
        NotifyOfPropertyChange(nameof(GroupRotationInputError));
        NotifyOfPropertyChange(nameof(HasGroupRotationInputError));
    }

    /// <summary>세션이 바뀜 → 입력칸을 세션 값으로 덮고(오류 지움) 판 전체 다시 알림.</summary>
    private void OnGroupRotationChanged()
    {
        var s = _groupSelection?.Rotation;
        if (s != null)
        {
            _groupRotationAngleText = GroupRotationPanelRules.FormatAngleInput(s.Angle);
            _groupRotationCenterLat = GroupRotationPanelRules.FormatCoordinateInput(s.Pivot.Lat);
            _groupRotationCenterLon = GroupRotationPanelRules.FormatCoordinateInput(s.Pivot.Lng);
            if (_groupRotationGridPivot != s.Pivot)
            {
                _groupRotationGridPivot = s.Pivot;
                try
                {
                    var c = new Coordinate(s.Pivot.Lat, s.Pivot.Lng);
                    _groupRotationCenterMgrs = c.MGRS.ToString();
                    _groupRotationCenterUtm = c.UTM.ToString();
                }
                catch { _groupRotationCenterMgrs = _groupRotationCenterUtm = null; }
            }
        }
        else _groupRotationGridPivot = null;
        _groupRotationInputError = null;
        bool picking = _groupSelection?.IsPickingPivot == true;
        if (_groupRotationWasPicking && !picking) IsAimStatusVisible = false;   // 고르기 끝(골랐거나 그만) → 안내 띠 내림
        _groupRotationWasPicking = picking;

        NotifyOfPropertyChange(nameof(IsGroupRotationPanelOpen));
        NotifyOfPropertyChange(nameof(GroupRotationSummary));
        NotifyOfPropertyChange(nameof(GroupRotationCenterLat));
        NotifyOfPropertyChange(nameof(GroupRotationCenterLon));
        NotifyOfPropertyChange(nameof(GroupRotationCenterMgrs));
        NotifyOfPropertyChange(nameof(GroupRotationCenterUtm));
        NotifyOfPropertyChange(nameof(GroupRotationAngleText));
        NotifyOfPropertyChange(nameof(GroupRotationAngle));
        NotifyOfPropertyChange(nameof(GroupRotationInputError));
        NotifyOfPropertyChange(nameof(HasGroupRotationInputError));
        NotifyOfPropertyChange(nameof(GroupRotationConfirmText));
        NotifyOfPropertyChange(nameof(IsGroupRotationPickingCenter));
    }
}
