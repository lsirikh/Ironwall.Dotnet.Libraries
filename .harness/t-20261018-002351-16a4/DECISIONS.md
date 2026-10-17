# DECISIONS

## 1. 「3D 줌 바꾸기…」에서 줌을 어떤 화면으로 고를까
- options: (a) 배지 우클릭 메뉴의 하위 메뉴(15 · 15.5 · … · 19) (b) 3D 만들기 패널(GMaps.Map3D.BakeOptions)을 "줌만" 모드로 다시 쓰기 (c) 새 대화 창
- chosen: (b) 같은 떠 있는 패널 — 제목 「3D 줌 바꾸기」, 요약 "영역: 이름", 방식 라디오 숨김, 줌 콤보, [바꾸기] [취소].
- why: 메뉴 글의 "…"(무엇을 더 고른다)와 맞고, 만들 때 줌을 고르는 화면과 같은 칸 · 같은 "?" 를 써서 사용자가 한 가지 모양만 익히면 된다. 새 창 · 새 스타일이 필요 없다.
- effect: MapViewModel.Map3DBakeOptions 의 `_map3DZoomEditArea` 가 모드를 가른다. 하위 메뉴로 바꾸려면 Map3DAreaItem.Actions 에 자식 목록을 더하고 OpenMap3DAreaZoomEditor 대신 ChangeMap3DAreaZoomAsync 를 바로 부르면 된다.

## 2. 오버레이 이미지가 없을 때 패널 모양
- options: 방식 라디오 셋을 그대로 보이기 / 라디오를 숨기고 줌만
- chosen: 라디오를 숨긴다(IsMap3DBakeModeVisible) — 요약 줄 "영역 안 오버레이 이미지 0개" 가 이유를 말하고, 방식은 예전처럼 tiles.
- why: 이미지가 없으면 합성 · 추출은 고를 의미가 없고, 예전에도 묻지 않았다.
- effect: XAML 의 GMaps.Map3D.BakeOptions.Modes 묶음 Visibility 하나만 바꾸면 늘 보이게 할 수 있다.

## 3. 「다시 만들기」 패널의 처음 줌
- options: 세팅의 3D 최소 줌 / 그 영역의 지금 줌
- chosen: 그 영역의 지금 줌(로컬 > 서버 > 세팅). 새 영역(그리기 · Enter)은 목표대로 세팅값.
- why: 다시 만들 때 줌이 조용히 세팅값으로 되돌아가면 사용자가 바꿔 둔 값을 잃는다.
- effect: RebakeMap3DArea 의 `RequestMap3DBake(item.Bbox, item.EnterZoom)` 인자를 빼면 세팅값이 된다.

## 4. 고를 수 있는 줌 밖의 세팅값(예: 3D 최소 줌 17.25 · 20)을 패널에서 어떻게 보일까
- options: 선택지에 그 값을 끼워 넣기 / 가장 가까운 선택지(0.5 단위, 15~19)로 맞추기
- chosen: 가장 가까운 선택지(Map3DGuide.EnterZoomChoiceFor) — 목표의 선택지 목록(15 · 15.5 · … · 19)을 그대로 지킨다.
- why: 지도 디지털 줌이 0.5 단위라 그 밖의 값은 어차피 그 단위로 판정되고, 목록이 영역마다 달라지지 않는다.
- effect: 세팅 3D 최소 줌은 10~20.5 그대로 둘 수 있다(이미 만든 영역 · 서버 값 없는 영역의 판정은 세팅값 그대로). 목록을 넓히려면 EnterZoomChoiceMin/Max 상수만 바꾸면 된다.

## 5. 서버 영역 목록에 없는 장면을 묻기 시작하는 줌(/api/scenes/at)
- options: 세팅 이탈 줌 그대로 / 가장 낮게 고를 수 있는 영역 줌(15)의 이탈 줌
- chosen: min(15, 세팅 3D 최소 줌) 을 진입 줌으로 본 이탈 줌(기본 14.5)부터 묻는다.
- why: 영역 줌을 15 로 둔 장면이 목록에 아직 없을 때도 그 줌에서 3D 로 들어가야 한다. 같은 중심은 다시 묻지 않아(400 ms 디바운스 · 같은 중심 건너뜀) 요청이 늘지 않는다.
- effect: EvaluateMap3DGate 의 한 줄. 줄이려면 세팅 이탈 줌으로 되돌리면 된다.

## 6. 세팅에서 줌 순서 검증 — 두 절에 나뉜 칸을 어떻게 견줄까
- options: (a) 절마다 자기 초안 + 다른 절은 저장된 값으로 견주기 (b) 설정 창 프레임워크가 절을 넘어 초안을 읽게 고치기
- chosen: (a) 「지도」 절: 초안 3D 최소 줌 vs 저장된 2D 로 돌아가는 줌 · 미리 준비하는 줌. 「3D 지도」 절: 저장된 3D 최소 줌 vs 초안 이탈 · 미리 준비.
- why: 프레임워크(SettingsSectionViewModel · SettingsFormController)를 건드리지 않고 목표의 검증(이탈 &lt; 최소, 미리 준비 ≤ 이탈)을 그대로 지킨다. 안전한 쪽(저장된 값 기준)으로만 틀린다.
- effect: 두 절을 함께 크게 옮길 때(예: 17→19 와 16.5→18.5)는 순서대로 두 번 저장해야 한다(올릴 땐 지도 절 먼저, 내릴 땐 3D 지도 절 먼저). 한 번에 하려면 (b) — SettingsSectionViewModel.CheckError 의 초안 조회를 폼 전체로 넓히면 된다.

## 7. 지도 절 "3D 최소 줌" 칸을 넣는 방식
- options: IMapSettingsPort 에 속성을 더하기 / MapSection 에 3D 창구(IMap3DSettingsPort)를 선택 인자로 받기
- chosen: `MapSection(..., Func<IMap3DSettingsPort>? map3d = null)` — 주면 맨 밑에 칸(키 map.Map3DEnterZoom, 실제 키 Map3D.EnterZoom)을 달고, 절에 들어갈 때 3D 창구를 다시 읽는다.
- why: Map3D.EnterZoom 의 주인은 이미 3D 창구(Map3DSettingsViewModel — 모델 한 벌로 절째 저장)라 두 절이 같은 모델을 쓴다. 지도 창구에 3D 를 섞지 않고, 기존 지도 절 시험(가짜 지도 창구)도 그대로 돈다.
- effect: 칸 위치는 지도 절 맨 밑(홈 좌표 뒤). 옮기려면 SettingsCatalog.MapSection 의 Concat 자리만 바꾸면 된다.

## 8. 메인 앱 시험 기대값 · 문구 변경(의도된 변경)
- options: 옛 문구("3D 로 바뀌는 줌") 유지 / 새 칸 이름("3D 최소 줌")으로 통일
- chosen: 통일 — SettingsValidation.Map3DExitOrderMessage 를 "2D 로 돌아가는 줌은 3D 최소 줌보다 작아야 합니다" 로, 「3D 지도」 "?" 문구도 3D 최소 줌으로.
  그래서 Map3DSettingsSectionTests 의 기대값을 바꿨다: 칸 10개 → 9개(3D 로 바뀌는 줌 빠짐), map3d.EnterZoom 칸을 쓰던 세 시험은 ExitZoom · PreloadZoom 으로, "?" 문구 한 줄("3D 로 바뀌는 줌보다 작아야" → "3D 최소 줌보다 작아야").
- why: 「3D 지도」 절에서 그 칸이 빠졌으므로 막대 · "?" 가 없는 칸 이름을 말하면 사용자가 찾지 못한다.
- effect: host.patch 안. 지도 절 새 칸 시험 8개를 같은 파일에 더했다.
