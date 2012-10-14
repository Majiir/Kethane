/*
 * Code copyright 2012 by Kulesz
 * This file is part of MMI Kethane Plugin.
 *
 * MMI Kethane Plugin is a free software: you can redistribute it and/or modify it under the 
 * terms of the GNU General Public License as published by the Free Software Foundation, 
 * either version 3 of the License, or (at your option) any later version. MMI Kethane Plugin 
 * is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even 
 * the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU General Public License for more details.
 * 
 * Some elements of this application are inspired or based on code written by members of KSP 
 * community (with respect to the license), especially:
 * 
 * Zoxygene (Life Support) mod        http://kerbalspaceprogram.com/forum/showthread.php/8949-PLUGIN-PART-0-16-Zoxygene-(Life-Support)-mod-v0-6-1-(12-07-28)    
 * ISA MapSat        http://kerbalspaceprogram.com/forum/showthread.php/9396-0-16-ISA-MapSat-Satellite-mapping-module-and-map-generation-tool-v3-1-0
 * Anatid Robotics / MuMech - MechJeb        http://kerbalspaceprogram.com/forum/showthread.php/12384-PLUGIN-PART-0-16-Anatid-Robotics-MuMech-MechJeb-Autopilot-v1-9
*/

namespace Kethane
{
internal class Misc
{
    // From Zoxygene plugin

    public static bool SMatch(string input, string pattern)
    {
        if (input.Length < pattern.Length)
            return false;

        for (int i = 0; i < (pattern.Length - 1); i++)
            if (pattern[i] != input[i])
                return false;

        return true;
    }
}
}