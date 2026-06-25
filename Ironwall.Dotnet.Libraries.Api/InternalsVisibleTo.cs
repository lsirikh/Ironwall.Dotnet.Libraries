using System.Runtime.CompilerServices;

// GOP-00: Accounts.Api 테스트에서 ApiService.BuildExceptionResponse(internal) 검증 접근 허용.
[assembly: InternalsVisibleTo("Ironwall.Dotnet.Libraries.Accounts.Api")]
