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

using System.Collections.Generic;

namespace Kethane
{
public class MMI_Kethane_Tank : Part
{
    [KSPField(guiActive = true, guiName = "Kethane", guiFormat = "#0.##")]
    public float Kethane = 0;

    [KSPField(guiActive = true, guiName = "Capacity", guiFormat = "#0.##")]
    public float Capacity = 100;

    public float DryMass = 0.25f;
    private float KethaneDensity = 0.001f;
    protected VInfoBox info;

    protected override void onPartStart()
    {
        this.stackIcon.SetIcon(DefaultIcons.FUEL_TANK);
        this.stackIcon.SetIconColor(XKCDColors.LightGrassGreen);
        this.stackIconGrouping = StackIconGrouping.SAME_MODULE;

        info = this.stackIcon.DisplayInfo();
        info.SetLength(1.0f);
        info.SetMsgBgColor(XKCDColors.OliveGreen);
        info.SetMsgTextColor(XKCDColors.LightGrassGreen);
        info.SetMessage("Kethane");
        info.SetProgressBarBgColor(XKCDColors.LightGrassGreen);
        info.SetProgressBarColor(XKCDColors.LightGrassGreen);
    }

    protected override void onPartUpdate()
    {
        this.mass = DryMass + Kethane * KethaneDensity;
        info.SetValue(Capacity == 0 ? 0 : Kethane / Capacity, 0, 1);
    }

    public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection)
    {
        partDataCollection.Add("Kethane", new KSPParseable((object)this.Kethane, KSPParseable.Type.FLOAT));
    }

    public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData)
    {
        this.Kethane = float.Parse(parsedData["Kethane"].value);
    }
}
}



