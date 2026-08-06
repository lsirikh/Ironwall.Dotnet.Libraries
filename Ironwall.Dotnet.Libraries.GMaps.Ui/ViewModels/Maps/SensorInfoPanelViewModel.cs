using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 등록 센서 정보 오버레이 VM (pidsgroup-rightclick FR-03/04/05/06)
   Created By   : GHLee
   Created On   : 2026-08-06
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 등록 센서 정보 오버레이의 DataContext. 단일 인스턴스 — <see cref="Load"/>로 그룹 컨텍스트 교체(재조회).
/// 열람 시점 스냅샷이며 구독/타이머 없음(닫기=Visibility 토글, PRD FR-03 생명주기 계약).
/// 데이터 조립은 MapViewModel이 담당(DeviceProvider 역필터) — 본 VM은 표시 상태만 보유.
/// </summary>
public class SensorInfoPanelViewModel : PropertyChangedBase
{
    public SensorInfoPanelViewModel()
    {
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());
        SensorHistoryCommand = new RelayCommand(p => { if (p is SensorInfoRowModel row) SensorHistoryRequested?.Invoke(row); });
        LocateCommand = new RelayCommand(p => { if (p is SensorInfoRowModel row) LocateRequested?.Invoke(row); });
    }

    #region - Events (MapViewModel 배선) -
    // Caliburn.Micro.Action과의 모호성 회피 — System.Action 한정 필수(기존 Events.Ui 교훈)
    public event System.Action? CloseRequested;
    public event System.Action<SensorInfoRowModel>? SensorHistoryRequested;
    public event System.Action<SensorInfoRowModel>? LocateRequested;
    #endregion

    #region - Commands -
    public RelayCommand CloseCommand { get; }
    public RelayCommand SensorHistoryCommand { get; }
    public RelayCommand LocateCommand { get; }
    #endregion

    #region - Properties -
    private int _groupId;
    private string _groupName = string.Empty;

    public int GroupId
    {
        get => _groupId;
        private set { _groupId = value; NotifyOfPropertyChange(nameof(GroupId)); }
    }

    public string GroupName
    {
        get => _groupName;
        private set { _groupName = value; NotifyOfPropertyChange(nameof(GroupName)); }
    }

    public ObservableCollection<SensorInfoRowModel> Rows { get; } = new();

    /// <summary>센서 수 = 필터 결과 Count (stale 가능한 DeviceGroupModel.DeviceCount 사용 금지 — FR-04).</summary>
    public int SensorCount => Rows.Count;
    public bool HasSensors => Rows.Count > 0;

    public int NormalCount   => Rows.Count(r => r.State is EnumCompositeEventStatus.Normal);
    public int DetectCount   => Rows.Count(r => r.State is EnumCompositeEventStatus.Detecting or EnumCompositeEventStatus.FaultedDetecting);
    public int FaultCount    => Rows.Count(r => r.State is EnumCompositeEventStatus.Faulted or EnumCompositeEventStatus.Connection);
    public int BlackoutCount => Rows.Count(r => r.State is EnumCompositeEventStatus.Blackout);
    #endregion

    /// <summary>그룹 컨텍스트 교체 + 스냅샷 재적재(재우클릭 = 본 메서드 재호출).</summary>
    public void Load(int groupId, string groupName, IEnumerable<SensorInfoRowModel> rows)
    {
        GroupId = groupId;
        GroupName = groupName;
        Rows.Clear();
        foreach (var row in rows) Rows.Add(row);
        NotifyOfPropertyChange(nameof(SensorCount));
        NotifyOfPropertyChange(nameof(HasSensors));
        NotifyOfPropertyChange(nameof(NormalCount));
        NotifyOfPropertyChange(nameof(DetectCount));
        NotifyOfPropertyChange(nameof(FaultCount));
        NotifyOfPropertyChange(nameof(BlackoutCount));
    }
}
