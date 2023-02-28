using Microsoft.Extensions.FileSystemGlobbing.Internal.PatternContexts;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompArch.Tomasulo;
public class ScenarioReader
{
    private readonly ILogger<ScenarioReader> _logger;

    //Assists the reading of a file in the following format. 
    //- Each text block is identified by an identifier, which is a commented line, which starts with #.
    //- All lines are whitespace trimmed.
    //- The following example includes 2 sections: "reservation stations" and "functional units"
    //- Each section is a KeyValuePair entry: the Key is the name of the section, the Value is a List of non-empty strings.
    //- Each comment after the first line within the same block is ignored.

    /*
#reservation stations
#('Load' must be always included)
#<name>,<count>
Load,2
Add,2
Mult,2

#functional units
#<name>,<count>
Adder,1
     */

    public ScenarioReader(ILogger<ScenarioReader> logger)
    {
        this._logger = logger;
    }

    public Dictionary<string, List<string>> ReadFile(string fileName)
    {
        _logger.LogDebug("Reading file {file}...", fileName);

        using StreamReader reader = new StreamReader(fileName);
        Dictionary<string, List<string>> sections = new ();
        while(!reader.EndOfStream)
        {
            var nextBlock = ReadNextBlock(reader);
            if (nextBlock is null) break;

            //ignore empty and unnamed blocks
            if (nextBlock.Value.Name is null)
            {
                _logger.LogDebug("Next section is unnamed. Number of lines: {count}.", nextBlock.Value.Lines.Count);
                continue;
            }

            if (nextBlock.Value.Lines.Count == 0)
            {
                _logger.LogDebug("Next section is empty. Name: {name}.",nextBlock.Value.Name);
                continue;
            }


            _logger.LogDebug("Next section read. Name: {name}, Lines: {count}.", nextBlock.Value.Name,nextBlock.Value.Lines.Count);
            sections.Add(nextBlock.Value.Name, nextBlock.Value.Lines);
        }
        return sections;
    }

    private (string? Name, List<string> Lines)? ReadNextBlock(StreamReader reader)
    {
        string line = reader.ReadLine()?.Trim() ?? "";
        if (reader.EndOfStream) return null;

        string? name = null;
        while (line.StartsWith("#") || string.IsNullOrEmpty(line))
        {
            if (line.StartsWith("#") && name is null) name = line.Substring(1);

            line = reader.ReadLine()?.Trim() ?? "";
            if(reader.EndOfStream) return null;
        }

        List<string> lines = new();
        while (!string.IsNullOrEmpty(line))
        {
            if(!line.StartsWith("#")) lines.Add(line);
            if (reader.EndOfStream) break;

            line = reader.ReadLine()?.Trim() ?? "";

        }

        return (name, lines);
    }
}
