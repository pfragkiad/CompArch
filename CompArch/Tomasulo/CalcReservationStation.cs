using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CompArch.Tomasulo;

public class CalcReservationStation : ReservationStation
{
    private readonly string _calculationName;

    public CalcReservationStation(string calculationName, int index, Tomasulo parent) : base(index, parent)
    {
        this._calculationName = calculationName;
    }

    public override string CategoryName => _calculationName;

    public string? AssignedFunctionalUnitsName { get; set; }


    public string? Vj { get; set; }

    public string? Vk { get; set; }

    public ReservationStation? Qj { get; set; }
    public ReservationStation? Qk { get; set; }

    public string? TargetRegister { get; private set; }

    public override void AddInstruction(Instruction instruction, int issueTime, int issueDuration, int executionDuration, int writebackDuration)
    {
        base.AddInstruction(instruction, issueTime, issueDuration, executionDuration, writebackDuration);
        //this must not be null
        TargetRegister = instruction.Operand1!;

        Qj = Qk = null;
        Vj = Vk = null;

        //search for the last write operation of the j operand
        ReservationStation? loadQjFound = Parent.LoadReservationStations?.FirstOrDefault(rs => rs.IsBusy && rs.TargetRegister == instruction.Operand2);
        if (loadQjFound is null)
            loadQjFound = Parent.AllCalcReservationStations.FirstOrDefault(rs => rs.IsBusy && rs.TargetRegister == instruction.Operand2);
        if (loadQjFound is not null)
            Qj = loadQjFound;
        else Vj = instruction.Operand2;

        //search for the last write operation of the k operand
        ReservationStation? loadQkFound = Parent.LoadReservationStations?.FirstOrDefault(rs => rs.IsBusy && rs.TargetRegister == instruction.Operand3);
        if (loadQkFound is null)
            loadQkFound = Parent.AllCalcReservationStations.FirstOrDefault(rs => rs.IsBusy && rs.TargetRegister == instruction.Operand3);
        if (loadQkFound is not null)
            Qk = loadQkFound;
        else Vk = instruction.Operand3;

        //update the register file! (RAT)
        Parent.RegisterFile[TargetRegister] = Name;

        if (Vj is not null & Vk is not null)
        {
            int FUs = Parent.AvailableFunctionalUnits[CategoryName];
            if (FUs > 0)
            {
                Console.WriteLine($"  {Name} reserves FU. FUs({CategoryName}): {FUs}->{FUs-1}");
                Parent.AvailableFunctionalUnits[CategoryName]--;
                StartExecutionTime = issueTime + issueDuration;
            }
        }

    }

    public override void ProceedTime() //same with Load RS
    {
        base.ProceedTime();

        if(CurrentTime ==LastExecutionTime)
        {
            int FUs = Parent.AvailableFunctionalUnits[CategoryName];
            Console.WriteLine($"  {Name} releases FU. FUs({CategoryName}): {FUs}->{FUs + 1}");
            Parent.AvailableFunctionalUnits[CategoryName]++;
        }

        if (CurrentTime == LastWriteBackTime
                && Status == ReservationStationStatus.WriteBackStarted) //WRITE BACK (broadcast value to CDB)
        {
            //write to cdb at the end of the WB 
            WriteToCDB();

            Reset();
        }

    }


    public override string ToString()
    {
        if (!IsBusy) return $"{Name}, Busy: No";

        if (CurrentTime <=LastIssueTime)
            return $"{Name}, Busy: Yes, Op: {Instruction!.Op}, State: ISSUE, Remaining Time: {LastIssueTime - CurrentTime}";

        if(!IsReady)
            return $"{Name}, Busy: Yes, Op: {Instruction!.Op}, State: WAIT, Vj: {Vj ?? "-"}, Vk: {Vk ?? "-"}, Qj: {Qj?.Name ?? "-"}, Qk: {Qk?.Name ?? "-"}";

        if (CurrentTime <= LastExecutionTime && CurrentTime >=StartExecutionTime)
            return $"{Name}, Busy: Yes, Op: {Instruction!.Op}, State: EXECUTE, Vj: {Vj}, Vk: {Vk}, Remaining Time: {LastExecutionTime - CurrentTime}";

        if(CurrentTime <=LastWriteBackTime) //if(CurrentTime<LastExecutionTime)
            return $"{Name}, Busy: Yes, Op: {Instruction!.Op}, State: WRITEBACK, Vj: {Vj}, Vk: {Vk}, Remaining Time: {LastWriteBackTime-CurrentTime}";

        throw new InvalidOperationException();
        //we assume the RS is not busy in any other case
        //return $"{Name}, Busy: No";
    }

}
