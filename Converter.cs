using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class MMI_Kethane_Converter : Part
{
    public float ConversionRatio; // How many fuel from one unit of Kethane
    public float ConversionSpeed; // How many Kethane is converted in one second

    protected override void onPartStart()
    {
        this.stackIcon.SetIcon(DefaultIcons.STRUT_CONNECTOR);
        this.stackIcon.SetIconColor(XKCDColors.LightGrassGreen);
        this.stackIconGrouping = StackIconGrouping.SAME_MODULE;
    }
}