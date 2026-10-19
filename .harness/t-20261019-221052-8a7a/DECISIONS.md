# DECISIONS — t-20261019-221052-8a7a

## 1. 로그인 직후 안내에서 Esc 를 어떻게 다룰지
- options: (a) 문구에서 Esc 를 아예 뺌 (b) WPF 안 3D 는 「나가기」만, 네이티브 창 3D(hwnd)만 Esc 를 함께 말함
- chosen: (b) — `StageText("shown")` = "지금 3D 지도를 보고 있습니다 (「나가기」 = 2D)", 새 `Map3DGuide.ShownText(hostMode)` 가 hwnd 면 "(나가기 · ESC = 2D)"
- why: Goal 1 이 "네이티브 창 3D 일 때만 Esc 언급" 을 명시. 기존 `EscExitsMap3D` 와 같은 판정을 써서 실제 동작과 늘 같다.
- effect: 로그인 직후 안내 1곳(MapViewModel.Map3D OnMap3DLoginSucceeded). 쓰이지 않는 "login" 단계 글(줌 16.5 밑 · ESC)은 범위 밖이라 그대로 둠.

## 2. 묶음 회전 Undo 를 저장 전에 기록할지, 뒤에 기록할지
- options: (a) 지금처럼 저장 전 전원 기록 후 실패분을 빼기(기록기에 빼기 API 없음) (b) 저장을 끝낸 뒤 성공한 멤버만 1단위로 기록
- chosen: (b)
- why: 실패 멤버를 Undo 에서 빼려면 결과를 알아야 한다. 기록기에 일부 삭제 API 를 새로 만들지 않는 최소 변경.
- effect: Undo 가 저장 응답 뒤에 생긴다(시험 DB 는 즉시 응답이라 체감 차이 없음). 저장 중 같은 멤버를 또 움직이면 "뒤" 값이 그 시점 값이 될 수 있다. 되돌리려면 OnGroupRotateCompleted 에서 기록 블록 위치만 옮기면 된다.

## 3. Ctrl+I 를 편집 모드에서만 — CanExecute 로 끌지, 실행 첫머리에서 막고 알릴지
- options: (a) CanExecute = 지도 있음 ∧ 편집 모드(단축키 무반응) (b) CanExecute 그대로, 실행 첫머리(권한 확인 뒤)에서 "편집 모드에서 쓸 수 있습니다" 안내 후 끝
- chosen: (b)
- why: 기존 시험 3곳(MapRegistrationCommandScenarioTests · ImageCropEdgeScenarioTests · ImageCropMenuScenarioTests 소스 고정)이 "CanExecute 는 그대로(단축키 무반응 대신 안내)" 를 고정하고 있고, 시험을 약하게 바꾸지 말라는 규칙. 단추와 Ctrl+I 는 같은 명령이라 조건이 저절로 같다(단추는 편집 줄에 있음).
- effect: 운영 모드 Ctrl+I = 짧은 안내만, 파일창 · 자르기 판 안 뜸. 권한 없음 안내가 편집 모드 안내보다 먼저.

## 4. 3D 빈 땅 클릭을 GIS 가 어떻게 알지
- options: (a) GIS 가 기존 빈 pick(kind null)으로 판을 닫음 (b) map-3d 가 건물 아닌 땅이면 새 메시지 {type:"building.none"} 를 보내고 GIS 가 그것으로 닫음
- chosen: (b)
- why: map-3d 는 건물 클릭에도 빈 pick 을 먼저 보낸다(view.js pointerup) — (a) 면 같은 건물을 다시 눌러도 판이 닫혀 미리보기가 취소된다.
- effect: map3d.patch(view.js 1줄 · buildingedit.js 주석 · building_check.mjs 시험). 패치를 적용하지 않은 옛 map-3d 에서는 지금처럼 판이 열린 채(오류 없음). 하늘(아무것도 안 맞음) 클릭은 알리지 않는다.
