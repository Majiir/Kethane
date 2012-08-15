using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class MMI_Kethane_Tank : Part
{
    [KSPField(guiActive = true, guiName = "Kethane", guiFormat = "#0.##")]
    public float Kethane;
     
    [KSPField(guiActive = true, guiName = "Capacity", guiFormat = "#0.##")]
    public float Capacity;

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
        //if (Kethane >= 0.999f * Capacity || Kethane < 0.001f)
           
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



