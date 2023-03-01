using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CompArch.Tomasulo;

public class Instruction
{
    public string Op;
    public string? Operand1;
    public string? Operand2;
    public string? Operand3;

    public string? Comment;

    public Instruction(string operation, string? operand1, string? operand2 = null, string? operand3 = null)
    {
        Op = operation;
        Operand1 = operand1;
        Operand2 = operand2;
        Operand3 = operand3;
    }

    public bool UsesRegister(string r)
    {
#pragma warning disable CS8601 // Possible null reference assignment.
        var operands = new string[] { Operand1, Operand2, Operand3 };
#pragma warning restore CS8601 // Possible null reference assignment.
        foreach (var op in operands.Where(o => !string.IsNullOrWhiteSpace(o)))
        {
            //+12(r)
            if (r == op) return true;

            Match m = Regex.Match(op, @"\d+\(\w+\)");
            if (m.Success) return true;
        }
        return false;
    }

    public IEnumerable<string> GetUsedRegisters()
    {
#pragma warning disable CS8601 // Possible null reference assignment.
        var operands = new string[] { Operand1, Operand2, Operand3 };
#pragma warning restore CS8601 // Possible null reference assignment.
        foreach (var op in operands.Where(o => !string.IsNullOrWhiteSpace(o)))
        {
            //+12(r)
            Match m = Regex.Match(op, @"\d+\((?<register>\w+)\)");
            if (m.Success)
                yield return m.Groups["register"].Value;
            else
                //else return it as a whole
                yield return op;
        }
    }

    public Instruction(string command)
    {
        command = command.Trim();

        int space = command.IndexOf(" ");
        if (space == -1)
        {
            Op = command; return;
        }

        Op = command[..space];

        command = command.Substring(space + 1);
        string[] tokens = command.Split(',').Select(t => t.Trim()).ToArray();
        Operand1 = tokens[0];
        if (tokens.Length > 1)
            Operand2 = tokens[1];
        if (tokens.Length > 2)
            Operand3 = tokens[2];
    }

    public string Command
    {
        get =>
        Operand3 is not null ? $"{Op} {Operand1}, {Operand2}, {Operand3}" :
                Operand2 is not null ? $"{Op} {Operand1}, {Operand2}" :
                Operand1 is not null ? $"{Op} {Operand1}" :
                Op;
    }

    public override string ToString()
    {
        string? issue = Issue != IssueEnd ? $"{Issue}-{IssueEnd}" : Issue.ToString();
        string? execute = WriteBack is not null ? $"{Execute}-{WriteBack - 1}" : Execute.ToString();

        StringBuilder sb = new StringBuilder(Command);

        if (Issue is not null) sb.Append($" | IS: {issue}");
        if (Execute is not null) sb.Append($", EX: {execute}");
        if (WriteBack is not null) sb.Append($", WB: {WriteBack}");
        if (Commit is not null) sb.Append($", CO: {Commit}");
        if (Comment is not null) sb.Append($", {Comment}");

        //if (Commit is not null)
        //    return $"{Command} | IS: {Issue}-{IssueEnd}, EX: {Execute}-{WriteBack - 1}, WB: {WriteBack}-{WriteBackEnd}, CO: {Commit}";
        //if (WriteBack is not null)
        //    return $"{Command} | IS: {Issue}-{IssueEnd}, EX: {Execute}-{WriteBack - 1}, WB: {WriteBack}-{WriteBackEnd}";

        //if (Commit is not null)
        //    return $"{Command} | IS: {issue}, EX: {execute}, WB: {WriteBack}, CO: {Commit}";
        //if (WriteBack is not null)
        //    return $"{Command} | IS: {issue}, EX: {execute}, WB: {WriteBack}";

        //if (Execute is not null)
        //    return $"{Command} | IS: {issue}, EX: {Execute}";
        //if (Issue is not null)
        //    return $"{Command} | IS: {issue}";
        //return Command;

        return sb.ToString();
    }

    public int? Issue, IssueEnd, Execute, WriteBack, Commit; //, WriteBackEnd

}

