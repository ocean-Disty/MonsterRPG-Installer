if (!isObject(GuiCustomShapeNameMLTextCtrl))
{
    new GuiControlProfile(GuiCustomShapeNameMLTextProfile)
    {
        fontType = "Verdana Bold";
        fontSize = 16;
        fontColor = "255 255 255";
		fontColorHL = "100 100 100 255";
		allowColorChars = 1;
		maxLength = 255;
    };

    new GuiControl(GuiCustomShapeNameMLTextCtrl : GuiMLTextCtrl)
    {
        profile =GuiCustomShapeNameMLTextProfile;
    };
}


function testPushWithoutPackages()
{
    deactivatePackage(GlassNotifications);
    deactivatePackage(GlassAFKCheckPackage);
    deactivatePackage(GlassUpdaterSupportPackage);
    
    canvas.pushDialog(MonsterRPGx_Main);
    
    activatePackage(GlassNotifications);
    activatePackage(GlassAFKCheckPackage);
    activatePackage(GlassUpdaterSupportPackage);
}