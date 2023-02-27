using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CompArch.Tomasulo;

public enum ReservationStationStatus
{
    NotBusy,
    IssueStarted,
    WaitForDependencies,
    ExecutionStarted,
    WriteBackStarted
}

public abstract class ReservationStation
{
    public Tomasulo Parent { get; private set; }

    public ReservationStation(int index, Tomasulo parent)
    {
        Index = index;
        Parent = parent;
    }

    public abstract string CategoryName { get; }

    public string Name => $"{CategoryName}{Index}";

    public bool IsBusy { get => Status != ReservationStationStatus.NotBusy; }

    public ReservationStationStatus Status { protected set; get; }

    public bool IsReady
    {

        get => Status ==
            ReservationStationStatus.ExecutionStarted ||
            Status == ReservationStationStatus.WriteBackStarted;
    }


    public Instruction? Instruction { protected set; get; }

    public int CurrentTime { protected set; get; }

    //In general StartIssueTime = LastIssueTime (cycle = 1)

    public int StartIssueTime { private set; get; }
    public int IssueDuration { private set; get; }
    public int LastIssueTime => StartIssueTime + IssueDuration - 1;


    int? _startExecutionTime;
    public int? StartExecutionTime
    {
        internal set
        {
            _startExecutionTime = value;
            StartWriteBackTime = LastExecutionTime + 1;
            Instruction!.Execute = _startExecutionTime;

            Instruction!.WriteBack = StartWriteBackTime;
            Instruction!.WriteBackEnd = Instruction!.WriteBack + WriteBackDuration-1;
        }

        get => _startExecutionTime;
    }

    public int ExecutionDuration { private set; get; }
    public int? LastExecutionTime => StartExecutionTime + ExecutionDuration - 1;

    //In general StartWriteBackTime = LastWriteBackTime (cycle = 1)
    public int? StartWriteBackTime { internal set; get; }
    public int WriteBackDuration { private set; get; }
    public int? LastWriteBackTime => StartWriteBackTime + WriteBackDuration - 1;

    public int Index { get; }

    public virtual void AddInstruction(Instruction instruction, int issueTime,
        int issueDuration, int executionDuration, int writebackDuration)
    {
        Instruction = instruction;
        Status = ReservationStationStatus.IssueStarted;

        CurrentTime = StartIssueTime = issueTime;

        //the following 2 are global settings
        IssueDuration = issueDuration;
        ExecutionDuration = executionDuration; //this should be retrieved externally
        WriteBackDuration = writebackDuration;
    }

    public virtual void ProceedTime()
    {
        if (!IsBusy) return;

        CurrentTime++;

        if (CurrentTime == LastIssueTime + 1)
            Status = ReservationStationStatus.WaitForDependencies;

        if (StartExecutionTime is null) return;

        if (CurrentTime == StartExecutionTime)
            Status = ReservationStationStatus.ExecutionStarted;
        else if (CurrentTime == StartWriteBackTime)
            Status = ReservationStationStatus.WriteBackStarted;
    }

    public void Reset()
    {
        Instruction = null;
        Status = ReservationStationStatus.NotBusy;
    }

    public virtual void WriteToCDB()
    {
        string result = Parent.GetNextValue();

        Console.WriteLine($"  Writing value to CDB: {Name}->{result}");

        foreach (var otherRs in Parent.AllCalcReservationStations.Where(rs => rs.Qk == this))
        {
            otherRs.Vk = result;  //Instruction!.Operand1; //should be the result sth like R[R1]
            otherRs.Qk = null;

            if (otherRs.Qj is null)
                otherRs.StartExecutionTime = CurrentTime + 1;
        }

        foreach (var otherRs in Parent.AllCalcReservationStations.Where(rs => rs.Qj == this))
        {
            otherRs.Vj = result; //Instruction!.Operand1;
            otherRs.Qj = null;

            if (otherRs.Qk is null)
                otherRs.StartExecutionTime = CurrentTime + 1;
        }

        foreach(var entry in Parent.RegisterFile)
        {
            if (entry.Value == Name)  //check if there is a value of the rs
                Parent.RegisterFile[entry.Key] = result;
        }
    }


}


