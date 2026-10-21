# 공통 규칙 (L2 하위 작업자 전원)

- 작업 폴더(공유 worktree): `C:\workspace_app\Ironwall.Dotnet.Libraries\.harness-worktrees\t-20261021-113151-39a2` — 다른 작업자 3명이 **같은 폴더**에서 서로 다른 파일을 동시에 고친다.
- 명세(읽기 전용): `C:/workspace_python/api-test-server/GOP_Restful_Api_연동설계.md` · `Gop_Message_Broker_연동설계.md` · `CHANGELOG.md`(8.0.15). 크다 — Grep 으로 필요한 절만 읽는다.
- **dotnet 은 반드시 잠금 래퍼로만 돌린다** (빌드가 겹치면 obj 충돌). Bash 에서:
  `powershell -NoProfile -File "$TEMP/locked.ps1" test <csproj 상대경로> --filter "FullyQualifiedName~<클래스>" -v q --nologo`
  (래퍼가 worktree 루트로 이동한다. 빌드만 할 때는 `build <csproj>`.) 잠금을 오래 잡지 않게 **항상 --filter** 로 자기 시험만 돌린다. 솔루션 전체 시험은 돌리지 않는다(총괄이 마지막에 한다).
- 빌드가 **남의 소유 파일** 컴파일 오류로 깨지면 고치지 말고 1~2분 뒤 다시 시도한다(다른 작업자가 고치는 중). 자기 파일은 고친 직후 컴파일이 되게 유지한다 — 남을 막지 않게.
- 소유 경로 밖 파일은 고치지 않는다. 꼭 필요하면 하지 말고 보고에 "필요한 변경" 으로 적는다.
- git 쓰기 명령 금지(commit · checkout · stash · reset 등). 창을 띄우는 시험 · 앱 실행 금지(사용자 PC 사용 중). Sounds · Libraries.Db 시험 금지.
- 원격 123.141.236.253 요청 금지. 라이브는 로컬 시험 서버 `https://localhost:8000/api`(8.0.11)만, 계정 파일은 환경변수 `IRONWALL_E2E_CRED_FILE` 가 가리키는 파일(**내용 출력 금지**). 기존 라이브 도우미 `tests/Shared/LiveApi/*` 를 참고. 만든 시험 자료 이름에는 `harness_20261021_113151_39a2` 를 붙이고 끝나면 지운다. 스크래치 파일은 `$TEMP` 에.
- 코드: 필요한 최소한만 · 주변 스타일 · 주석은 한국어 · **한글이 들어간 .cs/.xaml 은 UTF-8 BOM**(새 파일은 Write 뒤 첫 바이트 EF BB BF 확인; 기존 파일 Edit 은 BOM 유지됨). 6.3.2(원격)·8.0.11(로컬)을 깨지 않는다. 판본 분기는 `IServerContractProbe` 의 `>=` 판정(기존 사용처 방식을 따름).
- 사용자에게 보이는 말은 쉬운 말(내부 용어 · 영어 코드 노출 금지). UI 는 기존 오버레이 디자인 · 테마 토큰(DynamicResource)을 따른다.
- 공개 형식 · 생성자 시그니처를 바꾸면 `C:/workspace_app/Dotnet.Monitoring.Solution` 의 참조를 Grep 으로 확인한다(그쪽은 고치지 않는다 — 깨지면 겹(오버로드)으로 유지).
- 기존 시험을 약하게 하거나 지우지 않는다. 기존 시험 기대값이 명세 변경 때문에 바뀌어야 하면 보고에 근거와 함께 적는다.
- **증명**: 항목마다 헤드리스 시험을 붙이고 "고치기 전 실패 → 고친 뒤 통과" 를 보인다. 방법: 시험을 먼저 써서 빨강(단언 실패)을 확인하고 고친다, 또는 고친 뒤 핵심 줄을 잠시 되돌려 빨강을 본 뒤 복원.
- 갈림길(결과가 달라지는 선택)은 묻지 말고 가장 지시에 맞고 되돌리기 쉬운 쪽을 고르고 보고에 적는다.
- **최종 보고(한국어)**: ① 바꾼/만든 파일 목록 ② 항목별: 무엇을 고쳤나 · 시험 이름 · 빨강 증거(실패 메시지 한 줄) · 초록 증거(통과 수) ③ 갈림길(options/chosen/why/effect) ④ 못 한 것 · 남의 파일에 필요한 변경 ⑤ 라이브에서 만든 것 · 지운 것.
