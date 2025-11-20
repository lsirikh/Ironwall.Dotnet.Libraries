# Device Provider Service - Product Requirements Document (PRD)

## Document Information
- **Title**: DeviceProviderService Implementation (API-Based Device Caching)
- **Version**: 1.0
- **Date**: 2025-01-21
- **Author**: GHLee
- **Status**: Draft
- **Related Documents**:
  - `Device_Panels_Api_Migration_PRD.md`
  - `GOP_Restful_Api_연동설계.md`
  - `CLAUDE.md` (Kent Beck's TDD)

---

## 1. Executive Summary

### 1.1 Overview
DeviceProviderService는 GOP RESTful API를 통해 초기 Device 데이터를 가져와서 Provider 형태로 캐시화하는 서비스입니다. 기존 `DeviceDbService.FetchInstanceAsync()`의 패턴을 따르되, Database 대신 RESTful API를 사용하는 방식으로 마이그레이션합니다.

### 1.2 Purpose
- GOP RESTful API를 통한 Device 데이터 초기 로딩
- Controller, Sensor, Camera 장비를 Provider에 캐싱
- 대용량 데이터 (Sensor 4000개 이상) 처리를 위한 페이징 로직
- Application 시작 시 Splash Screen과 함께 초기화

### 1.3 Business Value
- **Performance**: 로컬 Provider 캐시를 통한 빠른 데이터 접근
- **Scalability**: 페이징 처리로 대용량 센서 데이터 지원
- **Maintainability**: DB 의존성 제거, API 기반 아키텍처로 전환

---

## 2. Scope

### 2.1 In Scope
- ✅ `DeviceProviderService` 클래스 구현
- ✅ `IDeviceProviderService` 인터페이스 정의
- ✅ Controller 초기 로딩 (`FetchControllersAsync`)
- ✅ Sensor 초기 로딩 (`FetchSensorsAsync`) - 페이징 포함
- ✅ Camera 초기 로딩 (`FetchCamerasAsync`)
- ✅ Provider 캐시 구축 (`DeviceProvider`, `ControllerDeviceProvider`, `SensorDeviceProvider`, `CameraDeviceProvider`)
- ✅ Splash Screen 메시지 전송 (Caliburn.Micro IEventAggregator)
- ✅ 대용량 데이터 처리 로직 (4000개 이상 Sensor)
- ✅ 에러 핸들링 및 로깅

### 2.2 Out of Scope
- ❌ CRUD 작업 (이미 `DevicePanelViewModel`에서 처리)
- ❌ Real-time 데이터 동기화 (별도 Feature)
- ❌ Database 스키마 생성 (`BuildSchemeAsync`)
- ❌ Connection 관리 (`Connect`, `Disconnect`)

---

## 3. Technical Architecture

### 3.1 Component Diagram

```
┌─────────────────────────────────────────────────────────┐
│           Ironwall.Dotnet.Libraries.Devices.Ui          │
│                                                           │
│  ┌────────────────────────────────────────────────────┐ │
│  │         DeviceProviderService                      │ │
│  │  ┌──────────────────────────────────────────────┐ │ │
│  │  │  + StartService(token)                       │ │ │
│  │  │  + FetchAllDevicesAsync(token)               │ │ │
│  │  │  - FetchControllersAsync(token)              │ │ │
│  │  │  - FetchSensorsAsync(token)   ← Pagination   │ │ │
│  │  │  - FetchCamerasAsync(token)                  │ │ │
│  │  └──────────────────────────────────────────────┘ │ │
│  │                      │                             │ │
│  │                      ├──► IDeviceApiService        │ │
│  │                      │    (GOP RESTful API)        │ │
│  │                      │                             │ │
│  │                      ├──► DtoToModelHelper         │ │
│  │                      │    (DTO → Model Conversion) │ │
│  │                      │                             │ │
│  │                      └──► DeviceProviders          │ │
│  │                           (In-Memory Cache)        │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### 3.2 Data Flow

```
┌─────────────────┐
│ Application     │
│ Startup         │
└────────┬────────┘
         │
         │ 1. Call StartService()
         ▼
┌─────────────────────────────────────────────────┐
│ DeviceProviderService                           │
│                                                  │
│  2. FetchControllersAsync()                     │
│     ┌────────────────────────────────────────┐ │
│     │ Loop: page=1,2,3... (limit=100)        │ │
│     │  - GET /api/controllers?page=n         │ │
│     │  - Convert DTO → Model                 │ │
│     │  - Add to ControllerDeviceProvider     │ │
│     └────────────────────────────────────────┘ │
│                                                  │
│  3. FetchSensorsAsync()                         │
│     ┌────────────────────────────────────────┐ │
│     │ Loop: page=1,2,3... (limit=100)        │ │
│     │  - GET /api/sensors?page=n             │ │
│     │    &includeController=true             │ │
│     │  - Convert DTO → Model                 │ │
│     │  - Add to SensorDeviceProvider         │ │
│     │  - Build Controller Relations          │ │
│     └────────────────────────────────────────┘ │
│                                                  │
│  4. FetchCamerasAsync()                         │
│     ┌────────────────────────────────────────┐ │
│     │ Loop: page=1,2,3... (limit=100)        │ │
│     │  - GET /api/cameras?page=n             │ │
│     │  - Convert DTO → Model                 │ │
│     │  - Add to CameraDeviceProvider         │ │
│     └────────────────────────────────────────┘ │
└──────────────────┬──────────────────────────────┘
                   │
                   │ 5. Publish Splash Screen Messages
                   ▼
         ┌──────────────────┐
         │ IEventAggregator │
         │ (Caliburn.Micro) │
         └──────────────────┘
```

### 3.3 Navigation Mapping (양방향 참조)

**Critical Requirement**: Controller와 Sensor 간의 양방향 네비게이션 매핑을 구축해야 합니다.

```
┌─────────────────────────────────────────────────────────┐
│         Navigation Mapping (Bidirectional)              │
│                                                           │
│   ControllerDeviceModel                                  │
│   ├─ Id: 1                                               │
│   ├─ Devices: List<IBaseDeviceModel>  ◄──┐              │
│   │   ├─ SensorDeviceModel (Id: 101)     │              │
│   │   ├─ SensorDeviceModel (Id: 102)     │              │
│   │   └─ SensorDeviceModel (Id: 103)     │              │
│                                           │              │
│   SensorDeviceModel                       │              │
│   ├─ Id: 101                              │              │
│   └─ Controller: IControllerDeviceModel ──┘              │
│      (참조: Controller Id: 1)                            │
│                                                           │
└─────────────────────────────────────────────────────────┘
```

**Mapping 구축 로직**:
1. **Controller → Sensor (Parent → Children)**:
   - `ControllerDeviceModel.Devices` 리스트에 해당 Controller에 속한 모든 Sensor 추가
   - 형제 센서 간 공통 부모 참조 가능

2. **Sensor → Controller (Child → Parent)**:
   - `SensorDeviceModel.Controller` 속성에 부모 Controller 인스턴스 할당
   - Controller의 정보 (IpAddress, Port 등) 직접 접근 가능

**Reference Pattern (from DeviceDbService)**:
```csharp
// 1. Controller Dictionary 구축
var ctrlDict = controllers.ToDictionary(c => c.Id);

// 2. Sensor 순회하며 양방향 매핑
foreach (var sensor in sensors)
{
    if (ctrlDict.TryGetValue(sensor.ControllerId, out var parent))
    {
        // Sensor → Controller (Child → Parent)
        sensor.Controller = parent;

        // Controller → Sensor (Parent → Children)
        parent.Devices ??= new List<IBaseDeviceModel>();
        parent.Devices.Add(sensor);
    }
}
```

### 3.4 Key Dependencies

| Component | Type | Purpose |
|-----------|------|---------|
| `IDeviceApiService` | Interface | GOP RESTful API 호출 |
| `DtoToModelHelper` | Static Class | DTO ↔ Model 변환 |
| `DeviceProvider` | Provider | 전체 Device 캐시 (공통) |
| `ControllerDeviceProvider` | Provider | Controller 장비 캐시 |
| `SensorDeviceProvider` | Provider | Sensor 장비 캐시 |
| `CameraDeviceProvider` | Provider | Camera 장비 캐시 |
| `ILogService` | Interface | 로깅 서비스 |
| `IEventAggregator` | Interface | Splash Screen 메시지 전송 |

---

## 4. Detailed Requirements

### 4.1 Functional Requirements

#### FR-1: Service Initialization
- **ID**: FR-1
- **Priority**: P0 (Critical)
- **Description**: Application 시작 시 `StartService()`를 호출하여 모든 Device 데이터를 로딩합니다.
- **Acceptance Criteria**:
  - ✅ `StartService()` 메소드가 존재
  - ✅ 내부적으로 `FetchAllDevicesAsync()` 호출
  - ✅ Splash Screen 메시지 전송 (`SplashScreenMessage`)
  - ✅ 에러 발생 시 로깅 및 예외 전파

#### FR-2: Controller Devices Fetching
- **ID**: FR-2
- **Priority**: P0 (Critical)
- **Description**: GOP API를 통해 Controller 장비 목록을 가져와 Provider에 저장합니다.
- **Acceptance Criteria**:
  - ✅ `FetchControllersAsync()` 메소드 구현
  - ✅ 페이징 처리 (`page=1,2,3...`, `limit=100`)
  - ✅ `IDeviceApiService.GetControllersAsync()` 사용
  - ✅ `DtoToModelHelper.ToControllerDeviceModel()` 변환
  - ✅ `DeviceProvider` 및 `ControllerDeviceProvider`에 추가
  - ✅ Splash Screen 메시지: "ControllerProvider의 정보를 모두 불러왔습니다..."

**Reference Pattern (from DeviceDbService)**:
```csharp
var controllers = await FetchControllersAsync(token);
_deviceProvider.Clear();
_controllerProvider.Clear();
if (controllers?.Any() != false)
    foreach (var item in controllers.OfType<IControllerDeviceModel>())
    {
        _deviceProvider.Add(item);
    }
```

#### FR-3: Sensor Devices Fetching (Large-Scale Data Handling + Navigation Mapping)
- **ID**: FR-3
- **Priority**: P0 (Critical)
- **Description**: GOP API를 통해 Sensor 장비 목록을 가져와 Provider에 저장합니다. **4000개 이상의 대용량 데이터를 처리**하며, **Controller와의 양방향 네비게이션 매핑**을 구축해야 합니다.
- **Acceptance Criteria**:
  - ✅ `FetchSensorsAsync()` 메소드 구현
  - ✅ 페이징 처리 (`page=1,2,3...`, `limit=100`)
  - ✅ `includeController=true` 파라미터 사용 (Controller 정보 포함)
  - ✅ `IDeviceApiService.GetSensorsAsync()` 사용
  - ✅ `DtoToModelHelper.ToSensorDeviceModel()` 변환
  - ✅ **Navigation Mapping 구축 (양방향 참조)**:
    - ✅ `SensorDeviceModel.Controller` 할당 (Child → Parent)
    - ✅ `ControllerDeviceModel.Devices` 리스트에 Sensor 추가 (Parent → Children)
  - ✅ `DeviceProvider` 및 `SensorDeviceProvider`에 추가
  - ✅ Splash Screen 메시지: "SensorProvider의 정보를 모두 불러왔습니다..."
  - ✅ 진행률 표시 (선택사항): "Sensor 로딩 중... (1200/4500)"

**Pagination Logic with Navigation Mapping**:
```csharp
// ──────────────── Step 1: Fetch All Controllers First ────────────────
var controllers = await FetchControllersAsync(token);
var ctrlDict = controllers.ToDictionary(c => c.Id);

// ──────────────── Step 2: Fetch Sensors with Pagination ────────────────
var allSensors = new List<SensorDeviceModel>();
int currentPage = 1;
int pageSize = 100;
int totalFetched = 0;

while (true)
{
    var response = await _apiService.GetSensorsAsync(
        page: currentPage,
        limit: pageSize,
        includeController: true,
        token: token);

    if (!response.Success || response.Data == null || response.Data.Count == 0)
        break;

    foreach (var dto in response.Data)
    {
        var sensor = dto.ToSensorDeviceModel();

        // ──────────────── Step 3: Build Navigation Mapping ────────────────
        // Sensor → Controller (Child → Parent)
        if (dto.Controller != null && ctrlDict.TryGetValue(dto.Controller.Id, out var parent))
        {
            sensor.Controller = parent;

            // Controller → Sensor (Parent → Children)
            parent.Devices ??= new List<IBaseDeviceModel>();
            parent.Devices.Add(sensor);
        }

        allSensors.Add(sensor);
        totalFetched++;
    }

    // Optional: Splash Screen with progress
    await _eventAggregator.PublishOnUIThreadAsync(
        new SplashScreenMessage()
        {
            Title = this.GetType().Name,
            Message = $"Sensor 로딩 중... ({totalFetched} loaded)"
        });

    if (response.Data.Count < pageSize)
        break; // Last page

    currentPage++;
}

// ──────────────── Step 4: Add to Providers ────────────────
foreach (var sensor in allSensors)
{
    _deviceProvider.Add(sensor);
}
```

#### FR-4: Camera Devices Fetching
- **ID**: FR-4
- **Priority**: P0 (Critical)
- **Description**: GOP API를 통해 Camera 장비 목록을 가져와 Provider에 저장합니다.
- **Acceptance Criteria**:
  - ✅ `FetchCamerasAsync()` 메소드 구현
  - ✅ 페이징 처리 (`page=1,2,3...`, `limit=100`)
  - ✅ `IDeviceApiService.GetCamerasAsync()` 사용
  - ✅ `DtoToModelHelper.ToCameraDeviceModel()` 변환
  - ✅ `DeviceProvider` 및 `CameraDeviceProvider`에 추가
  - ✅ Splash Screen 메시지: "CameraProvider의 정보를 모두 불러왔습니다..."

#### FR-5: Fetch Order & Navigation Mapping Strategy
- **ID**: FR-5
- **Priority**: P0 (Critical)
- **Description**: **Navigation Mapping 구축을 위해 Controller를 먼저 로딩한 후 Sensor를 로딩**해야 합니다.
- **Acceptance Criteria**:
  - ✅ **Fetch Order 준수**:
    1. ✅ `FetchControllersAsync()` - Controller 먼저 로딩
    2. ✅ Build Controller Dictionary (`ctrlDict`)
    3. ✅ `FetchSensorsAsync()` - Sensor 로딩 + Navigation Mapping
    4. ✅ `FetchCamerasAsync()` - Camera 로딩 (독립적)
  - ✅ Controller Dictionary 구축 (`Dictionary<int, ControllerDeviceModel>`)
  - ✅ Sensor 로딩 시 Dictionary 조회하여 부모 Controller 매핑
  - ✅ 양방향 참조 구축:
    - `sensor.Controller = parent`
    - `parent.Devices.Add(sensor)`

**Critical Note**: Sensor를 먼저 로딩하면 Controller 참조를 찾을 수 없으므로 **반드시 Controller → Sensor 순서를 유지**해야 합니다.

#### FR-6: Provider Cache Management
- **ID**: FR-6
- **Priority**: P0 (Critical)
- **Description**: Provider를 Clear하고 새로운 데이터로 채웁니다.
- **Acceptance Criteria**:
  - ✅ 각 Device Type별 Provider Clear 호출
  - ✅ `DeviceProvider.Clear()` - 전체 Device 캐시
  - ✅ `ControllerDeviceProvider.Clear()` - Controller 캐시
  - ✅ `SensorDeviceProvider.Clear()` - Sensor 캐시
  - ✅ `CameraDeviceProvider.Clear()` - Camera 캐시
  - ✅ Provider에 추가 시 타입 체크 (`OfType<T>()`)

### 4.2 Non-Functional Requirements

#### NFR-1: Performance
- **ID**: NFR-1
- **Target**: Sensor 4000개 로딩 시간 < 10초 (네트워크 환경: 100Mbps)
- **Metrics**:
  - Controller 100개 로딩: < 2초
  - Sensor 4000개 로딩: < 8초 (페이징 처리)
  - Camera 50개 로딩: < 1초

#### NFR-2: Reliability
- **ID**: NFR-2
- **Target**: API 호출 실패 시 적절한 에러 핸들링
- **Requirements**:
  - ✅ GOP 서버 연결 실패 시 로깅
  - ✅ 페이징 중 에러 발생 시 이미 로드된 데이터는 유지
  - ✅ Timeout 설정 (30초)
  - ✅ Retry 로직 (선택사항, 3회 시도)

#### NFR-3: Scalability
- **ID**: NFR-3
- **Target**: Sensor 10,000개까지 처리 가능
- **Requirements**:
  - ✅ 페이징 크기 조정 가능 (`pageSize` 파라미터)
  - ✅ 메모리 효율적인 처리 (스트리밍 방식)
  - ✅ UI Freezing 방지 (Splash Screen 업데이트)

#### NFR-4: Maintainability
- **ID**: NFR-4
- **Target**: Code Duplication 최소화
- **Requirements**:
  - ✅ 공통 페이징 로직 추출 (`FetchPagedDevicesAsync<T>()`)
  - ✅ 에러 핸들링 공통화
  - ✅ Logging 표준화

---

## 5. Implementation Plan

### 5.1 Phase 1: Interface & Setup (STRUCTURAL)
**Goal**: `IDeviceProviderService` 인터페이스 정의 및 프로젝트 설정

**Tasks**:
- [ ] Create `Services/` folder in `Ironwall.Dotnet.Libraries.Devices.Ui`
- [ ] Define `IDeviceProviderService` interface
  - [ ] Method: `Task StartService(CancellationToken token = default)`
  - [ ] Method: `Task FetchAllDevicesAsync(CancellationToken token = default)`
- [ ] Create `DeviceProviderService.cs` skeleton
- [ ] Add constructor dependencies:
  - [ ] `ILogService log`
  - [ ] `IEventAggregator eventAggregator`
  - [ ] `IDeviceApiService apiService`
  - [ ] `DeviceProvider deviceProvider`
  - [ ] `ControllerDeviceProvider controllerProvider`
  - [ ] `SensorDeviceProvider sensorProvider`
  - [ ] `CameraDeviceProvider cameraProvider`

**Deliverables**:
- `IDeviceProviderService.cs`
- `DeviceProviderService.cs` (empty methods)

**Commit**: `STRUCTURAL: Add DeviceProviderService interface and skeleton`

---

### 5.2 Phase 2: Controller Fetching (BEHAVIORAL)
**Goal**: Controller 데이터 로딩 구현

**Tasks**:
- [ ] Implement `FetchControllersAsync()`
  - [ ] Pagination loop (`page=1,2,3...`)
  - [ ] Call `IDeviceApiService.GetControllersAsync()`
  - [ ] Convert DTO → Model using `DtoToModelHelper`
  - [ ] Add to `DeviceProvider` and `ControllerDeviceProvider`
  - [ ] Publish Splash Screen message
- [ ] Error handling
  - [ ] Log API errors
  - [ ] Return empty list on failure
- [ ] Update `FetchAllDevicesAsync()` to call `FetchControllersAsync()`

**Test Criteria**:
- ✅ Load 100 Controllers from GOP server
- ✅ Verify all Controllers are in Provider
- ✅ Splash Screen message appears

**Commit**: `BEHAVIORAL: Implement Controller devices fetching with pagination`

---

### 5.3 Phase 3: Sensor Fetching with Navigation Mapping (BEHAVIORAL - High Priority)
**Goal**: Sensor 데이터 로딩 구현 (대용량 처리 + 양방향 참조)

**Prerequisites**:
- ✅ Phase 2 완료 (Controller Dictionary 구축 필요)

**Tasks**:
- [ ] Update `FetchAllDevicesAsync()` to build Controller Dictionary
  - [ ] After `FetchControllersAsync()`, create `ctrlDict = controllers.ToDictionary(c => c.Id)`
  - [ ] Pass `ctrlDict` to `FetchSensorsAsync(ctrlDict, token)`
- [ ] Implement `FetchSensorsAsync(Dictionary<int, ControllerDeviceModel> ctrlDict, CancellationToken token)`
  - [ ] Pagination loop (`page=1,2,3...`, `limit=100`)
  - [ ] Call `IDeviceApiService.GetSensorsAsync(includeController: true)`
  - [ ] Convert DTO → Model using `DtoToModelHelper`
  - [ ] **Build Navigation Mapping** (Critical):
    - [ ] Check `dto.Controller != null`
    - [ ] Lookup parent: `ctrlDict.TryGetValue(dto.Controller.Id, out var parent)`
    - [ ] Assign: `sensor.Controller = parent` (Child → Parent)
    - [ ] Initialize: `parent.Devices ??= new List<IBaseDeviceModel>()`
    - [ ] Add: `parent.Devices.Add(sensor)` (Parent → Children)
  - [ ] Add to `DeviceProvider` and `SensorDeviceProvider`
  - [ ] Publish Splash Screen message with progress
- [ ] Handle large datasets (4000+ sensors)
  - [ ] Optimize memory usage (avoid redundant allocations)
  - [ ] Progress reporting (`"Sensor 로딩 중... ({totalFetched} loaded)"`)
- [ ] Error handling
  - [ ] Handle missing Controller gracefully (log warning)
  - [ ] Partial data recovery (keep already loaded sensors)
  - [ ] Log detailed pagination errors

**Test Criteria**:
- ✅ Load 4000+ Sensors from GOP server
- ✅ Verify all Sensors are in Provider
- ✅ **Verify Navigation Mapping**:
  - ✅ All Sensors have `Controller` reference (not null)
  - ✅ All Controllers have `Devices` list populated
  - ✅ Sensor count matches Controller's child count
- ✅ Splash Screen progress updates work
- ✅ Performance < 10 seconds (100Mbps network)

**Commit**: `BEHAVIORAL: Implement Sensor devices fetching with navigation mapping and large-scale pagination`

---

### 5.4 Phase 4: Camera Fetching (BEHAVIORAL)
**Goal**: Camera 데이터 로딩 구현

**Tasks**:
- [ ] Implement `FetchCamerasAsync()`
  - [ ] Pagination loop (`page=1,2,3...`)
  - [ ] Call `IDeviceApiService.GetCamerasAsync()`
  - [ ] Convert DTO → Model using `DtoToModelHelper`
  - [ ] Add to `DeviceProvider` and `CameraDeviceProvider`
  - [ ] Publish Splash Screen message
- [ ] Error handling
  - [ ] Log API errors
  - [ ] Return empty list on failure

**Test Criteria**:
- ✅ Load 50 Cameras from GOP server
- ✅ Verify all Cameras are in Provider
- ✅ Splash Screen message appears

**Commit**: `BEHAVIORAL: Implement Camera devices fetching with pagination`

---

### 5.5 Phase 5: Integration & Optimization (REFACTOR)
**Goal**: 코드 최적화 및 통합 테스트

**Tasks**:
- [ ] Extract common pagination logic
  - [ ] Create `FetchPagedDevicesAsync<T>()` helper method
  - [ ] Refactor Controller/Sensor/Camera methods to use helper
- [ ] Performance optimization
  - [ ] Adjust page size based on device type
  - [ ] Add timeout configuration
  - [ ] Implement retry logic (optional)
- [ ] Code review
  - [ ] Remove code duplication
  - [ ] Verify null safety
  - [ ] Check exception handling
- [ ] Integration testing
  - [ ] Test with real GOP server
  - [ ] Test with 4000+ sensors
  - [ ] Test network failure scenarios
  - [ ] Test timeout scenarios

**Test Criteria**:
- ✅ All unit tests passing
- ✅ No code duplication
- ✅ Performance targets met
- ✅ Error handling comprehensive

**Commit**: `REFACTOR: Optimize pagination logic and error handling`

---

## 6. API Specifications

### 6.1 IDeviceProviderService Interface

```csharp
namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services;

/// <summary>
/// Device Provider 초기화 서비스
/// <para>GOP RESTful API를 통해 Device 데이터를 가져와 Provider에 캐싱합니다.</para>
/// </summary>
public interface IDeviceProviderService : IService
{
    /// <summary>
    /// 서비스 시작 - Application 시작 시 호출
    /// </summary>
    Task StartService(CancellationToken token = default);

    /// <summary>
    /// 모든 Device 데이터를 GOP API로부터 가져와 Provider에 저장
    /// </summary>
    Task FetchAllDevicesAsync(CancellationToken token = default);
}
```

### 6.2 DeviceProviderService Class

```csharp
namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services;

/// <summary>
/// Device Provider 초기화 서비스 구현
/// <para>GOP RESTful API → Provider 캐싱</para>
/// </summary>
internal class DeviceProviderService : IDeviceProviderService
{
    #region - Ctors -
    public DeviceProviderService(
        ILogService log,
        IEventAggregator eventAggregator,
        IDeviceApiService apiService,
        DeviceProvider deviceProvider,
        ControllerDeviceProvider controllerProvider,
        SensorDeviceProvider sensorProvider,
        CameraDeviceProvider cameraProvider)
    {
        _log = log;
        _eventAggregator = eventAggregator;
        _apiService = apiService;
        _deviceProvider = deviceProvider;
        _controllerProvider = controllerProvider;
        _sensorProvider = sensorProvider;
        _cameraProvider = cameraProvider;
    }
    #endregion

    #region - Public Methods -
    public async Task StartService(CancellationToken token = default)
    {
        try
        {
            await FetchAllDevicesAsync(token);
        }
        catch (Exception ex)
        {
            _log?.Error($"DeviceProviderService.StartService failed: {ex.Message}");
            throw;
        }
    }

    public async Task FetchAllDevicesAsync(CancellationToken token = default)
    {
        try
        {
            // ──────────── 1. Controllers (먼저 로딩 - Navigation Mapping을 위해) ────────────
            var controllers = await FetchControllersAsync(token);
            _deviceProvider.Clear();
            _controllerProvider.Clear();
            if (controllers?.Any() == true)
                foreach (var item in controllers)
                    _deviceProvider.Add(item);

            await PublishSplashMessage("ControllerProvider의 정보를 모두 불러왔습니다...");

            // ──────────── 2. Build Controller Dictionary (양방향 참조를 위한 Dictionary) ────────────
            var ctrlDict = controllers.ToDictionary(c => c.Id);

            // ──────────── 3. Sensors (Navigation Mapping 구축) ────────────
            var sensors = await FetchSensorsAsync(ctrlDict, token);
            _sensorProvider.Clear();
            if (sensors?.Any() == true)
                foreach (var item in sensors)
                    _deviceProvider.Add(item);

            await PublishSplashMessage("SensorProvider의 정보를 모두 불러왔습니다...");

            // ──────────── 4. Cameras (독립적 로딩) ────────────
            var cameras = await FetchCamerasAsync(token);
            _cameraProvider.Clear();
            if (cameras?.Any() == true)
                foreach (var cam in cameras)
                    _deviceProvider.Add(cam);

            await PublishSplashMessage("CameraProvider의 정보를 모두 불러왔습니다...");
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchAllDevicesAsync failed: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - Private Methods -
    private async Task<List<ControllerDeviceModel>> FetchControllersAsync(
        CancellationToken token = default)
    {
        // Implementation with pagination
        // Returns list of Controllers (without Devices list populated yet)
    }

    private async Task<List<SensorDeviceModel>> FetchSensorsAsync(
        Dictionary<int, ControllerDeviceModel> ctrlDict,
        CancellationToken token = default)
    {
        // Implementation with pagination (large-scale)
        // Builds Navigation Mapping:
        //   - sensor.Controller = parent (Child → Parent)
        //   - parent.Devices.Add(sensor) (Parent → Children)
    }

    private async Task<List<CameraDeviceModel>> FetchCamerasAsync(
        CancellationToken token = default)
    {
        // Implementation with pagination
    }

    private async Task PublishSplashMessage(string message)
    {
        if (_eventAggregator != null)
            await _eventAggregator.PublishOnUIThreadAsync(
                new SplashScreenMessage()
                {
                    Title = this.GetType().Name,
                    Message = message
                });
    }
    #endregion

    #region - Attributes -
    private readonly ILogService _log;
    private readonly IEventAggregator _eventAggregator;
    private readonly IDeviceApiService _apiService;
    private readonly DeviceProvider _deviceProvider;
    private readonly ControllerDeviceProvider _controllerProvider;
    private readonly SensorDeviceProvider _sensorProvider;
    private readonly CameraDeviceProvider _cameraProvider;
    #endregion
}
```

---

## 7. Testing Strategy

### 7.1 Unit Testing
- **Framework**: xUnit 2.9.3
- **Test Coverage**: 80% minimum
- **Test Cases**:
  - ✅ `FetchControllersAsync_ShouldLoadAllControllers`
  - ✅ `FetchSensorsAsync_ShouldLoadAllSensorsWithPagination`
  - ✅ `FetchCamerasAsync_ShouldLoadAllCameras`
  - ✅ `FetchAllDevicesAsync_ShouldClearProvidersBeforeLoading`
  - ✅ `FetchSensorsAsync_ShouldHandleApiFailureGracefully`

### 7.2 Integration Testing
- **Environment**: GOP Server (Development)
- **Test Scenarios**:
  - ✅ Load 100 Controllers
  - ✅ Load 4500 Sensors (large-scale test)
  - ✅ Load 50 Cameras
  - ✅ GOP Server connection failure
  - ✅ Network timeout (30 seconds)
  - ✅ Partial data loading (pagination error in middle)

### 7.3 Performance Testing
- **Metrics**:
  - Controller 100개: < 2초
  - Sensor 4000개: < 10초
  - Camera 50개: < 1초
- **Tools**: Stopwatch, log4net

---

## 8. Error Handling

### 8.1 Error Scenarios

| Scenario | Handling | User Feedback |
|----------|----------|---------------|
| GOP Server 연결 실패 | Log error, throw exception | Splash Screen: "GOP 서버 연결 실패" |
| API Timeout | Log error, return empty list | Splash Screen: "API 응답 시간 초과" |
| 페이징 중 에러 | Log error, keep loaded data | Splash Screen: "일부 데이터 로딩 실패" |
| DTO → Model 변환 실패 | Log error, skip item | 로그에만 기록 |
| Provider Add 실패 | Log error, continue | 로그에만 기록 |

### 8.2 Logging Standards

```csharp
_log?.Info($"FetchControllersAsync 완료 - Count={controllers.Count}");
_log?.Error($"FetchSensorsAsync failed: {ex.Message}");
_log?.Warning($"Partial sensor loading: {totalFetched}/{expectedTotal}");
```

---

## 9. Migration Notes

### 9.1 Differences from DeviceDbService

| Feature | DeviceDbService (DB) | DeviceProviderService (API) |
|---------|----------------------|---------------------------|
| Data Source | MySQL/MariaDB | GOP RESTful API |
| Connection | `Connect()` / `Disconnect()` | HTTP Client (stateless) |
| Schema | `BuildSchemeAsync()` | Not needed |
| Transactions | `MySqlTransaction` | Not needed |
| Pagination | SQL `LIMIT` | API `page` & `limit` params |
| Relations | SQL `JOIN` | DTO nested objects |
| CRUD | Full CRUD in service | Only Read (CRUD in ViewModels) |

### 9.2 Removed Features
- ❌ `Connect()` / `Disconnect()` - No connection state
- ❌ `BuildSchemeAsync()` - No schema creation
- ❌ `InsertAsync()` / `UpdateAsync()` / `DeleteAsync()` - Handled in ViewModels
- ❌ Transaction support - API is stateless

### 9.3 New Features
- ✅ Pagination for large-scale data (4000+ sensors)
- ✅ Progress reporting (Splash Screen updates)
- ✅ DTO → Model conversion using `DtoToModelHelper`
- ✅ Retry logic (optional)

---

## 10. Dependencies

### 10.1 NuGet Packages
- `Caliburn.Micro` - IEventAggregator (Splash Screen)
- `log4net` - Logging
- (Already installed in project)

### 10.2 Project References
- `Ironwall.Dotnet.Libraries.Devices.Api` - IDeviceApiService
- `Ironwall.Dotnet.Libraries.Devices.Providers` - Providers
- `Ironwall.Dotnet.Libraries.Devices.Ui.Helpers` - DtoToModelHelper
- `Ironwall.Dotnet.Libraries.Base` - ILogService

---

## 11. Success Criteria

### 11.1 Functional Success
- ✅ All Controllers loaded into Provider
- ✅ All Sensors loaded into Provider (4000+ tested)
- ✅ All Cameras loaded into Provider
- ✅ Splash Screen messages appear correctly
- ✅ Provider cache is populated on startup

### 11.2 Performance Success
- ✅ Controller loading < 2 seconds
- ✅ Sensor loading < 10 seconds (4000 items)
- ✅ Camera loading < 1 second
- ✅ No UI freezing during loading

### 11.3 Quality Success
- ✅ Unit test coverage > 80%
- ✅ No code duplication
- ✅ All errors logged
- ✅ Kent Beck's TDD followed (Red-Green-Refactor)

---

## 12. Risks & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| GOP Server 성능 저하 | High | Medium | Timeout 설정, Retry 로직 |
| 4000+ Sensor 로딩 시간 초과 | High | Low | 페이징 크기 조정 (50 → 100) |
| 네트워크 불안정 | Medium | Medium | Partial data recovery, 재시도 |
| Memory overflow (대용량 데이터) | High | Low | 스트리밍 방식 처리, Dispose 패턴 |
| API Schema 변경 | High | Low | DtoToModelHelper에서 버전 체크 |

---

## 13. Timeline

| Phase | Duration | Start Date | End Date | Status |
|-------|----------|------------|----------|--------|
| Phase 1: Interface & Setup | 1 day | TBD | TBD | Not Started |
| Phase 2: Controller Fetching | 1 day | TBD | TBD | Not Started |
| Phase 3: Sensor Fetching | 2 days | TBD | TBD | Not Started |
| Phase 4: Camera Fetching | 1 day | TBD | TBD | Not Started |
| Phase 5: Integration & Optimization | 2 days | TBD | TBD | Not Started |
| **Total** | **7 days** | - | - | - |

---

## 14. References

### 14.1 Related Documents
- `Device_Panels_Api_Migration_PRD.md` - Panel ViewModel 마이그레이션
- `GOP_Restful_Api_연동설계.md` - API 스펙 문서
- `CLAUDE.md` - TDD 방법론 (Kent Beck)

### 14.2 Related Code
- `DeviceDbService.cs` (line 258-306) - `FetchInstanceAsync()` 참조 구현
- `IDeviceApiService.cs` - GOP API 메소드 정의
- `DtoToModelHelper.cs` - DTO → Model 변환 헬퍼

---

## 15. Approval

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Author | GHLee | 2025-01-21 | - |
| Reviewer | - | - | - |
| Approver | - | - | - |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-01-21 | GHLee | Initial PRD creation |
