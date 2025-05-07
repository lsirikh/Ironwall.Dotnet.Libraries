using Caliburn.Micro;
using ClosedXML.Excel;
using Dotnet.Gym.Message.Accounts;
using Dotnet.Gym.Message.Enums;
using ExcelDataReader;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Db.Models;
using Ironwall.Dotnet.Libraries.Db.Services;
using MySqlX.XDevAPI.Common;
using System;
using System.IO;
using System.Net.Sockets;
using System.Windows.Interop;

namespace Ironwall.Dotnet.Libraries.Db.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 3/10/2025 4:47:40 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
internal class ExcelImporter : TaskService, IExcelImporter
{

    #region - Ctors -
    public ExcelImporter(ILogService log
                        , IEventAggregator eventAggregator
                        , DbSetupModel setupModel
                        , IDbServiceForGym dbService)
    {
        _log = log;
        _eventAggregator = eventAggregator;
        _dbService = dbService;
        _setup = setupModel;
    }
    #endregion
    #region - Implementation of Interface -
    protected override async Task RunTask(CancellationToken token = default)
    {
        await LoadExcelDataAsync(token).ConfigureAwait(false);
    }

    protected override Task ExitTask(CancellationToken token = default)
    {
        return Task.CompletedTask;
    }
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public async Task LoadExcelDataAsync(CancellationToken token = default)
    {
        try
        {
            if (!_setup.IsLoadExcel) return;
            //await _dbService.DeleteAllUsersAsync();

            // Excel 파일이 위치한 디렉토리
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var excelDir = Path.Combine(currentDir, _setup.ExcelFolder);

            // 폴더 내 첫 번째 Excel 파일 찾기
            var excelFiles = Directory.GetFiles(excelDir, "*.xlsx");
            if (excelFiles.Length == 0)
                throw new Exception($"No Excel files found in the directory({excelDir}).");
            
            var memberExcelFile = excelFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Contains("회원", StringComparison.OrdinalIgnoreCase));
            if (memberExcelFile == null)
                throw new Exception($"No Excel files found in the directory({excelDir}).");

            // 파일이 열려 있는 경우 대비하여 임시 폴더에 복사
            var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xlsx");
            File.Copy(memberExcelFile, tempFilePath, overwrite: true);
            Result = await ImportExcelToDbAsync(tempFilePath, token);

            // 파일 사용 후 삭제
            File.Delete(tempFilePath);
        }
        catch (Exception ex)
        {
            _log?.Error($"Error during Excel data import: {ex.Message}");
        }
    }

    public async Task<bool> ImportExcelToDbAsync(string filePath, CancellationToken token = default)
    {
        try
        {
            using var workbook = new XLWorkbook(filePath);
            if (workbook == null) return false;
            
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "ExcelImporter", Message = $"{filePath}의 정보를 불러옵니다." });

            var worksheet = workbook.Worksheet(2); // 두 번째 시트
            if (worksheet == null) return false;

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "ExcelImporter", Message = $"{filePath}의 정보를 성공적으로 불러옵니다." });

            int rowCount = worksheet.LastRowUsed().RowNumber();

            if (_dbService == null)
                throw new NullReferenceException($"{nameof(IDbServiceForGym)} was not instantiated...");

            var users = await _dbService.FetchUsersAsync(token);

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "ExcelImporter", Message = $"파싱 작업을 시작합니다..." });

            for (int i = 2; i <= rowCount; i++) // 헤더 제외
            {
                //if (i > 520)
                //    _log?.Info("520넘었다.");
                
                var row = worksheet.Row(i);
                string? locker = row.Cell(2).GetString()?.Trim();
                var locker_Color = row.Cell(2).Style.Font.FontColor;
                string? userName = row.Cell(3).GetString()?.Trim();
                // 회원명이 없으면 해당 행을 무시
                if (string.IsNullOrEmpty(userName))
                    continue;

                // 성별 변환
                string? genderStr = row.Cell(35).GetString()?.Trim();
                EnumGenderType gender = genderStr == "남" ? EnumGenderType.MALE :
                                        genderStr == "여" ? EnumGenderType.FEMALE :
                                        EnumGenderType.NONE;

                // 사용자 정보 생성
                var user = new UserModel
                {
                    UserName = userName,
                    MobilePhone = row.Cell(34).GetString()?.Trim() ?? "", // H.P.
                    Age = int.TryParse(row.Cell(36).GetString()?.Trim(), out var age) ? age : 0, // 값이 없거나 변환 실패 시 0
                    Gender = gender,
                    RegisterDate = DateTime.Today,
                    IsActive = EnumTrueFalse.True
                };

                var selectedUser = users?
                                .Where(entity => entity.UserName.Contains(user.UserName) 
                                && entity.Gender == user.Gender
                                && entity.MobilePhone == user.MobilePhone
                                ).FirstOrDefault();

                _currentInfo = $"{user.UserName}/{user.MobilePhone}/{user.Age}/{user.Gender}";

                if (_eventAggregator != null)
                    await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "ExcelImporter", Message = $"{_currentInfo}를 불러왔습니다..." });

                // Locker 처리
                if (locker != null) 
                {
                    string? type = string.Empty;
                    if(locker_Color?.HasValue != null)
                    {
                        type = locker_Color?.Color.Name;
                        //if(type != null)
                        //{
                        //    _log.Info($"{user.UserName}/{type}");
                        //}
                        //else
                        //{

                        //}
                    }

                    user.Locker = type switch
                    {
                        "ff00b050" => new LockerModel { Locker = locker },
                        "ffff0000" => new LockerModel { ShoeLocker = locker },
                        _ => null
                    };
                }

                // 등록 기간 처리
                if (DateTime.TryParse(row.Cell(6).GetString(), out var startDate) &&
                    DateTime.TryParse(row.Cell(7).GetString(), out var endDate))
                {
                    user.ActivePeriod = new ActivePeriod
                    {
                        StartDate = startDate,
                        EndDate = endDate
                    };
                }
                else
                {
                    user.ActivePeriod = null;
                    user.IsActive = EnumTrueFalse.False;
                }

                


                if (selectedUser == null) 
                {
                    var ret = await _dbService.InsertUserAsync(user, token);
                    var msg = $"사용자 {user.UserName}({ret})님이 추가되었습니다.";
                    _log?.Info(msg);
                    if(_eventAggregator != null)
                        await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "ExcelImporter", Message= msg });
                }
                else{

                    //라커비교
                    var selectedLocker = selectedUser?.Locker;
                    var userLocker = user.Locker;
                    var msg = string.Empty;
                    if (selectedLocker != null && userLocker != null)
                    {
                        if (selectedLocker?.Locker != userLocker?.Locker
                            || selectedLocker?.ShoeLocker != userLocker?.ShoeLocker)
                        {
                            selectedLocker.Locker = userLocker?.Locker;
                            selectedLocker.ShoeLocker = userLocker?.ShoeLocker;
                            await _dbService.UpdateLockerAsync(selectedLocker);
                            msg = $"사용자 {selectedUser.UserName}({selectedUser.Id})님의 라커 업데이터...";
                            _log?.Info(msg);
                        }
                    }

                    if (_eventAggregator != null && msg != null)
                        await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "ExcelImporter", Message = msg });

                    //사용기간 비교
                    var selectedActivePeriod = selectedUser?.ActivePeriod;
                    var userActivePeriod = user.ActivePeriod;
                    if (selectedActivePeriod != null && userActivePeriod != null)
                    {
                        if (selectedActivePeriod.StartDate != userActivePeriod?.StartDate
                            || selectedActivePeriod.EndDate != userActivePeriod?.EndDate)
                        {
                            selectedActivePeriod.StartDate = userActivePeriod?.StartDate;
                            selectedActivePeriod.EndDate = userActivePeriod?.EndDate;
                            await _dbService.UpdateActiveUserAsync(selectedActivePeriod);
                            msg = $"사용자 {selectedUser.UserName}({selectedUser.Id})님의 서비스 기간 업데이터...";
                            _log?.Info(msg);
                        }
                    }

                    if (_eventAggregator != null && msg != null)
                        await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "ExcelImporter", Message = msg });
                }
                
            }
            ResultMessage = "모든 회원 정보가 정상적으로 처리되었습니다.";
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = "ExcelImporter", Message = ResultMessage });
            return true;
        }
        catch (Exception ex)
        {
            ResultMessage = $"{_currentInfo}의 정보를 처리하는 중에 다음과 같은 예외가 발생 : {ex.Message}";
            _log?.Error(ResultMessage);
            return false;
        }
    }


    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public bool Result
    {
        get { return _result; }
        set { _result = value; }
    }

    public string? ResultMessage
    {
        get { return _resultMessage; }
        set { _resultMessage = value; }
    }

    #endregion
    #region - Attributes -
    private bool _result;
    private string? _resultMessage = string.Empty;
    private string? _currentInfo;
    private ILogService? _log;
    private IEventAggregator? _eventAggregator;
    private IDbServiceForGym? _dbService;
    private DbSetupModel? _setup;
    #endregion
}