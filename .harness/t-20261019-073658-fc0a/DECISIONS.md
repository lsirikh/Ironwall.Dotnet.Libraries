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
- effect: 추가 지시(시험 4 "수동이면 진입 줌 칸 비활성")도 이 결정대로 "수동 저장 뒤 주석이 뜨고 · 자동 저장 뒤 사라지며 · 수동이어도 값은 고칠 수 있다"로 시험한다. 주석은 저장 뒤 갱신(초안 고르기만으로는 바뀌지 않음). 나중에 칸 활성 관용구가 생기면 SettingsCatalog.Map3DSection 의 Map3DEnterZoomKey 칸에 붙인다.

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

## 8. 만드는 중 · 이벤트 지도 이동(flyTo) 중에 「3D 로 보기」를 누르면
- options: 준비를 시작했다가 다음 게이트가 취소(이전 동작 — 안내가 깜빡임) / 처음부터 들어가지 않음
- chosen: 처음부터 들어가지 않는다 — 만드는 중이면 "3D 를 만드는 중입니다 — 다 만든 뒤 다시 누르세요" 알림, 이동 중이면 조용히 무시(끝난 자리에서 다시 누름). 단추는 그대로 보인다.
- why: 시나리오 시험(should_block_entry_but_keep_button_while_area_is_being_rebaked · should_hold_entry_until_flight_ends…)에서 시작→즉시 취소로 안내가 깜빡이는 것을 찾았다.
- effect: MapViewModel.EnterMap3DDirect 앞 두 줄. 단추를 아예 숨기려면 IsMap3DEnterButtonVisible 에 `!_isMap3DBaking` 를 더하고 굽기 시작 · 끝에 알림을 더한다.

## 9. 시나리오 시험에서 WebView2 · 설정 파일을 어떻게 피할지
- options: LoadMap3DFromSettings(실제 설정 읽기 · 구독 · WebView2 미리 데우기)까지 부른다 / 설정 모델만 바꿔 끼우고 게이트 · 표시 메서드를 직접 부른다
- chosen: 후자 — 3D 호스트에는 "그 장면 ready · rendered"(미리 불러 둔 장면) 상태만 꽂아 재사용 경로로 펼치고, 매 시나리오 끝에 WebView2 를 만들지 않았는지(HostMode "none") 확인. 설정 저장은 `_map3DSettings` 교체로 대신(저장 · 다시 읽기 왕복은 MapSettingsHelper.ReadMap3D 시험이 따로 본다).
- why: 창 · WebView2 시험 금지, 설정 파일은 실행 폴더 appsettings 라 다른 시험과 겹친다.
- effect: "장면 실패" 칸은 "만들기 실패 = 서버 목록에 장면이 올라오지 않음"으로 잰다(WebView2 탐색 실패는 헤드리스로 만들 수 없음). 측정 · 선 그리기 막힘은 컨트롤러가 필요해 앵커 그리기 · 심볼 배치 · 편집 모드로 잰다(같은 IsMap3DBlockedByInputMode 판정).

## 10. 설정 파일 읽기를 시험하려고 MapSettingsHelper 를 나눌지
- options: LoadMap3D 그대로(실행 폴더 appsettings 만 읽음) / 절(JSON) → 모델 부분을 `public static ReadMap3D(JToken?)` 로 떼기
- chosen: 떼기(공개 멤버 더하기만, LoadMap3D 동작 같음)
- why: 옛 파일(EntryMode 없음 · ExitZoom 만) · 이상한 값 · 왕복을 실제 읽기 코드로 재려고.
- effect: 없음(LoadMap3D 가 ReadMap3D + Normalize 를 부른다).