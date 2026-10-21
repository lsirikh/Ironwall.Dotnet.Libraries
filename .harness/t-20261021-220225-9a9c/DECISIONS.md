# DECISIONS — t-20261021-220225-9a9c (GOP 8.0.15 L1 빨강 → 초록 증명)

## 1. "그 묶음의 제품 수정만 되돌리기" 를 어떻게 할까
- options: ① 88e12b1a 이전 파일로 통째 되돌림 ② 묶음마다 그 수정 줄만 최소로 무력화(조건 앞에 `false &&`, 추가 줄 삭제, 옛 줄 복원)
- chosen: ②
- why: 한 파일에 여러 묶음 수정이 섞여 있고(EventApiService 에 G01 · G42 · G51), 시험이 새 형식(`TrackPointsResult`, `AccountSyncNatsService` 등)을 참조해 ①은 컴파일 오류가 된다 — 그러면 "시험이 실패하는 것" 을 볼 수 없다. ②는 컴파일을 유지한 채 그 묶음의 동작만 옛 동작으로 돌린다. pattern: verify
- effect: 되돌림 정의는 `$TEMP/w/revert.py` 의 표(PLAN §되돌림 정의에 그대로 옮김). 복구는 작업 전에 떠 둔 제품 파일 백업을 복사하고 `cmp` + `git diff --quiet 88e12b1a -- <제품 파일>` 로 확인했다. 다른 방식으로 다시 하려면 표만 바꾸면 된다.

## 2. G01 을 한 번에 되돌릴까, 핵심 · 소비처로 나눌까
- options: ① `ApiError.IsRetryable` 만 false 로(모든 소비처가 한꺼번에 빨강) ② 핵심(G01core)과 소비처 분기(G01use)를 따로 되돌림
- chosen: ②
- why: ①만 하면 소비처 6곳의 분기 자체가 시험에 잡히는지는 증명되지 않는다. 둘로 나눠 핵심은 Messages 시험이, 소비처 분기는 각 소비처 시험이 잡는 것을 따로 보였다. pattern: verify · broad
- effect: 결과표에 G01 이 두 줄(G01core · G01use)로 나온다.

## 3. G02 는 앱 로그 배선 시험이 없었다 — 새로 쓸까
- options: ① Messages 의 줄 내용 시험만으로 판정 ② `BearerAuthHandler` 생성자가 `ApiMessageHelper.FailureLog` 를 앱 로거로 잇는지 보는 시험을 더함
- chosen: ② (`Accounts.Api/Tests/BearerAuthFailureLogWiringTests.cs`)
- why: 배선이 빠지면 줄은 만들어져도 아무 데도 남지 않는데 기존 시험은 그것을 못 잡는다("약한 시험"). 정적 상태를 쓰므로 기존 직렬 컬렉션(`BearerAuthHandler-static`)에 넣었다. pattern: verify · reuse
- effect: G02 도 두 줄(G02helper · G02wire)로 증명.

## 4. 기존 시험에 꼬리표를 붙일까
- options: ① 이름 목록으로 걸러 돌림 ② 기존 시험에 `[Trait("Scenario","GOP815-Gnn")]` 를 더해 `--filter Scenario=GOP815-Gnn` 으로 돌림
- chosen: ② — 시험 본문 · 이름은 그대로(이름이 should_X_when_Y 가 아닌 `G01_*` · `G51_*` 도 바꾸지 않음, 기존 시험 재작성 금지)
- why: 다음 사람이 같은 명령으로 묶음별 증명을 다시 돌릴 수 있다. pattern: record
- effect: 꼬리표만 지우면 원래대로.

## 5. 전수 시험에서 Sounds · Libraries.Db 를 어떻게 뺄까
- options: ① sln 그대로 돌리고 해당 실패를 무시 ② `--filter "FullyQualifiedName!~Ironwall.Dotnet.Libraries.Sounds.Tests&FullyQualifiedName!~Ironwall.Dotnet.Libraries.Db.Tests"`
- chosen: ②
- why: Sounds 는 실제 소리가 나고 Libraries.Db 는 다른 PC DB 에 쓴다 — 아예 돌지 않게 해야 한다. 두 프로젝트의 시험 네임스페이스는 각각 `Ironwall.Dotnet.Libraries.Sounds.Tests` · `Ironwall.Dotnet.Libraries.Db.Tests` 뿐임을 확인했다(GMaps.Db 등 다른 Db 프로젝트는 이 문자열에 걸리지 않는다). pattern: follow-recommended
- effect: 하네스 검사(`dotnet test Ironwall.Dotnet.Libraries.sln`, 필터 없음)는 이 둘도 돌린다 — 그쪽 결과는 하네스 기록을 본다.

## 7. G12 · G13 첫 되돌림이 덜 됐다 — 결과를 어떻게 적을까
- options: ① 첫 회차(403 · 503 분류만 빨강)로 판정 ② 판독기(`TrackPointApiReader`) 알림 · 점검과 서비스의 보조 판정(루프 뒤 cursor 남음 → Failed)까지 되돌린 보강 회차(G12full · G13full)를 더 돌림
- chosen: ② — 첫 회차 결과도 그대로 남김
- why: 첫 회차에서 "2쪽째 500" · "400 Invalid cursor" · 판독기 점검 시험이 되돌린 상태에서도 통과했다. 시험이 약한 게 아니라 제 되돌림이 보조 경로를 빠뜨려서였다(보강 회차에서 모두 빨강). 통과로 적기 전에 지시문의 세 시험(403 · 500 · 400)이 모두 빨강이 되는 것을 봐야 했다. pattern: verify
- effect: 결과표의 G12 · G13 은 보강 회차 기준.

## 6. 손댄 시험 파일 중 BOM 이 없던 한글 파일
- options: ① 그대로 둠 ② UTF-8 BOM 을 붙임
- chosen: ② (ServerAxisRetryAndListViewTests · ServerConsoleRetryableConflictTests · EventMappingConflictAndChildBodyTests · ActionEventsFromEventQueryTests · TrackingFetchOutcomeTests)
- why: 저장소 규칙 "한글 소스 UTF-8 BOM 필수". 내용은 바뀌지 않는다.
- effect: 바이트 앞 3개만 달라진다.
