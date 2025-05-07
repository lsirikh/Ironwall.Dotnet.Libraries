using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Maps

{
    public interface IMapModel
    {
        int Id { get; set; }             //Not Counted
        string MapName { get; set; }     //1
        int MapNumber { get; set; }      //2
        string FileName { get; set; }    //3
        string FileType { get; set; }    //4
        string Url { get; set; }         //5
        double Width { get; set; }       //6
        double Height { get; set; }      //7
        bool Used { get; set; }          //8
        bool Visibility { get; set; }    //9

        bool IsEqual(MapModel model);
    }
}