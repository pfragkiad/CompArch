﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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

    public void AddFunctionalUnits(string name, int count)
    {
        AvailableFunctionalUnits ??= new Dictionary<string, int>();
        AvailableFunctionalUnits.Add(name, count);
    }

    #endregion

    #region Calc RS

    public Dictionary<string, HashSet<string>> CalcCommands { get; private set; }

    public Dictionary<string, string> CalcCommandReservationStations { get; private set; }

    public Dictionary<string, List<CalcReservationStation>> CalcReservationStations { get; private set; }

    public void AddCalcReservationStations(
        string reservationStationName, int calcReservationStationsCount,
        string? functionalUnitsName, params string[] calcCommands)
    {
        CalcReservationStations ??= new Dictionary<string, List<CalcReservationStation>>();

        CalcReservationStations.Add(reservationStationName,
           Enumerable.Range(1, calcReservationStationsCount).Select(i =>
           {
               var rs = new CalcReservationStation(reservationStationName, i, this);

               if (!string.IsNullOrWhiteSpace(functionalUnitsName)) rs.AssignedFunctionalUnitsName = functionalUnitsName;
               return rs;
           }
           ).ToList());

        CalcCommands ??= new Dictionary<string, HashSet<string>>();
        CalcCommands.Add(reservationStationName, calcCommands.ToHashSet());

        CalcCommandReservationStations ??= new Dictionary<string, string>();
        foreach (var command in calcCommands)
            CalcCommandReservationStations.Add(command, reservationStationName);
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

    public void SetCode(string code,
        List<string> registers,
        Dictionary<string, int> executionTimes,
        TomasuloOptions options) =>
        SetCode(code, registers, executionTimes, options.IssuesPerCycle, options.IssueDuration, options.WriteBackDuration);

    public void SetCode(
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
        if (!areInstructionRegistersIncluded)
        {
            //get non-included registers
            var nonIncludedRegisters =
                instructionRegisters.Where(r => !registers.Contains(r));
            throw new InvalidOperationException(
                $"The following registers are not declared: {string.Join(", ", nonIncludedRegisters)}");
        }

        this._executionTimes = executionTimes;

        _issuesPerCycle = issuesPerCycle;
        _issueDuration = issueDuration;
        _writeBackDuration = writeBackDuration;



        SetRegisters(registers);

    }
    #endregion


    public int CurrentCycle { get; private set; }

    public Queue<Instruction> InstructionsQueue { get; private set; }

    public void Run()
    {
        if(this.instructions is null || instructions.Count==0)
        {
            Console.WriteLine("No instructions to process.");
            return;
        }

        const int iStartCycle = 0;
        CurrentCycle = iStartCycle;
        InstructionsQueue = new Queue<Instruction>(this.instructions);

        // instructions[0].Issue!.Value;
        //Queue<Instruction> instructionsQueue = new Queue<Instruction>(instructions!);
        //LinkedList<Instruction> instructions = new LinkedList<Instruction>(this.instructions!);

        List<Instruction> instructionsWaitingForRS = new List<Instruction>();

        while (InstructionsQueue.Any() || AllReservationStations.Any(s => s.IsBusy))
        {
            //if there is a non-issed instruction then STALL until its corresponding RS is ready
            do
            {
                ProceedCycleAndUpdateRS();

                for (int i = instructionsWaitingForRS.Count - 1; i >= 0; i--)
                {
                    Instruction nonIssuedInstruction = instructionsWaitingForRS[i];

                    ReservationStation? availableRs = GetNextAvailableRS(nonIssuedInstruction);
                    if (availableRs is not null)
                    {
                        instructionsWaitingForRS.Remove(nonIssuedInstruction);
                        availableRs.IssueInstruction(nonIssuedInstruction, CurrentCycle, _issueDuration, _executionTimes![nonIssuedInstruction.Op], _writeBackDuration);
                    }
                }
                //proceed the cycle until its corresponding RS is ready / else STALL
            } while (instructionsWaitingForRS.Any());

            if (!InstructionsQueue.Any()) continue;

            //issue new commands (should DEQUEUE commands ONLY if the RS is available!)
            List<Instruction> instructionsToBeIssued = GetInstructionsToBeIssued();

            foreach (var instruction in instructionsToBeIssued)
            {
                //var next = instructionsQueue.Dequeue();
                ReservationStation? availableRs = GetNextAvailableRS(instruction);

                if (availableRs is null)
                {
                    instructionsWaitingForRS.Add(instruction);
                    continue;
                }

                //an available rs has been found!
                availableRs.IssueInstruction(instruction, CurrentCycle, _issueDuration, _executionTimes![instruction.Op], _writeBackDuration);
            }

            PrintRegistrationStationsAndRegistersStatus();
        }

        Console.WriteLine("\nSCHEDULE:");
        //at the end print the table!
        foreach (var instruction in InstructionsQueue!)
            Console.WriteLine(instruction);
    }

    private List<Instruction> GetInstructionsToBeIssued()
    {
        int instructionsToIssue = Math.Min(InstructionsQueue.Count, _issuesPerCycle);
        List<Instruction> instructionsToBeIssued = new();
        for (int i = 0; i < instructionsToIssue; i++)
            instructionsToBeIssued.Add(InstructionsQueue.Dequeue());
        return instructionsToBeIssued;
    }

    private void PrintRegistrationStationsAndRegistersStatus()
    {
        Console.WriteLine("RESERVATION STATIONS:");
        var allRSs = AllReservationStations;
        foreach (var rs in allRSs) Console.WriteLine($"  {rs}");

        Console.WriteLine("REGISTERS:");
        foreach (var entry in RegisterFile) Console.WriteLine($"  {entry.Key}: {entry.Value}");
    }

    private ReservationStation? GetNextAvailableRS(Instruction? instruction)
    {
        ReservationStation? availableRs = null;
        if (LoadCommands!.Contains(instruction.Op))
            availableRs = LoadReservationStations!.FirstOrDefault(rs => !rs.IsBusy);
        else //it is a calc command
        {
            string rsName = CalcCommandReservationStations[instruction.Op];
            availableRs = CalcReservationStations[rsName].FirstOrDefault(rs => !rs.IsBusy);
        }

        return availableRs;
    }

    private void ProceedCycleAndUpdateRS()
    {
        CurrentCycle++;

        Console.WriteLine($"\nCYCLE: {CurrentCycle}");
        Console.WriteLine("EVENTS:");

        //for each reservation station update the status and time
        //this should run prior to issuing new instructions
        var runningRSs = AllReservationStations.Where(rs => rs.IsBusy).ToList();
        foreach (var rs in runningRSs)
            rs.GotoNextCycle();
    }


    public void WriteToCDB(ReservationStation whoWrites, int currentTime) //This is common for both the CalcRS and LoadRS
    {
        string result = GetNextValue();

        Console.WriteLine($"  Writing value to CDB: {whoWrites.Name}->{result}");

        foreach (var otherRs in AllCalcReservationStations.Where(rs => rs.Qk == whoWrites))
        {
            otherRs.Vk = result;  //Instruction!.Operand1; //should be the result sth like R[R1]
            otherRs.Qk = null;

            if (otherRs.Qj is null)
                otherRs.StartExecutionTime = currentTime + 1;
        }

        foreach (var otherRs in AllCalcReservationStations.Where(rs => rs.Qj == whoWrites))
        {
            otherRs.Vj = result; //Instruction!.Operand1;
            otherRs.Qj = null;

            if (otherRs.Qk is null)
                otherRs.StartExecutionTime = currentTime + 1;
        }

        foreach (var entry in RegisterFile)
        {
            if (entry.Value == whoWrites.Name)  //check if there is a value of the rs
                RegisterFile[entry.Key] = result;
        }
    }
}