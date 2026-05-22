# Ironwall.Dotnet.Libraries

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![License](https://img.shields.io/badge/License-Private-red)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey)
![Libraries](https://img.shields.io/badge/Libraries-30+-green)

> **Sensorway Framework**
> WPF 기반 모니터링 솔루션 개발을 위한 포괄적인 라이브러리 컬렉션

## 개요

Ironwall.Dotnet.Libraries는 Sensorway에서 개발한 .NET 8.0 기반의 재사용 가능한 라이브러리 모음입니다. 외곽울타리 침입탐지 시스템(PIDS) 및 다양한 모니터링 애플리케이션 개발을 위한 핵심 인프라를 제공합니다.

### 주요 특징

- **모듈식 아키텍처** - 30+ 독립적인 라이브러리로 구성된 확장 가능한 프레임워크
- **MVVM 패턴** - Caliburn.Micro 기반의 완전한 ViewModel 계층
- **데이터 관리** - Dapper 기반의 효율적인 데이터베이스 서비스
- **실시간 메시징** - Redis/NATS Pub/Sub 통합
- **지도 시각화** - GMap.NET 기반의 커스텀 지도 솔루션
- **ONVIF 통합** - IP 카메라 제어 및 스트리밍
- **의존성 주입** - Autofac 기반의 모듈 시스템

## 목차

- [아키텍처](#아키텍처)
- [라이브러리 카탈로그](#라이브러리-카탈로그)
- [시작하기](#시작하기)
- [기술 스택](#기술-스택)
- [문서](#문서)
- [개발 환경](#개발-환경)
- [변경 이력](#변경-이력)
- [라이선스](#라이선스)

## 아키텍처

### 계층 구조

```
┌─────────────────────────────────────────────────┐
│  애플리케이션 계층 (Dotnet.Monitoring.Solution) │
├─────────────────────────────────────────────────┤
│  UI 계층                                         │
│  - Devices.Ui    - Events.Ui                    │
│  - GMaps.Ui      - Sounds.Ui                    │
├─────────────────────────────────────────────────┤
│  비즈니스 로직 계층                              │
│  - Accounts      - Devices      - Events        │
│  - GMaps         - Sounds       - Streaming     │
│  - OnvifSolution - Gateway                      │
├─────────────────────────────────────────────────┤
│  데이터 접근 계층                                │
│  - Accounts.Db   - Devices.Db   - Events.Db    │
│  - GMaps.Db                                     │
├─────────────────────────────────────────────────┤
│  통신 계층                                       │
│  - Redis         - Nats         - Api           │
├─────────────────────────────────────────────────┤
│  프레임워크 계층                                 │
│  - Framework     - Base         - ViewModel     │
│  - Enums         - Utils                        │
└─────────────────────────────────────────────────┘
```

### 의존성 모델

라이브러리는 계층적 의존성 구조를 따릅니다:
- **상위 계층**은 **하위 계층**을 참조할 수 있습니다
- **하위 계층**은 **상위 계층**을 참조할 수 없습니다
- **동일 계층** 내에서는 인터페이스를 통한 느슨한 결합을 유지합니다

## 라이브러리 카탈로그

### 프레임워크 (Framework Layer)

#### Ironwall.Dotnet.Framework
핵심 프레임워크 서비스 및 부트스트래핑
- 애플리케이션 라이프사이클 관리
- 서비스 등록 및 초기화
- 글로벌 설정 관리

#### Ironwall.Dotnet.Libraries.Base
공통 기본 클래스 및 인터페이스
- `BaseCommonProvider`, `EntityCollectionProvider`
- `TaskService`, `DispatcherService`, `LogService`
- `IService`, `ILoadable`, `ICollector`

#### Ironwall.Dotnet.Libraries.ViewModel
MVVM ViewModel 계층
- `BaseViewModel`, `BaseCustomViewModel`
- `BaseDataGridViewModel`, `BasePanelViewModel`
- `ConductorOneViewModel`, `ConductorAllViewModel`

#### Ironwall.Dotnet.Libraries.Enums
열거형 정의
- `EnumDeviceType`, `EnumEventStatus`, `EnumColorType`
- `EnumMapProvider`, `EnumMapMode`

#### Ironwall.Dotnet.Libraries.Utils
유틸리티 및 변환기
- `BindingProxy`, `BoolToInverseVisibleConverter`
- `EnumBindingSourceExtension`
- `NumericOnlyBehavior`, `ClearSelectionOnEscBehavior`

---

### 비즈니스 모듈 (Business Layer)

#### Ironwall.Dotnet.Libraries.Accounts
사용자 인증 및 계정 관리
- 사용자 모델 및 프로바이더
- 세션 관리
- 토큰 생성

#### Ironwall.Dotnet.Libraries.Accounts.Db
계정 데이터베이스 서비스
- CRUD 작업 (Create, Read, Update, Delete)
- 로그인 이력 추적
- Dapper 기반 쿼리

#### Ironwall.Dotnet.Libraries.Devices
장치 관리
- 컨트롤러, 센서, 카메라 모델
- 장치 프로바이더
- 장치 상태 모니터링

#### Ironwall.Dotnet.Libraries.Devices.Db
장치 데이터베이스 서비스
- 장치 정보 영속화
- 장치 설정 관리

#### Ironwall.Dotnet.Libraries.Devices.Api
Device API 서비스 (GOP RESTful API 연동)
- `IDeviceApiService` / `DeviceApiService`
- Controller, Sensor, Camera CRUD 작업
- 필터링, 페이지네이션, 정렬 지원
- `ResponseHelper`: HTTP 응답 변환 헬퍼
- `DeviceApiModule`: Autofac 의존성 주입 모듈
- xUnit 단위 테스트 (15개 테스트, 100% 통과)

#### Ironwall.Dotnet.Libraries.Devices.Ui
장치 UI 컴포넌트 및 서비스
- **DeviceProviderService**: GOP API를 통한 Device 데이터 Fetching 및 Provider 업데이트
- **NavigationMappingHelper**: Controller ↔ Sensor 양방향 Navigation 참조 설정 (TDD 구현)
- **DtoToModelHelper**: DTO ↔ Model 변환 헬퍼
- 장치 목록 ViewModel
- 장치 속성 다이얼로그
- xUnit 단위 테스트 (18개 테스트, 100% 통과)

#### Ironwall.Dotnet.Libraries.Events
이벤트 처리
- Detection, Malfunction, Connection 이벤트
- 이벤트 모델 및 프로바이더
- 이벤트 카드 시스템

#### Ironwall.Dotnet.Libraries.Events.Db
이벤트 데이터베이스 서비스
- 이벤트 로그 저장
- 이벤트 히스토리 조회

#### Ironwall.Dotnet.Libraries.Events.Api
Event API 서비스 (GOP RESTful API 연동)
- `IEventApiService` / `EventApiService`
- Detection, Malfunction, Connection, Action 이벤트 CRUD
- 날짜 범위 검색, 다중 필터 지원
- `ResponseHelper`: HTTP 응답 변환 헬퍼
- `EventApiModule`: Autofac 의존성 주입 모듈
- xUnit 단위 테스트 (15개 테스트, 100% 통과)

#### Ironwall.Dotnet.Libraries.Events.Ui
이벤트 UI 컴포넌트
- 이벤트 패널 ViewModel
- 이벤트 다이얼로그
- 이벤트 카드 리스트

#### Ironwall.Dotnet.Libraries.GMaps
GMap.NET 통합
- 지도 설정 모델 (`GMapSetupModel`)
- 홈 포지션 관리 (`HomePositionModel`)
- 지도 제공자 (`MapProvider`)
- 커스텀 맵 지원

#### Ironwall.Dotnet.Libraries.GMaps.Db
지도 데이터베이스 서비스
- 지도 설정 저장
- 심볼 위치 정보 관리

#### Ironwall.Dotnet.Libraries.GMaps.Ui
지도 UI 컴포넌트
- `GMapViewModel` - 지도 렌더링
- 마커 컨트롤 (`GMapMarkerPidsControl`, `GMapMarkerCustomControl`)
- 심볼 관리 및 시각화

#### Ironwall.Dotnet.Libraries.Sounds
오디오 알림 시스템
- NAudio 기반 재생
- 이벤트별 사운드 매핑
- 오디오 장치 선택

#### Ironwall.Dotnet.Libraries.Sounds.Ui
사운드 설정 UI
- 사운드 파일 선택
- 오디오 장치 설정
- 재생 테스트

#### Ironwall.Dotnet.Libraries.Streaming
비디오 스트리밍
- RTSP 스트림 처리
- 프레임 캡처
- 스트림 관리

#### Ironwall.Dotnet.Libraries.OnvifSolution
ONVIF 카메라 통합
- 카메라 검색 및 연결
- PTZ 제어
- 프로파일 관리

#### Ironwall.Dotnet.Libraries.Gateway
게이트웨이 이벤트 관리
- 게이트웨이 이벤트 모델
- 설정 UI
- 이벤트 매핑

---

### 통신 (Communication Layer)

#### Ironwall.Dotnet.Libraries.Redis
Redis Pub/Sub 메시징
- `RedisService` - 메시지 발행/구독
- `RedisSetupModel` - 연결 설정
- 채널 관리

#### Ironwall.Dotnet.Libraries.Nats
NATS 메시징 (NATS.Client.Core v2)
- `NatsService` - Pub/Sub, Request/Reply
- `NatsSetupModel` - 클러스터 설정
- 고성능 메시징

#### Ironwall.Dotnet.Libraries.Api
HTTP API 통합
- `ApiService` - RESTful API 클라이언트
- `ApiSetupModel` - API 설정
- `HttpResponseMessageExtensions` - 응답 변환 확장 메서드
- 타임아웃, 재시도 정책, 에러 처리 지원

#### Ironwall.Dotnet.Libraries.Api.Messages
GOP RESTful API DTO 정의
- **Common**: `ApiResponse<T>`, `ApiListResponse<T>`, `PaginationDto`, `MetaDto`, `ApiError`
- **Devices**: `ControllerDeviceDto`, `SensorDeviceDto`, `CameraDeviceDto`
- **Events**: `DetectionEventDto`, `MalfunctionEventDto`, `ConnectionEventDto`, `ActionEventDto`, `ActionEventCreateDto`
- **Integrations**: `EventMappingDto` - 3rd party 이벤트 매핑
- **Defines**: `IEventDto` - 이벤트 공통 인터페이스
- **Helpers**: `FromEventConverter` - 다형성 JSON 변환기

#### Ironwall.Dotnet.Libraries.Devices.Api
Device API 서비스 (GOP RESTful API 연동)
- `IDeviceApiService` / `DeviceApiService`
- Controller, Sensor, Camera CRUD 작업
- 필터링, 페이지네이션, 정렬 지원
- xUnit 단위 테스트 포함 (100% 커버리지)

#### Ironwall.Dotnet.Libraries.Events.Api
Event API 서비스 (GOP RESTful API 연동)
- `IEventApiService` / `EventApiService`
- Detection, Malfunction, Connection, Action 이벤트 CRUD
- 날짜 범위 검색, 다중 필터 지원
- xUnit 단위 테스트 포함 (100% 커버리지)

---

### 모니터링 모델 (Monitoring Models)

#### Ironwall.Dotnet.Monitoring.Models
모니터링 전용 모델
- `PidsSymbolModel` - 심볼 모델
- `GeometricSymbolModel` - 기하학 심볼
- 이벤트-심볼 매핑

---

### 외부 라이브러리

#### GMap.NET
지도 시각화 라이브러리
- `GMap.NET.Core` - 핵심 지도 엔진
- `GMap.NET.WindowsPresentation` - WPF 통합
- 타일 캐싱 및 커스텀 맵 지원

## 시작하기

### 필수 요구사항

- **.NET 8.0 SDK** (Windows)
- **Visual Studio 2022** (v17.9 이상)
- **MariaDB/MySQL** (v10.3 이상) - 데이터베이스 계층 사용 시
- **Redis Server** (v6.0 이상) - Redis 라이브러리 사용 시
- **NATS Server** (v2.0 이상) - NATS 라이브러리 사용 시

### 설치

#### 1. 저장소 클론
```bash
git clone <repository-url>
cd Ironwall.Dotnet.Libraries
```

#### 2. NuGet 패키지 복원
```bash
dotnet restore Ironwall.Dotnet.Libraries.sln
```

#### 3. 솔루션 빌드
```bash
dotnet build Ironwall.Dotnet.Libraries.sln --configuration Release
```

### 라이브러리 참조

#### 프로젝트에서 라이브러리 참조 추가

**방법 1: 프로젝트 참조 (개발 환경)**
```xml
<ItemGroup>
  <ProjectReference Include="..\Ironwall.Dotnet.Libraries\Ironwall.Dotnet.Libraries.Base\Ironwall.Dotnet.Libraries.Base.csproj" />
  <ProjectReference Include="..\Ironwall.Dotnet.Libraries\Ironwall.Dotnet.Libraries.ViewModel\Ironwall.Dotnet.Libraries.ViewModel.csproj" />
</ItemGroup>
```

**방법 2: DLL 참조 (배포 환경)**
```xml
<ItemGroup>
  <Reference Include="Ironwall.Dotnet.Libraries.Base">
    <HintPath>libs\Ironwall.Dotnet.Libraries.Base.dll</HintPath>
  </Reference>
</ItemGroup>
```

#### Autofac 모듈 등록 예제

```csharp
// Bootstrapper.cs
protected override void ConfigureContainer(ContainerBuilder builder)
{
    // 순서대로 모듈 등록 (Order 메타데이터 활용)
    builder.RegisterModule(new AccountDbModule(setup, _log, 10));
    builder.RegisterModule(new DeviceUiModule(setup, _log, 20));
    builder.RegisterModule(new EventUiModule(setup, _log, 30));
    builder.RegisterModule(new RedisModule(setup, _log, 40));
    builder.RegisterModule(new NatsModule(setup, _log, 50));
    builder.RegisterModule(new SoundModule(setup, _log, 60));
    builder.RegisterModule(new GMapUiModule(setup, _log, 70));
    builder.RegisterModule(new GatewayModule(setup, _log, 80));
}
```

## 기술 스택

### 프레임워크 및 런타임
| 기술 | 버전 | 용도 |
|------|------|------|
| .NET | 8.0 | 런타임 플랫폼 |
| WPF | - | UI 프레임워크 |
| C# | 12.0 | 프로그래밍 언어 |

### 핵심 패키지
| 패키지 | 버전 | 용도 |
|------|------|------|
| Caliburn.Micro | 4.0.230 | MVVM 프레임워크 |
| Autofac | 8.3.0 | 의존성 주입 |
| Dapper | 2.1.66 | 마이크로 ORM |
| MySql.Data | 9.2.0 | MySQL 커넥터 |

### UI 라이브러리
| 패키지 | 버전 | 용도 |
|------|------|------|
| MahApps.Metro | 2.4.10 | 모던 UI |
| MaterialDesignThemes | 5.2.1 | Material Design |
| Microsoft.Xaml.Behaviors | 1.1.122 | Behavior 패턴 |

### 통신 및 메시징
| 패키지 | 버전 | 용도 |
|------|------|------|
| StackExchange.Redis | 2.8.16 | Redis 클라이언트 |
| NATS.Client.Core | 2.5.2 | NATS 클라이언트 |
| Newtonsoft.Json | 13.0.3 | JSON 직렬화 |

### 멀티미디어
| 패키지 | 버전 | 용도 |
|------|------|------|
| NAudio | 2.2.1 | 오디오 재생 |
| FFmpeg.AutoGen | 7.1.0 | 비디오 디코딩 |

### 테스트
| 패키지 | 버전 | 용도 |
|------|------|------|
| xUnit | 2.9.3 | 단위 테스트 프레임워크 |
| xunit.runner.visualstudio | 2.8.2 | Visual Studio 테스트 러너 |
| Microsoft.NET.Test.Sdk | 17.11.1 | .NET 테스트 SDK |

### 지도 및 시각화
| 라이브러리 | 용도 |
|------|------|
| GMap.NET.Core | 지도 엔진 |
| GMap.NET.WindowsPresentation | WPF 지도 컨트롤 |

## 문서

상세한 기술 문서는 다음을 참조하세요:

### API 레퍼런스 
 
주요 인터페이스 및 기본 클래스:

#### 서비스 계층
- `IService` - 모든 서비스의 기본 인터페이스
- `TaskService` - Template Method 패턴 기반 서비스
- `ILogService` - 로깅 서비스 인터페이스

#### 데이터 계층
- `BaseProvider<T>` - 제네릭 데이터 프로바이더
- `EntityCollectionProvider<T>` - 컬렉션 기반 프로바이더

#### ViewModel 계층
- `BaseViewModel` - 모든 ViewModel의 기본 클래스
- `ConductorOneViewModel<T>` - 단일 활성 화면 관리
- `BaseDataGridViewModel<T>` - DataGrid 패턴

#### API 계층 (GOP RESTful API 통합)

**기본 서비스**:
- `IApiService` / `ApiService` - HTTP 클라이언트 기반 API 서비스
- `IDeviceApiService` / `DeviceApiService` - Device CRUD 작업
- `IEventApiService` / `EventApiService` - Event CRUD 작업

**응답 타입**:
- `ApiResponse<T>` - 단일 엔티티 응답
- `ApiListResponse<T>` - 페이지네이션 목록 응답

**사용 예제**:

```csharp
// 1. Autofac 모듈 등록
builder.RegisterModule(new DeviceApiModule(setup, log, 100));
builder.RegisterModule(new EventApiModule(setup, log, 110));

// 2. Device API 사용
var deviceService = container.Resolve<IDeviceApiService>();

// Controller 목록 조회 (페이지네이션 + 필터링)
var response = await deviceService.GetControllersAsync(
    groupDevice: 1,
    status: "ACTIVATED",
    includeSensors: true,
    page: 1,
    limit: 20
);

if (response.Success)
{
    Console.WriteLine($"Total: {response.Pagination.Total}");
    foreach (var controller in response.Data)
    {
        Console.WriteLine($"Controller: {controller.NameDevice} ({controller.Id})");
    }
}

// Controller 생성
var newController = new ControllerDeviceDto
{
    GroupDevice = 1,
    NameDevice = "Controller-01",
    Status = "ACTIVATED"
};
var createResponse = await deviceService.CreateControllerAsync(newController);

// 3. Event API 사용
var eventService = container.Resolve<IEventApiService>();

// Detection Event 조회 (날짜 범위 필터)
var events = await eventService.GetDetectionEventsAsync(
    startDate: "2025-01-01T00:00:00Z",
    endDate: "2025-12-31T23:59:59Z",
    controller: 1,
    page: 1,
    limit: 50
);

// Action Event 생성 (다형성 이벤트 참조)
var actionEvent = new ActionEventCreateDto
{
    TypeEvent = "Action",
    Content = "침입 경보 확인 완료",
    User = "admin",
    FromEvent = 123,              // Detection Event ID
    FromEventType = "detection",  // "detection" or "malfunction"
    Datetime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
};
var actionResponse = await eventService.CreateActionEventAsync(actionEvent);

// 4. 응답의 FromEvent 다형성 처리 (GET 시)
var actionDto = await eventService.GetActionEventsAsync();
if (actionDto.Data.Any())
{
    var firstAction = actionDto.Data.First();
    if (firstAction.FromEvent is DetectionEventDto detection)
    {
        Console.WriteLine($"Detection Result: {detection.Result}");
    }
    else if (firstAction.FromEvent is MalfunctionEventDto malfunction)
    {
        Console.WriteLine($"Malfunction Type: {malfunction.TypeMalfunction}");
    }
}
```

**에러 처리**:

```csharp
var response = await deviceService.GetControllersAsync();

if (!response.Success)
{
    Console.WriteLine($"Error: {response.Meta.Message}");
    if (response.Error != null)
    {
        Console.WriteLine($"Detail: {response.Error.Detail}");
    }
}
```

## 개발 환경

### 빌드

**전체 솔루션 빌드:**
```bash
dotnet build Ironwall.Dotnet.Libraries.sln --configuration Release
```

**특정 라이브러리 빌드:**
```bash
dotnet build Ironwall.Dotnet.Libraries.Base/Ironwall.Dotnet.Libraries.Base.csproj
```

**출력 경로:**
```
bin/Release/net8.0-windows/
```

### 테스트

**단위 테스트 실행:**
```bash
# 전체 테스트 실행
dotnet test Ironwall.Dotnet.Libraries.sln

# 특정 프로젝트 테스트 실행
dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj
dotnet test Ironwall.Dotnet.Libraries.Devices.Api/Ironwall.Dotnet.Libraries.Devices.Api.csproj
dotnet test Ironwall.Dotnet.Libraries.Events.Api/Ironwall.Dotnet.Libraries.Events.Api.csproj
```
 
**테스트 커버리지:**
- **Devices.Ui**: 18개 테스트 (DeviceProviderService: 11, NavigationMappingHelper: 7)
- **Devices.Api**: 15개 테스트 (Controllers: 5, Sensors: 5, Cameras: 5)
- **Events.Api**: 15개 테스트 (Detection: 3, Malfunction: 3, Connection: 2, Action: 2, Integration: 5)
- **Total**: 48개 테스트, 100% 통과 ✅

### 패키지 게시 (내부용)

```bash
dotnet pack Ironwall.Dotnet.Libraries.Base.csproj --configuration Release --output nupkgs
```

## 변경 이력 (Version History by Branch)

> 각 버전은 git branch 기준으로 정리되었습니다. 최신 버전이 상단에 위치합니다.

---

### [Unreleased] — v2.6.2 (2026-05-22 기준)

**이벤트 파이프라인 전체 성능 최적화** (Event_Performance_Optimization)
- `ApplyCompositeStatus` — `MarshalUpdate()` 코얼레싱으로 NATS 스레드→WPF STA 위반 해소
- `DetectionNatsSyncService` / `MalfunctionNatsSyncService` — `PublishOnUIThreadAsync` 전환 (EA BackgroundThread ObservableCollection 접근 Blocker 제거)
- `SymbolEventManager._deviceLookupById` — `ConcurrentDictionary<int, DeviceSymbolLookupModel>` O(1) 보조 인덱스 추가 (O(N) fallback 3곳 제거) + `TryResolveDevice` 헬퍼
- `EventCardListPanelViewModel._cardByEntryId` — `Dictionary<string, EventCardBaseViewModel>` O(1) 인덱스 (기존 O(N) `ViewModelProvider.FirstOrDefault` 대체)
- `HandleAutoReportAsync` / `HandleAutoRecoveryAsync` — `async void` → `async Task` 전환 + `EventUiModule` ContinueWith 래퍼 (`AutoReportInFlight = false` 성공/실패 모든 경로 보장)
- API 실패 시 `entry.NextRetryAfter = Now + 30s` 설정 — 무한 재시도 차단 (`BACKOFF_SECONDS = 30`)
- `SoundAlarmController.OnQueueCleared` — `State = Idle` + `_lastEventTime = default` 완전 리셋 (일괄 조치보고 후 Playing 고착 해소)
- `EventQueueManager` — `_scratchPrevGroupStates.Clear()` 재사용 (Dequeue 매회 `new Dictionary<>()` 할당 제거) + `FindEntryByDevice` foreach min 단일 패스
- `EventCardBaseViewModel` — `IsFlipped`, `_actionInProgress` 필드 추가 (VirtualizingStackPanel 가상화 IMPL-10 준비)
- TEST: 4개 신규 (ApplyCompositeStatus STA 검증, OnQueueCleared 3개) — 302 pass, 0 errors

**자동조치보고 이중경로 통합** (AutoActionReport_DualPath_Fix)
- `EventCardBaseViewModel` per-card `System.Timers.Timer` 완전 제거 — Path A 타이머 삭제 (이중 API 호출 / 20초 무한재시도 / `GC.Collect()` 안티패턴 동시 해소)
- `EventEntry.EventId` 필드 추가 — NATS 수신 시점에 서버 이벤트 ID 보관 (`SourceEvent` 대체)
- `EventQueueManager.OnSharedTimerTick` 재설계 — kill-switch + `AutoReportInFlight` 가드
- `EventCardListPanelViewModel.HandleAutoReport(EventEntry)` — API → Dequeue → UI 카드 제거 → NATS 단일 경로 구현 (Path B 완성)
- `EventUiModule.eqm.OnAutoReport += elp.HandleAutoReport` 와이어링 완성
- **BUG-C1 해결**: `OnAutoReport` 구독자 0개 → UI 카드 영구 좀비화 차단
- **BUG-C2 해결**: `_timer.AutoReset=true` → 20초 무한 재시도 제거
- **BUG-C3 해결**: Double Dispose + `GC.Collect()` 안티패턴 제거

**사운드 타입 즉시 전환** (SoundTypeSwitch_ImmediateStop_Fix)
- `ISoundService.StopAndPlayAsync(EnumEventType, CancellationToken)` 추가 — 재생 중 타입 전환 시 이전 사운드 즉시 중지
- `SoundService._switchSemaphore` — 동시 전환 직렬화 (`SemaphoreSlim maxCount=1`)
- `PlayWith*` / `PlayContinuously*` / `PlayOnce*` — per-item `CancellationToken.ThrowIfCancellationRequested` 추가
- `SoundAlarmController` 3-Action → 1-Action `stopAndPlay` 리팩터 (인터페이스 대칭 단순화)
- `SoundAlarmControllerTests` 14개 테스트 (기존 10개 갱신 + 신규 타입 전환 시나리오 4개)

---

**개별 디바이스 복합 상태 SSOT 전환 + Fault 자동복구** (BUG-01, BUG-02, REQ-01)
- `EventQueueManager.ComputeDeviceState()` 신규 — `_entries` SSOT 기반 디바이스 복합 상태 계산 (`ComputeGroupState()` 대칭 구조)
- `OnDeviceStateChanged(deviceId, deviceType, prev, next)` 이벤트 추가 — `Enqueue`/`Dequeue`/`DequeueAll` 전 위치에서 발화
- `Enqueue()` 자동복구 원자 처리: Detection 도착 시 동일 디바이스의 Fault 엔트리를 단일 lock 내에서 원자 제거
- `OnAutoRecovery(faultEntryId)` 이벤트 추가 — 자동복구 발생 시 상위 레이어(서버 API + UI + NATS) 통지
- `SymbolEventManager.HandleDeviceStateChanged()` 추가 — `ApplyCompositeStatus(next)` 멱등 직접 세팅
- `DeviceSymbolLookupModel.ApplyCompositeStatus(status)` 추가 — 누적 추론 방식(`ProcessEvent`) 대체
- `EventCardListPanelViewModel.HandleAutoRecovery()` 추가 — `"etc 자동복구"` API 보고 + 카드 제거 + NATS 발행
- **BUG-01 해결**: 동일 센서 Detection→Fault 전환 시 개별 심볼 미갱신 → `OnDeviceStateChanged` 구독으로 해결
- **BUG-02 해결**: FaultedDetecting 부분 Dequeue 시 잘못된 심볼 상태 → SSOT 재계산으로 해결
- **REQ-01 구현**: Fault 활성 중 Detection 도착 → Fault 자동조치보고 + Detection 정상 처리
- `EventQueueManagerTests` +8 (55개 전체 통과, BUG-01/BUG-02/REQ-01/V-05 검증)

**브랜치:** `v2.2` | **작업자:** GH.LEE

#### ⚠️ Breaking Changes

**GatewayEvent Group N:N 마이그레이션**
- `GatewayEvents.Group (int)` → `GatewayEventGroups` 연결 테이블 + `List<int> DeviceGroups` 구조로 전환
- DB 스키마 변경: `GatewayEventGroups(EventId, GroupId)` 신규 테이블 + `ON DELETE CASCADE`
- 기존 `Group` 컬럼은 마이그레이션 기간 유지 (`[Obsolete]` 브리지 프로퍼티) — 다음 릴리스에서 DROP 예정
- `BuildSchemeAsync` 자동 마이그레이션: 기존 `Group` 값을 `GatewayEventGroups`로 INSERT IGNORE
- `IGatewayEventModel.DeviceGroups: List<int>` 추가, `Group: int` Obsolete 처리
- `NatsDomainService` Intersect 매칭 변경: `Contains(entity.Group)` → `DeviceGroups.Intersect(...).Any()`

#### 개선 및 버그 수정

**GatewaySetupView ComboBox 인라인 그룹 선택** (Breaking Change 동반)
- 그룹 컬럼: 팝업 다이얼로그 방식 → DataGrid 인라인 ComboBox (BindingProxy 패턴)
- `GatewayGroupPickerViewModel` 삭제, `GatewayEventViewModel.SelectedGroup` 어댑터 프로퍼티 추가

**Detection 사운드 시스템 + 이중 경로 정리**
- `SoundAlarmController` 슬라이딩 타이머 구현 — EventQueueManager와 SoundService 연동
- `NatsDomainService.ProcessDetection`에서 `ProcessDeviceEvent` 직접 호출 제거 (BUG-02)
- `EventUiModule`에 `SoundAlarmController` DI 와이어링 완료

**배치 조치보고 이중 INSERT 및 Malfunction 심볼 복원 수정** (BUG-01 + BUG-03)
- `NatsDomainService.HandleAsync(SendActionRequestMessage)` INSERT 제거 — NATS 발행 전용 transport adapter로 전환 (BUG-01)
- `DetectionEventCardViewModel.SendAction()` / `MalfunctionEventCardViewModel.SendAction()` — 단일 조치보고 INSERT 직접 수행 (V-01 확인 결과 적용)
- `MalfunctionNatsSyncService` 신규: NATS MALFUNCTION 구독 → `EventQueueManager.Enqueue()` → EntryId 부여 → 심볼 복원 체인 (BUG-03)
- `NatsDomainService.ProcessFault()` `ProcessDeviceEvent()` 직접 호출 제거 — EventQueue 단일 경로로 통일

**Malfunction 복합 상태 및 FenceGroup 3-레이어 시각화** (FR-01~FR-10)
- `EnumCompositeEventStatus` 신규 enum: Normal / Detecting / Faulted / FaultedDetecting / Connection
- `IPidsEventCapable.CompositeStatus` 프로퍼티 추가 — EventStatus 병행 운영
- `EventQueueManager.ComputeGroupState()` — `_entries` HashSet 파생 계산 (별도 카운터 금지, SSOT)
- `OnGroupStateChanged(groupId, prev, next)` 이벤트 — OnGroupFirstEvent/OnGroupEmpty 대체
- `EventQueueManager` lock _gate 스레드 안전화 — 이벤트 발화 lock 외부 로컬 캡처 패턴
- `SymbolEventManager.HandleGroupStateChanged()` — Normal/Detecting/Faulted/FaultedDetecting 라우팅
- `DeviceSymbolLookupModel.ProcessEvent(Fault)` 분기 추가 — CompositeStatus.Faulted 직접 설정
- `DeviceSymbolLookupModel.ProcessEventReport()` — CompositeStatus Normal 복원 추가
- `PidsGroupMarkerStyle.xaml` 3-레이어 재설계: BasePolyline(기본) + FaultOverlay(Orange) + DetectionOverlay(Red blink)
- `MalfunctionNatsSyncService` fan-out 검증 — Controller/Cable cut → GroupIds 전체 Enqueue
- `EventUiModule` 와이어링 교체: `OnGroupStateChanged += sem.HandleGroupStateChanged`

**GMapCustomControl 이미지 드래그/리사이즈 버그 수정**
- 이미지 마커 드래그 → 리사이즈 핸들 반응 개선
- ZIndex DB 영속화 연동 안정화

---

### v2.2 (2026-03-24 ~)

**작업자:** GH.LEE | **브랜치:** `v2.2` (현재) | **완료 PRD 6건 + 핫픽스 2건 + 진행중 PRD 24건**

#### 주요 변경사항 — CustomMap 오버레이 + 맵 상호작용 재설계

**CustomMap 오버레이 시스템** (마스터 PRD + 하위 3건)
- CustomMap 베이스맵 → 오버레이 전환 (WPF Canvas Overlay 기반)
- 등록/진행 임베디드 패널 (4-Phase `MapRegistrationControl`)
- 오버레이 영속화 + 복수 등록/삭제 지원 (15건 버그 수정)
- `CustomMapOverlayService` 신규 — OnRender 기반 타일 렌더링
- `LruTileCache` 타일 캐시 + xUnit 테스트
- OverlayImage 레이어 시스템 연동 (Seed + Visibility + Opacity)

**맵 마우스 상호작용 재설계** (PRD 4건)
- 맵 패닝 우클릭 → 좌클릭 전환 (`DragButton=Left`)
- EditMode OFF 시 심볼/이미지 `IsHitTestVisible=false` (패닝 투과)
- EditMode ON 시 빈 공간 좌클릭 패닝 유지 (WPF 이벤트 버블링)
- 우클릭 컨텍스트 메뉴 전체 마커 타입 통합 (베이스 클래스 기반)

**심볼 ZIndex/ZOrder 제어 시스템** (PRD 2건)
- 심볼 ZIndex 전체 파이프라인: `ISymbolModel → DB → DTO → Marker → Shape`
- `GetMarkerAtScreen` 우선순위 정렬 (ZIndex DESC → 면적 ASC → 거리 ASC)
- 우클릭 메뉴 레이어 순서 제어 (맨위로/위로/아래로/맨아래로)
- ZIndex DB 영속화 (`Symbols` 테이블 `ZIndex` 컬럼 추가)

**MapViewModel 정리**
- MapViewModel Provider 정리 및 의존성 정비
- MBTiles ZoomLevel Shadowing 버그 수정

**핫픽스**
- OverlayImage ZOrder Edit 모드 OFF 비반영 → `InvalidateVisual()` 추가
- OverlayMap 리사이즈 타일 누락 → `MainMap_SizeChanged` 핸들러 추가
- 레이어 패널 MaxHeight 제거 + 스크롤 잘림 해결

---

### v2.1 (2026-02 ~ 2026-03)

**작업자:** GH.LEE | **브랜치:** `v2.1`

#### 주요 변경사항 — 맵 시스템 대규모 개선

**MBTiles 오프라인 맵 통합**
- MBTiles DefinedMap 통합 — 인터넷 없이 사용 가능한 오프라인 맵 지원
- 맵 초기화/전환 프로세스 분리 (`InitializeMBTilesMap` + `SwitchMBTilesMap`)
- 맵 전환 시 위치/줌 유지, 타일 겹침 수정, 전환 안정성 개선
- MBTiles Datas ↔ DB 동기화

**레이어 관리 시스템**
- 레이어 관리 시스템 신규 구현 — DB CRUD 연동
- 레이어 패널 트리 재설계 + 10 xUnit tests

**GMap UI 리뉴얼**
- GMap UI 디자인 리뉴얼 + 방송 패널 추가
- 관심지역(ROI) 관리 기능 + 5 xUnit tests

**조치보고 시스템**
- 전체 조치보고 완성
- `ExecuteBatchReportAsync` — 배치 처리 (6 tests)

**버그 수정**
- 콤보박스 초기 선택 빈칸 수정 (`NotifyOfPropertyChange` 추가)
- DevicePanel ProgressCircle 미표시 수정

---

### v2.0 (2025-12 ~ 2026-02)

**작업자:** GH.LEE | **브랜치:** `v2.0`

#### 주요 변경사항 — GOP v2.0 대규모 리뉴얼

**신규 디바이스 타입**
- Speaker, Enclosure, Lamp 디바이스 모델 및 API 서비스 추가
- Camera 하위 모델 확장 (PTZ, Thermal 등)
- GOP v2.0 Enum 타입 대거 추가

**NATS 실시간 동기화 서비스**
- `DetectionNatsSyncService` — NATS DETECTION 수신 서비스
- `CameraPtzNatsSyncService` — PTZ_STATUS NATS 수신 서비스
- `DeviceNatsSyncService` — SYNC_DEVICE NATS 수신 서비스
- `SymbolEventManager` NATS Sync 연동 확장

**PIDS 심볼 확장**
- SmartSensor, IpSpeaker PIDS 심볼 추가
- `DeviceSymbolLookupModel.SyncFromDevice()` 구현

**Event API 서비스**
- Event API 서비스 및 통합 테스트 (20/20 green)

**UI 개선**
- `DeviceAssignDialog` 다중 선택 DataGrid로 재설계
- 구역 column 제거 + 조치보고 checkbox 추가
- FAULT_FENCE 장애 색깔 버그 수정

---

### v1.9.2 (2025-12)

**작업자:** GH.LEE | **브랜치:** `v1.9.2`

#### 주요 변경사항
- Image 컴포넌트 기능 구현

---

### v1.9.1 (2025-12)

**작업자:** GH.LEE | **브랜치:** `v1.9.1`

#### 주요 변경사항
- Image Object Property 및 Symbol 생성 (DB 업데이트 미완성)

---

### v1.9 (2025-11 ~ 2025-12)

**작업자:** GH.LEE | **브랜치:** `v1.9`

#### 주요 변경사항 — BaseBearing, FOV, 안정성 개선

**BaseBearing 속성 추가 (Phase 20)**
- `PidsSymbolModel`에 BaseBearing 속성 추가 (STRUCTURAL)
- Database 스키마 BaseBearing 컬럼 추가 (STRUCTURAL)
- FOV BaseBearing 초기화 테스트 (BEHAVIORAL)
- BaseBearing UI 컨트롤 구현 (BEHAVIORAL)

**버그 수정**
- DB 로드 시 `DetectionBearing → BaseBearing` 초기화 수정
- 런타임 전용 FOV 속성 DB 저장 방지
- MySQL concurrency error — UPDATE 쿼리 `UpdatedAt` 명시로 해결
- 이벤트 탐지/조치보고 이중 조치보고 버그 수정

**기타**
- ISO6301 파싱 로직 추가
- 카메라 FOV 업데이트

---

### v1.8 (2025-11)

**작업자:** GH.LEE | **브랜치:** `v1.8`

#### 주요 변경사항
- `SymbolEventManager` 그룹/싱글 lookup 구분 로직 구현
- PidsSymbol 장비매칭 Dropdown + GroupLine 탐지 색상 변경

---

### v1.7 (2025-11)

**작업자:** GH.LEE | **브랜치:** `v1.7`

#### 주요 변경사항
- 소규모 업데이트 및 안정화

---

### v1.6 (2025-11)

**작업자:** GH.LEE | **브랜치:** `v1.6`

#### 주요 변경사항 — Events.Ui API 마이그레이션

**Events.Db → Events.Api 전환**
- Events.Ui에서 Events.Db 의존성 완전 제거
- Panel ViewModel 및 보조 ViewModel을 Events.Api 기반으로 마이그레이션
- `EventProviderService` 신규 구현 (`FetchDetectionEventsAsync` TDD GREEN)

**DTO 정비**
- DTO를 GOP API 스펙에 맞게 수정
- Connection & Action 이벤트 변환 로직 구현
- Detection, Malfunction 이벤트 DTO/모델 정비

---

### v1.5 (2025-11)

**작업자:** GH.LEE | **브랜치:** `v1.5`

#### 주요 변경사항 — Device API 마이그레이션 (Db → Api)

**Devices.Ui API 전환**
- `CameraDevicePanelViewModel` — ApiService 기반으로 마이그레이션
- `SensorDevicePanelViewModel` — ApiService 기반으로 마이그레이션
- `ControllerDevicePanelViewModel` — ApiService 기반으로 마이그레이션

**DeviceProviderService 신규 구현**
- Controller/Sensor/Camera 디바이스 fetching with pagination (Phase 2~4)
- `includeSensors`, `includeController` 속성 추가

**NavigationMappingHelper (TDD)**
- Controller ↔ Sensor 양방향 Navigation 참조 설정
- `SetupBidirectionalReferences()`, `GetOrphanedSensors()` 구현
- 7개 xUnit 테스트 (TDD Red → Green → Refactor)

**DtoToModelHelper**
- DTO ↔ Model 변환 헬퍼 (전체 테스트 커버리지)

**기타**
- Message 통합, API 관련 문서 작성

---

### v1.4 (2025-11)

**작업자:** GH.LEE | **브랜치:** `v1.4`

#### 주요 변경사항 — GOP RESTful API 통합 라이브러리

**Ironwall.Dotnet.Libraries.Api.Messages** (신규)
- 공통 응답 타입: `ApiResponse<T>`, `ApiListResponse<T>`, `PaginationDto`, `MetaDto`, `ApiError`
- Device DTO: `ControllerDeviceDto`, `SensorDeviceDto`, `CameraDeviceDto`
- Event DTO: `DetectionEventDto`, `MalfunctionEventDto`, `ConnectionEventDto`, `ActionEventDto`
- 다형성 JSON 직렬화 (`FromEventConverter`)

**Ironwall.Dotnet.Libraries.Devices.Api** (신규)
- Device CRUD API 서비스 (Controller, Sensor, Camera)
- 필터링, 페이지네이션, 정렬 지원
- xUnit 15개 테스트 (100% 통과)

**Ironwall.Dotnet.Libraries.Events.Api** (신규)
- Event CRUD API 서비스 (Detection, Malfunction, Connection, Action)
- 날짜 범위 검색, 다중 필터 지원
- xUnit 15개 테스트 (100% 통과)

**GOP API 연동 메시지 시스템 구축**

---

### v1.3.3 ~ v1.3.4 (2025-10)

**작업자:** GH.LEE | **브랜치:** `v1.3.3`, `v1.3.4`

#### 주요 변경사항
- Gateway(3rd party 이벤트 정의) 라이브러리 추가
- NATS 라이브러리 수정
- 팝업 관련 프로세스 업데이트

---

### v1.3.2 (2025-10)

**작업자:** GH.LEE | **브랜치:** `v1.3.2`

#### 주요 변경사항
- NATS 라이브러리 개발
- Redis 라이브러리 업데이트
- 카메라 모델 수정

---

### v1.3.1 (2025-10)

**작업자:** GH.LEE | **브랜치:** `v1.3.1`

#### 주요 변경사항
- 스트리밍 라이브러리 업데이트 및 구현
- **MapSetupViewModel 개선**: EnumMapProvider 기반 MapTypes/MapNames, 타일 디렉토리 선택
- **MapSetupModel 확장**: MapType, MapMode, MapName, TileDirectory, HomePosition
- **NatsSetupModel 확장**: IP, Port, 인증 정보
- **RedisSetupModel 추가**: IP, Port, 비밀번호, 채널 이름
- **Gateway Behavior 패턴** 도입: `GatewayEventSelectedItemsBehavior`

---

### v1.3.0 (2025-10)

**작업자:** GH.LEE | **브랜치:** `v1.3.0`

#### 주요 변경사항
- v1.2.9와 동일 (안정화 버전 태깅)

---

### v1.2.9 (2025-09 ~ 2025-10)

**작업자:** GH.LEE | **브랜치:** `v1.2.9`

#### 주요 변경사항
- 이벤트 연결 및 이벤트 서비스 구현
- RTSP 팝업 이벤트 설정 UI 및 DB 구현
- RTSP 팝업을 서버 기반으로 전환하여 개발

---

### v1.2.81 (2025-09)

**작업자:** GH.LEE | **브랜치:** `v1.2.81`

#### 주요 변경사항
- 카메라 팝업 기능 구현 및 CustomControl 구현
- Streaming Libraries 심각한 버그 발견 → 롤백 후 재작업

---

### v1.2.8 (2025-09)

**작업자:** GH.LEE | **브랜치:** `v1.2.8`

#### 주요 변경사항
- RTSP 스트리밍 라이브러리 구축 및 안정화
- RTSP 감시 금지 구역 추가
- 유지보수/에러 이벤트 연동

---

### v1.2.7 (2025-09)

**작업자:** GH.LEE | **브랜치:** `v1.2.7`

#### 주요 변경사항
- `PidsGroupSymbol` 구축 및 DB 연동
- `InfraSymbol` 추가 및 DB 등록
- Line/Area 계열 심볼 추가, Adorner 추가
- 카메라 View 영역 애니메이션 디버깅
- Infra 마커 버그 수정

---

### v1.2.6 (2025-08 ~ 2025-09)

**작업자:** GH.LEE | **브랜치:** `v1.2.6`

#### 주요 변경사항 — 군대부호 시스템 전체 구현
- 군대부호 UI 구현, DB 구축, 미리보기 기능
- 군대부호 이미지 수정 및 기능 수정
- 군대부호 필수 속성 구현
- 군대부호 전환 로직 수정 및 디버깅
- Line 계열 PIDS 심볼 추가 구성 준비
- LineMarker 에러 수정 및 Adorner 연동 버그 수정

---

### v1.2.5 (2025-08)

**작업자:** GH.LEE | **브랜치:** `v1.2.5`

#### 주요 변경사항 — PidsSymbol 시스템 구축
- PidsSymbol DB 스키마 구성 및 DB CRUD 로직 구성, 단위 테스트 완료
- Pids 심볼 카메라 앵글 구현
- Adorner 회전 컨트롤러 진동 버그 수정
- 심볼 속성 추가/변경 및 UI XAML 수정
- PidsMarker 속성 창 구성
- `GeometricProperty` 디버깅 완료
- PropertyWindow Marker 전환 시 버그 수정
- PropertyWindow Binding 작업 및 상호 연동 버그 수정

---

### v1.2.4 (2025-08)

**작업자:** GH.LEE | **브랜치:** `v1.2.4`

#### 주요 변경사항
- PIDS 심볼 시각화 시스템 구현
- 장치 타입별 색상 코딩 (`EnumColorType`)
- 마커 스타일 테마 추가 (`PidsMarkerStyle.xaml`)
- `PidsSymbolModel`, `DeviceSymbolLookupModel` 등 신규 모델
- 다수 기능 추가 및 기존 파일 수정

---

### v1.2.3 (2025-08)

**작업자:** GH.LEE | **브랜치:** `v1.2.3`

#### 주요 변경사항
- Sensorway 관련 수정 및 업데이트

---

### v1.2.2 (2025-08)

**작업자:** GH.LEE | **브랜치:** `v1.2.2`

#### 주요 변경사항
- 소규모 업데이트

---

### v1.2.1 (2025-08)

**작업자:** GH.LEE | **브랜치:** `v1.2.1`

#### 주요 변경사항
- Adorner 업데이트 및 GMap.NET 관련 업데이트

---

### v1.2.0 (2025-08)

**작업자:** GH.LEE | **브랜치:** `v1.2.0`

#### 주요 변경사항 — 프로젝트 초기 구축
- GMap.NET 적용
- 불필요 참조 라이브러리 삭제
- WPF Property Panel 바인딩 오염 문제 해결
  - 마커 선택 시 이전 Property Panel의 바인딩이 새 마커 속성을 오염시키는 현상 수정
  - `DisconnectFromMarker()` 메서드로 이전 마커 참조 무효화
  - Property Panel 완전 재생성을 통한 바인딩 오염 근본 차단

---

## 라이선스

**Private/Proprietary License**

Copyright (C) 2023-2026 Sensorway Co., Ltd. All rights reserved.

이 소프트웨어는 Sensorway Co., Ltd.의 독점 소유이며 무단 복제, 배포, 수정을 금지합니다.

## 연락처

### 개발팀
- **개발자**: GH.LEE
- **이메일**: lsirikh@naver.com
- **부서**: SW Team

### 회사 정보
- **회사명**: 주식회사 센서웨이 (Sensorway Co., Ltd.)
- **주소**: 경기도 고양시 통일로 140, A33 (삼송테크노밸리)
- **전화**: 02-957-6500
- **이메일**: sensorway@sensorway.co.kr
- **웹사이트**: http://www.sensorway.co.kr

---

**문서 버전**: 2.6.2
**최종 업데이트**: 2026-05-22
**문서 상태**: ✅ 최종 승인
