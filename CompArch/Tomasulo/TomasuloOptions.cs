using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json;

namespace CompArch.Tomasulo;
public class TomasuloOptions
{
    public static string Header = "Tomasulo";

    public int IssuesPerCycle { get; set; } = 1;

    public int IssueDuration { get; set; } = 1;

    public int WriteBackDuration { get; set; } = 1;

    public int CommitsPerCycle { get; set; } = 1;


    #region Serialization

    static JsonSerializerOptions options = new JsonSerializerOptions()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
        //PropertyNamingPolicy = JsonNamingPolicy.CamelCase, 
    };

    public static TomasuloOptions? GetFromJsonString(string json) =>
        JsonSerializer.Deserialize<TomasuloOptions>(json, options);

    public string ToJsonString() =>
        JsonSerializer.Serialize(this, options);
    
    #endregion
}
