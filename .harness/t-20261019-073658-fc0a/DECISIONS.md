# DECISIONS

## 1. 수동(단추) 방식에서도 3D 장면을 미리 불러 둘지
- options: 미리 준비(1×1 미리 불러오기)도 끈다 / 그대로 미리 준비한다
- chosen: 그대로 미리 준비한다(미리 준비하는 줌 이상 · 영역 안)
- why: 「3D 로 보기」를 눌렀을 때 바로 뜨게 — 줌 진입만 막으면 명세(“줌으로는 절대 자동 진입하지 않는다”)를 지킨다.
- effect: 수동이어도 줌 15 부터 WebView2 가 장면을 불러 둔다. 끄려면 MapViewModel.EvaluateMap3DGate 의 ShouldPreload 조건에 `Map3DMath.IsZoomEntry(...)` 를 더한다.

## 2. 자동(줌) 방식에서도 「3D 로 보기」 단추를 보일지
- options: 수동일 때만 / 두 방식 모두
- chosen: 두 방식 모두(2D · 화면 가운데가 만들어 둔 3D 영역 안일 때)
- why: 자동에서도 진입 줌 아래에서 바로 들어가거나, ESC 로 나온 뒤 같은 줌에서 다시 들어갈 길이 생긴다(진입 보류 상태).
- effect: Map3DMath.IsEnterButtonVisible 에 들어가는 방식 인자를 더하면 수동 전용으로 바꿀 수 있다.

## 3. 「이 영역 3D 로 보기」를 자동 방식에서도 직접 진입으로 바꿀지
- options: 자동은 예전처럼 줌 17.5 로 옮겨 게이트가 들어가게 / 두 방식 모두 직접 진입
- chosen: 두 방식 모두 직접 진입(영역 가운데 · 줌 17.5 로 옮기는 것은 그대로 두고 곧바로 EnterMap3DDirect)
- why: 경로 하나로 단순하고, 영역 줌을 높인 영역에서도 같은 결과. 장면 목록에 아직 없으면 예전처럼 게이트에 맡긴다.
- effect: MapViewModel.Map3DAreas.cs ViewMap3DArea 의 EnterMap3DDirect 호출을 `!IsZoomEntry` 일 때만으로 좁히면 예전 자동 동작.

## 4. 수동이면 "3D 로 들어가는 줌" 칸을 비활성/숨김으로 할지
- options: 칸 비활성(설정 화면에 새 관용구 추가) / 숨김(새 관용구) / 칸은 그대로 + 저장된 방식에 따른 상태 주석
- chosen: 칸은 그대로 두고 저장된 방식이 수동이면 칸 아래 상태 주석 "수동(단추)이라 지금은 쓰지 않습니다"(기존 NoteSource 관용구)
- why: 설정 창(SettingsFieldDescriptor · SettingsFieldViewModel)에 다른 칸 초안에 따라 칸을 끄거나 숨기는 관용구가 없다 — 새로 만들면 공용 설정 커널(뷰 · 뷰모델) 변경이 커진다. 값은 영역 기본 3D 줌으로도 쓰여 수동에서도 고칠 수 있는 게 맞다.
- effect: 주석은 저장 뒤 갱신(초안 고르기만으로는 바뀌지 않음). 나중에 칸 활성 관용구가 생기면 SettingsCatalog.Map3DSection 의 Map3DEnterZoomKey 칸에 붙인다.

## 5. 화면에서 뺀 "2D 로 돌아가는 줌"(ExitZoom) 값 처리
- options: 모델에서 지운다 / 저장된 값 그대로 둔다 / 진입 줌을 쓸 때 진입 − 0.5 로 함께 맞춘다
- chosen: 모델 · 내부 판정(준비 중 취소 · 진입 보류 해제 · 영역 게이트)은 그대로 두고, 메인 앱 창구가 EnterZoom 을 쓸 때 ExitZoom = EnterZoom − 0.5 로 함께 쓴다. 줌 순서 규칙은 "미리 준비하는 줌 ≤ 3D 로 들어가는 줌 − 0.5"(SettingsValidation.Map3DPreloadOrder) 하나로.
- why: 라이브러리 Normalize(미리 준비 ≤ 이탈)와 어긋나지 않고, 화면에 없는 값 때문에 저장이 막히는 일이 없다. 옛 설정 파일 호환.
- effect: 차이 0.5 는 SettingsValidation.Map3DExitGap 에서 바꾼다.

## 6. 새 단추 AutomationId
- options: 명세의 `Map.Map3D.EnterButton` / 기존 「나가기」 규칙의 `GMaps.Map3D.EnterButton`
- chosen: 단추 = `Map.Map3D.EnterButton`(명세 그대로), 감싼 테두리 = `GMaps.Map3D.Enter`(「나가기」의 `GMaps.Map3D.Exit` 과 짝)
- why: 명세가 이름을 못박았다(자동화 시험이 그 이름을 찾는다).
- effect: 바꾸려면 MapView.xaml 한 줄.

## 7. 메인 앱 저장소 직접 수정이 막힘
- options: BLOCKED / 워크트리 안 복사본에서 고치고 시험한 뒤 git diff 로 host.patch
- chosen: 복사본(.harness/<id>/hostcopy — 라이브러리 참조를 이 워크트리로 돌림)에서 고치고 Map3DSettingsSectionTests 등 시험 → `git diff --no-index` 로 host.patch, 복사본은 지움
- why: 명세의 결과물이 host.patch 이고, 그 저장소 직접 커밋은 원래 금지다. 이 세션은 그 경로에 쓰기 권한이 없었다.
- effect: 메인 앱에서 `git apply .harness/t-20261019-073658-fc0a/host.patch`(검사 `git apply --check` 통과).
