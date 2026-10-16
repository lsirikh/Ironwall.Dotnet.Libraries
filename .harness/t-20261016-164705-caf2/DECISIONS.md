# 결정 기록 — 로그인 설정 창

## 1. 설정 창을 어디에 띄우나 (MDIX DialogHost 팝업 · 호스트 다이얼로그 메시지 · 로그인 패널 안 덮개)
- options: ① 로그인 패널의 `md:DialogHost`(Identifier=LoginPanel) 팝업 ② 새 `Open…DialogMessageModel` 을 내고 호스트 셸이 띄움 ③ 로그인 패널 뷰 안에 덮개(ContentControl + 암시적 DataTemplate)로 커널 틀을 얹음
- chosen: ③
- why: ①은 MDIX 팝업이 제 흰 카드 크롬을 한 겹 더 그린다(기본 흰 WPF 크롬 금지) · ②는 호스트 배선이 하나 더 필요하고 로그인 전 셸 상태에 기댄다. ③은 라이브러리 안에서 끝나고 창 없이 시험할 수 있다.
- effect: 설정 창은 로그인 패널 칸 위(스크림 `ScrimModalBrush`)에 뜬다. 바꾸려면 `LoginPanelView.xaml` 의 덮개 ContentControl 만 바꾸면 된다(VM 은 그대로).

## 2. 창에 넣을 SSO 설정 항목
- options: SSO 연동 사용만 · SSO 서버 주소 · 앱 이름(clientId) 등도 함께
- chosen: **SSO 연동 사용** 하나 + 지금 상태 한 줄(읽기 전용)
- why: 코드가 실제로 읽는 SSO 설정은 appsettings `AppSettings:IsSsoEnabled` 하나뿐이다. clientId 는 상수(`SsoAgentGateway.AppClientId`)이고 SSO 서버 주소는 GIS 가 아니라 에이전트가 안다 — 새 설정을 만들지 않는다는 지시에 따름.
- effect: 항목을 더하려면 `ILoginSettingsPort` 에 멤버를 더하고(더하기만) 창에 줄을 더한다.

## 3. 화면 문구 — 'SSO' 그대로 쓸지 '통합 로그인' 으로 바꿀지
- options: 'SSO 연동 사용' · '통합 로그인 사용'
- chosen: 'SSO 연동 사용' · 'SSO 에이전트 연결됨/없음' · 'SSO 연동 꺼짐'
- why: 로그인 화면 · 로그아웃 창 · "?" 설명이 이미 'SSO 로 로그인' · 'SSO 에이전트' 를 쓴다(화면 문구 관례). 한 화면에 두 이름이 섞이지 않게 했다.
- effect: 문구는 `LoginSettingsDialogView.xaml` · `LoginPanelViewModel.SsoStatusText` · `AccountsHelp` 세 곳.

## 4. 재시작 길이 없을 때
- options: 라이브러리가 직접 프로세스를 다시 띄움 · 창구에 맡기고 없으면 안내만
- chosen: 창구(`CanRestart` · `Restart()`)에 맡긴다. 호스트에 지금 앱 재시작 길이 없어(Dotnet.Monitoring.Solution 에서 Restart 검색 0건) 호스트 구현은 `CanRestart=false` 로 적었다 → 저장 뒤 "다시 시작하면 적용됩니다" 안내 + [닫기]만.
- why: 와치독 · 저장 안 한 창 확인 등 종료 경로는 호스트가 안다 — 라이브러리가 프로세스를 띄우면 그 길을 건너뛴다.
- effect: 호스트가 재시작 길을 만들면 `CanRestart=true` 로 바꾸기만 하면 [지금 다시 시작] · [나중에] 가 나온다.

## 5. 값을 바꾸지 않고 [저장]
- options: 그대로 기록 · [저장]을 꺼 둠
- chosen: 값이 저장된 값과 같으면 [저장]이 꺼진다(Enter 도 안 나감)
- why: 바뀐 것이 없는데 '재시작 뒤 적용' 안내를 내면 헷갈린다.
- effect: `LoginSettingsDialogViewModel.IsPrimaryEnabled`.
