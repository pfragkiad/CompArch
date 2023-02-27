using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompArch;

public enum BranchPredictionScenario
{
    S2008_2Ai,
    S2008_2Aii,
    S2008_2Aiii,
    Mine1
}


public class BranchPredictor
{
    public void Predict(string scenario)
    {
        scenario = DecodeRlePattern(scenario);
        Console.WriteLine(scenario);

        Console.Write("Static taken: ");
        Console.WriteLine(scenario.Count(c => c == 'N'));

        {
            char state = scenario[0];
            int mispredictions = 0;
            for (int i = 1; i < scenario.Length; i++)
                if (scenario[i] != state)
                {
                    mispredictions++; state = scenario[i];
                }
            Console.Write("1-bit: ");
            Console.WriteLine(mispredictions);
        }

        {
            int state = scenario[0] == 'T' ? 0b11 : 0;
            int mispredictions = 0;
            for (int i = 1; i < scenario.Length; i++)
            {
                bool isTaken = scenario[i] == 'T';
                if ((state == 3 || state == 2) && !isTaken ||
                    (state == 0 || state == 1) && isTaken)
                    mispredictions++;

                if (state < 3 && isTaken) state++;
                else if (state > 0 && !isTaken) state--;
            }
            Console.Write("2-bit: ");
            Console.WriteLine(mispredictions);

        }
    }


    public void Predict(BranchPredictionScenario scenario)
    {
        string s = scenario switch
        {
            BranchPredictionScenario.Mine1 => "4TN6TN7T5NT6NT3N",
            BranchPredictionScenario.S2008_2Ai => "6TN8TN9TN10TN",
            BranchPredictionScenario.S2008_2Aii => "5T8N10T5N7T",
            BranchPredictionScenario.S2008_2Aiii => "4TN6TN7T5NT6NT3N",
            _ => throw new ArgumentOutOfRangeException()
        };

        Predict(s);
    }

}
