using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Misc
{
    //Regex.IsMatch is so sloooooow, it lags the game having it..... hence the custom matching tool
    public static bool SMatch(string input, string pattern)
    {
        //this is to avoid oob exception if the input is smaller than the pattern
        if (input.Length < pattern.Length)
            return false;

        for (int i = 0; i < (pattern.Length - 1); i++)
            if (pattern[i] != input[i])
                return false;

        return true;
    }
}