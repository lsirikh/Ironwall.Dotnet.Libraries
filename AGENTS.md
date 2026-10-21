# AGENTS.md — Ironwall.Dotnet.Libraries

Sensorway 의 .NET 8 재사용 라이브러리 모음(30+) — PIDS · 모니터링 앱의 공통 기반(WPF · Caliburn.Micro · Autofac · Dapper · Redis/NATS · GMap.NET · ONVIF).
구성·사용법의 정본은 [`README.md`](README.md) 와 [`ProjectManual.md`](ProjectManual.md), 변경 이력은 `CHANGELOG.md` 다.

## 프로젝트 정보

```yaml
project_name: "Ironwall.Dotnet.Libraries"
language: "C#"
framework: ".NET 8, WPF, Caliburn.Micro, GMap.NET"
test_framework: "xUnit"
package_manager: "NuGet"
test_command: "dotnet test Ironwall.Dotnet.Libraries.sln"
check_timeout_min: 20
```

- **이 저장소를 쓰는 앱이 있다**: `Dotnet.Monitoring.Solution` 이 형제 폴더 경로(`..\..\Ironwall.Dotnet.Libraries\…`)로 여러 프로젝트를 직접 참조한다. 공개 형식 · 생성자 · 이벤트 모양을 바꾸면 그쪽 빌드가 깨질 수 있다.
- `Ironwall.Dotnet.Libraries.OnvifSolution` 은 서브모듈(github `Ironwall-Onvif-Solution`)이다 — 이 저장소에서 고치지 않는다.
- `GMap.NET/` 은 대부분 git 밖(벤더 소스)이다. 빌드에 필요한 `GMap.NET/Directory.Build.props` · `sn.snk` 는 `.worktreeinclude` 로 하네스 worktree 에 복사된다.
- 코딩 규칙: `.claude/rules/csharp/`.

## 문서

- 분석·요구사항 → `docs/prds/` · 다른 프로젝트와 주고받는 통지·요청·회신 → `docs/coordinations/` · 디자인·와이어프레임·스토리보드 → `docs/designs/`.
- 사용자가 요청할 때만 만든다. 기존 문서는 그대로 둔다.

## 사용자 작업 방식 (평소 지시를 모은 것 — 하네스 작업자도 따른다)

- **끝까지 · 묻지 않고**: 이미 지시한 일의 다음 단계(시험 · 정리 · 다시 확인)는 "진행할까요?" 없이 이어 간다. 갈림길은 가장 완벽한 마감 쪽을 고르고 보고에 적는다. 하네스는 비용보다 완성이 먼저다 — 상한에 걸리면 이어서 끝낸다. "보고만 하고 안 고친 결함"을 남기지 않는다.
- **원인 먼저 · 증거로**: 버그는 로그 · 실측으로 원인을 밝힌 뒤 고친다(추측으로 이것저것 시도 금지). "버그다"라고 할 때는 확정 증거 2개 이상.
- **시험**: 헤드리스로 되는 것은 헤드리스로(시나리오를 최대한 많이, 독립 시나리오는 병렬). 고친 것은 "고치기 전 실패 → 고친 뒤 통과" 같은 증명 방법을 함께 보고한다 — 증명 못 하면 통과로 적지 않는다. 화면을 띄우는 시험(헤디드)은 한 번에 하나만, 사용자가 PC 를 쓰는 중이면 창을 띄우지 않는다. 소리가 나는 Sounds 시험 · 다른 PC DB 에 쓰는 Libraries.Db 시험은 전수 시험에서 뺀다.
- **시험 뒤 정리**: 띄운 프로세스 · 임시 폴더 · 시험 서버의 시험 자료 · 바꾼 설정을 원래대로 하고, 지운 것을 보고한다.
- **운영 서버 쓰기 금지**: 원격 운영 서버(GOP API · SSO, 123.141.236.253)에는 시험 쓰기를 하지 않는다. 이벤트 · 장비 쓰기 시험은 로컬 시험 서버(127.0.0.1)에서만. 서버 판 교체 · 배포는 우리 일이 아니다 — 서버 팀에 필요한 요청은 `docs/coordinations/` 문서로 묻지 말고 보낸다.
- **GIS 는 절대 안 죽는다**: 영상 · PTZ 팝업 등 무엇이 잘못돼도 관제 프로그램 본체는 살아 있어야 한다.
- **UI**: 위치가 의미를 갖는 조작은 드래그 1순위 + 키보드 · 버튼 폴백(`.claude/rules/common/drag-first-ux.md`). 새 화면은 기존 오버레이 창 디자인 · 테마 토큰을 그대로 따른다(기본 흰 WPF 크롬은 미완성). 설명 문구는 화면에 늘어놓지 말고 기능 단위 "?" 말풍선으로, 화면에는 상태 · 행동 알림만. 사용자에게 보이는 말은 쉬운 말(법선 · yaw · 스냅 같은 말 금지).
- **확인은 실제 앱으로**: UI 결함은 격리된 가짜 화면이 아니라 실제 앱 · 실제 컨트롤에서 확인한다. 분기를 새로 만들면 호출부 · DI 배선까지 실제로 이어졌는지 본다.
- **파일 · 디스크**: 한글을 쓴 .cs/.xaml/.ps1 는 UTF-8 BOM. 지우기 · 덮어쓰기 전에는 백업. 기본맵(.mbtiles, 수백 GB)은 앱 프로젝트 `Datas` 에 있는 것이 **원본 — 절대 지우지 않는다**. 빌드 · 실행이 쓰는 사본은 `MapDataDirectory`(이 개발 PC = `D:/Maps/`)에만 만들고 C 드라이브의 다른 곳에는 만들지 않는다.
- **녹화**: 요청할 때만 — 관제 창(또는 그 모니터)만 길게, `C:\Users\gh\Videos` 에.

<!-- harness:begin -->
## 하네스 — 에이전트 공통 규칙 (이 블록은 `node harness/cli.js install` 이 관리한다)

### 자율
- 되돌릴 수 있는 일(파일 편집 · 로컬 테스트와 빌드 · 읽기 전용 명령)은 묻지 말고 한다.
- 되돌릴 수 없거나 밖으로 나가는 일(push · 사용자 브랜치 병합 · 삭제 · 외부 시스템 쓰기 · 비밀 다루기)은 완성된 결과를 보여 주고 한 번만 승인받는다.
- 무엇이 끝인지 처음에 정하고 끝날 때까지 간다 — 중간 요약이나 "계속할까요?" 로 멈추지 않는다. 결과를 바꾸는 갈림길도 묻지 않고 지시에 가장 맞고 나중에 바꾸기 쉬운 쪽을 고르고, 끝난 뒤 보고에 "분기점 · 고른 것 · 이유 · 결과" 로 정리한다.
- 코드는 목표에 필요한 최소한만 쓴다 — 요청 밖 기능 · 옵션 · 한 번 쓰는 추상화 금지 · 표준 라이브러리와 기존 헬퍼 우선 · 바꾼 줄은 모두 요청으로 이어진다(관계없는 정리 · 리팩터 금지) · 자기 변경이 만든 미사용 코드만 지운다. 대답 · 보고는 결론 먼저, 짧게.

### 언어
- 사람이 읽는 것(대답 · 진행 보고 · 요약 · 문서 · PLAN.md · BLOCKED.md · 커밋 메시지)은 **사용자가 쓰는 언어**로 쓴다 — 이 사용자는 한국어. 작업 도중 영어로 바꾸지 않는다. 식별자 · 명령 · 경로 · 원문 인용은 그대로 둔다.

### git
- git 쓰기는 하네스를 거친다. 사용자 브랜치로의 병합 · push · 브랜치 삭제는 사용자가 명시적으로 말한 뒤에만 한다 — 병합은 `node harness/cli.js accept <id>`, push 는 `node harness/cli.js push [<remote>] [<branch>]`(빨리감기만 · 강제 없음). `git push` 를 직접 쓰지 않는다(훅이 막는다).
- `git reset --hard`(와 커밋을 옮기는 reset) · `git clean -f` · `git checkout -- .` · `git rebase` · 직접 `git merge`(`--ff-only` 포함) · `git pull` · `git stash` · `git commit --amend` 는 쓰지 않는다(훅이 막는다). 되돌리기는 `git revert`.
- 모든 작업은 커밋으로 끝난다. 대화형 세션에서는 **자기가 고친 파일만** 경로로 지정해 커밋한다: `git add -- <파일>` → `git commit -m "<무엇을 왜>" -- <파일>`. `git add -A` · `git add .` 는 쓰지 않는다.
- 다른 세션 · 사람의 미커밋 변경은 건드리지 않는다(stash · 되돌리기 · 커밋 모두) · 비밀 파일(`.env*` · `*.pem` · `*.key` · `id_rsa*` · `.npmrc` · `credentials*` · `*.tfstate` · `service-account*.json`)은 커밋하지 않는다.

### 큰 작업 · 여러 세션
- 여러 파일에 걸친 기능 추가 · 수정 요청은 `node harness/cli.js go "<요청 원문>"` 으로 보낸다(`--detach` 로 띄우고 `wait <id>` 로 보고를 받는다 — 셸 안에서 돌리면 셸의 시간 상한에 함께 죽는다). 자기 worktree · 브랜치(`harness/<id>`)에서 끝까지 돌고 보고서를 낸다. 한두 줄 수정 · 질문 · 조사는 그 자리에서 하고, 고친 파일만 커밋한다.
- 여러 모듈 · 여러 에이전트로 나눠 만들고 시험해야 하는 큰 요청은 `node harness/cli.js factory "<요청 원문>"` — 계획 에이전트가 카드(쓰기 소유 경로 · 의존 · 계약)로 나누고, 카드마다 작업이 병렬로 돌아 통합 브랜치 `harness/factory/<id>` 에 하나씩 합쳐지고, 시험 에이전트들이 나눠 시험한다(`factory status|stop|accept <id>` — accept 는 사용자가 말한 뒤). 동시에 같은 저장소를 고치는 작업은 `go … --owns "<glob>"` 로 바꿀 경로를 정한다. 대화형 세션에서도 서로 독립적인 일(조사 · 시나리오 시험 · 검토 · 서로 다른 파일)은 서브에이전트로 나눠 한 메시지에서 동시에 띄운다 — 같은 파일을 고치는 일은 나누지 않는다.
- 사용자가 이미지 · 영상 · 음성 · 문서를 함께 줬으면(붙여 넣은 이미지는 `[Image: source: <경로>]` 로 보인다) 각 파일을 `--attach <경로>` 로 넘긴다 — 영상은 장면 이미지와 전사로, 음성은 전사로 바뀌어 작업자에게 간다.
- 도는 · 끝난 작업에 요구를 덧붙일 때는 `node harness/cli.js note <id> --file <파일>`(다음 턴에 · 끝난 작업은 resume 때 전달). 실행 중인 작업을 멈출 때는 `node harness/cli.js stop <id>`(셸을 죽이지 않는다) — 진행분을 커밋하고 interrupted 로 바꾼다. 그 뒤 `resume` 또는 `cancel`.
- 현황 `node harness/cli.js status` · 받아들이기 `accept <id>`(사용자가 말한 뒤) · 취소 `cancel <id>` · 되살리기 `restore <id>` · 정리 `gc`(끝난 작업) · `cleanup-legacy`(이전 하네스 잔여물 — `--apply` 는 사용자가 말한 뒤).
- 무인 작업자는 허용 목록 밖 명령(`docker build` 등)이 자동 거부된다 — 필요하면 사용자가 `go`/`resume` 에 `--allow "Bash(docker build:*)"` 를 주거나 프로젝트 yaml 블록에 `worker_allow: ["…"]` 를 둔다(git 쓰기 · 셸은 거부된다). BLOCKED 작업은 `resume <id> [--allow …]` 로 같은 세션이 이어 간다.

### 문서 (요청할 때만)
- 문서는 사용자가 요청할 때만 만든다 — 요청 없이 만들거나 고치지 않는다. 종류는 셋: 분석·요구사항 `prd`(docs/prds/) · 다른 프로젝트와의 통신 `coordination`(docs/coordinations/) · 디자인·와이어프레임·스토리보드 `design`(docs/designs/).
- 만들기 `node harness/cli.js doc new <prd|coordination|design> "<제목>"` · 상태가 바뀌면 `doc status <경로> <draft|active|done|superseded>`(대체는 `--by <새 문서>`) · 점검 `doc check` · 문서를 명세로 작업을 보낼 때 `go … --doc <경로>` — 받아들이면(accept) 그 문서에 작업 id 가 기록된다.

### 하네스 보고 (자동)
- 하네스가 오류로 멈추거나 · 의도와 다르게 행동하거나 · 더 나은 방향이 보이면 묻지 말고 올린다: `node harness/cli.js report --kind <bug|intent|improve> --title "<한 줄>" --body "<무엇을 하던 중 · 하네스가 어떻게 했고 · 어떻게 되길 기대했나>"`. 사용자의 건의도 이렇게 올린다. **하네스 동작만** 쓴다 — 프로젝트 코드 · 이름 · 경로 · 비밀 · 작업 내용은 쓰지 않는다(경로 · 이름은 자동으로 가리지만 처음부터 쓰지 않는다). 하네스 예외는 하네스가 스스로 기록해 올린다.

### 할 수 없을 때
- 사용자 없이는 더 나아갈 수 없거나 의도적으로 보호된 것에 막혔을 때만 멈춘다. 하네스 작업에서는 `.harness/<id>/BLOCKED.md` 에 이유 · 시도한 것 · 필요한 것을 쓰고, 대화형 세션에서는 그 세 가지를 그대로 보고한다. 검사를 통과시키려고 테스트를 지우거나 고치지 않는다. 할 수 없으면 할 수 없다고 쓴다.

### 검증
- 완료 신호는 저장소에 원래 있는 테스트 · 빌드 명령의 종료 코드다(사용자가 준 검사가 있으면 그것). 따로 검증 단계를 만들지 않는다.
- 프로젝트 블록에 `lint_command` 가 있으면 끝내기 전에 그것도 돌려 통과시킨다(`{files}` 는 고친 파일 목록). 규칙을 끄거나 무시 주석으로 넘기지 않는다. "끝났다" 고 말할 때는 돌린 명령과 결과(통과 · 실패 수)를 함께 적는다 — 돌리지 않았으면 "확인하지 않았다" 고 쓴다.
<!-- harness:end -->
