using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CompArch.Tomasulo;

public class LoadReservationStation : ReservationStation
{
    public LoadReservationStation(int index, Tomasulo parent) : base(index, parent) { }

    public override string CategoryName => "Load";

    public string? TargetRegister => Instruction?.Operand1!;

    public string? SourceRegister { private set; get; }
    public int? Offset { private set; get; }

    public string Address => $"M[{Offset}+{SourceRegister}]";


    public override void IssueInstruction(Instruction instruction, int issueTime, int issueDuration, int executionDuration)
    {
        base.IssueInstruction(instruction, issueTime, issueDuration, executionDuration);

        //LD R1, 0(R2)
        var m = Regex.Match(instruction.Operand2!,
            @"(?<offset>(\+|-)?\d+)\((?<target>\w+)\)");

        SourceRegister = m.Groups["target"].Value;
        Offset = int.Parse(m.Groups["offset"].Value);

        //the following are set only if the RS is ready!
        //IsReady = true;
        //StartExecutionTime = issueTime + issueDuration;

        //update the register file!
        Parent.RegisterFile[TargetRegister] = Name;

    }

    //THIS SHOULD CHANGE
    public override bool ShouldWaitForDependencies => false;

    public override string ToString()
    {
        if (!IsBusy) return $"{Name}, Busy: No";

        if (Status==ReservationStationStatus.IssueStarted)
            return $"{Name}, Busy: Yes, Address: {Address}, State: ISSUE, Remaining Time: {LastIssueTime - CurrentTime}";

        if (Status==ReservationStationStatus.ExecutionStarted)
            return $"{Name}, Busy: Yes, Address: {Address}, State: EXECUTE, Remaining Time: {LastExecutionTime - CurrentTime}";

        if (Status==ReservationStationStatus.WriteBackStarted) //cannot be equal!
            return $"{Name}, Busy: Yes, Address: {Address}, State: WRITEBACK, Remaining Time: {WriteBackTime - CurrentTime}";


        //we assume the RS is not busy in any other case
        return $"{Name}, Status: Unknown";

    }
}
