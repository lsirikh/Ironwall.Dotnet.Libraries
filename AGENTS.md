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

<!-- harness:begin -->
## 하네스 — 에이전트 공통 규칙 (이 블록은 `node harness/cli.js install` 이 관리한다)

### 자율
- 되돌릴 수 있는 일(파일 편집 · 로컬 테스트와 빌드 · 읽기 전용 명령)은 묻지 말고 한다.
- 되돌릴 수 없거나 밖으로 나가는 일(push · 사용자 브랜치 병합 · 삭제 · 외부 시스템 쓰기 · 비밀 다루기)은 완성된 결과를 보여 주고 한 번만 승인받는다.
- 무엇이 끝인지 처음에 정하고 끝날 때까지 간다 — 중간 요약이나 "계속할까요?" 로 멈추지 않는다.

### 언어
- 사람이 읽는 것(대답 · 진행 보고 · 요약 · 문서 · PLAN.md · BLOCKED.md · 커밋 메시지)은 **사용자가 쓰는 언어**로 쓴다 — 이 사용자는 한국어. 작업 도중 영어로 바꾸지 않는다. 식별자 · 명령 · 경로 · 원문 인용은 그대로 둔다.

### git
- git 쓰기는 하네스를 거친다. 사용자 브랜치로의 병합 · push · 브랜치 삭제는 사용자가 명시적으로 말한 뒤에만 한다 — 병합은 `node harness/cli.js accept <id>`, push 는 `node harness/cli.js push [<remote>] [<branch>]`(빨리감기만 · 강제 없음). `git push` 를 직접 쓰지 않는다(훅이 막는다).
- `git reset --hard`(와 커밋을 옮기는 reset) · `git clean -f` · `git checkout -- .` · `git rebase` · 직접 `git merge`(`--ff-only` 포함) · `git pull` · `git stash` · `git commit --amend` 는 쓰지 않는다(훅이 막는다). 되돌리기는 `git revert`.
- 모든 작업은 커밋으로 끝난다. 대화형 세션에서는 **자기가 고친 파일만** 경로로 지정해 커밋한다: `git add -- <파일>` → `git commit -m "<무엇을 왜>" -- <파일>`. `git add -A` · `git add .` 는 쓰지 않는다.
- 다른 세션 · 사람의 미커밋 변경은 건드리지 않는다(stash · 되돌리기 · 커밋 모두).
- 비밀 파일(`.env*` · `*.pem` · `*.key` · `id_rsa*` · `.npmrc` · `credentials*` · `*.tfstate` · `service-account*.json`)은 커밋하지 않는다.

### 큰 작업 · 여러 세션
- 여러 파일에 걸친 기능 추가 · 수정 요청은 `node harness/cli.js go "<요청 원문>"` 으로 보낸다. 자기 worktree · 브랜치(`harness/<id>`)에서 끝까지 돌고 보고서를 낸다.
- 사용자가 이미지 · 영상 · 음성 · 문서를 함께 줬으면(붙여 넣은 이미지는 `[Image: source: <경로>]` 로 보인다) 각 파일을 `--attach <경로>` 로 넘긴다 — 영상은 장면 이미지와 전사로, 음성은 전사로 바뀌어 작업자에게 간다.
- 실행 중인 작업을 멈출 때는 `node harness/cli.js stop <id>`(셸을 죽이지 않는다) — 진행분을 커밋하고 interrupted 로 바꾼다. 그 뒤 `resume` 또는 `cancel`.
- 현황 `node harness/cli.js status` · 받아들이기 `accept <id>`(사용자가 말한 뒤) · 취소 `cancel <id>` · 되살리기 `restore <id>` · 정리 `gc`(끝난 작업) · `cleanup-legacy`(이전 하네스 잔여물 — `--apply` 는 사용자가 말한 뒤).
- 무인 작업자는 허용 목록 밖 명령(`docker build` 등)이 자동 거부된다 — 필요하면 사용자가 `go`/`resume` 에 `--allow "Bash(docker build:*)"` 를 주거나 프로젝트 yaml 블록에 `worker_allow: ["…"]` 를 둔다(git 쓰기 · 셸은 거부된다). BLOCKED 작업은 `resume <id> [--allow …]` 로 같은 세션이 이어 간다.
- 한두 줄 수정 · 질문 · 조사는 그 자리에서 하고, 고친 파일만 커밋한다.

### 문서 (요청할 때만)
- 문서는 사용자가 요청할 때만 만든다 — 요청 없이 만들거나 고치지 않는다. 종류는 셋: 분석·요구사항 `prd`(docs/prds/) · 다른 프로젝트와의 통신 `coordination`(docs/coordinations/) · 디자인·와이어프레임·스토리보드 `design`(docs/designs/).
- 만들기 `node harness/cli.js doc new <prd|coordination|design> "<제목>"` · 상태가 바뀌면 `doc status <경로> <draft|active|done|superseded>`(대체는 `--by <새 문서>`) · 점검 `doc check`.
- 문서를 명세로 작업을 보낼 때 `go … --doc <경로>` — 받아들이면(accept) 그 문서에 작업 id 가 기록된다.

### 할 수 없을 때
- 사용자 없이는 더 나아갈 수 없거나 의도적으로 보호된 것에 막혔을 때만 멈춘다. 하네스 작업에서는 `.harness/<id>/BLOCKED.md` 에 이유 · 시도한 것 · 필요한 것을 쓰고, 대화형 세션에서는 그 세 가지를 그대로 보고한다.
- 검사를 통과시키려고 테스트를 지우거나 고치지 않는다. 할 수 없으면 할 수 없다고 쓴다.

### 검증
- 완료 신호는 저장소에 원래 있는 테스트 · 빌드 명령의 종료 코드다(사용자가 준 검사가 있으면 그것). 따로 검증 단계를 만들지 않는다.
<!-- harness:end -->
