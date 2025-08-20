using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using MySql.Data.MySqlClient;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Services;

/// <summary>
/// Symbol DB Service 인터페이스
/// </summary>
/// <remarks>
/// Symbol 및 GeometrySymbol 데이터의 데이터베이스 CRUD 작업을 정의합니다.
/// 비동기 패턴을 사용하며, CancellationToken을 지원합니다.
/// </remarks>
public interface IGMapDbSymbolService
{
    #region - Service Management -
    /// <summary>서비스를 시작합니다 (DB 연결, 스키마 생성, 데이터 로드)</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>시작 성공 여부</returns>
    Task<bool> StartService(CancellationToken token = default);

    /// <summary>서비스를 중지합니다 (DB 연결 해제)</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>중지 성공 여부</returns>
    Task<bool> StopService(CancellationToken token = default);

    /// <summary>데이터베이스에 연결합니다</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Task</returns>
    Task Connect(CancellationToken token = default);

    /// <summary>데이터베이스 연결을 해제합니다</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Task</returns>
    Task Disconnect(CancellationToken token = default);

    /// <summary>데이터베이스 스키마를 생성합니다</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Task</returns>
    Task BuildSchemeAsync(CancellationToken token = default);

    /// <summary>모든 Symbol 데이터를 로드하여 Provider에 저장합니다</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Task</returns>
    Task FetchInstanceAsync(CancellationToken token = default);

    /// <summary>새로운 MySQL 연결 인스턴스를 생성하고 엽니다</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>열린 MySqlConnection 인스턴스</returns>
    Task<MySqlConnection> OpenConnectionAsync(CancellationToken token = default);
    #endregion

    #region - Basic Symbol CRUD Operations -
    /// <summary>모든 Symbol을 조회합니다</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Symbol 목록</returns>
    Task<List<ISymbolModel>?> FetchSymbolsAsync(CancellationToken token = default);

    /// <summary>ID로 Symbol을 조회합니다</summary>
    /// <param name="id">Symbol ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>Symbol 모델</returns>
    Task<ISymbolModel?> FetchSymbolAsync(int id, CancellationToken token = default);

    /// <summary>Pid로 Symbol을 조회합니다</summary>
    /// <param name="pid">Symbol Pid</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>Symbol 모델</returns>
    Task<ISymbolModel?> FetchSymbolByPidAsync(int pid, CancellationToken token = default);

    /// <summary>카테고리별로 Symbol을 조회합니다</summary>
    /// <param name="category">카테고리</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>Symbol 목록</returns>
    Task<List<ISymbolModel>?> FetchSymbolsByCategoryAsync(EnumMarkerCategory category, CancellationToken token = default);

    /// <summary>새로운 Symbol을 삽입합니다</summary>
    /// <param name="model">Symbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>생성된 ID</returns>
    Task<int> InsertSymbolAsync(ISymbolModel model, CancellationToken token = default);

    /// <summary>Symbol을 업데이트합니다</summary>
    /// <param name="model">Symbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>업데이트된 Symbol 모델</returns>
    Task<ISymbolModel?> UpdateSymbolAsync(ISymbolModel model, CancellationToken token = default);

    /// <summary>Symbol을 삭제합니다</summary>
    /// <param name="model">Symbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    Task<bool> DeleteSymbolAsync(ISymbolModel model, CancellationToken token = default);

    /// <summary>Pid로 Symbol을 삭제합니다</summary>
    /// <param name="pid">Symbol Pid</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    Task<bool> DeleteSymbolByPidAsync(int pid, CancellationToken token = default);

    /// <summary>카테고리별로 Symbol을 일괄 삭제합니다</summary>
    /// <param name="category">카테고리</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제된 개수</returns>
    Task<int> DeleteSymbolsByCategoryAsync(EnumMarkerCategory category, CancellationToken token = default);
    #endregion

    #region - GeometrySymbol CRUD Operations -
    /// <summary>모든 기하 심볼을 조회합니다 (JOIN 쿼리)</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>GeometrySymbol 목록</returns>
    /// <remarks>
    /// Symbols와 GeometrySymbols 테이블을 조인하여 완전한 기하 심볼 정보를 반환합니다.
    /// BASIC_SHAPES 카테고리의 Symbol만 조회합니다.
    /// </remarks>
    Task<List<IGeometricSymbolModel>?> FetchGeometrySymbolsAsync(CancellationToken token = default);

    /// <summary>ID로 단일 기하 심볼을 조회합니다</summary>
    /// <param name="id">Symbol ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>GeometrySymbol 모델</returns>
    /// <remarks>
    /// Symbols와 GeometrySymbols 테이블을 조인하여 완전한 기하 심볼 정보를 반환합니다.
    /// </remarks>
    Task<IGeometricSymbolModel?> FetchGeometrySymbolAsync(int id, CancellationToken token = default);

    /// <summary>Shape 타입별로 기하 심볼을 조회합니다</summary>
    /// <param name="shapeType">기하학적 모양 타입</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>GeometrySymbol 목록</returns>
    /// <remarks>
    /// 특정 ShapeType(Circle, Square, Triangle 등)에 해당하는 기하 심볼을 조회합니다.
    /// </remarks>
    Task<List<IGeometricSymbolModel>?> FetchGeometrySymbolsByShapeTypeAsync(EnumShapeType shapeType, CancellationToken token = default);

    /// <summary>새로운 기하 심볼을 삽입합니다 (트랜잭션 사용)</summary>
    /// <param name="model">GeometrySymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>생성된 Symbol ID</returns>
    /// <remarks>
    /// 트랜잭션을 사용하여 Symbols와 GeometrySymbols 테이블에 동시 삽입합니다.
    /// 실패 시 자동으로 롤백됩니다.
    /// </remarks>
    Task<int> InsertGeometrySymbolAsync(IGeometricSymbolModel model, CancellationToken token = default);

    /// <summary>기하 심볼을 업데이트합니다 (트랜잭션 사용)</summary>
    /// <param name="model">GeometrySymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>업데이트된 GeometrySymbol 모델</returns>
    /// <remarks>
    /// 트랜잭션을 사용하여 Symbols와 GeometrySymbols 테이블을 동시 업데이트합니다.
    /// 실패 시 자동으로 롤백됩니다.
    /// </remarks>
    Task<IGeometricSymbolModel?> UpdateGeometrySymbolAsync(IGeometricSymbolModel model, CancellationToken token = default);

    /// <summary>기하 심볼을 삭제합니다 (CASCADE 삭제)</summary>
    /// <param name="model">GeometrySymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    /// <remarks>
    /// Symbols 테이블에서 삭제하면 GeometrySymbols 테이블의 관련 레코드는 
    /// CASCADE 제약조건에 의해 자동으로 삭제됩니다.
    /// </remarks>
    Task<bool> DeleteGeometrySymbolAsync(IGeometricSymbolModel model, CancellationToken token = default);
    #endregion

    #region - Properties -
    /// <summary>데이터베이스 연결 상태</summary>
    /// <value>연결되어 있으면 true, 그렇지 않으면 false</value>
    bool IsConnected { get; }
    #endregion
}