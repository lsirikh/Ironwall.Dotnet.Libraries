# PLAN — 가짜 카메라 ONVIF 검색 → 조회 → 등록

## 목표
사용자 질문: "가짜 카메라가 onvif 로 검색되면 조회해서 등록할 수 있는 거지?"
시작 시점엔 불가(검색 기능 0건 · 가짜 팜도 WS-Discovery 무응답). 이를 가능하게 만든다.

1. 가짜 카메라 팜(tools/dummy-cameras)이 WS-Discovery Probe(UDP 3702, 239.255.255.250)에 ProbeMatch 로 응답.
2. GIS 카메라 장비 패널(Devices.Ui)에 [ONVIF 검색] → 목록 → [조회](장비 정보 · RTSP · PTZ 여부) → [추가](Temp-state Draft) → 기존 [저장] 으로 서버 등록.

## 결정
- D1 검색 · 조회 코드는 Devices.Ui 안(`Services/Onvif/`). Utils 는 UI 공용이라 맞지 않고 OnvifSolution 은 서브모듈이라 손대지 않는다.
- D2 조회는 운영 ONVIF 경로(OnvifSolution `IOnvifService.InitializeProfileAsync`). 서비스는 `OnvifServiceModule` 로 자체 컨테이너에서 처음 쓸 때 만든다(CameraProviderRegistry.CreateDefault 와 같은 방식, 앱 DI 등록 불필요). 시간 제한 15초(WaitAsync).
- D3 등록은 서버 직접 쓰기가 아니라 Draft 행 추가(Temp-state 정본). 저장은 기존 [저장] → 기존 검증(포트 ≥1 · 종류축 · 권한).
- D4 같은 IP:포트 카메라가 목록(저장분+Draft)에 있으면 "이미 등록됨" — 중복 Draft 차단. 저장 · 갱신 중(_processGate)엔 추가 거부.
- D5 종류축(TypeAxisCode)은 비워 둔다 — 수동 [추가]와 같다.
- D6 가짜 팜: FarmOptions 기본 = 끔(-1, 기존 시험이 3702 를 안 건드리게). CLI 기본 = 3702 멀티캐스트(`--discovery-port`, `--no-discovery`). 0.0.0.0 에 묶어 그룹 가입하지만 **이 PC 에서 온 Probe 에만** 답한다(XAddr 가 127.0.0.1 — LAN 에 가짜 카메라를 알리지 않음). 3702 를 못 잡으면 경고만 하고 계속.
- D7 GIS 검색기: 인터페이스마다(루프백 포함) 멀티캐스트 + 선택적 유니캐스트 대상. Probe 2회(같은 MessageID), EndpointReference 로 합침, RelatesTo 다른 응답 버림, Types 에 NetworkVideoTransmitter 없는 장치 버림, XAddr 는 IPv4 http 우선.
- D8 실카메라 계정은 코드 · 파일 · 커밋에 넣지 않는다. 계정 칸 기본값은 비움. 조회 때 쓴 계정이 그 행 Draft 에 저장된다.
- D9 화면 규칙(계약 시험): 콘솔 UI 프로젝트는 MaterialDesign 버튼 스타일 금지 → Console.Button(.Mini). 입력칸 HintAssist.Hint 글자는 "새 설명 문구"로 잡힘 → 짧은 TextBlock 라벨 + "?"(HelpKey Devices.Camera.OnvifDiscovery).

## 진행
- [x] 가짜 팜 `src/Onvif/WsDiscoveryResponder.cs` + Farm/CLI 배선 + 도움말 · Describe 줄
- [x] Devices.Ui `Services/Onvif/WsDiscoveryClient.cs`(Probe 생성 · ProbeMatches 파싱 · UDP)
- [x] Devices.Ui `Services/Onvif/OnvifCameraLookup.cs` + `OnvifCameraDraftFactory.cs`
- [x] Devices.Ui `ViewModels/Panels/OnvifDiscoveryViewModel.cs` + `Views/Panels/OnvifDiscoveryView.xaml` + 카메라 패널 도구줄 [ONVIF 검색](Radar 아이콘, Grid.Column 5) + 펼침 영역 + `Help/DevicesHelp.Camera.cs`
- [x] 시험: Devices.Ui `Tests/OnvifDiscoveryTests.cs` 23건 통과(파싱 · 루프백 UDP · 변환 · VM)
- [x] 시험: dummy-cameras `tests/OnvifDiscoveryFarmTests.cs` 6건 통과 — 팜 검색(유니캐스트 · 멀티캐스트 임시 포트) → OnvifSolution 실조회(PTZ · 고정 · 계정 거부) → VM → Draft
- [x] 기존 dummy-cameras 시험 50건(ONVIF · PTZ · 프리셋 · 생성기) 통과, Devices.Ui Help · Camera 53건 통과
- [x] `dotnet test Ironwall.Dotnet.Libraries.sln` 전부 통과(처음엔 Utils 계약 시험 2건 실패 → D9 로 수정)

## 발견
- Devices.Ui · dummy-cameras 는 Ironwall.Dotnet.Libraries.sln 밖 → 하네스 검사로는 안 돈다. 직접 --filter 로 돌렸다.
- Utils.Tests 의 계약 시험(HelpContractTests · ConsoleStyleContractTests)은 저장소 전체 XAML 을 훑는다 → Devices.Ui 뷰도 sln 검사에 걸린다.
- OnvifService.InitializeProfileAsync 는 캐시 키에 Ticks 를 붙여 호출마다 캐시가 자란다(서브모듈, 손대지 않음 — 조회는 사람이 누르는 횟수라 영향 작음).
- Write 도구는 BOM 없이 쓴다 → 한글 소스는 첫 글자에 U+FEFF 를 넣어 BOM 을 붙였다.

## 남은 일 / 못 한 것
- 실앱(GIS) 화면 확인은 안 했다(사용자 데스크톱에 창 띄우는 시험 금지 규칙). XAML 은 빌드 · 계약 시험만 통과.
- Devices.Ui 전체 시험 묶음은 돌리지 않았다(창을 띄우는 WPF 시험 포함 가능) — 관련 필터(ONVIF · Help · Camera)만.
- 실카메라(2169 · 2170 · 2171) 검색은 시험 서버 망에서만 가능 — 이 작업에서 실행하지 않음.
