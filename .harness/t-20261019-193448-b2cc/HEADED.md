# HEADED — 헤디드 2단계 실기 시나리오 (t-20261019-193448-b2cc)

헤드리스로 확정할 수 없는 항목(실제 마우스 끌기 · WebView2 3D 화면 · 렌더 · 떨림 체감 · 파일 고르기 창 · 실제 Ctrl/Shift 조합 · 네이티브 창 3D)만 모았다.
헤드리스로 이미 확정한 것은 REPORT.md · areas/*.md 의 시나리오 표에 있고 여기서 다시 하지 않는다.
코디네이터가 이 순서 그대로 스크립트로 옮겨 돌린다.

## 0. 돌리기 전 규칙
- **한 번에 하나만**: 헤디드 실행기 · UIA 감시 · 하네스 빌드를 겹치지 않는다(겹치면 PC 멈춤 · 키보드/마우스 점유).
- 사용자가 PC 를 쓰지 않을 때만. `quser` 의 STATE 가 Active 인지 먼저 본다(Disc 면 SendInput 이 거부돼 전부 실패). 원격이 끊길 수 있으면 `tools/hold-desktop-for-ui-tests.ps1` 로 콘솔을 넘긴다.
- 녹화 영상은 만들지 않는다 — 판정은 **UIA 값 · 앱 로그 줄 · 캡처 PNG**. 실행 중에는 Toast 로 지금 시험 단계를 띄운다.
- 운영 · 원격 서버(123.141.236.253)에는 절대 쓰지 않는다.

## 1. 시험 자료 준비
1. **GIS 빌드**: 이 작업이 v2.6 에 합쳐진 뒤의 메인 앱(Dotnet.Monitoring.Solution, v0.5) 개발 빌드. 실행 폴더 appsettings 의 `Url` 이 로컬 시험 서버(`https://localhost:8000/api`)인지, 앱 로그의 서버 판본 프로브 줄을 먼저 확인한다.
2. **로컬 GOP 시험 서버**: `C:\workspace_python\api-test-server`(Docker 컨테이너 `api-test-server` 8000 · `api-test-postgres`). 컨테이너를 새로 만들지 말 것(INIT_SAMPLE_DATA 함정). 같은 계정 재로그인은 실행 중인 GIS 세션을 내쫓으므로(단일 세션) 진단 로그인은 최소화.
3. **map-3d 로컬 서버**: 이 작업의 `map3d.patch` 가 적용된 map-3d 에서 `.venv\Scripts\python -m map3d serve --port 8765`. GIS 설정 「3D 지도」 서버 주소 = `http://127.0.0.1:8765`.
4. **시험 지역**: 지도 데이터가 있는 시험 자리(예: 안양 시험 영역) 하나. 시험용 3D 영역은 이 시험에서 새로 만든다(이름 앞에 `HT-` — 정리 때 찾기 쉽게).
5. **시험 그림**: `%TEMP%\headed-b2cc\` 에 ① 불투명 실사 JPG(1000×800 정도) ② 바깥이 투명한 PNG(L 자 모양) ③ 1×1 PNG 를 둔다(정리 때 폴더째 지움).
6. **설정 기록**: 시작 전 「3D 지도」 설정(들어가는 방식 · 서버 주소 · 네이티브 창 사용 여부)을 적어 두고 끝에 원복.

## 2. 실행 순서
A(3D 진입/이탈) → B(3D 영역) → C(3D 상호작용) → D(2D 편집) → E(오버레이 이미지) → F(3D 품질 눈 확인) → 3. 정리.
각 영역 표의 "판정" 칸: UIA = AutomationId 의 값/보임 · 로그 = 앱 로그(UTF-8)에서 그 줄을 찾음 · 캡처 = 그 순간 PNG 1장.
공통 상태 판정: 지도 루트 `GMaps.Map.Root` 의 ItemStatus 가 "3D" / "2D".

## 3. 정리(반드시 — 지운 것을 보고에 적는다)
- map-3d: 시험으로 만든 `HT-` 영역(작업) 지우기 — GIS 영역 목록 「지우기…」 또는 map-3d 화면. 건물 높이 직접 입력은 영역 메뉴 「모두 자동으로…」 로 되돌린 뒤 지운다.
- GIS: 시험으로 추가한 오버레이 이미지 · 심볼 · 묶음 회전한 시험 심볼(시험 그룹)을 지우거나 Undo 로 되돌린다. 레이어 잠금을 원래대로.
- 설정 원복(1-6), `%TEMP%\headed-b2cc\` 삭제, 시험 중 띄운 map-3d 서버 · GIS 프로세스 종료, 남은 msedgewebview2 · testhost 확인.

# 영역별 단계
## A 3D 진입/이탈

공통 판정 로그 줄(코드의 정확한 문자열):
- 진입: `[Map3D] 바로 들어가기(<enter-button|area-view>) job=<id> mode=<auto|manual>` → `[Map3D] 진입 시작 job=<id> zoom=<z> reuse=<True|False> preloaded=<True|False> prewarmed=<True|False>` → (`[Map3D] ready job=<id>` · `[Map3D] rendered job=<id> items=<n>`) → `[Map3D] 3D 표시 job=<id> host=<composition|hwnd>` → `[Map3D] 전환 시간 total=<s> webview=<s|-> page=<s|-> snapshot=<s|-> rendered=<s|-> reveal=<s> (prewarmed=<true|false>, preloaded=<true|false>)`
- rendered 시간 초과: `[Map3D] rendered 를 1.5 s 안에 받지 못함 → 그냥 펼침 job=<id>`(경고)
- 나감: `[Map3D] 2D 복귀(<exit-button|esc|input-mode|settings-off|host-fallback|load-failed|area-view|area-delete|list-missing|scene-missing|…>) zoom=<z> bearing=<b>` → `[Map3D] 2D 입력 상태(<원인> · now) hit=True enabled=True visible=True 막는조상=없음 가운데=지도 캡처=없음 … 3D펼침=False … 지도상태=없음` → 같은 줄 `· idle)`
- 남은 제스처: `[Map3D] 나가며 남은 2D 입력 상태 정리: <imageDrag,rubberBand,linePress,panSuppress,coreDrag,capture 중>`(경고)
- 미리: `[Map3D] WebView2 미리 데우기 시작` · `[Map3D] WebView2 미리 데우기 끝 <s>s` · `[Map3D] 미리 불러오기 job=<id> zoom=<z>`
- 준비 중 취소: `[Map3D] 로딩 중 게이트 이탈(zoom=<z>) → 취소` · `[Map3D] 로딩 중 입력 모드 시작 → 취소`
- 영역 보기: `[Map3D] 영역 보기 id=<id> → (<lat>,<lon>) zoom=<z>`

| 단계 | 조작 · AutomationId | 기대 | 판정 |
|---|---|---|---|
| H-A1 처음 시점(home) 실제 전송 | 줌 17 · 영역 안에서 `Map.Map3D.EnterButton` 누름(수동) / 자동은 줌 17 로 | 영역 전체가 화면에 들어오는 비스듬한 시점으로 0.6 s 눕기, 회전 영역 「이 영역 3D 로 보기」(`GMaps.Map3D.AreaList` 항목 · `GMaps.Map3D.AreaBadge.{id}` 우클릭)는 사각형이 반듯하게 | `GMaps.Map.Root` ItemStatus "3D" · `GMaps.Map3D.ExitButton` 보임 · 로그 "3D 표시 job=" · 캡처(영역 네 변이 화면 안) |
| H-A2 축소 한계 체감(영역 F) | 3D 에서 휠로 끝까지 축소 | 영역이 너무 작아지기 전에 멈춤 · 2D 로 안 나감 | ItemStatus "3D" 유지 · 캡처 |
| H-A3 hwnd Esc 실기 | 원격(RDP) 또는 `UseCompositionHost=false` → 3D 진입 → Esc | 나감 · 2D 입력 즉시 동작 | 로그 "3D 표시 job=… host=hwnd" → "2D 복귀(esc)" · ItemStatus "2D" · `GMaps.Map3D.ExitButton` 사라짐 |
| H-A4 composition 2D 따라가기 · 나간 뒤 방위 | 3D 에서 마우스로 돌리고(약 60°) 확대/축소 → `GMaps.Map3D.ExitButton` | 3D 동안 나침반 · 축척 · 좌표 칩(고도 줄 "58.3 m" 꼴)이 3D 를 따라감, "3D 가능" 칩 `GMaps.Map3D.Chip` 은 안 보임, 줌 컨트롤(`GMaps.MapZoom.Label` · `GMaps.MapZoom.ZoomInButton` · `GMaps.MapZoom.ZoomOutButton` · `GMaps.MapZoom.Slider`) 숨김 · 나가면 들어가기 직전 2D 자리 · 방위 | "2D 복귀(exit-button) zoom=<들어가기 전 줌> bearing=<들어가기 전 방위>" · 나간 뒤 `GMaps.MapZoom.*` 다시 보임 |
| H-A5 나간 뒤 실제 마우스 | 나간 직후 지도 클릭 · 우클릭 · 휠 위/아래 · 끌기, 2D 끌던 중 3D 펼침(자동 줌 진입 순간 끌기) 뒤 나가기 | 모두 즉시 동작(D-3: 끌던 중 펼친 경우도) | "2D 입력 상태(exit-button · now|idle) hit=True … 지도상태=없음" · 끌다 들어간 경우 "나가며 남은 2D 입력 상태 정리: …coreDrag" |
| H-A6 미리 데우기 · 미리 불러오기 | 앱 시작 → 지도 뜬 뒤 대기 → 줌 15 · 영역 안 → 줌 17 | 첫 진입이 짧다 | "WebView2 미리 데우기 시작/끝" · "미리 불러오기 job=" · "전환 시간 … (prewarmed=true, preloaded=true)" |
| H-A7 진행 패널 | 처음(미리 안 연) 진입 | `GMaps.Map3D.Progress` 에 "3D 지도로 전환합니다"(`GMaps.Map3D.Progress.Title`) · 단계 %(`GMaps.Map3D.Progress.Detail`) → 펼침, 나갈 때 "2D 지도로 돌아갑니다" 0.5 s | UIA 글 · 로그 순서 |
| H-A8 만들기 취소 뒤 자동 진입(D-4) | 자동 · 영역 안 줌 17 에서 `GMaps.Map3D.DrawButton` → 옆에 새 영역 → [만들기] → 진행 판 `GMaps.Map3D.CancelBake` | 취소 직후 지도를 안 움직여도 옛 영역 3D 로 | "만들기 취소 응답 … result=requested" 뒤 "진입 시작 job=<옛 id>" |
| H-A9 거절된 영역 보기 뒤(D-1) | 편집 모드에서 영역 목록 「이 영역 3D 로 보기」(거절 안내) → 편집 끔 → 다른 자리 · 줌 18 → `Map.Map3D.EnterButton` → `GMaps.Map3D.ExitButton` | 줌 18 · 누른 자리로 돌아옴 | "2D 복귀(exit-button) zoom=18.0" |


## B 3D 영역

공통: 판정 로그는 앱 로그(UTF-8)의 `[Map3D]` 줄. 상태 줄 = 지도 아래 조준 상태(AimStatusMessage). 3D/2D 는 지도 루트 ItemStatus "3D"/"2D"(컴포지션 WebView2 는 UIA 트리에 없다).
「만들기 취소」 id `GMaps.Map3D.CancelBake` 는 3곳 — 진행 판(`GMaps.Map3D.Progress` 아래), 배지 우클릭 ContextMenu, 지도 메뉴 `GMaps.Map3D.AreaList` 하위 메뉴 — 부모 범위로 구분한다.

| # | 단계 | AutomationId | 기대 | 판정(UIA 값 / 로그 줄 / 캡처) |
|---|---|---|---|---|
| H-1 | 지도 30° 로 돌림 → 「3D 영역 그리기」 → 실제 마우스로 400×200 px 러버밴드 | `GMaps.Map3D.DrawButton` | 마퀴 옆 크기 라벨(가로 × 세로 m), 놓으면 만들기 판 | `GMaps.Map3D.BakeOptions` 보임 · 로그 `[Map3D] 만들기 패널 — 영역 안 오버레이 이미지 {n}개, 기본={mode}, 줌={z}` · 캡처(마퀴 = 화면 사각형) |
| H-2 | 그리기 모드에서 3×3 px 드래그(4 px 미만) | `GMaps.Map3D.DrawButton` | 무시 — 판 안 열림 · 그리기 모드 유지(십자 커서) | `GMaps.Map3D.BakeOptions` 없음 · `만들기 패널` 로그 없음 · 상태 줄 "지도에서 드래그해 3D 로 만들 영역을 그리세요 (Enter = 지금 화면 · ESC = 취소)" 그대로 |
| H-3 | 2 km 넘게 그림 | — | 다시 그리기 모드 + "영역이 너무 큽니다 …" | 로그 `[Map3D] 영역이 너무 큼 {가로 × 세로} → 다시 그리기` |
| H-4 | 회전 영역이 있는 지도를 키 · 나침반 · 3D 동기로 돌림(팬 없이) | `GMaps.Map3D.AreaBadge.{id}` | 테두리 · 배지가 다음 프레임에 새 방위로(옛 방위로 남지 않음) | 캡처 2장(전/후, 팬 없음) — 꼭짓점이 영역 모서리에 붙음 |
| H-5 | 영역 안 이미지가 있는 곳에 영역 그리기 → 판 라디오 4개를 차례로 누름 → [만들기] | `GMaps.Map3D.BakeOptions.Modes` · `.Extract` · `.Photo` · `.Mask` · `.Tiles` · `.EnterZoom` · `.Create` · `.Title` · `.Summary` | 라디오 하나만 선택 · Summary "영역 안 오버레이 이미지 n개" · Title "3D 만들기" | 로그 `[Map3D] 굽기 요청 bbox=[…] rect={…|-} overlay={mode} images={n} enterZoom={z}` |
| H-6 | 이미지 없는 곳 영역 → 판 | `GMaps.Map3D.BakeOptions.Modes` | 라디오 숨김, 줌만 | Summary "영역 안 오버레이 이미지 0개" · 로그 `overlay=tiles images=0` |
| H-7 | 판 [취소] | `GMaps.Map3D.BakeOptions.Cancel` | 판 닫힘 · 요청 없음 | 로그 `[Map3D] 굽기 방식 고르기 취소` · `굽기 요청` 줄 없음 |
| H-8 | 굽는 중 진행 판 「만들기 취소」 | `GMaps.Map3D.Progress`(.Title/.Detail) 아래 `GMaps.Map3D.CancelBake` | 진행 판 닫힘 · "3D 만들기를 취소했습니다 — 이전에 만든 3D 는 그대로입니다" | 로그 `[Map3D] 만들기 취소 요청 job={job}` → `[Map3D] 만들기 취소 응답 job={job} result=…` → `[Map3D] 굽기 진행 조회 끊음(만들기 취소)` |
| H-9 | 영역 배지 우클릭(다시 만드는 중 아님) | `GMaps.Map3D.AreaBadge.{id}` | 메뉴: 이 영역 3D 로 보기 · 3D 줌 바꾸기… · (직접 입력 높이 n개 — 모두 자동으로…) · 다시 만들기 · 지우기… | UIA 메뉴 항목 이름 |
| H-10 | 「다시 만들기」 → [만들기] → 굽는 중 같은 배지 우클릭 · 「3D 영역 목록」 | `GMaps.Map3D.AreaBadge.{id}` · `GMaps.Map3D.AreaList` | 맨 위 「만들기 취소」(`GMaps.Map3D.CancelBake`), **「다시 만들기」 · 「지우기…」 없음**(D1), 목록 줄 끝 "· 다시 만드는 중" | UIA 메뉴 항목 · 로그 `[Map3D] 영역 다시 만들기 id={id}` |
| H-11 | H-10 전에 열어 둔 옛 메뉴의 「지우기…」를 굽기 시작 뒤 누름(가능하면) | — | 확인 창 없이 "만드는 중에는 지울 수 없습니다 — 「만들기 취소」 뒤 지우세요" | 로그 `[Map3D] 다시 만드는 중 — 영역 지우기 막음 id={id}` |
| H-12 | 다시 만들기 끝 | — | 새 장면 · 옛 영역 사라짐 | 로그 `[Map3D] 다시 만들기 — 옛 장면 지움 {old} → {new}` · `[Map3D] 장면 내려놓음(rebake) job={old} 보던중=False` · 상태 줄 "3D 준비됨 — …" |
| H-13 | scene-missing 실제 흐름: map-3d 웹 화면에서 영역을 지운 뒤 이 GIS 에서 그 영역 「이 영역 3D 로 보기」 | 지도 루트 ItemStatus | 2D 로 돌아오고 "이 3D 장면은 지워졌습니다 — 2D 지도로 돌아왔고 3D 영역 목록을 새로 고칩니다", 목록에서 빠짐 | 로그 `[Map3D] 3D 화면 불러오기 실패 job={id} reason=scene-missing` → `[Map3D] 장면 내려놓음(scene-missing) job={id} 보던중=…` → `[Map3D] 영역 목록 {n}개` |
| H-14 | 회전 영역 「이 영역 3D 로 보기」 | 지도 루트 ItemStatus | 3D 가 영역 회전각으로 열림(사각형이 화면에 반듯) | 로그 `[Map3D] 영역 보기 id={id} → ({lat},{lon}) zoom={z}` · `[Map3D] 3D 표시 job={id} host=…` · 캡처 |
| H-15 | map-3d 를 끈 채 보던 3D 영역 지우기 — D6 확인 | — | 2D 로 나가고 "3D 서버(map-3d)가 꺼져 있습니다", 영역 남음 | 로그 `[Map3D] 2D 복귀(area-delete) …` · `[Map3D] 영역 지우기 — 서버 닿지 않음: …` |


## C 3D 상호작용
| # | 단계 | AutomationId | 기대 | 판정(UIA 값 / 로그 줄 / 캡처) |
|---|---|---|---|---|
| H-C1 | 3D(composition)에서 카메라 상자를 연 채 지도를 관성으로 크게 돌리고 줌 | GMaps.Map3D.View · GMaps.Map3D.OverlayLayer · GMaps.CameraPopup.Video | 상자가 마커를 따라가며 앞뒤로 떨리지 않음(C-38/C-40) | 캡처 연속 3장 · 체감. 로그 `[Map3D][perf]` 줄 |
| H-C2 | 관성 회전 중 상자 머리 띠를 잡고 끌기 → 놓기 | GMaps.CameraPopup.* | 끄는 동안 마우스만 따름, 놓은 뒤 3D 자리 규칙으로(C-40) | 캡처 · 2D 로 나가도 같은 자리 관계(C-34) |
| H-C3 | 3D 카메라 마커 실제 더블클릭(400 ms 안 두 번) | GMaps.Map3D.View | 상자 1개만 열림 · click 두 번으로 카드가 먼저 열리지 않음(C-25) | 로그 `[Map3D] pick dblclick kind=pids id=…` 1줄 |
| H-C4 | 3D 에서 오른쪽 버튼으로 끌기(회전) 후 놓기 | GMaps.Map3D.View | 우클릭 메뉴가 뜨지 않음(끌기는 context 아님) | 로그에 `3D 위 우클릭 메뉴 열기` 없음 |
| H-C5 | 3D 마커 우클릭 | GMaps.Map3D.View | 메뉴가 누른 자리에 뜸(DPI 150% 포함, C-27) | 로그 `[Map3D] 3D 위 우클릭 메뉴 열기 — 항목 n개 @(x,y)` · `3D 위 우클릭 메뉴 닫힘 — 열린 지 … ms` · 캡처 |
| H-C6 | 원격(RDP) = hwnd 3D 에서 카메라 더블클릭 | GMaps.Map3D.View(네이티브 창) | 2D 로 돌아와 상자가 보임(C-24) | 로그 `[Map3D] 3D 표시 job=… host=hwnd` → `[Map3D] 2D 복귀(camera-popup)` |
| H-C7 | hwnd 3D 에서 건물 클릭 → Esc | — | 판 안 뜸 · 안내 글 · 첫 Esc 로 2D(C-101) | 로그 `[Map3D] 건물 고름 — 네이티브 창 3D 라 「건물 높이」 판을 열지 않음` → `[Map3D] 2D 복귀(esc)` |
| H-C8 | 판 손잡이를 끄는 중 Esc | Map.BuildingHeight.Handle · Map.BuildingHeight.Height | 끌기만 취소(끌기 전 높이), 판 · 3D 유지 | UIA Map.BuildingHeight.Height 값 = 끌기 전 · 지도 ItemStatus "3D" |
| H-C9 | 판 층수 칸에서 Ctrl+Z · Shift+↑ · Shift+↓ | Map.BuildingHeight.Floors · .Undo · .Height | Ctrl+Z = 되돌리기(서버 요청), Shift+↑ = +0.1 m(C-102) | Map.BuildingHeight.Height 값 · 로그 `[Map3D] 건물 높이 저장 id=…` |
| H-C10 | 층수 칸에 "10" 입력 직후(포커스 그대로) [적용] 클릭 | Map.BuildingHeight.Floors · .Apply · .Summary | (D4, 고치지 않음) [적용] 이 비활성이라 클릭이 LostFocus 를 못 일으키는지 확인 — Enter 로는 반영(C-103) | Map.BuildingHeight.Apply IsEnabled · Summary 글 |
| H-C11 | 판 열린 채 3D 빈 땅 클릭 | Map.BuildingHeight.Panel | 판 유지, 3D 윤곽만 사라짐(C-12) — 어긋남 체감 기록 | 캡처 |
| H-C12 | 판에서 층수 · 높이 · 손잡이를 바꿈 | Map.BuildingHeight.Floors · .Height · .Handle · .Summary | 3D 의 그 건물만 즉시 높이 바뀜(미리보기), [취소] 로 원래대로 | 캡처 전/후 · Summary "적용하면 a m → b m(n층)" |
| H-C13 | 낮은 건물(2 m 미만) −1층 · ↓ | Map.BuildingHeight.MinusFloor · .Height | 높이 그대로(C-59 수정 확인) | Map.BuildingHeight.Height 값 불변 |
| H-C14 | 잘못된 값("abc") 남긴 채 [적용] | Map.BuildingHeight.Height · .Error · .Apply | [적용] 비활성 · 오류 줄 유지(C-105 수정 확인) | Map.BuildingHeight.Apply IsEnabled=false · Error 글 |
| H-C15 | 저장 누르고 곧바로 「나가기」 → 다시 3D → 같은 건물 | GMaps.Map3D.ExitButton · Map.BuildingHeight.Undo | 다시 들어온 판의 [되돌리기] 비활성(C-81 수정 확인) | 로그 `[Map3D] 건물 높이 저장 응답이 3D 를 나간 뒤 도착 — 되돌리기 칸에 쌓지 않음`(응답이 늦을 때만) |

### AutomationId 목록
- 판: `Map.BuildingHeight.Panel` · `.Close` · `.Current` · `.Source` · `.ReadOnly` · `.FloorHeight` · `.MinusFloor` · `.Floors` · `.PlusFloor` · `.Height` · `.Handle` · `.Error` · `.Summary` · `.Apply` · `.Cancel` · `.Reset` · `.Undo` · 도움말 `Help.Map.BuildingHeight.Panel`
- 3D: `GMaps.Map3D.View`(Map3DViewHost.cs:232) · `GMaps.Map3D.OverlayLayer` · `GMaps.Map3D.ExitButton` · `GMaps.Map3D.Exit` · `GMaps.Map3D.Enter` · `GMaps.Map3D.Progress` · `GMaps.Map3D.Chip` · `GMaps.Map3D.AreaList` · 지도 루트 ItemStatus = "3D"/"2D"(Map3DStatusText)
- 카메라 상자: `GMaps.CameraPopup.Video` · `.VideoPlaying` · `.VideoStatus` · `.VideoRetry` · `.HostRestart` · `.PtzMoving` · `.PtzReason`

### 판정 로그 줄(코드의 정확한 문자열, `…` = 값)
- `[Map3D] 건물 고름 id=… 높이=… 출처=… job=…`
- `[Map3D] 건물 고름 — 네이티브 창 3D 라 「건물 높이」 판을 열지 않음 id=… job=…`(새)
- `[Map3D] 건물 높이 저장 id=… → … (…) job=…`
- `[Map3D] 건물 높이 저장 응답이 3D 를 나간 뒤 도착 — 되돌리기 칸에 쌓지 않음 id=… job=…`(새)
- `[Map3D] 건물 높이 저장 — 서버 닿지 않음: …`(Warning) · `[Map3D] 건물 높이 저장 실패: …`(Error)
- `[Map3D] 「건물 높이」 판 닫기 — 3D 로 보내기 실패(격리): …`(Error) · `[Map3D] 건물 고르기 처리 실패(격리): …`(Error)
- `[Map3D] 건물 높이 모두 자동으로 id=… 되돌림=…` · `[Map3D] 건물 높이 모두 자동으로 — 서버 닿지 않음: …` · `[Map3D] 다시 붙이지 못한 직접 입력 id=… n=…`
- `[Map3D] pick click|dblclick|context kind=…|- id=…|- → 마커|빈 곳` · `[Map3D] pick … kind=track id=… → 동작 없음(2D 와 같음)` · `[Map3D] pick … kind=image id=… → 오버레이 이미지|없음` · `[Map3D] pick 처리 실패(격리): …`
- `[Map3D] 3D 위 우클릭 메뉴 열기 — 항목 n개 @(x,y)` · `[Map3D] 3D 위 우클릭 메뉴 닫힘 — 열린 지 … ms`
- `[Map3D] 2D 복귀(camera-popup|camera-stream|component-card|symbol-detail|esc|exit-button) zoom=… bearing=…` · `[Map3D] 3D 표시 job=… host=composition|hwnd|none`
- `[Map3D] 창 자리 처리 실패(격리): …` · `[Map3D] 지켜볼 목록 보내기 실패(격리): …` · `[Map3D] 3D 창 따라가기 시작 실패(격리 — 창만): …`


## D 2D 편집
공통: 편집 모드 켜고 도형 3개 이상. 판정 로그 `[CameraAim] …` 은 `SetAimStatus` 가 남기는 줄(MapViewModel.cs:1133), 앱 로그 UTF-8.

| # | 단계 | AutomationId | 기대 | 판정 |
|---|---|---|---|---|
| H-D1 | 러버밴드 2개 → 손잡이 실제 끌기 시계 ~90° → 놓음 | Map.GroupSelection.RotateHandle → Map.GroupRotation.Panel | 끄는 동안 미리보기 · 놓아도 판 유지 · Angle ≈ 90.0 | UIA Map.GroupRotation.Angle · 로그 "그룹 선택 2개" |
| H-D2 | Shift 누른 채 손잡이 끌기 | Map.GroupSelection.RotateHandle | 15° 단위만 | UIA Angle 15.0/30.0… |
| H-D3 | 중심 십자 끌기 | Map.GroupRotation.CenterHandle · .CenterLat · .CenterLon | 위경도 칸 따라 바뀜 · 새 중심 기준 회전 | UIA 값 |
| H-D4 | 「지도에서 고르기」 → 지도 클릭 / 다시 켜고 Esc | Map.GroupRotation.PickCenter | 십자 커서 → 클릭 자리 중심 / Esc = 고르기만 그만 | 로그 "[CameraAim] 지도를 눌러 회전 중심 고르기 — (ESC = 그만)" |
| H-D5 | 각도 칸 · 막대 포커스 중 ←/→ · Esc | Map.GroupRotation.Angle · .Slider | ←/→ 가 묶음 이동 · 확정으로 안 샘 · Esc 만 취소 | 로그 "[CameraAim] 회전 취소" · 위치 원래대로 |
| H-D6 | ±1 · ±0.1 · 확정 · 취소 · ✕ | Map.GroupRotation.Plus1/.Minus1/.Plus01/.Minus01/.Confirm/.Cancel/.Close | 확인 글(.Summary) · 종류 요약(.Kinds) 갱신 · 확정 = 저장 · 취소/✕ = 원위치 | 로그 "[CameraAim] 2개 회전 +N.N° 저장" / "[CameraAim] 회전 취소" |
| H-D7 | 각도 "abc" | Map.GroupRotation.Angle · .Error | 오류 글 · 지도 그대로 | UIA Error 글 |
| H-D8 | ] [ Shift+] Enter Esc 실제 키 | (지도 포커스) | ±1° · ±0.1° · 확정 · 취소 | 로그 "[CameraAim] 회전 +N.N° · Enter 확정 · Esc 취소" |
| H-D9 | 미리보기 중 묶음 끌기 · 방향키 · 묶음 메뉴 잠금 · 속성 판 값 변경 | — | 각각 먼저 확정 후 그 조작 · Esc 로 회전 안 돌아감(①②) | 로그 "[CameraAim] …회전 … 저장" 이 그 조작 앞 |
| H-D10 | 레이어 패널 → 행 실제 클릭 + Ctrl / Shift / Ctrl+Shift | GMaps.Toolbar.LayerPanelButton · GMaps.LayerPanel.Symbol.{Id} · GMaps.LayerPanel.Row.{Id} | 지도 선택 = 같은 집합(1 = 단일, 2+ = 묶음 · 공통 속성창) | 로그 "그룹 선택 {n}개" · "그룹 공통 속성창 표시 — {n}개, …" · UIA SelectionItem |
| H-D11 | 패널 행 초점 ↑↓ · Space · Ctrl+A · Esc | GMaps.LayerPanel.Symbol.{Id} | 패널 선택만 · 지도 묶음 안 움직임 | 로그에 "격자 이동" 없음 |
| H-D12 | 줌 낮춰 안 보이는 심볼 섞인 범위 Shift(⑤) | GMaps.LayerPanel.Symbol.{Id} | 안 보이는 심볼 빠짐 | 로그 "그룹 선택 {n}개" 의 n |
| H-D13 | 같은 종류 2개 → 그룹 판 칸 실제 입력 · 빈칸 확인 → Ctrl+Z | GMaps.Property.*(예 GMaps.Property.Title · .Altitude · .FenceHeight) | 전원 적용 · Ctrl+Z 한 번 · 판 따라옴(③) | 로그 "속성창 변경에 의한 마커 속성 변경: Width = …" |
| H-D14 | 묶음 회전 확정 후 그룹 판 방향 · 감시 방향 칸 | GMaps.Property.* | 새 값(③) | UIA 값 |
| H-D15 | 다른 마커가 가진 장비를 콤보에서 고름 → 예 / 아니오 · 닫기 | (속성 판 장비 콤보) | 예: 옛 마커(들) 비움 · 연결 / 아니오: 원래 장비 | 로그 "[장비 재할당] '…'(…) 은 '…' 에 연결됨 — '…' 로 옮길지 확인" → "[장비 재할당] '…' : '…' → '…'" |
| H-D16 | Ctrl+↑ · Ctrl+↓ (지도 포커스) | — | 변화 없음(기울이기 제거) · Ctrl+←/→ 는 회전 모드일 때만 회전 | 화면 캡처 |


## E 오버레이 이미지(GIS)
판정은 UIA 값 · 앱 로그 줄 · 캡처 PNG. 안내 문구는 `SetAimStatus` 가 `[CameraAim] <문구>` Info 로그를 남기므로 그 줄로도 판정한다.

| 단계 | AutomationId / 조작 | 기대 | 판정 |
|---|---|---|---|
| H-E1 편집 모드 → 「이미지 불러오기」 | `GMaps.EditStrip.LoadImage` 클릭(또는 Ctrl+I) → 파일 고르기 대화상자에서 PNG 선택 | `Map.ImageCrop.Panel` 이 뜸 · 제목 "이미지 자르기" · `Map.ImageCrop.Skip` 보임 | UIA 존재 · 캡처 |
| H-E2 다각형 실제 그리기 | `Map.ImageCrop.Canvas` 위 클릭 4번 → 첫 점 클릭(닫기) | `Map.ImageCrop.PointCount` = "꼭짓점 4개 · 닫힘" | UIA Name/Text |
| H-E3 꼭짓점 끌기 · 가운데 손잡이 끌기 | 캔버스에서 꼭짓점을 10 DIU 넘게 끌기 / 선분 가운데 네모 끌기 | 점 이동 / 점 하나 늘어남(PointCount +1) | UIA · 캡처 |
| H-E4 휠 · 이동 · 맞추기 | 캔버스 위 휠(확대) · 가운데 단추 끌기 · Space 누른 채 왼쪽 끌기 · `Map.ImageCrop.Fit` | 커서 아래 그림 고정 확대 · 이동 · 처음 맞춤 | 캡처 전/후 |
| H-E5 더블클릭 닫기 | 3점 찍고 캔버스 더블클릭 | "· 닫힘" | UIA |
| H-E6 판 안 Ctrl+Z/Ctrl+Y | 캔버스에 포커스 두고 Ctrl+Z · Ctrl+Y · `Map.ImageCrop.Undo`/`.Redo` | 판 안 다각형만 되돌림 — **지도 Undo 가 돌지 않음**(D-1) | PointCount 변화 · 지도 그림 그대로 · 로그에 Undo 줄 없음 |
| H-E7 판 안 Delete · 방향키 | 지도에서 그림을 선택한 채 우클릭 「다각형으로 자르기…」 → 판에서 꼭짓점 누르고 Delete · ←/→ | 꼭짓점만 지워짐 · 삭제 확인 팝업 없음 · 지도 그림 안 움직임(D-1) | 팝업 UIA 없음 · 캡처 |
| H-E8 Esc 두 번 · 닫기 확인 | Esc(그리던 점 취소) → Esc → `Map.ImageCrop.CloseConfirm` 보임 → `Map.ImageCrop.KeepEditing` / `Map.ImageCrop.ConfirmClose` | 확인 띠 → 계속 / 닫힘 | UIA |
| H-E9 이유 줄 | 2점만 찍고 `Map.ImageCrop.Apply` / 1×1 그림 삼각형 적용 | `Map.ImageCrop.Error` 보임("3개" / "남는 픽셀이 없습니다") | UIA Text |
| H-E10 적용 · 그대로 · 취소 · ✕ | `Map.ImageCrop.Apply` / `.Skip` / `.Cancel` / 머리 띠 `.Close` | 자른 그림 / 원본 / 추가 안 함 | 로그 `[ImageCrop] 처음 불러올 때 자름: … 상자=(…) 꼭짓점=N` · `[ImageCrop] 오버레이 이미지 추가 취소: …` |
| H-E11 머리 띠 끌기 | 판 머리 띠(38 DIU) 끌기 | 판이 움직이고 캔버스는 그대로 | 캡처 |
| H-E12 우클릭 메뉴 | 편집 모드에서 놓인 그림 우클릭 | `GMaps.Symbol.ContextMenu.ImageCrop`, 자른 그림이면 바로 아래 `GMaps.Symbol.ContextMenu.ImageUncrop` · 운영 모드엔 없음 | UIA |
| H-E13 다시 자르기 · 되돌리기 · 지도 Ctrl+Z | 메뉴 → 적용 → 지도 Ctrl+Z → Ctrl+Y → 「자르기 되돌리기」 | 같은 자리 · 회전 유지 | 로그 `[ImageCrop] 다시 자름 id=… 회전=…` · `[ImageCrop] 자르기 되돌림 id=…` · 캡처 |
| H-E14 판 떠 있는데 Ctrl+I | 판 열린 채 Ctrl+I | 파일 창 안 뜸 · "자르기 판을 먼저 닫으세요" | 로그 `[ImageCrop] 자르기 판이 떠 있어 이미지 불러오기 막음` |
| H-E15 판 떠 있는데 편집 모드 끄기 | 편집 토글 끔 | 판 사라짐 · 그림 추가 안 됨 | 로그 `[ImageCrop] 편집 모드가 꺼져 자르기 판 취소` |
| H-E16 운영 모드 Ctrl+I(결정 ②) | 편집 모드 끈 채 Ctrl+I → 판 띄운 채 줌 15+ 3D 영역 | 현재: 판이 뜨고 3D 진입 가능 — 관찰만 | 캡처 |
| H-E17 자른 뒤 3D | 자른 그림이 있는 영역 3D 진입 | 바깥 투명(사진 텍스처가 다각형 모양) | 캡처 · map-3d 쪽은 F |

판정 로그 줄(코드 문자열 그대로):
- `[ImageCrop] 자르기 판이 떠 있어 이미지 불러오기 막음`
- `[ImageCrop] 편집 모드가 꺼져 자르기 판 취소`
- `[ImageCrop] 그림을 읽지 못해 자르기 없이 추가: {ex.Message}` (Warning)
- `[ImageCrop] 오버레이 이미지 추가 취소: {title}`
- `[ImageCrop] 처음 불러올 때 자름: {copied} → {cropped} 상자=({x},{y},{w}x{h}) 꼭짓점={n}`
- `[ImageCrop] 저장 폴더에 쓰지 못해 추가 안 함: {ex.Message}` (Error)
- `[ImageCrop] 그림 읽기 실패 {path}: {ex.Message}` (Warning)
- `[ImageCrop] 판이 떠 있는 동안 그림이 바뀌어 자르기 취소 id={id} {path} → {marker.FilePath}`
- `[ImageCrop] 자른 그림 저장 실패 id={id}: {ex.Message}` (Error)
- `[ImageCrop] 다시 자름 id={id} {path} → {newPath} 상자=({x},{y},{w}x{h}) 회전={bearing:F1}`
- `[ImageCrop] 자르기 되돌림 id={id} {FilePath} ← {record.Source}`
- `[ImageCrop] 저장 실패 id={id}: {ex.Message}` (Error)
- 안내 문구(`[CameraAim] …`): "자르기 판을 먼저 닫으세요" · "그림이 바뀌어 자르기를 취소했습니다" · "저장하지 못해 자르지 않았습니다" · "저장하지 못해 되돌리지 않았습니다" · "그림을 저장하지 못해 추가하지 않았습니다 — 저장 폴더를 확인하세요" · "자른 그림을 저장하지 못했습니다 — 저장 폴더를 확인하세요" · "잘랐습니다 — 되돌리려면 Ctrl+Z 또는 우클릭 「자르기 되돌리기」" · "자르기 전 그림으로 되돌렸습니다" · "자르기 전 그림 파일이 없어 되돌릴 수 없습니다" · "자른 그림이 아니라 되돌릴 것이 없습니다" · "그림 파일을 읽을 수 없어 자를 수 없습니다"


## F map-3d(E · F 의 map-3d 쪽 · B/C 뷰어)
map-3d 장면 폴더(`scene.json` · `log.txt` · `scene.glb`)와 GIS 3D 를 눈으로 본다. 판정 문구 · 키는 코드 그대로.
| 단계 | 기대 | 판정 |
|---|---|---|
| 투명으로 자른 PNG 오버레이(composite) 굽기 | 불투명 모양 안만 사진, 밖은 위성 | scene.json `sources.overlay.items[].region`(투명 사용 · 꼭짓점 수 · 면적 m² · 조각 · 구멍), `removed_osm` · log.txt "오버레이 N장을 지형 텍스처에 합성(composite)" |
| 완전 투명 PNG | 아무것도 안 구움 | log.txt 경고 "불투명 부분이 너무 작음(… m²) — 투명으로 자른 모양을 확인", "… 가 영역 밖이라 굽지 않음" · `overlays_baked` == [] |
| extract 굽기 · 사진 텍스처 | 사진과 맞는 건물 벽 · 지붕에 사진 | log.txt "오버레이 N: 그림 성격 …, 건물 윤곽 …개", "사진 텍스처: 건물 N동" · `sources.overlay.items[].extract.sources`(matched · map_removed · map_kept_blur …) · `buildings[].origin`(map/national/photo) · `height_from` |
| 흐린 자리 바닥 | 흐린 곳 바닥이 사진 땅(건물 지운 것) | log.txt "오버레이 N: 흐린 자리 바닥 = 사진 땅(건물 지움 · …)" · `extract.ground`(pixels · inpaint) |
| 그림자 · 해 | 구운 그림자가 해 반대쪽 · 뷰어 해와 같은 방향 | `sun{source, elevation_deg, azimuth_deg, azimuth_source, shadow_k, baked}` · 고위도(>~75°) 장면은 `elevation_deg` 3 |
| 난간 | 4 m 이상 · 40 m² 이상 평지붕만, 중정은 안팎 | `buildings[].parapet` |
| 공장색 | 탱크 · 굴뚝 근처 낮은 건물 외벽 공업색 · 사진 벽 없는 건물은 사진색 물든 벽 | 눈 확인 |
| 축소 한계 | 영역 전체가 꽉 차는 거리 × 1.3 이상 멀어지지 않음 | 휠 끝까지 · 사방 돌려 확인 |
| 장면 밖 오버레이 · 추적 | 영역 밖 마커 · 선 · 추적 표식이 허공에 안 뜸 · 그 마커의 카메라 상자도 안 따라옴 | GIS 가 받은 `overlay.screen` 항목 `visible:false`(x · y null) |
| 마커 A → 건물 → 마커 A 빠르게 | 더블클릭(카메라 팝업) 아님, 클릭 두 번 | GIS 로그의 `pick` action "click" |


