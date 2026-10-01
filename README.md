# Ironwall .NET Libraries

.NET 8 기반 관제 애플리케이션에서 사용하는 공통 라이브러리 모음입니다. 장치·이벤트·계정 데이터와 데이터베이스 접근, 지도와 영상 UI, ONVIF 및 Redis 연동을 모듈로 나눕니다.

## 주요 영역

| 모듈 | 역할 |
|---|---|
| `Base`, `Utils`, `ViewModel`, `Framework` | 공통 서비스, 데이터 관리, WPF MVVM 기반 |
| `Accounts`, `Devices`, `Events` 및 `.Db`, `.Ui` | 도메인 모델, 저장, 화면 |
| `GMaps`, `Canvas`, `AdornerDecorator` | 지도와 도형 기반 UI |
| `OnvifSolution`, `Streaming`, `Dotnet.Streaming.UI` | 카메라 제어와 영상 표시 |
| `Redis`, `Api`, `Api.Aligo` | 메시지 및 외부 API 연동 |

실제 폴더명에는 `Ironwall.Dotnet.Libraries.` 접두사가 붙습니다. [솔루션](Ironwall.Dotnet.Libraries.sln)에서 프로젝트 간 참조를 확인할 수 있습니다.

## 개발 환경과 사용 방식

Windows / .NET 8 / WPF를 중심으로 Autofac, Caliburn.Micro 등을 사용합니다. 데이터베이스나 영상 기능의 런타임 의존성은 각 `.csproj`를 확인해야 합니다.

이 저장소는 하나의 완성 앱이 아니라 여러 앱에서 사용하는 라이브러리 모음입니다. 일부 프로젝트는 다른 저장소의 프로젝트를 참조합니다. 예를 들어 Aligo 모듈은 Gym 메시지 프로젝트를 참조하므로 전체 솔루션 빌드 전에 경로를 맞춰야 합니다. 사용할 모듈부터 의존성을 확인하는 것이 좋습니다.

기존 Sensorway Framework의 코드와 포함된 외부 라이브러리의 출처를 유지합니다. 라이브러리별로 대상 프레임워크와 네이티브 실행 조건이 다를 수 있습니다.
