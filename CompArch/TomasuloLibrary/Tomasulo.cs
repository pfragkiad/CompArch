namespace CompArch.TomasuloLibrary;
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

    //int _writeBackDuration = 1;
    //public int WriteBackDuration { get => _writeBackDuration; }

    //superscalar settings
    int _issuesPerCycle = 1;
    public int IssuesPerCycle { get => _issuesPerCycle; }

    int _issueDuration = 1;
    public int IssueDuration { get => _issueDuration; }

    int _commitsPerCycle = 1;
    public int CommitsPerCycle { get => _commitsPerCycle; }

    int _writeBacksPerCycle = 1;
    public int WriteBacksPerCycle { get => _writeBacksPerCycle; }


    List<Instruction>? instructions;
    public List<Instruction>? Instructions { get => instructions; }
    //public void AddInstructions(List<Instruction> instructions)
    //{
    //    this.instructions = instructions;
    //}

    Dictionary<string, int>? _executionTimes;
    public Dictionary<string, int>? ExecutionTimes { get => _executionTimes; set => _executionTimes = value; }

    //public void SetCode(string code,
    //    List<string> registers,
    //    Dictionary<string, int> executionTimes,
    //    TomasuloOptions options) =>
    //    SetCode(code, registers, executionTimes, options.IssuesPerCycle, options.IssueDuration, options.WriteBackDuration);

    public void SetCode(
        string code,
        List<string> registers,
        Dictionary<string, int> executionTimes,
        int issuesPerCycle = 1,
        int issueDuration = 1,
        int commitsPerCycle = 1,
        int writeBacksPerCycle = 1)
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
        _commitsPerCycle = commitsPerCycle;
        _writeBacksPerCycle = writeBacksPerCycle;
        // _writeBackDuration = writeBackDuration;



        SetRegisters(registers);

    }
    #endregion


    public int CurrentCycle { get; private set; }

    public Queue<Instruction> InstructionsQueue { get; private set; } = new Queue<Instruction>();

    public void Run()
    {
        if (this.instructions is null || instructions.Count == 0)
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

        while (InstructionsQueue.Any() || AllReservationStations.Any(s => s.IsBusy) || Instructions.Any(i => i.Commit is null))
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
                        availableRs.IssueInstruction(nonIssuedInstruction, CurrentCycle, _issueDuration, _executionTimes![nonIssuedInstruction.Op]);
                    }
                }
                //proceed the cycle until its corresponding RS is ready / else STALL
            } while (instructionsWaitingForRS.Any());

            if (InstructionsQueue.Any())
            {
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
                    availableRs.IssueInstruction(instruction, CurrentCycle, _issueDuration, _executionTimes![instruction.Op]);
                }
            }

            PrintRegistrationStationsAndRegistersStatus();
        }

        PrintSchedule();
    }

    private void PrintSchedule()
    {
        Console.WriteLine("\nSCHEDULE:");
        //at the end print the table!
        int i = 0;
        foreach (Instruction instruction in Instructions!)
            Console.WriteLine($"{++i}. {instruction}");
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
        var allRSs = AllReservationStations.Where(rs => rs.IsBusy);
        foreach (var rs in allRSs)
            Console.WriteLine($"  {rs}");

        Console.WriteLine("REGISTERS:");
        foreach (var entry in RegisterFile.Where(r => r.Value != "-"))
            Console.WriteLine($"  {entry.Key}: {entry.Value}");
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
        if ((instructions?.Count ?? 0) == 0)
            throw new InvalidOperationException("There are no instructions in the code.");

        CurrentCycle++;

        Console.WriteLine($"\nCYCLE: {CurrentCycle}");
        Console.WriteLine("EVENTS:");

        //for each reservation station update the status and time
        //this should run prior to issuing new instructions
        var runningRSs = AllReservationStations.Where(rs => rs.IsBusy).ToList();
        foreach (var rs in runningRSs)
        {
            rs.GotoNextCycle();

            //SOLVE FUNCTIONAL UNITS SELECTION HERE (shouldn't it be addressed in the GotoNextCycle?)
            string? usedFU = rs.AssignedFunctionalUnitsName;
            //this will happen only if the RS writes back on the current cycle
            //solve the conflict here!
            if (!rs.IsBusy
                && !string.IsNullOrWhiteSpace(usedFU) &&
                AvailableFunctionalUnits[usedFU] == 1)
            {
                //get all the RS that wait for the same FU and assign the FU to the earliest one
                var rsWithSameFu = runningRSs
                    .Where(rs2 => rs2.AssignedFunctionalUnitsName == usedFU && rs2.Status == ReservationStationStatus.WaitForFunctionalUnit)
                    .OrderBy(rs2 => rs2.Instruction!.Issue).ToList();

                if (rsWithSameFu.Any())
                {

                    if (rsWithSameFu.Count > 1)
                    {
                        string rsWithSameFuString = string.Join(", ", rsWithSameFu.Select(rs3 => rs3.Name));
                        Console.WriteLine($"  Assigning FU {usedFU} to the oldest of {rsWithSameFuString} => {rsWithSameFu[0].Name}");
                    }

                    rsWithSameFu[0].ForceAssignFU();

                }
            }
        }

        CheckForCommits();
    }

    private void CheckForCommits()
    {
        //the commits are checked at the end of each cycle - 
        Instruction? firstInstructionToCommit = instructions?.FirstOrDefault(i => i.Commit is null && i.WriteBack is not null && CurrentCycle > i.WriteBack);
        if (firstInstructionToCommit is null) return;

        int firstInstructionToCommitIndex = instructions!.IndexOf(firstInstructionToCommit);

        //ALL previous instructions must be committed or else there should be no commits
        if (instructions.Take(firstInstructionToCommitIndex).Any(i => i.Commit is null)) return;

        var candidateInstructionsToCommit = instructions
            .Skip(firstInstructionToCommitIndex)
            //this is the maximum value (it can be less if we are at the end of the list)
            .Take(_commitsPerCycle); 

        //for (int i = firstInstructionToCommitIndex;
        //    i <= Math.Min(firstInstructionToCommitIndex + _commitsPerCycle,instructions.Count) - 1; i++)
        //{
        //var instruction = instructions[i];
        foreach (var instruction in candidateInstructionsToCommit)
        {
            if (instruction.WriteBack is null || instruction.WriteBack >= CurrentCycle) break;

            //can commit now!
            instruction.Commit = CurrentCycle;
            Console.WriteLine($"  Commiting command '{instruction.Command}'...");
        }
    }

    public int LastWBTime { get; private set; }


    public void WriteToCDB(ReservationStation whoWrites) //This is common for both the CalcRS and LoadRS
    {
        string result = GetNextValue();

        Console.WriteLine($"  Writing value to CDB: {whoWrites.Name}->{result}");

        foreach (var affectedRs in AllCalcReservationStations.Where(rs => rs.Qk == whoWrites))
        {
            affectedRs.Vk = result;  //Instruction!.Operand1; //should be the result sth like R[R1]
            affectedRs.Qk = null;
        }

        foreach (var affectedRs in AllCalcReservationStations.Where(rs => rs.Qj == whoWrites))
        {
            affectedRs.Vj = result; //Instruction!.Operand1;
            affectedRs.Qj = null;
        }

        foreach (var entry in RegisterFile)
        {
            if (entry.Value == whoWrites.Name)  //check if there is a value of the rs
                RegisterFile[entry.Key] = result;
        }

        LastWBTime = CurrentCycle;
    }
}
