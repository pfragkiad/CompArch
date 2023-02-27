global using static CompArch.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace CompArch;

public static class Utilities
{
    public static string DecodeRlePattern(string p)
    {

        return Regex.Replace(p, @"(\d+)(T|N)",
            (Match m) =>
            {
                int repetitions = int.Parse(m.Groups[1].Value);
                return new string(m.Groups[2].Value[0], repetitions);
            });

    }

    public static string[] GetTokens(string line,char separator=',') =>
        line.Split(separator).Select(t => t.Trim()).ToArray();
}
