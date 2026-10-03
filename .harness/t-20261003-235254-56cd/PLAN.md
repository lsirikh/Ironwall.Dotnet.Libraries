# PLAN — 조치보고 창 사진 보기 + 눌러서 크게 보기

## 목표
조치보고 창(탐지 조치보고 대화상자 `DetectionReportDialogView`)에서 탐지 사진(썸네일)을 볼 수 있고, 사진을 누르면 크게 본다.

## 결정
- "조치보고 창" = `Events.Ui/Views/Dialogs/DetectionReportDialogView.xaml`(+ 장애 `MalfunctionReportDialogView`). 장애 이벤트 모델에는 사진(Thumbnail)이 없어 장애 창은 대상 아님.
- 탐지 창에는 150×150 썸네일이 이미 있었으나 ① 창을 연 뒤 SYNC_DETECTION 으로 사진이 늦게 도착하면 갱신되지 않았다(HasThumbnail 이 연 순간 값으로 굳음) ② 누를 수 없었다.
- ① 대화상자 VM 이 원본 카드의 PropertyChanged(Thumbnail/HasThumbnail/ThumbnailUri)를 듣고 편집기(DetectionSelectionViewModel.RefreshThumbnail)를 갱신. 창을 닫거나(Deactivate) 다른 이벤트로 다시 열면 구독 해제.
- ② 썸네일을 Button(키보드로도 눌림)으로 바꾸고, 누르면 같은 DataTemplate 안 Popup 을 창 가운데에 띄운다. 크기는 창의 85% 안에서 비율 유지(`ThumbnailZoomRules.Fit`, 단위 시험). 사진을 누르거나 · 바깥을 누르거나 · ESC 로 닫는다(ESC 는 확대만 닫고 조치보고 창은 닫지 않음).
- x:Name 은 새로 달지 않는다(Caliburn 지시자 규칙) — 팝업은 코드 숨김에서 버튼의 형제로 찾는다.

## 진행
- [x] 위치 · 기존 구현 조사
- [x] ThumbnailZoomRules + 시험
- [x] 뷰(XAML · 코드 숨김)
- [x] 늦게 온 사진 갱신(VM)
- [x] 시험: `Events.Ui/Tests/ActionReportThumbnailZoomTests.cs`(크기 판정 7 · 늦은 사진/구독 해제 3 · XAML 2) — Events.Ui 1688건 전부 통과
- [x] 솔루션 `dotnet test` 실행 — 보인 결과 줄은 전부 통과(출력이 길어 끝 30줄만 확인, 최종 판정은 하네스 검사)

## 남은 일 · 미검증
- 실제 앱 창에서 확대 팝업 위치 · 크기 · ESC 동작은 눈으로 확인하지 못했다(사용자 데스크톱 사용 중일 수 있어 WPF 창 띄우는 시험 금지).
- 같은 URL 에 새 사진(PTZ 재촬영, 카드의 ?v= 캐시버스트)은 대화상자 쪽이 옛 캐시 사진을 보일 수 있다 — 범위 밖으로 둠.
