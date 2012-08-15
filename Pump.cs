using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class MMI_Kethane_Pump : Part
{
    public float PumpingSpeed = 2.0f;

    protected override void onPartStart()
    {
        this.stackIcon.SetIcon(DefaultIcons.SAS);
        this.stackIcon.SetIconColor(XKCDColors.LightGrassGreen);
        this.stackIconGrouping = StackIconGrouping.SAME_MODULE;
    }
}