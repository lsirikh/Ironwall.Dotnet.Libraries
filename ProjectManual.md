# Ironwall.Dotnet.Libraries Project Manual

> **Sensorway Framework - 통합 라이브러리 매뉴얼**
> Version: 1.3.1
> Company: Sensorway Co., Ltd.
> Department: SW Team
> Primary Developer: GHLee (lsirikh@naver.com)

---

## 목차 (Table of Contents)

1. [개요 (Overview)](#개요-overview)
2. [아키텍처 개요 (Architecture Overview)](#아키텍처-개요-architecture-overview)
3. [프로젝트 구조 (Project Structure)](#프로젝트-구조-project-structure)
4. [MVVM 패턴 구현 (MVVM Pattern Implementation)](#mvvm-패턴-구현-mvvm-pattern-implementation)
5. [핵심 라이브러리 (Core Libraries)](#핵심-라이브러리-core-libraries)
6. [도메인 라이브러리 (Domain Libraries)](#도메인-라이브러리-domain-libraries)
7. [데이터 액세스 패턴 (Data Access Patterns)](#데이터-액세스-패턴-data-access-patterns)
8. [의존성 그래프 (Dependency Graph)](#의존성-그래프-dependency-graph)
9. [개발 가이드 (Development Guide)](#개발-가이드-development-guide)
10. [기술 스택 (Technology Stack)](#기술-스택-technology-stack)

---

## 개요 (Overview)

Ironwall.Dotnet.Libraries는 보안 관제 및 모니터링 시스템을 위한 포괄적인 MVVM 기반 WPF 프레임워크입니다.
총 31개의 모듈화된 .NET 8.0 프로젝트로 구성되어 있으며, 디바이스 관리, 이벤트 처리, 지도 시각화, 비디오 스트리밍 등의 기능을 제공합니다.

### 주요 특징

- **MVVM 아키텍처**: Caliburn.Micro 기반의 체계적인 MVVM 패턴
- **모듈화 설계**: 도메인별 독립적인 라이브러리 구조
- **의존성 주입**: Autofac을 통한 완전한 DI 컨테이너
- **3계층 아키텍처**: Business Logic → Data Access → Presentation
- **스레드 안전성**: DispatcherService와 컬렉션 동기화
- **확장 가능성**: 인터페이스 기반 설계로 쉬운 확장

---

## 아키텍처 개요 (Architecture Overview)

### 계층 구조 (Layered Architecture)

```
┌─────────────────────────────────────────────────────────────┐
│                     Presentation Layer                      │
│  (*.Ui Projects: ViewModels, Views, Behaviors, Controls)    │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                      Business Logic Layer                   │
│     (Domain Projects: Models, Providers, Services)          │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                     Data Access Layer                       │
│      (*.Db Projects: DbServices, Repositories)              │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                       Foundation Layer                      │
│  (Base, Framework, Enums, Models, ViewModel)                │
└─────────────────────────────────────────────────────────────┘
```

### 프로젝트 명명 규칙 (Project Naming Convention)

| Pattern | Purpose | Example |
|---------|---------|---------|
| `Ironwall.Dotnet.Libraries.{Domain}` | 비즈니스 로직 | `Ironwall.Dotnet.Libraries.Devices` |
| `Ironwall.Dotnet.Libraries.{Domain}.Db` | 데이터베이스 계층 | `Ironwall.Dotnet.Libraries.Devices.Db` |
| `Ironwall.Dotnet.Libraries.{Domain}.Ui` | 프레젠테이션 계층 | `Ironwall.Dotnet.Libraries.Devices.Ui` |
| `Ironwall.Dotnet.Libraries.{Domain}.Base` | 기본 클래스 | `Ironwall.Dotnet.Libraries.Streaming.Base` |
| `Ironwall.Dotnet.Framework.*` | 프레임워크 핵심 | `Ironwall.Dotnet.Framework.Models` |
| `Ironwall.Dotnet.Monitoring.*` | 모니터링 도메인 모델 | `Ironwall.Dotnet.Monitoring.Models` |

---

## 프로젝트 구조 (Project Structure)

### 전체 프로젝트 목록 (Complete Project List)

#### 1. 핵심 기반 프로젝트 (Core Foundation)

| Project | Purpose | Key Components |
|---------|---------|----------------|
| **Ironwall.Dotnet.Libraries.Base** | 기반 인프라 라이브러리 | `BaseProvider<T>`, `EntityCollectionProvider<T>`, `ParentBootstrapper<T>`, `IService`, `LogService`, `DispatcherService` |
| **Ironwall.Dotnet.Framework** | 확장 프레임워크 | `EventHelper`, `IdCodeGenerator`, `DateTimeHelper`, `FileManager`, `PasswordHelper`, `TokenGenerator` |
| **Ironwall.Dotnet.Framework.Models** | 통신 모델 및 DTO | `IAccountRequestModel`, `IDeviceDataModel`, `IEventDataModel` |
| **Ironwall.Dotnet.Monitoring.Models** | 도메인 모델 | `BaseDeviceModel`, `CameraDeviceModel`, `BaseEventModel`, `DetectionEventModel` |
| **Ironwall.Dotnet.Libraries.Enums** | 공유 열거형 | `EnumDeviceType`, `EnumEventType`, `EnumAccountLevel` |
| **Ironwall.Dotnet.Libraries.ViewModel** | ViewModel 기반 클래스 | `BaseViewModel<T>`, `ConductorAllViewModel`, `ConductorOneViewModel` |

#### 2. 디바이스 도메인 (Devices Domain)

| Project | Purpose | Key Components |
|---------|---------|----------------|
| **Ironwall.Dotnet.Libraries.Devices** | 디바이스 비즈니스 로직 | `DeviceProvider`, `CameraDeviceProvider`, `ControllerDeviceProvider`, `SensorDeviceProvider` |
| **Ironwall.Dotnet.Libraries.Devices.Db** | 디바이스 데이터베이스 | `DeviceDbService`, `IDeviceDbService` |
| **Ironwall.Dotnet.Libraries.Devices.Ui** | 디바이스 UI | `CameraDeviceViewModel`, `CameraDeviceView`, `CameraPresetsView` |

#### 3. 이벤트 도메인 (Events Domain)

| Project | Purpose | Key Components |
|---------|---------|----------------|
| **Ironwall.Dotnet.Libraries.Events** | 이벤트 비즈니스 로직 | `EventProvider`, `ActionEventProvider`, `DetectionEventProvider`, `MalfunctionEventProvider` |
| **Ironwall.Dotnet.Libraries.Events.Db** | 이벤트 데이터베이스 | `EventDbService`, `IEventDbService` |
| **Ironwall.Dotnet.Libraries.Events.Ui** | 이벤트 UI | `EventCardViewModel`, `DetectionEventViewModel`, `ActionEventViewModel` |

#### 4. 지도/GIS 도메인 (Maps/GIS Domain)

| Project | Purpose | Key Components |
|---------|---------|----------------|
| **Ironwall.Dotnet.Libraries.GMaps** | 지도 비즈니스 로직 | `MapProvider`, `SymbolProvider`, `GeometricSymbolProvider`, `MilitarySymbolProvider` |
| **Ironwall.Dotnet.Libraries.GMaps.Db** | 지도 데이터베이스 | `GMapDbService`, `GMapDbSymbolService` |
| **Ironwall.Dotnet.Libraries.GMaps.Ui** | 지도 UI | `GMapView`, `GMapMarkerCustomControl`, `GMapPropertyBaseControl`, Adorners |

#### 5. 스트리밍 도메인 (Streaming Domain)

| Project | Purpose | Key Components |
|---------|---------|----------------|
| **Ironwall.Dotnet.Libraries.Streaming.Base** | 스트리밍 기반 | 스트리밍 인프라 모델 및 프로바이더 |
| **Ironwall.Dotnet.Libraries.Streaming** | 비디오 스트리밍 | LibVLCSharp 통합, `StreamingService`, Polly 재시도 정책 |

#### 6. 계정 도메인 (Accounts Domain)

| Project | Purpose | Key Components |
|---------|---------|----------------|
| **Ironwall.Dotnet.Libraries.Accounts** | 계정 비즈니스 로직 | `AccountSetupModel`, `IAccountSetupModel` |
| **Ironwall.Dotnet.Libraries.Accounts.Db** | 계정 데이터베이스 | 계정 데이터베이스 서비스 |

#### 7. 지원 라이브러리 (Support Libraries)

| Project | Purpose | Key Components |
|---------|---------|----------------|
| **Ironwall.Dotnet.Libraries.Utils** | WPF 유틸리티 | `BoolToInverseVisibleConverter`, `EnumBindingSourceExtension`, `PasswordControl` |
| **Ironwall.Dotnet.Libraries.AdornerDecorator** | Adorner 레이어 | 지도 편집용 Adorner |
| **Ironwall.Dotnet.Libraries.Canvas** | Canvas 기능 | Canvas 관련 기능 |
| **Ironwall.Dotnet.Libraries.Sounds** | 사운드 시스템 | `SoundProvider`, `SoundService` |
| **Ironwall.Dotnet.Libraries.Sounds.Ui** | 사운드 UI | 사운드 관리 UI |
| **Ironwall.Dotnet.Libraries.Redis** | Redis 통합 | Redis 캐싱/메시징 |
| **Ironwall.Dotnet.Libraries.Api** | API 통합 | API 서비스 레이어 |
| **Ironwall.Dotnet.Libraries.Api.Aligo** | Aligo API | Aligo API 구현 |
| **Ironwall.Dotnet.Libraries.OnvifSolution** | ONVIF 프로토콜 | IP 카메라 ONVIF 통합 |
| **Ironwall.Dotnet.Libraries.OnvifSolution.Base** | ONVIF 기반 | ONVIF 기본 기능 |
| **Ironwall.Dotnet.Libraries.Db** | 범용 데이터베이스 | Dapper, EF 유틸리티 |
| **Ironwall.Dotnet.Libraries.Db2** | 추가 데이터베이스 | 추가 DB 유틸리티 |

---

## MVVM 패턴 구현 (MVVM Pattern Implementation)

### MVVM 개요

이 솔루션은 **Caliburn.Micro** 프레임워크를 기반으로 한 순수 MVVM 패턴을 따릅니다.

```
┌─────────────┐         ┌──────────────┐         ┌─────────────┐
│    View     │ ◄────── │  ViewModel   │ ◄────── │    Model    │
│   (XAML)    │  Bind   │  (C# Class)  │  Ref    │ (C# Class)  │
└─────────────┘         └──────────────┘         └─────────────┘
       │                        │                        │
       │                        │                        │
   UserControl             BaseViewModel<T>         IBaseModel
   Window                  Conductor<T>             Domain Models
```

### 1. Model Layer

#### 기본 인터페이스 (Base Interfaces)

**`IBaseModel`** - 모든 모델의 기본 인터페이스
```csharp
public interface IBaseModel
{
    int Id { get; set; }
}
```

**`BaseModel`** - 기본 구현
```csharp
public class BaseModel : IBaseModel
{
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }
}
```

#### 도메인 모델 계층 구조

**디바이스 모델 계층**
```
IBaseModel
    ↓
IBaseDeviceModel (DeviceNumber, DeviceGroup, DeviceName, DeviceType, Version, Status)
    ↓
├─ ICameraDeviceModel (IP, Port, RTSP, PTZ, Presets)
├─ ISensorDeviceModel (Sensor specific properties)
└─ IControllerDeviceModel (Controller specific properties)
```

**이벤트 모델 계층**
```
IBaseModel
    ↓
IBaseEventModel (MessageType, DateTime)
    ↓
├─ IDetectionEventModel (Detection specific)
├─ IActionEventModel (Action specific)
├─ IMalfunctionEventModel (Malfunction specific)
└─ IConnectionEventModel (Connection specific)
```

#### 모델 위치 (Model Locations)

| Model Category | Location | Examples |
|----------------|----------|----------|
| 도메인 모델 | `Ironwall.Dotnet.Monitoring.Models` | `CameraDeviceModel`, `DetectionEventModel`, `AccountModel` |
| 통신 모델 | `Ironwall.Dotnet.Framework.Models` | `IDeviceDataModel`, `IEventDataModel` |
| UI 모델 | `*.Ui/Models/` | `EventCardModel`, `SymbolPropertyModel` |

### 2. View Layer

#### 명명 규칙 (Naming Conventions)

| Type | Pattern | Example |
|------|---------|---------|
| 메인 뷰 | `{Feature}View.xaml` | `CameraDeviceView.xaml` |
| UserControl | `{Feature}Control.xaml` | `GMapMarkerCustomControl.xaml` |
| Window | `{Feature}Window.xaml` | `MainWindow.xaml` |
| Dialog | `{Feature}Dialog.xaml` | `CameraSelectionDialog.xaml` |

#### 폴더 구조 (Folder Structure)

```
Views/
├── {Feature}View.xaml           # 메인 뷰
├── Dashboards/                  # 대시보드 뷰
│   └── {Feature}DashboardView.xaml
├── Dialogs/                     # 모달 다이얼로그
│   └── {Feature}SelectionView.xaml
├── Panels/                      # 패널 컨트롤
│   └── {Feature}PanelView.xaml
└── Components/                  # 재사용 컴포넌트
    └── {Feature}CardView.xaml
```

#### View 기술 스택

- **WPF**: Windows Presentation Foundation (net8.0-windows)
- **MaterialDesignThemes**: Material Design 스타일링
- **MahApps.Metro**: Metro UI 컨트롤
- **LiveChartsCore**: 실시간 차트
- **Custom Controls**: 커스텀 컨트롤 (GMap, Adorners)

#### View 예시

**CameraDeviceView.xaml**
```xml
<UserControl x:Class="Ironwall.Dotnet.Libraries.Devices.Ui.Views.CameraDeviceView"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes">
    <Grid>
        <!-- Caliburn.Micro 자동 바인딩: x:Name="Items" → ViewModel.Items 프로퍼티 -->
        <DataGrid x:Name="Items" />
    </Grid>
</UserControl>
```

### 3. ViewModel Layer

#### 기본 ViewModel 계층 구조

```
Conductor<IScreen> (Caliburn.Micro)
    ↓
BaseViewModel<T> where T : IBaseModel
    ↓
├─ SelectableBaseViewModel
├─ BaseDataGridViewModel
├─ BaseDataGridPanelViewModel
├─ BasePanelViewModel
├─ ConductorAllViewModel (여러 화면 관리)
└─ ConductorOneViewModel (단일 화면 관리)
```

#### **BaseViewModel\<T\>** - 모든 ViewModel의 기본 클래스

**Location**: `Ironwall.Dotnet.Libraries.ViewModel/ViewModels/BaseViewModel.cs`

**핵심 기능**:

```csharp
public abstract class BaseViewModel<T> : Conductor<IScreen>, IBaseViewModel<T>
    where T : IBaseModel
{
    #region - Ctors -
    public BaseViewModel(IEventAggregator eventAggregator, ILogService logService)
    {
        _eventAggregator = eventAggregator;
        _logService = logService;
        _cancellationTokenSource = new CancellationTokenSource();
        _token = _cancellationTokenSource.Token;
    }
    #endregion

    #region - Overrides -
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // EventAggregator 구독 시작
        _eventAggregator?.SubscribeOnUIThread(this);
        return base.OnActivateAsync(cancellationToken);
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        // EventAggregator 구독 해제
        _eventAggregator?.Unsubscribe(this);

        if (close)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        return base.OnDeactivateAsync(close, cancellationToken);
    }
    #endregion

    #region - IHandle<CloseAllMessageModel> -
    public virtual Task HandleAsync(CloseAllMessageModel message, CancellationToken cancellationToken)
    {
        return TryCloseAsync();
    }
    #endregion

    #region - Properties -
    public T Model { get; set; }  // 바인딩할 모델
    #endregion

    #region - Attributes -
    protected readonly IEventAggregator _eventAggregator;
    protected readonly ILogService _logService;
    protected CancellationTokenSource _cancellationTokenSource;
    protected CancellationToken _token;
    #endregion
}
```

#### Conductor 패턴

**ConductorAllViewModel** - 여러 화면 동시 관리
```csharp
public class ConductorAllViewModel : Conductor<IScreen>.Collection.AllActive, IConductorViewModel
{
    public bool IsVisible { get; set; }

    public Task HandleAsync(CloseAllMessageModel message, CancellationToken cancellationToken)
    {
        foreach (var item in Items.ToList())
        {
            await item.TryCloseAsync();
        }
        return Task.CompletedTask;
    }
}
```

**ConductorOneViewModel** - 단일 화면 관리
```csharp
public class ConductorOneViewModel : Conductor<IScreen>.Collection.OneActive, IConductorViewModel
{
    public bool IsVisible { get; set; }
}
```

#### ViewModel 특화 클래스

| ViewModel Type | Purpose | Location |
|----------------|---------|----------|
| `SelectableBaseViewModel` | 선택 가능한 아이템 | `Libraries.ViewModel/ViewModels/SelectableBaseViewModel.cs` |
| `BaseDataGridViewModel` | DataGrid 바인딩 | `Libraries.ViewModel/ViewModels/DataGrids/BaseDataGridViewModel.cs` |
| `BaseDataGridPanelViewModel` | DataGrid 패널 | `Libraries.ViewModel/ViewModels/Panels/BaseDataGridPanelViewModel.cs` |
| `BasePanelViewModel` | 패널 관리 | `Libraries.ViewModel/ViewModels/Panels/BasePanelViewModel.cs` |
| `EventCardViewModel<T>` | 이벤트 카드 | `Libraries.Events.Ui/ViewModels/EventCardViewModel.cs` |

#### ViewModel 예시

**CameraDeviceViewModel**
```csharp
public class CameraDeviceViewModel : BaseViewModel<CameraDeviceModel>
{
    public CameraDeviceViewModel(
        CameraDeviceModel model,
        IEventAggregator eventAggregator,
        ILogService logService)
        : base(eventAggregator, logService)
    {
        Model = model;
    }

    // Caliburn.Micro 자동 바인딩: Button x:Name="OpenPresets" → OpenPresets() 메서드
    public async Task OpenPresets()
    {
        await _eventAggregator.PublishOnUIThreadAsync(
            new OpenPresetsMessage(Model), _token);
    }

    // INotifyPropertyChanged 자동 구현 (Caliburn.Micro)
    public string CameraName
    {
        get => Model.DeviceName;
        set
        {
            Model.DeviceName = value;
            NotifyOfPropertyChange(() => CameraName);
        }
    }
}
```

### 4. Caliburn.Micro 컨벤션 (Conventions)

#### 자동 바인딩 (Auto Binding)

```xml
<!-- View -->
<Button x:Name="SaveCamera" Content="저장" />
<TextBox x:Name="CameraName" />
<ListBox x:Name="Items" />
```

```csharp
// ViewModel - 메서드/프로퍼티 이름이 같으면 자동 바인딩
public void SaveCamera()  // Button x:Name="SaveCamera" 자동 바인딩
{
    // 저장 로직
}

public string CameraName { get; set; }  // TextBox x:Name="CameraName" 자동 바인딩

public ObservableCollection<CameraDeviceModel> Items { get; set; }  // ListBox 자동 바인딩
```

#### 메시지 (Messages)

```xml
<Button cal:Message.Attach="[Event Click] = [Action OpenDialog($dataContext)]" />
```

#### View Discovery

- **Naming**: `CameraDeviceViewModel` → `CameraDeviceView` 자동 탐색
- **Namespace**: `ViewModels` → `Views` 자동 매핑

---

## 핵심 라이브러리 (Core Libraries)

### 1. Ironwall.Dotnet.Libraries.Base

**Purpose**: 전체 솔루션의 기반 인프라

#### 핵심 클래스

##### **ParentBootstrapper\<T\>** - 애플리케이션 부트스트래퍼

**Location**: `Ironwall.Dotnet.Libraries.Base/ParentBootstrapper.cs`

**역할**:
- Caliburn.Micro 기반 애플리케이션 초기화
- Autofac 컨테이너 설정
- 서비스 순차적 시작 (`IService.ExecuteAsync`)
- 전역 예외 처리

**사용 예시**:
```csharp
public class AppBootstrapper : ParentBootstrapper<ShellViewModel>
{
    protected override void ConfigureContainer(ContainerBuilder builder)
    {
        // 모듈 등록
        builder.RegisterModule(new DeviceModule());
        builder.RegisterModule(new EventModule());
        builder.RegisterModule(new DeviceDbModule(_setupModel));
    }
}
```

**주요 메서드**:
- `ConfigureContainer(ContainerBuilder)`: DI 컨테이너 설정 (추상 메서드)
- `Start()`: 모든 `IService` 순차 실행 (메타데이터 순서 기반)
- `OnStartup()`: 애플리케이션 시작
- `OnExit()`: 서비스 정리 및 종료

##### **EntityCollectionProvider\<T\>** - 스레드 안전 컬렉션

**Location**: `Ironwall.Dotnet.Libraries.Base/DataProviders/EntityCollectionProvider.cs`

**역할**:
- `ObservableCollection<T>`의 스레드 안전 래퍼
- UI 스레드 동기화 (`BindingOperations.EnableCollectionSynchronization`)
- `DispatcherService`를 통한 UI 스레드 마샬링

**핵심 기능**:
```csharp
public class EntityCollectionProvider<T> : ICollector<T>
{
    public ObservableCollection<T> CollectionEntity { get; set; }

    public void Add(T item)
    {
        DispatcherService.Invoke(() =>
        {
            lock (_lock)
            {
                CollectionEntity.Add(item);
            }
        });
    }

    public void Remove(T item)
    {
        DispatcherService.Invoke(() =>
        {
            lock (_lock)
            {
                CollectionEntity.Remove(item);
            }
        });
    }

    public void Clear()
    {
        DispatcherService.Invoke(() =>
        {
            lock (_lock)
            {
                CollectionEntity.Clear();
            }
        });
    }
}
```

##### **BaseProvider\<T\>** - 데이터 프로바이더 기반 클래스

**Location**: `Ironwall.Dotnet.Libraries.Base/DataProviders/BaseProvider.cs`

**역할**:
- 모든 도메인 프로바이더의 기반 클래스
- `EntityCollectionProvider<T>` 상속
- 데이터 컬렉션 관리

**사용 예시**:
```csharp
public class CameraDeviceProvider : BaseProvider<ICameraDeviceModel>, ILoadable
{
    public async Task Initialize(CancellationToken token = default)
    {
        // DB에서 데이터 로드
        var cameras = await _dbService.FetchCameraListAsync();
        foreach (var camera in cameras)
        {
            Add(camera);
        }
    }
}
```

##### **IService** - 백그라운드 서비스 인터페이스

**Location**: `Ironwall.Dotnet.Libraries.Base/Services/IService.cs`

**정의**:
```csharp
public interface IService
{
    Task ExecuteAsync(CancellationToken token = default);
    Task StopAsync(CancellationToken token = default);
}
```

**역할**:
- 백그라운드 서비스의 표준 인터페이스
- `ParentBootstrapper`에서 메타데이터 순서에 따라 순차 실행

**구현 예시**:
```csharp
public class DeviceDbService : IDeviceDbService, IService
{
    public async Task ExecuteAsync(CancellationToken token = default)
    {
        // 데이터베이스 초기화
        await CreateTableDeviceAsync();
        await CreateTableCameraAsync();

        // 프로바이더에 데이터 로드
        foreach (var provider in _providers.OfType<ILoadable>())
        {
            await provider.Initialize(token);
        }
    }

    public Task StopAsync(CancellationToken token = default)
    {
        // 정리 작업
        return Task.CompletedTask;
    }
}
```

##### **LogService** - 로깅 서비스

**Location**: `Ironwall.Dotnet.Libraries.Base/Services/LogService.cs`

**역할**:
- log4net 기반 중앙 집중식 로깅
- 모든 서비스/프로바이더/ViewModel에 주입

**사용 예시**:
```csharp
public class SomeService
{
    private readonly ILogService _logService;

    public SomeService(ILogService logService)
    {
        _logService = logService;
    }

    public void DoSomething()
    {
        _logService.Info("작업 시작");
        try
        {
            // 작업 수행
        }
        catch (Exception ex)
        {
            _logService.Error($"오류 발생: {ex.Message}", ex);
        }
    }
}
```

##### **DispatcherService** - UI 스레드 디스패처

**Location**: `Ironwall.Dotnet.Libraries.Base/Services/DispatcherService.cs`

**역할**:
- UI 스레드로 작업 마샬링
- 백그라운드 스레드에서 UI 업데이트 시 필수

**사용 예시**:
```csharp
// 백그라운드 스레드에서 UI 컬렉션 업데이트
DispatcherService.Invoke(() =>
{
    Cameras.Add(newCamera);
});
```

#### 인터페이스

| Interface | Purpose | Location |
|-----------|---------|----------|
| `IBaseModel` | 모든 모델의 기본 인터페이스 (Id 프로퍼티) | `Models/IBaseModel.cs` |
| `IService` | 백그라운드 서비스 인터페이스 | `Services/IService.cs` |
| `ILoadable` | 초기화 가능한 객체 인터페이스 | `Interfaces/ILoadable.cs` |
| `ICollector<T>` | 컬렉션 관리 인터페이스 | `DataProviders/ICollector.cs` |
| `ILogService` | 로깅 서비스 인터페이스 | `Services/ILogService.cs` |

#### 폴더 구조

```
Ironwall.Dotnet.Libraries.Base/
├── DataProviders/
│   ├── BaseProvider.cs
│   ├── EntityCollectionProvider.cs
│   └── ICollector.cs
├── Interfaces/
│   └── ILoadable.cs
├── Models/
│   ├── BaseModel.cs
│   └── IBaseModel.cs
├── Services/
│   ├── IService.cs
│   ├── LogService.cs
│   └── DispatcherService.cs
└── ParentBootstrapper.cs
```

### 2. Ironwall.Dotnet.Framework

**Purpose**: 프레임워크 확장 기능 (헬퍼, 유틸리티, 서비스)

#### 폴더 구조

```
Ironwall.Dotnet.Framework/
├── Constants/          # 상수 정의
├── DataProviders/      # 데이터 프로바이더
├── Enums/              # 열거형
├── Events/             # 이벤트 헬퍼
│   └── EventHelper.cs
├── Helpers/            # 헬퍼 클래스
│   ├── DateTimeHelper.cs
│   ├── FileManager.cs
│   ├── IdCodeGenerator.cs
│   ├── PasswordHelper.cs
│   └── TokenGenerator.cs
└── Services/           # 서비스
```

#### 주요 헬퍼

##### **IdCodeGenerator** - ID 생성기

**역할**: 고유 ID 생성 (디바이스, 이벤트 등)

```csharp
public class IdCodeGenerator
{
    public static int GenerateDeviceId();
    public static int GenerateEventId();
}
```

##### **DateTimeHelper** - 날짜/시간 헬퍼

**역할**: 날짜 포맷팅, 변환

```csharp
public class DateTimeHelper
{
    public static string ToFormattedString(DateTime dateTime);
    public static DateTime FromFormattedString(string formatted);
}
```

##### **PasswordHelper** - 비밀번호 헬퍼

**역할**: 비밀번호 해싱, 검증

```csharp
public class PasswordHelper
{
    public static string HashPassword(string password);
    public static bool VerifyPassword(string password, string hash);
}
```

##### **TokenGenerator** - 토큰 생성기

**역할**: 세션 토큰 생성

```csharp
public class TokenGenerator
{
    public static string GenerateToken();
}
```

### 3. Ironwall.Dotnet.Libraries.ViewModel

**Purpose**: MVVM ViewModel 기반 클래스 및 공통 ViewModel

#### 폴더 구조

```
Ironwall.Dotnet.Libraries.ViewModel/
├── Models/
│   └── CommonMessages.cs        # 공통 메시지 모델
├── Services/
├── ViewModels/
│   ├── BaseViewModel.cs         # 기본 ViewModel
│   ├── SelectableBaseViewModel.cs
│   ├── Components/              # 컴포넌트 ViewModel
│   ├── Conductors/              # Conductor ViewModel
│   │   ├── ConductorAllViewModel.cs
│   │   └── ConductorOneViewModel.cs
│   ├── DataGrids/               # DataGrid ViewModel
│   │   ├── BaseDataGridViewModel.cs
│   │   └── BaseDataGridPanelViewModel.cs
│   └── Panels/                  # Panel ViewModel
│       └── BasePanelViewModel.cs
```

#### 공통 메시지 (Common Messages)

**Location**: `Ironwall.Dotnet.Libraries.ViewModel/Models/CommonMessages.cs`

**CloseAllMessageModel** - 모든 창 닫기 메시지
```csharp
public class CloseAllMessageModel { }
```

**사용 예시**:
```csharp
// 메시지 발행
await _eventAggregator.PublishOnUIThreadAsync(new CloseAllMessageModel(), _token);

// 메시지 수신
public class SomeViewModel : BaseViewModel<SomeModel>, IHandle<CloseAllMessageModel>
{
    public Task HandleAsync(CloseAllMessageModel message, CancellationToken cancellationToken)
    {
        return TryCloseAsync();
    }
}
```

---

## 도메인 라이브러리 (Domain Libraries)

### 도메인 프로젝트 패턴

각 도메인은 **3계층 아키텍처**를 따릅니다:

```
{Domain}              # 비즈니스 로직
    ↓
{Domain}.Db           # 데이터베이스 계층
    ↓
{Domain}.Ui           # 프레젠테이션 계층
```

### 표준 폴더 구조

#### 비즈니스 로직 프로젝트 (예: `Ironwall.Dotnet.Libraries.Devices`)

```
Ironwall.Dotnet.Libraries.Devices/
├── Defines/           # 상수 및 정의
├── Models/            # 도메인별 모델 (선택적)
├── Modules/           # Autofac 모듈
│   └── DeviceModule.cs
└── Providers/         # 데이터 프로바이더
    ├── DeviceProvider.cs
    ├── CameraDeviceProvider.cs
    ├── ControllerDeviceProvider.cs
    └── SensorDeviceProvider.cs
```

#### 데이터베이스 프로젝트 (예: `Ironwall.Dotnet.Libraries.Devices.Db`)

```
Ironwall.Dotnet.Libraries.Devices.Db/
├── Models/            # DB 설정 모델
│   └── DeviceDbSetupModel.cs
├── Modules/           # Autofac 모듈
│   └── DeviceDbModule.cs
├── Services/          # DB 서비스
│   ├── IDeviceDbService.cs
│   └── DeviceDbService.cs
└── Tests/             # xUnit 테스트
    └── DeviceDbServiceTests.cs
```

#### UI 프로젝트 (예: `Ironwall.Dotnet.Libraries.Devices.Ui`)

```
Ironwall.Dotnet.Libraries.Devices.Ui/
├── Behaviors/         # WPF Behaviors
│   └── CameraDeviceSelectedItemsBehavior.cs
├── Helpers/           # UI 헬퍼
├── Modules/           # Autofac 모듈
│   └── DeviceUiModule.cs
├── Resources/         # 리소스 (이미지, 사운드 등)
├── Services/          # UI별 서비스
├── ViewModels/        # ViewModel
│   ├── CameraDeviceViewModel.cs
│   ├── Dashboards/
│   ├── Dialogs/
│   └── Panels/
└── Views/             # XAML View
    ├── CameraDeviceView.xaml
    ├── Dashboards/
    ├── Dialogs/
    └── Panels/
```

---

## 데이터 액세스 패턴 (Data Access Patterns)

### 기술 스택

- **ORM**: Dapper (Micro-ORM)
- **Database**: MySQL (MySql.Data 9.2.0 - 9.3.0)
- **Testing**: xUnit (2.9.3)

### Repository 패턴

각 도메인은 `{Domain}DbService`를 통해 데이터 액세스를 수행합니다.

#### DbService 인터페이스 예시

**IDeviceDbService**
```csharp
public interface IDeviceDbService
{
    // Device CRUD
    Task<IEnumerable<IBaseDeviceModel>> FetchDeviceListAsync();
    Task<bool> InsertDeviceAsync(IBaseDeviceModel model);
    Task<bool> UpdateDeviceAsync(IBaseDeviceModel model);
    Task<bool> DeleteDeviceAsync(int id);

    // Camera CRUD
    Task<IEnumerable<ICameraDeviceModel>> FetchCameraListAsync();
    Task<bool> InsertCameraAsync(ICameraDeviceModel model);
    Task<bool> UpdateCameraAsync(ICameraDeviceModel model);
    Task<bool> DeleteCameraAsync(int id);

    // Table Management
    Task CreateTableDeviceAsync();
    Task CreateTableCameraAsync();
    Task DropTableDeviceAsync();
}
```

#### DbService 구현 예시

**DeviceDbService**
```csharp
public class DeviceDbService : IDeviceDbService, IService
{
    private readonly ILogService _logService;
    private readonly DeviceDbSetupModel _setupModel;
    private readonly IEnumerable<IBaseProvider> _providers;

    public DeviceDbService(
        DeviceDbSetupModel setupModel,
        IEnumerable<IBaseProvider> providers,
        ILogService logService)
    {
        _setupModel = setupModel;
        _providers = providers;
        _logService = logService;
    }

    // IService 구현
    public async Task ExecuteAsync(CancellationToken token = default)
    {
        // 테이블 생성
        await CreateTableDeviceAsync();
        await CreateTableCameraAsync();

        // 프로바이더 초기화
        foreach (var provider in _providers.OfType<ILoadable>())
        {
            await provider.Initialize(token);
        }
    }

    public Task StopAsync(CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    // CRUD 구현
    public async Task<IEnumerable<ICameraDeviceModel>> FetchCameraListAsync()
    {
        using (var connection = new MySqlConnection(_setupModel.ConnectionString))
        {
            await connection.OpenAsync();
            var sql = "SELECT * FROM cameras";
            return await connection.QueryAsync<CameraDeviceModel>(sql);
        }
    }

    public async Task<bool> InsertCameraAsync(ICameraDeviceModel model)
    {
        using (var connection = new MySqlConnection(_setupModel.ConnectionString))
        {
            await connection.OpenAsync();
            var sql = @"INSERT INTO cameras
                       (device_number, device_name, ip, port, rtsp_url, ...)
                       VALUES (@DeviceNumber, @DeviceName, @Ip, @Port, @RtspUrl, ...)";
            var result = await connection.ExecuteAsync(sql, model);
            return result > 0;
        }
    }
}
```

### DB 설정 모델

**DeviceDbSetupModel**
```csharp
public class DeviceDbSetupModel : IMariaDbSetupModel
{
    public string ConnectionString { get; set; }
    public string Database { get; set; }
    public string Server { get; set; }
    public string Port { get; set; }
    public string UserId { get; set; }
    public string Password { get; set; }

    public DeviceDbSetupModel(string server, string port, string database, string userId, string password)
    {
        Server = server;
        Port = port;
        Database = database;
        UserId = userId;
        Password = password;
        ConnectionString = $"Server={server};Port={port};Database={database};Uid={userId};Pwd={password};";
    }
}
```

### 모듈 등록 (Autofac Module)

**DeviceDbModule**
```csharp
public class DeviceDbModule : Module
{
    private DeviceDbSetupModel _model;
    private int _count = 10;  // 실행 순서

    public DeviceDbModule(string server, string port, string database, string userId, string password)
    {
        _model = new DeviceDbSetupModel(server, port, database, userId, password);
    }

    protected override void Load(ContainerBuilder builder)
    {
        // Setup Model 등록
        builder.RegisterInstance(_model).AsSelf().SingleInstance();

        // Service 등록 (메타데이터 순서 포함)
        builder.RegisterType<DeviceDbService>()
            .As<IDeviceDbService>()
            .As<IService>()
            .SingleInstance()
            .WithMetadata("Order", _count);
    }
}
```

### 데이터 흐름 (Data Flow)

```
1. Application Startup
   ↓
2. ParentBootstrapper.Start()
   ↓
3. DeviceDbService.ExecuteAsync()
   - CreateTableDeviceAsync()
   - CreateTableCameraAsync()
   ↓
4. CameraDeviceProvider.Initialize()
   - FetchCameraListAsync()
   - Add to CollectionEntity
   ↓
5. UI Binding (ViewModel → View)
   - CameraDeviceViewModel.Items → DataGrid
```

---

## 의존성 그래프 (Dependency Graph)

### 레이어 의존성

```
┌─────────────────────────────────────────────────────────────┐
│                          UI Layer                           │
│                                                             │
│  Devices.Ui   Events.Ui   GMaps.Ui   Streaming   Sounds.Ui  │
└──────────────────────────┬──────────────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────────────┐
│                    Business Logic Layer                     │
│                                                             │
│    Devices      Events      GMaps    Streaming.Base Sounds  │
└──────────────────────────┬──────────────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────────────┐
│                      Data Access Layer                      │
│                                                             │
│     Devices.Db   Events.Db   GMaps.Db   Accounts.Db         │
└──────────────────────────┬──────────────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────────────┐
│                     Foundation Layer                        │
│                                                             │
│  Base  Framework  Framework.Models  Monitoring.Models       │
│  Enums   ViewModel   Utils   AdornerDecorator               │
└─────────────────────────────────────────────────────────────┘
```

### 프로젝트별 주요 의존성

#### Core Dependencies

| Project | Key Dependencies |
|---------|------------------|
| **Libraries.Base** | Autofac, Caliburn.Micro, log4net, Newtonsoft.Json |
| **Framework** | Libraries.Base, Autofac, Caliburn.Micro, CsvHelper |
| **Framework.Models** | Framework, Libraries.Base, 외부 DLL (Ironwall.Message.Base, Middleware.Message.Framework) |
| **Monitoring.Models** | Libraries.Base, Libraries.Enums |
| **Libraries.ViewModel** | Libraries.Base, Caliburn.Micro, Autofac |

#### Devices Domain

| Project | Key Dependencies |
|---------|------------------|
| **Devices** | Libraries.Base, Monitoring.Models |
| **Devices.Db** | Libraries.Base, Devices, Dapper, MySql.Data, xUnit |
| **Devices.Ui** | Libraries.Base, Libraries.ViewModel, Devices, Devices.Db, MaterialDesignThemes, MahApps.Metro, OnvifSolution |

#### Events Domain

| Project | Key Dependencies |
|---------|------------------|
| **Events** | Libraries.Base, Monitoring.Models, Devices |
| **Events.Db** | Libraries.Base, Events, Devices, Dapper, MySql.Data, xUnit |
| **Events.Ui** | Libraries.Base, Libraries.ViewModel, Events, Events.Db, Devices, Devices.Ui, MaterialDesignThemes, LiveChartsCore |

#### GMaps Domain

| Project | Key Dependencies |
|---------|------------------|
| **GMaps** | Libraries.Base, Monitoring.Models, GMap.NET (외부 라이브러리) |
| **GMaps.Db** | Libraries.Base, GMaps, Monitoring.Models, Dapper, MySql.Data |
| **GMaps.Ui** | Libraries.Base, Libraries.ViewModel, GMaps, GMaps.Db, AdornerDecorator, Events.Ui, GMap.NET, CoordinateSharp, BitMiracle.LibTiff.NET |

#### Streaming Domain

| Project | Key Dependencies |
|---------|------------------|
| **Streaming.Base** | Libraries.Base, Libraries.Enums |
| **Streaming** | Libraries.Base, Libraries.ViewModel, Streaming.Base, LibVLCSharp, VideoLAN.LibVLC.Windows, Polly, MaterialDesignThemes |

### 공통 NuGet 패키지

**모든 프로젝트**:
- Autofac (8.2.0 - 8.4.0)
- Newtonsoft.Json (13.0.3 - 13.0.4)

**WPF 프로젝트**:
- Caliburn.Micro (4.0.230 - 5.0.258)
- MaterialDesignThemes (5.2.1)

**Database 프로젝트**:
- Dapper (2.1.35 - 2.1.66)
- MySql.Data (9.2.0 - 9.3.0)
- xUnit (2.9.3)

---

## 개발 가이드 (Development Guide)

### 새 도메인 추가하기

새로운 도메인(예: `Alarms`)을 추가하려면 다음 단계를 따르세요:

#### 1단계: 비즈니스 로직 프로젝트 생성

**프로젝트 이름**: `Ironwall.Dotnet.Libraries.Alarms`

**폴더 구조**:
```
Ironwall.Dotnet.Libraries.Alarms/
├── Defines/
├── Models/
│   └── AlarmModel.cs
├── Modules/
│   └── AlarmModule.cs
└── Providers/
    └── AlarmProvider.cs
```

**AlarmModel.cs**:
```csharp
public class AlarmModel : BaseModel, IAlarmModel
{
    public string AlarmName { get; set; }
    public DateTime AlarmTime { get; set; }
    public EnumAlarmType AlarmType { get; set; }
}

public interface IAlarmModel : IBaseModel
{
    string AlarmName { get; set; }
    DateTime AlarmTime { get; set; }
    EnumAlarmType AlarmType { get; set; }
}
```

**AlarmProvider.cs**:
```csharp
public class AlarmProvider : BaseProvider<IAlarmModel>, ILoadable
{
    private readonly ILogService _logService;

    public AlarmProvider(ILogService logService)
    {
        _logService = logService;
    }

    public async Task Initialize(CancellationToken token = default)
    {
        _logService.Info("AlarmProvider 초기화 시작");
        // 초기 데이터 로드 로직
    }
}
```

**AlarmModule.cs**:
```csharp
public class AlarmModule : Module
{
    private int _count = 0;

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AlarmProvider>()
            .AsSelf()
            .As<ILoadable>()
            .SingleInstance()
            .WithMetadata("Order", _count++);
    }
}
```

#### 2단계: 데이터베이스 프로젝트 생성

**프로젝트 이름**: `Ironwall.Dotnet.Libraries.Alarms.Db`

**폴더 구조**:
```
Ironwall.Dotnet.Libraries.Alarms.Db/
├── Models/
│   └── AlarmDbSetupModel.cs
├── Modules/
│   └── AlarmDbModule.cs
├── Services/
│   ├── IAlarmDbService.cs
│   └── AlarmDbService.cs
└── Tests/
    └── AlarmDbServiceTests.cs
```

**IAlarmDbService.cs**:
```csharp
public interface IAlarmDbService
{
    Task<IEnumerable<IAlarmModel>> FetchAlarmListAsync();
    Task<bool> InsertAlarmAsync(IAlarmModel model);
    Task<bool> UpdateAlarmAsync(IAlarmModel model);
    Task<bool> DeleteAlarmAsync(int id);
    Task CreateTableAlarmAsync();
}
```

**AlarmDbService.cs**:
```csharp
public class AlarmDbService : IAlarmDbService, IService
{
    private readonly AlarmDbSetupModel _setupModel;
    private readonly IEnumerable<ILoadable> _providers;
    private readonly ILogService _logService;

    public AlarmDbService(
        AlarmDbSetupModel setupModel,
        IEnumerable<ILoadable> providers,
        ILogService logService)
    {
        _setupModel = setupModel;
        _providers = providers;
        _logService = logService;
    }

    public async Task ExecuteAsync(CancellationToken token = default)
    {
        await CreateTableAlarmAsync();

        foreach (var provider in _providers.OfType<AlarmProvider>())
        {
            await provider.Initialize(token);
        }
    }

    public Task StopAsync(CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<IAlarmModel>> FetchAlarmListAsync()
    {
        using (var connection = new MySqlConnection(_setupModel.ConnectionString))
        {
            await connection.OpenAsync();
            var sql = "SELECT * FROM alarms";
            return await connection.QueryAsync<AlarmModel>(sql);
        }
    }

    public async Task<bool> InsertAlarmAsync(IAlarmModel model)
    {
        using (var connection = new MySqlConnection(_setupModel.ConnectionString))
        {
            await connection.OpenAsync();
            var sql = @"INSERT INTO alarms (alarm_name, alarm_time, alarm_type)
                       VALUES (@AlarmName, @AlarmTime, @AlarmType)";
            var result = await connection.ExecuteAsync(sql, model);
            return result > 0;
        }
    }

    public async Task CreateTableAlarmAsync()
    {
        using (var connection = new MySqlConnection(_setupModel.ConnectionString))
        {
            await connection.OpenAsync();
            var sql = @"CREATE TABLE IF NOT EXISTS alarms (
                id INT AUTO_INCREMENT PRIMARY KEY,
                alarm_name VARCHAR(100),
                alarm_time DATETIME,
                alarm_type INT
            )";
            await connection.ExecuteAsync(sql);
        }
    }
}
```

**AlarmDbModule.cs**:
```csharp
public class AlarmDbModule : Module
{
    private AlarmDbSetupModel _model;
    private int _count = 20;  // 실행 순서 (다른 모듈보다 뒤에)

    public AlarmDbModule(string server, string port, string database, string userId, string password)
    {
        _model = new AlarmDbSetupModel(server, port, database, userId, password);
    }

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterInstance(_model).AsSelf().SingleInstance();

        builder.RegisterType<AlarmDbService>()
            .As<IAlarmDbService>()
            .As<IService>()
            .SingleInstance()
            .WithMetadata("Order", _count);
    }
}
```

#### 3단계: UI 프로젝트 생성

**프로젝트 이름**: `Ironwall.Dotnet.Libraries.Alarms.Ui`

**폴더 구조**:
```
Ironwall.Dotnet.Libraries.Alarms.Ui/
├── Behaviors/
├── Helpers/
├── Modules/
│   └── AlarmUiModule.cs
├── Resources/
├── Services/
├── ViewModels/
│   ├── AlarmViewModel.cs
│   └── Dashboards/
│       └── AlarmDashboardViewModel.cs
└── Views/
    ├── AlarmView.xaml
    └── Dashboards/
        └── AlarmDashboardView.xaml
```

**AlarmViewModel.cs**:
```csharp
public class AlarmViewModel : BaseViewModel<AlarmModel>
{
    private readonly IAlarmDbService _dbService;

    public AlarmViewModel(
        AlarmModel model,
        IAlarmDbService dbService,
        IEventAggregator eventAggregator,
        ILogService logService)
        : base(eventAggregator, logService)
    {
        Model = model;
        _dbService = dbService;
    }

    public string AlarmName
    {
        get => Model.AlarmName;
        set
        {
            Model.AlarmName = value;
            NotifyOfPropertyChange(() => AlarmName);
        }
    }

    public async Task SaveAlarm()
    {
        var result = await _dbService.InsertAlarmAsync(Model);
        if (result)
        {
            _logService.Info("알람 저장 성공");
            await _eventAggregator.PublishOnUIThreadAsync(new AlarmSavedMessage(Model), _token);
        }
    }

    public async Task DeleteAlarm()
    {
        var result = await _dbService.DeleteAlarmAsync(Model.Id);
        if (result)
        {
            _logService.Info("알람 삭제 성공");
            await TryCloseAsync();
        }
    }
}
```

**AlarmView.xaml**:
```xml
<UserControl x:Class="Ironwall.Dotnet.Libraries.Alarms.Ui.Views.AlarmView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:cal="http://www.caliburnproject.org"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes">
    <Grid>
        <StackPanel>
            <!-- Caliburn.Micro 자동 바인딩 -->
            <TextBox x:Name="AlarmName"
                     materialDesign:HintAssist.Hint="알람 이름" />

            <Button x:Name="SaveAlarm" Content="저장" />
            <Button x:Name="DeleteAlarm" Content="삭제" />
        </StackPanel>
    </Grid>
</UserControl>
```

**AlarmUiModule.cs**:
```csharp
public class AlarmUiModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // ViewModels 등록
        builder.RegisterType<AlarmViewModel>().AsSelf();
        builder.RegisterType<AlarmDashboardViewModel>().AsSelf();
    }
}
```

#### 4단계: 애플리케이션에 통합

**App.xaml.cs** 또는 **Bootstrapper**:
```csharp
protected override void ConfigureContainer(ContainerBuilder builder)
{
    // 기존 모듈
    builder.RegisterModule(new DeviceModule());
    builder.RegisterModule(new EventModule());

    // 새 도메인 모듈 추가
    builder.RegisterModule(new AlarmModule());
    builder.RegisterModule(new AlarmDbModule("localhost", "3306", "ironwall_db", "root", "password"));
    builder.RegisterModule(new AlarmUiModule());
}
```

### ViewModel-View 바인딩 예시

#### 자동 바인딩 (Convention-based)

**ViewModel**:
```csharp
public class CameraDeviceViewModel : BaseViewModel<CameraDeviceModel>
{
    // 프로퍼티 바인딩
    public string CameraName
    {
        get => Model.DeviceName;
        set
        {
            Model.DeviceName = value;
            NotifyOfPropertyChange(() => CameraName);
        }
    }

    public ObservableCollection<PresetModel> Presets { get; set; }

    // 메서드 바인딩
    public async Task SaveCamera()
    {
        await _dbService.UpdateCameraAsync(Model);
    }

    public async Task OpenPresets()
    {
        // 프리셋 창 열기
    }

    public bool CanSaveCamera => !string.IsNullOrEmpty(CameraName);
}
```

**View**:
```xml
<UserControl x:Class="CameraDeviceView">
    <StackPanel>
        <!-- 프로퍼티 바인딩 -->
        <TextBox x:Name="CameraName" />

        <!-- 컬렉션 바인딩 -->
        <ListBox x:Name="Presets" />

        <!-- 메서드 바인딩 (CanSaveCamera로 자동 활성화 제어) -->
        <Button x:Name="SaveCamera" Content="저장" />

        <!-- 메서드 바인딩 -->
        <Button x:Name="OpenPresets" Content="프리셋 관리" />
    </StackPanel>
</UserControl>
```

#### 명시적 바인딩

**View**:
```xml
<Button Content="저장"
        cal:Message.Attach="[Event Click] = [Action SaveCamera]" />

<Button Content="프리셋"
        cal:Message.Attach="[Event Click] = [Action OpenPresets($dataContext)]" />

<DataGrid ItemsSource="{Binding Items}"
          SelectedItem="{Binding SelectedItem}" />
```

### EventAggregator 패턴

#### 메시지 정의

```csharp
public class CameraSavedMessage
{
    public ICameraDeviceModel Camera { get; set; }

    public CameraSavedMessage(ICameraDeviceModel camera)
    {
        Camera = camera;
    }
}
```

#### 메시지 발행 (Publisher)

```csharp
public class CameraEditViewModel : BaseViewModel<CameraDeviceModel>
{
    public async Task SaveCamera()
    {
        await _dbService.UpdateCameraAsync(Model);

        // 메시지 발행
        await _eventAggregator.PublishOnUIThreadAsync(
            new CameraSavedMessage(Model), _token);
    }
}
```

#### 메시지 수신 (Subscriber)

```csharp
public class CameraListViewModel : BaseViewModel<CameraDeviceModel>,
                                    IHandle<CameraSavedMessage>
{
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // 구독 시작
        _eventAggregator?.SubscribeOnUIThread(this);
        return base.OnActivateAsync(cancellationToken);
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        // 구독 해제
        _eventAggregator?.Unsubscribe(this);
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    // 메시지 처리
    public async Task HandleAsync(CameraSavedMessage message, CancellationToken cancellationToken)
    {
        _logService.Info($"카메라 저장됨: {message.Camera.DeviceName}");

        // UI 업데이트
        var existingCamera = Cameras.FirstOrDefault(c => c.Id == message.Camera.Id);
        if (existingCamera != null)
        {
            // 업데이트
            var index = Cameras.IndexOf(existingCamera);
            Cameras[index] = message.Camera;
        }
        else
        {
            // 추가
            Cameras.Add(message.Camera);
        }
    }
}
```

### Provider 사용법

#### Provider 정의

```csharp
public class CameraDeviceProvider : BaseProvider<ICameraDeviceModel>, ILoadable
{
    private readonly ILogService _logService;
    private readonly IDeviceDbService _dbService;

    public CameraDeviceProvider(
        ILogService logService,
        IDeviceDbService dbService)
    {
        _logService = logService;
        _dbService = dbService;
    }

    public async Task Initialize(CancellationToken token = default)
    {
        _logService.Info("CameraDeviceProvider 초기화");

        var cameras = await _dbService.FetchCameraListAsync();
        foreach (var camera in cameras)
        {
            Add(camera);  // BaseProvider의 Add 메서드 (스레드 안전)
        }

        _logService.Info($"{Count}개 카메라 로드 완료");
    }
}
```

#### Provider 사용

```csharp
public class CameraListViewModel : BaseViewModel<CameraDeviceModel>
{
    private readonly CameraDeviceProvider _cameraProvider;

    public CameraListViewModel(
        CameraDeviceProvider cameraProvider,
        IEventAggregator eventAggregator,
        ILogService logService)
        : base(eventAggregator, logService)
    {
        _cameraProvider = cameraProvider;

        // Provider의 CollectionEntity를 직접 바인딩
        Cameras = _cameraProvider.CollectionEntity;
    }

    public ObservableCollection<ICameraDeviceModel> Cameras { get; set; }
}
```

### 코딩 컨벤션

#### 네이밍

- **클래스**: PascalCase (`CameraDeviceViewModel`)
- **메서드**: PascalCase (`SaveCamera`, `FetchCameraListAsync`)
- **프로퍼티**: PascalCase (`CameraName`, `IsVisible`)
- **필드**: camelCase with underscore (`_logService`, `_eventAggregator`)
- **파라미터**: camelCase (`model`, `token`)
- **상수**: UPPER_SNAKE_CASE (`MAX_RETRY_COUNT`)

#### Region 구조

```csharp
public class SomeViewModel : BaseViewModel<SomeModel>
{
    #region - Ctors -
    public SomeViewModel(IEventAggregator eventAggregator, ILogService logService)
        : base(eventAggregator, logService)
    {
    }
    #endregion

    #region - Implementation of Interface -
    public async Task ExecuteAsync(CancellationToken token = default)
    {
    }
    #endregion

    #region - Overrides -
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
    }
    #endregion

    #region - Binding Methods -
    public async Task SaveData()
    {
    }

    public bool CanSaveData => !string.IsNullOrEmpty(Name);
    #endregion

    #region - Processes -
    private async Task LoadDataAsync()
    {
    }
    #endregion

    #region - IHanldes -  // Note: 오타는 프로젝트 전체에 일관되게 사용됨
    public Task HandleAsync(SomeMessage message, CancellationToken cancellationToken)
    {
    }
    #endregion

    #region - Properties -
    public string Name { get; set; }
    #endregion

    #region - Attributes -
    private readonly ILogService _logService;
    private CancellationTokenSource _cancellationTokenSource;
    #endregion
}
```

#### 파일 헤더

```csharp
/****************************************************************************
   Purpose      : {클래스의 목적}
   Created By   : GHLee
   Created On   : 2025-01-15
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
```

---

## 기술 스택 (Technology Stack)

### 프레임워크 및 런타임

| Technology | Version | Purpose |
|------------|---------|---------|
| **.NET** | 8.0 | 타겟 프레임워크 (net8.0-windows) |
| **WPF** | .NET 8.0 | Windows Presentation Foundation |
| **C#** | 12.0 | 프로그래밍 언어 (implicit usings) |

### MVVM 및 의존성 주입

| Library | Version | Purpose |
|---------|---------|---------|
| **Caliburn.Micro** | 4.0.230 - 5.0.258 | MVVM 프레임워크 |
| **Autofac** | 8.2.0 - 8.4.0 | 의존성 주입 컨테이너 |

### 데이터 액세스

| Library | Version | Purpose |
|---------|---------|---------|
| **Dapper** | 2.1.35 - 2.1.66 | Micro-ORM |
| **MySql.Data** | 9.2.0 - 9.3.0 | MySQL 드라이버 |
| **EntityFramework** | 6.5.1 | ORM (제한적 사용) |

### UI 라이브러리

| Library | Version | Purpose |
|---------|---------|---------|
| **MaterialDesignThemes** | 5.2.1 | Material Design 스타일 |
| **MahApps.Metro** | 2.4.10 | Metro UI 컨트롤 |
| **Microsoft.Xaml.Behaviors.Wpf** | 1.1.122 | WPF Behaviors |
| **LiveChartsCore** | 2.0.0-rc5.4 | 차트 라이브러리 |
| **LiveChartsCore.SkiaSharpView.WPF** | 2.0.0-rc5.4 | WPF용 차트 렌더러 |

### 로깅 및 직렬화

| Library | Version | Purpose |
|---------|---------|---------|
| **log4net** | 3.0.3 | 로깅 프레임워크 |
| **Newtonsoft.Json** | 13.0.3 - 13.0.4 | JSON 직렬화 |

### 테스팅

| Library | Version | Purpose |
|---------|---------|---------|
| **xUnit** | 2.9.3 | 단위 테스트 프레임워크 |
| **xunit.runner.visualstudio** | 2.8.2 | Visual Studio 테스트 러너 |
| **Microsoft.NET.Test.Sdk** | 17.12.0 - 17.14.0 | 테스트 SDK |

### 지도 및 GIS

| Library | Version | Purpose |
|---------|---------|---------|
| **GMap.NET** | Custom Build | 지도 렌더링 (외부 라이브러리) |
| **CoordinateSharp** | 3.2.1.1 | 좌표 변환 |
| **BitMiracle.LibTiff.NET** | 2.4.660 | TIFF 이미지 처리 |
| **System.Drawing.Common** | 9.0.7 | 이미지 처리 |

### 비디오 스트리밍

| Library | Version | Purpose |
|---------|---------|---------|
| **LibVLCSharp** | 3.9.4 | VLC 미디어 플레이어 래퍼 |
| **LibVLCSharp.WPF** | 3.9.4 | WPF용 VLC 컨트롤 |
| **VideoLAN.LibVLC.Windows** | 3.0.21 | VLC 라이브러리 |
| **Polly** | 8.6.3 | 복원력 및 재시도 정책 |

### 기타 유틸리티

| Library | Version | Purpose |
|---------|---------|---------|
| **CsvHelper** | 33.0.1 | CSV 파일 처리 |
| **ClosedXML** | 0.104.2 | Excel 파일 생성 |
| **ExcelDataReader** | 3.7.0 | Excel 파일 읽기 |
| **StackExchange.Redis** | (암시적) | Redis 통합 |

### 외부 DLL

프로젝트는 다음 외부 DLL을 참조합니다:
- `Ironwall.Message.Base.dll`
- `Ironwall.Middleware.Message.Framework.dll`
- `Sensorway.Broker.SeoIncheon.dll`

---

## 주요 디자인 패턴 (Key Design Patterns)

### 1. Repository Pattern
- **위치**: `*.Db` 프로젝트
- **예시**: `DeviceDbService`, `EventDbService`
- **목적**: 데이터 액세스 로직 캡슐화

### 2. Provider Pattern
- **위치**: `*/Providers/` 폴더
- **예시**: `CameraDeviceProvider`, `EventProvider`
- **목적**: 메모리 내 데이터 컬렉션 관리

### 3. Conductor Pattern
- **위치**: `Libraries.ViewModel`
- **예시**: `ConductorAllViewModel`, `ConductorOneViewModel`
- **목적**: 화면 네비게이션 관리

### 4. Observer Pattern
- **구현**: Caliburn.Micro `IEventAggregator`
- **목적**: 느슨한 결합의 컴포넌트 간 통신

### 5. Template Method Pattern
- **위치**: `BaseViewModel<T>`, `BaseProvider<T>`
- **목적**: 공통 로직 재사용

### 6. Factory Pattern
- **위치**: 다양한 `*/Factories/` 폴더
- **목적**: 객체 생성 로직 캡슐화

### 7. Module Pattern
- **위치**: `*/Modules/` 폴더
- **예시**: `DeviceModule`, `EventModule`
- **목적**: Autofac 의존성 등록

### 8. Singleton Pattern
- **구현**: Autofac `.SingleInstance()`
- **대상**: 서비스, 프로바이더

### 9. Strategy Pattern
- **위치**: Symbol Providers (GMaps)
- **목적**: 다양한 심볼 타입별 처리

### 10. Decorator Pattern
- **위치**: `AdornerDecorator` 프로젝트
- **목적**: UI 오버레이 기능

---

## 프로젝트 연락처 (Project Contact)

- **회사**: Sensorway Co., Ltd.
- **부서**: SW Team
- **주 개발자**: GHLee
- **이메일**: lsirikh@naver.com

---

## 버전 정보 (Version Information)

- **현재 브랜치**: v1.3.1
- **프레임워크**: .NET 8.0
- **최근 커밋**:
  - c387368 스트리밍 라이브러리 업데이트 및 구현
  - 57e7fba 이벤트 연결 및 이벤트 서비스 구현
  - c49cbd0 RTSP Popup이벤트 설정 UI 및 DB 구현
  - 6584f75 RTSP팝업 서버로 전환하여 개발
  - 147e835 카메라 팝업 기능 구현 및 CustomControl구현 테스트필요

---

## 추가 참고 사항

### 프로젝트 특징

1. **모듈화**: 각 도메인이 독립적인 라이브러리로 분리
2. **재사용성**: Base 클래스와 인터페이스를 통한 높은 재사용성
3. **확장성**: 새 도메인 추가가 기존 코드에 영향 없이 가능
4. **테스트 가능성**: 의존성 주입으로 단위 테스트 용이
5. **스레드 안전성**: DispatcherService와 컬렉션 동기화
6. **일관성**: 전체 솔루션에서 일관된 패턴과 규칙

### 권장 사항

1. **새 기능 추가 시**: 기존 패턴을 따라 3계층(Logic, Db, Ui) 구조로 개발
2. **데이터 액세스**: 항상 Provider를 통해 메모리 데이터 접근
3. **UI 업데이트**: DispatcherService 사용하여 스레드 안전성 보장
4. **로깅**: 모든 중요 작업에 LogService 사용
5. **메시징**: EventAggregator를 통한 느슨한 결합 유지
6. **테스트**: xUnit으로 DbService 및 핵심 로직 테스트 작성

---

**이 문서는 Ironwall.Dotnet.Libraries 솔루션의 공식 개발 매뉴얼입니다.**
**최종 업데이트**: 2025-10-27
