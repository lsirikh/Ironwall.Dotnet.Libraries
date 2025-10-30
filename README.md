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

#### Ironwall.Dotnet.Libraries.Devices.Ui
장치 UI 컴포넌트
- 장치 목록 ViewModel
- 장치 속성 다이얼로그

#### Ironwall.Dotnet.Libraries.Events
이벤트 처리
- Detection, Malfunction, Connection 이벤트
- 이벤트 모델 및 프로바이더
- 이벤트 카드 시스템

#### Ironwall.Dotnet.Libraries.Events.Db
이벤트 데이터베이스 서비스
- 이벤트 로그 저장
- 이벤트 히스토리 조회

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

### 지도 및 시각화
| 라이브러리 | 용도 |
|------|------|
| GMap.NET.Core | 지도 엔진 |
| GMap.NET.WindowsPresentation | WPF 지도 컨트롤 |

## 문서

상세한 기술 문서는 다음을 참조하세요:

### 기술 문서 (Design 폴더)

#### 서비스 아키텍처
- **[서비스 아키텍처 보고서](Design/MonitoringSolution_ServiceArchitecture_Report.md)**
  - DomainService, MessageService 분석
  - 6개 데이터베이스 서비스 상세
  - Redis, NATS 메시징 서비스
  - Sound, ONVIF 서비스
  - 8가지 아키텍처 패턴
  - 서비스 라이프사이클 및 등록

#### Gateway 라이브러리
- **[Gateway 매뉴얼](Design/GatewayManual.md)** (v1.0.1)
  - Gateway 라이브러리 기술 명세
  - 프로젝트 구조 및 아키텍처
  - Behavior 패턴 구현
  - XAML 통합 가이드

- **[Gateway 매뉴얼 보고서](Design/GatewayManual_Report.md)** (v1.0.1)
  - Gateway 개발 과정 보고서
  - Phase 1-8 구현 단계
  - Behavior 패턴 도입 배경
  - 문제 해결 과정

- **[Gateway 통합 계획](Design/GatewayIntegration_Plan.md)**
  - Gateway 모듈 통합 전략
  - Dotnet.Monitoring.Solution 통합 방법
  - 데이터베이스 스키마

#### NATS 라이브러리
- **[NATS 라이브러리 가이드](Design/Nats_Dotnet_Library.md)**
  - NATS.Client.Core v2 통합
  - Pub/Sub 및 Request/Reply 패턴
  - 설정 및 사용 예제

- **[NATS 라이브러리 보고서](Design/Nats_Dotnet_Library_Report.md)**
  - NATS 개발 과정 문서
  - 구현 단계별 상세 설명
  - 문제 해결 및 최적화

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

현재 단위 테스트 프로젝트는 구성 중입니다.

### 패키지 게시 (내부용)

```bash
dotnet pack Ironwall.Dotnet.Libraries.Base.csproj --configuration Release --output nupkgs
```

## 변경 이력

### v1.3.1 (2025-10-28)

**작업자:** GH.LEE

#### 주요 변경사항

##### Ironwall.Dotnet.Libraries.GMaps
- **MapSetupViewModel 개선**
  - `MapTypes` 속성: EnumMapProvider 기반으로 "Defined", "Custom" 제공
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

##### Ironwall.Dotnet.Libraries.Gateway
- **Behavior 패턴 도입**
  - `GatewayEventSelectedItemsBehavior` 구현
  - DataGrid 선택 항목 동기화 개선
  - TwoWay 바인딩 지원

#### 버그 수정
- MaterialDesign PackIconKind "MountainAltitude" → "ImageFilterHdr" 변경
- MapSetupView XAML 바인딩 오류 수정

#### 설계 개선
- MapType과 MapName의 명확한 구분
  - MapType: 제공자 타입 (Defined/Custom)
  - MapName: 실제 지도 이름 (Google 위성지도, OpenStreetMap 등)
- 타일 디렉토리 선택 UI 패턴 표준화 (읽기 전용 TextBox + 버튼)

---

### v1.2.4 (2025-08-28)

**작업자:** GH.LEE

#### 추가된 파일
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

#### 주요 개선사항
- PIDS 심볼 시각화 시스템 구현
- 장치 타입별 색상 코딩
- 마커 스타일 테마 추가

---

### v1.2.0 이전 (2025-09-01)

**작업자:** GH.LEE

#### 작업내용

##### 1. WPF Property Panel 바인딩 오염 문제 해결
- **문제**: 마커 선택 시 이전 Property Panel의 바인딩이 새 마커 속성을 오염시키는 현상
- **증상**: 두 번째, 세 번째 마커 선택 시 Title이 빈 문자열로, Size가 32x32로 변경
- **원인**: XAML 바인딩(`SelectedMarker="{Binding SelectedMarker}"`)이 자동으로 `OnSelectedMarkerChanged` 트리거

##### 2. 근본 원인 파악
- `GMapPropertyBaseControl`의 `OnSelectedMarkerChanged`에서 중복된 `SetupMarkerBindings()` 호출
- XAML에서 SelectedMarker 변경 시 기존 Property Panel의 기본값이 새 마커에 즉시 적용
- WPF 바인딩 타이밍 문제: PropertyChanged 콜백이 Behavior보다 먼저 실행

##### 3. 해결 방법
- 이전 마커와의 연결을 완전히 차단
- Property Panel 완전 재생성을 통한 바인딩 오염 근본 차단
- `DisconnectFromMarker()` 메서드로 이전 마커 참조 무효화
- 새 Property Panel 인스턴스 생성으로 깨끗한 상태 보장

##### 4. 학습 내용
- WPF 바인딩 시스템의 내부 동작 원리와 타이밍 이슈
- PropertyChanged 콜백과 Behavior의 실행 순서
- 바인딩 오염 문제의 근본적 해결을 위한 설계 패턴

---

## 라이선스

**Private/Proprietary License**

Copyright (C) 2023-2025 Sensorway Co., Ltd. All rights reserved.

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

**문서 버전**: 2.0.0
**최종 업데이트**: 2025-10-30
**문서 상태**: ✅ 최종 승인
