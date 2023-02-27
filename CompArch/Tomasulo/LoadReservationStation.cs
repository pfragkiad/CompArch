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


    public override void AddInstruction(Instruction instruction, int issueTime, int issueDuration, int executionDuration, int writebackDuration)
    {
        base.AddInstruction(instruction, issueTime, issueDuration, executionDuration, writebackDuration);

        //LD R1, 0(R2)
        var m = Regex.Match(instruction.Operand2!,
            @"(?<offset>(\+|-)?\d+)\((?<target>\w+)\)");

        SourceRegister = m.Groups["target"].Value;
        Offset = int.Parse(m.Groups["offset"].Value);

        //the following are set only if the RS is ready!
        //IsReady = true;
        StartExecutionTime = issueTime + issueDuration;

        //update the register file!
        Parent.RegisterFile[TargetRegister] = Name;

    }

    public override void ProceedTime()
    {
        base.ProceedTime();

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

        if (CurrentTime < StartExecutionTime)
            return $"{Name}, Busy: Yes, Address: {Address}, State: ISSUE, Remaining Time: {StartExecutionTime - CurrentTime - 1}";

        if (CurrentTime <= LastExecutionTime && CurrentTime >= StartExecutionTime)
            return $"{Name}, Busy: Yes, Address: {Address}, State: EXECUTE, Remaining Time: {LastExecutionTime - CurrentTime}";

        if (CurrentTime <= LastWriteBackTime) //cannot be equal!
            return $"{Name}, Busy: Yes, Address: {Address}, State: WRITEBACK, Remaining Time: {LastWriteBackTime - CurrentTime}";


        //we assume the RS is not busy in any other case
        return $"{Name}, Busy: No";

    }
}
