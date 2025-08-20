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

## 5. Ironwall.Dotnet.Libraries.Api.Aligo 소개

### 5.1 개요
`Ironwall.Dotnet.Libraries.Api.Aligo`는 **Aligo 문자/EMS API**를 사용하여 **단문(SMS), 장문(LMS), 멀티미디어 문자(MMS)** 등을 전송할 수 있는 서비스 라이브러리입니다.  
이 라이브러리는 **Autofac 기반의 의존성 주입(DI)** 구조를 사용하며,  
단위 테스트를 위해 `xUnit`을 사용하여 API 기능을 검증할 수 있도록 설계되었습니다.

### 5.2 주요 기능
- **Aligo SMS API를 이용한 단일/대량 문자 전송**
- **예약 문자 발송 기능 지원**
- **문자 전송 내역 조회**
- **발송 취소 기능(지원예정)**
- **잔여 포인트 확인 기능(지원예정)**
- **첨부 이미지 (최대 3개) 지원**


#### 5.3 프로젝트 구성

#### **📂 Models**
> Aligo 문자 전송 및 응답 모델 정의

- `ResponseModel.cs`  
  - API 응답 데이터를 저장하는 모델
- `SendAvailableModel.cs`  
  - 문자 전송 가능 여부를 확인하는 모델
- `SenderModel.cs`  
  - 발신자 정보 모델
- `SenderSpecModel.cs`  
  - 발신자 상세 정보 모델
- `SendListResponseModel.cs`  
  - 문자 전송 내역 조회 모델
- `SendMessageResponseModel.cs`  
  - 개별 문자 전송 응답 모델
- `SendResponseModel.cs`  
  - 전송 결과 모델

#### **📂 Modules**
> API 모듈 등록을 위한 클래스

- `AligoModule.cs`  
  - `Autofac`을 활용한 **의존성 주입(DI) 컨테이너 등록**을 수행하는 모듈 클래스

#### **📂 Providers**
> Aligo 문자 메시지 관련 데이터 제공자

- `EmsMessageProvider.cs`  
  - EMS 메시지 생성 및 관리

#### **📂 Services**
> Aligo API 연동 및 문자 전송 기능 제공

- `AligoService.cs`  
  - Aligo API를 활용하여 SMS/LMS/MMS를 전송하는 핵심 서비스 클래스
- `IAligoService.cs`  
  - Aligo 문자 전송 인터페이스 정의

#### **📂 Tests**
> Aligo API 서비스 테스트 관련 파일

- `TestAligoService.cs`  
  - 단위 테스트 코드 포함
- `sample1.jpg`, `sample2.jpg`  
  - MMS 테스트를 위한 샘플 이미지

#### 개발 환경
- **.NET Version**: `net8.0-windows`
- **언어**: `C#`
- **DI Framework**: `Autofac`
- **테스트 프레임워크**: `xUnit`

---

## 6. Ironwall.Dotnet.Libraries.Db 소개

### 6.1 개요
`Ironwall.Dotnet.Libraries.Db`는 **MariaDB/MySQL 기반의 데이터베이스 서비스**를 제공하는 라이브러리입니다.  
이 라이브러리는 `Dapper`, `EntityFramework`, `MySql.Data` 등의 ORM 및 DB 관리 패키지를 사용하여  
**DB 연결, 테이블 생성, 데이터 삽입/조회/수정/삭제** 기능을 제공합니다.  

또한, `ExcelDataReader`와 `ClosedXML`을 활용하여 **엑셀 데이터를 DB로 변환 및 등록**할 수 있습니다.


#### 6.2. 프로젝트 구성

#### **📂 Models**
> DB 설정 및 모델 정의

- `DbSetupModel.cs`  
  - DB 연결 정보(IP, 포트, 계정 정보 등)를 저장하는 설정 모델

#### **📂 Modules**
> DB 모듈 등록을 위한 클래스

- `DbModule.cs`  
  - `Autofac`을 활용한 **의존성 주입(DI) 컨테이너 등록**을 수행하는 모듈 클래스

#### **📂 Services**
> DB 연동 및 데이터 관리 서비스 제공

- `DbServiceForGym.cs`  
  - Gym Manager 시스템을 위한 **DB 관리 서비스**
- `IDbServiceForGym.cs`  
  - DB 서비스 인터페이스 정의

#### **📂 Tests**
> DB 서비스 테스트 관련 파일

- `TestDbServiceForGym.cs`  
  - `DbServiceForGym`에 대한 **단위 테스트 코드**
- `TestExcelImporter.cs`  
  - `ExcelImporter`를 활용한 **엑셀 데이터 DB 등록 테스트**
- `sample1.jpg`, `sample2.jpg`  
  - 테스트용 샘플 이미지

#### **📂 Utils**
> 엑셀 데이터를 읽어와 DB에 등록하는 유틸리티

- `ExcelImporter.cs`  
  - `ClosedXML`, `ExcelDataReader`를 활용한 **엑셀 데이터 파싱 및 DB 등록**
- `IExcelImporter.cs`  
  - 엑셀 데이터 등록 인터페이스 정의

#### 개발 환경
- **.NET Version**: `net8.0-windows`
- **언어**: `C#`
- **DBMS**: `MariaDB` / `MySQL`
- **ORM 및 SQL 매핑**: `Dapper`, `EntityFramework`
- **테스트 프레임워크**: `xUnit`

---

### Update Date: 2025/03/16
### Version : v1.0.0

> 현재 등록된 라이브러리 목록

* Ironwall.Dotnet.Libraries.Base
* Ironwall.Dotnet.Libraries.ViewModel
* Ironwall.Dotnet.Framework.Models
* Ironwall.Dotnet.Libraries.Api
* Ironwall.Dotnet.Libraries.Api.Aligo
* Ironwall.Dotnet.Libraries.Db
* Ironwall.Dotnet.Libraries.Utils

<hr>

### Update Date: 2025/03/30

> 업데이트된 라이브러리 목록

* Ironwall.Dotnet.Libraries.Base
* Version : v1.1.0

  1. SplashScreen 연동을 위한 Caliburn.micro 이벤트 메시지 추가

* Ironwall.Dotnet.Libraries.Api.Aligo
* Version : v1.0.0

  1. 단위 테스트 코드 일부 수정

* Ironwall.Dotnet.Libraries.Db
* Version : v1.1.0

  1. ExcelImporter 적용 가능 기능 구현 및 동작 확인
  2. TestExcelImporter 단위테스터 구현 및 동작 확인

<hr>
