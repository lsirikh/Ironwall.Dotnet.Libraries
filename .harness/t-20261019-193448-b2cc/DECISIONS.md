# DECISIONS

## 1. 메인 앱 시험을 어디서 돌릴지
- options: (a) 메인 체크아웃 c:\workspace_app\Dotnet.Monitoring.Solution 에서 그대로(그 앱이 참조하는 라이브러리는 메인 라이브러리 체크아웃 — 이 worktree 변경이 안 들어가고, 쓰는 중인 앱 bin 을 건드림) (b) worktree 안 `artifacts/host` 에 clone 하고 라이브러리 참조 경로를 이 worktree 로 바꿈
- chosen: (b) — 복제본은 `.harness-worktrees/host/`(git 제외 · 라이브러리 소스 스캔 시험이 건너뛰는 폴더). 복제본 csproj 의 `..\..\Ironwall.Dotnet.Libraries\` 를 `..\..\..\..\` 로 바꿈(복제본에서만, host.patch 에는 넣지 않음). 원본 소스를 읽는 대조 시험 14건 때문에 시험 실행 동안만 `.harness-worktrees/host/Ironwall.Dotnet.Libraries` junction 을 만들고 끝나면 링크만 지움(`run-host-tests.ps1`)
- why: 메인 체크아웃 · 실행 중 앱을 건드리지 않고, 이 worktree 의 라이브러리 변경을 메인 앱 시험에 실제로 태운다. 처음 둔 `artifacts/host` 는 Utils.Tests 의 소스 스캔(HelpSourceScan)에 잡혀 4건이 실패했고, junction 을 상주시키면 MapsHelpTests 가 무한 경로를 돌았다(둘 다 실측 · 우리 환경 문제)
- effect: 메인 앱 시험 수는 복제본 기준. 다른 방식이 필요하면 run-host-tests.ps1 만 바꾸면 된다.

## 2. 영역별 작업 나누기
- options: (a) 혼자 순서대로 (b) 영역 A~E 를 따로 받은 git worktree 의 작업자에게, F(map-3d)는 복사본에서 — 결과는 패치로 받아 이 worktree 에 합침
- chosen: (b)
- why: 시나리오 수백 개 · 결함 의심 약 30건. 같은 폴더에서 동시에 빌드하면 obj 충돌이 난다.
- effect: 패치를 합칠 때 겹치는 파일(MapViewModel.cs · MapViewModel.Map3D*.cs)은 손으로 맞춘다.

## 3. 다시 만드는 중인 영역을 지우면 새 장면으로 되살아나는 결함(B-50)의 고치는 방식
- options: (a) 지우기 = 그 굽기도 취소 (b) 다시 만드는 중에는 그 영역 「지우기…」를 메뉴에서 빼고(「다시 만들기」와 같게) 그래도 오면 안내하고 지우지 않음
- chosen: (b)
- why: (a)는 취소가 too_late 면 결국 새 장면이 들어와 같은 결함이 남는다. (b)는 「만들기 취소」가 이미 맨 위에 있어 한 번 더 누르면 된다.
- effect: 다시 만드는 동안 그 영역 지우기 불가. 바꾸려면 Map3DAreaItem 메뉴 조건과 지우기 처리 첫 줄.

## 4. 네이티브 창(hwnd) 3D 에서 건물을 고를 때(C-101)
- options: (a) 판을 열고 Esc 순서만 바꿈 (b) hwnd 에서는 판을 열지 않고 안내
- chosen: (b)
- why: hwnd 3D 는 WPF 판을 덮어 판이 보이지 않는다 — 열어 봐야 쓸 수 없고 Esc 만 가로챈다.
- effect: 원격 세션(hwnd)에서는 건물 높이 편집 불가 안내. composition(기본)은 그대로.

## 5. 회전 미리보기 중 잠금 · 속성 변경(D ① ②)
- options: (a) 그 조작을 막음 (b) 먼저 열린 회전을 확정하고 그 조작을 함
- chosen: (b)
- why: 이미 있는 규칙("다른 키 · 선택 바뀜 = 확정")과 같다.
- effect: 잠그거나 속성을 바꾸면 회전이 저장된다(Undo 로 되돌릴 수 있음).

## 6. 레이어 패널 → 지도 선택에서 줌 때문에 안 보이는 심볼(D ⑤)
- options: (a) 그대로 포함 (b) 지도 Ctrl+A 처럼 뺌(이미지 행은 포함 유지)
- chosen: (b)
- why: 코드 주석이 "Ctrl+A 와 같은 규칙"이고 과제 문구가 "잠금/숨김 제외". 안 보이는 것을 돌리거나 지우면 사용자가 모른다.
- effect: 줌을 키워야 묶음에 들어간다.

## 7. 이미지 자르기 판이 떠 있는데 편집 모드를 끔(E-17)
- options: (a) 판 유지 (b) 판 취소(처음 불러오기면 추가 안 함)
- chosen: (b)
- why: 편집 모드가 꺼진 지도에서 DB 를 고치는 경로를 없앤다. 다른 편집 상태(그리던 선 · 배치)도 편집 모드를 끄면 취소된다.
- effect: 자르다가 편집 모드를 끄면 자르기 내용은 버려진다.

## 8. 3D 를 나갈 때 남은 벤더 지도 끌기(A D-3)를 어떻게 지울지
- options: (a) 제품 코드에서 리플렉션으로 `_core` 를 만짐 (b) 벤더 OnMouseUp 을 가짜 이벤트로 부름 (c) 벤더 GMapControl 에 `IsCoreDragging` · `EndCoreDrag()` 를 더함
- chosen: (c)
- why: 벤더에 같은 목적의 통로(`IsCoreStarted`)가 이미 있고, (b)는 끌기가 아닐 때 선택 영역 통지 같은 부수 동작이 있다. 공개 형식은 더하기만.
- effect: GMap.NET/…/GMapControl.cs 에 public 멤버 2개 추가. 벤더 갱신 때 함께 옮겨야 한다.

## 9. −1층 결함(C-59)을 고치며 반대쪽(+1층이 높이를 낮춤, C-60)도 고칠지
- options: (a) 지시받은 내리기만 (b) 같은 원인인 올리기 쪽도
- chosen: (b)
- why: 같은 함수 · 같은 원인(층수 상한 200 에 걸린 450 m 넘는 건물 +1층 → 400 m). 조건 한 줄.
- effect: 되돌리려면 Map3DBuilding.StepFloors 의 `delta > 0 && next < height` 조건만 뺀다.

## 10. "저장 중 3D 나가기" 뒤 늦은 응답(C-81)의 경계
- options: (a) 3D 를 나갈 때(되돌리기 칸을 비우는 때)만 (b) 판을 닫거나 다른 건물을 고를 때도
- chosen: (a)
- why: 머리말 규칙이 "3D 를 나가면 비움". 같은 3D 안에서는 그 저장을 [되돌리기] 할 수 있는 것이 지금 설계.
- effect: 넓히려면 `_bhSession++` 를 CloseBuildingHeightPanel 의 clearUndo 조건 밖으로.

## 11. 준비 중 다른 영역 「이 영역 3D 로 보기」가 처음 자리를 잃는 결함(A D-6, 작업 중 새로 찾음)
- options: (a) 보고만 (b) 고침
- chosen: (b)
- why: A-30 기대("돌아갈 자리는 처음 진입 전")와 어긋나는 실제 결함이고 수정이 한 줄(Shown 일 때만 → Off 가 아닐 때).
- effect: MapViewModel.Map3DAreas.cs ViewMap3DArea 첫 줄.

## 12. 회귀 판정용 "결정 필요" 항목은 고치지 않고 지금 동작만 기록
- options: (a) 작업자가 정해서 고침 (b) 지금 동작을 시험 또는 보고서에 기록하고 REPORT.md "결정 필요"로
- chosen: (b)
- why: 기존 시험이 고정한 동작(ESC 문구 · 2000 m→600 m 자름)이거나 제품 정책(수동 모드 미리 불러오기 · 입력 모드 뒤 자동 재진입 · 지우기 전 2D 로 나감 · Ctrl+I 운영 모드 · 회전 저장 실패 처리 · 목이 좁은 건물 난간 · MBTiles 없을 때 줌 16)이라 사용자가 정할 일.
- effect: REPORT.md "결정 필요" 목록.

## 13. 기존 시험이 고정한 틀린 안내 문구("나가기 · ESC = 2D", A D-2)
- options: (a) 문구와 기존 시험을 함께 고침 (b) 고치지 않고 보고
- chosen: (b)
- why: 기존 시험을 고쳐 쓰지 않는다는 규칙. 문구는 로그인 직후 안내에서만 보인다.
- effect: 보고서 "결정 필요"에 남김.
