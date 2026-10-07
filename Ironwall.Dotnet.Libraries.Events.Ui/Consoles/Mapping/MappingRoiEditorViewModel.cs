using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 프리셋 ROI 설정 창 — 클릭으로 점 · 선을 찍어 다각형 ROI 를 만든다
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 한 프리셋의 ROI(서버 <c>/presets/{id}/rois</c>)를 그리고 · 고치고 · 지우는 창.
/// </summary>
/// <remarks>
/// <para>프리셋 관리 창의 <b>[ROI 설정…]</b>으로 연다. 저장 · 삭제는 <b>바로 서버로</b> 간다(프리셋 관리 창과 같다).</para>
/// <para>그리기 판정(점 추가 · 닫기 · 지우기 · 옮기기)은 전부 여기 있다 — 캔버스(<see cref="MappingRoiCanvas"/>)는
/// 마우스 좌표를 0~1 로 바꿔 넘기기만 한다. 그래서 헤드리스로 시험한다.</para>
/// </remarks>
public sealed class MappingRoiEditorViewModel : Screen
{
    /// <summary>새 ROI 의 기준 해상도 기본값.</summary>
    public const double DEFAULT_WIDTH = 1920;

    /// <summary>새 ROI 의 기준 해상도 기본값.</summary>
    public const double DEFAULT_HEIGHT = 1080;

    /// <summary>이 넓이(정규화) 아래의 다각형은 한 줄로 늘어선 것으로 본다.</summary>
    private const double MIN_AREA = 0.0001;

    private readonly IMappingRoiEditor _editor;
    private readonly List<MappingRoiPoint> _points = new();
    private MappingRoiDetail? _selected;
    private bool _isNew;
    private bool _isClosed;
    private string _editName = string.Empty;
    private bool _editEnabled = true;
    private string _editWidth = string.Empty;
    private string _editHeight = string.Empty;
    private bool? _editExclusion;
    private bool _editPixelUnit;
    private string _message = string.Empty;
    private DialogMessageSeverity _messageSeverity = DialogMessageSeverity.Normal;
    private bool _isBusy;
    private bool _isConfirmingDelete;
    private MappingVideoResolution? _cameraResolution;
    private (string Width, string Height)? _autoSize;

    /// <summary>생성자.</summary>
    /// <param name="editor">서버 왕복.</param>
    /// <param name="presetId">프리셋 id.</param>
    /// <param name="presetTitle">프리셋 표시(창 제목) — 예: <c>3. 정문</c>.</param>
    /// <param name="cameraName">카메라 이름(창 제목).</param>
    /// <param name="canEdit">편집 권한. <c>false</c> 면 보기만 한다.</param>
    /// <param name="cameraVideo">카메라 실시간 영상 창구. 있으면 캔버스 바탕에 영상을 깐다. 생략하면 빈 격자.</param>
    /// <param name="cameraId">영상 · 프리셋 이동에 쓸 카메라 id.</param>
    /// <param name="presetIndex">이 프리셋의 번호(카메라 프리셋 토큰) — [프리셋 자리로 이동] 에 쓴다.</param>
    public MappingRoiEditorViewModel(IMappingRoiEditor editor, int presetId, string presetTitle, string cameraName, bool canEdit = true,
                                     IMappingCameraVideo? cameraVideo = null, int cameraId = 0, int presetIndex = 0)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        PresetId = presetId;
        PresetTitle = string.IsNullOrWhiteSpace(presetTitle) ? $"#{presetId}" : presetTitle;
        CameraName = cameraName ?? string.Empty;
        CanEdit = canEdit;
        DisplayName = Title;
        _cameraVideo = cameraVideo;
        CameraId = cameraId;
        PresetIndex = presetIndex;
        if (cameraVideo is not null && cameraId > 0)
            Video = new MappingCameraVideoViewModel(cameraVideo, cameraId, $"mapping-roi-{presetId}");
    }

    /// <summary>그린 모양(점 · 닫힘)이 바뀌었다 — 캔버스가 다시 그린다.</summary>
    public event EventHandler? ShapeChanged;

    #region - 상태 -
    /// <summary>프리셋 id.</summary>
    public int PresetId { get; }

    /// <summary>프리셋 표시.</summary>
    public string PresetTitle { get; }

    /// <summary>카메라 이름.</summary>
    public string CameraName { get; }

    /// <summary>창 제목.</summary>
    public string Title => string.IsNullOrWhiteSpace(CameraName) ? $"{PresetTitle} ROI 설정" : $"{CameraName} · {PresetTitle} ROI 설정";

    /// <summary>편집 권한.</summary>
    public bool CanEdit { get; }

    /// <summary>서버에 한 번이라도 쓰기가 성공했는가.</summary>
    public bool HasChanges { get; private set; }

    /// <summary>ROI 목록(id 순).</summary>
    public ObservableCollection<MappingRoiDetail> Rois { get; } = new();

    /// <summary>목록이 비었나.</summary>
    public bool IsEmpty => Rois.Count == 0;

    /// <summary>가장 최근 목록 조회 — 시험이 기다릴 때 쓴다.</summary>
    internal Task LoadTask { get; private set; } = Task.CompletedTask;

    /// <summary>고른 ROI. 고르면 폼과 캔버스에 그 값을 채운다(고치던 것은 버린다).</summary>
    public MappingRoiDetail? SelectedRoi
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            _selected = value;
            NotifyOfPropertyChange();
            IsConfirmingDelete = false;
            if (value is not null)
            {
                _isNew = false;
                FillForm(value.Name, value.IsEnable, value.ResolutionWidth, value.ResolutionHeight, value.IsExclusion, value.IsPixelUnit, value.Points, closed: true);
            }
            else if (!_isNew)
            {
                FillForm(string.Empty, true, BasisWidth, BasisHeight, null, false, Array.Empty<MappingRoiPoint>(), closed: false);
            }
            NotifyForm();
        }
    }

    /// <summary>새 ROI 를 그리는 중인가.</summary>
    public bool IsNew => _isNew;

    /// <summary>폼 · 캔버스를 쓸 수 있나(고른 것이 있거나 새로 그리는 중).</summary>
    public bool HasForm => _isNew || _selected is not null;

    /// <summary>폼 머리.</summary>
    public string FormTitle => _isNew ? "새 ROI" : _selected?.Name ?? string.Empty;

    /// <summary>이름.</summary>
    public string EditName
    {
        get => _editName;
        set { _editName = value ?? string.Empty; NotifyOfPropertyChange(); NotifyValidation(); }
    }

    /// <summary>사용 여부.</summary>
    public bool EditEnabled
    {
        get => _editEnabled;
        set { _editEnabled = value; NotifyOfPropertyChange(); NotifyValidation(); }
    }

    /// <summary>기준 해상도 가로.</summary>
    public string EditWidth
    {
        get => _editWidth;
        set { _editWidth = value ?? string.Empty; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanvasAspect)); NotifyValidation(); RaiseShape(); ResizeVideo(); }
    }

    /// <summary>기준 해상도 세로.</summary>
    public string EditHeight
    {
        get => _editHeight;
        set { _editHeight = value ?? string.Empty; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanvasAspect)); NotifyValidation(); RaiseShape(); ResizeVideo(); }
    }

    /// <summary>캔버스 가로/세로 비율 — 해상도가 틀렸으면 카메라 해상도(모르면 16:9).</summary>
    public double CanvasAspect
        => TryReadSize(out var w, out var h) ? w / h : BasisWidth / BasisHeight;

    /// <summary>카메라 메인 스트림 해상도(호스트가 ONVIF 프로파일에서 읽어 준다). 못 읽었으면 <c>null</c>.</summary>
    public MappingVideoResolution? CameraResolution => _cameraResolution;

    /// <summary>새 ROI 기본 해상도 가로 — 카메라 해상도, 모르면 <see cref="DEFAULT_WIDTH"/>.</summary>
    private double BasisWidth => _cameraResolution?.Width ?? DEFAULT_WIDTH;

    /// <summary>새 ROI 기본 해상도 세로 — 카메라 해상도, 모르면 <see cref="DEFAULT_HEIGHT"/>.</summary>
    private double BasisHeight => _cameraResolution?.Height ?? DEFAULT_HEIGHT;

    /// <summary>지금 그린 꼭짓점(정규화, 그린 순서).</summary>
    public IReadOnlyList<MappingRoiPoint> Points => _points;

    /// <summary>다각형을 닫았나.</summary>
    public bool IsClosed => _isClosed;

    /// <summary>점을 더 찍을 수 있나.</summary>
    public bool CanDraw => CanEdit && !IsBusy && HasForm && !_isClosed && _points.Count < MappingRoiGeometry.MAX_POINTS;

    /// <summary>꼭짓점을 끌어 옮길 수 있나(닫은 뒤).</summary>
    public bool CanMovePoints => CanEdit && !IsBusy && HasForm && _isClosed;

    /// <summary>다각형을 닫을 수 있나.</summary>
    public bool CanClosePolygon => CanEdit && !IsBusy && HasForm && !_isClosed && _points.Count >= MappingRoiGeometry.MIN_POINTS;

    /// <summary>[마지막 점 지우기] 를 누를 수 있나.</summary>
    public bool CanUndoPoint => CanEdit && !IsBusy && HasForm && _points.Count > 0;

    /// <summary>[다시 그리기] 를 누를 수 있나.</summary>
    public bool CanRedraw => CanEdit && !IsBusy && HasForm && _points.Count > 0;

    /// <summary>그리기 단계 한 줄(상태 알림).</summary>
    public string DrawStatus
    {
        get
        {
            if (!HasForm) return string.Empty;
            if (!CanEdit) return $"점 {_points.Count}개";
            if (_isClosed) return $"닫힌 다각형 · 점 {_points.Count}개";
            return _points.Count switch
            {
                0 => "첫 점을 찍으세요",
                < MappingRoiGeometry.MIN_POINTS => $"점 {_points.Count}개 — 계속 찍으세요",
                >= MappingRoiGeometry.MAX_POINTS => $"점 {_points.Count}개(최대) — 첫 점을 눌러 닫으세요",
                _ => $"점 {_points.Count}개 — 첫 점을 누르면 닫힙니다",
            };
        }
    }

    /// <summary>폼 값이 틀렸으면 그 사유 한 줄, 맞으면 빈 문자열.</summary>
    public string ValidationText => HasForm ? Validate(out _) ?? string.Empty : string.Empty;

    /// <summary>사유 줄을 보일까.</summary>
    public bool HasValidationText => ValidationText.Length > 0;

    /// <summary>버튼 줄 왼쪽 글(결과 · 실패).</summary>
    public string Message
    {
        get => _message;
        private set { _message = value; NotifyOfPropertyChange(); }
    }

    /// <summary>버튼 줄 왼쪽 글의 무게.</summary>
    public DialogMessageSeverity MessageSeverity
    {
        get => _messageSeverity;
        private set { _messageSeverity = value; NotifyOfPropertyChange(); }
    }

    /// <summary>서버와 주고받는 중.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; NotifyOfPropertyChange(); NotifyCommands(); }
    }

    /// <summary>삭제 확인 줄을 보이는 중.</summary>
    public bool IsConfirmingDelete
    {
        get => _isConfirmingDelete;
        private set { _isConfirmingDelete = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(DeleteConfirmText)); }
    }

    /// <summary>삭제 확인 줄 글.</summary>
    public string DeleteConfirmText => _selected is { } r ? $"'{r.Name}' ROI 를 지울까요?" : string.Empty;

    /// <summary>[새 ROI] 를 누를 수 있나.</summary>
    public bool CanBeginNew => CanEdit && !IsBusy;

    /// <summary>[저장] 을 누를 수 있나 — 값이 맞고, 기존 ROI 면 무엇이 바뀌었을 때만.</summary>
    public bool CanSave => CanEdit && !IsBusy && HasForm && Validate(out var draft) is null && (_isNew || IsChanged(draft!));

    /// <summary>[삭제] 를 누를 수 있나.</summary>
    public bool CanDelete => CanEdit && !IsBusy && !_isNew && _selected is not null;

    /// <summary>[저장] 버튼 글.</summary>
    public string SaveText => _isNew ? "만들기" : "저장";
    #endregion

    #region - 카메라 영상 -
    /// <summary>영상 요청 가로(px) — 세로는 기준 해상도 비율로 정한다.</summary>
    public const int VIDEO_WIDTH = 960;

    private readonly IMappingCameraVideo? _cameraVideo;
    private bool _isMovingCamera;

    /// <summary>카메라 id(영상 · 프리셋 이동). 0 이면 모른다.</summary>
    public int CameraId { get; }

    /// <summary>이 프리셋의 번호(카메라 프리셋 토큰). 0 이면 모른다.</summary>
    public int PresetIndex { get; }

    /// <summary>
    /// 캔버스 바탕 영상. 없으면(<c>null</c>) 빈 격자에 그린다.
    /// 영상은 기준 해상도 비율로 받는다 — 호스트가 카메라 비율을 그 안에 맞추므로, 기준 해상도가 카메라와 같으면 ROI 좌표가 영상과 꼭 맞는다.
    /// </summary>
    public MappingCameraVideoViewModel? Video { get; }

    /// <summary>영상 상자가 있나.</summary>
    public bool HasVideo => Video is not null;

    /// <summary>[프리셋 자리로 이동] 을 보일까 — PTZ 카메라이고 번호를 알 때.</summary>
    public bool HasGotoPreset => _cameraVideo is not null && CameraId > 0 && PresetIndex > 0 && _cameraVideo.CanGotoPreset(CameraId);

    /// <summary>[프리셋 자리로 이동] 을 누를 수 있나 — 카메라를 움직이므로 편집 권한이 있을 때만.</summary>
    public bool CanGotoPreset => HasGotoPreset && CanEdit && !_isMovingCamera;

    /// <summary>가장 최근 프리셋 이동 — 시험이 기다릴 때 쓴다.</summary>
    internal Task GotoPresetTask { get; private set; } = Task.CompletedTask;

    /// <summary>[프리셋 자리로 이동] — 카메라를 이 프리셋 자리로 돌려 영상이 ROI 를 그릴 화면이 되게 한다.</summary>
    public Task GotoPresetAsync() => GotoPresetTask = GotoPresetCoreAsync();

    private async Task GotoPresetCoreAsync()
    {
        if (!CanGotoPreset) return;
        _isMovingCamera = true;
        NotifyOfPropertyChange(nameof(CanGotoPreset));
        try
        {
            var result = await _cameraVideo!.GotoPresetAsync(CameraId, PresetIndex);
            if (result.IsSuccess) Say($"카메라를 {PresetTitle} 자리로 옮겼습니다.", DialogMessageSeverity.Info);
            else Say(result.Message, DialogMessageSeverity.Warning);
        }
        catch (Exception)
        {
            Say("프리셋 자리로 이동하지 못했습니다.", DialogMessageSeverity.Warning);
        }
        finally
        {
            _isMovingCamera = false;
            NotifyOfPropertyChange(nameof(CanGotoPreset));
        }
    }

    /// <summary>캔버스 비율 → 영상 요청 크기(순수). 가로 <see cref="VIDEO_WIDTH"/>, 세로는 비율대로(짝수 · 공유 메모리 한도 안).</summary>
    public static (int Width, int Height) VideoSize(double aspect)
    {
        if (!(aspect > 0) || double.IsInfinity(aspect)) aspect = DEFAULT_WIDTH / DEFAULT_HEIGHT;
        var height = (int)Math.Round(VIDEO_WIDTH / aspect);
        height = Math.Clamp(height, 16, 2160) & ~1;
        return (VIDEO_WIDTH, height);
    }

    /// <summary>영상을 연다(창이 처음 뜰 때).</summary>
    internal void StartVideo()
    {
        if (Video is null) return;
        _videoOn = true;
        var (w, h) = VideoSize(CanvasAspect);
        Video.Start(w, h);
    }

    private bool _videoOn;

    private void ResizeVideo()
    {
        if (Video is null || !_videoOn) return;
        var (w, h) = VideoSize(CanvasAspect);
        _ = Video.Resize(w, h);
    }

    /// <summary>가장 최근 카메라 해상도 읽기 — 시험이 기다릴 때 쓴다.</summary>
    internal Task ResolutionTask { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// 카메라 메인 스트림 해상도를 읽어 새 ROI 의 기준 해상도로 쓴다(창이 처음 뜰 때). GIS 는 카메라를 직접 부르지 않는다 — 호스트 경유.
    /// 못 읽으면 지금처럼(기존 ROI 해상도 → 1920×1080). 기존 ROI 의 읽은 값은 바꾸지 않는다.
    /// </summary>
    internal Task ReadCameraResolutionAsync() => ResolutionTask = ReadCameraResolutionCoreAsync();

    private async Task ReadCameraResolutionCoreAsync()
    {
        if (_cameraVideo is null || CameraId <= 0) return;
        try
        {
            var result = await _cameraVideo.ReadVideoResolutionAsync(CameraId);
            if (result.IsSuccess && result.Value is { Width: > 0 and <= 100000, Height: > 0 and <= 100000 } size)
                ApplyCameraResolution(size);
        }
        catch (Exception)
        {
            // 못 읽으면 기본 해상도 그대로 — 창은 흔들리지 않는다.
        }
    }

    /// <summary>
    /// 늦게 도착한 카메라 해상도를 반영한다 — 고른 것이 없으면 빈 캔버스 비율을, [새 ROI] 를 눌렀지만 아직 점이 없고
    /// 해상도 칸을 손대지 않았으면 그 칸을 카메라 해상도로 바꾼다. 고른 기존 ROI · 그리기 시작한 새 ROI 는 건드리지 않는다.
    /// </summary>
    private void ApplyCameraResolution(MappingVideoResolution size)
    {
        _cameraResolution = size;
        NotifyOfPropertyChange(nameof(CameraResolution));

        if (!HasForm)
        {
            _editWidth = Num(size.Width);
            _editHeight = Num(size.Height);
        }
        else if (_isNew && _points.Count == 0 && _autoSize is { } auto && auto.Width == _editWidth && auto.Height == _editHeight)
        {
            _editWidth = Num(size.Width);
            _editHeight = Num(size.Height);
            _autoSize = (_editWidth, _editHeight);
        }
        else
        {
            return;
        }
        NotifyOfPropertyChange(nameof(EditWidth));
        NotifyOfPropertyChange(nameof(EditHeight));
        NotifyOfPropertyChange(nameof(CanvasAspect));
        NotifyValidation();
        RaiseShape();
        ResizeVideo();
    }
    #endregion

    #region - 그리기 -
    /// <summary>
    /// 점을 하나 찍는다(0~1). 다각형이 닫혔거나 · 한도에 찼거나 · 앞 변을 가로지르면 찍지 않는다.
    /// </summary>
    /// <returns>찍었으면 <c>true</c>.</returns>
    public bool AddPoint(double x, double y)
    {
        if (!CanDraw) return false;
        var p = MappingRoiGeometry.Clamp(new MappingRoiPoint(x, y));
        if (_points.Count > 0 && _points[^1] == p) return false;    // 같은 자리 두 번(더블클릭의 첫 클릭)
        if (MappingRoiGeometry.WouldCross(_points, p))
        {
            Say("앞서 그린 선과 겹치는 자리에는 점을 찍을 수 없습니다.", DialogMessageSeverity.Warning);
            return false;
        }
        _points.Add(p);
        Say(string.Empty);
        ShapeEdited();
        return true;
    }

    /// <summary>다각형을 닫는다(점 3개 이상). 닫는 마지막 변이 다른 변을 가로지르면 닫지 않는다.</summary>
    /// <returns>닫았으면 <c>true</c>.</returns>
    public bool ClosePolygon()
    {
        if (!CanClosePolygon) return false;
        if (MappingRoiGeometry.IsSelfIntersecting(_points))
        {
            Say("닫으면 선이 서로 겹칩니다. 마지막 점을 지우고 다시 찍으세요.", DialogMessageSeverity.Warning);
            return false;
        }
        _isClosed = true;
        Say(string.Empty);
        ShapeEdited();
        return true;
    }

    /// <summary>[마지막 점 지우기] — 닫힌 다각형이면 먼저 연다.</summary>
    public void UndoPoint()
    {
        if (!CanUndoPoint) return;
        if (_isClosed) _isClosed = false;
        else _points.RemoveAt(_points.Count - 1);
        Say(string.Empty);
        ShapeEdited();
    }

    /// <summary>[다시 그리기] — 점을 모두 지운다(서버는 [저장] 전까지 그대로).</summary>
    public void Redraw()
    {
        if (!CanRedraw) return;
        _points.Clear();
        _isClosed = false;
        Say(string.Empty);
        ShapeEdited();
    }

    /// <summary>그리던 중인 열린 꺾은선을 버린다(Esc). 닫힌 다각형은 건드리지 않는다.</summary>
    public void CancelDrawing()
    {
        if (!CanEdit || !HasForm || _isClosed || _points.Count == 0) return;
        _points.Clear();
        ShapeEdited();
    }

    /// <summary>닫힌 다각형의 꼭짓점 하나를 옮긴다(끌기).</summary>
    public void MovePoint(int index, double x, double y)
    {
        if (!CanMovePoints || index < 0 || index >= _points.Count) return;
        var p = MappingRoiGeometry.Clamp(new MappingRoiPoint(x, y));
        if (_points[index] == p) return;
        _points[index] = p;
        ShapeEdited();
    }
    #endregion

    #region - 동작 -
    /// <inheritdoc/>
    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        LoadTask = LoadAsync();
        StartVideo();
        _ = ReadCameraResolutionAsync();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        if (close)
        {
            _videoOn = false;
            Video?.Dispose();   // 스트림 · 공유 메모리를 닫는다 — 창이 닫힌 뒤 호스트가 계속 디코딩하지 않게
        }
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    /// <summary>목록을 다시 받는다. <paramref name="selectId"/> 가 있으면 그 ROI 를 고른다.</summary>
    public async Task LoadAsync(int? selectId = null)
    {
        IsBusy = true;
        try
        {
            var result = await _editor.ListRoisAsync(PresetId);
            if (!result.IsSuccess)
            {
                // 못 불러온 것을 "없다" 로 말하지 않는다.
                Say(result.Message, DialogMessageSeverity.Critical);
                return;
            }

            var keep = selectId ?? _selected?.Id;
            Rois.Clear();
            foreach (var roi in (result.Value ?? Array.Empty<MappingRoiDetail>()).OrderBy(r => r.Id))
                Rois.Add(roi);
            NotifyOfPropertyChange(nameof(IsEmpty));

            _selected = null;
            if (!_isNew) SelectedRoi = keep is int id ? Rois.FirstOrDefault(r => r.Id == id) : null;
            else NotifyOfPropertyChange(nameof(SelectedRoi));
            RaiseShape();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// [새 ROI] — 빈 캔버스로 연다. 해상도는 카메라 실제 해상도(메인 스트림)를 따르고,
    /// 못 읽었으면 기존 ROI 것을(없으면 1920×1080).
    /// </summary>
    public void BeginNew()
    {
        if (!CanBeginNew) return;
        var basis = Rois.FirstOrDefault(r => r.ResolutionWidth > 0 && r.ResolutionHeight > 0);
        var (width, height) = _cameraResolution is { } camera
            ? ((double)camera.Width, (double)camera.Height)
            : (basis?.ResolutionWidth ?? DEFAULT_WIDTH, basis?.ResolutionHeight ?? DEFAULT_HEIGHT);
        _isNew = true;
        _selected = null;
        NotifyOfPropertyChange(nameof(SelectedRoi));
        IsConfirmingDelete = false;
        FillForm(NextName(), true, width, height, null, false, Array.Empty<MappingRoiPoint>(), closed: false);
        _autoSize = (_editWidth, _editHeight);
        Say(string.Empty);
        NotifyForm();
    }

    /// <summary>[저장] · [만들기] — 바로 서버로 보낸다.</summary>
    public async Task SaveAsync()
    {
        if (!CanSave || Validate(out var draft) is not null) return;

        IsBusy = true;
        try
        {
            var result = _isNew
                ? await _editor.CreateRoiAsync(PresetId, draft!)
                : await _editor.ReplaceRoiAsync(PresetId, _selected!.Id, draft!);
            if (!result.IsSuccess)
            {
                // 그린 것은 그대로 둔다 — 고쳐서 다시 누를 수 있게.
                Say(result.Message, DialogMessageSeverity.Critical);
                return;
            }

            HasChanges = true;
            var created = _isNew;
            _isNew = false;
            IsBusy = false;
            await LoadAsync(result.Value!.Id);
            Say(created ? $"'{result.Value.Name}' ROI 를 만들었습니다." : $"'{result.Value.Name}' ROI 를 저장했습니다.", DialogMessageSeverity.Info);
        }
        finally
        {
            IsBusy = false;
            NotifyForm();
        }
    }

    /// <summary>[삭제] — 같은 창 안에서 한 번 더 묻는다.</summary>
    public void RequestDelete()
    {
        if (!CanDelete) return;
        IsConfirmingDelete = true;
    }

    /// <summary>삭제 확인 줄의 [취소].</summary>
    public void CancelDelete() => IsConfirmingDelete = false;

    /// <summary>삭제 확인 줄의 [삭제].</summary>
    public async Task ConfirmDeleteAsync()
    {
        if (!IsConfirmingDelete || !CanDelete) return;
        var target = _selected!;
        IsConfirmingDelete = false;

        IsBusy = true;
        try
        {
            var result = await _editor.DeleteRoiAsync(PresetId, target.Id);
            if (!result.IsSuccess)
            {
                Say(result.Message, DialogMessageSeverity.Critical);
                return;
            }

            HasChanges = true;
            _selected = null;
            IsBusy = false;
            await LoadAsync();
            Say($"'{target.Name}' ROI 를 지웠습니다.", DialogMessageSeverity.Info);
        }
        finally
        {
            IsBusy = false;
            NotifyForm();
        }
    }

    /// <summary>[닫기] · ✕ · ESC.</summary>
    public Task CloseAsync() => TryCloseAsync(HasChanges);
    #endregion

    #region - 판정 -
    /// <summary>폼 · 그림을 읽는다. 틀렸으면 사유를 돌려주고 <paramref name="draft"/> 는 <c>null</c>.</summary>
    internal string? Validate(out MappingRoiDraft? draft)
    {
        draft = null;
        var name = _editName.Trim();
        if (name.Length == 0)
            return "이름을 적으세요.";
        if (name.Length > 100)
            return "이름은 100자까지 적을 수 있습니다.";
        if (!TryReadSize(out var width, out var height))
            return "해상도는 1 이상의 숫자로 적으세요.";
        if (_points.Count < MappingRoiGeometry.MIN_POINTS)
            return $"점을 {MappingRoiGeometry.MIN_POINTS}개 이상 찍으세요.";
        if (!_isClosed)
            return "첫 점을 눌러 다각형을 닫으세요.";
        if (MappingRoiGeometry.IsSelfIntersecting(_points))
            return "선이 서로 겹칩니다. 꼭짓점을 옮기세요.";
        if (MappingRoiGeometry.Area(_points) < MIN_AREA)
            return "넓이가 너무 작습니다. 점을 넓게 찍으세요.";

        draft = new MappingRoiDraft(name, width, height, _editEnabled, _editExclusion, _points.ToList(), _editPixelUnit);
        return null;
    }

    private bool TryReadSize(out double width, out double height)
    {
        var okW = double.TryParse(_editWidth.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out width) && width >= 1 && width <= 100000;
        var okH = double.TryParse(_editHeight.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out height) && height >= 1 && height <= 100000;
        return okW && okH;
    }

    private bool IsChanged(MappingRoiDraft draft)
        => _selected is not { } r
           || !string.Equals(r.Name, draft.Name, StringComparison.Ordinal)
           || r.IsEnable != draft.IsEnable
           || r.ResolutionWidth != draft.ResolutionWidth
           || r.ResolutionHeight != draft.ResolutionHeight
           || !r.Points.SequenceEqual(draft.Points);

    private string NextName()
    {
        for (int n = Rois.Count + 1; ; n++)
        {
            var name = $"ROI {n}";
            if (Rois.All(r => !string.Equals(r.Name, name, StringComparison.Ordinal))) return name;
        }
    }
    #endregion

    #region - 알림 -
    private void FillForm(string name, bool enabled, double width, double height, bool? exclusion, bool pixelUnit,
                          IReadOnlyList<MappingRoiPoint> points, bool closed)
    {
        _editName = name;
        _editEnabled = enabled;
        _editWidth = Num(width > 0 ? width : DEFAULT_WIDTH);
        _editHeight = Num(height > 0 ? height : DEFAULT_HEIGHT);
        _editExclusion = exclusion;
        _editPixelUnit = pixelUnit;
        _points.Clear();
        _points.AddRange(points);
        _isClosed = closed && _points.Count >= MappingRoiGeometry.MIN_POINTS;
    }

    private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private void Say(string text, DialogMessageSeverity severity = DialogMessageSeverity.Normal)
    {
        MessageSeverity = severity;
        Message = text;
    }

    private void ShapeEdited()
    {
        NotifyOfPropertyChange(nameof(Points));
        NotifyOfPropertyChange(nameof(IsClosed));
        NotifyOfPropertyChange(nameof(DrawStatus));
        NotifyValidation();
        RaiseShape();
    }

    private void RaiseShape() => ShapeChanged?.Invoke(this, EventArgs.Empty);

    private void NotifyForm()
    {
        NotifyOfPropertyChange(nameof(IsNew));
        NotifyOfPropertyChange(nameof(HasForm));
        NotifyOfPropertyChange(nameof(FormTitle));
        NotifyOfPropertyChange(nameof(SaveText));
        NotifyOfPropertyChange(nameof(EditName));
        NotifyOfPropertyChange(nameof(EditEnabled));
        NotifyOfPropertyChange(nameof(EditWidth));
        NotifyOfPropertyChange(nameof(EditHeight));
        NotifyOfPropertyChange(nameof(CanvasAspect));
        ShapeEdited();
        ResizeVideo();
    }

    private void NotifyValidation()
    {
        NotifyOfPropertyChange(nameof(ValidationText));
        NotifyOfPropertyChange(nameof(HasValidationText));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        NotifyOfPropertyChange(nameof(CanBeginNew));
        NotifyOfPropertyChange(nameof(CanSave));
        NotifyOfPropertyChange(nameof(CanDelete));
        NotifyOfPropertyChange(nameof(CanDraw));
        NotifyOfPropertyChange(nameof(CanMovePoints));
        NotifyOfPropertyChange(nameof(CanClosePolygon));
        NotifyOfPropertyChange(nameof(CanUndoPoint));
        NotifyOfPropertyChange(nameof(CanRedraw));
        NotifyOfPropertyChange(nameof(DrawStatus));
    }
    #endregion
}
