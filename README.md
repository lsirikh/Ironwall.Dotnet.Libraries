# Ironwall Dotnet Based Libraries

### Goal
> 다양한 SW를 개발하기 위한 Sensorway SW의 라이브러리 모음 Sensorway Framework이다.

### Site : Common
<hr>

## 1. Ironwall.Dotnet.Libraries.Base 소개

### 1.1 개요
`Ironwall.Dotnet.Libraries.Base`는 **Sensorway Framework**의 핵심 라이브러리로, 공통적으로 사용되는 기능을 제공합니다.  
해당 라이브러리는 **.NET 8.0 (Windows)** 환경에서 동작하며, **WPF** 기반의 애플리케이션을 지원합니다.

### 1.2 프로젝트 구성

#### **📂 DataProviders**
> 데이터 관리 및 공통 인터페이스 제공

- `BaseCommonProvider.cs`
- `BaseProvider.cs`
- `EntityCollectionProvider.cs`
- `EntityListProvider.cs`
- `ICollector.cs`
- `InstanceFactory.cs`

#### **📂 Models**
> 데이터 모델 정의

- `CommonMessageModel.cs`
- `IBaseModel.cs`
- `ICommonMessageModel.cs`
- `IMessageModel.cs`

#### **📂 Services**
> 서비스 및 유틸리티 기능 제공

- `DispatcherService.cs`
- `IDataProviderService.cs`
- `ILoadable.cs`
- `ILogService.cs`
- `IService.cs`
- `LogService.cs`
- `TaskService.cs`
- `TimerService.cs`

#### **📄 ParentBootstrapper.cs**
> 애플리케이션의 **부트스트래퍼(Bootstrapper)** 역할 수행

#### 개발 환경
- **.NET Version**: `net8.0-windows`
- **언어**: `C#`
- **UI Framework**: `WPF`
- **DI Container**: `Autofac`

---

## 2. Ironwall.Dotnet.Libraries.ViewModel 소개

### 2.1 개요
`Ironwall.Dotnet.Libraries.ViewModel`은 **Caliburn.Micro MVVM 프레임워크**를 기반으로 **WPF 애플리케이션의 ViewModel 계층을 관리**하는 라이브러리입니다.  
이 라이브러리는 **ViewModel 컴포넌트**와 **컨덕터(Conductor)** 패턴을 지원하여 **동적 UI 관리**를 쉽게 구현할 수 있도록 합니다.

### 2.2 프로젝트 구성

#### **📂 Models**
> ViewModel에서 사용하는 데이터 모델 및 이벤트 아규먼트 정의

- `CommonMessages.cs`
  - 공통적으로 사용되는 메시지 모델 정의
- `ValueNotifyEventArgs.cs`
  - 이벤트 발생 시 데이터를 전달하는 **이벤트 아규먼트 클래스**

#### **📂 Services**
> ViewModel에서 사용할 수 있는 공통 서비스 (추후 추가 예정)

#### **📂 ViewModels**
> WPF ViewModel을 구성하는 주요 컴포넌트 및 컨덕터

##### **📂 Components**
- `BaseCustomViewModel.cs`
- `BaseDataGridPanelViewModel.cs`
- `BaseDataGridViewModel.cs`
- `BasePanelViewModel.cs`
- `BaseViewModel.cs`
- `IBaseCustomViewModel.cs`
- `IBasePanelViewModel.cs`
- `IBaseViewModel.cs`
- `ISelectableBaseViewModel.cs`
- `SelectableBaseViewModel.cs`

##### **📂 Conductors**
- `ConductorAllViewModel.cs`
- `ConductorOneViewModel.cs`
- `IConductorViewModel.cs`

#### 개발 환경
- **.NET Version**: `net8.0-windows`
- **언어**: `C#`
- **UI Framework**: `WPF`
- **MVVM Framework**: `Caliburn.Micro`

---

## 3. Ironwall.Dotnet.Libraries.Utils 소개

### 3.1 개요
`Ironwall.Dotnet.Libraries.Utils`는 **WPF 애플리케이션 개발**을 위한 **바인딩 확장 기능**과 **값 변환 기능**을 제공합니다.

### 3.2 프로젝트 구성

#### **📂 Utils**
> WPF 바인딩을 위한 확장 및 변환기 제공

- `BindingProxys.cs`
  - 바인딩 프록시 객체를 제공하여 **데이터 컨텍스트와의 바인딩 문제를 해결**합니다.
- `BoolToInverseVisibleConverter.cs`
  - `bool` 값을 **반전된 Visibility 값**으로 변환합니다.
  - `true` → `Collapsed`, `false` → `Visible`
- `EnumBindingSourceExtension.cs`
  - Enum 값을 바인딩 가능하도록 변환하는 **WPF 확장 기능**을 제공합니다.

---



## 4. Ironwall.Dotnet.Libraries.Api 소개

### 4.1 개요
`Ironwall.Dotnet.Libraries.Api`는 **API 모듈 및 서비스 로직을 관리하는 라이브러리**입니다.  
이 라이브러리는 **Autofac 기반의 의존성 주입(DI)** 구조를 사용하며,  
단위 테스트를 위해 `xUnit`을 사용하여 API 기능을 검증할 수 있도록 설계되었습니다.

### 4.2 프로젝트 구성

#### **📂 Models**
> API의 기본 설정을 관리하는 모델

- `ApiSetupModel.cs`
  - API의 설정을 관리하는 모델 클래스

#### **📂 Modules**
> API 모듈 등록을 위한 클래스

- `ApiModule.cs`
  - `Autofac`을 활용한 **의존성 주입(DI) 컨테이너 등록**을 수행하는 모듈 클래스

#### **📂 Services**
> API의 주요 기능을 제공하는 서비스 계층

- `ApiService.cs`
  - API의 핵심 비즈니스 로직을 담당하는 서비스 클래스
- `IApiService.cs`
  - API 서비스 인터페이스 정의 (DI 적용을 위한 인터페이스)

#### 개발 환경
- **.NET Version**: `net8.0-windows`
- **언어**: `C#`
- **DI Framework**: `Autofac`
- **테스트 프레임워크**: `xUnit`

---

## 5. Ironwall.Dotnet.Libraries.GMaps 소개

### 5.1 개요
`Ironwall.Dotnet.Libraries.GMaps`는 **GMap.NET 기반의 지도 관제 시스템**을 위한 라이브러리입니다.
지도 설정, 마커 관리, 타일 캐싱 등의 기능을 제공하며, **WPF 애플리케이션의 지도 뷰**를 담당합니다.

### 5.2 프로젝트 구성

#### **📂 Models**
> 지도 관련 데이터 모델 정의

- `GMapSetupModel.cs`
  - 지도 설정 정보를 관리하는 모델 (MapType, MapMode, MapName, TileDirectory 등)
- `HomePositionModel.cs`
  - 지도 홈 포지션 정보 (위도, 경도, 고도, 줌 레벨)

#### **📂 Providers**
> 지도 제공자 및 타일 관리

- `MapProvider.cs`
  - 사용 가능한 지도 목록을 관리하는 Provider
  - Defined 지도 (Google, Bing, OpenStreetMap 등) 및 Custom 지도 지원

#### **📂 ViewModels**
> 지도 설정 및 관제 ViewModel

- `MapSetupViewModel.cs`
  - 지도 설정 화면의 ViewModel
  - MapType, MapName, MapMode, TileDirectory, HomePosition 관리
  - 폴더 선택 다이얼로그 연동 (ButtonTileDirectory)

#### 개발 환경
- **.NET Version**: `net8.0-windows`
- **언어**: `C#`
- **UI Framework**: `WPF`
- **지도 라이브러리**: `GMap.NET`

---

## 6. Ironwall.Dotnet.Libraries.Nats 소개

### 6.1 개요
`Ironwall.Dotnet.Libraries.Nats`는 **NATS 메시징 시스템 연동**을 위한 라이브러리입니다.
경량 고성능 메시징을 제공하며, Pub/Sub 및 Request/Reply 패턴을 지원합니다.

### 6.2 프로젝트 구성

#### **📂 Models**
> NATS 설정 데이터 모델

- `NatsSetupModel.cs`
  - NATS 서버 연결 정보 관리 (IpAddress, Port, Username, Password)
  - NATS 클러스터 설정 지원

#### **📂 Services**
> NATS 연동 서비스

- `NatsService.cs`
  - NATS Pub/Sub 기능 구현
  - Request/Reply 패턴 지원
  - 메시지 직렬화/역직렬화

#### 개발 환경
- **.NET Version**: `net8.0-windows`
- **언어**: `C#`
- **NATS 클라이언트**: `NATS.Client`

---

## 7. Ironwall.Dotnet.Libraries.Redis 소개

### 7.1 개요
`Ironwall.Dotnet.Libraries.Redis`는 **Redis 메시징 시스템 연동**을 위한 라이브러리입니다.
Pub/Sub 패턴을 활용한 실시간 이벤트 전송 및 수신 기능을 제공합니다.

### 7.2 프로젝트 구성

#### **📂 Models**
> Redis 설정 데이터 모델

- `RedisSetupModel.cs`
  - Redis 서버 연결 정보 관리 (IpAddress, Port, Password, ChannelName)

#### **📂 Services**
> Redis 연동 서비스

- `RedisService.cs`
  - Redis Pub/Sub 기능 구현
  - 메시지 발행 및 구독 관리

#### 개발 환경
- **.NET Version**: `net8.0-windows`
- **언어**: `C#`
- **Redis 클라이언트**: `StackExchange.Redis`

---

## 버전 관리

### v1.3.1 (2025-10-28)

#### 📌 추가된 기능

##### Ironwall.Dotnet.Libraries.GMaps
- **MapSetupViewModel 개선**
  - `MapTypes` 속성: EnumMapProvider 기반으로 "Defined", "Custom" 반환
  - `MapNames` 속성: MapProvider에서 실제 지도 이름 목록 제공 (ObservableCollection)
  - `ButtonTileDirectory()` 메서드: FolderBrowserDialog 기반 타일 디렉토리 선택
  - HomePosition 복합 객체 저장 헬퍼 메서드 추가

- **MapSetupModel 확장**
  - MapType, MapMode, MapName, TileDirectory 속성 관리
  - HomePosition 객체 (위도, 경도, 고도, 줌 레벨, 사용 여부)

##### Ironwall.Dotnet.Libraries.Nats
- **NatsSetupModel 확장**
  - IpAddressNats: NATS 서버 IP 주소
  - PortNats: NATS 서버 포트
  - UserName: NATS 인증 사용자명 (선택적)
  - Password: NATS 인증 비밀번호 (선택적)

##### Ironwall.Dotnet.Libraries.Redis
- **RedisSetupModel 추가**
  - IpAddressRedis: Redis 서버 IP 주소
  - PortRedis: Redis 서버 포트
  - PasswordRedis: Redis 인증 비밀번호 (선택적)
  - NameChannel: Redis Pub/Sub 채널 이름

#### 🔧 수정된 파일
- `Ironwall.Dotnet.Libraries.GMaps/Models/GMapSetupModel.cs`
- `Ironwall.Dotnet.Libraries.GMaps/ViewModels/MapSetupViewModel.cs`
- `Ironwall.Dotnet.Libraries.Nats/Models/NatsSetupModel.cs`
- `Ironwall.Dotnet.Libraries.Redis/Models/RedisSetupModel.cs`
- `Ironwall.Dotnet.Libraries.Enums/EnumMapProvider.cs` (참조)

#### 🐛 버그 수정
- MaterialDesign PackIconKind "MountainAltitude" → "ImageFilterHdr" 변경 (유효하지 않은 아이콘 수정)

#### 📝 설계 개선
- MapType과 MapName의 명확한 구분
  - MapType: 제공자 타입 (Defined/Custom)
  - MapName: 실제 지도 이름 (Google 위성지도, OpenStreetMap 등)
- 타일 디렉토리 선택 UI 패턴 표준화 (읽기 전용 TextBox + 버튼)

---

## v1.2.4 (2025-08-28)

### 추가된 파일
- `Ironwall.Dotnet.Libraries.Enums/EnumColorType.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Providers/PidsSymbolProvider.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/Helpers/ColorHelper.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/Helpers/SymbolTypeHelper.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/Models/DeviceSymbolLookupModel.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/Resources/Images/controller01.png`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/Resources/Images/fence01.png`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/Themes/PidsMarkerStyle.xaml`
- `Ironwall.Dotnet.Monitoring.Models/Symbols/IPidsSymbolModel.cs`
- `Ironwall.Dotnet.Monitoring.Models/Symbols/PidsSymbolModel.cs`

### 수정된 파일
- `Ironwall.Dotnet.Libraries.Devices.Db/Services/DeviceDbService.cs`
- `Ironwall.Dotnet.Libraries.Enums/EnumDeviceType.cs`
- `Ironwall.Dotnet.Libraries.Enums/EnumEventStatus.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Db/Services/GMapDbSymbolService.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/GMapSymbols/GMapBaseMarker.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/GMapSymbols/GMapMarkerBaseControl.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/GMapSymbols/GMapMarkerCustomControl.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/GMapSymbols/GMapMarkerGeometricControl.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/GMapSymbols/GMapMarkerPidsControl.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/GMapSymbols/SensorMarkerControl.cs`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/Themes/CustomMarkerStyle.xaml`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/Themes/Generic.xaml`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/Themes/GeometricMarkerStyle.xaml`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/GMapViewModel.cs`
- `Ironwall.Dotnet.Monitoring.Models/Symbols/GeometricSymbolModel.cs`
- `Ironwall.Dotnet.Monitoring.Models/Symbols/ISymbolModel.cs`
- `Ironwall.Dotnet.Monitoring.Models/Symbols/SymbolModel.cs`


---
**일자:** 2025-09-01  
**작업자:** GH.LEE  

## 작업내용

### 1. 문제 상황 분석
- 마커 선택 시 이전 Property Panel의 바인딩이 새 마커 속성을 오염시키는 현상 발견
- 두 번째, 세 번째 마커 선택 시 Title이 빈 문자열로, Size가 32x32로 변경되는 문제 확인
- XAML 바인딩(`SelectedMarker="{Binding SelectedMarker}"`)이 자동으로 `OnSelectedMarkerChanged` 트리거하여 발생

### 2. 근본 원인 파악
- `GMapPropertyBaseControl`의 `OnSelectedMarkerChanged`에서 중복된 `SetupMarkerBindings()` 호출 발견
- XAML에서 SelectedMarker 변경 시 기존 Property Panel의 기본값(Title="", Width=32)이 새 마커에 즉시 적용
- WPF 바인딩 타이밍 문제: PropertyChanged 콜백이 Behavior보다 먼저 실행되는 구조적 한계

### 3. 시도한 해결방안들
- `OnSelectedMarkerChanged`에서 `e.NewValue` 직접 활용하여 원본 데이터 보존 시도
- `_isInitializing` 플래그 제거 (바인딩 없는 상태에서 의미 없음 확인)
- `CoerceValueCallback` 활용한 바인딩 정리 시도
- `ClearAllBindings()` 전후 속성값 로깅을 통한 문제점 추적

### 4. WPF Property Panel 설계 패턴 조사
- Visual Studio Properties Window, Extended WPF Toolkit PropertyGrid 등의 구현 방식 분석
- DataContext Null 패턴, 명시적 바인딩 정리, Dispatcher 동기화 등의 해결책 연구
- PropertyDescriptor 메모리 누수 방지 및 Helper 객체 패턴 학습

### 5. 최종 해결 방향 결정
- 이전 마커와의 연결을 완전히 차단하는 방식 채택
- Property Panel 완전 재생성을 통한 바인딩 오염 근본 차단
- `DisconnectFromMarker()` 메서드로 이전 마커 참조 무효화
- 새 Property Panel 인스턴스 생성으로 깨끗한 상태 보장

### 6. 향후 개선사항
- WeakEventManager를 활용한 메모리 누수 방지 구현 검토
- Behavior 기반의 바인딩 정리 자동화 검토
- 대량 마커 처리 시 성능 최적화 방안 검토

## 주요 학습내용
- WPF 바인딩 시스템의 내부 동작 원리와 타이밍 이슈
- PropertyChanged 콜백과 Behavior의 실행 순서
- 바인딩 오염 문제의 근본적 해결을 위한 설계 패턴들