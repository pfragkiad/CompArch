using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace CompArch.Tomasulo;
public class Tomasulo
{


    #region Register file

    public List<string> Registers { get; private set; }

    public Dictionary<string, string> RegisterFile { get; private set; }

    public void SetRegisters(IEnumerable<string> registers)
    {
        Registers = registers.ToList();
        RegisterFile = Registers.ToDictionary(r => r, r => "-");
    }

    int iValue = 0;
    public string GetNextValue() => $"v{++iValue}";

    #endregion

    #region Load RS


    public HashSet<string>? LoadCommands { get; private set; }
    public List<LoadReservationStation>? LoadReservationStations { get; private set; }

    public void AddLoadReservationStations(int loadReservationStationsCount, params string[] loadCommands)
    {
        LoadReservationStations =
            Enumerable.Range(1, loadReservationStationsCount).Select(i => new LoadReservationStation(i, this)).ToList();

        LoadCommands = loadCommands.ToHashSet();
    }
    #endregion

    #region Functional Units

    public Dictionary<string, int> AvailableFunctionalUnits { get; private set; }

    public void AddFunctionalUnits (string name, int count)
    {
        AvailableFunctionalUnits ??= new Dictionary<string, int>();
        AvailableFunctionalUnits.Add(name, count);
    }

    #endregion

    #region Calc RS

    public Dictionary<string, HashSet<string>> CalcCommands { get; private set; }
    public Dictionary<string, List<CalcReservationStation>> CalcReservationStations { get; private set; }

    public void AddCalcReservationStations(
        string name, int calcReservationStationsCount, 
        string? functionalUnitsName,  params string[] calcCommands)
    {
        CalcReservationStations ??= new Dictionary<string, List<CalcReservationStation>>();

        CalcReservationStations.Add(name,
           Enumerable.Range(1, calcReservationStationsCount).Select(i => {
               var rs = new CalcReservationStation(name, i, this);

               if (!string.IsNullOrWhiteSpace(functionalUnitsName)) rs.AssignedFunctionalUnitsName = functionalUnitsName;
               return rs;
           }
           ).ToList());

        CalcCommands ??= new Dictionary<string, HashSet<string>>();
        CalcCommands.Add(name, calcCommands.ToHashSet());
    }

    #endregion

    #region Utility collections
    public List<CalcReservationStation> AllCalcReservationStations { get => CalcReservationStations.SelectMany(rs => rs.Value).ToList(); }

    public List<ReservationStation> AllReservationStations
    {
        get => AllCalcReservationStations.Cast<ReservationStation>().Concat(LoadReservationStations).ToList();
    }
    #endregion

    #region Setup commands and execution times

    int _writeBackDuration = 1;
    public int WriteBackDuration { get => _writeBackDuration; }
    //superscalar settings
    int _issuesPerCycle = 1;
    public int IssuesPerCycle { get => _issuesPerCycle; }
  
    int _issueDuration = 1;
    public int IssueDuration { get => _issueDuration; }


    List<Instruction>? instructions;
    public List<Instruction>? Instructions { get => instructions; }
    //public void AddInstructions(List<Instruction> instructions)
    //{
    //    this.instructions = instructions;
    //}

    Dictionary<string, int>? _executionTimes;
    public Dictionary<string, int>? ExecutionTimes { get => _executionTimes; set => _executionTimes = value; }

    public void SetInstructions(string code,
        List<string> registers,
        Dictionary<string, int> executionTimes,
        TomasuloOptions options) =>
        SetInstructions(code, registers, executionTimes,options.IssuesPerCycle,options.IssueDuration,options.WriteBackDuration);
    
    public void SetInstructions(
        string code,
        List<string> registers,
        Dictionary<string, int> executionTimes,
        int issuesPerCycle = 1,
        int issueDuration = 1,
        int writeBackDuration = 1)
    {
        this.instructions = code.Trim().Split("\r\n").Select(l => new Instruction(l)).ToList();

        var instructionRegisters = instructions.SelectMany(i => i.GetUsedRegisters()).Distinct().Order().ToList();

        bool areInstructionRegistersIncluded =
            instructionRegisters.All(r => registers.Contains(r));
        if(!areInstructionRegistersIncluded)
        {
            //get non-included registers
            var nonIncludedRegisters =
                instructionRegisters.Where(r => !registers.Contains(r));
            throw new InvalidOperationException(
                $"The following registers are not declared: {string.Join(", ",nonIncludedRegisters)}");
        }

        this._executionTimes = executionTimes;

        _issuesPerCycle = issuesPerCycle;
        _issueDuration = issueDuration;
        _writeBackDuration = writeBackDuration;

        SetRegisters(registers);

    }
    #endregion


    public void Run()
    {
        int iStartCycle = 0;
        int iCycle = iStartCycle; // instructions[0].Issue!.Value;
        Queue<Instruction> instructionsQueue = new Queue<Instruction>(instructions!);
        while (instructionsQueue.Any()
            || AllReservationStations.Any(s => s.IsBusy))
        {
            iCycle++;

            Console.WriteLine($"\nCYCLE: {iCycle}");

            Console.WriteLine("EVENTS:");

            //for each reservation station update the status and time
            var runningRSs = AllReservationStations.Where(rs => rs.IsBusy).ToList();
            foreach (var rs in runningRSs)
                rs.ProceedTime();

            //issue new commands 
            Queue<Instruction> currentCycleInstructions = new Queue<Instruction>();
            if (instructionsQueue.Any())
            {
                for (int i = 0; i < _issuesPerCycle; i++)
                {
                    if (!instructionsQueue.Any()) break;

                    var next = instructionsQueue.Dequeue();
                    currentCycleInstructions.Enqueue(next);
                    next.Issue = iCycle;
                    next.IssueEnd = next.Issue + _issueDuration - 1;
                    Console.WriteLine($"  Issuing command '{next.Command}'...");
                }
            }

            //add new commands to reservation stations (OR STALL <- check this later)
            foreach (var instruction in currentCycleInstructions)
            {
                if (LoadCommands!.Contains(instruction.Op))
                {
                    //add to load-reservation station!
                    //get first non-busy RS
                    var nextRS = LoadReservationStations!.FirstOrDefault(rs => !rs.IsBusy);
                    nextRS.AddInstruction(
                        instruction, iCycle,
                        _issueDuration, _executionTimes![instruction.Op], _writeBackDuration);
                    Console.WriteLine($"  Adding command '{instruction.Command}' to {nextRS.Name}...");

                    continue;
                }

                {
                    //else it is one of the calc names
                    string rsName = CalcCommands.FirstOrDefault(entry => entry.Value.Contains(instruction.Op)).Key;
                    if (rsName == "") throw new InvalidOperationException($"Operation: '{instruction.Operand1}' is not registered.");
                    //get the first calc reservation station!

                    var nextRS = CalcReservationStations[rsName].FirstOrDefault(rs => !rs.IsBusy);
                    nextRS.AddInstruction(instruction, iCycle, _issueDuration, _executionTimes[instruction.Op], _writeBackDuration);
                    Console.WriteLine($"  Adding command '{instruction.Command}' to {nextRS.Name}...");

                }
            }

            Console.WriteLine("RESERVATION STATIONS:");
            //if (iCycle == 7) Debugger.Break();

            var allRSs = AllReservationStations;
            foreach (var rs in allRSs)
                Console.WriteLine($"  {rs}");

            Console.WriteLine("REGISTERS:");
            foreach (var entry in RegisterFile)
                Console.WriteLine($"  {entry.Key}: {entry.Value}");


        }

        Console.WriteLine("\nSCHEDULE:");
        //at the end print the table!
        foreach (var instruction in instructions!)
            Console.WriteLine(instruction);
    }


}
