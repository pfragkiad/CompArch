using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CompArch.Tomasulo;

public enum TomasuloScenarios
{
    Basic,
}

public class TomasuloScenarioReader
{
    private readonly Tomasulo _tomasulo;
    private readonly IConfiguration _configuration;
    private readonly ScenarioReader _reader;

    public TomasuloScenarioReader(Tomasulo tomasulo,
        IConfiguration configuration,
        ScenarioReader reader)
    {
        _tomasulo = tomasulo;

        _configuration = configuration;
        this._reader = reader;
    }

    public void RunManualScenario()
    {
        //setup functional units
        _tomasulo.AddFunctionalUnits("Adder", 1);

        //setup reservation stations
        _tomasulo.AddLoadReservationStations(2, "LD");
        _tomasulo.AddCalcReservationStations("Add", 2, "Adder", "ADD", "SUB");
        _tomasulo.AddCalcReservationStations("Mult", 2, null, "MUL", "DIV");

        //setup code and execution times
        string code = @"
        LD R1,0(R2)
        ADD R3,R4,R1
        SUB R5,R4,R1
        MUL R5,R4,R8  
        ";
        Dictionary<string, int> executionTimes = new()
        {
            {"LD",5},
            {"ADD",5},
            {"SUB",5},
            {"MUL",7}
        };

        List<string> registers = Enumerable.Range(1, 8).Select(i => $"R{i}").ToList();

        _tomasulo.SetCode(code, registers, executionTimes, 1, 1, 1, 1);

        _tomasulo.Run();
    }

    public void RunScenario(string scenarioFile)
    {
        //sample file
        //rules:
        //* There must be at least one empty line between different sections (empty space at the end is ignored).
        //* There must be at least one comment line at the start of each block

        /*
#functional units
#<name>,<count>
Adder,1

#reservation stations
#('Load' must be always included)
#<name>,<count>[,<functional unit>]
Load,2
Add,2,Adder
Mult,2


#commands 
#<command>,<duration>,<reservation station>
LD,5,Load
ADD,5,Add
SUB,5,Add
MUL,7,Mult

#registers
R1-R5,R8

#code
LD R1,0(R2)
ADD R3,R4,R1
SUB R5,R4,R1
MUL R5,R4,R8  
         */

        var settings = _reader.ReadFile(scenarioFile);
        int issuesPerCycle = 1;
        int commitsPerCycle = 1;
        int writeBacksPerCycle = 1;
        if (settings.ContainsKey("main"))
        {
            var dictionary = _reader.BlockToDictionary(settings["main"]);
            issuesPerCycle = int.Parse(dictionary["issuesPerCycle"]);
            commitsPerCycle = int.Parse(dictionary["commitsPerCycle"]);
            writeBacksPerCycle = int.Parse(dictionary["writeBacksPerCycle"]);
        }

        Dictionary<string, int> functionalUnitsCount = new Dictionary<string, int>();
        if (settings.ContainsKey("functional units"))
            /*
            #functional units
            #<name>,<count>
            Adder,1
             */
            foreach (string line in settings["functional units"])
            {
                string[] tokens = GetTokens(line);
                functionalUnitsCount.Add(tokens[0], int.Parse(tokens[1]));
            }

        Dictionary<string, int> reservationStationsCount = new Dictionary<string, int>();
        Dictionary<string, string> reservationStationsFunctionalUnits = new Dictionary<string, string>();
        if (settings.ContainsKey("reservation stations"))
            /*
            #reservation stations
            #('Load' must be always included)
            #<name>,<count>[,<functional unit>]
            Load,2
            Add,2,Adder
            Mult,2
            */
            foreach (string line in settings["reservation stations"])
            {
                string[] tokens = GetTokens(line);

                (string Name, int Count, string? FunctionalUnits) rs =
                    (tokens[0], int.Parse(tokens[1]), tokens.Length > 2 ? tokens[2] : null);

                reservationStationsCount.Add(rs.Name, rs.Count);
                if (rs.FunctionalUnits is not null)
                    reservationStationsFunctionalUnits.Add(rs.Name, rs.FunctionalUnits);
            }

        Dictionary<string, List<string>> commands = new Dictionary<string, List<string>>();
        Dictionary<string, int> executionTimes = new Dictionary<string, int>();
        if (settings.ContainsKey("commands"))
            /*
            #commands 
            #<command>,<duration>,<reservation station>
            LD,5,Load
            ADD,5,Add
            SUB,5,Add
            MUL,7,Mult            
             */
            foreach (string line in settings["commands"])
            {
                string[] tokens = GetTokens(line);
                (string Command, int Duration, string ReservationStation) c =
                    (tokens[0], int.Parse(tokens[1]), tokens[2]);

                executionTimes.Add(c.Command, c.Duration);

                if (!commands.ContainsKey(c.ReservationStation))
                    commands.Add(c.ReservationStation, new List<string>());
                commands[c.ReservationStation].Add(c.Command);
            }

        List<string> registers = new();
        if (settings.ContainsKey("registers"))
            /*
            #registers
            R1-R8,R10
            */
            foreach (string line in settings["registers"])
            {
                string[] tokens = GetTokens(line);

                foreach (string t in tokens)
                {
                    Match m = Regex.Match(t, @"(?<prefix>[A-Za-z]+)(?<start>\d+)-([A-Za-z]+)?(?<end>\d+)");
                    if (m.Success)
                    {
                        string prefix = m.Groups["prefix"].Value;
                        int start = int.Parse(m.Groups["start"].Value);
                        int end = int.Parse(m.Groups["end"].Value);

                        registers.AddRange(Enumerable.Range(start, end - start + 1).Select(i => $"{prefix}{i}"));
                    }
                    else
                    {
                        registers.Add(t);
                        //m = Regex.Match(t, @"\w+\d+"));
                        //if (m.Success)
                        //    registers.Add(m.Value);
                    }
                }
            }

        //if (settings.ContainsKey("code"))
        /*
        #code
        LD R1,0(R2)
        ADD R3,R4,R1
        SUB R5,R4,R1
        MUL R5,R4,R8  
         */
        string code = string.Join("\r\n", settings["code"]);


        //add functionsl units
        if (functionalUnitsCount.Count > 0)
        {
            foreach (var entry in functionalUnitsCount)
                _tomasulo.AddFunctionalUnits(entry.Key, entry.Value);
        }

        //add reservation stations
        _tomasulo.AddLoadReservationStations(reservationStationsCount["Load"], commands["Load"].ToArray());
        foreach (var entry in commands.Where(e => e.Key != "Load"))
            _tomasulo.AddCalcReservationStations(entry.Key,
                reservationStationsCount[entry.Key],
                    reservationStationsFunctionalUnits.ContainsKey(entry.Key) ?
                    reservationStationsFunctionalUnits[entry.Key] : null, //functional units 
                commands[entry.Key].ToArray());

        //read from global configuration
        //TomasuloOptions options = new TomasuloOptions();
        //_configuration.GetSection(TomasuloOptions.Header).Bind(options);

        //read from configuration (raw)
        //int issuesPerCycle = int.Parse(_configuration["Tomasulo:IssuesPerCycle"] ?? "1");
        //int issueDuration = int.Parse(_configuration["Tomasulo:IssueDuration"] ?? "1");
        //int writebackDuration = int.Parse(_configuration["Tomasulo:WriteBackDuration"] ?? "1");
        //_tomasulo.AddInstructions(code, executionTimes, issuesPerCycle, issueDuration, writebackDuration);


        //we should verify that instructions are include the required register
        _tomasulo.SetCode(code, registers, executionTimes,
            issuesPerCycle: issuesPerCycle,
            commitsPerCycle: commitsPerCycle,
            writeBacksPerCycle: writeBacksPerCycle);

        _tomasulo.Run();
    }


}
