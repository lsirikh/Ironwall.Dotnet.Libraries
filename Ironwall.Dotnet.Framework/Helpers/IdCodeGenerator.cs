using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Framework.Helpers
{
    static class IdCodeGenerator
    {
        static string GenIdCode()
        {
            var now = DateTime.Now;
            return $"{now.ToString("yy")}{now.ToString("MM")}{now.ToString("dd")}{now.ToString("HH")}{now.ToString("mm")}{now.ToString("ss")}{now.ToString("fff")}";
        }
    }
}
