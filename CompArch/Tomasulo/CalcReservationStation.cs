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


    public string? Vj { get; set; }

    public string? Vk { get; set; }

    public ReservationStation? Qj { get; set; }
    public ReservationStation? Qk { get; set; }

    public string? TargetRegister { get; private set; }

    public override void IssueInstruction(Instruction instruction, int issueTime, int issueDuration, int executionDuration)
    {
        base.IssueInstruction(instruction, issueTime, issueDuration, executionDuration);
        //this must not be null
        TargetRegister = instruction.Operand1!;

        Qj = Qk = null;
        Vj = Vk = null;

        //search for the last write operation of the j operand
        ReservationStation? loadQjFound = Parent.LoadReservationStations?.FirstOrDefault(rs => rs.IsBusy && rs.TargetRegister == instruction.Operand2);
        if (loadQjFound is null)
            loadQjFound = Parent.AllCalcReservationStations.FirstOrDefault(rs => rs.IsBusy && rs.TargetRegister == instruction.Operand2);
        if (loadQjFound is not null)
        {
            Qj = loadQjFound;
            Instruction.Comment += $"Depends on {Qj.Name} ({Parent.Instructions.IndexOf(Qj.Instruction)+1})";
        }
        else Vj = instruction.Operand2;

        //search for the last write operation of the k operand
        ReservationStation? loadQkFound = Parent.LoadReservationStations?.FirstOrDefault(rs => rs.IsBusy && rs.TargetRegister == instruction.Operand3);
        if (loadQkFound is null)
            loadQkFound = Parent.AllCalcReservationStations.FirstOrDefault(rs => rs.IsBusy && rs.TargetRegister == instruction.Operand3);
        if (loadQkFound is not null)
        {
            Qk = loadQkFound;
            Instruction.Comment += (Instruction.Comment is null ? "Depends" :" and") + 
                $" on {Qk.Name} ({Parent.Instructions.IndexOf(Qk.Instruction) + 1})";
        }
        else Vk = instruction.Operand3;

        //update the register file! (RAT)
        Parent.RegisterFile[TargetRegister] = Name;
    }

    public override bool ShouldWaitForDependencies => Qk is not null || Qj is not null;

    public override string ToString()
    {
        if (!IsBusy) return $"{Name}, Busy: No";

        if (Status==ReservationStationStatus.IssueStarted)
            return $"{Name}, Busy: Yes, Op: {Instruction!.Op}, State: ISSUE, Vj: {Vj ?? "-"}, Vk: {Vk ?? "-"}, Qj: {Qj?.Name ?? "-"}, Qk: {Qk?.Name ?? "-"}, Remaining Time: {LastIssueTime - CurrentTime}";

        if (Status==ReservationStationStatus.WaitForDependencies)
            return $"{Name}, Busy: Yes, Op: {Instruction!.Op}, State: WAIT FOR DEPENDENCIES, Vj: {Vj ?? "-"}, Vk: {Vk ?? "-"}, Qj: {Qj?.Name ?? "-"}, Qk: {Qk?.Name ?? "-"}";

        if (Status==ReservationStationStatus.WaitForFunctionalUnit)
            return $"{Name}, Busy: Yes, Op: {Instruction!.Op}, State: WAIT FOR FU, Vj: {Vj}, Vk: {Vk}, Qj: -, Qk: -";
      
        if (Status ==ReservationStationStatus.ExecutionStarted)
            return $"{Name}, Busy: Yes, Op: {Instruction!.Op}, State: EXECUTE, Vj: {Vj}, Vk: {Vk}, Remaining Time: {LastExecutionTime - CurrentTime}";

        if (Status==ReservationStationStatus.WriteBackStarted) //if(CurrentTime<LastExecutionTime)
            return $"{Name}, Busy: Yes, Op: {Instruction!.Op}, State: WRITEBACK, Vj: {Vj}, Vk: {Vk}, Remaining Time: {WriteBackTime - CurrentTime}";

        return $"{Name}, Status: Unknown";
    }

}
