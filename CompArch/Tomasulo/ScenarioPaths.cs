using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompArch.Tomasulo;

public class ScenarioPaths
{
    public static string Header = nameof(ScenarioPaths);

    public List<File> Files { get; set; }

    public string Selected { get; set; }


    public string? this[string key]
    {
        get { return Files?.FirstOrDefault(f => f.Key == key)?.Path; }
    }

    public string? SelectedPath { get => this[Selected]; }
}

public class File
{
    public string Key { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;
}
