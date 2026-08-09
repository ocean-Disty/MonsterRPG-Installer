////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////
/////////////////////////SUPPORT FOR STAT BARS//////////////////////////////
////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////


// First, let's define our custom control class
if (!isObject(GuiCustomMLTextCtrl))
{
    new GuiControlProfile(GuiCustomMLTextProfile)
    {
        fontType = "Verdana Bold";
        fontSize = 16;
        fontColor = "255 255 255";
    };

    new GuiControl(GuiCustomMLTextCtrl : GuiMLTextCtrl)
    {
        profile = GuiCustomMLTextProfile;
    };
}

if (!isObject(GuiCustomShapeNameMLTextProfile))
{
    new GuiControlProfile(GuiCustomShapeNameMLTextProfile)
    {
        // NOTE: this one profile renders EVERY shape name - player tags, bot tags and
        // the floating damage numbers alike. Blockland gives no per-name font hook, so
        // setting the damage popups to Georgia Bold Italic necessarily moves nametags
        // onto it too. Revert this line to "Verdana Bold Italic" to undo both.
        fontType = "Georgia Bold Italic";
        fontSize = 18;
        fontColor = "255 255 255";
        fontColorHL = "100 100 100 255";
        allowColorChars = 1;
        maxLength = 255;
        // Add these shadow properties
        fontOutline = true;
        fontOutlineColor = "0 0 0 255";  // Black shadow
        fontOutlineOffset = "1 2";       // Shadow offset
    };
}

if (!isObject(GuiCustomShapeNameMLTextCtrl))
{
    new GuiMLTextCtrl(GuiCustomShapeNameMLTextCtrl)
    {
        profile = GuiCustomShapeNameMLTextProfile;
        // Additional properties for better shadow rendering
        useDropShadow = true;
        shadowColor = "0 0 0 128";  // Semi-transparent black
        shadowOffset = "1 2";       // Same as fontOutlineOffset
    };
}

function setFormattedText(%control, %text)
{
    if (isObject(%control))
    {
        %formattedText = "<color:FFFFFF>" @ %text;
        %control.setText(%formattedText);
    }
    else
    {
        echo("Control not found: " @ %control);
    }
}

//moveMap.bind(keyboard, "rshift", MonsterRPGx_ToggleShiftVal); //right shift //"lshift"
$Pref::Client::CurrentTheme = "Add-Ons/Client_MonsterRPG/Scripts/Client/Support_Themes/THEMES/Theme_BurntGlass_Gold/";

// Remap registration lives in SCRIPTS/Client/Keybinds.cs now (MRPG_regAllRemaps),
// so the whole MonsterRPG set is listed together, in a deliberate order, and is
// REMOVED AGAIN when the player leaves the server. It emits the "MonsterRPGx"
// division header only once - setting $RemapDivision on every entry, as this block
// did, makes OptRemapList::fillList draw a title and a separator rule in front of
// each individual row.
//
// The four commented-out entries below were never registered and their commands
// are not bound anywhere, so they are left as the to-do list they always were:
//   MonsterRPGx_SpellGUI_Toggle    Spells
//   MonsterRPGx_DraftGUI_Toggle    Drafting Table
//   MonsterRPGx_ToggleShiftVal     Quick Transfer (while held)
//   MonsterRPGx_ToggleInspectVal   Inspect Item (while held)

if(!isObject(MonsterRPGx_HUDBase))
{
	new GuiControlProfile(MonsterRPGxGUI_TextProfile)
	{
		fontColor = "0 0 0 255";
		fontType = "Arial";
		fontSize = "14";
		justify = "Left";
		fontColors[1] = "255 0 0 255";
		fontColors[2] = "0 255 0 255";  
		fontColors[3] = "0 0 255 255"; 
		fontColors[4] = "127 0 0 255"; 
		fontColors[5] = "255 255 0 255";
		fontColors[6] = "255 0 255 255";
		fontColorLink = "255 96 96 255";
		fontColorLinkHL = "0 0 255 255";
	};
	MonsterRPGx_HUDFont.setActive(false);
}

if($Pref::Client::MonsterRPGx::HUD_Font $= "")
$Pref::Client::MonsterRPGx::HUD_Font = "arial bold";

if($Pref::Client::MonsterRPGx_HUDImage $= "")
$Pref::Client::MonsterRPGx_HUDImage = "barFull";

if($Pref::Client::MonsterRPGx_HUDImageWrap $= "")
$Pref::Client::MonsterRPGx_HUDImageWrap = true;

if(!isObject(MonsterRPGx_HUDBase)) //Let's not add the gui until we first get into the game.
{
	exec("Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGx_HUDBase.gui");
}

//AGAIN Thank you Pecon, this helps me a lot ;-;


///Thank yous Pecon, Hit up Boss Battles when you get the chance <3


//I don't remember who made this, Port? Well thanks anyway, made some other edits
function rgbToHex(%rgb)
{
	%r = _compToHex(255 * getWord(%rgb,0));
	%g = _compToHex(255 * getWord(%rgb,1));
	%b = _compToHex(255 * getWord(%rgb,2));
	return %r @ %g @ %b;
}

function hexToRgb(%rgb)
{
	%r = _hexToComp(getSubStr(%rgb,0,2)) / 255;
	%g = _hexToComp(getSubStr(%rgb,2,2)) / 255;
	%b = _hexToComp(getSubStr(%rgb,4,2)) / 255;
	return %r SPC %g SPC %b;
}

function _compToHex(%comp)
{
	%left = mFloor(%comp / 16);
	%comp = mFloor(%comp - %left * 16);
	%left = getSubStr("0123456789ABCDEF",%left,1);
	%comp = getSubStr("0123456789ABCDEF",%comp,1);
	return %left @ %comp;
}

function _hexToComp(%hex)
{
	%left = getSubStr(%hex,0);
	%comp = getSubStr(%hex,1);
	%left = striPos("0123456789ABCDEF",%left);
	%comp = striPos("0123456789ABCDEF",%comp);
	if(%left < 0 || %comp < 0)
	return 0;
	return %left * 16 + %comp;
}

function greenToRed(%a)
{
	%r = 1;
	%g = 1;
	if(%a >= (1/2))
	%r = mAbs(%a - 1) * 2;
	if(%a < (1/2))
	%g = %a * 2;
	return %r SPC %g SPC "0";
}

function blueToBlue(%a)
{
	%r = 0.2;
	%g = 0.1;
	if(%a >= (1/2))
	%r = mAbs(%a - 1) * 2;
	if(%a < (1/2))
	%g = %a * 2;
	return %r SPC %g SPC "1";
}



////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////
/////////////////////////GUIFUNCTIONS MonsterRPGx//////////////////////////////////
////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////



function MonsterRPGx_ToggleShiftVal(%toggle)
{
	MonsterRPGx_Main.isHoldingShift = %toggle;
}

function MonsterRPGx_ToggleInspectVal(%toggle)
{
	MonsterRPGx_Main.isHoldingInspect = %toggle;
}

function MonsterRPGx_GUI_Toggle(%toggle)
{
    //A bound key reaches this on EVERY server - the bind lives in moveMap, which
    //knows nothing about who we are connected to. Without this the inventory
    //dialog opens over somebody else's game and fires MonsterRPGx_RequestInvItems
    //at a server with no handler for it.
    if(!MRPG_isActive())
        return;

    echo("DEBUG: Entering MonsterRPGx_GUI_Toggle with toggle: " @ %toggle);
    if(%toggle && isObject(MonsterRPGx_Main))
    {
        echo("DEBUG: MonsterRPGx_Main exists, isAwake: " @ MonsterRPGx_Main.isAwake());
        if(MonsterRPGx_Main.isAwake())
        {
            echo("DEBUG: Attempting to pop dialog");
            canvas.popDialog(MonsterRPGx_Main);
            echo("DEBUG: Dialog popped successfully");
        }
        else
        {
            echo("DEBUG: Attempting to push dialog");
            canvas.pushDialog(MonsterRPGx_Main);
            echo("DEBUG: Dialog pushed successfully");
            echo("DEBUG: Sending command to server");
            commandToServer('MonsterRPGx_RequestInvItems');
            echo("DEBUG: Command sent to server");
        }
    }
    echo("DEBUG: Exiting MonsterRPGx_GUI_Toggle");
}

function MonsterRPGx_SpellGUI_Toggle(%toggle)
{
	if(%toggle && isObject(MonsterRPGx_Spells))
	{
		if(MonsterRPGx_Spells.isAwake())
		{
			MonsterRPGx_Spells.setVisible(%toggle);
		}
	}
}


function MonsterRPGx_ActivateHUD()
{
	canvas.pushDialog(MonsterRPGx_HUD);
	// Build the number-key spell hotbar over the HUD's Spell_Border frames + (re)claim
	// the 1-0/-/= keys for casting (SpellBar.cs). Idempotent - safe to call each time.
	MRPG_buildHudSpellBar();
}

// The server signals HUD activation on spawn via commandToClient('MonsterRPGx_ActivateHUD')
// (Core_Package.cs). Build the spell hotbar here so it exists from spawn without opening
// the menu. HUD itself is pushed elsewhere, so we only add our overlay (idempotent, no push).
function clientCmdMonsterRPGx_ActivateHUD()
{
	MRPG_buildHudSpellBar();
}

function MonsterRPGx_DraftGUI_Toggle(%toggle)
{
	if(%toggle && isObject(MonsterRPGx_RecipeMngmt))
	{
		if(MonsterRPGx_RecipeMngmt.isAwake())
		canvas.popDialog(MonsterRPGx_RecipeMngmt);
		else
		commandToServer('MonsterRPGx_RequestSAbrGUI',"DTblInv");
	}
}

function MonsterRPGx_Main::onSleep(%obj)
{
	commandToServer('MonsterRPGx_closeInv');
}

function MonsterRPGx_RecipeMngmt::onSleep(%obj)
{
	commandToServer('MonsterRPGx_closeInv');
}

function MonsterRPGx_RecipeMngmt::onWake(%obj)
{
	MonsterRPGx_DraftWndw_ItemSelWndw.setVisible(0);
	
	if(!MonsterRPGx_RecipeMngmt.initSetup)
	commandToServer('MonsterRPGx_DraftMenusSetup');
}

if(!isObject(MonsterRPGxScrollProfile))
{
	new GuiControlProfile(MonsterRPGxScrollProfile : ImpactScrollProfile)
	{
		bitmap = "Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGxScroll.png";
	};
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function MonsterRPGx_TransAmFuncs(%action)
{
	switch$(%action)
	{
		case "BtnSingle":
		
		MonsterRPGx_TransAm_Sldr.setValue(1);
		
		MonsterRPGx_TransAmFuncs(BtnTransfer);
		
		case "Btn1-4":
		
		%maxSldr = getWord(MonsterRPGx_TransAm_Sldr.range,1);
		%newVal = mFloor(%maxSldr * 0.25);
		MonsterRPGx_TransAm_Sldr.setValue(%newVal);
		
		MonsterRPGx_TransAmFuncs(BtnTransfer);
		
		case "Btn1-2":
		
		%maxSldr = getWord(MonsterRPGx_TransAm_Sldr.range,1);
		%newVal = mFloor(%maxSldr * 0.5);
		MonsterRPGx_TransAm_Sldr.setValue(%newVal);
		
		MonsterRPGx_TransAmFuncs(BtnTransfer);
		
		case "Btn3-4":
		
		%maxSldr = getWord(MonsterRPGx_TransAm_Sldr.range,1);
		%newVal = mFloor(%maxSldr * 0.75);
		MonsterRPGx_TransAm_Sldr.setValue(%newVal);
		
		MonsterRPGx_TransAmFuncs(BtnTransfer);
		
		case "BtnAll":
		
		%maxSldr = getWord(MonsterRPGx_TransAm_Sldr.range,1);
		MonsterRPGx_TransAm_Sldr.setValue(%maxSldr);
		
		MonsterRPGx_TransAmFuncs(BtnTransfer);
		
		case "UpdateEdt":
		
		%valSldr = MonsterRPGx_TransAm_Sldr.getValue();
		%newVal = mFloatLength(%valSldr,0);
		
		MonsterRPGx_TransAm_Edt.setValue(%newVal);
		MonsterRPGx_TransAm_Sldr.setValue(%newVal);
		
		case "UpdateSldr":
		
		%valEdt = MonsterRPGx_TransAm_Edt.getValue();
		%maxSldr = getWord(MonsterRPGx_TransAm_Sldr.range,1);
		%newVal = mClamp(%valEdt,1,%maxSldr);
		
		MonsterRPGx_TransAm_Edt.setValue(%newVal);
		MonsterRPGx_TransAm_Sldr.setValue(%newVal);
		
		case "BtnTransfer":
		
		%cellInvA = getWord(MonsterRPGx_Main.prevCell,0);
		%cellNumA = getWord(MonsterRPGx_Main.prevCell,1);
		%cellInvB = getWord(MonsterRPGx_Main.currCell,0);
		%cellNumB = getWord(MonsterRPGx_Main.currCell,1);
		
		%sendAm = getMax(MonsterRPGx_TransAm_Sldr.getValue(),1);
		commandToServer('MonsterRPGx_TransAmount',%cellInvA,%cellNumA,%cellInvB,%cellNumB,%sendAm);
		
		canvas.popDialog(MonsterRPGx_Transfer);
		
		//Hide Info and Transfer Windows
		%relCell_info = "MonsterRPGx_" @ %cellInvA @ "_InfoParent_" @ %cellNumA;
		%relCell_info.setVisible(false);
		
		MonsterRPGx_Main.prevCell = "";
		MonsterRPGx_Main.currCell = "";
		MonsterRPGx_Main.prevSelTime = "";
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function MonsterRPGx_LocksmithFuncs(%action,%obj,%data)
{
	%codeLen = mClamp(MonsterRPGx_LockSmInv.securityLevel,0,7);
	
	switch$(%action)
	{
		case "Randomize":
		
		for(%c = 0; %c < %codeLen; %c++)
		{
			%ref = "MonsterRPGx_LockSmInv_Txt" @ %c;
			%ref.setValue("<font:impact:20>" @ getRandom(0,9));
		}
		
		case "Assign":
		
		for(%c = 0; %c < %codeLen; %c++)
		{
			%ref = "MonsterRPGx_LockSmInv_Txt" @ %c;
			%str = setWord(%str,%c,strReplace(%ref.getValue(),"<font:impact:20>",""));
		}
		commandToServer('MonsterRPGx_LockKeyAssign',strReplace(%str," ",""));
		
		case "ModValue":
		
		%currVal = strReplace(%obj.getValue(),"<font:impact:20>","");
		
		if((%newVal = %currVal + %data) < 0)
		%newVal = 9;
		else if(%newVal > 9)
		%newVal = 0;
		
		%obj.setValue("<font:impact:20>" @ %newVal);
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function MonsterRPGx_DraftWndwTabSwitch(%tab)
{
	%visIC = %visGC = %visGF = %visCr = %visSS = %visIS = false;
	%bmpIC = %bmpGC = %bmpGF = %bmpCr = %bmpSS = %bmpIS = "base/client/ui/tab1";
	
	switch$(%tab)
	{
		case "ItemCraft":
		%visIC = true;
		%bmpIC = "Add-Ons/Client_MonsterRPG/GUIs/TabMonsterRPGxUse";
		case "GenCraft":
		%visGC = true;
		%bmpGC = "Add-Ons/Client_MonsterRPG/GUIs/TabMonsterRPGxUse";
		case "GridForm":
		%visGF = true;
		%bmpGF = "Add-Ons/Client_MonsterRPG/GUIs/TabMonsterRPGxUse";
		case "Crucible":
		%visCr = true;
		%bmpCr = "Add-Ons/Client_MonsterRPG/GUIs/TabMonsterRPGxUse";
		case "Server":
		%visSS = true;
		%bmpSS = "Add-Ons/Client_MonsterRPG/GUIs/TabMonsterRPGxUse";
		case "Items":
		%visIS = true;
		%bmpIS = "Add-Ons/Client_MonsterRPG/GUIs/TabMonsterRPGxUse";
	}
	
	MonsterRPGx_DraftWndw_ItemCraft.setVisible(%visIC);
	MonsterRPGx_DraftWndw_GenCraft.setVisible(%visGC);
	MonsterRPGx_DraftWndw_GridForm.setVisible(%visGF);
	MonsterRPGx_DraftWndw_Crucible.setVisible(%visCr);
	MonsterRPGx_DraftWndw_ServerSettings.setVisible(%visSS);
	MonsterRPGx_DraftWndw_ItemSettings.setVisible(%visIS);
	
	MonsterRPGx_DraftTab_ItemCraft.setBitmap(%bmpIC);
	MonsterRPGx_DraftTab_GenCraft.setBitmap(%bmpGC);
	MonsterRPGx_DraftTab_GridForm.setBitmap(%bmpGF);
	MonsterRPGx_DraftTab_Crucible.setBitmap(%bmpCr);
	MonsterRPGx_DraftTab_ServerSettings.setBitmap(%bmpSS);
	MonsterRPGx_DraftTab_ItemSettings.setBitmap(%bmpIS);
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function MonsterRPGx_RecipeSelect(%type)
{
	switch$(%type)
	{
		case "ItemCraft":
		
		%invType = "DTblInvItem";
		%list = MonsterRPGx_DraftRcpList_ItemCraft;
		%mxSlots = 5;
		
		case "GenCraft":
		
		%invType = "DTblInvGen";
		%list = MonsterRPGx_DraftRcpList_GenCraft;
		%mxSlots = 10;
		
		if(strStr(MonsterRPGx_DraftRcpList_GenCraft.getRowTextByID(MonsterRPGx_DraftRcpList_GenCraft.getSelectedID()),"(Shapeless)") > -1)
		MonsterRPGx_DTblInvGen_BoolisShapeless.setValue(1);
		else
		MonsterRPGx_DTblInvGen_BoolisShapeless.setValue(0);
		
		case "GridForm":
		
		%invType = "DTblInvForm";
		%list = MonsterRPGx_DraftRcpList_GridForm;
		%mxSlots = 29;
		
		%rowTxt = %list.recipe[%list.getSelectedID()];
		%inputObj = getWord(%rowTxt,25);
		%inputAm = mClamp(getWord(%rowTxt,26),0,999);
		
		if(isObject(%inputObj) && %inputAm > 0) //if(isObject(%inputSlot.tool)) //%inputSlot = MonsterRPGx_DTblInvForm_ItemIcon_25;
		{
			%inputObjExists = true;
			%gridIcon = %inputObj.MonsterRPGx_GUIGridIcon;
		}
		
		case "Crucible":
		
		%invType = "DTblInvCrbl";
		%list = MonsterRPGx_DraftRcpList_Crucible;
	}
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	
	%recipe = %list.recipe[%list.getSelectedID()];
	
	if(%type !$= "Crucible")
	{
		for(%c = 0; %c < %mxSlots; %c++)
		{
			%relCell_icon = "MonsterRPGx_" @ %invType @ "_ItemIcon_" @ %c;
			%relCell_stackAm = "MonsterRPGx_" @ %invType @ "_InfoTxtStackAm_" @ %c;
			%relCell_stackAmSw = "MonsterRPGx_" @ %invType @ "_InfoSwStackAm_" @ %c;
			
			if(%type $= "GridForm" && %c < 25)
			{
				if(getWord(%recipe,%c) $= "1" && %inputObjExists)
				{
					%relCell_icon.setBitmap(%gridIcon);
					%relCell_icon.tool = 1;
				}
				else
				{
					%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/boolOff");
					%relCell_icon.tool = 0;
				}
			}
			else
			{
				if(%type $= "GridForm")
				%cMod = 25 + ((%c - 25) * 2);
				else
				%cMod = %c * 2;
				if(%type !$= "GridForm" || %c != 27)
				{
					if(isObject(%itemID = getWord(%recipe,%cMod)))
					{
						if(isObject(%relCell_stackAmSw))
						{
							%relCell_stackAmSw.setVisible(true);
							%relCell_stackAm.setText("<font:impact:16><just:right><color:ffffff>" @ getWord(%recipe,%cMod + 1));
						}
						%relCell_icon.tool = %itemID;
						
						if(%itemID.iconName $= "")
						%relCell_icon.setBitmap("Add-Ons/Print_Letters_Default/icons/" @ getSubStr(%itemID.uiName,0,1));
						else
						%relCell_icon.setBitmap(%itemID.iconName);
						if(%itemID.doColorShift)
						%relCell_icon.mColor = getColorI(%itemID.colorShiftColor);
						else
						%relCell_icon.mColor = "255 255 255 255";
					}
					else
					{
						if(%itemID $= "<MissingItem>")
						{
							if(isObject(%relCell_stackAmSw))
							{
								%relCell_stackAmSw.setVisible(true);
								%relCell_stackAm.setText("<font:impact:16><just:right><color:ffffff>" @ getWord(%recipe,%cMod + 1));
							}
							%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/MissingItem");
							%relCell_icon.tool = %itemID;
						}
						else
						{
							if(isObject(%relCell_stackAmSw))
							%relCell_stackAmSw.setVisible(false);
							%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/" @ %relCell_icon.MonsterRPGx_BtnImg);
							%relCell_icon.tool = "";
						}
						%relCell_icon.mColor = "255 255 255 255";
					}
				}
			}
		}
	}
	
	//////////////////////////////////////////////////
	
	else
	{
		for(%c = 0; %c < 4; %c++)
		{
			%menuA = "MonsterRPGx_DraftWndw_AlSel" @ %c;
			%menuR = "MonsterRPGx_DraftWndw_RtSel" @ %c;
			%sw = "MonsterRPGx_DraftWndw_AlSw" @ %c;
			
			%menuA.setSelected(MonsterRPGx_DraftWndw_Crucible.metal[getWord(%recipe,%c * 2)]);
			%menuR.setSelected(getWord(%recipe,(%c * 2) + 1));
			
			if(%c < 2)
			{
				%relCell_icon = "MonsterRPGx_" @ %invType @ "_ItemIcon_" @ %c;
				%relCell_stackAm = "MonsterRPGx_" @ %invType @ "_InfoTxtStackAm_" @ %c;
				%relCell_stackAmSw = "MonsterRPGx_" @ %invType @ "_InfoSwStackAm_" @ %c;
				
				if(isObject(%itemID = getWord(%recipe,8 + (%c * 2))))
				{
					if(isObject(%relCell_stackAmSw))
					{
						%relCell_stackAmSw.setVisible(true);
						%relCell_stackAm.setText("<font:impact:16><just:right><color:ffffff>" @ getWord(%recipe,%cMod + 1));
					}
					%relCell_icon.tool = %itemID;
					
					if(%itemID.iconName $= "")
					%relCell_icon.setBitmap("Add-Ons/Print_Letters_Default/icons/" @ getSubStr(%itemID.uiName,0,1));
					else
					%relCell_icon.setBitmap(%itemID.iconName);
					if(%itemID.doColorShift)
					%relCell_icon.mColor = getColorI(%itemID.colorShiftColor);
					else
					%relCell_icon.mColor = "255 255 255 255";
				}
				else
				{
					if(isObject(%relCell_stackAmSw))
					%relCell_stackAmSw.setVisible(false);
					%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/" @ %relCell_icon.MonsterRPGx_BtnImg);
					%relCell_icon.tool = "";
					%relCell_icon.mColor = "255 255 255 255";
				}
			}
		}
		//MonsterRPGx_DraftWndw_AlSelOutput.setText(%recipe,8);
		MonsterRPGx_DraftBtnFuncs("UpdateGraph","");
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function MonsterRPGx_DraftBtnFuncs(%func,%type)
{
	switch$(%func)
	{
		case "Reset":
		
		switch$(%type)
		{
			case "ItemCraft":
			%invType = "DTblInvItem";
			%mxSlots = 5;
			case "GenCraft":
			MonsterRPGx_DTblInvGen_BoolisShapeless.setValue(0);
			%invType = "DTblInvGen";
			%mxSlots = 10;
			case "GridForm":
			%invType = "DTblInvForm";
			%mxSlots = 29;
			case "Crucible":
			%invType = "DTblInvCrbl";
			%mxSlots = 2;
			default:
			return;
		}			
		
		for(%c = 0; %c < %mxSlots; %c++)
		{
			%relCell_icon = "MonsterRPGx_" @ %invType @ "_ItemIcon_" @ %c;
			%relCell_stackAmSw = "MonsterRPGx_" @ %invType @ "_InfoSwStackAm_" @ %c;
			
			if(isObject(%relCell_stackAmSw))
			%relCell_stackAmSw.setVisible(false);
			if(isObject(%relCell_icon))
			{
				%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/" @ %relCell_icon.MonsterRPGx_BtnImg);
				%relCell_icon.tool = "";
				%relCell_icon.mColor = "255 255 255 255";
			}
		}
		if(%type $= "Crucible")
		{
			for(%c = 0; %c < 4; %c++)
			{
				%menuA = "MonsterRPGx_DraftWndw_AlSel" @ %c;
				%menuR = "MonsterRPGx_DraftWndw_RtSel" @ %c;
				%sw = "MonsterRPGx_DraftWndw_AlSw" @ %c;
				
				%menuA.setSelected(0);
				%menuR.setSelected(0);
				%sw.setColor(getField("27 57 130 220" TAB "47 77 150 220" TAB "67 87 170 220" TAB "87 107 180 220",%c));
				%sw.resize(getWord("0 32 64 96",%c),127,32,1);
			}
			//MonsterRPGx_DraftWndw_AlSelOutput.setText("-");
		}
		
		//////////////////////////////////////////////////
		
		case "Delete" or "Create":
		
		switch$(%type)
		{
			case "ItemCraft":
			%list = MonsterRPGx_DraftRcpList_ItemCraft;
			%invType = "DTblInvItem";
			%mxSlots = 5;
			%bStart = 0;
			case "GenCraft":
			%list = MonsterRPGx_DraftRcpList_GenCraft;
			%invType = "DTblInvGen";
			%mxSlots = 10;
			%bStart = 0;
			case "GridForm":
			%list = MonsterRPGx_DraftRcpList_GridForm;
			%invType = "DTblInvForm";
			%mxSlots = 29;
			%bStart = 25;
			case "Crucible":
			%list = MonsterRPGx_DraftRcpList_Crucible;
			%invType = "DTblInvCrbl";
			default:
			return;
		}
		
		if(%func $= "Delete")
		{
			if(%list.getSelectedID() == -1)
			{
				CLIENTCMDMessageBoxOkBG("MonsterRPGx : No Selection","No recipe was selected; recipe deletion failed.");
				return;
			}
			if(%type $= "GenCraft" && strStr(MonsterRPGx_DraftRcpList_GenCraft.getRowTextByID(MonsterRPGx_DraftRcpList_GenCraft.getSelectedID()),"(Shapeless)") > -1)
			%shapeless = true;
			
			commandToServer('MonsterRPGx_ManageRecipe',"Delete",%type,%list.getSelectedID() TAB %shapeless);
		}
		else
		{
			switch$(%type)
			{
				case "Crucible":
				
				for(%c = 0; %c < 4; %c++)
				{
					%menuA = "MonsterRPGx_DraftWndw_AlSel" @ %c;
					%menuR = "MonsterRPGx_DraftWndw_RtSel" @ %c;
					
					%recipe = setWord(%recipe,%c * 2,%menuA.getSelected());
					%recipe = setWord(%recipe,(%c * 2) + 1,%menuR.getSelected());
				}
				for(%c = 0; %c < 2; %c++)
				{
					%relCell_icon = "MonsterRPGx_DTblInvCrbl_ItemIcon_" @ %c;
					%relCell_stackAm = "MonsterRPGx_DTblInvCrbl_InfoTxtStackAm_" @ %c;
					%itemID = %relCell_icon.tool;
					
					%recipe = setWord(%recipe,(%c * 2) + 8,%itemID);
					%recipe = setWord(%recipe,(%c * 2) + 8 + 1,strReplace(%relCell_stackAm.getValue(),"<font:impact:16><just:right><color:ffffff>",""));
				}
				commandToServer('MonsterRPGx_ManageRecipe',"CreateCrbl",%type,%recipe);
				
				case "ItemCraft" or "GenCraft" or "GridForm":
				
				if(%type $= "GenCraft" && MonsterRPGx_DTblInvGen_BoolisShapeless.getValue())
				%recType = "CreateShapeless";
				else
				%recType = "Create";
				
				for(%b = 0; %b < %bStart; %b++) //grid forming!!!
				{
					%relCell_icon = "MonsterRPGx_" @ %invType @ "_ItemIcon_" @ %b;
					
					%bool = %relCell_icon.tool;
					%recipe = setWord(%recipe,%b,%bool);
				}
				for(%c = %bStart; %c < %mxSlots; %c++)
				{
					%relCell_icon = "MonsterRPGx_" @ %invType @ "_ItemIcon_" @ %c;
					%itemID = %relCell_icon.tool;
					
					if(%type !$= "GridForm" || %c != 27)
					{
						if(%itemID == 0) //(%type $= "GridForm" && %c < %cStart) || 
						%stackAm = "";
						else
						{
							%relCell_stackAm = "MonsterRPGx_" @ %invType @ "_InfoTxtStackAm_" @ %c; //server will set stackAm to "0" if item doesn't exist
							%stackAm = strReplace(%relCell_stackAm.getText(),"<font:impact:16><just:right><color:ffffff>","");
						}
						
						%cMod = %bStart + ((%c - %bStart) * 2);
						%recipe = setWord(%recipe,%cMod,%itemID);
						%recipe = setWord(%recipe,%cMod + 1,%stackAm);
					}
				}
				commandToServer('MonsterRPGx_ManageRecipe',%recType,%type,%recipe); //commandToServer('',getSubStr(%recipe,1,strLen(%recipe) - 1),%type);
			}
		}
		
		//////////////////////////////////////////////////
		
		case "UpdateGraph":
		
		for(%c = 0; %c < 4; %c++)
		{
			%menuA = "MonsterRPGx_DraftWndw_AlSel" @ %c;
			%menuR = "MonsterRPGx_DraftWndw_RtSel" @ %c;
			
			if(%menuR.getSelected() > 0 && %menuA.getSelected() > 0) //if((%tmpV = %menuR.getValue()) !$= "" && %tmpV !$= "-")
			{
				%menuRV = mClamp(%menuR.getSelected(),0,MonsterRPGxPar_CrblInv.lqdMoldMax);
				%maxR += %menuRV;
			}
		}			
		for(%c = 0; %c < 4; %c++)
		{
			%menuA = "MonsterRPGx_DraftWndw_AlSel" @ %c;
			%menuR = "MonsterRPGx_DraftWndw_RtSel" @ %c;
			%sw = "MonsterRPGx_DraftWndw_AlSw" @ %c;
			
			if(%menuA.getSelected() > 0)
			%menuRV = mClamp(%menuR.getSelected(),0,MonsterRPGxPar_CrblInv.lqdMoldMax);
			else
			%menuRV = 0;
			%relSize = mFloatLength((%menuRV / %maxR) * 128,0);
			%sw.position = getWord("0 32 64 96",%c) SPC mClamp(128 - %relSize,0,127);
			%sw.extent = 32 SPC mClamp(%relSize,1,128);
			
			if(%maxR <= MonsterRPGxPar_CrblInv.lqdMoldMax)
			%sw.setColor(getField("27 57 130 220" TAB "47 77 150 220" TAB "67 87 170 220" TAB "87 107 180 220",%c));
			else
			%sw.setColor("220 0 0 220");
		}
		
		//////////////////////////////////////////////////
		
		case "UploadSettings":
		
		if(MonsterRPGx_DtblConfirm.type $= "ServerSettings")
		{
			%strMain = MonsterRPGx_DTbl_EnabEnc.getValue() TAB
			MonsterRPGx_DTbl_EnabDurab.getValue() TAB
			MonsterRPGx_DTbl_DgrdUse.getValue() TAB
			MonsterRPGx_DTbl_DgrdTime.getValue() TAB
			MonsterRPGx_DTbl_EnabStack.getValue() TAB
			MonsterRPGx_DTbl_EnabItemStor.getValue() TAB
			MonsterRPGx_DTbl_EnabBrStor.getValue() TAB
			MonsterRPGx_DTbl_EnabVehStor.getValue() TAB
			MonsterRPGx_DTbl_EnabHunger.getValue() TAB
			MonsterRPGx_DTbl_EnabThirst.getValue() TAB
			MonsterRPGx_DTbl_EnabModding.getValue() TAB
			MonsterRPGx_DTbl_EnabAutoSave.getValue() TAB
			MonsterRPGx_DTbl_MenuPerCraft.getSelected();
			
			%strInDepth = MonsterRPGx_DTbl_EncStart.getValue() TAB
			MonsterRPGx_DTbl_EncMax.getValue() TAB
			MonsterRPGx_DTbl_WghtSymb.getValue() TAB
			MonsterRPGx_DTbl_MaxDist.getValue() TAB
			MonsterRPGx_DTbl_dropCntrs.getValue() TAB
			MonsterRPGx_DTbl_invQPlyr.getValue() TAB
			MonsterRPGx_DTbl_invQSrvr.getValue() TAB
			MonsterRPGx_DTbl_hmrDest.getValue() TAB
			MonsterRPGx_DTbl_wndDest.getValue() TAB 
			MonsterRPGx_DTbl_redHungerAm.getValue() TAB 
			MonsterRPGx_DTbl_redThirstAm.getValue() TAB
			strReplace(MonsterRPGx_DTbl_oreFreq.getValue(),"%","") TAB
			strReplace(MonsterRPGx_DTbl_gemFreq.getValue(),"%","") TAB
			MonsterRPGx_DTbl_bowSpearAmmo.getValue();
			
			%strSched = MonsterRPGx_DTbl_invSaveSched.getValue() TAB
			MonsterRPGx_DTbl_ShopUpdateShed.getValue() TAB
			//MonsterRPGx_DTbl_moneyUpdateSched.getValue() TAB
			MonsterRPGx_DTbl_LootUpdateSched.getValue() TAB
			//MonsterRPGx_DTbl_PlantUpdateSched.getValue() TAB
			MonsterRPGx_DTbl_HungerUpdateSched.getValue() TAB
			MonsterRPGx_DTbl_ThirstUpdateSched.getValue() TAB
			MonsterRPGx_DTbl_DgrdSched.getValue();
			
			%strDefV = MonsterRPGx_DTbl_defStckLmt.getValue() TAB
			MonsterRPGx_DTbl_defWght.getValue() TAB
			MonsterRPGx_DTbl_defDurab.getValue() TAB
			MonsterRPGx_DTbl_defDgrdUse.getValue() TAB
			MonsterRPGx_DTbl_defDgrdTime.getValue() TAB
			MonsterRPGx_DTbl_defEff.getValue() TAB
			MonsterRPGx_DTbl_brnRate.getValue();
			
			commandToServer('MonsterRPGx_AqsApplyDraftData',"ServerSettings-Receive",%strMain,%strInDepth,%strSched,%strDefV);
			MonsterRPGx_DtblConfirm.setVisible(0);
		}
		else
		{
			if((%rCount = MonsterRPGx_DraftRcpList_ItemSettings.rowCount()) > 0)
			{
				for(%c = 0; %c < %rCount; %c++)
				{
					%rowText = MonsterRPGx_DraftRcpList_ItemSettings.getRowText(%c);
					%itemData = getFields(%rowText,1,8) TAB getFields(%rowText,11,13);
					%itemID = MonsterRPGx_DraftRcpList_ItemSettings.getRowID(%c);
					
					commandToServer('MonsterRPGx_AqsApplyDraftData',"ItemSettings-Receive",%itemData,%itemID,"",""); 
				}
				
				commandToServer('MonsterRPGx_AqsApplyDraftData',"ItemSettings-RcvFinal","","","","");
				MonsterRPGx_DtblConfirm.setVisible(0);
			}
			else
			messageBoxOk("MonsterRPGx : No Item Data","No item data is present; saving of item settings failed. Make sure to click the \"reload\" button first to list items and their MonsterRPGx values.");
		}
		
		//////////////////////////////////////////////////
		
		case "ModItemSettings":
		
		if((%id = MonsterRPGx_DraftRcpList_ItemSettings.getSelectedID()) > -1)
		{
			MonsterRPGx_DtblItemSetMod.setVisible(1);
			MonsterRPGx_DtblItemSetMod.itemID = %id;
			%itemData = MonsterRPGx_DraftRcpList_ItemSettings.getRowTextByID(%id);
			
			if(%id.iconName $= "")
			{
				//if(%id.uiName $= "")
				//	MonsterRPGx_Dtbl_MISitemicon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/NoItem"); //if no iconName and no uiName
				//else
				//{
				%lRef = getSubStr(%id.uiName,0,1);
				MonsterRPGx_Dtbl_MISitemicon.setBitmap("Add-Ons/Print_Letters_Default/icons/" @ %lRef);
				//}
			}
			else
			MonsterRPGx_Dtbl_MISitemicon.setBitmap(%id.iconName);
			if(%id.doColorShift)
			MonsterRPGx_Dtbl_MISitemicon.mColor = getColorI(%id.colorShiftColor);
			else
			MonsterRPGx_Dtbl_MISitemicon.mColor = "255 255 255 255";
			
			MonsterRPGx_Dtbl_MISstcklm.setValue(getField(%itemData,1));
			MonsterRPGx_Dtbl_MISwght.setValue(getField(%itemData,2));
			MonsterRPGx_Dtbl_MISdurab.setValue(getField(%itemData,3));
			MonsterRPGx_Dtbl_MISdgrdUse.setValue(getField(%itemData,4));
			MonsterRPGx_Dtbl_MISdgrdTime.setValue(getField(%itemData,5));
			MonsterRPGx_Dtbl_MISeffc.setValue(getField(%itemData,6));
			MonsterRPGx_Dtbl_MISisFuel.setValue(getField(%itemData,7));
			MonsterRPGx_Dtbl_MISbrnRate.setValue(getField(%itemData,8));
			MonsterRPGx_Dtbl_MISfrncRslt.setSelected(getField(%itemData,12)); //skip 9 and access itemID data instead of uiName
			MonsterRPGx_Dtbl_MIScrblRslt.setSelected(getField(%itemData,13)); //skip 10 and access metal id data instead of string name
			MonsterRPGx_Dtbl_MISisStatic.setValue(getField(%itemData,11));
		}
		
		//////////////////////////////////////////////////
		
		case "ApplyItemSetMod":
		
		%itemID = MonsterRPGx_DtblItemSetMod.itemID;
		
		%itemData = %itemID.uiName TAB
		MonsterRPGx_Dtbl_MISstcklm.getValue() TAB
		MonsterRPGx_Dtbl_MISwght.getValue() TAB
		MonsterRPGx_Dtbl_MISdurab.getValue() TAB
		MonsterRPGx_Dtbl_MISdgrdUse.getValue() TAB
		MonsterRPGx_Dtbl_MISdgrdTime.getValue() TAB
		MonsterRPGx_Dtbl_MISeffc.getValue() TAB
		MonsterRPGx_Dtbl_MISisFuel.getValue() TAB
		MonsterRPGx_Dtbl_MISbrnRate.getValue() TAB
		MonsterRPGx_Dtbl_MISfrncRslt.getValue() TAB
		MonsterRPGx_Dtbl_MIScrblRslt.getValue() TAB
		MonsterRPGx_Dtbl_MISisStatic.getValue() TAB
		MonsterRPGx_Dtbl_MISfrncRslt.getSelected() TAB //unseen itemID data
		MonsterRPGx_Dtbl_MIScrblRslt.getSelected(); //unseen metal id data
		
		MonsterRPGx_DraftRcpList_ItemSettings.setRowByID(%itemID,%itemData);
		MonsterRPGx_DtblItemSetMod.setVisible(0);
		MonsterRPGx_DtblItemSetMod.itemID = "";
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function MonsterRPGx_ItemSelectFuncs(%type,%action,%rtnBtn)
{
	if(%type $= "")
	%type = MonsterRPGx_DraftWndw_ItemSelWndw.menuType;
	
	switch$(%type)
	{
		case "Item":
		%menu = MonsterRPGx_DraftWndw_ItemSelItemsMenu;
		case "Mold":
		%menu = MonsterRPGx_DraftWndw_ItemSelMoldsMenu;
		case "Material":
		%menu = MonsterRPGx_DraftWndw_ItemSelMatsMenu;
		case "BoolGrid":
		
		default:
		return;
	}		
	switch$(%action)
	{
		case "Open":
		
		if((%ItemID = %menu.getSelected()) > 0)
		{
			%bmp = MonsterRPGx_DraftWndw_ItemSelIcon;
			
			if(%itemID.iconName $= "")
			{
				%lRef = getSubStr(%itemID.uiName,0,1);
				%bmp.setBitmap(%itemID.iconName);
			}
			else
			%bmp.setBitmap(%itemID.iconName);
			
			if(%itemID.doColorShift)
			%bmp.mColor = getColorI(%itemID.colorShiftColor);
			else
			%bmp.mColor = "255 255 255 255";
		}
		else
		{
			MonsterRPGx_DraftWndw_ItemSelIcon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/noItem");
			MonsterRPGx_DraftWndw_ItemSelIcon.mColor = "255 255 255 255";
		}
		//MonsterRPGx_DraftWndw_ItemSelAmEdit.setValue(1);
		//%menu.setSelected(0);
		
		MonsterRPGx_DraftWndw_ItemSelWndw.setVisible(1);
		MonsterRPGx_DraftWndw_ItemSelWndw.rtnBtn = %rtnBtn;
		MonsterRPGx_DraftWndw_ItemSelWndw.menuType = %type;
		
		MonsterRPGx_DraftWndw_ItemSelItemsMenu.setVisible(0);
		MonsterRPGx_DraftWndw_ItemSelMoldsMenu.setVisible(0);
		MonsterRPGx_DraftWndw_ItemSelMatsMenu.setVisible(0);
		
		%menu.setVisible(1);
		//%menu.setText("-");
		
		//////////////////////////////////////////////////
		
		case "Choose":
		
		%bmp = MonsterRPGx_DraftWndw_ItemSelIcon;
		
		if((%itemID = %menu.getSelected()) > 0)
		{
			if(%itemID.iconName $= "")
			{
				%lRef = getSubStr(%itemID.uiName,0,1);
				%bmp.setBitmap("Add-Ons/Print_Letters_Default/icons/" @ %lRef);
			}
			else
			%bmp.setBitmap(%itemID.iconName);
			
			if(%itemID.doColorShift)
			%bmp.mColor = getColorI(%itemID.colorShiftColor);
			else
			%bmp.mColor = "255 255 255 255";
		}
		else
		%bmp.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/NoItem");
		
		//////////////////////////////////////////////////
		
		case "Select":
		
		%tmpBtn = MonsterRPGx_DraftWndw_ItemSelWndw.rtnBtn;
		%relCell_icon = strReplace(%tmpBtn,"ItemButton","ItemIcon");
		%relCell_stackAm = strReplace(%tmpBtn,"ItemButton","InfoTxtStackAm");
		%relCell_stackAmSw = strReplace(%tmpBtn,"ItemButton","InfoSwStackAm");
		
		%tmpBtnB = strReplace(%tmpBtn,"_"," ");
		%invType = getWord(%tmpBtnB,1);
		%cellNum = getWord(%tmpBtnB,3);
		
		MonsterRPGx_DraftWndw_ItemSelWndw.rtnBtn = "";
		MonsterRPGx_DraftWndw_ItemSelWndw.menuType = "";
		MonsterRPGx_DraftWndw_ItemSelWndw.setVisible(0);
		
		if(isObject(%itemID = %menu.getSelected()))
		{
			if(%invType !$= "DTblInvForm" || %cellNum >= 25)
			{
				if(%invType $= "DTblInvForm" && %cellNum == 25)
				{
					%gridIcon = %itemID.MonsterRPGx_GUIGridIcon;
					
					for(%c = 0; %c < 25; %c++)
					{
						%relGridCell_icon = "MonsterRPGx_DTblInvForm_ItemIcon_" @ %c;
						%relGridCell_icon.setBitmap(%gridIcon);
						%relGridCell_icon.tool = 1;
					}
				}
				if(isObject(%relCell_stackAmSw))
				{
					%stackAm = mClamp(MonsterRPGx_DraftWndw_ItemSelAmEdit.getValue(),1,999);
					%relCell_stackAmSw.setVisible(true);
					%relCell_stackAm.setText("<font:impact:16><just:right><color:ffffff>" @ %stackAm);
				}
			}
			%relCell_icon.tool = %itemID;
			
			if(%itemID.iconName $= "")
			%relCell_icon.setBitmap("Add-Ons/Print_Letters_Default/icons/" @ getSubStr(%itemID.uiName,0,1));
			else
			%relCell_icon.setBitmap(%itemID.iconName);
			if(%itemID.doColorShift)
			%relCell_icon.mColor = getColorI(%itemID.colorShiftColor);
			else
			%relCell_icon.mColor = "255 255 255 255";
		}
		else
		{
			if(%invType $= "DTblInvForm" && %cellNum == 25)
			{
				for(%c = 0; %c < 25; %c++)
				{
					%relGridCell_icon = "MonsterRPGx_DTblInvForm_ItemIcon_" @ %c;
					%relGridCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/boolOff");
					%relGridCell_icon.tool = 0;
				}
			}
			
			if(isObject(%relCell_stackAmSw))
			%relCell_stackAmSw.setVisible(false);
			%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/" @ %relCell_icon.MonsterRPGx_BtnImg);
			%relCell_icon.tool = "";
			%relCell_icon.mColor = "255 255 255 255";
		}
		
		//////////////////////////////////////////////////
		
		case "GridToggle":
		
		%inputSlot = MonsterRPGx_DTblInvForm_ItemIcon_25;
		%relCell_icon = strReplace(%rtnBtn,"ItemButton","ItemIcon");
		
		if(!isObject(%inputSlot.tool))
		{
			CLIENTCMDMessageBoxOkBG("MonsterRPGx : No Material Item","Please choose an item in the Grid Forming material slot first, before toggling cells in the grid.");
			return;
		}
		else
		{
			if(%relCell_icon.tool == 0)
			{
				%relCell_icon.tool = 1;
				%relCell_icon.setBitmap(%inputSlot.tool.MonsterRPGx_GUIGridIcon);
			}
			else
			{
				%relCell_icon.tool = 0;
				%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/" @ %relCell_icon.MonsterRPGx_BtnImg);
			}
		}
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function MonsterRPGx_shopFuncs(%action)
{
	switch$(%action)
	{
		case "Buy":
		%list = MonsterRPGx_Shop_listBuy;
		%itemAm = MonsterRPGx_Shop_amBuy;
		case "Sell":
		%list = MonsterRPGx_Shop_listSell;
		%itemAm = MonsterRPGx_Shop_amSell;
		case "BuySelect" or "SellSelect":
		
		if(%action $= "BuySelect")
		{
			%list = MonsterRPGx_Shop_listBuy;
			%txt = MonsterRPGx_Shop_nameBuy;
			%icon = MonsterRPGx_Shop_iconBuy;
		}
		else
		{
			%list = MonsterRPGx_Shop_listSell;
			%txt = MonsterRPGx_Shop_nameSell;
			%icon = MonsterRPGx_Shop_iconSell;
		}
		
		%itemID = %list.getSelectedID();
		if(%itemID.iconName $= "")
		{
			%lRef = getSubStr(%itemID.uiName,0,1);
			%icon.setBitmap("Add-Ons/Print_Letters_Default/icons/" @ %lRef);
		}
		else
		%icon.setBitmap(%itemID.iconName);
		if(%itemID.doColorShift)
		%icon.mColor = getColorI(%itemID.colorShiftColor);
		else
		%icon.mColor = "255 255 255 255";
		
		%txt.setText("<font:impact:20>" @ getSubStr(%itemID.uiName,0,22));
		return;
		
		default:
		return;
	}
	if((%id = %list.getSelectedID()) > -1)
	{
		%txt = %list.getRowTextByID(%id);
		commandToServer('MonsterRPGx_shopBuySell',%action,%id,mClamp(%itemAm.getSelected(),1,999));
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function MonsterRPGx_DraftSvrSetMsg(%type)
{
	switch$(%type)
	{
		case "Enc":
		%msg = "The Encumbrance feature causes players to gradually slow down (and eventually be unable to move) as they carry more and more items in their inventory; each item is given a specific weight value.";
		case "Durab":
		%msg = "Durability gives each item a certain amount of \"health\", which is reduced either when items are used as tools or gradually over time.";
		case "DgrdUse":
		%msg = "This option will cause items to degrade by a random amount when used as a physical tool item, or when used as a tool in some of the GUIs. Note: Durability itself must also be enabled.";
		case "DgrdTime":
		%msg = "This option will cause items to gradually degrade over time, based on the timed-degrade update tick.  Note: Durability itself must also be enabled.";
		case "ItemStck":
		%msg = "Toggle the option to allowing stacking of items in cells.";
		case "ItemStor":
		%msg = "Toggle ability for players to access backpack or container inventories (some can still be viewed in some cases).";
		case "BrStor":
		%msg = "Toggle ability for players to access brick inventories (some events will still work if disabled).";
		case "VehStor":
		%msg = "Toggle ability for players to access vehicle inventories (some events will still work if disabled).";
		case "Hngr":
		%msg = "Toggle the Hunger feature - over time, players will have their hunger level reduced, and will need to eat any of the various food items to stay alive.";
		case "Thrst":
		%msg = "Toggle the Thirst feature - over time, players will have their thirst level reduced, and will need to obtain / drink water from any of the water-related items to stay alive.";
		case "ItemMod":
		%msg = "Toggle item modding - if disabled, players won't be able to apply modifications to items via the anvil brick.";
		case "AutoSave":
		%msg = "Enable / disable periodic saving of all item, brick and vehicle inventories.";
		case "PerCraft":
		%msg = "Four different options relative to the personal crafting window; if you select anything other than \"Disabled\", all players will be able to craft without having to use a crafting table.";
		
		
		case "EncStart":
		%msg = "The weight at which (when exceeded) players begin to slow down.<br><br>Range: 0.00 to 999.0";
		case "EncMax":
		%msg = "The weight at which (when exceeded) players will be unable to move.<br><br>Range: 0.00 to 999.0";
		case "WghtSymb":
		%msg = "Text symbol used in player GUIs to signify item-weight.<br><br>Range: 1 to 3 characters long.";
		case "VehDist":
		%msg = "Distance at which players are able to access and make changes to a brick's / vehicle's inventory.<br><br>Range: 0.00 to 100.00 meters";
		case "DropBkpk":
		%msg = "Toggle dropping a player's backpack or any containers they're carrying on death.";
		case "PlyrQta":
		%msg = "Max amount of inventories (items, bricks, etc.) players can have.<br><br>Range: 0 to 999";
		case "SrvrQta":
		%msg = "Max amount of inventories (items, bricks, etc.) that can be present per server instance.<br><br>Range: 0 to 999";
		case "HmrDest":
		%msg = "Allow / disable destruction of any MonsterRPGx brick via the hammer tool.";
		case "WndDest":
		%msg = "Allow / disable destruction of any MonsterRPGx brick via the wand tool.";
		case "RedHngr":
		%msg = "Max amount to reduce a player's hunger per tick, if hunger is enabled; amounts are randomized.<br><br>Range: 0.00 to 99.00";
		case "RedThrst":
		%msg = "Max amount to reduce a player's thirst per tick, if thirst is enabled; amounts are randomized.<br><br>Range: 0.00 to 99.00";
		case "OreFreq":
		%msg = "How often to generate ore bricks when players are digging.<br><br>Range: 0 to 100 percent";
		case "GemFreq":
		%msg = "How often to generate gem bricks when players are digging (also depends on ore frequency).<br><br>Range: 0 to 100 percent";
		case "bwSprAmmo":
		%msg = "If enabled, the default bow weapon will require MonsterRPGx arrow items to fire, and the spear will only fire based on the amount of spears in the stack.";
		
		
		case "SaveSchd":
		%msg = "How often to save item, brick and vehicle inventories.<br><br>Range: 1000 to 3600000 milliseconds(ms) (1000ms = 1 second)";
		case "ShopSchd":
		%msg = "How often to update a shop's inventory and the shopkeeper's money.<br><br>Range: 1000 to 3600000 milliseconds(ms) (1000ms = 1 second)";
		case "LootSchd":
		%msg = "How often to randomize items within loot bricks.<br><br>Range: 1000 to 3600000 milliseconds(ms) (1000ms = 1 second)";
		//case "PlntSchd":
		//	%msg = "How often to update plant bricks (for farming).<br><br>Range: 1000 to 3600000 milliseconds(ms) (1000ms = 1 second)";
		case "HngrSchd":
		%msg = "How often to decrease player hunger levels.<br><br>Range: 1000 to 3600000 milliseconds(ms) (1000ms = 1 second)";
		case "ThrstSchd":
		%msg = "How often to decrease player thirst levels.<br><br>Range: 1000 to 3600000 milliseconds(ms) (1000ms = 1 second)";
		case "DgrdSchd":
		%msg = "How often to decrease the durability levels of stored items.<br><br>Range: 1000 to 3600000 milliseconds(ms) (1000ms = 1 second)";
		
		
		case "StckLmtDef":
		%msg = "Default stack limit for items (if a stack limit value hasn't already been assigned.<br><br>Range: 1 to 999";
		case "WghtDef":
		%msg = "Default weight for items (if a weight value hasn't already been assigned.<br><br>Range: 1.0 to 999.0";
		case "DurabDef":
		%msg = "Default durability for items (if a durability value hasn't already been assigned.<br><br>Range: 1.00 to 999.00";
		case "MaxDgUseDef":
		%msg = "Default degrade-on-use amount for items (if a value hasn't already been assigned.<br><br>Range: 1.00 to 999.00";
		case "MaxDgTimeDef":
		%msg = "Default degrade-over-time amount for items (if a value hasn't already been assigned.<br><br>Range: 1.00 to 999.00";
		case "EffcDef":
		%msg = "Default efficiency for items (if an efficiency value hasn't already been assigned.<br><br>Range: 1.00 to 999.00";
		case "BurnRtDef":
		%msg = "Default burn rate for items (if a burn rate value hasn't already been assigned.<br><br>Range: 1.00 to 999.00";
	}
	
	CLIENTCMDMessageBoxOkBG("MonsterRPGx : Server Settings Help",%msg);
}



////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////
/////////////////////////SUPPORT FOR DYNAMIC AVATAR/////////////////////////
////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////



// The avatar view is built at runtime by Equipment.cs (MRPG_refreshEquip), so
// only touch it once it exists - this used to error every load.
if(isObject(MonsterRPGx_Avatar))
{
	MonsterRPGx_Avatar.setEmpty();
	MonsterRPGx_Avatar.dynamicObject = "MonsterRPGx_Avatar_Object";
	MonsterRPGx_Avatar.setObject(MonsterRPGx_Avatar.dynamicObject, "base/data/shapes/player/m.dts", "", 0);
}

function MonsterRPGx_Equipment::adjustAvatar(%gui)
{
	MonsterRPGx_Avatar.setSequence(MonsterRPGx_Avatar.dynamicObject, 0, "headup", 0);
	MonsterRPGx_Avatar.setSequence(MonsterRPGx_Avatar.dynamicObject, 1, "root", 0.85);
	MonsterRPGx_Avatar.setOrbitDist(4.34);
	MonsterRPGx_Avatar.setCameraRot(0.25, 0, 4.25);
	
	//THE MONSTERRPG CHARACTER WINS WHENEVER WE KNOW IT.
	//
	//Everything below this point styles the doll from the STOCK BLOCKLAND avatar prefs -
	//$pref::Avatar::HeadColor, $Pref::Avatar::FaceName and the parts list. That is your
	//Blockland blockhead, not your MonsterRPG character, which is why this screen and
	//character selection showed two different people. MRPG_applyCharLookToView is the one
	//place a character is styled onto a view (CharacterScreen.cs); character selection
	//goes through the same call, so the two cannot drift again.
	//
	//It returns 0 when no character look is loaded yet - a fresh join before MRPG_CharGet
	//has answered, or a save with no character created - and in that case we fall through
	//to the stock behaviour rather than painting a half-built default over the doll.
	if(isFunction("MRPG_applyCharLookToView")
		&& MRPG_applyCharLookToView(MonsterRPGx_Avatar, MonsterRPGx_Avatar.dynamicObject))
		return;

	if(MonsterRPGx_Avatar.parts > 0)
	{
		%gui.hideAllNodes();
		%gui.adjustAvatarNodes();
		%gui.adjustAvatarColors();
	}
	else
	{
		%gui.applyDefaultPrefs();
	}
}

//Restyle the equipment doll if it is currently built. Called when character data lands so
//the doll updates without the player having to reopen the panel.
function MRPG_refreshEquipAvatar()
{
	if(!isObject(MonsterRPGx_Avatar) || !isObject(MonsterRPGx_Equipment))
		return;
	if(MonsterRPGx_Avatar.dynamicObject $= "")
		return;
	MonsterRPGx_Equipment.adjustAvatar();
}

function MonsterRPGx_Equipment::adjustAvatarColors(%gui)
{
	if(MonsterRPGx_Avatar.parts > 0)
	{
		MonsterRPGx_Avatar.setNodeColor(MonsterRPGx_Avatar.dynamicObject, "headskin", $pref::Avatar::HeadColor);
		
		for(%i = 0; %i < MonsterRPGx_Avatar.parts; %i++)
		{
			%color = MonsterRPGx_Avatar.color[%i];
			
			if(%color $= "skin")
			{
				%color = $pref::Avatar::HeadColor;
			}
			
			MonsterRPGx_Avatar.setNodeColor(MonsterRPGx_Avatar.dynamicObject, MonsterRPGx_Avatar.part[%i], %color);
		}
	}
}

function MonsterRPGx_Equipment::adjustAvatarNodes(%gui)
{
	MonsterRPGx_Avatar.unHideNode(MonsterRPGx_Avatar.dynamicObject, "headskin");
	
	if(MonsterRPGx_Avatar.parts > 0)
	{
		for(%i = 0; %i < MonsterRPGx_Avatar.parts; %i++)
		{
			MonsterRPGx_Avatar.unHideNode(MonsterRPGx_Avatar.dynamicObject, MonsterRPGx_Avatar.part[%i]);
		}
	}
	
	MonsterRPGx_Avatar.setIflFrame(MonsterRPGx_Avatar.dynamicObject, "face", getIflFrame("face", $Pref::Avatar::FaceName));
	MonsterRPGx_Avatar.setIflFrame(MonsterRPGx_Avatar.dynamicObject, "decal", getIflFrame("decal", MonsterRPGx_Avatar.decal));
}

function MonsterRPGx_Equipment::applyDefaultPrefs(%gui)
{
	%gui.hideAllNodes();
	
	%nodeCount = 0;
	
	%node[%nodeCount++] = "headSkin" TAB "1 1 0 1";
	%node[%nodeCount++] = "chest" TAB "1 1 1 1";
	%node[%nodeCount++] = "LArm" TAB "1 1 1 1";
	%node[%nodeCount++] = "RArm" TAB "1 1 1 1";
	%node[%nodeCount++] = "LHand" TAB "1 1 0 1";
	%node[%nodeCount++] = "RHand" TAB "1 1 0 1";
	%node[%nodeCount++] = "pants" TAB "0 0 0.8 1";
	%node[%nodeCount++] = "LShoe" TAB "0.2 0.2 0.2 1";
	%node[%nodeCount++] = "RShoe" TAB "0.2 0.2 0.2 1";
	
	for(%i = 1; %i <= %nodeCount; %i++)
	{
		MonsterRPGx_Avatar.unHideNode(MonsterRPGx_Avatar.dynamicObject, getField(%node[%i], 0));
		MonsterRPGx_Avatar.setNodeColor(MonsterRPGx_Avatar.dynamicObject, getField(%node[%i], 0), getField(%node[%i], 1));
	}
	
	MonsterRPGx_Avatar.setIflFrame(MonsterRPGx_Avatar.dynamicObject, "face", 0);
	MonsterRPGx_Avatar.setIflFrame(MonsterRPGx_Avatar.dynamicObject, "decal", 0);
}

function MonsterRPGx_Equipment::hideAllNodes(%gui)
{
	%nodeCount = 0;
	
	%node[%nodeCount++] = "plume";
	%node[%nodeCount++] = "triPlume";
	%node[%nodeCount++] = "septPlume";
	%node[%nodeCount++] = "Visor";
	
	%node[%nodeCount++] = "helmet";
	%node[%nodeCount++] = "pointyHelmet";
	%node[%nodeCount++] = "flareHelmet";
	%node[%nodeCount++] = "scoutHat";
	%node[%nodeCount++] = "bicorn";
	%node[%nodeCount++] = "copHat";
	%node[%nodeCount++] = "knitHat";
	
	%node[%nodeCount++] = "chest";
	%node[%nodeCount++] = "femchest";
	
	%node[%nodeCount++] = "pants";
	%node[%nodeCount++] = "skirtHip";
	
	%node[%nodeCount++] = "armor";
	%node[%nodeCount++] = "bucket";
	%node[%nodeCount++] = "cape";
	%node[%nodeCount++] = "pack";
	%node[%nodeCount++] = "quiver";
	%node[%nodeCount++] = "tank";
	
	%node[%nodeCount++] = "epaulets";
	%node[%nodeCount++] = "epauletsRankA";
	%node[%nodeCount++] = "epauletsRankB";
	%node[%nodeCount++] = "epauletsRankC";
	%node[%nodeCount++] = "epauletsRankD";
	%node[%nodeCount++] = "ShoulderPads";
	
	%node[%nodeCount++] = "LArm";
	%node[%nodeCount++]  = "LArmSlim";
	
	%node[%nodeCount++]  = "LHand";
	%node[%nodeCount++]  = "LHook";
	
	%node[%nodeCount++]  = "RArm";
	%node[%nodeCount++]  = "RArmSlim";
	
	%node[%nodeCount++]  = "RHand";
	%node[%nodeCount++]  = "RHook";
	
	%node[%nodeCount++]  = "pants";
	%node[%nodeCount++]  = "skirtHip";
	
	%node[%nodeCount++]  = "LShoe";
	%node[%nodeCount++]  = "LPeg";
	
	%node[%nodeCount++]  = "RShoe";
	%node[%nodeCount++]  = "RPeg";
	
	%node[%nodeCount++]  = "SkirtTrimLeft";
	%node[%nodeCount++]  = "SkirtTrimRight";
	%node[%nodeCount++]  = "LSki";
	%node[%nodeCount++]  = "RSki";
	
	for(%i = 1; %i <= %nodeCount; %i++)
	{
		MonsterRPGx_Avatar.hideNode(MonsterRPGx_Avatar.dynamicObject, %node[%i]);
	}
}


function MonsterRPGx_Equipment::onWake(%gui)
{
	%gui.adjustAvatar();
}

function MonsterRPGx_ClearData()
{
	//CLEAR CLIENT DATA
	
	$MonsterRPG::Client::Server = 0;
	$MonsterRPG::Client::Level = 0;
	$MonsterRPG::Client::Exp = 0;
	$MonsterRPG::Client::MaxExp = 0;
	$MonsterRPG::Client::ChatID = 0;
	
	
	//CLEAR PARTY DATA
	
	$MonsterRPG::Party::InParty = 0;
	$MonsterRPG::Party::Name = 0;
	$MonsterRPG::Party::MaxMembers = 0;
	$MonsterRPG::Party::DropRateBonus = 0;
	$MonsterRPG::Party::YieldBonus = 0;
	$MonsterRPG::Party::FameBonus = 0;
	$MonsterRPG::Party::MembersCount = 0;
}


//simpler scalar method
function GuiControl::UIS_setOriginalValues(%this)
{
    if(!%this.UIS_setOriginalValues)
    {
        %this.UIS_originalPosition = %this.position;
        %this.UIS_originalExtent   = %this.extent;
        %this.UIS_realPosition     = %this.getScreenPosition();
		
        %this.UIS_setOriginalValues = true;
	}
	
    for(%i = %this.getCount() - 1; %i >= 0; %i--)
    {
        %child = %this.getObject(%i);
		
        %child.UIS_centerPosition = %this.UIS_centerPosition;
        %child.UIS_setOriginalValues();
	}
}

//bitmaps are scaled via top left corner to bottom right +scale
function GuiControl::UIS_applyScaling(%this, %sizeScalar, %newCenterPosition)
{
    if(!%this.UIS_setOriginalValues)
	%this.UIS_setOriginalValues();
	
    if(!%this.UIS_SkipScaling && !%this.UIS_doRescale())
    {
        %offset   = vectorSub(%this.UIS_realPosition, %this.UIS_centerPosition);
        %newCenterOffset = vectorSub(%newCenterPosition, %this.getGroup().getScreenPosition());
        %position = vectorAdd(%newCenterOffset, vectorScale(%offset, %sizeScalar));
        %extent   = vectorScale(%this.UIS_originalExtent, %sizeScalar);
		
        %this.resize(getWord(%position, 0), getWord(%position, 1), getWord(%extent, 0), getWord(%extent, 1));
		
		%this.newScaledPos = %position;
		%this.newScaledExt = %extent;
	}
	
    for(%i = %this.getCount() - 1; %i >= 0; %i--)
    {
        %child = %this.getObject(%i);
		
        %child.UIS_applyScaling(%sizeScalar, %newCenterPosition);
	}
}


function GuiControl::UIS_doRescale(%this)
{
    return false;    
}

function UIS_getScaledFontSize(%size)
{
    return getMin(%size * (getResX() / 1920), 128);
}

//////////////////////////////////////////////////////////////////////////////
// THE HUD BARS  -  HP / Mana orbs and the exp strip
//////////////////////////////////////////////////////////////////////////////
//
// EVERY NUMBER HERE COMES OFF newScaledPos / newScaledExt, WHICH ONLY EXIST AFTER
// UIS_applyScaling HAS RUN. That is the whole reason this was resolution-dependent.
//
// The orbs are drawn by clipping: the *_Percent swatch slides DOWN by the missing
// fraction of its own height and the colb bitmap inside it slides UP by exactly the same
// number of pixels, so the artwork stays put on screen while the swatch's clipToParent
// eats the top of it. The two movements MUST use the same figure - the swatch's height -
// or the art creeps relative to its frame.
//
// WHAT WAS WRONG:
//
//   1. Nothing re-applied the fill after a rescale. scaleNewCanvas rewrites every
//      newScaledPos/Ext and resizes the orbs back to FULL, but update*Bitmap early-returns
//      unless the percentage has changed, and the Set*Bitmap flags were only cleared on
//      DISCONNECT. So after a resolution change the bars sat at 100% - or at the old
//      resolution's pixel offsets - until the player's health happened to move. The error
//      is proportional to the size of the orb, which is why it read as "does not scale
//      above 1080p" rather than as a stale-cache bug.
//
//   2. The exp bar animated in absolute pixels captured before the loop started, and
//      re-read its own live extent as the next frame's baseline, so a rescale mid-fill
//      kept stamping old-resolution widths. It also dropped any exp update that arrived
//      while the loop was running, because PrevExpPercent was only assigned inside
//      `if(!inExpLoop)`.
//
//   3. It was a 1ms reschedule chain - two of them - which the comments elsewhere in this
//      HUD correctly call the most expensive thing on it.
//
// THE FIX IN ONE LINE: state is kept as a FRACTION, and pixels are derived from the
// CURRENT scaled geometry every time anything is drawn. A rescale then costs one redraw
// rather than needing the value to change before it can correct itself.

// Draw one clip-swatch orb at %percent (0..100). Returns 0 if the HUD has not been
// scaled yet, so the caller knows not to mark the bar as done.
function MRPG_setColbFill(%swatch, %bitmap, %percent)
{
	if(!isObject(%swatch) || !isObject(%bitmap))
		return 0;
	//No scaling pass yet. The old code went ahead anyway and fed resize() empty strings,
	//which collapses the control to its minExtent - a visibly broken orb that then needed
	//a health change to repair itself.
	if(%swatch.newScaledExt $= "" || %bitmap.newScaledExt $= "")
		return 0;

	if(%percent $= "")  %percent = 100;
	%percent = mClamp(%percent, 0, 100);

	%sx = mFloor(getWord(%swatch.newScaledPos, 0));
	%sy = mFloor(getWord(%swatch.newScaledPos, 1));
	%sw = mFloor(getWord(%swatch.newScaledExt, 0));
	%sh = mFloor(getWord(%swatch.newScaledExt, 1));

	//ONE figure, used twice with opposite signs. Rounding it once here rather than
	//rounding each of the two resizes separately is what stops the art drifting a pixel
	//against its frame at awkward scales.
	%drop = mFloor(%sh * ((100 - %percent) / 100));

	%swatch.resize(%sx, %sy + %drop, %sw, %sh);

	%bx = mFloor(getWord(%bitmap.newScaledPos, 0));
	%by = mFloor(getWord(%bitmap.newScaledPos, 1));
	%bw = mFloor(getWord(%bitmap.newScaledExt, 0));
	%bh = mFloor(getWord(%bitmap.newScaledExt, 1));
	%bitmap.resize(%bx, %by - %drop, %bw, %bh);

	return 1;
}

// Draw a horizontal fill bar by narrowing its clip swatch. The art inside stays full
// width and is clipped, so only the swatch is touched here.
function MRPG_setBarWidth(%ctrl, %frac)
{
	if(!isObject(%ctrl) || %ctrl.newScaledExt $= "")
		return 0;

	if(%frac $= "") %frac = 0;
	if(%frac < 0)   %frac = 0;
	if(%frac > 1)   %frac = 1;

	%x = mFloor(getWord(%ctrl.newScaledPos, 0));
	%y = mFloor(getWord(%ctrl.newScaledPos, 1));
	%w = mFloor(getWord(%ctrl.newScaledExt, 0) * %frac);
	%h = mFloor(getWord(%ctrl.newScaledExt, 1));

	//minExtent on these swatches is "8 2", so resizing to nothing leaves an 8px stub of
	//fill sitting at zero exp. Hide it instead of drawing a lie.
	if(%w < 1)
	{
		%ctrl.setVisible(0);
		return 1;
	}
	%ctrl.setVisible(1);
	%ctrl.resize(%x, %y, %w, %h);
	return 1;
}

function updateHPBitmap(%HPPercent)
{
	%HPPercent = mClamp(%HPPercent, 0, 100);
	if($MonsterRPG::Client::PrevHPPercent == %HPPercent && $MonsterRPG::Client::SetHPBitmap)
		return;

	//The ghost shows where the bar WAS, then fades. It has to be drawn before Prev is
	//overwritten below.
	if(isObject(Fade_HP_Colb))
	{
		Fade_HP_Colb.setVisible(true);
		Fade_HP_Colb.schedule(2000, setVisible, 0);
	}
	MRPG_setColbFill(Fade_HP_Percent, Fade_HP_Colb, $MonsterRPG::Client::PrevHPPercent);

	//Only claim the bar is drawn if it actually drew. Otherwise the equality test above
	//would latch a value that never reached the screen.
	if(MRPG_setColbFill(HP_Percent, HP_Colb, %HPPercent))
		$MonsterRPG::Client::SetHPBitmap = 1;

	$MonsterRPG::Client::PrevHPPercent = %HPPercent;
}

function updateManaBitmap(%ManaPercent)
{
	%ManaPercent = mClamp(%ManaPercent, 0, 100);
	if($MonsterRPG::Client::PrevManaPercent == %ManaPercent && $MonsterRPG::Client::SetManaBitmap)
		return;

	if(isObject(Fade_Mana_Colb))
	{
		Fade_Mana_Colb.setVisible(true);
		Fade_Mana_Colb.schedule(2000, setVisible, 0);
	}
	MRPG_setColbFill(Fade_Mana_Percent, Fade_Mana_Colb, $MonsterRPG::Client::PrevManaPercent);

	if(MRPG_setColbFill(Mana_Percent, Mana_Colb, %ManaPercent))
		$MonsterRPG::Client::SetManaBitmap = 1;

	$MonsterRPG::Client::PrevManaPercent = %ManaPercent;
}

//////////////////////////////////////////////////////////////////////////////
// EXP  -  animated, in fractions
//////////////////////////////////////////////////////////////////////////////
// 33ms, not 1ms. The bar travels at a fixed fraction-per-second, so the cadence controls
// smoothness only and nothing about the animation's duration depends on it - dropping two
// 1ms reschedule chains to one 30fps one costs nothing visible.
$MRPG::Exp::TickMs = 33;
$MRPG::Exp::Rate   = 0.60;   // fraction of the whole bar per second

function updateExpBitmap(%ExpPercent)
{
	%ExpPercent = mClamp(%ExpPercent, 0, 100);

	//RECORDED UNCONDITIONALLY. The old code only stored this inside `if(!inExpLoop)`, so
	//an exp gain that landed while the bar was still filling was discarded outright and
	//the bar settled at the previous target.
	$MonsterRPG::Client::ExpTarget      = %ExpPercent / 100;
	$MonsterRPG::Client::PrevExpPercent = %ExpPercent;
	$MonsterRPG::Client::SetExpBitmap   = 1;

	//First sight of a value: start AT it rather than sweeping up from empty on spawn.
	if($MonsterRPG::Client::ExpFrac $= "")
	{
		$MonsterRPG::Client::ExpFrac     = $MonsterRPG::Client::ExpTarget;
		$MonsterRPG::Client::ExpFadeFrac = $MonsterRPG::Client::ExpTarget;
	}

	if(!$MonsterRPG::Client::inExpLoop)
		MRPG_expTick();
}

function MRPG_expTick()
{
	cancel($doExpSch);

	//Gate, and do NOT reschedule when it is shut. See ServerGate.cs rule 3.
	if(!MRPG_isActive()){ $MonsterRPG::Client::inExpLoop = 0; return; }

	%target = $MonsterRPG::Client::ExpTarget;
	%fade   = $MonsterRPG::Client::ExpFadeFrac;
	%solid  = $MonsterRPG::Client::ExpFrac;
	%step   = $MRPG::Exp::Rate * ($MRPG::Exp::TickMs / 1000);

	//A LEVEL-UP WRAPS THE BAR BACKWARDS - 98% to 3%. Animating that plays as the bar
	//draining, which is the exact opposite of what just happened, so a decrease snaps.
	if(%target < %fade - 0.001)
	{
		%fade  = %target;
		%solid = %target;
	}
	else if(%fade < %target)
	{
		%fade = %fade + %step;
		if(%fade > %target) %fade = %target;
	}
	else if(%solid < %fade)
	{
		//the leading ghost has arrived; the solid bar now catches up to it
		%solid = %solid + %step;
		if(%solid > %fade) %solid = %fade;
	}

	$MonsterRPG::Client::ExpFadeFrac = %fade;
	$MonsterRPG::Client::ExpFrac     = %solid;

	//PIXELS ARE DERIVED HERE, EVERY TICK, FROM THE LIVE SCALED GEOMETRY - which is what
	//makes a resolution change mid-fill a non-event instead of a bar frozen at the old
	//screen's width.
	%drawn = MRPG_setBarWidth(Exp_Fade_Percent, %fade);
	MRPG_setBarWidth(Exp_Percent, %solid);

	if(%drawn && %solid >= %target - 0.0005 && %fade >= %target - 0.0005)
	{
		$MonsterRPG::Client::inExpLoop = 0;
		if(isObject(Exp_Fade_Percent))
			Exp_Fade_Percent.setVisible(false);
		return;
	}

	$MonsterRPG::Client::inExpLoop = 1;
	$doExpSch = schedule($MRPG::Exp::TickMs, 0, MRPG_expTick);
}

//////////////////////////////////////////////////////////////////////////////
// Re-draw every bar at its CURRENT value using the CURRENT scaled geometry.
//
// Called from scaleNewCanvas, i.e. after every canvas reset. This is the piece that was
// missing: without it a rescale left the bars at whatever pixels the previous resolution
// had produced, and nothing corrected them until the underlying value moved.
//////////////////////////////////////////////////////////////////////////////
function MRPG_reapplyBars()
{
	%hp   = $MonsterRPG::Client::PrevHPPercent;
	%mana = $MonsterRPG::Client::PrevManaPercent;
	if(%hp   $= "") %hp   = 100;
	if(%mana $= "") %mana = 100;

	MRPG_setColbFill(HP_Percent,   HP_Colb,   %hp);
	MRPG_setColbFill(Mana_Percent, Mana_Colb, %mana);

	//The ghosts follow the LIVE value rather than the old one. A "where it was" marker
	//drawn against geometry that has just changed underneath it is not information.
	MRPG_setColbFill(Fade_HP_Percent,   Fade_HP_Colb,   %hp);
	MRPG_setColbFill(Fade_Mana_Percent, Fade_Mana_Colb, %mana);

	MRPG_setBarWidth(Exp_Fade_Percent, $MonsterRPG::Client::ExpFadeFrac);
	MRPG_setBarWidth(Exp_Percent,      $MonsterRPG::Client::ExpFrac);

	//The exp ghost is only meaningful while the fill is running.
	if(!$MonsterRPG::Client::inExpLoop && isObject(Exp_Fade_Percent))
		Exp_Fade_Percent.setVisible(false);
}

function clientCmdaddMonsterRPGGUI()
{
	PlayGui.add("MonsterRPGx_HitMarker");
	PlayGui.add("TopPrintDlg");
	PlayGui.add("MiddlePrintDlg");
	PlayGui.add("MonsterRPGx_MAIN_INTERFACE");
	PlayGui.add("Damage_Indicator");
	PlayGui.add("MonsterRPGx_LevelUpPopup");
	$MonsterRPG::Client::PrevHPPercent = 100;
	
	MonsterRPGx_MAIN_INTERFACE.setVisible(false);
	
	MonsterRPGx_MAIN_INTERFACE.UIS_setOriginalValues();
	MonsterRPGx_LevelUpPopup.UIS_setOriginalValues();
	
	scaleNewCanvas(MonsterRPGx_MAIN_INTERFACE);
	
	modifySwatchCtrls(MonsterRPGx_Main);
	
	PlayGui_Vignette.setBitmap("Add-ons/Client_MonsterRPG/GUIs/Vignette1.png");
	applyAllVignettes();
}

// ONE OF THE TWO HELLOS. Sent from GameConnection::autoAdminCheck
// (Core_OLDpackage.cs), which stock calls inside GameConnection::startLoad - so
// this lands before the mission download finishes. MRPG_Hello (LoadingScreen.cs)
// is the other and lands later; both funnel into MRPG_ClientEnter, which is
// idempotent, so whichever arrives first opens the gate.
function clientCmdaddMRPGClientToServer()
{
	initiateMouseHandlers();

	//The gate. Sets $MonsterRPG::Client::inMonsterRPGServer and starts everything
	//that used to start itself at boot - see ServerGate.cs.
	MRPG_ClientEnter();

	PlayGui_ShapeNameHud.profile = "GuiCustomShapeNameMLTextProfile";

	//Projected 2D bot nameplates - must start after inMonsterRPGServer is set,
	//since both plate loops bail out on that flag.
	MRPGPlates_Start();
}

function clientCmdSendRealItemName(%realName)
{
	$MonsterRPG::Client::realItemName = %realName;

	//Update the visible tool-name HUD immediately. The scroll/select functions ask
	//the server for this BEFORE sending 'UseTool', so without this push the label
	//always lagged one selection behind (or stayed blank when no handler existed).
	if($ScrollMode == $SCROLLMODE_TOOLS)
		setCustomToolName(%realName);
}

function getRPGMaterial(%level)
{
	if(%level >= 1 && %level < 10){ return "Bronze"; }
	else if(%level >= 10 && %level < 25){ return "Iron"; }
	else if(%level >= 25 && %level < 50){ return "Silver"; }
	else if(%level >= 50 && %level < 75){ return "Gold"; }
	else if(%level >= 75 && %level < 100){ return "Mithril"; }
	else if(%level >= 100 && %level < 125){ return "Adamant"; }
	else if(%level >= 125 && %level < 150){ return "Adamant"; }
}

function getRPGLevelRequired(%material)
{
	switch(%material)
	{
		case "Bronze": return 1;
		case "Iron": return 10;
		case "Silver": return 25;
		case "Gold": return 50;
		case "Mithril": return 75;
		case "Adamant": return 100;
	}
}

function getRPGTierMultiplier(%material)
{
	switch(%material)
	{
		case "Bronze": return 1;
		case "Iron": return 4;
		case "Silver": return 8;
		case "Gold": return 12;
		case "Mithril": return 16;
		case "Adamant": return 20;
	}
}

function clientCmdPlayGui_CreateNewToolHud (%numSlots)
{
	if (%numSlots < 0 || %numSlots > 46)
	{
		return;
	}
	$HUD_NumToolSlots = %numSlots;
	PlayGui.createNewToolHUD ();
}

function setRPGItemIconBySlot(%itemID,%level,%icon,%slot)
{
	if(%itemID[%slot].isSword)
	{
		%itemID[%slot].setBitmap ("Add-Ons/Client_MonsterRPG/GUIs/Weapon_Icons/icon_" @ %itemID[%slot].class);
		%material = getRPGMaterial(%itemID[%slot].level);
		
		switch(%material)
		{
			case "Bronze": %itemID[%slot].setColor("0.804 0.498 0.196 1.000");
			case "Iron": %itemID[%slot].setColor("0.631 0.616 0.58 1");
			case "Silver": %itemID[%slot].setColor("0.753 0.753 0.753 1");
			case "Gold": %itemID[%slot].setColor("1 0.843 0 1");
			case "Mithril": %itemID[%slot].setColor("0.565 0.565 0.875 1.000");
			case "Adamant": %icon.setColor("0.172 0.172 0.168 1.000");
		}
	}
}

function setRPGItemIcon(%itemDB,%level,%icon)
{
	if(MonsterRPG_isSwordItem(%itemDB))
	{
		%icon.setBitmap ("Add-Ons/Client_MonsterRPG/GUIs/Weapon_Icons/icon_" @ MonsterRPG_getSwordClass(%itemDB));

		%clr = MonsterRPG_getMaterialColor(getRPGMaterial(%level));
		if(%clr $= "" && %itemDB.doColorShift)
			%clr = %itemDB.colorShiftColor;

		if(%clr !$= "")
		{
			//Inventory cells tint via the mColor field (0-255); setColor covers the
			//hotbar-style controls.
			%icon.mColor = getColorI(%clr);
			%icon.setColor(%clr);
		}
	}
}

function useNewTools(%val)
{
	commandToServer('GetRealItemName');
	if (%val)
	{
		if ($ScrollMode != $SCROLLMODE_TOOLS)
		{
			if ($CurrScrollToolSlot <= 0)
			{
				$CurrScrollToolSlot = 0;
			}
			%i = 0;
			while (%i < $HUD_NumToolSlots)
			{
				%idx = (%i + $CurrScrollToolSlot) % $HUD_NumToolSlots;
				if ($ToolData[%idx] > 0)
				{
					$CurrScrollToolSlot = %idx;
					break;
				}
				%i += 1;
			}
			if (%i == $HUD_NumToolSlots)
			{
				return;
			}
			setScrollMode ($SCROLLMODE_TOOLS);
			HUD_ToolActive.setVisible (True);
			setNewActiveTool ($CurrScrollToolSlot);
			setCustomToolName($MonsterRPG::Client::realItemName);
			commandToServer ('UseTool', $CurrScrollToolSlot);
		}
		else 
		{
			setScrollMode ($SCROLLMODE_NONE);
		}
	}
}

function clientCmdNewSetActiveTool(%slot)
{
	commandToServer('GetRealItemName');
	setScrollMode ($SCROLLMODE_TOOLS);
	$CurrScrollToolSlot = %slot;
	HUD_ToolActive.setVisible(True);
	setNewActiveTool ($CurrScrollToolSlot);
	setCustomToolName($MonsterRPG::Client::realItemName);
	commandToServer('UseTool', $CurrScrollToolSlot);
}

function setNewActiveTool(%index)
{
	commandToServer('GetRealItemName');
	if (!isObject(HUD_ToolActive) || !isObject(HUD_ToolName))
	{
		return;
	}
	if (%index < 0)
	{
		HUD_ToolActive.setVisible(0);
		setCustomToolName("");
		return;
	}
	HUD_ToolActive.setVisible(1);
	%x = 0;
	%y = 64 * %index;
	%w = 64;
	%h = 64;
	HUD_ToolActive.resize(%x, %y, %w, %h);
	setCustomToolName($MonsterRPG::Client::realItemName);
}

function findClosestValue(%inputValue, %valueList) {
    %closestValue = getWord(%valueList, 0);
    %closestDistance = mAbs(%inputValue - %closestValue);
	
    %count = getWordCount(%valueList);
    for (%i = 1; %i < %count; %i++) {
        %currentValue = getWord(%valueList, %i);
        %currentDistance = mAbs(%inputValue - %currentValue);
		
        if (%currentDistance < %closestDistance) {
            %closestValue = %currentValue;
            %closestDistance = %currentDistance;
		}
	}
	
    return %closestValue;
}

function getRPGuiScale()
{
	%resY = getWord(getRes(),1);
	%closestValue = findClosestValue(%resY,"720 1080 2160");
	
	if(%closestValue == 2160)
	{
		return "4k";
	}
	else if(%closestValue == 1080)
	{
		return "Standard";
	}
	else if(%closestValue == 720)
	{
		return "Small";
	}
	else
	{
		return "Standard";
	}
}

function setToClosestValue(%inputValue, %valueList) {
    %closestValue = findClosestValue(%inputValue, %valueList);
    return %closestValue;
}

function PlayGui::killNewToolHud (%this)
{
	if (isEventPending (HUD_ToolBox.moveSchedule))
	{
		cancel (HUD_ToolBox.moveSchedule);
	}
	if (isObject (HUD_ToolBox))
	{
		HUD_ToolBox.delete ();
	}
	if (isObject (HUD_ToolNameBG))
	{
		HUD_ToolNameBG.delete ();
	}
	if (isObject (HUD_ToolNameBG2))
	{
		HUD_ToolNameBG2.delete ();
	}
	if (isObject (HUD_ToolName))
	{
		HUD_ToolName.delete ();
	}
	if (isObject (HUD_ToolName2))
	{
		HUD_ToolName2.delete ();
	}
}

function PlayGui::createNewToolHUD (%this)
{
	%this.killNewToolHud ();
	%numSlots = $HUD_NumToolSlots;
	%res = getRes ();
	%screenWidth = getWord (%res, 0);
	%screenHeight = getWord (%res, 1);
	%iconWidth = mFloor(%screenWidth / $BSD_NumInventorySlots) / 2;
	%closestValue = findClosestValue(%iconWidth,"32 64 128 254");
	if (%closestValue > 254)
	{
		%closestValue = 254;
	}
	%iconWidth = %closestValue;
	%iconBoxHeight = $HUD_NumToolSlots * %iconWidth;
	%topSpace = 0;
	%sideSpace = 0;
	%newBox = new GuiBitmapCtrl ("");
	%newBox.setName ("HUD_ToolBox");
	%this.add (%newBox);
	%newBox.setProfile (HUDBitmapProfile);
	%newBox.setBitmap ("Add-Ons/Client_MonsterRPG/GUIs/Weapon_Icons/itemBG" @ %closestValue @ ".png");
	%newBox.wrap = 1;
	
	
	%x = (%screenWidth - %iconWidth) - %sideSpace;
	%y = 0;
	%w = %iconWidth;
	%h = %iconBoxHeight;
	%newBox.resize (%x, %y, %w, %h);
	%newActive = new GuiBitmapCtrl ("");
	%newBox.add (%newActive);
	%newActive.setProfile (HUDBitmapProfile);
	%newActive.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/Weapon_Icons/ActiveItem64.png");
	%newActive.setVisible (0);
	%x = 0;
	%y = 0;
	%w = %iconWidth;
	%h = %iconWidth;
	%newActive.resize (%x, %y, %w, %h);
	%newActive.setName ("HUD_ToolActive");
	%i = 0;
	while (%i < %numSlots)
	{
		%newIcon = new GuiBitmapCtrl ("");
		%newBox.add (%newIcon);
		%newIcon.setProfile (HUDBitmapProfile);
		if ($ToolData[%i] > 0)
		{
			//Swords first - the generated dueling swords' iconName ("./icon_...")
			//never resolves as a file and their isSword/class fields don't reach
			//the client, so detection goes through MonsterRPG_isSwordItem (uiName
			//fallback). Same logic as clientCmdhandleNewItemPickup; this rebuild
			//used to repaint the first-letter "B" over correctly drawn icons.
			if(MonsterRPG_isSwordItem($ToolData[%i]))
			{
				%newIcon.setBitmap ("Add-Ons/Client_MonsterRPG/GUIs/Weapon_Icons/icon_" @ MonsterRPG_getSwordClass($ToolData[%i]));

				%clr = MonsterRPG_getMaterialColor(getRPGMaterial($ToolData[%i].level));
				if(%clr $= "" && $ToolData[%i].doColorShift)
					%clr = $ToolData[%i].colorShiftColor;
				if(%clr $= "")
					%clr = "1 1 1 1";
				%newIcon.setColor(%clr);
			}
			else if (!isFile ($ToolData[%i].iconName @ ".png"))
			{
				%firstLetter = getSubStr ($ToolData[%i].uiName, 0, 1);
				%letterFile = "Add-Ons/Print_Letters_Default/icons/" @ %firstLetter @ ".png";
				if (isFile (%letterFile))
				{
					%newIcon.setBitmap (%letterFile);
				}
				else
				{
					%newIcon.setBitmap ("base/client/ui/brickIcons/unknown.png");
				}
			}
			else
			{
				%newIcon.setBitmap($ToolData[%i].iconName);

				if ($ToolData[%i].doColorShift)
				{
					%newIcon.setColor ($ToolData[%i].colorShiftColor);
				}
				else
				{
					%newIcon.setColor ("1 1 1 1");
				}
			}
		}
		
		%x = 0;
		%y = %i * %iconWidth;
		%w = %iconWidth;
		%h = %iconWidth;
		%newIcon.resize (%x, %y, %w, %h);
		$HUD_ToolIcon[%i] = %newIcon;
		%i += 1;
	}
	
	// newSwatch
	%newSwatch = new GuiBitmapCtrl ("");
	PlayGui.add (%newSwatch);
	%newSwatch.setProfile (HUDBrickNameProfile);
	%newSwatch.setBitmap ("Add-Ons/Client_MonsterRPG/GUIs/Popup_Elements/inv_popup_flag64.png");
	%w = %iconWidth;
	%h = 18;
	%x = (%screenWidth - %iconWidth) - %sideSpace;
	%y = (%iconWidth * %numSlots) + 0;
	
	%newSwatch.OriginalX = %x;
	%newSwatch.OriginalY = %y;
	%newSwatch.OriginalW = %w;
	%newSwatch.OriginalH = %h;
	
	%newSwatch.resize (%x, %y, %w, %h);
	%newSwatch.setName ("HUD_ToolNameBG");
	%newText = new GuiMLTextCtrl ("");
	%newSwatch.add (%newText);
	%newText.setProfile (HUDCenterTextProfile);
	%w = %iconWidth;
	%h = 18;
	%x = 4;
	%y = 0;
	
	%newText.OriginalX = %x;
	%newText.OriginalY = %y;
	%newText.OriginalW = %w;
	%newText.OriginalH = %h;
	
	%newText.resize (%x, %y, %w, %h);
	%newText.setName("HUD_ToolName");
	
	// tooltip
	%newText = new GuiTextCtrl ("");
	HUD_ToolNameBG.add (%newText);
	%newText.setProfile (HUDCenterTextProfile);
	%key = strupr (getWord (moveMap.getBinding ("useTools"), 1));
	%newText.setText (%key SPC "= tools");
	%x = 0;
	%y = 0;
	%w = %iconWidth;
	%h = 18;
	%newText.resize (%x, %y, %w, %h);
	%newText.setName ("ToolTip_Tools");
	if (!$pref::HUD::showToolTips)
	{
		ToolTip_Tools.setVisible (0);
	}
	setCustomToolName("");
	if ($CurrScrollToolSlot $= "")
	{
		$CurrScrollToolSlot = 0;
	}
	if ($ScrollMode != $SCROLLMODE_TOOLS && $pref::HUD::HideToolBox)
	{
		if ($pref::HUD::showToolTips)
		{
			PlayGui.hideToolBox ($HUD_NumToolSlots * 64, 10, 0);
		}
		else 
		{
			PlayGui.hideToolBox (($HUD_NumToolSlots * 64) + 25, 10, 0);
		}
		HUD_ToolActive.setVisible (0);
	}
	else if ($ScrollMode == $SCROLLMODE_TOOLS)
	{
		setNewActiveTool ($CurrScrollToolSlot);
		HUD_ToolActive.setVisible (1);
		ToolTip_Tools.setVisible (0);
	}
	setScrollMode ($SCROLLMODE_NONE);
	%resX = getWord (getRes (), 0);
	%resY = getWord (getRes (), 1);
	if (%resX >= 1024)
	{
		%w = getWord (HUD_SuperShift.getExtent (), 0);
		%h = getWord (HUD_SuperShift.getExtent (), 1);
		%x = getWord (HUD_SuperShift.getPosition (), 0);
		%y = %resY - %h;
		HUD_SuperShift.resize (%x, %y, %w, %h);
	}
	else 
	{
		%w = getWord (HUD_SuperShift.getExtent (), 0);
		%h = getWord (HUD_SuperShift.getExtent (), 1);
		%x = getWord (HUD_SuperShift.getPosition (), 0);
		%y = %resY - (87 + %h);
		HUD_SuperShift.resize (%x, %y, %w, %h);
	}
}

function setCustomToolName(%name)
{
	%count = getWordCount(%name);
	%size = 16 - (strLen(%name) / 4);
	HUD_ToolName.setText("<just:center><color:FFFFFF><font:verdana bold:" @ %size @ ">" @ %name);
	HUD_ToolNameBG.resize(HUD_ToolNameBG.OriginalX,HUD_ToolNameBG.OriginalY,HUD_ToolNameBG.OriginalW,HUD_ToolNameBG.OriginalH + strLen(%name) / 4);
	HUD_ToolName.resize(HUD_ToolName.OriginalX,HUD_ToolName.OriginalY + strLen(%name) / 6,HUD_ToolName.OriginalW,HUD_ToolName.OriginalH);
}

//Sword detection that works even if the eval-generated datablocks' isSword/class
//fields didn't reach the client: generated dueling swords are named
//"<Material> <Class>" (e.g. "Bronze Rapier"), and uiName always transmits.
function MonsterRPG_isSwordItem(%itemDB)
{
	if(%itemDB.isSword)
		return true;
	return MonsterRPG_getSwordClass(%itemDB) !$= "";
}

function MonsterRPG_getSwordClass(%itemDB)
{
	if(%itemDB.class !$= "")
		return %itemDB.class;

	if(getWordCount(%itemDB.uiName) == 2)
	{
		%mat = getWord(%itemDB.uiName,0);
		if(%mat $= "Bronze" || %mat $= "Iron" || %mat $= "Silver" || %mat $= "Gold" || %mat $= "Mithril" || %mat $= "Adamant")
		{
			%cls = getWord(%itemDB.uiName,1);
			if(%cls $= "Shortsword" || %cls $= "Rapier" || %cls $= "Longsword" || %cls $= "Katana" || %cls $= "Scimitar")
				return %cls;
		}
	}
	return "";
}

//Material color table (float RGBA) - shared by the hotbar and inventory icons.
function MonsterRPG_getMaterialColor(%material)
{
	switch$(%material)
	{
		case "Bronze":  return "0.804 0.498 0.196 1.000";
		case "Iron":    return "0.631 0.616 0.58 1";
		case "Silver":  return "0.753 0.753 0.753 1";
		case "Gold":    return "1 0.843 0 1";
		case "Mithril": return "0.565 0.565 0.875 1.000";
		case "Adamant": return "0.172 0.172 0.168 1.000";
	}
	return "";
}

function clientCmdhandleNewItemPickup(%slot, %itemData, %level)
{
	if(isObject(%itemData))
		echo("ICONDBG: slot=" @ %slot @ " item=" @ %itemData.getName() @ " uiName=" @ %itemData.uiName @ " isSword=" @ %itemData.isSword @ " class=" @ %itemData.class @ " level=" @ %level @ " iconName=" @ %itemData.iconName);

	$ToolData[%slot] = %itemData;
	$ToolData[%slot].level = %level;
	if (isObject ($HUD_ToolIcon[%slot]))
	{
		if (!isObject (%itemData))
		{
			$HUD_ToolIcon[%slot].setBitmap ("");
		}
		else if(MonsterRPG_isSwordItem(%itemData))
		{
			//Swords: class-based weapon icon tinted by material (from level). Checked
			//BEFORE the iconName file test because the sword datablocks' iconName
			//("./icon_...") does not resolve as a file at client runtime, which would
			//otherwise drop swords into the first-letter fallback below.
			$HUD_ToolIcon[%slot].setBitmap ("Add-Ons/Client_MonsterRPG/GUIs/Weapon_Icons/icon_" @ MonsterRPG_getSwordClass(%itemData));

			%clr = MonsterRPG_getMaterialColor(getRPGMaterial(%level));
			if(%clr $= "" && %itemData.doColorShift)
				%clr = %itemData.colorShiftColor;
			if(%clr $= "")
				%clr = "1 1 1 1";
			$HUD_ToolIcon[%slot].setColor(%clr);
		}
		else if (!isFile (%itemData.iconName @ ".png"))
		{
			%firstLetter = getSubStr (%itemData.uiName, 0, 1);
			%letterFile = "Add-Ons/Print_Letters_Default/icons/" @ %firstLetter @ ".png";
			if (isFile (%letterFile))
			{
				$HUD_ToolIcon[%slot].setBitmap (%letterFile);
			}
			else
			{
				$HUD_ToolIcon[%slot].setBitmap ("base/client/ui/brickIcons/unknown.png");
			}
		}
		else
		{
			$HUD_ToolIcon[%slot].setBitmap (%itemData.iconName);

			if ($ToolData[%slot].doColorShift)
			{
				$HUD_ToolIcon[%slot].setColor ($ToolData[%slot].colorShiftColor);
			}
			else
			{
				$HUD_ToolIcon[%slot].setColor ("1 1 1 1");
			}
		}
	}
	if ($ScrollMode == $SCROLLMODE_TOOLS)
	{
		if ($CurrScrollToolSlot == %slot)
		{
			HUD_ToolName.setText("<color:FFFFFF><font:verdana:8>" @ $MonsterRPG::Client::realItemName);
			commandToServer ('useTool', %slot);
		}
	}
	if (!%silent)
	{
		alxPlay (ItemPickup);
	}
}

function clientCmdSendHitIndData(%data)
{
	Damage_Indicator.resetHits();
	$MonsterRPG::Client::lastHitAngle = %data;
}

function clientCmdSendDeathVar(%data)
{
	$MonsterRPG::Client::isDead = %data;
	Damage_Indicator.resetHits();
}

function GuiControl::resetHits()
{
	$MonsterRPG::Client::lastHitAngle = 0;
	$MonsterRPG::Client::GotHit = 0;
	Damage_Indicator.setVisible(0);
}

function clientCmdSetBloodVignette (%multiply, %color)
{
	if(isObject(Vignette_Blood))
	{
		//same reasoning as clientCmdSetDeathScreen - colour must apply even
		//while hidden, since visibility is set immediately afterwards
		if (%color $= "")
		{
			error ("ERROR: clientCmdSetBloodVignette(" @ %multiply @ ", " @ %color @ ") - null color");
			return;
		}
		%multiply = mClamp (%multiply, 0, 1);
		%color = getColorF (%color);
		Vignette_Blood.mMultiply = %multiply;
		Vignette_Blood.setColor (%color);
	}
}

function clientCmdSetBloodVignetteVisible(%val)
{
	if(isObject(Vignette_Blood))
	{
		Vignette_Blood.setVisible(%val);
	}
}

function clientCmdSetDeathScreen(%multiply, %color)
{
	if(isObject(DeathScreen))
	{
		//No isVisible() gate. The server sends SetDeathScreen and THEN
		//SetDeathScreenVisible, so gating on visibility threw away the colour
		//on the very tick you died and showed the overlay with the previous
		//(transparent) colour until the next HUD tick corrected it.
		if (%color $= "")
		{
			error ("ERROR: clientCmdSetDeathScreen(" @ %multiply @ ", " @ %color @ ") - null color");
			return;
		}
		%multiply = mClamp (%multiply, 0, 1);
		%color = getColorF (%color);
		DeathScreen.mMultiply = %multiply;
		DeathScreen.setColor (%color);
	}
}

function clientCmdSetDeathScreenVisible(%val)
{
	if(isObject(DeathScreen))
	{
		DeathScreen.setVisible(%val);
	}
}

//Stretch a full-screen overlay to exactly cover its parent.
//
//horizSizing/vertSizing "relative" only rescales a child when the PARENT
//resizes. These overlays are built at 640x480 and added to a PlayGui that is
//already at the player's resolution, so that resize never fires and they sit
//at 640x480 in the top-left corner. "width"/"height" make them track the
//parent from here on; this call fixes the frame they are created in.
function fitOverlayToParent(%ctrl, %parent)
{
	if(!isObject(%ctrl) || !isObject(%parent)){ return; }

	%ext = %parent.getExtent();
	%ctrl.resize(0, 0, getWord(%ext, 0), getWord(%ext, 1));
}

function applyAllVignettes()
{
	//Rebuilt on every call (this runs again on each server join). Without
	//this the old controls stay parented and the overlays stack, so the
	//screen darkens a little more every time you reconnect.
	//
	//Client_BuggySurvival/HitIndicatorClient.cs also creates a control named
	//Vignette_Blood for its hit indicator, so only reclaim that name if the
	//existing one is ours - otherwise just correct its sizing and leave it.
	if(isObject(Vignette_Blood) && Vignette_Blood.isMonsterRPGOverlay)
	{
		Vignette_Blood.delete();
	}

	if(isObject(DeathScreen)){ DeathScreen.delete(); }

	//only build the blood vignette if nobody else already owns that name
	if(!isObject(Vignette_Blood))
	{
		%VIGBlood = new GuiBitmapCtrl()
		{
			profile = "GuiDefaultProfile";
			//stretch with the parent instead of scaling proportionally
			horizSizing = "width";
			vertSizing = "height";
			position = "0 0";
			extent = "640 480";
			minExtent = "8 2";
			enabled = 1;
			//server drives visibility (SetBloodVignetteVisible, Core_HUD.cs);
			//starting visible would tint the whole screen until the first tick
			visible = 0;
			clipToParent = 1;
			bitmap = "Add-ons/Client_MonsterRPG/GUIs/VignetteBlood.png";
			wrap = 0;
			//must be 0 - locking the bitmap to its source aspect letterboxes
			//the overlay on any non-4:3 display instead of covering the screen
			lockAspectRatio = 0;
			alignLeft = 0;
			overflowImage = 1;
			keepCached = 0;
			mColor = "0 0 0 0";
			mMultiply = 1;
			isMonsterRPGOverlay = 1;
		};

		%VIGBlood.setName("Vignette_Blood");
		PlayGui_Vignette.add("Vignette_Blood");
	}

	//correct the sizing whether we built it or another add-on did
	fitOverlayToParent(Vignette_Blood, PlayGui_Vignette);

	%DeathScreen = new GuiBitmapCtrl()
	{
		profile = "GuiDefaultProfile";
		horizSizing = "width";
		vertSizing = "height";
		position = "0 0";
		extent = "640 480";
		minExtent = "8 2";
		enabled = 1;
		//Starts hidden. It used to be created visible, which was survivable
		//while it was a 640x480 box in the corner but now covers the screen -
		//that would flash a black overlay between GUI setup and the first
		//SetDeathScreenVisible from the server's HUD loop.
		visible = 0;
		clipToParent = 1;
		bitmap = "Add-ons/Client_MonsterRPG/GUIs/DeathScreen.png";
		wrap = 0;
		lockAspectRatio = 0;
		alignLeft = 0;
		overflowImage = 1;
		keepCached = 0;
		mColor = "0 0 0 0";
		mMultiply = 1;
		isMonsterRPGOverlay = 1;
	};


	%DeathScreen.setName("DeathScreen");
	PlayGui_Vignette.add("DeathScreen");
	fitOverlayToParent(DeathScreen, PlayGui_Vignette);
}


// LIST IN WIP

// Global variables
$MLTextFullContent[1] = "";
$MLTextFullContent[2] = "";
$MLTextFullContent[3] = "";
$MLTextControlHeight = 129;
$MLTextLineHeight = 16;
$MLTextMaxLines = 9;

// THE RETRY IS BOUNDED. This is called at file scope, before MonsterRPGx_Main.gui
// has been exec'd (client.cs execs this file first), so the controls genuinely are
// missing on the first pass and a retry is correct - they appear a few hundred ms
// later in the same load. But an UNBOUNDED 100ms retry means that if one of the
// three controls is ever renamed or dropped from the .gui, the client runs that
// schedule ten times a second for the rest of the session with nothing to show for
// it and no message saying why. Ten seconds is far longer than the load takes.
function initScrollableText(%guiIndex, %tries)
{
    if (isObject("MonsterRPGx_ScrollMLText_" @ %guiIndex))
    {
        generateScrollableText(%guiIndex);
        return;
    }

    if (%tries $= "")
        %tries = 0;

    if (%tries >= 100)
    {
        error("MonsterRPGx: MonsterRPGx_ScrollMLText_" @ %guiIndex @ " never appeared - "
            @ "giving up after 10s. Check MonsterRPGx_Main.gui still defines it.");
        return;
    }

    schedule(100, 0, "initScrollableText", %guiIndex, %tries + 1);
}

function generateScrollableText(%guiIndex)
{
    $MLTextFullContent[%guiIndex] = "";
    
    if (%guiIndex == 1)
    {
        for (%i = 0; %i < $MonsterRPG::PlayerCount; %i++)
        {
            $MLTextFullContent[%guiIndex] = $MLTextFullContent[%guiIndex] @ "<color:43A047>" @ $MonsterRPG::Player[%i] @ " <color:C9A46B>[" @ $MonsterRPG::Player::Level[%i] @ "]\n";
        }
    }
    else if (%guiIndex == 2)
    {
        for (%i = 0; %i < $MonsterRPG::Party::MembersCount; %i++)
        {
			if($MonsterRPG::Party::Leader == $MonsterRPG::Party::Member[%i])
			{
				$MLTextFullContent[%guiIndex] = $MLTextFullContent[%guiIndex] @ "<color:43A047>" @ $MonsterRPG::Party::Member[%i] @ " <color:C9A46B>[" @ $MonsterRPG::Party::Member::Level[%i] @ "] <color:FF7043>[L]\n";
			}
			else
			{
				$MLTextFullContent[%guiIndex] = $MLTextFullContent[%guiIndex] @ "<color:43A047>" @ $MonsterRPG::Party::Member[%i] @ " <color:C9A46B>[" @ $MonsterRPG::Party::Member::Level[%i] @ "] <color:FF7043>[M]\n";
			}
        }
    }
    
    $MLTextFullContent[%guiIndex] = $MLTextFullContent[%guiIndex] @ "GHOST_LINE\n";
    
    setMLTextControlExtent(%guiIndex);
    updateMLTextScrollPosition(%guiIndex, 0);
}

function setMLTextControlExtent(%guiIndex)
{
    %mlText = "MonsterRPGx_ScrollMLText_" @ %guiIndex;
    %currentPos = %mlText.getPosition();
    %currentExtent = %mlText.getExtent();
    %x = getWord(%currentPos, 0);
    %y = getWord(%currentPos, 1);
    %width = getWord(%currentExtent, 0);
    
    %mlText.resize(%x, %y, %width, $MLTextControlHeight);
    
    %newExtent = %mlText.getExtent();

    if (getWord(%newExtent, 1) != $MLTextControlHeight)
    {
        error("WARNING: MLText " @ %guiIndex @ " height is not " @ $MLTextControlHeight @ " pixels! Adjusting...");
        %mlText.resize(%x, %y, %width, $MLTextControlHeight);
    }
}

function updateMLTextScrollPosition(%guiIndex, %scrollPercentage)
{
    %allLines = strReplace($MLTextFullContent[%guiIndex], "\n", "\t");
    %lineCount = getFieldCount(%allLines) - 1;

    %visibleLines = $MLTextMaxLines;

    %startLine = mFloor(%scrollPercentage * (%lineCount - %visibleLines));
    %startLine = mClamp(%startLine, 0, %lineCount - %visibleLines);

    $MLTextStartLine = %startLine;

    %visibleText = "";
    for (%i = %startLine; %i < %startLine + %visibleLines && %i < %lineCount; %i++)
    {
        %line = getField(%allLines, %i);
        if (%line !$= "GHOST_LINE")
        {
            %visibleText = %visibleText @ %line @ "\n";
        }
    }

    %mlText = "MonsterRPGx_ScrollMLText_" @ %guiIndex;
    %mlText.setText(%visibleText);

    setMLTextControlExtent(%guiIndex);
}

function MonsterRPGx_ScrollVerticalThumb_Generic::onMouseDown(%this, %modifier, %mousePoint, %mouseClickCount, %size)
{
    %guiIndex = getSubStr(%this.getName(), strlen(%this.getName()) - 1, 1);
    %this.dragging = true;
    %this.controlToDrag = "MonsterRPGx_ScrollVerticalThumbBitmap_" @ %guiIndex;
    %this.guiIndex = %guiIndex;
    
    %controlPos = %this.controlToDrag.getPosition();
    %this.clickOffset = VectorSub(%controlPos, %mousePoint);

    unhighlightLastClickedItem();
    
    %this.updateDragPosition(%size);
}

function MonsterRPGx_ScrollVerticalThumb_Generic::updateDragPosition(%this, %size)
{
    if (%this.dragging)
    {
        %mousePoint = Canvas.getCursorPos();
        
        %control = %this.controlToDrag;
        %w = getWord(%control.getExtent(), 0);
        %h = getWord(%control.getExtent(), 1);
        
        %track = "MonsterRPGx_ScrollVerticalTrack_" @ %this.guiIndex;
        %trackPos = %track.getPosition();
        %trackExtent = %track.getExtent();
        
        %x = getWord(%control.getPosition(), 0);
        %y = getWord(%mousePoint, 1) + getWord(%this.clickOffset, 1);
        
		if(%size == 1)
		{
        	%buffer = %h / 1.75;
        	%bufferBottom = %h / 1.75;
		}
		else
		{
			%buffer = %h / 3;
        	%bufferBottom = %h / 2.5;
		}
        
        %minY = getWord(%trackPos, 1) - %buffer;
        %maxY = getWord(%trackPos, 1) + getWord(%trackExtent, 1) - %h - %bufferBottom;
        
        %y = mClamp(%y, %minY, %maxY);
        
        %control.resize(%x, %y, %w, %h);
        
        %scrollRange = %maxY - %minY;
        %scrollPercentage = (%y - %minY) / %scrollRange;
        %scrollPercentage = mClampF(%scrollPercentage, 0.0, 1.0);
        
        updateMLTextScrollPosition(%this.guiIndex, %scrollPercentage);
        
        if (%this.dragging)
        {
            %this.dragSchedule = %this.schedule(33, "updateDragPosition");
        }
    }
}

function MonsterRPGx_ScrollVerticalThumb_Generic::onMouseUp(%this, %modifier, %mousePoint)
{
    %this.dragging = false;
    
    if (isEventPending(%this.dragSchedule))
        cancel(%this.dragSchedule);
}

function MonsterRPGx_ScrollVerticalThumb_1::onMouseDown(%this, %modifier, %mousePoint, %mouseClickCount)
{
    MonsterRPGx_ScrollVerticalThumb_Generic::onMouseDown(%this, %modifier, %mousePoint, %mouseClickCount);
}

function MonsterRPGx_ScrollVerticalThumb_1::updateDragPosition(%this, %size)
{
    MonsterRPGx_ScrollVerticalThumb_Generic::updateDragPosition(%this, %size);
}

function MonsterRPGx_ScrollVerticalThumb_1::onMouseUp(%this, %modifier, %mousePoint)
{
    MonsterRPGx_ScrollVerticalThumb_Generic::onMouseUp(%this, %modifier, %mousePoint);
}

//

function MonsterRPGx_ScrollVerticalThumb_2::onMouseDown(%this, %modifier, %mousePoint, %mouseClickCount)
{
    MonsterRPGx_ScrollVerticalThumb_Generic::onMouseDown(%this, %modifier, %mousePoint, %mouseClickCount);
}

function MonsterRPGx_ScrollVerticalThumb_2::updateDragPosition(%this, %size)
{
    MonsterRPGx_ScrollVerticalThumb_Generic::updateDragPosition(%this, %size);
}

function MonsterRPGx_ScrollVerticalThumb_2::onMouseUp(%this, %modifier, %mousePoint)
{
    MonsterRPGx_ScrollVerticalThumb_Generic::onMouseUp(%this, %modifier, %mousePoint);
}

//

function MonsterRPGx_ScrollVerticalThumb_3::onMouseDown(%this, %modifier, %mousePoint, %mouseClickCount)
{
    MonsterRPGx_ScrollVerticalThumb_Generic::onMouseDown(%this, %modifier, %mousePoint, %mouseClickCount);
}

function MonsterRPGx_ScrollVerticalThumb_3::updateDragPosition(%this, %size)
{
	%size = 1;
    MonsterRPGx_ScrollVerticalThumb_Generic::updateDragPosition(%this, %size);
}

function MonsterRPGx_ScrollVerticalThumb_3::onMouseUp(%this, %modifier, %mousePoint)
{
    MonsterRPGx_ScrollVerticalThumb_Generic::onMouseUp(%this, %modifier, %mousePoint);
}



function initMultipleScrollableTexts(%numGuis)
{
    for (%i = 1; %i <= %numGuis; %i++)
    {
        initScrollableText(%i);
    }
}

initMultipleScrollableTexts(3);

$MonsterRPG::Client::ClickedListItem = "";
$MonsterRPG::Client::HighlightedLineIndex = -1;
$MonsterRPG::Client::HoveredListItem = "";
$MonsterRPG::Client::HoverUpdatePending = false;
$MonsterRPG::Client::HighlightedItems = new SimSet();
$MonsterRPG::Client::StoredHighlightedText = "";
$MonsterRPG::ButtonControls = new SimSet();

function MonsterRPGx_GenericGUIControlList::onMouseEnter(%this)
{
    %fullName = %this.getName();
    %nameParts = strreplace(%fullName, "_", "\t");
    %listNumber = getField(%nameParts, 2);
    %controlNumber = getField(%nameParts, 3);
    %lineIndex = %controlNumber - 1 + $MLTextStartLine;

    if (!isLineEmptyOrGhost(%listNumber, %lineIndex))
    {
        $MonsterRPG::Client::HoveredListItem = %this.getName();
        %this.updateVisualState();
    }
}

function MonsterRPGx_GenericGUIControlList::onMouseLeave(%this)
{
    if ($MonsterRPG::Client::HoveredListItem $= %this.getName())
    {
        $MonsterRPG::Client::HoveredListItem = "";
    }
    %this.updateVisualState();
}

function MonsterRPGx_GenericGUIControlList::updateHoverState(%this, %isHovering)
{
    $MonsterRPG::Client::HoverUpdatePending = false;
    if (%isHovering)
    {
        $MonsterRPG::Client::HoveredListItem = %this.getName();
    }
    else if ($MonsterRPG::Client::HoveredListItem $= %this.getName())
    {
        $MonsterRPG::Client::HoveredListItem = "";
    }
    
    %this.updateVisualState();
}

function MonsterRPGx_GenericGUIControlList::onMouseDown(%this, %modifier, %mousePoint, %mouseClickCount)
{
    %fullName = %this.getName();
    %nameParts = strreplace(%fullName, "_", "\t");
    %listNumber = getField(%nameParts, 2);
    %controlNumber = getField(%nameParts, 3);
    %lineIndex = %controlNumber - 1 + $MLTextStartLine;

        // Existing code for non-button controls
        if (isLineEmptyOrGhost(%listNumber, %lineIndex))
        {
            return;
        }

        if ($MonsterRPG::Client::HighlightedItems.isMember(%this))
        {
            $MonsterRPG::Client::HighlightedItems.remove(%this);
        }
        else
        {
            if ($MonsterRPG::Client::HighlightedItems.getCount() > 0)
            {
                unhighlightAllItems();
            }
            $MonsterRPG::Client::HighlightedItems.add(%this);
        }
        
        %this.updateVisualState();
        scheduleUnhighlight();
        
        $MonsterRPG::Client::StoredHighlightedText = getHighlightedLineText(%listNumber);
}

function MonsterRPGx_GenericGUIControlList::updateVisualState(%this)
{
    %fullName = %this.getName();
    %nameParts = strreplace(%fullName, "_", "\t");
    %listNumber = getField(%nameParts, 2);
    %controlNumber = getField(%nameParts, 3);
    
    %bitmapCtrl = "MonsterRPGx_ScrollTextLine_" @ %listNumber @ "_" @ %controlNumber;
    
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
        if (isLineEmptyOrGhost(%listNumber, %lineIndex))
        {
            %alpha = 0;
        }
        else if ($MonsterRPG::Client::HighlightedItems.isMember(%this))
        {
            %alpha = 0.588;
        }
        else if ($MonsterRPG::Client::HoveredListItem $= %this.getName())
        {
            %alpha = 0.196;
        }
        else
        {
            %alpha = 0;
        }
    
    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_GenericGUIControlList::onMouseUp(%this, %modifier, %mousePoint)
{
    %this.updateVisualState();
}

function MonsterRPGx_GenericGUIControlList::setHighlighted(%this, %highlight)
{
    if (%highlight)
    {
        $MonsterRPG::Client::HighlightedItems.add(%this);
    }
    else
    {
        $MonsterRPG::Client::HighlightedItems.remove(%this);
    }
    %this.updateVisualState();
}

function MonsterRPGx_findLastOccurrence(%string, %char)
{
    %length = strlen(%string);
    for (%i = %length - 1; %i >= 0; %i--)
    {
        if (getSubStr(%string, %i, 1) $= %char)
        {
            return %i;
        }
    }
    return -1;
}

function parseNameAndLevel(%formattedString)
{
    %result = "";
    %level = "";
    
    %bracketPos = MonsterRPGx_findLastOccurrence(%formattedString, "[");
    
    if (%bracketPos != -1)
    {
        %name = trim(getSubStr(%formattedString, 0, %bracketPos));
        
        %level = getSubStr(%formattedString, %bracketPos + 1, strlen(%formattedString) - %bracketPos - 2);
        
        %result = %name TAB %level;
    }
    else
    {
        %result = %formattedString TAB "0";
    }
    
    return %result;
}

function isLineEmptyOrGhost(%listNumber, %lineIndex)
{
    %allLines = strReplace($MLTextFullContent[%listNumber], "\n", "\t");
    %lineText = trim(getField(%allLines, %lineIndex));
    return %lineText $= "" || %lineText $= "GHOST_LINE";
}

function scheduleUnhighlight()
{
    cancel($MonsterRPG::Client::UnhighlightSchedule);
    $MonsterRPG::Client::UnhighlightSchedule = schedule(5000, 0, "unhighlightAllItems");
}

function unhighlightAllItems()
{
    while ($MonsterRPG::Client::HighlightedItems.getCount() > 0)
    {
        %item = $MonsterRPG::Client::HighlightedItems.getObject(0);
        $MonsterRPG::Client::HighlightedItems.remove(%item);
        %item.updateVisualState();
    }
    cancel($MonsterRPG::Client::UnhighlightSchedule);
    
    $MonsterRPG::Client::StoredHighlightedText = "";
}

function getStoredHighlightedText()
{
    return $MonsterRPG::Client::StoredHighlightedText;
}

function unhighlightControl(%controlName)
{
    if (isObject(%controlName))
    {
        %controlName.setHighlighted(false);
        $MonsterRPG::Client::ClickedListItem = "";
    }
}

function getHighlightedLineText(%listNumber)
{
    %highlightedText = "";
    for (%i = 0; %i < $MonsterRPG::Client::HighlightedItems.getCount(); %i++)
    {
        %item = $MonsterRPG::Client::HighlightedItems.getObject(%i);
        %fullName = %item.getName();
        %nameParts = strreplace(%fullName, "_", "\t");
        %itemListNumber = getField(%nameParts, 2);
        %controlNumber = getField(%nameParts, 3);
        
        if (%itemListNumber == %listNumber)
        {
            %lineIndex = %controlNumber - 1 + $MLTextStartLine;
            %allLines = strReplace($MLTextFullContent[%listNumber], "\n", "\t");
            %lineText = getField(%allLines, %lineIndex);
            
            if (%lineText !$= "GHOST_LINE" && %lineText !$= "")
            {
                %parsedInfo = parseNameAndLevel(%lineText);
                %highlightedText = %highlightedText @ %parsedInfo @ "\n";
            }
        }
    }
    return %highlightedText;
}

function unhighlightLastClickedItem()
{
    if ($MonsterRPG::Client::ClickedListItem !$= "")
    {
        %controlName = $MonsterRPG::Client::ClickedListItem;
        if (isObject(%controlName))
        {
            %controlName.setHighlighted(false);
            $MonsterRPG::Client::ClickedListItem = "";
            $MonsterRPG::Client::HighlightedLineIndex = -1;
        }
        else
        {
            error("Last clicked item control not found: " @ %controlName);
        }
    }
    cancel($MonsterRPG::Client::UnhighlightSchedule);
}

function assignScrollTextLineMouseMethods(%listNumber, %controlNumber)
{
    %controlName = "MonsterRPGx_ScrollTextLineMouse_" @ %listNumber @ "_" @ %controlNumber;
    if (isObject(%controlName))
    {
        %bitmapCtrl = "MonsterRPGx_ScrollTextLine_" @ %listNumber @ "_" @ %controlNumber;
        
        %currentColor = %bitmapCtrl.getColor();
        %r = getWord(%currentColor, 0);
        %g = getWord(%currentColor, 1);
        %b = getWord(%currentColor, 2);
    
        %newColor = %r SPC %g SPC %b SPC 0;
        %bitmapCtrl.setColor(%newColor);

        eval("function " @ %controlName @ "::onMouseEnter(%this) { MonsterRPGx_GenericGUIControlList::onMouseEnter(%this); }");
        eval("function " @ %controlName @ "::onMouseLeave(%this) { MonsterRPGx_GenericGUIControlList::onMouseLeave(%this); }");
        eval("function " @ %controlName @ "::onMouseDown(%this, %modifier, %mousePoint, %mouseClickCount) { MonsterRPGx_GenericGUIControlList::onMouseDown(%this, %modifier, %mousePoint, %mouseClickCount); }");
        eval("function " @ %controlName @ "::onMouseUp(%this, %modifier, %mousePoint) { MonsterRPGx_GenericGUIControlList::onMouseUp(%this, %modifier, %mousePoint); }");
        eval("function " @ %controlName @ "::updateVisualState(%this) { MonsterRPGx_GenericGUIControlList::updateVisualState(%this); }");
        eval("function " @ %controlName @ "::updateHoverState(%this, %isHovering) { MonsterRPGx_GenericGUIControlList::updateHoverState(%this, %isHovering); }");
    }
    else
    {
        error("Control" SPC %controlName SPC "not found");
    }
}

function initiateMouseHandlers()
{
    for (%list = 1; %list <= 4; %list++)
    {
        for (%control = 1; %control <= 8; %control++)
        {
            assignScrollTextLineMouseMethods(%list, %control);
        }
    }
}

function MonsterRPGx_MainInvMouse::onMouseDown(%this)
{
    useRPGTab("Inv");
}

function MonsterRPGx_MainPartyMouse::onMouseDown(%this)
{
    useRPGTab("Party");
}

function MonsterRPGx_MainSkillsMouse::onMouseDown(%this)
{
    useRPGTab("Skills");
}

function MonsterRPGx_MainStatsMouse::onMouseDown(%this)
{
   useRPGTab("Stats");
}

function MonsterRPGx_MainEquipMouse::onMouseDown(%this)
{
    useRPGTab("Equip");
}

function MonsterRPGx_MainQuestMouse::onMouseDown(%this)
{
    useRPGTab("Quest");
}

//

function MonsterRPGx_PartyInviteMouse::onMouseDown(%this)
{
    MonsterRPGx_InviteMember();
}

function MonsterRPGx_PartyKickMouse::onMouseDown(%this)
{
    MonsterRPGx_KickMember();
}

function MonsterRPGx_PartyLeaveMouse::onMouseDown(%this)
{
    MonsterRPGx_PartyLeave();
}

function MonsterRPGx_PartyCreateMouse::onMouseDown(%this)
{
    MonsterRPGx_CreateParty();
}

function MonsterRPGx_PartyPromoteMouse::onMouseDown(%this)
{
    MonsterRPGx_PromoteMember();
}

// Add these visual feedback functions (optional)
function MonsterRPGx_PartyInviteMouse::onMouseEnter(%this)
{
	%bitmapCtrl = "MonsterRPGx_PartyInviteMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0.196;

	%newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_PartyInviteMouse::onMouseLeave(%this)
{
   	%bitmapCtrl = "MonsterRPGx_PartyInviteMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0;

	%newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

// Add these visual feedback functions (optional)
function MonsterRPGx_PartyKickMouse::onMouseEnter(%this)
{
	%bitmapCtrl = "MonsterRPGx_PartyKickMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0.196;

	%newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_PartyKickMouse::onMouseLeave(%this)
{
   	%bitmapCtrl = "MonsterRPGx_PartyKickMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0;

	%newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_PartyLeaveMouse::onMouseEnter(%this)
{
	%bitmapCtrl = "MonsterRPGx_PartyLeaveMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0.196;

	%newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_PartyLeaveMouse::onMouseLeave(%this)
{
   	%bitmapCtrl = "MonsterRPGx_PartyLeaveMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0;

	%newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_PartyCreateMouse::onMouseEnter(%this)
{
	%bitmapCtrl = "MonsterRPGx_PartyCreateMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0.196;

	%newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_PartyCreateMouse::onMouseLeave(%this)
{
   	%bitmapCtrl = "MonsterRPGx_PartyCreateMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0;

	%newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_PartyPromoteMouse::onMouseEnter(%this)
{
	%bitmapCtrl = "MonsterRPGx_PartyPromoteMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0.196;

	%newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_PartyPromoteMouse::onMouseLeave(%this)
{
   	%bitmapCtrl = "MonsterRPGx_PartyPromoteMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0;

	%newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_MainInvMouse::onMouseEnter(%this)
{
    %bitmapCtrl = "MonsterRPGx_MainInvMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0.196;

    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_MainInvMouse::onMouseLeave(%this)
{
    %bitmapCtrl = "MonsterRPGx_MainInvMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0;

    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_MainEquipMouse::onMouseEnter(%this)
{
    %bitmapCtrl = "MonsterRPGx_MainEquipMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0.196;

    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_MainEquipMouse::onMouseLeave(%this)
{
    %bitmapCtrl = "MonsterRPGx_MainEquipMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0;

    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_MainPartyMouse::onMouseEnter(%this)
{
    %bitmapCtrl = "MonsterRPGx_MainPartyMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0.196;

    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_MainPartyMouse::onMouseLeave(%this)
{
    %bitmapCtrl = "MonsterRPGx_MainPartyMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0;

    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_MainStatsMouse::onMouseEnter(%this)
{
    %bitmapCtrl = "MonsterRPGx_MainStatsMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0.196;

    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_MainStatsMouse::onMouseLeave(%this)
{
    %bitmapCtrl = "MonsterRPGx_MainStatsMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0;

    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_MainSkillsMouse::onMouseEnter(%this)
{
    %bitmapCtrl = "MonsterRPGx_MainSkillsMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0.196;

    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_MainSkillsMouse::onMouseLeave(%this)
{
    %bitmapCtrl = "MonsterRPGx_MainSkillsMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0;

    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_MainQuestMouse::onMouseEnter(%this)
{
    %bitmapCtrl = "MonsterRPGx_MainQuestMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0.196;

    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

function MonsterRPGx_MainQuestMouse::onMouseLeave(%this)
{
    %bitmapCtrl = "MonsterRPGx_MainQuestMSBitmap";
    %currentColor = %bitmapCtrl.getColor();
    %r = getWord(%currentColor, 0);
    %g = getWord(%currentColor, 1);
    %b = getWord(%currentColor, 2);
    
    %alpha = 0;

    %newColor = %r SPC %g SPC %b SPC %alpha;
    %bitmapCtrl.setColor(%newColor);
}

// Call this function after your GUI is loaded
//initiateMouseHandlers();

// You can create similar specific implementations for other GUI elements (e.g., MonsterRPGx_Button_2, MonsterRPGx_Checkbox_1, etc.)


//
// Dragging GUIs for MonsterRPGx

function MonsterRPGx_PlayInvMouse::onMouseDown(%this, %modifier, %mousePoint, %mouseClickCount)
{
    %this.dragging = true;
    %this.controlToDrag = MonsterRPGx_PlyrInv; // Ensure this is the correct reference

    // Offset between the cursor and the window's corner, held for the whole drag.
    %this.clickOffset = VectorSub(%this.controlToDrag.getPosition(), %mousePoint);
}

// Frame-synced drag: onMouseDragged fires once per rendered frame the cursor moves,
// so the window follows the mouse smoothly. (The old approach polled on a fixed
// 33ms schedule - ~30Hz against a 60Hz+ redraw - which is what felt jittery.)
function MonsterRPGx_PlayInvMouse::onMouseDragged(%this, %modifier, %mousePoint, %clicks)
{
    if(!%this.dragging)
        return;

    %control = %this.controlToDrag;
    %w = getWord(%control.getExtent(), 0);
    %h = getWord(%control.getExtent(), 1);
    %x = getWord(%mousePoint, 0) + getWord(%this.clickOffset, 0);
    %y = getWord(%mousePoint, 1) + getWord(%this.clickOffset, 1);
    %control.resize(%x, %y, %w, %h);
}

function MonsterRPGx_PlayInvMouse::onMouseUp(%this, %modifier, %mousePoint)
{
    %this.dragging = false;
    %this.clickOffset = "0 0";
}


function modifySwatchCtrls(%this) 
{
    %count = %this.getCount(); // Get the number of controls in the GUI
	
    for (%i = 0; %i < %count; %i++) 
    {
        %ctrl = %this.getObject(%i); // Get the control at index %i
		
        // Check if the control is a GuiSwatchCtrl (or based on your criteria)
        if (%ctrl.getClassName() $= "GuiSwatchCtrl") 
        {
            // Split the name of the control
            %nameParts = strreplace(%ctrl.getName(), "_", "\t");
			
            // Extract the parts from the split name
            %prefix = getField(%nameParts, 0);
            %cellType = getField(%nameParts, 1);
            %suffix = getField(%nameParts, 2);
            %num = getField(%nameParts, 3);
			
            // Check if the prefix and suffix match your criteria
            if (%prefix $= "MonsterRPGx" && %suffix $= "ItemBGColor") 
            {
                // Construct the name of the new bitmap control
                %newBitmapName = %prefix @ "_" @ %cellType @ "_ItemBackground_" @ %num;
				
                // Check if the bitmap control already exists
                if (!isObject(%newBitmapName)) 
                {
                    // Create the new bitmap control
                    %newBitmap = new GuiBitmapCtrl(%newBitmapName) 
                    {
                        profile = "GuiDefaultProfile";
                        position = "0 0";
                        extent = "64 64";
                        bitmap = "Add-ons/Client_MonsterRPG/GUIs/noItem.png";
                        mColor = "255 255 255 255";
					};
					
                    // Add the new bitmap control to the swatch control
                    %ctrl.add(%newBitmap);
				}
				
                // Bring the bitmap control to the front
                %ctrl.bringToFront(%newBitmapName);
			}
		}
	}
    
    for(%i = %count - 1; %i >= 0; %i--)
    {
        %child = %this.getObject(%i);
        modifySwatchCtrls(%child);
	}
}


// Dynamic FOV SUPPORT

$DynamicFOV::Itensity = 5;
$DynamicFOV::MaxFOV = 135;

function RestoreDefaultFOV()
{
	if($DynamicFOV::CurrentFOV != $pref::Player::defaultFov && !$DynamicFOV::Zooming)
	{
		setFov($pref::Player::defaultFov);
		$DynamicFOV::CurrentFOV = $pref::Player::defaultFov;
	}
}

function clientCmdDynamicFOVTick()
{
	if(isObject(ServerConnection))
	{
		if(isObject(%player = ServerConnection.getControlObject()) && !$DynamicFOV::Zooming)
		{
			$DynamicFOV::Velocity = vectorLen(%player.getVelocity());
			$DynamicFOV::CurrentFOV = $pref::Player::defaultFov + $DynamicFOV::Velocity * $DynamicFOV::Itensity;
			
			if($DynamicFOV::CurrentFOV > $DynamicFOV::MaxFOV)
			{
				$DynamicFOV::CurrentFOV = $DynamicFOV::MaxFOV;
			}
			
			SetFOV($DynamicFOV::CurrentFOV);
		}
	}
	else
	restoreDefaultFOV();
}


/// Scroll bar for spelsl

function Spells_Scroll_Event::onMouseDown(%this, %modifier, %mousePoint, %mouseClickCount)
{
    %this.dragging = true; // Flag to indicate dragging
    %this.startDragPos = %mousePoint; // Store the starting mouse position
	
    // Define the list of controls to drag
    %this.numControlsToDrag = 1; // Change this to the number of controls you want to drag
    %this.controlToDrag[0] = "Spells_Scroll_Button"; // Change this to the name of the control
	%this.controlToLimit = "Spells_Scroll_BG"; // Change this to the name of the control
}


function Spells_Scroll_Event::onMouseDragged(%this, %modifier, %mousePoint, %clicks)
{
    if (%this.dragging)
    {
        //%offset = VectorSub(%mousePoint, %this.startDragPos);
        
        // Update positions of all the GUI controls being dragged
        for (%i = 0; %i < %this.numControlsToDrag; %i++)
        {
            %w = getWord(%this.controlToDrag[%i].getExtent(),0);
            %h = getWord(%this.controlToDrag[%i].getExtent(),1);
            %x = getWord(%this.controlToDrag[%i].getPosition(),0);
            %y = getWord(%mousePoint, 1) - 150;
			
			 // Calculate the allowed Y range based on controlToLimit's extent height
            %maxY = getWord(%this.controlToLimit.getExtent(), 1) - %h;
            %y = mClamp(%y, 0, %maxY); // Ensure newY stays within limits
			
			// Limit the y by the extent of the controlToLimit
            %this.controlToDrag[%i].resize(%x,%y,%w,%h);
		}
	}
}

function Spells_Scroll_Event::onMouseUp(%this)
{
    if (%this.dragging)
    {
        %this.dragging = false;
        %this.startDragPos = "0 0";
	}
}

function scaleNewCanvas(%this)
{
	%res = getRes();
	%scrW = getWord(%res, 0);
	%scrH = getWord(%res, 1);
	%ExtAdj = %scrH / 768;
		
	%this.resize(0,0,%scrW,%scrH);
	%this.UIS_applyScaling(%ExtAdj, %scrW / 2 SPC %scrH);

	//UIS_applyScaling has just put every bar back to FULL and rewritten the geometry they
	//are drawn from, so their on-screen state is now a lie until something redraws them.
	//Nothing did: update*Bitmap only redraws when the VALUE changes, so the bars stayed
	//wrong until the player next took damage or gained exp. This is that redraw.
	//
	//Unconditional: all three call sites pass MonsterRPGx_MAIN_INTERFACE, which is the
	//tree the bars live in, and MRPG_setColbFill / MRPG_setBarWidth both no-op on a
	//control that does not exist or has never been scaled. Testing %this against the
	//interface would mean calling getId() on it, which is itself a console error on the
	//one path where it might legitimately be missing.
	MRPG_reapplyBars();
}

/// LEVEL up

function clientCmdLevelUpText(%data)
{
    %skillName = getField(%data, 0);
    %level = getField(%data, 1);
    
    %skillParts = strReplace(%skillName, " » ", " ");
    %subcategory = getWord(%skillParts, 0);
    %category = getWord(%skillParts, 1);
    
    if (%category $= "")
    {
        // This is a main category skill
        %formattedSkillName = "<color:00c800>" @ %subcategory;
    }
    else
    {
        // This is a subcategory skill
        %formattedSkillName = "<color:00c800>" @ %subcategory @ " <color:FFFFFF>>> <color:A0A0A0>" @ %category;
    }
    
    MonsterRPGx_LevelUpText.setText("\n<font:verdana bold:30><just:center><color:FFFFFF>Skill Level Up!\n<font:verdana bold:24>Your " @ %formattedSkillName @ " <color:FFFFFF>skill is now level <color:00c800>" @ %level);

    MonsterRPGx_LevelUpPopup.setVisible(1);
    MonsterRPGx_LevelUpText.forceReflow();
    
    cancel(MonsterRPGx_LevelUpPopup.SchClear);
    MonsterRPGx_LevelUpPopup.SchClear = MonsterRPGx_LevelUpPopup.schedule(5000, setVisible, 0);
    
    %pX = getWord(MonsterRPGx_LevelUpPopup.UIS_originalPosition, 0);
    %pY = getWord(MonsterRPGx_LevelUpPopup.UIS_originalPosition, 1);
    %pW = getWord(MonsterRPGx_LevelUpPopup.UIS_originalExtent, 0);
    %pH = getWord(MonsterRPGx_LevelUpPopup.UIS_originalExtent, 1);
    
    %tX = getWord(MonsterRPGx_LevelUpText.UIS_originalPosition, 0);
    %tY = getWord(MonsterRPGx_LevelUpText.UIS_originalPosition, 1);
    %tW = getWord(MonsterRPGx_LevelUpText.UIS_originalExtent, 0);
    %tH = getWord(MonsterRPGx_LevelUpText.UIS_originalExtent, 1);
    MonsterRPGx_LevelUpPopup.resize(%pX, %pY, %pW + (%tW / 2), %pH);
    MonsterRPGx_LevelUpPopup.CenterX();
    MonsterRPGx_LevelUpText.CenterX();
}