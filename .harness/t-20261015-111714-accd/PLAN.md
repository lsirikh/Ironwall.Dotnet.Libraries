# PLAN — 설정 파일 경로를 한곳으로(Config\)

## 목표
런타임에 쓰는 appsettings.json · login-prefs.json 을 설치 폴더 루트가 아닌 `{실행 폴더}\Config\` 에 둔다(SSO 회신 Q-3 — 설치 폴더 루트 사용자 쓰기 권한 걷어내기의 라이브러리 쪽 준비). 메인 솔루션 적용은 다음 작업.

## 진행
- [x] `Ironwall.Dotnet.Libraries.Base\Services\AppSettingsLocation.cs` 신설(공개 정적 클래스)
  - `ResolveReadPath(base, file, exists?)` — Config 있으면 Config, 없고 루트 있으면 루트, 둘 다 없으면 Config
  - `PrepareWrite(base, file)` — Config 폴더 생성, Config 에 없고 루트에 있으면 복사(이관), Config 경로 반환
  - 편의: `AppSettingsPath` · `AppSettingsReadPath` · `LoginPrefsReadPath` · `PrepareAppSettingsWrite()` · `PrepareLoginPrefsWrite()` · `GetConfigPath`
- [x] 호출부 교체: GMaps.Ui `MapSettingsHelper`(읽기=ReadPath, 쓰기=PrepareWrite, `GetSettingsFilePath` 제거) · `Symbol3DFeature` · Monitoring.Models `SensorPresetSettings` · Accounts.Api `ApiAccountGateway`(login-prefs)
- [x] 기존 시험의 설정 파일 경로를 새 정본 위치로(NavKit.SettingsPath · PaletteAndPropertyTests · TrackingPlaybackScenarioTests) — 단언은 그대로
- [x] 단위 시험 `Ironwall.Dotnet.Libraries.Watchdog.Tests\AppSettingsLocationTests.cs`(새 위치 · 옛 위치만 · 둘 다 · 둘 다 없음 · 이관)
- [x] `dotnet test Ironwall.Dotnet.Libraries.sln` 통과 확인(실패 0, Watchdog.Tests 12건 통과)
- [x] sln 밖 지도 시험 3개(MapNav · MapEdit · MapAux) 컴파일 확인(오류 0) — 창을 띄우는 WPF 시험이라 실행은 하지 않음

## 결정
- 이관은 **복사**(옛 루트 파일은 지우지 않음): 새 설치기에선 루트가 쓰기 불가라 지울 수 없고, 메인 솔루션은 다음 작업 전까지 루트를 읽는다.
- CameraPopupHost 는 appsettings · login-prefs 를 직접 읽지 않음(grep 0건) → 손대지 않음.
- 시험 위치: Base 전용 시험 프로젝트가 없어 Base 를 이미 (Watchdog 경유로) 참조하는 가벼운 `Watchdog.Tests` 에 둠(sln 수정 회피).
- 기존 지도 시험은 bin 루트 appsettings.json 에 직접 쓰고 읽었는데, 이관 뒤엔 Config 파일이 남아 루트를 가리므로 시험 도우미 경로를 정본(Config)으로 바꿈 — 시험 의도 · 단언은 불변.

## 남은 일
- 메인 솔루션(Dotnet.Monitoring.Solution): IConfiguration `AddJsonFile` · 자체 저장 코드가 `AppSettingsLocation` 을 쓰게, 설치기에서 루트 쓰기 권한 제거.
