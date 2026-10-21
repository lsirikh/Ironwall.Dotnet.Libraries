# DECISIONS — L1

(1~6 은 앞선 작업 t-20261021-113123-a5f4 의 결정을 그대로 옮긴 것이다. 이번 작업은 그 변경을 v2.6 위에 다시 적용했다.)

## 1. G02 실패 로그(req=)를 어디서 남길까
- options: ① 도메인 서비스 34개 파일 · 114곳의 실패 로그에 각각 req 를 붙인다 ② 응답 변환기(ApiMessageHelper) 한 곳에서 실패 응답마다 한 줄을 남기고, 로거는 밖에서 연결한다 ③ ApiService(HTTP 계층)에서 남긴다
- chosen: ② — `ApiMessageHelper.FailureLog`(정적 Action) + `BearerAuthHandler` 생성자에서 앱 로거로 연결(`??=`, 첫 로거 유지). 봉투가 없으면 `X-Request-ID` 헤더 값을 `Meta.RequestId` 에도 채운다.
- why: ①은 흩어져 빠지기 쉽고 owns 범위를 크게 넘는다. ③은 Api 프로젝트가 Messages 를 참조하지 않아 봉투의 meta.request_id 를 볼 수 없다. BearerAuthHandler 는 모든 도메인 ApiService 파이프라인에 들어가 있다.
- effect: 실패 응답마다 `[API] 실패 <상태> <코드> <메서드> <경로> req=<id>` 경고 한 줄이 앱 로그에 남는다. 화면 문구는 그대로. 다른 곳에 잇고 싶으면 `FailureLog` 를 다른 자리에서 설정하면 된다.

## 2. G42 `GetActionEventsAsync` 에 인자를 더하는 방식
- options: ① 기존 메서드 끝에 `fromEventId` · `user` 선택 인자 추가 ② 같은 이름의 오버로드(인자 7개, 선택 아님)를 새로 두고 기존 메서드는 위임
- chosen: ②
- why: ①은 시험 · 미리보기 도구의 Moq 식 트리 20여 곳이 "식 트리는 선택 인자를 생략할 수 없음"(CS0854)으로 컴파일이 깨진다. ②는 기존 호출 · 목을 그대로 두고 새 쿼리를 보낼 수 있다.
- effect: 연결 이벤트 조치 이력은 새 오버로드로 조회한다. 6.3.2 도 두 쿼리를 받아(서버 v6.3.2 소스) 판본 게이트는 두지 않았다. 나중에 하나로 합치려면 목 20여 곳을 7인자로 바꾼 뒤 기존 메서드를 지우면 된다.

## 3. G12 · G13 Playback 에 실패 종류를 전하는 통로
- options: ① `ITrackPointReader.FetchAsync` 반환형을 결과 객체로 바꾼다 ② 인터페이스에 기본 구현 멤버(`LastFetchNotice` · `CheckAvailabilityAsync`)를 더하고 서버 판독기만 구현한다
- chosen: ② (`ITrackingApiService` 에는 `GetTrackPointsWithStatusAsync` · `GetHealthAsync` 를 새로 두고 기존 `GetTrackPointsAsync` 는 위임)
- why: ①은 sln 밖 시험(tests/GMaps.MapAux.Tests)의 가짜 판독기 2개와 로컬 판독기를 모두 고쳐야 한다. ②는 예외를 던지지 않는 기존 계약을 그대로 둔다.
- effect: 403 → "추적 이력을 볼 권한이 없습니다(지도 보기 권한 필요)", 그 밖의 실패 · 부분 → "일부만 불러왔습니다"(읽은 점은 그대로 보임), health 503 → "추적 이력 저장소를 쓸 수 없습니다"(조회 안 함, 다음 불러오기에서 다시 점검). health 는 막히지 않은 첫 확인 뒤로는 다시 부르지 않는다.

## 5. G08 "콘솔이 열려 있을 때만 다시 읽기" 를 어떻게 잇나
- options: ① 서비스가 EventAggregator 메시지를 내고 콘솔이 받는다 ② 서비스가 작은 인터페이스(IAccountSyncTarget)로 콘솔을 지연 해석해 열림 · 권한을 보고 직접 부른다
- chosen: ② — 계정 콘솔이 `IAccountSyncTarget`(IsSyncTargetOpen = IsActive · ReloadFromServerAsync) 를 구현. 고치던 칸이 있으면(DetailIsDirty) 다시 읽지 않는다.
- why: "열려 있을 때만 · 500ms 로 모아 · 권한 있을 때만" 판정을 한 곳(서비스)에서 시험할 수 있고, 콘솔은 목록 재조회만 맡는다.
- effect: 본인 계정 변경이면 `/auth/me/permissions` 로 권한을 다시 평가한다(403 은 조용히). 그룹 변경(SYNC_USER_GROUP)으로 본인 권한이 바뀌는 경우는 명세 지시대로 다루지 않는다(권한 재조회 타이머 · 다음 로그인 때 반영).

## 6. G09 "구성 0개면 [적용] 끔" 의 범위
- options: ① 수정 화면에서 늘 끈다(이름만 고칠 때도) ② 구성을 보내는 저장일 때만 끈다 — 구성을 건드리지 않은 이름 · 설명만의 PATCH 는 허용
- chosen: ② (등록은 늘 구성을 보내므로 늘 막힌다)
- why: 구성 키를 싣지 않는 PATCH 는 서버가 0개 검사를 하지 않는다. ①이면 이미 0개로 저장된 옛 템플릿의 이름도 못 고친다.
- effect: 시험 `should_still_allow_a_rename_of_a_template_whose_components_are_untouched` 가 이 동작을 고정한다. 지시문 문자 그대로(①)로 바꾸려면 `ReportConsoleViewModel.DetailCanApply` 의 0개 조건에서 "구성 손댐" 조건을 빼면 된다.

## 4. G28 서버 목록이 계정 이름 칸을 쓰는가
- options: ① 목록도 view=full 유지(사유 주석만) ② view=basic&include=server_config 로 바꿈
- chosen: ② — 서버 콘솔 상세는 단건 조회(`GetServerAsync`, view=full)로 따로 읽어 계정 이름을 채운다(`ServerMonitorViewModel.LoadDetailAsync`). 목록 행은 계정 이름을 쓰지 않는다.
- effect: 단건 조회가 실패했을 때만 상세가 목록 행 값으로 대신 차고, 그때는 계정 이름 칸이 빈다(조회 실패 상황).
