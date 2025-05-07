using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Maps

{
    public class MapModel : IMapModel
    {
        #region - Implementations for IMapModel -
        [JsonProperty("id", Order = 1)]
        public int Id { get; set; }             //Not Counted
        [JsonProperty("mapName", Order = 2)]
        public string MapName { get; set; }     //1
        [JsonProperty("mapNumber", Order = 3)]
        public int MapNumber { get; set; }      //2
        [JsonProperty("fileName", Order = 4)]
        public string FileName { get; set; }    //3
        [JsonProperty("fileType", Order = 5)]
        public string FileType { get; set; }    //4
        [JsonProperty("fileUrl", Order = 6)]
        public string Url { get; set; }         //5
        [JsonProperty("width", Order = 7)]
        public double Width { get; set; }       //6
        [JsonProperty("height", Order = 8)]
        public double Height { get; set; }      //7
        [JsonProperty("used", Order = 9)]
        public bool Used { get; set; }          //8
        [JsonProperty("visibility", Order = 10)]
        public bool Visibility { get; set; }    //9
        #endregion

        public bool IsEqual(MapModel model)
        {
            if (model == null)
                return false;

            return Id == model.Id &&
                   MapName == model.MapName &&
                   MapNumber == model.MapNumber &&
                   FileName == model.FileName &&
                   FileType == model.FileType &&
                   Url == model.Url &&
                   Math.Abs(Width - model.Width) < 0.01 &&  // 부동 소수점 비교
                   Math.Abs(Height - model.Height) < 0.01 && // 부동 소수점 비교
                   Used == model.Used &&
                   Visibility == model.Visibility;
        }
    }
}
