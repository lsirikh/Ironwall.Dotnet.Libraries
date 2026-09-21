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
│  UI 계층 (콘솔)                                  │
│  - Devices.Ui    - Events.Ui    - Accounts.Ui   │
│  - Reports.Ui    - GMaps.Ui     - Sounds.Ui     │
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
유틸리티 · 변환기 · **콘솔 커널**
- `BindingProxy`, `BoolToInverseVisibleConverter`
- `EnumBindingSourceExtension`
- `NumericOnlyBehavior`, `ClearSelectionOnEscBehavior`
- **콘솔 커널** `Console/` (네임스페이스는 `…Utils.Consoles` — 폴더는 단수, 이름공간은 복수다):
  `ConsoleShell` · `ConsoleRail`/`ConsoleRailItem`/`ConsoleRailEntry` · `ConsoleToolbar` · `ConsoleDetailHost`(고정 적용 막대) ·
  `ConsoleSection`/`ConsoleField`/`NotReceivedBox`(**셋 다 `Console/ConsoleSection.cs` 한 파일에 있다**) ·
  `ConsoleEmptyState` · `ConsoleColumns` · `ConsolePrefs` · `ConsoleLayoutMath`
- **다이얼로그 커널(T4)** `Console/Dialogs/`: `ConsoleDialogFrame`(+`ConsoleDialogFramePeer`) · `ConsoleDialogText` · `DialogSizeRules` · `DialogKeyRules` · `DialogFocusRules` · `DialogIds`. 뷰모델 없음 — 컨트롤 + 정적 규칙 클래스다
- **드래그 커널** `Behaviors/Drag/`: `CaptureDragBehavior` · `DragHandle`(**`CaptureDragBehavior.cs` 안에 있다**) · `DropZone`/`DragPayload`/`IDragDropHandler`(**`DragContracts.cs`**) · `DropZoneChrome` · `ReorderKeyboardBehavior` · `DragMath`(순수) · `DragAdorners`
- 기본 템플릿은 `Themes/Generic.xaml`(+`Themes/Dialogs.xaml`), **공유 스타일은 `Theme` 프로젝트**로 옮겼다 — 아래 §콘솔 커널 소비법

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

#### Ironwall.Dotnet.Libraries.Accounts.Ui
계정 · 권한 UI — **계정 콘솔**(N-06)
- `Views/Panels/AccountConsolePanelView` + `ViewModels/Panels/AccountConsolePanelViewModel` — 레일 6(사용자 · 권한 설정 · 세션 관리 · 권한 부여 · 감사 로그 · ─ · 세션 설정)
- `Consoles/` — `Lists/`(열 명세) · `Forms/`(상세 5절) · `Groups/`(사용자→그룹 드롭 판정) · `Matrix/`(권한 매트릭스 + 드래그 페인팅 좌표 수학) · `Sessions/` · `Audit/` · `Grants/`
- **호스트가 잡는 클래스 · 뷰 이름은 동결**돼 있다(`AccountConsolePanelViewModel` / `AccountConsolePanelView`) — 바꾸면 호스트가 창을 못 찾는다
- 테스트는 별도 프로젝트 `Ironwall.Dotnet.Libraries.Accounts.Ui.Tests`

#### Ironwall.Dotnet.Libraries.Reports.Ui
보고서 UI — **보고서 콘솔**(N-09)
- `Views/Panels/ReportConsoleView` + `ViewModels/Panels/ReportConsoleViewModel` — 레일 3(생성 이력 · 새 보고서 · 템플릿) · 상세 칸 기본 폭 **380**
- `Consoles/` — `Lists/`(열 · 상태 칩) · `Preview/`(**`ReportPreviewSurface` — WebView2 공역 판정, 순수 함수**) · `Templates/`(구성 체크 · 순서 · 드롭 핸들러)
- 테스트는 라이브러리 csproj 안 `Tests/`

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
장치 UI 컴포넌트 및 서비스 — **콘솔 넷이 여기 산다**
- **DeviceProviderService**: GOP API를 통한 Device 데이터 Fetching 및 Provider 업데이트
- **NavigationMappingHelper**: Controller ↔ Sensor 양방향 Navigation 참조 설정 (TDD 구현)
- **DtoToModelHelper**: DTO ↔ Model 변환 헬퍼
- `Consoles/` — 장비 콘솔(N-02) · `Assembly/` 조립기·프리셋(N-03) · `Wiring/` 셋업·결선맵(N-04) · `Units/` **부대 콘솔**(N-11) · `Servers/` **서버 모니터**(N-12) · `ByComponent/` · `Properties/` · `Forms/` · `Lists/` · `Groups/` · `Dialogs/`
- 뷰 · 뷰모델 위치가 두 가지다 — `Consoles/<기능>/` 은 View + ViewModel 을 **같이** 두고(N-04 · N-11 · N-12), 장비 대시보드는 `Views/Dashboards/` + `ViewModels/Dashboards/` 로 갈라져 있다
- 테스트는 **라이브러리 csproj 안 `Tests/`** 에 있다(별도 테스트 프로젝트가 아니다)

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
이벤트 UI 컴포넌트 — 이벤트 콘솔 · 억제 스케줄 · 맵핑 워크벤치
- 이벤트 패널 ViewModel / 이벤트 다이얼로그 / 이벤트 카드 리스트
- `Views/Dashboards/EventDashboardView` + `ViewModels/Dashboards/EventDashboardViewModel` — **이벤트 콘솔**(N-07). 레일 6(개요 · 탐지 · 장애 · 연결 · 조치 · **억제 스케줄**)
- `Consoles/` — `Overview/`(개요 T3 · 추이 기간 끌기) · `Lists/` · `Detail/` · `Tray/`(조치 트레이) · `Suppression/`(**억제 스케줄** N-08) · `Mapping/`(**이벤트 맵핑 워크벤치** N-13, View + VM 동거)
- `Views/Consoles/` — `SuppressionListView` · `SuppressionDrawerView`(780 서랍) · `SuppressionDetailView` · `ActionTrayView` · `EventOverviewView` · `EventDetailView`
- ⚠ **`Events.Ui/Resources/Resources.xaml` 는 앱 리소스 트리에 병합되지 않는다** — 새 컨버터 · 스타일은 **뷰 로컬**(`UserControl.Resources`)에 등록한다. 라이브러리 Resources 에 넣으면 빌드는 통과하고 **런타임 `XamlParseException`** 이 난다
- ⚠ 옛 독립 억제창(`Views/Panels/EventSuppressionSchedulePanelView`)이 **아직 살아 있다**(호스트가 직접 띄운다). 그래서 콘솔 쪽 자동화 식별자는 `Console.Suppression.*` 로 이름을 갈라 놓았다

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
dotnet test Ironwall.Dotnet.Libraries.Utils.Tests/Ironwall.Dotnet.Libraries.Utils.Tests.csproj
dotnet test Ironwall.Dotnet.Libraries.Accounts.Ui.Tests/Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.csproj
```

> ⚠ **테스트가 어디 있는지가 프로젝트마다 다르다.** `Utils` · `Accounts.Ui` · `ViewModel` · `Theme` 만 별도 `*.Tests.csproj` 를 갖고,
> `Devices.Ui` · `Events.Ui` · `Messages` · `Reports.Ui` 는 **제품 csproj 안 `Tests/` 폴더**에 테스트를 둔다(`dotnet test` 를 제품 csproj 에 바로 건다).
> `GMaps.Db` 만 `-p:IncludeTests=true` 게이트가 따로 있다.
>
> ⚠ **솔루션 빌드는 WPF 임시 프로젝트(`_wpftmp`)를 타지 않아** `*.Ui` 의 `CS0535`(인터페이스 미구현)를 놓친다.
> 장비 계열을 손댔으면 `tools/build-device-console-projects.ps1` 로 프로젝트별 14개를 따로 빌드한다(`-Test` 를 주면 테스트까지).
>
> ⚠ 전체 스위트는 이 저장소 실측 **약 461초**다. 하네스 기본 타임아웃(240초)으로는 **항상** INFRA 타임아웃이 난다.

**테스트 현황** (통합 브랜치 `v2.14.0` @ **`093ca377`** 실측 — N-04 ~ N-13 전부 머지된 상태, 2026-09-21)

| 프로젝트 | 통과 | 실패 |
|---|---|---|
| `Devices.Ui` | **1288** | 0 |
| `Events.Ui` | **1138** | **15**(전부 이번 작업 이전부터 깨져 있던 것) |
| `Messages` | **374** | 0 |
| `Utils.Tests` | **193** | 0 |
| `Reports.Ui` | **166** | 0 |
| `Accounts.Ui.Tests` | **148** | 0 |
| **합계** | **3307** | **15** |

`Events.Ui` 의 실패 15건은 네 덩어리다 — 시각 직렬화 `Z` vs `+00:00` 4건 · PIDS FOV 줌 환산 6건 · NATS 탐지 발행 1건 · **XAML 을 글자로 읽어 단언하는 시험** 4건(이름만 바꿔도 깨진다). 전부 제품 변경을 따라오지 못한 단언이거나 구조적으로 무른 시험이다. **N-08 머지 전후로 집합이 바뀌지 않았다.**

### 패키지 게시 (내부용)

```bash
dotnet pack Ironwall.Dotnet.Libraries.Base.csproj --configuration Release --output nupkgs
```

## 변경 이력 (Version History by Branch)

> 각 버전은 git branch 기준으로 정리되었습니다. 최신 버전이 상단에 위치합니다.

---

### [Unreleased] — v2.6.2 (2026-05-22 기준)

**전 창 콘솔 재구성 N-04 ~ N-14 — 어디에 무엇이 있나** (봉투 all-windows-console-redesign · 통합 브랜치 `v2.14.0`, 2026-09-21 · **실기 전부 미검증** — 앱을 띄우지 않았고 운영 서버에 요청 0)

*내일 이 코드를 처음 여는 사람을 위한 지도다. 무엇이 왜 그렇게 돼 있는지는 [CHANGELOG](CHANGELOG.md) 의 노드별 항목에 있다.*

| 창 | 노드 | 프로젝트 · 폴더 | View / ViewModel |
|---|---|---|---|
| 장비 셋업 · 결선맵 | N-04 | `Devices.Ui/Consoles/Wiring/` (View + VM 동거) | `WiringView` / `WiringViewModel : Screen, IDragDropHandler` |
| 다이얼로그 가족 T4 | N-05 | `Utils/Console/Dialogs/` | `ConsoleDialogFrame`(컨트롤 · 뷰모델 없음) |
| 계정 콘솔 | N-06 | `Accounts.Ui/Views\|ViewModels/Panels/` + `Consoles/` | `AccountConsolePanelView` / `AccountConsolePanelViewModel` |
| 이벤트 콘솔 | N-07 | `Events.Ui/Views\|ViewModels/Dashboards/` + `Consoles/{Overview,Lists,Detail,Tray}` | `EventDashboardView` / `EventDashboardViewModel` |
| 억제 스케줄 | N-08 | `Events.Ui/Consoles/Suppression/` + `Views/Consoles/` | `SuppressionListView` · `SuppressionDrawerView` · `SuppressionDetailView` / `SuppressionConsoleViewModel` · `SuppressionDrawerViewModel` |
| 보고서 콘솔 | N-09 | `Reports.Ui/Views\|ViewModels/Panels/` + `Consoles/{Lists,Preview,Templates}` | `ReportConsoleView` / `ReportConsoleViewModel` |
| 설정 창(T2) | N-10 | **호스트 앱** `Dotnet.Monitoring.Solution` (브랜치 `n10`) | 라이브러리는 커널만 빌려준다 |
| 부대 콘솔 | N-11 | `Devices.Ui/Consoles/Units/` (View + VM 동거) | `UnitConsoleView` / `UnitConsoleViewModel : Screen` |
| 서버 모니터 | N-12 | `Devices.Ui/Consoles/Servers/` + `Devices.Api/Servers/`(판본 통로) | `ServerMonitorView` / `ServerMonitorViewModel : Screen` |
| 이벤트 맵핑 워크벤치 | N-13 | `Events.Ui/Consoles/Mapping/` (View + VM 동거) + `Messages/Dto/Integrations/` | `MappingWorkbenchView` / `MappingWorkbenchViewModel : Screen, IDragDropHandler` |
| 셸 표면(창 이동 · 크기 · 자리 기억) | N-14 | **호스트 앱** (브랜치 `n14`) | 라이브러리 숙제는 `N14-library-follow-up.md` |

> N-06 · N-07 · N-09 는 **기존 파일을 다시 쓴 것**이라 "새로 생긴 파일" 로 찾으면 안 보인다.
> 호스트가 이름으로 잡는 **뷰 · 뷰모델 클래스 이름은 동결**돼 있다 — `AccountConsolePanelViewModel` · `EventDashboardViewModel` · `ReportConsoleViewModel` · `DeviceDashboardViewModel`.

**콘솔 커널을 어떻게 쓰나** (`Utils/Console/` · 네임스페이스 `…Utils.Consoles`)

- **틀**: `ConsoleShell` 이 슬롯 넷(Rail · Toolbar · List · Detail)과 StatusBar 를 받는다. 치수(머리 40 · 레일 184/56 · 툴바 48 · 행 38 · 상태 띠 30 · 상세 340, 300~480)와 폭별 도킹/서랍/접힘은 **`ConsoleLayoutMath` 한 곳**이 정한다 — 창이 치수를 다시 쓰지 않는다. **1280 이상 도킹 / 960~1279 상세 서랍 / 960 미만 레일 56.**
- **레일**: `ConsoleRail` 에 `ConsoleRailEntry` 를 바인딩한다(`Tag` 문자열 switch 금지). 항목 `AutomationId` 는 커널이 **`Console.{ConsoleKey}.Rail.{항목키}` 로 강제**한다 — 한 요소에 `AutomationId` 는 하나뿐이라 창 고유 이름을 같이 둘 수 없다.
- **툴바**: `ConsoleToolbar` — 꺼진 버튼은 **늘 사유 툴팁**을 갖는다. ⚠ **`CanDelete=False` 는 버튼을 감추지 못한다**(`ShowDelete` DP 가 없다) — 삭제가 없는 콘솔에는 죽은 버튼이 남는다. `⋯` 오버플로 슬롯도 없다.
- **상세 + 적용 막대**: `ConsoleDetailHost`(여섯 상태 겉모습). **적용 막대는 별도 타입이 아니라 이 컨트롤의 바닥**이다 — 템플릿 부품 `PART_Apply` / `PART_Revert` / `PART_Footer`, 라우티드 이벤트 `ApplyClick` / `RevertClick`, DP `IsDirty` · `CanApply` · `CanRevert` · `ApplyText`(기본 `"적용"`) · `RevertText`(`"되돌리기"`) · `ShowButtons` · `FooterText` · `ShakeToken`(220ms 흔들기, `SystemParameters.ClientAreaAnimation` 존중). **`ConsoleKey` 를 주면** `Console.{키}.Detail.Apply` / `.Revert` / `.ApplyBar` / `.Message` 가 자동 생성된다.
- **빈 상태**: `ConsoleEmptyState` — DP 넷뿐이다. `Glyph`(`Geometry`, 기본은 얇은 외곽 상자) · `Title`(**무엇이** 없는가) · `Hint`(**다음에 무엇을** 하나 — 비면 숨는다) · `Action`(단추 하나 슬롯 — 비면 숨는다). **기본 스타일은 암시적이라 `Generic.xaml` 에 그대로 있다 — 앱 스코프 병합이 필요 없다.** 빈 목록을 `TextBlock` 으로 겹쳐 놓지 말고 이것을 쓴다(네 콘솔이 잉크 0.0% 로 렌더되던 결함이 여기서 나왔다).
- **공유 스타일은 `Theme` 프로젝트로 옮겼다**: **`Ironwall.Dotnet.Libraries.Theme/Themes/Styles.Console.xaml`**(23키 · **전부 키 있는 스타일, 암시적 0**). 소비자는 **사전 하나만** 병합하면 된다 —
  ```xml
  <ResourceDictionary Source="pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Theme.Current.xaml" />
  ```
  (그 안에서 `Styles.Console.xaml` 을 끌어온다. 병합 순서는 `MD3.Defaults` → `Theme.Current`.) 뷰는 `{StaticResource Console.Button}` 처럼 쓰고, **커널 템플릿은 `{DynamicResource}`** 로 당긴다.
  키: `Console.Button` / `.Primary` / `.Ghost` / `.Mini` · `Console.SearchBox` · `Console.CheckBox` · `Console.ToggleSwitch` · `Console.ScrollBar`(+`.Page` `.Thumb`) · `Console.ScrollViewer` · `Console.DataGrid`(+`.ColumnHeader` `.Cell` `.Cell.Flush` `.Row`) · `Console.ReorderList` · `Console.Pill`(+`.Normal` `.Warning` `.Critical` `.Info` `.Dot`).
  **왜 옮겼나**: `Generic.xaml` 은 **테마 사전**이라 다른 어셈블리의 뷰가 `StaticResource` 로 닿지 못한다 — 콘솔들이 조용히 MDIX 암시 스타일로 떨어져 Teal500 `#009688` 이 샜다(장비 콘솔 렌더 4장에서 6324픽셀). 반대 방향도 막혀 있다: `Generic.xaml` 안의 `StaticResource` 는 `Application.Resources` 를 못 보고 **오류 없이 `UnsetValue`** 를 돌려준다.
  ⚠ **`Console.EmptyState` 는 스타일 키가 아니다** — 같은 이름의 `AutomationId` 만 있다(`Generic.xaml:942`). 승격 커밋 메시지가 이 한 줄을 잘못 적었다.
  ⚠ 두 사전이 같은 키를 들면 **조용히 갈린다** → `Utils.Tests/ConsoleStyleContractTests` 가 빌드 단계에서 잡는다(커널이 당기는 키가 Theme 에 실재하는가 · 두 곳에 중복 정의가 없는가 · 커널이 공유 키를 `StaticResource` 로 당기지 않는가 · 꺼진 상태에 `Opacity` 를 쓰지 않는가).
- **드래그 커널**(`Utils/Behaviors/Drag/`, OLE 미사용): 목록에 `CaptureDragBehavior`, 행 안에 `DragHandle`(`Thumb`), 놓을 곳에 `DropZone.Key`(+ `DropZoneChrome`), 순서 목록에는 `DropZone.IsReorder` + `ReorderKeyboardBehavior`. 판정 · 처리는 창이 `IDragDropHandler` 로. **호출 1회면 즉시 전송, N회로 번지면 `DraftTrayViewModel` 에 쌓았다가 [적용]**(진행률 · 부분 실패 4분류 · 되돌리기는 서버 호출 0). `KeyboardFallback` 이 비면 **디버그 빌드에서 예외** — 드래그 전용 UI 는 만들지 않는다.
- **커널에 아직 없는 것**(창마다 지역으로 지었다 · 승격 후보): **T2 틀**(레일 + 섹션 폼 + 저장 막대 — `ConsoleShell` 은 800폭에서 레일을 56으로 접어 설정 창에 못 쓴다) · **780 오버레이 서랍**(억제) · **트리 목록**(부대) · `ConsoleField.ErrorText` · `ConsoleToolbar.DeleteText`/필터 칩 슬롯 · `DraftTrayViewModel` 의 "첫 실패에서 멈춤" · `Console.ReorderList` 의 **컨테이너 스타일이 인라인이라** 행에 `AutomationId` 를 달려고 `ItemContainerStyle` 을 주면 선택 바 · Stretch · 포커스 링이 **통째로 사라진다**(지금은 템플릿을 옮겨 적어 쓴다).
- **커널에 남아 있는 시각 결함 둘**(`093ca377` 기준 미수정 · 수정 진행 중): ① `ConsoleDetailHost` 의 스크롤 내용 여백이 `Margin="16,4,16,24"` 라 **상세 칸 마지막 문단이 적용 막대에 2~4px 까지 붙는다**(잘리지는 않는다) ② **적용 막대의 이동 차단 안내가 잘린다** — 버튼 둘이 자리를 먹고 남는 폭이 좁으면 약 **124px** 인데 커널 문장은 약 **190px** 를 쓴다. 두 줄까지 접은 뒤 `TextTrimming` 이 먹어 "적용하거나 되돌린…" 으로 끝난다. **창 쪽에서 문구를 줄여 피하지 말고**(계정 콘솔이 그렇게 했다) 커널이 고쳐질 때 되돌릴 수 있게 그 사실을 주석으로 남긴다.

**화면을 보려면 — 오프라인 미리보기 도구** (앱도 서버도 띄우지 않는다. `--snapshot` 을 주면 입력 없이 PNG 를 뜨고 종료한다. 실패하면 MessageBox 대신 그 폴더에 `snapshot-error.txt` 를 남긴다.)

| 도구 | 실행 | 스위치 |
|---|---|---|
| `tools/device-console-preview` | `dotnet run --project tools/device-console-preview -- <모드>` | 모드(**먼저 걸리는 하나만 실행**): `--assembly` · `--units` · `--dialogs` · `--wiring` · `--servers`(없으면 장비 콘솔) / 공통: `--snapshot <폴더>` · `--dark` · `--legacy` |
| `tools/events-console-preview` | `dotnet run --project tools/events-console-preview` | `--mapping` · `--suppression` · `--snapshot <폴더>` · `--dark` |
| `tools/reports-console-preview` | `dotnet run --project tools/reports-console-preview` | `--snapshot <폴더>` · `--dark` · `--readonly`(권한 없는 화면) |
| `tools/accounts-console-preview` | `dotnet run --project tools/accounts-console-preview` | `--snapshot <폴더>` · `--dark`(스냅샷 스윕은 두 테마를 모두 돈다) |
| `tools/console-gallery` | `dotnet run --project tools/console-gallery` | `--snapshot <폴더>`(상태별 PNG + 드래그 재현 로그) · **`--matrix <폴더>`**(전 상태 × 두 테마 매트릭스 + 셀 좌표 JSON) · `--legacy`(`--matrix` 안에서만 — v2.14.0 이전 토큰을 덮어 "전/후"를 같은 무대에서 잰다) |
| `tools/build-device-console-projects.ps1` | `powershell -File tools/build-device-console-projects.ps1 [-Test]` | 장비 계열 14개 프로젝트를 **하나씩** 빌드(솔루션 빌드가 WPF `_wpftmp` 를 안 타서 `CS0535` 를 놓친다) |

> 미리보기가 **드래그를 실제로 재현하는지** 확인하고 쓸 것. N-13 에서 스냅샷 스크립트가 드래그를 건너뛰고 메서드를 직접 부르는 바람에, **드롭이 핸들러에 한 번도 닿지 않는 결함이 정상으로 보이는 스크린샷 18장 뒤에 숨어 있었다.**
> 설정 창 · 셸 표면 미리보기(`tools/settings-preview` · `tools/shell-surface-preview`)는 **호스트 저장소**에 있다.

**물린 규칙 — 이것들을 어기면 빌드는 통과하고 화면만 조용히 깨진다**

1. **`DataGrid` 열마다 `CellStyle` 을 명시한다.** `Console.DataGrid` 가 그리드 수준에서 `CellStyle` 을 걸어도, 열이 자기 `CellStyle` 을 지정하지 않으면 **MaterialDesign 의 암시적 `DataGridCell` 스타일이 이긴다** — 선택 행이 MDIX 회색(`#D8D8D8` 라이트 / `#757575` 다크)으로 갈라지고, 커널의 3px 선택 바가 그 위에서 **1.96:1** 이 된다. **코드로 만든 열은 특히** 잊기 쉽다. 핸들 열은 `Console.DataGrid.Cell.Flush`(패딩 0) — 그냥 두면 `Padding="12,0"` 이 26px 핸들 열을 통째로 지운다.
2. **컨트롤에 content 로 넘긴 `TextBlock` 은 색을 상속받지 못할 수 있다.** 템플릿의 `ContentPresenter` 가 `TextElement.Foreground` 를 걸어도, `Header` 로 넘긴 `TextBlock` 은 **템플릿이 아니라 논리 부모**(예: `ConsoleField`)를 통해 상속한다. 아무도 색을 주지 않으면 **순수 검정**이 되고, 다크에서 `#000000` on `#161D26` = **1.24:1** — 사실상 투명이다(라이트는 같은 검정이 밝은 판 위라 19.2:1 로 우연히 살아남아 **한쪽 테마에서만 터진다**). `Foreground` 를 **명시**하고, 창 루트에 `TextElement.Foreground="{DynamicResource TextPrimaryBrush}"` 를 깔아 빠뜨려도 토큰에 떨어지게 한다.
3. **드롭존은 `ItemsControl` 위에 있어야 한다.** 커널은 드롭존이 `ItemsControl` 일 때만 삽입 위치를 계산하고(`CaptureDragBehavior.cs:205`) 그 인덱스가 음수면 후보로 올리지 않는다(`:218`). `DropZoneChrome`(= `ContentControl`)이나 `Border` 에 붙이면 **파선 윤곽은 계속 떠서 살아 있어 보이는데 드롭이 핸들러에 닿지 않는다.** `ReorderKeyboardBehavior` 도 키를 목록에서 읽으므로 `Alt+↑↓` 가 **같이** 죽는다. 세 첨부 속성(`DropZone.Key` · `.IsReorder` · `.State`)은 `ListBox`/`ItemsControl` 에 건다 — 가라앉은 면은 그 뒤의 `Border` 가 맡는다.
4. **`AutomationId` 는 peer 가 실재하는 요소에만 붙는다.** `TextBlock` · `Border` · `ContentControl`(`DropZoneChrome` 포함) · `Grid` · `StackPanel` · `md:PackIcon` 은 **UIA 트리에 나오지 않는다** — 붙여도 자동화가 못 찾고, 실제로 그 상태의 호스트 페이지 오브젝트가 **폐기 예정인 옛 패널을 조용히 몰고 있었다.** 단언할 값은 테두리 없는 **읽기 전용 `TextBox`**(`ValuePattern` 까지 온다) · `Label` · `ListBoxItem` · `Button` 으로 낸다.
5. **한 뷰를 두 노드가 고치면 병합은 텍스트 작업이 아니라 설계 판단이다.** N-08(억제 레일)이 내려앉은 `EventDashboardView(Model)` 은 그 사이 N-07 의 적대 검토 수정 · N-13 의 맵핑 입구 · 시각 교정이 **각각 고쳐 놓은 상태**였다. 충돌난 다섯 자리를 손으로 풀었고, 규칙은 **"어느 한쪽을 통째로 택하지 않는다"** 였다 — 툴바는 [중단] · [맵핑] · [정리] 를 **전부** 남기고, 목록 자리는 빈 상태와 억제 목록을 **둘 다** 들고, `ListStatusText` 는 기존 형태를 지킨 채 분기만 앞에 더한다. 가장 중요한 한 자리: **`Delete()` 는 미적용 문지기를 억제 분기보다 먼저 지난다** — 어느 한쪽을 통째로 택했다면 삭제가 미적용 편집을 **조용히 버렸을 것**이다. 공유 뷰를 건드리는 노드가 둘 이상이면, 병합 전에 **무엇을 잃으면 안 되는지**를 먼저 적고 시작한다.
6. 그 밖에 — **`x:Name` 은 건드리지 않는다**(이 앱에서 `x:Name` 은 Caliburn 바인딩 지시자다. 계측은 `AutomationProperties.AutomationId` 만) · **색 토큰은 매번 재해석**한다(`DynamicResource`. 1회 캐싱 · `static readonly` Frozen 은 테마 전환 때 옛 색으로 굳는다) · **바인딩된 목록을 `Clear()+Add()` 로 다시 채우지 않는다**(선택과 상세가 풀린다 — `Insert`/`Move`/`Remove` 로 맞춘다) · **요소에 로컬 값을 쓰면 Style 트리거가 진다**(알약의 지역 `Foreground` 하나가 커널의 대비 수정을 통째로 무효로 만들었다) · **한글은 음절 단위로 감긴다** — 마지막 줄이 한 글자만 남으면 문구를 **짧게 고치는 것**이 유일한 결정적 해법이다(`TextWrapping="WrapWithOverflow"` 를 쓴다).

**콘솔 커널 — 전 창 재구성의 공용 토대** (봉투 all-windows-console-redesign N-01 · 브랜치 `v2.11.0` · 2026-09-19 · 기존 창 무변경 · 실제 입력 미검증)
- 왜: 맵 OverlayWindow 를 뺀 전 창을 와이어프레임 구조(레일 · 목록 · 상세 + 드래그 관리)로 다시 짠다. 그 틀과 드래그 규약을 창마다 복사하지 않도록 먼저 공용 자산으로 세웠다
- 틀: `Ironwall.Dotnet.Libraries.Utils.Consoles` — `ConsoleShell`(슬롯 Rail · Toolbar · List · StatusBar · Detail) · `ConsoleRail` · `ConsoleToolbar` · `ConsoleDetailHost` · `ConsoleSection` · `ConsoleField` · `NotReceivedBox` · `ConsoleColumns` · `ConsolePrefs` · `ConsoleLayoutMath`. 기본 템플릿은 `Utils/Themes/Generic.xaml`, 목록 스타일은 Theme 의 `Styles.Console.xaml`(`Console.DataGrid`)
- 드래그: `Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag` — 목록에 `CaptureDragBehavior`, 행 안에 `DragHandle`, 놓을 곳에 `DropZone.Key`(+ `DropZoneChrome`), 순서 목록에는 `DropZone.IsReorder` + `ReorderKeyboardBehavior`. 판정 · 처리는 창이 `IDragDropHandler` 로. 호출 1회면 즉시, N회로 번지면 `DraftTrayViewModel` 에 쌓았다가 [적용]. **`KeyboardFallback` 이 비면 디버그 빌드에서 예외** — 드래그 전용 UI 는 만들지 않는다
- 써 보기: `tools/console-gallery` 에서 `dotnet run`(가짜 데이터 · 서버 호출 0). `--snapshot <폴더>` 를 주면 입력 없이 상태별 PNG 와 드래그 재현 로그를 뜬다

**부품 조립기 · 프리셋 · 프리셋으로 등록** (device-assembly-preset — 봉투 N-03 · 브랜치 `v2.13.0`, 2026-09-19 · 실제 마우스 끌어 놓기 · 호스트에서 창 열기 · 서버 쓰기는 미확인)
- 어디: `Devices.Ui/Consoles/Assembly/**` — `Model`(보드 · key 규칙 · 펼치기 · 차이, 순수) · `Catalog`(`IComponentCatalog` — `CatalogService` 가 같이 구현, 가족 규칙표) · `Blocks`(`ComponentBlock` 형태 5종) · `Presets`(파일 저장소) · `Register`(등록 POST 1건 · 적용 PATCH 1건) · 창 뷰모델/뷰 6쌍 · `AssemblyLauncher`(`IWindowManager` 로 연다)
- 입구는 장비 콘솔에 있다(툴바 · 상세 칸). 7.0+ 서버에서만 보인다
- 규칙 셋: ① 조립은 끝까지 Draft — 서버 호출은 마지막 한 번 ② 유형 공통 사실(states · commands …)은 보여 주기만, 본문에 절대 없다 ③ 부품을 빼면 그 key 의 재정의를 `null` 로 같이 보낸다
- 새 부품 유형이 카탈로그에 생기면 코드 변경 없이 팔레트에 나온다. 블록 형태는 `ComponentFamilyRules` 의 규칙표가 정한다(모르면 기타)
- 써 보기: `tools/device-console-preview` 에서 `dotnet run -- --assembly`
- PRD: `docs/prds/device-assembly-preset-prd.md` · 결과: `docs/tests/device-assembly-preset-test-result.md`

**장비 콘솔** (device-console-redesign — 봉투 N-02 · 브랜치 `v2.12.0`, 2026-09-19 · 호스트 앱 안 표시 · 실제 마우스 끌어 놓기 · 살아 있는 서버 쓰기는 미확인)
- 화면: `DeviceDashboardView` 가 콘솔 커널(`ConsoleShell`) 위의 3단 — 레일(그룹 + 7 카테고리, 축 계약이면 + 부품으로 찾기 · 배지 `▲장애 · 합계`) · 목록(그리드 하나, 열은 `DeviceColumnCatalog` 에서 생성) · 상세(고정 적용 막대). 호스트는 그대로다(뷰모델 이름 유지)
- **속성을 고치려면 명세에 한 줄**: `Consoles/Properties/DevicePropertyCatalog` — 키 · 라벨 · API 경로 · 절 · 편집기 · 쓰기 가능 여부(+ 못 쓰는 까닭) · 카테고리 · 계약 세대. 폼(`Consoles/Forms`)은 명세에서 만들어진다 — 장비 종류마다 폼을 손으로 짜지 않는다. 뷰모델 속성과 어긋나면 `DevicePropertyCatalogTests` 가 잡는다
- 저장 경로는 하나: 콘솔은 전송하지 않는다. `IDeviceConsoleSource` 가 패널 뷰모델의 기존 저장 · 삭제 · 재조회를 부른다. 패널의 버튼 경로는 `async void` 라 **시작 여부**(`Save()` 의 반환)와 **끝남**(`BusyEnded`, UI 스레드로 옮겨 울림)만 믿는다
- 드래그: 장비 → 그룹 칩(`Consoles/Groups` — 그룹당 호출 1회 · 되돌리기 · 칩 클릭이 키보드 폴백). 판정은 순수 함수 `DeviceGroupDrop.Plan`
- 설정 축 쓰기 통로: DTO 의 `DeviceConfigWrite` + `AllowDeviceConfigWrite`(기본 꺼짐 — 꺼지면 본문이 글자 하나까지 같다)
- 써 보기: `tools/device-console-preview` 에서 `dotnet run`(진짜 뷰 + 진짜 뷰모델 · 가짜 데이터 · 서버 호출 0). `--snapshot <폴더>` · `--legacy` · `--dark`
- PRD: `docs/prds/device-console-redesign-prd.md` · 결과: `docs/tests/device-console-redesign-test-result.md`

**장비 관리 창 v8 축 전환 1차** (device-console-v8 — 브랜치 `v2.10.0` · Phase 1(기반) v2.6 머지 `19c6eaae`, 2026-09-19 · 화면 구현 완료 · 실기(화면) 미검증)
- GOP API v7.0+ 표현 모델 수용: 판별자 `category_device`(경로가 정본) + 종류축 `type_<category>` 원값 보존(`TypeAxisCode`) + 표현 축 묶음 `IBaseDeviceModel.Axes`(접속·형상/부품·관측·의도 + `meta.view/sections`). 읽기는 DTO 재조립본이 아니라 **수신 원본**에서(`DeviceAxesMapper`)
- **축 계약 저장은 `PATCH`** — 8.0.1 실측: `PUT` 은 축을 통째 교체해 `connection.type/channel` 과 카메라 `components` 를 무경고로 지운다. 6.3 은 종전 `PUT`(무회귀). `components` 는 `AllowComponentsWrite` 를 켠 호출부만 전송
- 통문(gate): Api CRUD 3종 · `GateDeviceProvider` · 위치 저장 · 재조회/`SYNC_DEVICE` 판별자 라우팅(병합 키 `(Id, 판별자)`)
- `ICatalogService`(어휘 카탈로그 `/api/devices/spec` 캐시 — 종류축 콤보의 단일 원천) · 계약 게이트 + "서버 판본 미확정" 배너 · 공용 `DeviceTypeResolver`(Events.Ui·Devices.Ui)
- 6.3.2 운영 무회귀 스냅샷(`LegacyContractSnapshotTests`) · 쓰기 본문 가드(`DeviceWriteBodyGuardTests`)
- 화면: 통문 패널(7번째 탭) · 7 패널 "종류" 열 + 카탈로그 필터 콤보(`TypeAxisPanelSupport`) · 상세 9절(`DeviceAxisSectionsViewModel` — 미수신과 빈 값을 구분, 전부 읽기 전용)
- 이번에 열지 않은 것: 접속 방식·상위 장비·채널 편집, 부품 편집(서버가 `components` 를 통째 교체하므로 조립기 화면의 몫)
- PRD: `docs/prds/device-console-v8-prd.md` · 실측: `docs/tests/device-console-v8-verification.md`

**PidsGroup 우클릭 — 등록 센서 정보 오버레이 + 그룹 탐지 이력** (pidsgroup-rightclick — 구현 완료 · v2.6 머지 `6624d4d` + 메인솔루션 배선 `d0a19c1`, 2026-08-06 · 런타임 육안 미검증)
- 맵 구역(PidsGroup) 심볼 우클릭 메뉴 신설: [등록 센서 정보] / [그룹 탐지 이력] (미연결·빈 그룹=disable+ToolTip)
- 등록 센서 정보 오버레이 윈도우(드래그·상태 배지 4종·센서 테이블·행 우클릭→센서 탐지 이력/지도 위치 확인) — 서버 변경 0(DeviceProvider 역필터)
- 그룹 탐지 이력 = 기존 탐지 이력 다이얼로그의 그룹 모드: 멤버 센서 팬아웃(총합 500 절단)·센서별 멀티 시리즈 차트(신규 토큰 ChartSeries1~3Brush)·센서 필터 칩·센서 컬럼·최다 발생 센서 통계 + 장비정보 그룹 설정 탭 행 우클릭 진입
- 설계: `docs/design/pidsgroup-rightclick-storyboard.html` · PRD: `docs/prds/pidsgroup-rightclick-prd.md`

**탐지 신호 이력** (Detection_Signal_History — 구현 완료 · v2.6 머지 `736207e` + 메인솔루션 배선, 2026-07-23)
- 탐지 이벤트 신호 크기(`detail.signal`) 표면화: 이력 그리드 신호 컬럼 + 실시간 카드 병기 (Message Type 표시는 중복이라 제거)
- 센서 우클릭(맵 심볼 / DevicePanel 그리드) → 탐지 신호 이력 팝업 다이얼로그(기간 프리셋·시간축 차트·필터/통계·조치보고 연계)
- 설계: `docs/design/Detection_Signal_History_Storyboard.html` · PRD: `docs/prds/Detection_Signal_History-prd.md`

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
