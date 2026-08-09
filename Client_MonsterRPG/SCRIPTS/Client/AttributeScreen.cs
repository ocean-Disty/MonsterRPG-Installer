//////////////////////////////////////////////////////////////////////////////
// AttributeScreen.cs  -  spend attribute points, after the character exists
//////////////////////////////////////////////////////////////////////////////
//
// WHY THIS IS ITS OWN SCREEN RATHER THAN THE CHARACTER SCREEN WITH PARTS HIDDEN.
//
// The first plan was to "morph" CharacterScreen.cs: hide the pickers and re-centre what
// was left. That screen is ~2,200 lines built around race/covenant/clothing/hair/face
// pickers, proportion sliders and a race-modifier model, none of which an attribute screen
// has any use for. Hiding them leaves all of that state live and forces every future edit
// to reason about two modes at once - and the creation flow is the one screen in the game
// that must never break, because a player who cannot finish it cannot play at all.
//
// So this is a separate, small screen. It shares the SERVER side completely - the same
// MRPG_AllocStat command, the same $MRPG_StatBuf push buffer - so there is one authority
// on what a stat is worth and no second copy of the spending rules.
//
// WHAT IT DELIBERATELY DOES NOT DO
//   * no creation controls of any kind. Appearance is decided once.
//   * no local prediction. "+" asks the server and redraws from the reply; only the
//     server knows the real SkillPoints balance.
//   * no "-". A spent point is permanent (ServerCmdMRPG_AllocStat), so an undo button
//     would be a lie. Respeccing is the ability tree's business, not this screen's.
//
// INPUT. Stock GuiBitmapButtonCtrl with a command, NOT the character screen's custom
// plate+catcher system. That system keys every button into one global registry
// ($CS_BtnPlate/$CS_BtnN) hit-tested by one shared catcher, so a second screen using it
// would have its buttons hit-tested by the character screen's catcher and vice versa.
// Stock buttons handle their own input and cannot interfere.
//
// Open: MRPG_openAttrScreen();   (also bound to "J", and offered on level-up)
//////////////////////////////////////////////////////////////////////////////

$MRPG_AttrOpen = 0;

//Matches the loading screen and the character screen exactly. All three are the same
//"you are looking at your character, not at the world" surface, and a different shade
//between them reads as a rendering fault. Change one, change all three.
$MRPG::Attr::BG = "8 10 14 255";

$MRPG::Attr::List = "STR DEX VIT INT WIS CHA";

//Button art. These live under Button_Elements/, NOT directly under GUIs/ - a wrong path
//here does not error, it just renders an invisible button, which is a genuinely annoying
//thing to chase. Verified against the folder contents.
$MRPG::Attr::BtnSquare = "Add-Ons/Client_MonsterRPG/GUIs/Button_Elements/Button_square";
$MRPG::Attr::BtnLong   = "Add-Ons/Client_MonsterRPG/GUIs/Button_Elements/Button_long";

function MRPG_attrLongName(%a)
{
	if(%a $= "STR") return "Strength";
	if(%a $= "DEX") return "Dexterity";
	if(%a $= "VIT") return "Vitality";
	if(%a $= "INT") return "Intellect";
	if(%a $= "WIS") return "Wisdom";
	if(%a $= "CHA") return "Charisma";
	return %a;
}

//Short line under each name. Kept here rather than read from the server push because the
//server's own desc field is written for the Stats tab's narrow column; this screen has
//room for a sentence that actually says what the stat does in a fight.
function MRPG_attrBlurb(%a)
{
	if(%a $= "STR") return "Carry weight and the mass behind every swing.";
	if(%a $= "DEX") return "Swing speed, reach control and how fast you recover.";
	if(%a $= "VIT") return "Maximum health, and how well you take a hit.";
	if(%a $= "INT") return "Spell power and how quickly you learn.";
	if(%a $= "WIS") return "Mana pool and regeneration.";
	if(%a $= "CHA") return "Prices, persuasion and how tribes receive you.";
	return "";
}


//////////////////////////////////////////////////////////////////////////////
// BUILD
//////////////////////////////////////////////////////////////////////////////

function MRPG_buildAttrScreen()
{
	//REBUILD IF THE EXISTING DIALOG IS FROM AN OLDER VERSION OF THIS FILE. Re-exec'ing
	//client scripts in a running session is routine here, and returning on the dialog
	//alone would keep a stale screen forever while the source looked correct.
	if(isObject(MRPG_AttrDlg))
	{
		//current version (the rows exist), or open right now - either way, keep it
		if(isObject(MRPG_AttrRow_STR) || $MRPG_AttrOpen)
			return MRPG_AttrDlg;
		//stale: built by an older copy of this file, and safe to replace
		MRPG_AttrDlg.delete();
	}

	%res = getRes();
	%sw = getWord(%res, 0);  %sh = getWord(%res, 1);

	%dlg = new GuiControl(MRPG_AttrDlg)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = %sw SPC %sh;
	};

	//Full opaque cover - same reasoning as the character screen. This is a menu, not an
	//overlay on the world.
	%bg = new GuiSwatchCtrl(MRPG_AttrBg)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = %sw SPC %sh; minExtent = "8 2"; color = $MRPG::Attr::BG;
	};
	%dlg.add(%bg);

	//---- the panel, centred by arithmetic so it lands right at any resolution ----
	%pw = 760;  %ph = 560;
	%px = mFloor((%sw - %pw) / 2);
	%py = mFloor((%sh - %ph) / 2);

	%panel = new GuiSwatchCtrl(MRPG_AttrPanel)
	{
		profile = "GuiDefaultProfile"; horizSizing = "center"; vertSizing = "center";
		position = %px SPC %py; extent = %pw SPC %ph; minExtent = "8 2";
		color = "16 13 9 255";
	};
	%dlg.add(%panel);

	//gold rule top and bottom - the screen's only ornament, and enough to make it read
	//as deliberate rather than as a debug panel
	MRPG_attrSwatch(%panel, 0, 0, %pw, 2, "150 123 63 255");
	MRPG_attrSwatch(%panel, 0, %ph - 2, %pw, 2, "150 123 63 255");
	MRPG_attrSwatch(%panel, 0, 2, %pw, 64, "22 18 12 255");

	%t = MRPG_attrLabel(%panel, "MRPG_AttrTitle", 0, 18, %pw, 26);
	%t.setText("<just:center><font:verdana bold:20><color:F1ECC2>ATTRIBUTES");

	%s = MRPG_attrLabel(%panel, "MRPG_AttrPoints", 0, 42, %pw, 20);
	%s.setText("<just:center><font:verdana bold:12><color:C9A24E>-");

	//---- one row per attribute ----
	//
	//Two columns of three. A single column of six would push the panel taller than the
	//portrait beside it and leave the screen lopsided.
	%colW = mFloor(%pw / 2) - 30;
	for(%i = 0; %i < getWordCount($MRPG::Attr::List); %i++)
	{
		%a  = getWord($MRPG::Attr::List, %i);
		%cx = 24 + (%i >= 3 ? %colW + 36 : 0);
		%ry = 92 + (%i % 3) * 142;

		//Names assembled into a local first. Every other runtime-built control in this
		//add-on does it this way (CS_label, TC_label); an expression left inline in the
		//new() name slot is not a pattern that is proven here, and a control that silently
		//comes out unnamed is invisible to every isObject() lookup below.
		%rowName = "MRPG_AttrRow_" @ %a;
		%valName = "MRPG_AttrVal_" @ %a;
		%btnName = "MRPG_AttrPlus_" @ %a;

		%row = new GuiSwatchCtrl(%rowName)
		{
			profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
			position = %cx SPC %ry; extent = %colW SPC 124; minExtent = "8 2";
			color = "22 18 12 255";
		};
		%panel.add(%row);

		MRPG_attrSwatch(%row, 0, 0, %colW, 1, "60 50 32 255");

		%nm = MRPG_attrLabel(%row, "", 14, 12, %colW - 100, 22);
		%nm.setText("<font:verdana bold:16><color:F1ECC2>" @ MRPG_attrLongName(%a)
			@ "  <font:verdana bold:11><color:8A8175>" @ %a);

		%bl = MRPG_attrLabel(%row, "", 14, 38, %colW - 100, 40);
		%bl.setText("<font:verdana bold:11><color:9A948A>" @ MRPG_attrBlurb(%a));

		//the value, in its own well so it reads as a figure rather than as more text
		%well = new GuiSwatchCtrl()
		{
			profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
			position = (%colW - 92) SPC 12; extent = "56 34"; minExtent = "4 4";
			color = "12 9 6 255";
		};
		%row.add(%well);
		MRPG_attrLabel(%well, %valName, 0, 7, 56, 22);

		//"+" - a stock button so it owns its own input; see the header.
		%b = new GuiBitmapButtonCtrl(%btnName)
		{
			profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
			position = (%colW - 30) SPC 12; extent = "34 34"; minExtent = "4 4";
			text = "+"; command = "MRPG_attrSpend(\"" @ %a @ "\");";
			bitmap = $MRPG::Attr::BtnSquare;
		};
		%row.add(%b);
	}

	//---- footer ----
	%st = MRPG_attrLabel(%panel, "MRPG_AttrStatus", 0, %ph - 74, %pw, 20);
	%st.setText("");

	%close = new GuiBitmapButtonCtrl(MRPG_AttrClose)
	{
		profile = "GuiDefaultProfile"; horizSizing = "center"; vertSizing = "bottom";
		position = mFloor((%pw - 160) / 2) SPC (%ph - 48); extent = "160 32"; minExtent = "8 2";
		text = "Close"; command = "MRPG_closeAttrScreen();";
		bitmap = $MRPG::Attr::BtnLong;
	};
	%panel.add(%close);

	return %dlg;
}

//Local helpers. Deliberately NOT CS_swatch/CS_label - this screen must not depend on the
//character screen having been built, and CS_btn in particular would cross-register this
//screen's buttons into the character screen's hit-test registry.
function MRPG_attrSwatch(%parent, %x, %y, %w, %h, %col)
{
	%s = new GuiSwatchCtrl()
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "1 1"; color = %col;
	};
	%parent.add(%s);
	return %s;
}
function MRPG_attrLabel(%parent, %name, %x, %y, %w, %h)
{
	%t = new GuiMLTextCtrl(%name)
	{
		profile = "GuiMLTextProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "8 2"; lineSpacing = "2";
	};
	%parent.add(%t);
	return %t;
}


//////////////////////////////////////////////////////////////////////////////
// RENDER  -  entirely from the server's push buffer
//////////////////////////////////////////////////////////////////////////////

function MRPG_renderAttrScreen()
{
	if(!$MRPG_AttrOpen)
		return;

	%pts = $MRPG_StatPoints;
	if(%pts $= "") %pts = 0;

	for(%i = 0; %i < getWordCount($MRPG::Attr::List); %i++)
	{
		%a = getWord($MRPG::Attr::List, %i);

		%val = "";
		for(%j = 0; %j < $MRPG_StatBufCount; %j++)
			if($MRPG_StatBuf[%j, "abbr"] $= %a)
				%val = $MRPG_StatBuf[%j, "value"];

		%vc = "MRPG_AttrVal_" @ %a;
		if(isObject(%vc))
			%vc.setText("<just:center><font:verdana bold:17><color:F1ECC2>"
				@ (%val $= "" ? "-" : %val));

		//A "+" with no points behind it is a button that cannot work, so it goes away
		//rather than sitting there inviting a click that does nothing.
		%pb = "MRPG_AttrPlus_" @ %a;
		if(isObject(%pb))
			%pb.setVisible(%pts > 0);
	}

	if(isObject(MRPG_AttrPoints))
	{
		if(%pts > 0)
			MRPG_AttrPoints.setText("<just:center><font:verdana bold:13><color:FFE9A0>"
				@ %pts @ " point" @ (%pts == 1 ? "" : "s") @ " to spend");
		else
			MRPG_AttrPoints.setText("<just:center><font:verdana bold:12><color:8A8175>"
				@ "No points available - earn more by levelling up");
	}

	if(isObject(MRPG_AttrStatus))
	{
		if(%pts > 0)
			MRPG_AttrStatus.setText("<just:center><font:verdana bold:11><color:8A8175>"
				@ "Spending is permanent.");
		else
			MRPG_AttrStatus.setText("");
	}
}


//////////////////////////////////////////////////////////////////////////////
// ACTIONS
//////////////////////////////////////////////////////////////////////////////

function MRPG_attrSpend(%abbr)
{
	if($MRPG_StatPoints < 1)
		return;

	//ASK, DO NOT ASSUME. The reply (MRPG_pushStats -> clientCmdMRPG_StatDone) is what
	//redraws this screen. Incrementing here would show a point spent even when the server
	//refused, and the server is the only side that knows the true balance.
	commandToServer('MRPG_AllocStat', %abbr);
}

function MRPG_openAttrScreen()
{
	MRPG_buildAttrScreen();
	if(isObject(MRPG_AttrDlg))
		Canvas.pushDialog(MRPG_AttrDlg);
	$MRPG_AttrOpen = 1;

	//Pull fresh numbers every time. The buffer may be stale from another panel, or empty
	//on a session where nothing has asked for stats yet.
	commandToServer('MRPG_GetStats');
	MRPG_renderAttrScreen();
}

function MRPG_closeAttrScreen()
{
	$MRPG_AttrOpen = 0;
	if(isObject(MRPG_AttrDlg))
		Canvas.popDialog(MRPG_AttrDlg);
}

function MRPG_toggleAttrScreen(%val)
{
	//Key-DOWN edge AND "is this a MonsterRPG server". Off-server "j" is not bound
	//to this command at all - the broker in Keybinds.cs gives the key back on leave -
	//so this guard only catches a stray call.
	if(!MRPG_gateKey(%val))
		return;
	if($MRPG_AttrOpen)
		MRPG_closeAttrScreen();
	else
		MRPG_openAttrScreen();
}


//////////////////////////////////////////////////////////////////////////////
// LEVEL-UP OFFER
//
// The server already pushes SkillPoints on every stat sync, so noticing that the count
// went UP is enough - no new server message is needed for this.
//
// IT DOES NOT OPEN THE SCREEN BY ITSELF. Stealing the screen from someone who is mid-fight
// (levelling up happens ON a kill) would be worse than not telling them at all. It says so
// and lets them choose.
//////////////////////////////////////////////////////////////////////////////
$MRPG_AttrLastSeen = -1;

function MRPG_attrNotePoints()
{
	%now = $MRPG_StatPoints;
	if(%now $= "") %now = 0;

	if($MRPG_AttrLastSeen >= 0 && %now > $MRPG_AttrLastSeen && !$MRPG_AttrOpen)
	{
		%n = %now - $MRPG_AttrLastSeen;
		MRPG_centerPrintAttr(%n);
	}
	$MRPG_AttrLastSeen = %now;
}

function MRPG_centerPrintAttr(%n)
{
	//Bottom print rather than centre: the centre of the screen during a fight is where the
	//player is looking, and a banner across it is the last thing wanted mid-swing.
	if(isFunction("bottomPrint"))
		bottomPrint("<just:center><font:verdana bold:14>\c6You gained "
			@ %n @ " attribute point" @ (%n == 1 ? "" : "s")
			@ ".\n<font:verdana bold:12>\c3Press J to spend "
			@ (%n == 1 ? "it" : "them") @ ".", 6, 1);
}


//////////////////////////////////////////////////////////////////////////////
// KEYBIND
//////////////////////////////////////////////////////////////////////////////

// NO BIND HERE. Every MonsterRPG key is borrowed on join and handed back on
// leave by the broker in SCRIPTS/Client/Keybinds.cs, which is the only place a
// MonsterRPG key is named. A bind at file scope - which is what used to be on
// this line - writes into the one global moveMap at game launch and has no way
// to ever give it back.
