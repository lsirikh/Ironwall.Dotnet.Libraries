using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/****************************************************************************
   Purpose      : MapViewModel 심볼 클립보드 — Ctrl+C 복사 / Ctrl+V 붙여넣기(커서 위치·멀티 상대배치).
                  단일 진입 삭제(Delete)는 기존 ExecuteDeleteSelected 확인팝업 경로 재사용(별도 파일).
   Note         : 인메모리 버퍼(OS 클립보드 아님). 오버레이 이미지는 v1 제외(D-05). 붙여넣기=배치 Undo(BeginBatch).
                  DB I/O는 백그라운드, 마커/선택 변경은 UI 스레드(ConfigureAwait(false) 미사용으로 UI 컨텍스트 유지).
                  복제 코어 CreateSymbolCopyAsync는 Duplicate(오프셋)와 Paste(커서)가 공유 —
                  PIDS Id 유실(`= pidsSymbol`)·`LinkedDeviceId+1000` 실장비 충돌 버그를 여기서 근본 수정.
                  MapSymbol_Shortcut_CopyPasteDelete FR-01~03.
   Created On   : 2026-07-13 · Sensorway Co., Ltd.
****************************************************************************/
public partial class MapViewModel
{
    #region - 복사 버퍼 (FR-02) -
    /// <summary>복사 버퍼 — (딥클론 스냅샷, 원본 위경도). 붙여넣기 시 CloneModel()로 재클론(반복 붙여넣기 안전).</summary>
    private readonly List<(ISymbolSnapshot snapshot, PointLatLng sourcePos)> _copyBuffer = new();

    /// <summary>멀티 배치 앵커 = 복사 시 첫 항목의 위경도(D-04). 붙여넣기 시 커서와의 델타로 나머지 상대배치.</summary>
    private PointLatLng _copyAnchor;

    /// <summary>맵 단축키(Ctrl+C/V) 활성 컨텍스트 — 편집모드 ∧ 맵 가시. 윈도우 광역 후킹이 타 패널서 오작동하지 않게 격리(RISK-02).</summary>
    private bool IsMapShortcutContextActive() => IsEditModeEnabled && (MainMap?.IsVisible ?? false);
    #endregion

    #region - Ctrl+C 복사 (FR-02) -
    /// <summary>현재 선택(그룹 우선, 없으면 단일)을 복사 버퍼에 딥클론 스냅샷으로 저장. 읽기전용(RBAC 불요).
    /// 오버레이 이미지는 제외(v1 — D-05). 대상 식별은 인스턴스/타입 기반(Id-only 매칭 금지).</summary>
    public void CopySelectionToBuffer()
    {
        try
        {
            // 복사 대상 수집 — 그룹 선택 우선, 없으면 단일 선택.
            var sources = new List<IEditableMarker>();
            if (_groupSelection?.HasSelection ?? false)
                sources.AddRange(_groupSelection.Selection.Where(m => m != null && !m.IsDisposed));
            else if (SelectedMarker != null && !SelectedMarker.IsDisposed)
                sources.Add(SelectedMarker);

            _copyBuffer.Clear();
            foreach (var m in sources)
            {
                if (m is GMapImageMarker) continue;              // 오버레이 이미지 복사 제외(v1)
                var snap = SymbolSnapshot.Capture(m);            // 라이브 모델 딥클론
                if (snap == null || snap.IsImage) continue;      // 이미지/스냅샷 실패 제외
                _copyBuffer.Add((snap, m.Position));
            }

            if (_copyBuffer.Count == 0)
            {
                SetAimStatus("복사할 심볼이 없습니다(이미지는 제외).", autoHide: true);
                return;
            }
            _copyAnchor = _copyBuffer[0].sourcePos;   // 앵커 = 첫 항목(D-04)
            SetAimStatus($"{_copyBuffer.Count}개 복사됨 — Ctrl+V로 붙여넣기(마우스 위치)", autoHide: true);
            _log?.Info($"[복사] {_copyBuffer.Count}개 심볼 버퍼 저장(앵커 {_copyAnchor.Lat:F6},{_copyAnchor.Lng:F6})");
        }
        catch (Exception ex) { _log?.Error($"[복사] 실패: {ex.Message}"); }
    }
    #endregion

    #region - Ctrl+V 붙여넣기 (FR-03) -
    /// <summary>복사 버퍼를 현재 마우스 커서 위경도에 붙여넣기(멀티는 앵커 기준 상대 간격 유지). 편집권한 게이트.
    /// 배치 Undo(BeginBatch) 1매크로 + 트리 1회 리빌드(NFR-01) + 결과 자동선택. 반복 붙여넣기 가능(버퍼 미소거).</summary>
    public async void PasteFromBufferAsync()
    {
        try
        {
            if (_copyBuffer.Count == 0) { SetAimStatus("붙여넣을 항목이 없습니다.", autoHide: true); return; }
            if (!CanEditMap()) { _log?.Warning("[RBAC] 맵 편집 권한 없음 — 붙여넣기 차단"); ShowNoMapEditPermissionInfo(); return; }
            if (MainMap == null) return;

            // 기준점 = 현재 마우스 커서 위경도(맵 밖이면 뷰 중앙 폴백). 앵커→커서 델타를 전 항목에 평행이동.
            var cursor = MainMap.GetLastCursorLatLng();
            double dLat = cursor.Lat - _copyAnchor.Lat;
            double dLng = cursor.Lng - _copyAnchor.Lng;

            var pasted = new List<IEditableMarker>();
            using (_editRecorder?.BeginBatch($"붙여넣기 {_copyBuffer.Count}개"))   // 배치 Undo 1매크로
            {
                foreach (var (snapshot, sourcePos) in _copyBuffer)
                {
                    // 재클론 — 버퍼 원본 스냅샷 보존(반복 Ctrl+V 안전).
                    if (snapshot.CloneModel() is not ISymbolModel clone) continue;
                    var target = new PointLatLng(sourcePos.Lat + dLat, sourcePos.Lng + dLng);
                    var saved = await CreateSymbolCopyAsync(clone, sourcePos, target, appendCopySuffix: false);   // D-01: 붙여넣기 _Copy 미부가
                    if (saved == null) continue;
                    var marker = AddMarkerFromSymbol(saved, isExistingMarker: false, deferLayerSync: true);       // 트리 리빌드 지연(배치 후 1회)
                    if (marker != null) pasted.Add(marker);
                }
            }

            if (pasted.Count > 0)
            {
                _ = LoadLayersFromDbAsync();      // 트리 1회 리빌드(NFR-01)
                SelectPastedMarkers(pasted);      // 붙여넣은 항목 자동선택(단일/멀티)
                MainMap.InvalidateVisual();
                SetAimStatus($"{pasted.Count}개 붙여넣기 완료", autoHide: true);
                _log?.Info($"[붙여넣기] {pasted.Count}개 심볼 at ({cursor.Lat:F6},{cursor.Lng:F6})");
            }
            else SetAimStatus("붙여넣기 실패.", autoHide: true);
        }
        catch (Exception ex) { _log?.Error($"[붙여넣기] 실패: {ex.Message}"); }
    }

    /// <summary>붙여넣은 마커 자동선택 — 단일=편집선택(속성창), 멀티=그룹선택.</summary>
    private void SelectPastedMarkers(List<IEditableMarker> pasted)
    {
        if (pasted == null || pasted.Count == 0) return;
        if (pasted.Count == 1)
        {
            SelectMarkerForEditing(pasted[0]);
            return;
        }
        // 멀티 — 단일 adorner/패널 정리 후 그룹 선택으로 세팅(러버밴드와 동일 경로).
        MainMap?.DeselectAllMarkers();
        SelectedMarker = null;
        HidePropertyPanel();
        _groupSelection?.SetSelection(pasted);
        NotifyOfPropertyChange(nameof(SelectedMarkers));
        NotifyOfPropertyChange(nameof(HasSelectedItem));
    }
    #endregion

    #region - 복사 코어 (FR-01) — Duplicate/Paste 공유 -
    /// <summary>복사 코어 — 호출자 소유의 딥클론 모델을 targetPos로 재배치·미링크·삽입 후 DB fetch 결과 반환.
    /// Duplicate(오프셋)·Paste(커서)가 공유. clone은 호출자가 미리 딥클론해 넘긴다(원본/버퍼 오염 방지).</summary>
    /// <param name="clone">딥클론된 심볼 모델(이 메서드가 변형·삽입).</param>
    /// <param name="sourcePos">원본 위경도(LinePoints 평행이동 델타 계산용).</param>
    /// <param name="targetPos">새 위경도.</param>
    /// <param name="appendCopySuffix">true=제목에 "_Copy" 부가(복제 버튼). false=원본 제목 유지(붙여넣기 — D-01).</param>
    /// <returns>DB 삽입 후 새 Id로 fetch한 심볼 모델. 실패 시 null.</returns>
    private async Task<ISymbolModel?> CreateSymbolCopyAsync(ISymbolModel clone, PointLatLng sourcePos, PointLatLng targetPos,
                                                            bool appendCopySuffix, CancellationToken token = default)
    {
        if (clone == null) return null;
        try
        {
            // 1~2. 재배치 + 미링크 + 제목/Id 정책(DB 무관 순수 로직 — 단위테스트 대상).
            ApplyCopyTransform(clone, sourcePos, targetPos, appendCopySuffix);

            // 3. 타입별 Insert + Fetch (파생 우선순위: PidsGroup > Line > Geometric/Military/Infra/Pids > SymbolModel).
            //    반드시 Fetch 결과(새 Id 보유)를 반환 — PIDS의 `= pidsSymbol`(Id=0) 버그 계승 금지.
            switch (clone)
            {
                case PidsGroupSymbolModel m:
                    return await _gMapDbSymbolService.FetchPidsGroupSymbolAsync(
                        await _gMapDbSymbolService.InsertPidsGroupSymbolAsync(m, token), token);
                case LineSymbolModel m:
                    return await _gMapDbSymbolService.FetchLineSymbolAsync(
                        await _gMapDbSymbolService.InsertLineSymbolAsync(m, token), token);
                case GeometricSymbolModel m:
                    return await _gMapDbSymbolService.FetchGeometrySymbolAsync(
                        await _gMapDbSymbolService.InsertGeometrySymbolAsync(m, token), token);
                case MilitarySymbolModel m:
                    return await _gMapDbSymbolService.FetchMilitarySymbolAsync(
                        await _gMapDbSymbolService.InsertMilitarySymbolAsync(m, token), token);
                case InfraSymbolModel m:
                    return await _gMapDbSymbolService.FetchInfraSymbolAsync(
                        await _gMapDbSymbolService.InsertInfraSymbolAsync(m, token), token);
                case PidsSymbolModel m:
                    return await _gMapDbSymbolService.FetchPidsSymbolAsync(
                        await _gMapDbSymbolService.InsertPidsSymbolAsync(m, token), token);
                case SymbolModel m:
                    return await _gMapDbSymbolService.FetchSymbolAsync(
                        await _gMapDbSymbolService.InsertSymbolAsync(m, token), token);
                default:
                    _log?.Warning($"[복사] 지원되지 않는 심볼 타입: {clone.GetType().Name}");
                    return null;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[복사] 심볼 생성 실패({clone?.GetType().Name}): {ex.Message}");
            return null;
        }
    }

    /// <summary>복사 변형(순수·DB무관, 단위테스트 대상) — 딥클론 모델을 targetPos로 재배치 + LinePoints(Line/PidsGroup)
    /// 평행이동 + PIDS/PidsGroup 미링크(D-02, 실장비 오참조·`+1000` 버그 차단) + 제목 정책(D-01) + Id 리셋(DB 신규발급).</summary>
    internal static void ApplyCopyTransform(ISymbolModel clone, PointLatLng sourcePos, PointLatLng targetPos, bool appendCopySuffix)
    {
        if (clone == null) return;
        // 재배치 — 앵커 위경도 + LinePoints를 델타만큼 평행이동(간격 유지).
        double dLat = targetPos.Lat - sourcePos.Lat;
        double dLng = targetPos.Lng - sourcePos.Lng;
        clone.Latitude = targetPos.Lat;
        clone.Longitude = targetPos.Lng;
        if (clone is LineSymbolModel line && line.LinePoints != null)   // PidsGroup도 Line 상속 → 포함
        {
            for (int i = 0; i < line.LinePoints.Count; i++)
            {
                var p = line.LinePoints[i];
                line.LinePoints[i] = new GeoPoint(p.Latitude + dLat, p.Longitude + dLng, p.Altitude);
            }
        }
        // 미링크 + 제목 + Id 리셋.
        if (clone is PidsSymbolModel pids) pids.LinkedDeviceId = 0;
        if (clone is PidsGroupSymbolModel pg) pg.LinkedDeviceGroup = 0;
        if (appendCopySuffix) clone.Title = $"{clone.Title}_Copy";
        clone.Id = 0;   // 딥클론이 소스 Id를 물고 오므로 리셋 → DB AUTO_INCREMENT 신규 발급.
    }
    #endregion
}
