# DECISIONS

## 1. 3D 설정을 어디에 두고 어떻게 읽을까
- options: (a) IGMapSetupModel/GMapSetupModel 에 Map3D 속성 추가 → 앱이 바인딩 · 주입 / (b) Map3DModel 만 만들고 MapSettingsHelper.LoadMap3D 가 AppSettings.Map3D 를 직접 읽음(LoadMapEventFlyTo 와 같은 방식)
- chosen: (b)
- why: 공개 인터페이스(IGMapSetupModel)에 멤버를 더하면 메인 솔루션 쪽 구현 · 복사 경로에 영향이 갈 수 있고, 메인 앱 연결 없이 라이브러리만으로 동작해야 했다.
- effect: appsettings 의 `AppSettings.Map3D { "IsEnabled": true, "ServerUrl": "http://127.0.0.1:8765" }` 로 바꿀 수 있다(키 없으면 기본값). 지도 다시 불러올 때(ConfigureCommonMapSettings) 다시 읽는다. 저장 · 설정 화면은 없음 — 필요하면 SaveSettingAsync("Map3D", …) 한 줄로 더할 수 있다.

## 2. ready 전 WebView2 를 어떻게 "숨길까"
- options: (a) Visibility=Hidden/Collapsed / (b) 왼쪽 위 1×1 크기로 보이게 둔 채 대기(Park), ready 뒤 펼침
- chosen: (b)
- why: WebView2 를 숨기면 페이지가 "안 보임" 상태가 되어 그리기(requestAnimationFrame)가 멈추고, 지도 엔진의 load/ready 가 늦거나 오지 않을 수 있다. 1×1 은 화면에 사실상 안 보이면서 페이지는 살아 있다.
- effect: 2D 로 돌아올 때도 1×1 로 접어 재사용한다(다음 진입이 빠름). 바꾸려면 Map3DViewHost.Park/Reveal 만 고치면 된다.

## 3. 3D 를 처음 열 때 기울기 · 애니메이션
- options: (a) 주소에 pitch=60&animate=1 로 바로 열기 / (b) 주소는 pitch=0&animate=0(위에서 내려다본 같은 축척)으로 열고, ready 뒤 펼치는 순간 camera(pitch 60, animate true)를 보내 눕히기
- chosen: (b), 기울기 60°(Map3DMath.DefaultPitchDeg)
- why: 로딩은 1×1 대기 중에 일어나므로 (a) 는 애니메이션이 사용자에게 안 보인다. (b) 는 펼친 순간 "2D 와 같은 화면 → 눕기" 가 보인다. 60° 는 스펙에 값이 없어 건물이 잘 보이는 일반값으로 골랐다.
- effect: 상수 하나(Map3DMath.DefaultPitchDeg)로 바꿀 수 있다.

## 4. 2D → 3D 로 보내는 줌 값
- options: (a) 실효줌(타일 + 0.5·디지털단계) 그대로 / (b) 실제 배율 기준 메르카토르 줌(타일 + log2(배율표))
- chosen: (b) — 게이트 판정(17.0 진입)은 사용자에게 보이는 실효줌으로 한다.
- why: 디지털 줌 배율표가 {1, 1.25, 1.5, 2} 라 실효줌 17.5 는 실제로 17.32 배율이다. "같은 축척" 이 되려면 (b) 여야 한다. 돌아올 때는 2D 가 낼 수 있는 실효줌 중 메르카토르 줌이 가장 가까운 값을 고른다.
- effect: Map3DMath.ToMercatorZoom/ToEffectiveZoom.

## 5. ESC · 로딩 실패 뒤 곧바로 다시 3D 로 들어가는 문제
- options: (a) 다시 들어가게 둠 / (b) 게이트가 한 번 꺼질 때(실효줌 < 17 또는 장면 밖)까지 진입 보류
- chosen: (b)
- why: ESC 로 2D 에 돌아오면 줌이 17 이상 그대로라 다음 판정에서 바로 3D 로 되돌아간다. 실패 시에도 같은 줌에서 무한 재시도가 된다.
- effect: MapViewModel.Map3D 의 _map3DEntrySuppressed.

## 6. 장면 id 와 3D 화면 주소의 job 값
- options: (a) scenes/at 의 view_url 을 그대로 씀 / (b) 장면 id 를 job 값으로 써서 주소를 GIS 가 만듦(embed · 카메라 쿼리 포함)
- chosen: (b)
- why: 굽기 결과(작업 id)와 scenes/at 결과를 한 목록으로 다루고, 주소 모양은 스펙의 `/view?job={id}&embed=1&…` 하나로 맞춘다. 스펙상 장면 id = 작업 id 로 읽었다(실기 미확인).
- effect: 다르다면 Map3DScene 에 ViewUrl 의 job 값을 담도록 Map3DClient.FindSceneAtAsync 만 바꾸면 된다.

## 7. 3D 에서 돌린 방위를 2D 에 되돌릴 때 회전 게이트
- options: (a) 회전 kill-switch · 앵커 게이트를 우회해 강제로 적용 / (b) MapRotation 에 넣어 기존 게이트 판정을 따름
- chosen: (b)
- why: 회전 기능 OFF · 사이트 고정 A모드는 사용자가 정한 잠금이다 — 3D 가 그 잠금을 풀면 안 된다.
- effect: 회전 OFF 면 2D 는 정북 그대로(위치 · 줌만 돌아옴). 로그에 복귀 후 방위가 남는다.
