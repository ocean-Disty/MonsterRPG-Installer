//////////////////////////////////////////////////////////////////////////////
// Dialogue.cs  -  the conversation window
//////////////////////////////////////////////////////////////////////////////
//
// Replaces the TopPrint/MiddlePrint + numpad rendering for menus opened with
// showMenu(%client, 0, 2). The server still owns the menu (Core_Dialogue.cs); this only
// draws it and reports which row was clicked, so a dialogue option runs exactly the same
// evalText a centerprint option would.
//
// Themed off the constants the other panels use (RPGPanels.cs:36-47) rather than fresh
// colours, so this reads as part of the same interface:
//
//     Gold F1ECC2   speaker + accents      Name DAD9DD   body text
//     Divider 138 106 47 150               Hover 232 184 92 46
//     backdrop 8 10 14                     (same as loading / character / attribute)
//
// Runtime-built, like AttributeScreen.cs and CorpseWindow.cs.
//////////////////////////////////////////////////////////////////////////////

$Dlg_Open = 0;
$Dlg_Count = 0;
$Dlg_Hover = -1;

if($Dlg::W        $= ""){ $Dlg::W = 620; }
if($Dlg::RowH     $= ""){ $Dlg::RowH = 34; }
if($Dlg::TickMs   $= ""){ $Dlg::TickMs = 50; }
if($Dlg::MaxRows  $= ""){ $Dlg::MaxRows = 8; }

function MRPG_dlgSwatch(%parent, %name, %x, %y, %w, %h, %col)
{
	%s = new GuiSwatchCtrl(%name)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "1 1"; color = %col;
	};
	%parent.add(%s);
	return %s;
}

function MRPG_dlgText(%parent, %name, %x, %y, %w, %h)
{
	%t = new GuiMLTextCtrl(%name)
	{
		profile = "GuiMLTextProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "8 2";
		lineSpacing = "2"; selectable = "0"; autoResize = "1";
	};
	%parent.add(%t);
	return %t;
}

function MRPG_buildDialogue()
{
	if(isObject(MRPG_DlgDlg))
		return MRPG_DlgDlg;

	%res = getRes();
	%sw = getWord(%res, 0);  %sh = getWord(%res, 1);

	%dlg = new GuiControl(MRPG_DlgDlg)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = %sw SPC %sh;
	};

	//NO FULL-SCREEN BACKDROP. A conversation happens in the world - you should still see
	//the person you are talking to, and the villagers around them. This is the one MRPG
	//panel that deliberately does NOT black out the scene.
	%w = $Dlg::W;
	%x = mFloor((%sw - %w) / 2);
	%y = mFloor(%sh * 0.52);            // lower half, so the speaker stays visible above

	%panel = new GuiSwatchCtrl(MRPG_DlgPanel)
	{
		profile = "GuiDefaultProfile"; horizSizing = "center"; vertSizing = "center";
		position = %x SPC %y; extent = %w SPC 200; minExtent = "8 2";
		color = "8 10 14 238";
	};
	%dlg.add(%panel);

	MRPG_dlgSwatch(%panel, "", 0, 0, %w, 2, "150 123 63 255");        // top rule

	MRPG_dlgText(%panel, "MRPG_DlgSpeaker", 16, 12, %w - 32, 22);
	MRPG_dlgSwatch(%panel, "MRPG_DlgRule", 16, 40, %w - 32, 1, $MRPG::UI::Divider);
	MRPG_dlgText(%panel, "MRPG_DlgBody", 16, 50, %w - 32, 40);

	//Option rows. Built once at max count and shown/hidden per conversation - rebuilding
	//controls every time a menu opens is how you end up leaking GuiControls.
	for(%i = 0; %i < $Dlg::MaxRows; %i++)
	{
		%row = MRPG_dlgSwatch(%panel, "MRPG_DlgRow_" @ %i, 12, 0, %w - 24, $Dlg::RowH,
			"0 0 0 0");
		%row.setVisible(0);

		MRPG_dlgText(%row, "MRPG_DlgRowText_" @ %i, 12, 8, %w - 48, 20);

		//A stock button over the row handles the click; the swatch under it is the
		//hover wash. Same split the inventory cells use.
		%btn = new GuiBitmapButtonCtrl("MRPG_DlgRowBtn_" @ %i)
		{
			profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
			position = "0 0"; extent = (%w - 24) SPC $Dlg::RowH; minExtent = "4 4";
			text = ""; command = "MRPG_dlgPick(" @ %i @ ");";
			bitmap = ""; buttonType = "PushButton";
		};
		%row.add(%btn);
	}

	Canvas.add(%dlg);
	return %dlg;
}

function clientCmdMRPG_Dialogue(%action, %speaker, %body, %opts)
{
	if(%action $= "Close")
	{
		MRPG_closeDialogue();
		return;
	}
	if(%action !$= "Open")
		return;

	MRPG_buildDialogue();

	if(%speaker $= "")
		%speaker = "???";
	MRPG_DlgSpeaker.setText("<font:verdana bold:17><color:" @ $MRPG::UI::Gold @ ">" @ %speaker);
	MRPG_DlgBody.setText("<font:verdana bold:13><color:" @ $MRPG::UI::Name @ ">" @ %body);

	//Lay the rows out under the body text, which is variable height.
	%bodyH = getWord(MRPG_DlgBody.getExtent(), 1);
	if(%bodyH < 20) %bodyH = 20;
	%rowTop = 50 + %bodyH + 14;

	%n = getFieldCount(%opts);
	if(%n > $Dlg::MaxRows)
		%n = $Dlg::MaxRows;
	$Dlg_Count = %n;

	for(%i = 0; %i < $Dlg::MaxRows; %i++)
	{
		%row = "MRPG_DlgRow_" @ %i;
		if(%i >= %n)
		{
			%row.setVisible(0);
			continue;
		}

		%row.setVisible(1);
		%row.resize(12, %rowTop + %i * $Dlg::RowH, $Dlg::W - 24, $Dlg::RowH);
		%row.setColor((%i % 2) ? $MRPG::UI::BandOdd : $MRPG::UI::BandEven);

		%t = "MRPG_DlgRowText_" @ %i;
		%t.setText("<font:verdana bold:13><color:" @ $MRPG::UI::Name @ ">" @ getField(%opts, %i));
	}

	//Fit the panel to whatever it ended up holding.
	MRPG_DlgPanel.resize(getWord(MRPG_DlgPanel.position, 0),
	                     getWord(MRPG_DlgPanel.position, 1),
	                     $Dlg::W, %rowTop + %n * $Dlg::RowH + 14);

	if(!$Dlg_Open)
	{
		MRPG_DlgDlg.setVisible(1);
		$Dlg_Open = 1;
		$Dlg_Hover = -1;
		MRPG_dlgTick();
	}
}

//Hover wash, polled - the rows are plain swatches and this matches how the party list and
//the item tooltip already detect hovering.
function MRPG_dlgTick()
{
	cancel($Dlg_Sch);
	//Gate, and do NOT reschedule when it is shut - see ServerGate.cs rule 3.
	if(!$Dlg_Open || !MRPG_isActive())
		return;
	$Dlg_Sch = schedule($Dlg::TickMs, 0, MRPG_dlgTick);

	%cur = Canvas.getCursorPos();
	%cx = getWord(%cur, 0);  %cy = getWord(%cur, 1);
	%on = -1;

	for(%i = 0; %i < $Dlg_Count; %i++)
	{
		%row = "MRPG_DlgRow_" @ %i;
		if(!isObject(%row) || !%row.isVisible())
			continue;
		%p = %row.getCanvasPosition();  %e = %row.getExtent();
		if(%cx >= getWord(%p, 0) && %cx < getWord(%p, 0) + getWord(%e, 0)
		&& %cy >= getWord(%p, 1) && %cy < getWord(%p, 1) + getWord(%e, 1))
		{
			%on = %i;
			break;
		}
	}

	if(%on == $Dlg_Hover)
		return;

	if($Dlg_Hover >= 0 && isObject(%old = "MRPG_DlgRow_" @ $Dlg_Hover))
		%old.setColor(($Dlg_Hover % 2) ? $MRPG::UI::BandOdd : $MRPG::UI::BandEven);
	if(%on >= 0 && isObject(%new = "MRPG_DlgRow_" @ %on))
		%new.setColor($MRPG::UI::Hover);

	$Dlg_Hover = %on;
}

function MRPG_dlgPick(%index)
{
	if(!$Dlg_Open)
		return;
	//The server decides what the option does and whether the window reopens - it may be a
	//follow-up question rather than an exit. Hiding here would fight that, so this only
	//reports the click.
	commandToServer('MRPG_DialoguePick', %index);
}

function MRPG_closeDialogue()
{
	cancel($Dlg_Sch);
	$Dlg_Open = 0;
	$Dlg_Hover = -1;
	if(isObject(MRPG_DlgDlg))
		MRPG_DlgDlg.setVisible(0);
}

//Escape closes, and tells the server so the menu is released rather than left active with
//no window - which would leave %client.menu pointing at a conversation that is over.
function MRPG_dlgEscape(%val)
{
	if(!%val || !$Dlg_Open)
		return;
	MRPG_closeDialogue();
	commandToServer('MRPG_DialogueClose');
}

function MRPG_initDialogue()
{
	MRPG_buildDialogue();
	if(isObject(MRPG_DlgDlg))
		MRPG_DlgDlg.setVisible(0);
}
