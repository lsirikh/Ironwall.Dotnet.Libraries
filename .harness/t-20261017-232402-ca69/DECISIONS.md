# DECISIONS

## 1. 설정을 바꾸면 지도에 어떻게 "즉시 반영"할까
- options: (a) MapSettingsHelper 에 저장 알림 이벤트(Map3DSaved)를 두고 MapViewModel 이 받아 다시 불러오기 (b) 메인 앱이 EventAggregator 메시지로 지도에 알리기 (c) 파일 감시(FileSystemWatcher)
- chosen: (a) `MapSettingsHelper.Map3DSaved`(정적 이벤트, 저장 끝난 뒤) → `MapViewModel.OnMap3DSettingsSaved` → `LoadMap3DFromSettings()` 다시 실행. 3D 를 끄고 저장했으면 보던 3D 를 접는다.
- why: 목표 문구("MapSettingsHelper 저장 → 다시 불러오기")와 같고, 메인 앱 · 라이브러리 어느 쪽에서 저장해도 같은 길로 반영된다. 새 메시지 형식이 필요 없다.
- effect: 칸마다 setter 가 저장하므로 [저장] 한 번에 바꾼 칸 수만큼 다시 불러온다(가볍다). 줄이려면 OnMap3DSettingsSaved 에 디바운스를 넣으면 된다. 「WPF 안에서 그리기」는 3D 화면을 처음 만들 때만 보므로 다음 실행부터 반영된다("?" 에 적음).

## 2. 줌 순서 위반을 어디서 막을까
- options: (a) 설정 창 프레임워크에 절 단위 "칸 사이 규칙"(SettingsSectionDescriptor.Check)을 더하기 (b) 칸 규칙(Rule)이 다른 칸 값을 공유 객체로 읽기 (c) 저장할 때 조용히 Normalize 로 고치기
- chosen: (a) `Check` 가 초안 값으로 `SettingsValidation.Map3DZoomOrder` 를 보고, 어기면 절 HasError → [저장] 꺼짐 · 막대 문구 "입력을 고쳐야 저장할 수 있습니다 — {이유}".
- why: 칸 규칙은 자기 칸 값만 받아서 다른 칸을 고친 뒤 오류가 남거나 사라지지 않는 문제가 있고, (c) 는 "저장 막고 이유 표시" 요구와 다르다.
- effect: 메인 앱 SettingsSectionDescriptor · SettingsSectionViewModel · SettingsFormController 에 몇 줄 추가(다른 절은 Check 가 없어 동작 그대로). 라이브러리 LoadMap3D 의 Normalize 는 손으로 고친 파일을 위한 마지막 안전망으로 남는다.

## 3. 「안내 표시 시간」 칸의 위치
- options: 목표의 나열 순서(만들기 방식 뒤) / 소제목 "전환" 안(미리 준비하는 줌 뒤)
- chosen: "전환" 묶음 안.
- why: 소제목은 이어진 칸끼리만 묶이고, 안내 시간은 2D↔3D 전환 때 보이는 값이라 "전환" 이 맞다.
- effect: SettingsCatalog.Map3DSection 의 Fields 순서만 바꾸면 옮길 수 있다.

## 4. 레일 위치 · 기존 시험 기대값
- options: 지도 절 바로 뒤 / 센서 모델 뒤 / 정보 절 앞
- chosen: 센서 모델 뒤(map → sensorModels → map3d → gateway). 그래서 메인 앱 `SettingsConsoleScenarioTests.BuiltRailKeys`(Build() 레일 목록을 그대로 비교하는 시험)에 "map3d" 를 넣었다.
- why: 지도 묶음 옆이 찾기 쉽고, 기존 시험(SensorModelsRailScenarioTests)이 "센서 모델은 지도 절 바로 뒤" 를 계약으로 걸고 있어 그 자리는 비켰다. 새 절을 더하면 레일 목록 비교는 반드시 바뀐다(시험 약화 아님 — 기대 레일 목록 갱신).
- effect: SettingsCatalog.Build 의 목록 순서와 그 배열을 함께 옮기면 된다.

## 5. 기존 고정 상수 · 공개 API
- options: 상수를 지우고 설정만 쓰기 / 상수는 기본값으로 남기고 인자 받는 오버로드 더하기
- chosen: 오버로드 더하기(Map3DMath.Decide · ShouldLeave3D · ShouldStay3DAfterMove · ShouldPreload, Map3DGuide.IsAreaTooLarge · AreaDrawLabel · AreaTooLargeText · ShouldShowAreaOutlines · ShouldShowChip · StageText, 새 ChipTextFor · LimitText · AreaViewZoomFor · Zoom).
- why: "공개 형식 더하기만" · 기존 시험 유지.
- effect: 「이 영역 3D 로 보기」 줌은 max(17.5, 진입 줌) — 진입 줌을 높여도 그 영역이 3D 로 들어간다. 만들어 둔 화면 펼침 안내(0.4 s)는 설정한 안내 시간이 더 짧으면 그 시간.

## 6. 메인 앱 작업 장소
- options: 메인 앱 저장소를 직접 고치기 / worktree 안의 무시되는 폴더(obj/h3d)에 복사본을 만들어 고치고 patch 만 남기기
- chosen: 복사본(git 이 무시하는 `claude-skill-harness-h3d/Dotnet.Monitoring.Solution`, 라이브러리는 정션 `claude-skill-harness-h3d/Ironwall.Dotnet.Libraries` → 이 worktree)에서 빌드 · 시험하고 `host.patch`(git 정규형 LF — `git apply` 가 CRLF 작업 파일에 맞춰 적용, `--check` 확인)를 만들었다. 처음엔 `obj/` 아래에 뒀으나 메인 앱 시험이 `\obj\` 경로의 XAML 을 걸러 내 옮겼다.
- why: 메인 앱 저장소는 다른 작업이 쓰는 중이고 이 작업은 그 커밋을 할 수 없다.
- effect: 코디네이터가 메인 앱 저장소 루트에서 `git apply .../host.patch` 하면 된다. 복사본 · 정션은 끝에 지웠다.
