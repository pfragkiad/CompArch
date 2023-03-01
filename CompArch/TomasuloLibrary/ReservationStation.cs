using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CompArch.TomasuloLibrary;

public enum ReservationStationStatus
{
    NotBusy,
    IssueStarted,
    WaitForDependencies,
    WaitForFunctionalUnit,
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

    public Instruction? Instruction { protected set; get; }

    public string? AssignedFunctionalUnitsName { get; set; }


    public int CurrentTime => Parent.CurrentCycle;  //{ protected set; get; }

    //In general StartIssueTime = LastIssueTime (cycle = 1)

    public int StartIssueTime { private set; get; }
    public int IssueDuration { private set; get; }
    public int LastIssueTime => StartIssueTime + IssueDuration - 1;

    public virtual bool ShouldWaitForDependencies { get; }


    int? _startExecutionTime;
    public int? StartExecutionTime
    {
        internal set
        {
            _startExecutionTime = value;
            WriteBackTime = LastExecutionTime + 1;
            Instruction!.Execute = _startExecutionTime;

            Instruction!.WriteBack = WriteBackTime;
            //Instruction!.WriteBackEnd = Instruction!.WriteBack + WriteBackDuration - 1;
        }

        get => _startExecutionTime;
    }

    public int ExecutionDuration { private set; get; }
    public int? LastExecutionTime => _startExecutionTime + ExecutionDuration - 1;

    //In general StartWriteBackTime = LastWriteBackTime (cycle = 1)
    public int? WriteBackTime { internal set; get; }
    //public int WriteBackDuration { private set; get; }
    //public int? LastWriteBackTime => StartWriteBackTime + WriteBackDuration - 1;

    public int Index { get; }

    public virtual void IssueInstruction(Instruction instruction, int issueTime,
        int issueDuration, int executionDuration)
    {
        Instruction = instruction;
        Status = ReservationStationStatus.IssueStarted;

        instruction.Issue = issueTime;
        instruction.IssueEnd = instruction.Issue + issueDuration - 1;

        Console.WriteLine($"  Issuing/Adding command '{instruction.Command}' to {Name}...");

        StartIssueTime = issueTime;

        //the following 2 are global settings
        IssueDuration = issueDuration;
        ExecutionDuration = executionDuration; //this should be retrieved externally
        //WriteBackDuration = writebackDuration;
    }

    public virtual void GotoNextCycle()
    {
        if (!IsBusy) return;

        //still issuing the command
        if (CurrentTime <= LastIssueTime) return;

        //wait for dependencies or FU
        if (Status == ReservationStationStatus.IssueStarted)
            Status = ShouldWaitForDependencies ? ReservationStationStatus.WaitForDependencies : ReservationStationStatus.WaitForFunctionalUnit;

        //check if we can reserve an FU and start execution
        bool statusUpdated = CheckForFunctionUnitAndStartExecution();
        if (statusUpdated) return;

        if (CurrentTime == WriteBackTime)
        {
            Status = ReservationStationStatus.WriteBackStarted;
            Console.WriteLine($"  {Name} writes to CDB. {Name} is released.");

            //RELEASE RS so that other instructions!
            Reset();

            //write to CDB at the END of the WB 
            Parent.WriteToCDB(this);

        }
    }

    public void ForceAssignFU()
    {
        int availableFUs = Parent.AvailableFunctionalUnits[AssignedFunctionalUnitsName!];

        //there is an available FU, so reserve it
        Console.WriteLine($"  {Name} reserves FU. FUs({AssignedFunctionalUnitsName}): {availableFUs}->{availableFUs - 1}");
        Parent.AvailableFunctionalUnits[AssignedFunctionalUnitsName]--;

        //start execution NOW!
        Status = ReservationStationStatus.ExecutionStarted;
        Console.WriteLine($"  {Name} starts execution.");
        StartExecutionTime = CurrentTime;
    }

    //Returns true on status update, else false.
    public bool CheckForFunctionUnitAndStartExecution()
    {

        if (Status == ReservationStationStatus.WaitForDependencies && !ShouldWaitForDependencies && Parent.LastWBTime == CurrentTime)
        {
            //we should not start execution immediately, we should wait at least one cycle
            Status = ReservationStationStatus.WaitForFunctionalUnit;
            return true;
        }

        if (Status == ReservationStationStatus.WaitForDependencies && !ShouldWaitForDependencies //and CurrentTime>Parent > LastWBTime
            || Status == ReservationStationStatus.WaitForFunctionalUnit)
        {
            //check for available function unit if there is one for the reservation station category
            if (AssignedFunctionalUnitsName is not null && Parent.AvailableFunctionalUnits.ContainsKey(AssignedFunctionalUnitsName))
            {
                int availableFUs = Parent.AvailableFunctionalUnits[AssignedFunctionalUnitsName];
                if (availableFUs == 0)
                {
                    Console.WriteLine($"  {Name} is waiting for available FU.");
                    if (Status != ReservationStationStatus.WaitForFunctionalUnit)
                        Status = ReservationStationStatus.WaitForFunctionalUnit;
                    return true;
                }

                //there is an available FU, so reserve it
                Console.WriteLine($"  {Name} reserves FU. FUs({AssignedFunctionalUnitsName}): {availableFUs}->{availableFUs - 1}");
                Parent.AvailableFunctionalUnits[AssignedFunctionalUnitsName]--;
            }

            //start execution NOW!
            Status = ReservationStationStatus.ExecutionStarted;
            Console.WriteLine($"  {Name} starts execution.");

            StartExecutionTime = CurrentTime;
            return true;
        }

        return false;
    }

    public void Reset()
    {
        if (AssignedFunctionalUnitsName is not null && Parent.AvailableFunctionalUnits.ContainsKey(AssignedFunctionalUnitsName)
            && Status == ReservationStationStatus.WriteBackStarted)
            Parent.AvailableFunctionalUnits[AssignedFunctionalUnitsName]++;

        Instruction = null;
        Status = ReservationStationStatus.NotBusy;
    }




}


