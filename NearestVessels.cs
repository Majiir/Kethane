/* Name: NearestVessels.cs, Nearest vessels list for KSP.
   Version: 1.0.
   Author: Tony Chernogovsky, SPb, Russia.
       mailto: tosh@bk.ru?subject=NearestVessels
   License: BY-SA, Attribution & Share-alike Creative Common Licence. Feel
       free to modify as long as a) original author is mentioned, and b) 
       you distribute your code under the same license. */

// Used by MMI Kethane plugin

using UnityEngine;
using System.Collections.Generic;

namespace Kethane
{
internal class NearestVessels
{
    public List<Vessel> vessels = new List<Vessel>();
    private List<float> distances = new List<float>();

    public void List(Vessel around, float maxRange)
    {
        vessels.Clear();
        distances.Clear();

        Vector3 p = Vector3.zero, p1 = Vector3.zero;

        try
        {
            p = around.findWorldCenterOfMass();
        }
        catch
        {
            //            Debug.Log( "NearestVessels: findWorldCenterOfMass failed for vessel " 
            //                       + around.vesselName );
            return;
        }

        maxRange *= maxRange;

        foreach (Vessel v in FlightGlobals.Vessels)
        {
            if (v == around)
                continue;

            try
            {
                p1 = v.findWorldCenterOfMass();
            }
            catch
            {
                // Sometimes debris become 'invisible' for an unknown reason.
                // findWorldCenterOfMass then fails horribly.
                //                Debug.Log( "NearestVessels: findWorldCenterOfMass failed for vessel " 
                //                           + v.vesselName );
                continue;
            }

            float d = (p1 - p).sqrMagnitude;
            if (d > maxRange)
                continue;

            bool added = false;
            for (int i = 0; i < distances.Count; i++)
                if (d < distances[i])
                {
                    distances.Insert(i, d);
                    vessels.Insert(i, v);

                    added = true;
                    break;
                }

            if (!added)
            {
                distances.Add(d);
                vessels.Add(v);
            }
        }
    }

    public Vessel Next(Vessel v)
    {
        if (vessels.Count <= 0)
            return null;

        int i = vessels.IndexOf(v);
        if ((i < 0) || (i >= vessels.Count - 1))
            i = 0;
        else
            i++;
        return vessels[i];
    }

    public Vessel Find(string vesselName)
    {
        foreach (Vessel v in vessels)
            if (v.vesselName == vesselName)
                return v;
        return null;
    }
}
}
