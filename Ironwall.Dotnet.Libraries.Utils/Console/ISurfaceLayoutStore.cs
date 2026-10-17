namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/****************************************************************************
   Purpose      : SurfaceFrame 이 자리를 맡기는 저장소 계약 — 파일 형식은 모른다
   Created By   : Claude (D-09/D-10 라이브러리 승격)
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <see cref="SurfaceFrame"/> 이 자리를 기억 · 복원할 때 쓰는 저장소 계약.
///
/// <para><b>왜 인터페이스인가</b>: 실제 저장소(<c>SurfaceLayoutStore</c>, 파일 경로 · 스키마 버전 ·
/// 손상 복구를 아는 구체 클래스)는 앱마다 다른 폴더에 다른 파일로 쓴다 — 라이브러리가 그 경로를
/// 알 필요는 없고 알아서도 안 된다(라이브러리 → 호스트 역참조 금지). 호스트가 구현체를 만들어
/// <see cref="SurfaceFrame.Store"/> 에 얹는다.</para>
/// </summary>
public interface ISurfaceLayoutStore
{
    /// <summary>기억해 둔 자리 — 없거나 쓸 수 없으면 null.</summary>
    SurfaceBounds? TryGet(string surfaceKey);

    /// <summary>자리를 적어 둔다(아직 디스크에 쓰지는 않아도 된다).</summary>
    void Set(string surfaceKey, SurfaceBounds bounds);

    /// <summary>지금까지 적어 둔 것을 실제로 저장한다. 실패해도 예외를 올리지 않는다.</summary>
    bool Save();
}
