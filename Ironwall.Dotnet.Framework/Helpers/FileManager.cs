using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Helpers;

static class FileManager
{
    /// <summary>
    /// CopyUriFile은 프로젝트 하위 폴더에 Uri로 부터 받은 주소의
    /// 정보를 이용해서 데이터를 복사하는 기능을 제공한다.
    /// </summary>
    /// <param name="folder">(필수)프로젝트 하위 폴더 명칭 지정</param>
    /// <param name="Uri">(필수)복사할 데이터의 위치정보</param>
    /// <returns>상대적 위치에 따른 Uri 값</returns>
    static async Task<string?> CopyUriFile(string folder, string Uri)
    {
        try
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(Uri))
                return string.Empty;

            DirectoryInfo di = new DirectoryInfo(System.Environment.CurrentDirectory + $"\\{folder}");
            if (!di.Exists) { di.Create(); }

            string returnValue = "";
            string fileName = "";

            var task = new Task<bool>(() =>
            {
                
                FileInfo fi = new FileInfo(Uri);
                if (fi.Exists)
                    fileName = Path.GetFileName(Uri);

                string destinationFileName = System.IO.Path.Combine(di.FullName, $"{fileName}");
                try
                {
                    File.Copy(Uri, destinationFileName, true);
                }
                catch
                {}

                fi = new FileInfo(destinationFileName);
                if (fi.Exists)
                {
                    returnValue = folder+"\\"+Path.GetFileName(destinationFileName);
                }
                else
                {
                    Debug.WriteLine("파일 복사 실패");
                    return false;
                }
                return true;
            });
            task.Start();

            return await task ? returnValue : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Raise Exception in CopyUriFile : {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// SaveCSVFile을 이용하여 T 타입의 모델을 CSV 형태로 저장할 수 있다
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="Data"></param>
    /// <param name="folder"></param>
    /// <param name="filename"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    static async Task<bool> SaveCSVFile<T>(List<T> Data, string? folder = default, string? filename = default, CancellationToken token = default)
    {
        try
        {
            DirectoryInfo di;

            if (!string.IsNullOrEmpty(folder))
                di = new DirectoryInfo(System.Environment.CurrentDirectory + $"\\{folder}");
            else
                di = new DirectoryInfo(System.Environment.CurrentDirectory);

            if (!di.Exists) { di.Create(); }

            var task = new Task<bool>(() =>
            {
                var uri = Path.Combine(di.FullName, $"{filename}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.csv");
                using (var writer = new StreamWriter(uri, false, Encoding.UTF8))
                using (var csvWriter = new CsvWriter(writer, CultureInfo.CurrentCulture))
                {
                    //Header와 Data를 알아서 넣어줌...
                    csvWriter.WriteRecords(Data);
                }
                return true;
            }, token);
            task.Start();

            return await task;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Raise Exception in SaveFile : {ex.Message}");
            return false;
        }
    }

    static async Task<IEnumerable<T>?> ReadCSVFile<T>(string uri, CancellationToken token = default)
    {
        var items = new List<T>();
        if (string.IsNullOrEmpty(uri))
            return null;

        try
        {
            FileInfo fi = new FileInfo(uri);
            if (!fi.Exists) return null;

            var task = new Task<bool>(() => 
            {
                var csvConfig = new CsvConfiguration(CultureInfo.CurrentCulture)
                {
                    HasHeaderRecord = true,
                    //Comment = '#',
                    //AllowComments = true,
                    Delimiter = ",",
                };

                using(var streamReader = File.OpenText(uri))
                using(var csvReader = new CsvReader(streamReader, csvConfig))
                {
                    while (csvReader.Read())
                    {
                        if (token.IsCancellationRequested)
                            break;

                        var record = csvReader.GetRecord<T>();
                        if(record !=null)
                            items.Add(record);
                    }
                }
                return true;
            }, token);

            task.Start();

            return await task ? items : null;  
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Raise Exception in ReadCSVFile : {ex.Message}");
            return null;
        }
    }
}
