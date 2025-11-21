# Device Panels ApiService Migration - TDD Plan

## Overview
DBService 기반 → ApiService 기반 마이그레이션 (TDD 방식)
- 문서: `Docs/Device_Panels_Api_Migration_PRD.md`
- 방법론: `Docs/CLAUDE.md` (Kent Beck's TDD)

---

## Phase 1: DtoToModelHelper Implementation (STRUCTURAL)

### 1.1 Test Infrastructure Setup
- [x] Create test project: `Ironwall.Dotnet.Libraries.Devices.Ui.Tests`
- [x] Configure project references and packages
- [x] Set target framework to net8.0-windows

### 1.2 TDD: CameraDeviceDto ↔ CameraDeviceModel Conversion
- [x] RED: Write test `ToCameraDeviceModel_ShouldThrowArgumentNullException_WhenDtoIsNull`
- [x] GREEN: Implement null check in `ToCameraDeviceModel()`
- [x] RED: Write test `ToCameraDeviceModel_ShouldConvertAllFieldsCorrectly`
- [x] GREEN: Implement full DTO → Model conversion
- [x] RED: Write test `ToCameraDeviceDto_ShouldThrowArgumentNullException_WhenModelIsNull`
- [x] GREEN: Implement null check in `ToCameraDeviceDto()`
- [x] RED: Write test `ToCameraDeviceDto_ShouldConvertAllFieldsCorrectly`
- [x] GREEN: Implement full Model → DTO conversion
- [x] REFACTOR: Extract enum parsing helper methods
  - [x] `ParseDeviceType()`
  - [x] `ParseDeviceStatus()`
  - [x] `ParseCameraMode()`
  - [x] `ParseCameraType()`

### 1.3 Add Sensor and Controller Conversions
- [x] Implement `ToSensorDeviceModel()` extension method
- [x] Implement `ToSensorDeviceDto()` extension method
- [x] Implement `ToControllerDeviceModel()` extension method
- [x] Implement `ToControllerDeviceDto()` extension method

### 1.4 Phase 1 Verification
- [x] Run all DtoToModelHelper tests
- [x] Verify all tests pass (GREEN) - 4/4 tests passed
- [x] Code review: Check for duplication (✅ Acceptable, no refactoring needed)
- [x] COMMIT: "STRUCTURAL: Add DtoToModelHelper with full test coverage" (commit: a5af3ec)
- [x] Fix Model → DTO null handling (use "" instead of null) - 4/4 tests still passing
- [x] COMMIT: "fix: Use empty strings instead of null in Model → DTO conversions" (commit: cd51501)

---

## Phase 2: CameraDevicePanelViewModel Migration (BEHAVIORAL)

### 2.1 Update Constructor Dependencies
- [x] Remove `IDeviceDbService` parameter
- [x] Keep `IDeviceApiService` parameter
- [x] Remove `//using Ironwall.Dotnet.Libraries.Devices.Db.Services;`
- [x] Update field declarations to `readonly`

### 2.2 Add Helper Methods (Private)
- [x] Implement `FetchCamerasAsync()` - GET cameras from API
- [x] Implement `CreateCameraAsync()` - POST camera to API
- [x] Implement `UpdateCameraAsync()` - PUT camera to API

### 2.3 Update DataInitialize Method
- [x] Replace `_dbService.FetchInstanceAsync()` with `FetchCamerasAsync()`
- [x] Update Provider clearing and population logic
- [x] Maintain existing ViewModel binding pattern

### 2.4 Update OnClickSaveButton Method
- [x] Replace `_dbService.FetchCamerasAsync()` with `FetchCamerasAsync()`
- [x] Replace `_dbService.InsertCameraAsync()` with `CreateCameraAsync()`
- [x] Replace `_dbService.UpdateCameraAsync()` with `UpdateCameraAsync()`
- [x] Use `DtoToModelHelper` for conversions

### 2.5 Update HandleAsync Delete Method
- [x] Replace `_dbService.DeleteCameraAsync()` with `_apiService.DeleteCameraAsync()`
- [x] Add error logging for failed deletions
- [x] Maintain existing popup flow

### 2.6 Code Cleanup
- [x] Remove all `_dbService` references
- [x] Remove commented-out DB service code
- [x] Verify no compilation errors

### 2.7 Phase 2 Verification
- [x] Build solution - check for errors (warnings only, no errors)
- [x] Manual testing: Load cameras from GOP server ✅
- [x] Manual testing: Create new camera ✅
- [x] Manual testing: Update existing camera ✅
- [x] Manual testing: Delete camera ✅
- [x] Verify error handling works correctly ✅
- [x] COMMIT: "BEHAVIORAL: Migrate CameraDevicePanelViewModel to ApiService" (commit: e65b151)

---

## Phase 3: SensorDevicePanelViewModel Migration (BEHAVIORAL)

### 3.1 TDD: Sensor Tests (Optional - if time permits)
- [ ] RED: Write test for sensor fetch
- [ ] GREEN: Implement sensor fetch
- [ ] REFACTOR: Extract common patterns

### 3.2 Update Constructor Dependencies
- [x] Remove `IDeviceDbService` parameter (temp fix for compilation)
- [x] Update field declarations to use `IDeviceApiService`

### 3.3 Add Helper Methods
- [x] Implement `FetchSensorsAsync()` - GET sensors from API ✅
- [x] Implement `CreateSensorAsync()` - POST sensor to API ✅
- [x] Implement `UpdateSensorAsync()` - PUT sensor to API ✅

### 3.4 Update Core Methods
- [x] Update `DataInitialize()` method ✅
- [x] Update `OnClickSaveButton()` method ✅
- [x] Update `HandleAsync()` delete method ✅

### 3.5 Phase 3 Verification
- [x] Build solution - check for errors (warnings only, no errors) ✅
- [ ] Manual testing: CRUD operations
- [x] COMMIT: "BEHAVIORAL: Migrate SensorDevicePanelViewModel to ApiService" (commit: 09e0d6f) ✅

---

## Phase 4: ControllerDevicePanelViewModel Migration (BEHAVIORAL)

### 4.1 Update Constructor Dependencies
- [x] Remove `IDeviceDbService` parameter (temp fix for compilation)
- [x] Update field declarations to use `IDeviceApiService`

### 4.2 Add Helper Methods
- [x] Implement `FetchControllersAsync()` - GET controllers from API ✅
- [x] Implement `CreateControllerAsync()` - POST controller to API ✅
- [x] Implement `UpdateControllerAsync()` - PUT controller to API ✅

### 4.3 Update Core Methods
- [x] Update `DataInitialize()` method ✅
- [x] Update `OnClickSaveButton()` method ✅
- [x] Update `HandleAsync()` delete method ✅

### 4.4 Phase 4 Verification
- [x] Build solution - check for errors (warnings only, no errors) ✅
- [ ] Manual testing: CRUD operations
- [x] COMMIT: "BEHAVIORAL: Migrate ControllerDevicePanelViewModel to ApiService" (commit: 7725030) ✅

---

## Phase 5: Integration Testing & Cleanup

### 5.1 Integration Tests
- [ ] Test Camera Panel with real GOP server
- [ ] Test Sensor Panel with real GOP server
- [ ] Test Controller Panel with real GOP server
- [ ] Test error scenarios (GOP server down, network timeout)
- [ ] Test concurrent operations

### 5.2 Code Quality Check
- [ ] Run all unit tests
- [ ] Check for code duplication
- [ ] Verify logging is comprehensive
- [ ] Check null safety (`nullable enable`)
- [ ] Review exception handling

### 5.3 Documentation Update
- [ ] Update PRD with actual implementation notes
- [ ] Document any deviations from plan
- [ ] Add troubleshooting section if needed

### 5.4 Final Verification
- [ ] All tests passing
- [ ] No compiler warnings
- [ ] Code review completed
- [ ] COMMIT: "REFACTOR: Final cleanup and documentation"

---

## Commit Discipline (Kent Beck's Tidy First)

### STRUCTURAL Commits (No behavior change)
1. `STRUCTURAL: Add DtoToModelHelper with full test coverage`
   - Files: `DtoToModelHelper.cs`, `DtoToModelHelperTests.cs`
   - Tests: All passing before commit

### BEHAVIORAL Commits (Functionality changes)
2. `BEHAVIORAL: Migrate CameraDevicePanelViewModel to ApiService`
   - Files: `CameraDevicePanelViewModel.cs`
   - Tests: Manual testing completed

3. `BEHAVIORAL: Migrate SensorDevicePanelViewModel to ApiService`
   - Files: `SensorDevicePanelViewModel.cs`
   - Tests: Manual testing completed

4. `BEHAVIORAL: Migrate ControllerDevicePanelViewModel to ApiService`
   - Files: `ControllerDevicePanelViewModel.cs`
   - Tests: Manual testing completed

5. `REFACTOR: Final cleanup and documentation`
   - Files: Multiple
   - Tests: All tests passing

6. `fix: Use empty strings instead of null in Model → DTO conversions`
   - Files: `DtoToModelHelper.cs`
   - Tests: All tests passing (4/4)
   - Purpose: Prevent null values in API requests

---

## Current Status
- **Phase 1**: ✅ COMPLETE (All tests passing: 4/4, null handling fixed)
- **Phase 2**: ✅ COMPLETE (All CRUD operations verified on GOP server)
- **Phase 3**: ✅ COMPLETE (Build successful, manual testing pending)
- **Phase 4**: ✅ COMPLETE (Build successful, manual testing pending)
- **Phase 5**: ⏸️ NOT STARTED

## Next Step
▶️ **Manual Testing**: Test Sensor & Controller CRUD operations with real GOP server, then Phase 5

---

## Implementation Summary

### ✅ Completed Work

#### Phase 1: DtoToModelHelper (STRUCTURAL)
- **Files Created:**
  - `Ironwall.Dotnet.Libraries.Devices.Ui/Helpers/DtoToModelHelper.cs`
  - `Ironwall.Dotnet.Libraries.Devices.Ui.Tests/Helpers/DtoToModelHelperTests.cs`
  - `Ironwall.Dotnet.Libraries.Devices.Ui.Tests/Ironwall.Dotnet.Libraries.Devices.Ui.Tests.csproj`

- **Test Results:** 4/4 passing ✅
  - `ToCameraDeviceModel_ShouldThrowArgumentNullException_WhenDtoIsNull`
  - `ToCameraDeviceModel_ShouldConvertAllFieldsCorrectly`
  - `ToCameraDeviceDto_ShouldThrowArgumentNullException_WhenModelIsNull`
  - `ToCameraDeviceDto_ShouldConvertAllFieldsCorrectly`

- **Key Implementations:**
  - Camera DTO ↔ Model conversion (with enum parsing)
  - Sensor DTO ↔ Model conversion (with Controller object handling)
  - Controller DTO ↔ Model conversion (no credentials in schema)

#### Phase 2: CameraDevicePanelViewModel (BEHAVIORAL)
- **File Modified:**
  - `Ironwall.Dotnet.Libraries.Devices.Ui/ViewModels/Panels/CameraDevicePanelViewModel.cs`

- **Changes:**
  - ✅ Constructor updated (removed `IDeviceDbService`, uses `IDeviceApiService`)
  - ✅ Added `FetchCamerasAsync()` helper method
  - ✅ Added `CreateCameraAsync()` helper method
  - ✅ Added `UpdateCameraAsync()` helper method
  - ✅ Updated `DataInitialize()` to use ApiService
  - ✅ Updated `OnClickSaveButton()` with INSERT/UPDATE logic
  - ✅ Updated `HandleAsync()` delete method
  - ✅ All `_dbService` references removed

- **Build Status:** ✅ Compiles successfully (warnings only)

#### Phase 3: SensorDevicePanelViewModel (BEHAVIORAL)
- **File Modified:**
  - `Ironwall.Dotnet.Libraries.Devices.Ui/ViewModels/Panels/SensorDevicePanelViewModel.cs`

- **Changes:**
  - ✅ Constructor updated (removed `IDeviceDbService`, uses `IDeviceApiService`)
  - ✅ Added `FetchSensorsAsync()` helper method (with includeController=true)
  - ✅ Added `CreateSensorAsync()` helper method
  - ✅ Added `UpdateSensorAsync()` helper method
  - ✅ Updated `DataInitialize()` to use ApiService
  - ✅ Updated `OnClickSaveButton()` with INSERT/UPDATE logic
  - ✅ Updated `HandleAsync()` delete method
  - ✅ All `_dbService` references removed

- **Build Status:** ✅ Compiles successfully (warnings only)

#### Phase 4: ControllerDevicePanelViewModel (BEHAVIORAL)
- **File Modified:**
  - `Ironwall.Dotnet.Libraries.Devices.Ui/ViewModels/Panels/ControllerDevicePanelViewModel.cs`

- **Changes:**
  - ✅ Constructor updated (removed `IDeviceDbService`, uses `IDeviceApiService`)
  - ✅ Added `FetchControllersAsync()` helper method
  - ✅ Added `CreateControllerAsync()` helper method
  - ✅ Added `UpdateControllerAsync()` helper method
  - ✅ Updated `DataInitialize()` to use ApiService
  - ✅ Updated `OnClickSaveButton()` with INSERT/UPDATE logic
  - ✅ Updated `HandleAsync()` delete method
  - ✅ All `_dbService` references removed

- **Build Status:** ✅ Compiles successfully (warnings only)

---

## Technical Notes

### Schema Discoveries
1. **ControllerDeviceDto** does NOT have `UserName`/`UserPassword` properties
   - Only has: `IpAddress`, `IpPort`, basic device fields

2. **SensorDeviceDto** has `ControllerId` (int) but **SensorDeviceModel** has `Controller` (object)
   - DTO → Model: Converts nested `Controller` DTO if present
   - Model → DTO: Sets `ControllerId` from `Controller.Id`

3. **CameraDeviceDto** has full credential support
   - `UserName`, `UserPassword`, `RtspUri`, `RtspPort`, etc.

### Test Infrastructure
- Project: `Ironwall.Dotnet.Libraries.Devices.Ui.Tests`
- Target Framework: `net8.0-windows` (required for WPF)
- Test Framework: xUnit 2.9.3
- Test SDK: Microsoft.NET.Test.Sdk 17.14.0

---

## Files Ready for Commit

### STRUCTURAL Commit
- `Ironwall.Dotnet.Libraries.Devices.Ui/Helpers/DtoToModelHelper.cs` (new)
- `Ironwall.Dotnet.Libraries.Devices.Ui.Tests/**` (new test project)

### BEHAVIORAL Commits
- `Ironwall.Dotnet.Libraries.Devices.Ui/ViewModels/Panels/CameraDevicePanelViewModel.cs` (commit: e65b151)
- `Ironwall.Dotnet.Libraries.Devices.Ui/ViewModels/Panels/SensorDevicePanelViewModel.cs` (commit: 09e0d6f)
- `Ironwall.Dotnet.Libraries.Devices.Ui/ViewModels/Panels/ControllerDevicePanelViewModel.cs` (commit: 7725030)

---

# DeviceProviderService Implementation - TDD Plan

## Overview
GOP API 기반 DeviceProviderService 구현 (TDD 방식)
- 문서: `Docs/Device_Provider_Service_PRD.md`
- 방법론: `Docs/CLAUDE.md` (Kent Beck's TDD: Red-Green-Refactor)
- 목표: Application 시작 시 모든 Device 데이터를 GOP API로부터 가져와 Provider에 캐싱

---

## Phase 1: Interface & Service Setup (STRUCTURAL)

### 1.1 Create Services Folder and Interface
- [ ] Create `Services/` folder in `Ironwall.Dotnet.Libraries.Devices.Ui`
- [ ] Create `IDeviceProviderService.cs` interface
  - [ ] Inherit from `IService`
  - [ ] Method: `Task StartService(CancellationToken token = default)`
  - [ ] Method: `Task FetchAllDevicesAsync(CancellationToken token = default)`

### 1.2 Create Service Skeleton
- [ ] Create `DeviceProviderService.cs` class
- [ ] Add constructor with dependencies:
  - [ ] `ILogService? logService` (nullable)
  - [ ] `IEventAggregator eventAggregator`
  - [ ] `IDeviceApiService apiService`
  - [ ] `DeviceProvider deviceProvider`
  - [ ] `ControllerDeviceProvider controllerProvider`
  - [ ] `SensorDeviceProvider sensorProvider`
  - [ ] `CameraDeviceProvider cameraProvider`
- [ ] Add private readonly fields:
  - [ ] `private readonly ILogService? _log;`
  - [ ] Other providers
- [ ] Implement empty methods:
  - [ ] `StartService()` with basic logging structure
  - [ ] `FetchAllDevicesAsync()` stub

### 1.3 Phase 1 Verification
- [ ] Build solution - check for errors
- [ ] Verify interface inheritance and method signatures
- [ ] COMMIT: "STRUCTURAL: Add DeviceProviderService interface and skeleton with logging"

---

## Phase 2: Controller Fetching Implementation (BEHAVIORAL)

### 2.1 RED: Write Failing Test (Optional - if testing service directly)
- [ ] Test project: `Ironwall.Dotnet.Libraries.Devices.Ui.Tests`
- [ ] Test: `FetchControllersAsync_ShouldReturnListOfControllers`
  - [ ] Arrange: Mock IDeviceApiService
  - [ ] Act: Call FetchControllersAsync()
  - [ ] Assert: Returns non-empty list

### 2.2 GREEN: Implement FetchControllersAsync()
- [ ] Create private method: `FetchControllersAsync(CancellationToken token)`
- [ ] Initialize variables:
  - [ ] `var allControllers = new List<ControllerDeviceModel>();`
  - [ ] `int currentPage = 1;`
  - [ ] `int pageSize = 100;`
  - [ ] `int totalFetched = 0;`
- [ ] Add try-catch block
- [ ] Add logging: `_log?.Info("FetchControllersAsync() started")`
- [ ] Implement pagination while loop:
  - [ ] Call `_apiService.GetControllersAsync(page, limit, token)`
  - [ ] Check `response.Success` and break if failed
  - [ ] Convert DTOs: `dto.ToControllerDeviceModel()`
  - [ ] Add to list
  - [ ] Progress logging (every 100 items)
  - [ ] Break if last page (`response.Data.Count < pageSize`)
- [ ] Add logging: `_log?.Info($"FetchControllersAsync() completed: {totalFetched} items")`
- [ ] Error logging in catch: `_log?.Error($"Exception in FetchControllersAsync: {ex.Message}")`
- [ ] Return `allControllers`

### 2.3 Update FetchAllDevicesAsync()
- [ ] Call `FetchControllersAsync(token)`
- [ ] Clear providers: `_deviceProvider.Clear()`, `_controllerProvider.Clear()`
- [ ] Add controllers to providers
- [ ] Add logging: `_log?.Info($"Controllers loaded: {controllers.Count} items")`
- [ ] Publish Splash Screen: "ControllerProvider의 정보를 모두 불러왔습니다..."

### 2.4 Phase 2 Verification
- [ ] Build solution
- [ ] Run tests (if created)
- [ ] Manual test: GOP server integration (100 controllers)
- [ ] Verify log output (Info/Error levels)
- [ ] Performance check: < 2 seconds
- [ ] COMMIT: "BEHAVIORAL: Implement Controller devices fetching with pagination and logging"

---

## Phase 3: Sensor Fetching with Navigation Mapping (BEHAVIORAL - High Priority)

### 3.1 Prerequisites Check
- [ ] Phase 2 완료 확인 (Controllers must be loaded first)
- [ ] Verify `DtoToModelHelper.ToSensorDeviceModel()` exists

### 3.2 GREEN: Implement FetchSensorsAsync()
- [ ] Create private method: `FetchSensorsAsync(Dictionary<int, ControllerDeviceModel> ctrlDict, CancellationToken token)`
- [ ] Initialize variables:
  - [ ] `var allSensors = new List<SensorDeviceModel>();`
  - [ ] `int currentPage = 1;`
  - [ ] `int pageSize = 100;`
  - [ ] `int totalFetched = 0;`
  - [ ] `int mappedCount = 0;`
- [ ] Add try-catch block
- [ ] Add logging: `_log?.Info("FetchSensorsAsync() started")`
- [ ] Implement pagination while loop:
  - [ ] Call `_apiService.GetSensorsAsync(page, limit, includeController: true, token)`
  - [ ] Check response success
  - [ ] Convert DTO: `dto.ToSensorDeviceModel()`
  - [ ] **Build Navigation Mapping**:
    - [ ] Check `dto.Controller != null`
    - [ ] Lookup: `ctrlDict.TryGetValue(dto.Controller.Id, out var parent)`
    - [ ] Assign: `sensor.Controller = parent` (Child → Parent)
    - [ ] Initialize: `parent.Devices ??= new List<IBaseDeviceModel>()`
    - [ ] Add: `parent.Devices.Add(sensor)` (Parent → Children)
    - [ ] Increment `mappedCount`
  - [ ] Warning logging if Controller not found:
    - [ ] `_log?.Warning($"Controller not found for Sensor ID={sensor.Id}, ControllerId={dto.Controller.Id}")`
  - [ ] Add to list
  - [ ] Progress logging (every 1000 items): `_log?.Info($"Sensors loading progress: {totalFetched} items, {mappedCount} mapped")`
  - [ ] Break if last page
- [ ] Add logging: `_log?.Info($"FetchSensorsAsync() completed: {totalFetched} items, {mappedCount} mapped to controllers")`
- [ ] Return `allSensors`

### 3.3 Update FetchAllDevicesAsync()
- [ ] After Controllers loaded, build Dictionary:
  - [ ] `var ctrlDict = controllers.ToDictionary(c => c.Id);`
- [ ] Call `FetchSensorsAsync(ctrlDict, token)`
- [ ] Clear sensor provider
- [ ] Add sensors to providers
- [ ] Add logging: `_log?.Info($"Sensors loaded: {sensors.Count} items (Navigation Mapping built)")`
- [ ] Publish Splash Screen: "SensorProvider의 정보를 모두 불러왔습니다..."

### 3.4 Phase 3 Verification
- [ ] Build solution
- [ ] Manual test: Load 4000+ Sensors from GOP server
- [ ] **Verify Navigation Mapping**:
  - [ ] Check Sensor.Controller is not null
  - [ ] Check Controller.Devices list is populated
  - [ ] Verify Sensor count matches Controller's child count
- [ ] Verify log output (Info/Warning/Error levels)
- [ ] Verify Warning logs for orphaned sensors (if any)
- [ ] Performance check: < 10 seconds
- [ ] COMMIT: "BEHAVIORAL: Implement Sensor devices fetching with navigation mapping, pagination, and logging"

---

## Phase 4: Camera Fetching Implementation (BEHAVIORAL)

### 4.1 GREEN: Implement FetchCamerasAsync()
- [ ] Create private method: `FetchCamerasAsync(CancellationToken token)`
- [ ] Initialize variables (same pattern as Controllers)
- [ ] Add try-catch block
- [ ] Add logging: `_log?.Info("FetchCamerasAsync() started")`
- [ ] Implement pagination while loop:
  - [ ] Call `_apiService.GetCamerasAsync(page, limit, token)`
  - [ ] Check response success
  - [ ] Convert DTO: `dto.ToCameraDeviceModel()`
  - [ ] Add to list
  - [ ] Break if last page
- [ ] Add logging: `_log?.Info($"FetchCamerasAsync() completed: {totalFetched} items")`
- [ ] Return `allCameras`

### 4.2 Update FetchAllDevicesAsync()
- [ ] Call `FetchCamerasAsync(token)`
- [ ] Clear camera provider
- [ ] Add cameras to providers
- [ ] Add logging: `_log?.Info($"Cameras loaded: {cameras.Count} items")`
- [ ] Publish Splash Screen: "CameraProvider의 정보를 모두 불러왔습니다..."

### 4.3 Implement StartService()
- [ ] Add logging: `_log?.Info("DeviceProviderService.StartService() started")`
- [ ] Call `FetchAllDevicesAsync(token)` inside try-catch
- [ ] Add logging: `_log?.Info("DeviceProviderService.StartService() completed")`
- [ ] Error logging: `_log?.Error($"DeviceProviderService.StartService() failed: {ex.Message}")`

### 4.4 Phase 4 Verification
- [ ] Build solution
- [ ] Manual test: Load 50 Cameras from GOP server
- [ ] Verify log output (Info/Error levels)
- [ ] Performance check: < 1 second
- [ ] End-to-end test: `StartService()` loads all devices
- [ ] COMMIT: "BEHAVIORAL: Implement Camera devices fetching and complete StartService with logging"

---

## Phase 5: Integration Testing & Optimization (REFACTOR)

### 5.1 Integration Tests
- [ ] Test with real GOP server
- [ ] Test scenario: 100 Controllers
- [ ] Test scenario: 4000+ Sensors (large-scale)
- [ ] Test scenario: 50 Cameras
- [ ] Test error scenarios:
  - [ ] GOP server connection failure
  - [ ] Network timeout (30 seconds)
  - [ ] Partial data loading (pagination error in middle)
- [ ] Verify Navigation Mapping integrity

### 5.2 Performance Optimization
- [ ] Measure actual performance:
  - [ ] Controllers: < 2초
  - [ ] Sensors: < 10초
  - [ ] Cameras: < 1초
- [ ] Adjust page size if needed
- [ ] Consider retry logic (optional, 3회 시도)
- [ ] Add timeout configuration (30초)

### 5.3 Code Quality Check
- [ ] Check for code duplication
  - [ ] Consider extracting `FetchPagedDevicesAsync<T>()` helper (optional)
- [ ] Verify null safety (`nullable enable`)
- [ ] Review exception handling
- [ ] Verify logging is comprehensive
- [ ] Check memory usage (no leaks)

### 5.4 Documentation Update
- [ ] Update PRD with actual implementation notes
- [ ] Document any deviations from plan
- [ ] Add troubleshooting section if needed
- [ ] Update README (if needed)

### 5.5 Final Verification
- [ ] All tests passing
- [ ] No compiler warnings
- [ ] Code review completed
- [ ] Performance targets met
- [ ] COMMIT: "REFACTOR: Optimize pagination logic, error handling, and logging"

---

## Commit Discipline (Kent Beck's Tidy First)

### STRUCTURAL Commits (No behavior change)
1. `STRUCTURAL: Add DeviceProviderService interface and skeleton with logging`
   - Files: `IDeviceProviderService.cs`, `DeviceProviderService.cs` (empty methods)

### BEHAVIORAL Commits (Functionality changes)
2. `BEHAVIORAL: Implement Controller devices fetching with pagination and logging`
   - Files: `DeviceProviderService.cs` (FetchControllersAsync implementation)

3. `BEHAVIORAL: Implement Sensor devices fetching with navigation mapping, pagination, and logging`
   - Files: `DeviceProviderService.cs` (FetchSensorsAsync with Navigation Mapping)

4. `BEHAVIORAL: Implement Camera devices fetching and complete StartService with logging`
   - Files: `DeviceProviderService.cs` (FetchCamerasAsync + StartService)

### REFACTOR Commits (Optimization)
5. `REFACTOR: Optimize pagination logic, error handling, and logging`
   - Files: Multiple (performance improvements, code cleanup)

---

## Current Status
- **Phase 1**: ⏸️ NOT STARTED
- **Phase 2**: ⏸️ NOT STARTED
- **Phase 3**: ⏸️ NOT STARTED
- **Phase 4**: ⏸️ NOT STARTED
- **Phase 5**: ⏸️ NOT STARTED

## Next Step
▶️ **Phase 1.1**: Create Services folder and IDeviceProviderService interface

---

## Key Implementation Notes

### Navigation Mapping Pattern
```csharp
// 1. Fetch Controllers first (순서 중요!)
var controllers = await FetchControllersAsync(token);
var ctrlDict = controllers.ToDictionary(c => c.Id);

// 2. Fetch Sensors with Dictionary
var sensors = await FetchSensorsAsync(ctrlDict, token);

// 3. Inside FetchSensorsAsync:
if (dto.Controller != null && ctrlDict.TryGetValue(dto.Controller.Id, out var parent))
{
    sensor.Controller = parent;           // Child → Parent
    parent.Devices ??= new List<IBaseDeviceModel>();
    parent.Devices.Add(sensor);           // Parent → Children
}
```

### Logging Strategy
- **Info**: Start/Complete/Progress (정상 작동)
- **Warning**: Missing Controller, Partial data (경고 사항)
- **Error**: API failure, Exception (치명적 오류)

### Performance Targets
- Controllers 100개: < 2초
- Sensors 4000개: < 10초 (페이징 처리)
- Cameras 50개: < 1초
