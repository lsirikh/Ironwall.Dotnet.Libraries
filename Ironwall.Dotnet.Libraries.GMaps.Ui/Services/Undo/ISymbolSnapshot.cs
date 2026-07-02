using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;

/****************************************************************************
   Purpose      : 심볼/이미지의 완전 상태 스냅샷 — 라이브 모델의 딥클론(추가/삭제 취소용 복원 페이로드).
   Note         : Map_Edit_Undo_Redo FR-02. **재-fetch 금지**(Infra Fetch가 BuildingType 하드코딩 →
                  손실). 반드시 라이브 모델을 딥클론(JSON)한다. Line/PidsGroup 점 리스트 포함.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public interface ISymbolSnapshot
{
    /// <summary>스냅샷 대상 심볼 Id(삭제 취소 시 이 Id로 복원 시도).</summary>
    int Id { get; }

    /// <summary>딥클론된 모델(ISymbolModel 하위 또는 IImageModel). 복원 시 이 객체를 사용.</summary>
    object Model { get; }

    /// <summary>모델 런타임 타입(복원 시 타입 분기용).</summary>
    Type ModelType { get; }

    /// <summary>원본 마커의 런타임 타입 전체이름(마커 재구성 분기용). 예: GMapPidsMarker.</summary>
    string MarkerTypeName { get; }

    /// <summary>이미지 마커(GMapImageMarker) 여부 — 복원 경로 분기.</summary>
    bool IsImage { get; }

    /// <summary>모델을 다시 딥클론해 반환(복원 시 원본 스냅샷 오염 방지).</summary>
    object CloneModel();
}
