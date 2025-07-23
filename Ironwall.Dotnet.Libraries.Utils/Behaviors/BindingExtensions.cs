using System;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/15/2025 2:02:05 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public static class BindingExtensions
{
    public static Binding Clone(this Binding src)
    {
        var dst = new Binding
        {
            Path = src.Path,
            Mode = src.Mode,
            UpdateSourceTrigger = src.UpdateSourceTrigger,
            Source = src.Source,
            ElementName = src.ElementName,
            Converter = src.Converter,
            ConverterParameter = src.ConverterParameter,
            ConverterCulture = src.ConverterCulture,
            ValidatesOnDataErrors = src.ValidatesOnDataErrors,
            ValidatesOnExceptions = src.ValidatesOnExceptions,
            NotifyOnValidationError = src.NotifyOnValidationError,
        };
        foreach (var rule in src.ValidationRules)
            dst.ValidationRules.Add(rule);
        return dst;
    }
}
