# Sso.Client.Sdk (사내 배포본)

SSO Suite 가 배포하는 클라이언트 SDK 의 **고정 판본**이다. 공개 NuGet 피드에 없으므로 리포에 둔다.

| 항목 | 값 |
|---|---|
| 판본 | 3.10.6 (`lib/net8.0` 포함) |
| 출처 | SSO 서버 `/developer` · SSO 저장소 `packaging/sdk/` |
| SHA-256 | `9F748CC15748F7199CFDE302EAC5DD2226F0F532E124FB0AD43397EC71FD9861` (SSO 3.10.6 통보) |
| 쓰는 곳 | `Ironwall.Dotnet.Libraries.Sso` (그 폴더의 `nuget.config` 가 이 폴더를 원본으로 지정) |

판본을 올릴 때: 새 `.nupkg` 를 넣고 해시를 SSO 통보와 대조한 뒤, `Ironwall.Dotnet.Libraries.Sso.csproj` 의
`Version` 을 바꾼다. 옛 파일은 지운다(같은 패키지 ID 의 여러 판이 섞이지 않게).
