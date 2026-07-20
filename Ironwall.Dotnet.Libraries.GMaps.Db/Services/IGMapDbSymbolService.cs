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

    /// <summary>잠금만 부분 UPDATE — 전체 행 재기록(UpdateSymbolAsync)이 Category 판별자를 런타임값으로
    /// 덮어 PidsGroup이 재부팅 소실되던 사고(2026-07-15 현장) 차단. 레이어패널 잠금 등 타입 무관 경로 전용.</summary>
    Task<bool> UpdateSymbolLockAsync(int id, bool isLocked, CancellationToken token = default);

    /// <summary>제목만 부분 UPDATE — 위와 동일 사유(레이어패널 이름변경 경로).</summary>
    Task<bool> UpdateSymbolTitleAsync(int id, string title, CancellationToken token = default);

    /// <summary>가시성(ShowShape)만 부분 UPDATE — 위와 동일 사유(레이어패널 심볼 Visibility 토글 경로).
    /// 전체 행 재기록(UpdateSymbolAsync)이 Category 판별자를 오염시키므로 이 부분 경로만 사용.</summary>
    Task<bool> UpdateSymbolShowShapeAsync(int id, bool showShape, CancellationToken token = default);

    /// <summary>Symbol의 ZOrder만 업데이트합니다</summary>
    Task UpdateSymbolZOrderAsync(int symbolId, int zOrder, CancellationToken token = default);

    /// <summary>여러 Symbol의 ZOrder를 Batch UPDATE합니다 (단일 SQL)</summary>
    Task BatchUpdateZOrderAsync(List<(int id, int zOrder)> changes, CancellationToken token = default);

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

    #region - PidsSymbol CRUD Operations -
    /// <summary>모든 PIDS 심볼을 조회합니다 (JOIN 쿼리)</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>PidsSymbol 목록</returns>
    /// <remarks>
    /// Symbols와 PidsSymbols 테이블을 조인하여 완전한 PIDS 심볼 정보를 반환합니다.
    /// PIDS_EQUIPMENT 카테고리의 Symbol만 조회합니다.
    /// </remarks>
    Task<List<IPidsSymbolModel>?> FetchPidsSymbolsAsync(CancellationToken token = default);

    /// <summary>ID로 단일 PIDS 심볼을 조회합니다</summary>
    /// <param name="id">Symbol ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>PidsSymbol 모델</returns>
    /// <remarks>
    /// Symbols와 PidsSymbols 테이블을 조인하여 완전한 PIDS 심볼 정보를 반환합니다.
    /// </remarks>
    Task<IPidsSymbolModel?> FetchPidsSymbolAsync(int id, CancellationToken token = default);

    /// <summary>LinkedDeviceId로 PIDS 심볼을 조회합니다</summary>
    /// <param name="deviceId">연결된 디바이스 ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>PidsSymbol 모델</returns>
    /// <remarks>
    /// 특정 디바이스에 연결된 PIDS 심볼을 조회합니다.
    /// 1:1 관계를 가정하므로 단일 결과만 반환합니다.
    /// </remarks>
    Task<IPidsSymbolModel?> FetchPidsSymbolByDeviceIdAsync(int deviceId, CancellationToken token = default);

    /// <summary>DeviceType별로 PIDS 심볼을 조회합니다</summary>
    /// <param name="deviceType">장비 타입 (Fence, IpCamera, PIR 등)</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>PidsSymbol 목록</returns>
    /// <remarks>
    /// 특정 장비 타입에 해당하는 모든 PIDS 심볼을 조회합니다.
    /// 장비별 관리 및 필터링에 사용됩니다.
    /// </remarks>
    Task<List<IPidsSymbolModel>?> FetchPidsSymbolsByDeviceTypeAsync(EnumDeviceType deviceType, CancellationToken token = default);

    /// <summary>EventStatus별로 PIDS 심볼을 조회합니다</summary>
    /// <param name="eventStatus">이벤트 상태 (Normal, Detecting, Fault, Connection)</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>PidsSymbol 목록</returns>
    /// <remarks>
    /// 특정 이벤트 상태에 해당하는 모든 PIDS 심볼을 조회합니다.
    /// 모니터링 및 알람 관리에 사용됩니다.
    /// </remarks>
    Task<List<IPidsSymbolModel>?> FetchPidsSymbolsByEventStatusAsync(EnumEventStatus eventStatus, CancellationToken token = default);

    /// <summary>새로운 PIDS 심볼을 삽입합니다 (트랜잭션 사용)</summary>
    /// <param name="model">PidsSymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>생성된 Symbol ID</returns>
    /// <remarks>
    /// 트랜잭션을 사용하여 Symbols와 PidsSymbols 테이블에 동시 삽입합니다.
    /// DetectionRange, DetectionAngle, DetectionBearing은 실시간 데이터이므로 저장하지 않습니다.
    /// 실패 시 자동으로 롤백됩니다.
    /// </remarks>
    Task<int> InsertPidsSymbolAsync(IPidsSymbolModel model, CancellationToken token = default);

    /// <summary>PIDS 심볼을 업데이트합니다 (트랜잭션 사용)</summary>
    /// <param name="model">PidsSymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>업데이트된 PidsSymbol 모델</returns>
    /// <remarks>
    /// 트랜잭션을 사용하여 Symbols와 PidsSymbols 테이블을 동시 업데이트합니다.
    /// DetectionRange, DetectionAngle, DetectionBearing은 실시간 데이터이므로 업데이트하지 않습니다.
    /// 실패 시 자동으로 롤백됩니다.
    /// </remarks>
    Task<IPidsSymbolModel?> UpdatePidsSymbolAsync(IPidsSymbolModel model, CancellationToken token = default);

    /// <summary>PIDS 심볼을 삭제합니다 (CASCADE 삭제)</summary>
    /// <param name="model">PidsSymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    /// <remarks>
    /// Symbols 테이블에서 삭제하면 PidsSymbols 테이블의 관련 레코드는 
    /// CASCADE 제약조건에 의해 자동으로 삭제됩니다.
    /// </remarks>
    Task<bool> DeletePidsSymbolAsync(IPidsSymbolModel model, CancellationToken token = default);

    /// <summary>LinkedDeviceId로 PIDS 심볼을 삭제합니다</summary>
    /// <param name="deviceId">연결된 디바이스 ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    /// <remarks>
    /// 특정 디바이스에 연결된 PIDS 심볼을 삭제합니다.
    /// 디바이스 해제 시 사용됩니다.
    /// </remarks>
    Task<bool> DeletePidsSymbolByDeviceIdAsync(int deviceId, CancellationToken token = default);
    #endregion

    #region - MilitarySymbol CRUD Operations -
    /// <summary>모든 군사 심볼을 조회합니다 (JOIN 쿼리)</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>MilitarySymbol 목록</returns>
    /// <remarks>
    /// Symbols와 MilitarySymbols 테이블을 조인하여 완전한 군사 심볼 정보를 반환합니다.
    /// MILITARY_SYMBOLS 카테고리의 Symbol만 조회합니다.
    /// </remarks>
    Task<List<IMilitarySymbolModel>?> FetchMilitarySymbolsAsync(CancellationToken token = default);

    /// <summary>ID로 단일 군사 심볼을 조회합니다</summary>
    /// <param name="id">Symbol ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>MilitarySymbol 모델</returns>
    /// <remarks>
    /// Symbols와 MilitarySymbols 테이블을 조인하여 완전한 군사 심볼 정보를 반환합니다.
    /// </remarks>
    Task<IMilitarySymbolModel?> FetchMilitarySymbolAsync(int id, CancellationToken token = default);

    /// <summary>새로운 군사 심볼을 삽입합니다 (트랜잭션 사용)</summary>
    /// <param name="model">MilitarySymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>생성된 Symbol ID</returns>
    /// <remarks>
    /// 트랜잭션을 사용하여 Symbols와 MilitarySymbols 테이블에 동시 삽입합니다.
    /// NATO APP-6D 표준에 따른 군사 심볼 속성을 모두 저장합니다.
    /// 실패 시 자동으로 롤백됩니다.
    /// </remarks>
    Task<int> InsertMilitarySymbolAsync(IMilitarySymbolModel model, CancellationToken token = default);

    /// <summary>군사 심볼을 업데이트합니다 (트랜잭션 사용)</summary>
    /// <param name="model">MilitarySymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>업데이트된 MilitarySymbol 모델</returns>
    /// <remarks>
    /// 트랜잭션을 사용하여 Symbols와 MilitarySymbols 테이블을 동시 업데이트합니다.
    /// 부대 이동, 소속 변경, 편제 개편 등의 상황을 반영합니다.
    /// 실패 시 자동으로 롤백됩니다.
    /// </remarks>
    Task<IMilitarySymbolModel?> UpdateMilitarySymbolAsync(IMilitarySymbolModel model, CancellationToken token = default);

    /// <summary>군사 심볼을 삭제합니다 (CASCADE 삭제)</summary>
    /// <param name="model">MilitarySymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    /// <remarks>
    /// Symbols 테이블에서 삭제하면 MilitarySymbols 테이블의 관련 레코드는 
    /// CASCADE 제약조건에 의해 자동으로 삭제됩니다.
    /// 부대 해체, 작전 종료 등의 상황에서 사용됩니다.
    /// </remarks>
    Task<bool> DeleteMilitarySymbolAsync(IMilitarySymbolModel model, CancellationToken token = default);
    #endregion

    #region - LineSymbol CRUD Operations -
    /// <summary>모든 라인 심볼을 조회합니다 (JOIN 쿼리 및 포인트 포함)</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>LineSymbol 목록</returns>
    /// <remarks>
    /// Symbols, LineSymbols, LinePoints 테이블을 조인하여 완전한 라인 심볼 정보를 반환합니다.
    /// AREA_BOUNDARY 카테고리의 Symbol만 조회합니다.
    /// 각 라인의 포인트들은 SequenceOrder 순서대로 정렬되어 반환됩니다.
    /// </remarks>
    Task<List<ILineSymbolModel>?> FetchLineSymbolsAsync(CancellationToken token = default);

    /// <summary>ID로 단일 라인 심볼을 조회합니다</summary>
    /// <param name="id">Symbol ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>LineSymbol 모델</returns>
    /// <remarks>
    /// Symbols와 LineSymbols 테이블을 조인하고, LinePoints를 별도 조회하여 
    /// 완전한 라인 심볼 정보를 반환합니다.
    /// 포인트들은 SequenceOrder에 따라 순서가 보장됩니다.
    /// </remarks>
    Task<ILineSymbolModel?> FetchLineSymbolAsync(int id, CancellationToken token = default);

    /// <summary>새로운 라인 심볼을 삽입합니다 (트랜잭션 사용)</summary>
    /// <param name="model">LineSymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>생성된 Symbol ID</returns>
    /// <remarks>
    /// 트랜잭션을 사용하여 Symbols, LineSymbols, LinePoints 테이블에 동시 삽입합니다.
    /// LinePoints는 제공된 순서대로 SequenceOrder가 부여되어 저장됩니다.
    /// 빈 포인트 리스트도 허용되며, 나중에 업데이트 가능합니다.
    /// 실패 시 자동으로 롤백됩니다.
    /// </remarks>
    Task<int> InsertLineSymbolAsync(ILineSymbolModel model, CancellationToken token = default);

    /// <summary>라인 심볼을 업데이트합니다 (트랜잭션 사용)</summary>
    /// <param name="model">LineSymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>업데이트된 LineSymbol 모델</returns>
    /// <remarks>
    /// 트랜잭션을 사용하여 Symbols, LineSymbols 테이블을 업데이트하고,
    /// LinePoints는 기존 데이터를 삭제 후 새로 삽입하는 방식으로 처리합니다.
    /// 포인트 순서 변경, 추가, 삭제가 모두 가능합니다.
    /// 실패 시 자동으로 롤백됩니다.
    /// </remarks>
    Task<ILineSymbolModel?> UpdateLineSymbolAsync(ILineSymbolModel model, CancellationToken token = default);

    /// <summary>라인 심볼을 삭제합니다 (CASCADE 삭제)</summary>
    /// <param name="model">LineSymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    /// <remarks>
    /// Symbols 테이블에서 삭제하면 LineSymbols와 LinePoints 테이블의 관련 레코드는 
    /// CASCADE 제약조건에 의해 자동으로 삭제됩니다.
    /// 경로 제거, 영역 해제 등의 상황에서 사용됩니다.
    /// </remarks>
    Task<bool> DeleteLineSymbolAsync(ILineSymbolModel model, CancellationToken token = default);
    #endregion

    #region - InfraSymbol CRUD Operations -

    /// <summary>모든 인프라 심볼을 조회합니다 (JOIN 쿼리)</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>InfraSymbol 목록</returns>
    /// <remarks>
    /// Symbols와 InfraSymbols 테이블을 조인하여 완전한 인프라 심볼 정보를 반환합니다.
    /// INFRASTRUCTURE 카테고리의 Symbol만 조회합니다.
    /// 건물 타입, 용도, 층수, 면적 정보를 포함합니다.
    /// </remarks>
    Task<List<IInfraSymbolModel>?> FetchInfraSymbolsAsync(CancellationToken token = default);

    /// <summary>ID로 단일 인프라 심볼을 조회합니다</summary>
    /// <param name="id">Symbol ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>InfraSymbol 모델</returns>
    /// <remarks>
    /// Symbols와 InfraSymbols 테이블을 조인하여 완전한 인프라 심볼 정보를 반환합니다.
    /// 건물 종류(BuildingType), 용도(BuildingUsage), 층수 등의 상세 정보를 포함합니다.
    /// </remarks>
    Task<IInfraSymbolModel?> FetchInfraSymbolAsync(int id, CancellationToken token = default);

    /// <summary>새로운 인프라 심볼을 삽입합니다 (트랜잭션 사용)</summary>
    /// <param name="model">InfraSymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>생성된 Symbol ID</returns>
    /// <remarks>
    /// 트랜잭션을 사용하여 Symbols와 InfraSymbols 테이블에 동시 삽입합니다.
    /// 건물 타입, 용도, 지상/지하 층수, 건물 면적 정보를 저장합니다.
    /// 실패 시 자동으로 롤백됩니다.
    /// </remarks>
    Task<int> InsertInfraSymbolAsync(IInfraSymbolModel model, CancellationToken token = default);

    /// <summary>인프라 심볼을 업데이트합니다 (트랜잭션 사용)</summary>
    /// <param name="model">InfraSymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>업데이트된 InfraSymbol 모델</returns>
    /// <remarks>
    /// 트랜잭션을 사용하여 Symbols와 InfraSymbols 테이블을 동시 업데이트합니다.
    /// 건물 리모델링, 용도 변경, 증축 등의 변경사항을 반영합니다.
    /// 실패 시 자동으로 롤백됩니다.
    /// </remarks>
    Task<IInfraSymbolModel?> UpdateInfraSymbolAsync(IInfraSymbolModel model, CancellationToken token = default);

    /// <summary>인프라 심볼을 삭제합니다 (CASCADE 삭제)</summary>
    /// <param name="model">InfraSymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    /// <remarks>
    /// Symbols 테이블에서 삭제하면 InfraSymbols 테이블의 관련 레코드는 
    /// CASCADE 제약조건에 의해 자동으로 삭제됩니다.
    /// 건물 철거, 시설 폐쇄 등의 상황에서 사용됩니다.
    /// </remarks>
    Task<bool> DeleteInfraSymbolAsync(IInfraSymbolModel model, CancellationToken token = default);

    #endregion

    #region - PidsGroupSymbol CRUD Operations -
    /// <summary>모든 PIDS 그룹 심볼을 조회합니다 (JOIN 쿼리 및 포인트 포함)</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>PidsGroupSymbol 목록</returns>
    /// <remarks>
    /// Symbols, PidsGroupSymbols, PidsGroupPoints 테이블을 조인하여 완전한 PIDS 그룹 심볼 정보를 반환합니다.
    /// PIDS_GROUP 카테고리의 Symbol만 조회합니다.
    /// 각 그룹의 경계선 포인트들은 SequenceOrder 순서대로 정렬되어 반환됩니다.
    /// </remarks>
    Task<List<IPidsGroupSymbolModel>?> FetchPidsGroupSymbolsAsync(CancellationToken token = default);

    /// <summary>ID로 단일 PIDS 그룹 심볼을 조회합니다</summary>
    /// <param name="id">Symbol ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>PidsGroupSymbol 모델</returns>
    /// <remarks>
    /// Symbols와 PidsGroupSymbols 테이블을 조인하고, PidsGroupPoints를 별도 조회하여 
    /// 완전한 PIDS 그룹 심볼 정보를 반환합니다.
    /// 경계선 포인트들은 SequenceOrder에 따라 순서가 보장됩니다.
    /// </remarks>
    Task<IPidsGroupSymbolModel?> FetchPidsGroupSymbolAsync(int id, CancellationToken token = default);

   
    /// <summary>새로운 PIDS 그룹 심볼을 삽입합니다 (트랜잭션 사용)</summary>
    /// <param name="model">PidsGroupSymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>생성된 Symbol ID</returns>
    /// <remarks>
    /// 트랜잭션을 사용하여 Symbols, PidsGroupSymbols, PidsGroupPoints 테이블에 동시 삽입합니다.
    /// LineSymbol 기능(LineOpacity, LinePattern 등)과 PIDS 그룹 속성(LinkedDeviceGroup, EventStatus)을 모두 저장합니다.
    /// 경계선 포인트들은 제공된 순서대로 SequenceOrder가 부여되어 저장됩니다.
    /// 실패 시 자동으로 롤백됩니다.
    /// </remarks>
    Task<int> InsertPidsGroupSymbolAsync(IPidsGroupSymbolModel model, CancellationToken token = default);

    /// <summary>PIDS 그룹 심볼을 업데이트합니다 (트랜잭션 사용)</summary>
    /// <param name="model">PidsGroupSymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>업데이트된 PidsGroupSymbol 모델</returns>
    /// <remarks>
    /// 트랜잭션을 사용하여 Symbols, PidsGroupSymbols 테이블을 업데이트하고,
    /// PidsGroupPoints는 기존 데이터를 삭제 후 새로 삽입하는 방식으로 처리합니다.
    /// 그룹 경계 변경, 이벤트 상태 변경, 포인트 추가/삭제가 모두 가능합니다.
    /// 실패 시 자동으로 롤백됩니다.
    /// </remarks>
    Task<IPidsGroupSymbolModel?> UpdatePidsGroupSymbolAsync(IPidsGroupSymbolModel model, CancellationToken token = default);

    /// <summary>PIDS 그룹 심볼을 삭제합니다 (CASCADE 삭제)</summary>
    /// <param name="model">PidsGroupSymbol 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    /// <remarks>
    /// Symbols 테이블에서 삭제하면 PidsGroupSymbols와 PidsGroupPoints 테이블의 관련 레코드는 
    /// CASCADE 제약조건에 의해 자동으로 삭제됩니다.
    /// 감시 구역 해제, 그룹 재구성 등의 상황에서 사용됩니다.
    /// </remarks>
    Task<bool> DeletePidsGroupSymbolAsync(IPidsGroupSymbolModel model, CancellationToken token = default);

    /// <summary>LinkedDeviceGroup으로 PIDS 그룹 심볼을 삭제합니다</summary>
    /// <param name="deviceGroup">연결된 디바이스 그룹 ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    /// <remarks>
    /// 특정 디바이스 그룹에 연결된 PIDS 그룹 심볼을 삭제합니다.
    /// 그룹 전체 해제 또는 재배치 시 사용됩니다.
    /// </remarks>
    Task<bool> DeletePidsGroupSymbolByDeviceGroupAsync(int deviceGroup, CancellationToken token = default);
    #endregion


    #region - Image CRUD Operations -
    /// <summary>모든 이미지를 조회합니다</summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Image 목록</returns>
    /// <remarks>
    /// Images 테이블에서 모든 이미지 오버레이 정보를 조회합니다.
    /// 경계 좌표(Left, Top, Right, Bottom), 표시 속성(Opacity, Visibility, Rotation) 등을 포함합니다.
    /// </remarks>
    Task<List<IImageModel>?> FetchImagesAsync(CancellationToken token = default);

    /// <summary>ID로 단일 이미지를 조회합니다</summary>
    /// <param name="id">Image ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>Image 모델</returns>
    /// <remarks>
    /// Images 테이블에서 특정 ID의 이미지 정보를 조회합니다.
    /// </remarks>
    Task<IImageModel?> FetchImageAsync(int id, CancellationToken token = default);

    /// <summary>새로운 이미지를 삽입합니다</summary>
    /// <param name="model">Image 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>생성된 Image ID</returns>
    /// <remarks>
    /// Images 테이블에 새 이미지 정보를 삽입합니다.
    /// 파일 경로, 경계 좌표, 표시 속성 등을 저장합니다.
    /// </remarks>
    Task<int> InsertImageAsync(IImageModel model, CancellationToken token = default);

    // ── 삭제 취소용 Id 보존 복원(Map_Edit_Undo_Redo FR-06). Id 점유 시 IdCollisionException → 호출부 폴백. ──
    Task<int> RestoreSymbolAsync(ISymbolModel model, CancellationToken token = default);
    Task<int> RestoreGeometrySymbolAsync(IGeometricSymbolModel model, CancellationToken token = default);
    Task<int> RestorePidsSymbolAsync(IPidsSymbolModel model, CancellationToken token = default);
    Task<int> RestoreMilitarySymbolAsync(IMilitarySymbolModel model, CancellationToken token = default);
    Task<int> RestoreLineSymbolAsync(ILineSymbolModel model, CancellationToken token = default);
    Task<int> RestoreInfraSymbolAsync(IInfraSymbolModel model, CancellationToken token = default);
    Task<int> RestorePidsGroupSymbolAsync(IPidsGroupSymbolModel model, CancellationToken token = default);
    Task<int> RestoreImageAsync(IImageModel model, CancellationToken token = default);

    /// <summary>이미지를 업데이트합니다</summary>
    /// <param name="model">Image 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>업데이트된 Image 모델</returns>
    /// <remarks>
    /// Images 테이블의 기존 이미지 정보를 업데이트합니다.
    /// 경계 좌표, 회전, 투명도 등의 속성 변경에 사용됩니다.
    /// </remarks>
    Task<IImageModel?> UpdateImageAsync(IImageModel model, CancellationToken token = default);

    /// <summary>이미지를 삭제합니다</summary>
    /// <param name="id">Image ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    /// <remarks>
    /// Images 테이블에서 특정 이미지를 삭제합니다.
    /// </remarks>
    Task<bool> DeleteImageAsync(int id, CancellationToken token = default);
    #endregion

    #region - Properties -
    /// <summary>데이터베이스 연결 상태</summary>
    /// <value>연결되어 있으면 true, 그렇지 않으면 false</value>
    bool IsConnected { get; }
    #endregion
}