//////////////////////////////////////////////////////////////////////////////
// TreeClient.cs  -  "The Nameless Tree" in-game screen (press K)
//////////////////////////////////////////////////////////////////////////////
//
// A pannable PoE-style node GRAPH on a wood board inside a gilded frame. Nodes are
// tinted gem-orbs (Button_round tinted by state) joined by branch lines that LIGHT UP
// along owned paths and PULSE gold at the frontier you can spend into. Drag the board
// to pan; hover OR click a node to read it (a Stats-style detail card); clicking an
// available node asks to confirm the spend. Class advancement is a styled dropdown.
// The client knows the SHAPE; the server pushes state (points/alloc/opts/gold/level)
// and validates every alloc. Respec costs 100,000 gold.
//
// Panning: a GuiScrollCtrl clips (its view-rect clip propagates to grandchildren), so
// the big canvas lives in a viewport-sized pane and we pan by moving the canvas.

$TC::Branches = "steel ember hunt";
$TC::RGB::steel = "207 96 63";
$TC::RGB::ember = "189 114 166";
$TC::RGB::hunt  = "135 172 102";
$TC::RGB::gold  = "201 162 78";
$TC::RGB::lock  = "70 66 60";
$TC::RGB::conn  = "96 80 54 150";

// shared UI palette (matches the Stats menu)
$TC::UI::Gold = "F1ECC2";
$TC::UI::Name = "DAD9DD";
$TC::UI::Lock = "8A8175";
$TC::UI::Dim  = "6E6A60";
$TC::UI::Div  = "138 106 47 170";

$TC::Gfx  = "Add-Ons/Client_MonsterRPG/GUIs/";
$TC::Btn  = "Add-Ons/Client_MonsterRPG/GUIs/Button_Elements/";
$TC::NodeBmp = "Add-Ons/Client_MonsterRPG/GUIs/Button_Elements/Button_round";
$TC::Wood = "Add-Ons/Client_MonsterRPG/GUIs/avatar_wood_bg";

$TC::LaneX::steel = 360;
$TC::LaneX::ember = 980;
$TC::LaneX::hunt  = 1600;
$TC::CX  = 110;
$TC::TOP = 150;
$TC::ROW = 120;
$TC::CanvasW = 1980;
$TC::CanvasH = 1280;

// zoom: opens zoomed IN; wheel or the +/- buttons zoom out to see the whole web.
// $TC_ZoomTarget is what we EASE toward each tick; $TC_ZoomHold != 0 while a +/- button is held.
$TC_Zoom    = 1.25;
$TC_ZoomTarget = 1.25;
$TC_ZoomHold = 0;
$TC_ZoomMin = 0.42;
$TC_ZoomMax = 1.55;
$TC_CanW = 1980;
$TC_CanH = 1280;

$TC_Built = 0;
$TC_CanvasBuilt = 0;
$TC_Open = 0;
$TC_TickSch = "";


//////////////////////////////////
////////// NODE DATA /////////////
//////////////////////////////////

function TC_defAt(%id, %br, %x, %y, %type, %name, %prereq, %eff)
{
	$TC::N[%id, "br"]   = %br;   $TC::N[%id, "x"]    = %x;   $TC::N[%id, "y"]   = %y;
	$TC::N[%id, "type"] = %type; $TC::N[%id, "name"] = %name;
	$TC::N[%id, "prereq"] = %prereq; $TC::N[%id, "eff"] = %eff;
	if(%type !$= "hub")
		$TC::List = trim($TC::List SPC %id);
}
function TC_def(%id, %br, %col, %tier, %type, %name, %prereq, %eff)
{
	TC_defAt(%id, %br, TC_laneX(%br) + %col * $TC::CX, $TC::TOP + (%tier - 1) * $TC::ROW, %type, %name, %prereq, %eff);
}
function TC_laneX(%br)
{
	if(%br $= "steel") return $TC::LaneX::steel;
	if(%br $= "ember") return $TC::LaneX::ember;
	if(%br $= "hunt")  return $TC::LaneX::hunt;
	return 760;
}

function TC_initNodes()
{
	if($TC::List !$= "")
		return;

	TC_defAt("hub","gold",980,40,"hub","The Nameless","","Start of the tree. You gain 1 Ability Point per level, +1 at each class gate. Allocate any node touching one you already own.");

	// MELEE
	TC_def("sen","steel", 0,1,"notable", "Toughened Body","hub", "+Max HP. Entry into the Melee path.");
	TC_def("spa","steel", -1,2,"small", "Brawn","sen", "+Max HP.");
	TC_def("sa1","steel", 1,2,"active", "Dash","sen", "Unlocks the Dash spell.");
	TC_def("ssp","steel", 0,3,"notable", "Bloodletter","sa1", "+Max HP. (Notable)");
	TC_def("sl1","steel", -2,4,"small", "Thick Hide","ssp", "+Max HP.");
	TC_def("sl2","steel", -1,4,"small", "Rend","ssp", "+Max HP.");
	TC_def("sr1","steel", 1,4,"small", "Iron Skin","ssp", "+Max HP.");
	TC_def("sr2","steel", 2,4,"small", "Savagery","ssp", "+Max HP.");
	TC_def("sl3","steel", -2,5,"small", "Endurance","sl1", "+Max HP.");
	TC_def("sa2","steel", -1,5,"active", "Super Jump","sl2", "Unlocks the Super Jump spell.");
	TC_def("sa3","steel", 1,5,"active", "Dance of Swords","sr1", "Unlocks the Dance of Swords spell.");
	TC_def("sr3","steel", 2,5,"small", "Frenzy","sr2", "+Max HP.");
	TC_def("sl4","steel", -2,6,"notable", "Executioner","sl3", "+Max HP. (Notable)");
	TC_def("sl5","steel", -1,6,"small", "Bloodlust","sa2", "+Max HP.");
	TC_def("sr4","steel", 1,6,"small", "Cruelty","sa3", "+Max HP.");
	TC_def("sr5","steel", 2,6,"notable", "Bulwark","sr3", "+Max HP. (Notable)");
	TC_def("scv","steel", 0,7,"notable", "Warlord","sl4 sr5", "+Max HP. (Notable)");
	TC_def("slp","steel", -1,7,"small", "Fury","scv", "+Max HP.");
	TC_def("srp","steel", 1,7,"small", "Grit","scv", "+Max HP.");
	TC_def("sa4","steel", 0,8,"active", "Rocket","scv", "Unlocks the Rocket spell.");
	TC_def("sel","steel", -1,8,"small", "Vigor","slp", "+Max HP.");
	TC_def("ser","steel", 1,8,"small", "War Scars","srp", "+Max HP.");
	TC_def("sky","steel", 0,9,"key", "Berserker's Pact","sa4", "KEYSTONE: a large Max HP boost. (Full effect is a later pass.)");

	// MAGIC
	TC_def("een","ember", 0,1,"notable", "Attuned Mind","hub", "+Max Mana. Entry into the Magic path.");
	TC_def("epa","ember", -1,2,"small", "Kindling","een", "+Max Mana.");
	TC_def("ea1","ember", 1,2,"active", "Self Heal","een", "Unlocks the Self Heal spell.");
	TC_def("esp","ember", 0,3,"notable", "Emberheart","ea1", "+Max Mana. (Notable)");
	TC_def("el1","ember", -2,4,"small", "Deep Well","esp", "+Max Mana.");
	TC_def("el2","ember", -1,4,"small", "Scorch","esp", "+Max Mana.");
	TC_def("er1","ember", 1,4,"small", "Insight","esp", "+Max Mana.");
	TC_def("er2","ember", 2,4,"small", "Channeling","esp", "+Max Mana.");
	TC_def("el3","ember", -2,5,"small", "Mana Flow","el1", "+Max Mana.");
	TC_def("ea2","ember", -1,5,"active", "Cold Arrow","el2", "Unlocks the Cold Arrow spell.");
	TC_def("ea3","ember", 1,5,"active", "Heal","er1", "Unlocks the Heal spell.");
	TC_def("er3","ember", 2,5,"small", "Attunement","er2", "+Max Mana.");
	TC_def("el4","ember", -2,6,"notable", "Pyromancer","el3", "+Max Mana. (Notable)");
	TC_def("el5","ember", -1,6,"small", "Runic Skin","ea2", "+Max Mana.");
	TC_def("er4","ember", 1,6,"small", "Hexweave","ea3", "+Max Mana.");
	TC_def("er5","ember", 2,6,"notable", "Sanctifier","er3", "+Max Mana. (Notable)");
	TC_def("ecv","ember", 0,7,"notable", "Archmagus","el4 er5", "+Max Mana. (Notable)");
	TC_def("elp","ember", -1,7,"small", "Soulfont","ecv", "+Max Mana.");
	TC_def("erp","ember", 1,7,"small", "Clarity","ecv", "+Max Mana.");
	TC_def("ea4","ember", 0,8,"active", "Implosion Arrow","ecv", "Unlocks the Implosion Arrow spell.");
	TC_def("eel","ember", -1,8,"active", "Absorb","elp", "Unlocks the Absorb spell.");
	TC_def("eer","ember", 1,8,"small", "Sagecraft","erp", "+Max Mana.");
	TC_def("eky","ember", 0,9,"key", "Blood Pact","ea4", "KEYSTONE: a large Max Mana boost. (Full effect is a later pass.)");

	// ARCHERY
	TC_def("hen","hunt", 0,1,"notable", "Keen Senses","hub", "+Max HP and Mana. Entry into the Archery path.");
	TC_def("hpa","hunt", -1,2,"small", "Sharpshot","hen", "+Max HP and Mana.");
	TC_def("ha1","hunt", 1,2,"active", "Rapid Fire","hen", "Unlocks the Rapid Fire spell.");
	TC_def("hsp","hunt", 0,3,"notable", "Deadeye","ha1", "+Max HP and Mana. (Notable)");
	TC_def("hl1","hunt", -2,4,"small", "Fleetfoot","hsp", "+Max HP and Mana.");
	TC_def("hl2","hunt", -1,4,"small", "Precision","hsp", "+Max HP and Mana.");
	TC_def("hr1","hunt", 1,4,"small", "Steady Aim","hsp", "+Max HP and Mana.");
	TC_def("hr2","hunt", 2,4,"small", "Predator","hsp", "+Max HP and Mana.");
	TC_def("hl3","hunt", -2,5,"small", "Camouflage","hl1", "+Max HP and Mana.");
	TC_def("ha2","hunt", -1,5,"active", "Shotgun","hl2", "Unlocks the Shotgun spell.");
	TC_def("ha3","hunt", 1,5,"active", "Snipe","hr1", "Unlocks the Snipe spell.");
	TC_def("hr3","hunt", 2,5,"small", "Quickdraw","hr2", "+Max HP and Mana.");
	TC_def("hl4","hunt", -2,6,"notable", "Venomlord","hl3", "+Max HP and Mana. (Notable)");
	TC_def("hl5","hunt", -1,6,"small", "Keen Eye","ha2", "+Max HP and Mana.");
	TC_def("hr4","hunt", 1,6,"small", "Toxin","ha3", "+Max HP and Mana.");
	TC_def("hr5","hunt", 2,6,"notable", "Trapmaster","hr3", "+Max HP and Mana. (Notable)");
	TC_def("hcv","hunt", 0,7,"notable", "Master Hunter","hl4 hr5", "+Max HP and Mana. (Notable)");
	TC_def("hlp","hunt", -1,7,"small", "Ambush","hcv", "+Max HP and Mana.");
	TC_def("hrp","hunt", 1,7,"small", "Agility","hcv", "+Max HP and Mana.");
	TC_def("ha4","hunt", 0,8,"small", "Trailblazer","hcv", "+Max HP and Mana.");
	TC_def("hel","hunt", -1,8,"small", "Tracking","hlp", "+Max HP and Mana.");
	TC_def("her","hunt", 1,8,"small", "Sharpshot","hrp", "+Max HP and Mana.");
	TC_def("hky","hunt", 0,9,"key", "One With The Hunt","ha4", "KEYSTONE: a large Max HP and Mana boost. (Full effect is a later pass.)");

	// BRIDGES
	TC_defAt("b1","gold",670,750,"notable","Battlecaster","sr5 el4","+Max HP and Max Mana. Bridges Melee and Magic.");
	TC_defAt("b2","gold",1290,750,"notable","Spellshot","er5 hl4","+Max HP and Max Mana. Bridges Magic and Archery.");
}

function TC_has(%id)
{
	if(%id $= "hub")
		return 1;
	%a = $TC_Alloc;
	for(%i = 0; %i < getWordCount(%a); %i++)
		if(getWord(%a, %i) $= %id)
			return 1;
	return 0;
}
function TC_unlockable(%id)
{
	if(TC_has(%id) || $TC_Free < 1)
		return 0;
	%pre = $TC::N[%id, "prereq"];
	if(%pre $= "")
		return 1;
	for(%i = 0; %i < getWordCount(%pre); %i++)
		if(TC_has(getWord(%pre, %i)))
			return 1;
	return 0;
}
// state color as "R G B A" for the tinted gem-orb
function TC_stateColor(%id)
{
	if($TC::N[%id, "type"] $= "hub")   return $TC::RGB::gold @ " 255";
	if(TC_has(%id))                    return TC_brRGB($TC::N[%id, "br"]) @ " 255";
	if(TC_unlockable(%id))             return $TC::RGB::gold @ " 255";
	return $TC::RGB::lock @ " 210";
}
function TC_brRGB(%br)
{
	if(%br $= "steel") return $TC::RGB::steel;
	if(%br $= "ember") return $TC::RGB::ember;
	if(%br $= "hunt")  return $TC::RGB::hunt;
	return $TC::RGB::gold;
}
function TC_dotSize(%type)
{
	if(%type $= "hub")     return 46;
	if(%type $= "key")     return 40;
	if(%type $= "active")  return 32;
	if(%type $= "notable") return 30;
	return 20;
}
function TC_typeTag(%id)
{
	switch$($TC::N[%id, "type"])
	{
		case "hub":     return "Root of the tree";
		case "key":     return "KEYSTONE - powerful, with a trade-off";
		case "active":  return "Active node - unlocks a spell";
		case "notable": return "Notable passive";
	}
	return "Minor passive";
}
function TC_clamp(%v, %lo, %hi)
{
	if(%v < %lo) return %lo;
	if(%v > %hi) return %hi;
	return %v;
}
function TC_comma(%n)
{
	%n = mFloor(%n);
	%out = "";
	while(%n >= 1000)
	{
		%c = %n % 1000;  %n = mFloor(%n / 1000);
		if(%c < 100) %c = "0" @ %c;
		if(%c < 10)  %c = "0" @ %c;
		%out = "," @ %c @ %out;
	}
	return %n @ %out;
}


//////////////////////////////////
///////// BUILD HELPERS //////////
//////////////////////////////////

function TC_label(%parent, %name, %x, %y, %w, %h, %profile)
{
	%t = new GuiMLTextCtrl(%name)
	{
		profile = %profile; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "8 2"; lineSpacing = "2";
		allowColorChars = "1"; maxChars = "-1"; selectable = "0"; autoResize = "0";
	};
	%parent.add(%t);
	return %t;
}
// dynamic bitmap button: base plate + hidden "_Fr" hover frame + label + catcher.
// Feedback is driven by the mouse callbacks below (hover shows the frame, press
// depresses the label, action fires on release-while-hovered).
function TC_button(%parent, %mouseName, %x, %y, %w, %h, %label, %bmpBase, %bmpFrame)
{
	%b = new GuiBitmapCtrl(%mouseName @ "Plate")
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "4 4"; bitmap = %bmpBase;
	};
	%parent.add(%b);
	%b.lblY = (%h - 16) / 2;
	%fr = new GuiBitmapCtrl(%mouseName @ "Fr")
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = %w SPC %h; minExtent = "2 2"; bitmap = %bmpFrame; visible = "0";
	};
	%b.add(%fr);
	%lbl = TC_label(%b, %mouseName @ "Lbl", 0, %b.lblY, %w, 16, "GuiMLTextProfile");
	%lbl.setText("<just:center><font:verdana bold:13><color:F6EFCB>" @ %label);
	%m = new GuiMouseEventCtrl(%mouseName)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = %w SPC %h; minExtent = "4 4"; lockMouse = "0";
	};
	%b.add(%m);
}
// visual states: normal (no frame) / hover (frame) / press (frame + label nudged down)
$TC_BtnHover = "";
function TC_btnSet(%name, %state)
{
	%plate = %name @ "Plate";
	if(!isObject(%plate))
		return;
	%fr  = %name @ "Fr";
	%lbl = %name @ "Lbl";
	if(isObject(%fr))  %fr.setVisible(%state !$= "normal");
	if(isObject(%lbl)) %lbl.position = "0 " @ (%plate.lblY + (%state $= "press" ? 2 : 0));
}
function TC_btnEnter(%name) { $TC_BtnHover = %name;  TC_btnSet(%name, "hover"); }
function TC_btnLeave(%name) { if($TC_BtnHover $= %name) $TC_BtnHover = "";  TC_btnSet(%name, "normal"); }
function TC_btnDown(%name)  { $TC_BtnHover = %name; TC_btnSet(%name, "press"); }   // pressing = hovering (confirm box can appear under the cursor with no enter event)
function TC_btnUp(%name)    // returns 1 only if released while still hovered (a real click)
{
	%over = ($TC_BtnHover $= %name);
	TC_btnSet(%name, %over ? "hover" : "normal");
	return %over;
}
function TC_btnResetAll()
{
	$TC_BtnHover = "";
	TC_btnSet("TC_ChooseBtn", "normal");  TC_btnSet("TC_RespecBtn", "normal");  TC_btnSet("TC_CloseBtn", "normal");
	TC_btnSet("TC_ConfirmYes", "normal"); TC_btnSet("TC_ConfirmNo", "normal");
	TC_btnSet("TC_ZoomIn", "normal");     TC_btnSet("TC_ZoomOut", "normal");
}
function TC_goldFrame(%parent, %name, %x, %y, %w, %h)
{
	%o = new GuiSwatchCtrl() { profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = (%x - 7) SPC (%y - 7); extent = (%w + 14) SPC (%h + 14); minExtent = "2 2"; color = "30 22 12 255"; };
	%parent.add(%o);
	%g = new GuiSwatchCtrl() { profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = (%x - 4) SPC (%y - 4); extent = (%w + 8) SPC (%h + 8); minExtent = "2 2"; color = "170 138 72 255"; };
	%parent.add(%g);
	%hl = new GuiSwatchCtrl() { profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = (%x - 2) SPC (%y - 2); extent = (%w + 4) SPC (%h + 4); minExtent = "2 2"; color = "214 184 108 255"; };
	%parent.add(%hl);
	%box = new GuiSwatchCtrl(%name) { profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "8 2"; color = "24 21 27 255"; };
	%parent.add(%box);
	return %box;
}
function TC_studs(%parent, %x, %y, %w, %h)
{
	%c = "222 196 120 255";
	TC_stud(%parent, %x - 5,      %y - 5,      %c);
	TC_stud(%parent, %x + %w - 9, %y - 5,      %c);
	TC_stud(%parent, %x - 5,      %y + %h - 9, %c);
	TC_stud(%parent, %x + %w - 9, %y + %h - 9, %c);
}
function TC_stud(%parent, %x, %y, %c)
{
	%s = new GuiSwatchCtrl() { profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = "14 14"; minExtent = "2 2"; color = %c; };
	%parent.add(%s);
}


//////////////////////////////////
////////// BUILD PANEL ///////////
//////////////////////////////////

function MRPG_buildTreePanel()
{
	if($TC_Built && isObject(MonsterRPGx_TreeDlg))
		return;
	TC_initNodes();

	%dlg = new GuiControl(MonsterRPGx_TreeDlg)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = "1024 768";
	};
	%bg = new GuiSwatchCtrl(MonsterRPGx_TreeBg)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = "1024 768"; color = "0 0 0 175";
	};
	%dlg.add(%bg);

	%px = 62;  %py = 44;  %pw = 900;  %ph = 680;
	%panel = TC_goldFrame(%dlg, MonsterRPGx_TreePanel, %px, %py, %pw, %ph);

	%t = TC_label(%panel, "TC_Title", 22, 10, 480, 26, "GuiMLTextProfile");
	%t.setText("<just:left><font:verdana bold:22><color:F1ECC2>The Nameless Tree");
	TC_label(%panel, "MonsterRPGx_TreeClass", 470, 14, 408, 20, "GuiMLTextProfile");
	TC_label(%panel, "MonsterRPGx_TreePts",   22, 44, 440, 16, "GuiMLTextProfile");
	TC_label(%panel, "MonsterRPGx_TreeGold",  470, 44, 408, 16, "GuiMLTextProfile");

	// pannable wood board
	%scroll = new GuiScrollCtrl(TC_Scroll)
	{
		profile = "MRPG_PanelScrollProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "18 66"; extent = "864 486"; minExtent = "8 2";
		hScrollBar = "alwaysOff"; vScrollBar = "alwaysOff"; constantThumbHeight = "0"; willFirstRespond = "1";
	};
	$TC_ViewW = 864;  $TC_ViewH = 486;
	%content = new GuiSwatchCtrl(TC_Content)
	{
		profile = "GuiDefaultProfile"; position = "0 0"; extent = $TC_ViewW SPC $TC_ViewH; minExtent = "8 2"; color = "0 0 0 0";
	};
	%scroll.add(%content);
	%canvas = new GuiSwatchCtrl(TC_Canvas)
	{
		profile = "GuiDefaultProfile"; position = "0 0"; extent = $TC::CanvasW SPC $TC::CanvasH; minExtent = "8 2"; color = "26 18 10 255";
	};
	%content.add(%canvas);
	%panel.add(%scroll);
	MRPG_buildTreeCanvas();

	// zoom controls (top-right of the board) + a hint
	TC_button(%panel, "TC_ZoomIn",  846, 72,  34, 30, "+", $TC::Btn @ "Button_square", $TC::Btn @ "Button_square_Fr");
	TC_button(%panel, "TC_ZoomOut", 846, 106, 34, 30, "-", $TC::Btn @ "Button_square", $TC::Btn @ "Button_square_Fr");

	// ---- bottom: detail card (left) + class advancement (right) + buttons ----
	%card = new GuiSwatchCtrl() { profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "22 560"; extent = "430 110"; minExtent = "8 2"; color = "255 255 255 10"; };
	%panel.add(%card);
	%dh = TC_label(%card, "TC_DetailTitle", 12, 8, 406, 18, "GuiMLTextProfile");
	%dh.setText("<font:verdana bold:15><color:F1ECC2>The Nameless Tree");
	%div = new GuiSwatchCtrl() { profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "34 30" ; extent = "384 2"; minExtent = "1 1"; color = $TC::UI::Div; };
	%card.add(%div);
	%db = TC_label(%card, "TC_Detail", 12, 36, 406, 68, "GuiMLTextProfile");
	%db.setText("<font:verdana bold:11><color:8A8175>Hover or click a node to read it.");

	%ch = TC_label(%panel, "TC_AdvHdr", 470, 560, 408, 16, "GuiMLTextProfile");
	%ch.setText("<font:verdana bold:13><color:F1ECC2>CLASS ADVANCEMENT");
	%dd = new GuiPopUpMenuCtrl(TC_ClassDD)
	{
		profile = "MonsterRPGx_BurntGlassBlue_PopUpMenuProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "470 582"; extent = "250 26"; minExtent = "8 2"; maxPopupHeight = "120"; text = "";
	};
	%panel.add(%dd);
	TC_button(%panel, "TC_ChooseBtn", 730, 581, 148, 28, "Choose", $TC::Btn @ "Button_middle", $TC::Btn @ "Button_middle_Fr");
	TC_label(%panel, "MonsterRPGx_TreeGate", 470, 616, 408, 30, "GuiMLTextProfile");

	TC_button(%panel, "TC_RespecBtn", 470, 648, 200, 30, "Respec  -  100,000 g", $TC::Btn @ "Button_long_red", $TC::Btn @ "Button_long_Fr");
	TC_button(%panel, "TC_CloseBtn",  686, 648, 192, 30, "Close",                $TC::Btn @ "Button_long",     $TC::Btn @ "Button_long_Fr");

	TC_studs(%dlg, %px, %py, %pw, %ph);
	MRPG_buildTreeConfirm(%dlg);

	$TC_Built = 1;
}

// the graph: wood, tracked connector lines, tinted gem-orb nodes + labels, drag catcher
function MRPG_buildTreeCanvas()
{
	if($TC_CanvasBuilt || !isObject(TC_Canvas))
		return;

	%wood = new GuiBitmapCtrl(TC_Wood)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "0 0"; extent = $TC::CanvasW SPC $TC::CanvasH; minExtent = "8 2"; bitmap = $TC::Wood; wrap = "1";
	};
	TC_Canvas.add(%wood);

	$TC::ConnN = 0;
	%ids = trim("hub " @ $TC::List);
	for(%i = 0; %i < getWordCount(%ids); %i++)
	{
		%id  = getWord(%ids, %i);
		%pre = $TC::N[%id, "prereq"];
		for(%j = 0; %j < getWordCount(%pre); %j++)
		{
			%p = getWord(%pre, %j);
			TC_seg($TC::N[%p, "x"], $TC::N[%p, "y"], $TC::N[%p, "x"], $TC::N[%id, "y"], %id, %p);
			TC_seg($TC::N[%p, "x"], $TC::N[%id, "y"], $TC::N[%id, "x"], $TC::N[%id, "y"], %id, %p);
		}
	}

	for(%i = 0; %i < getWordCount(%ids); %i++)
	{
		%id = getWord(%ids, %i);
		%sz = TC_dotSize($TC::N[%id, "type"]);
		%dot = new GuiBitmapCtrl("TC_Dot_" @ %id)
		{
			profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
			position = ($TC::N[%id, "x"] - %sz / 2) SPC ($TC::N[%id, "y"] - %sz / 2);
			extent = %sz SPC %sz; minExtent = "2 2"; bitmap = $TC::NodeBmp; mColor = "128 128 128 255";
		};
		%dot.bcx = $TC::N[%id, "x"];  %dot.bcy = $TC::N[%id, "y"];  %dot.bsz = %sz;   // base geometry for zoom
		TC_Canvas.add(%dot);
		if($TC::N[%id, "type"] !$= "small")
		{
			%lb = TC_label(TC_Canvas, "TC_Lbl_" @ %id, $TC::N[%id, "x"] - 74, $TC::N[%id, "y"] + %sz / 2 + 2, 148, 14, "GuiMLTextProfile");
			%lb.blcx = $TC::N[%id, "x"];  %lb.blty = $TC::N[%id, "y"] + %sz / 2 + 2;
			%lb.setText("<just:center><shadow:1:1><shadowcolor:000000><font:verdana bold:11><color:F4EEDC>" @ $TC::N[%id, "name"]);
		}
	}

	// branch banners on the board (pan/zoom with the tree)
	MRPG_makeBranchHeader("steel", "MELEE",   "EA9678");
	MRPG_makeBranchHeader("ember", "MAGIC",   "E0A8D2");
	MRPG_makeBranchHeader("hunt",  "ARCHERY", "B0D696");

	//lockMouse = 1 IS WHAT MAKES THE DRAG SMOOTH. Without the lock a GuiMouseEventCtrl only
	//receives onMouseDragged while the cursor is still over it, so the event stream broke up
	//mid-drag and panning had to fall back to polling the cursor on a timer - the ~30Hz
	//against a 60Hz+ redraw that felt jittery. Every other drag surface in this add-on
	//(MRPG_EquipDragMouse, MonsterRPGx_PlayInvMouse) already sets this; the tree board was
	//the one that did not.
	%m = new GuiMouseEventCtrl(TC_CanvasMouse)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "0 0"; extent = $TC::CanvasW SPC $TC::CanvasH; minExtent = "8 2"; lockMouse = "1";
	};
	TC_Canvas.add(%m);

	$TC_CanvasBuilt = 1;
	MRPG_treeApplyZoom($TC_Zoom);
}

function MRPG_makeBranchHeader(%br, %text, %hex)
{
	%lb = TC_label(TC_Canvas, "TC_BrHdr_" @ %br, TC_laneX(%br) - 100, 92, 200, 20, "GuiMLTextProfile");
	%lb.bhx = TC_laneX(%br);  %lb.bhy = 92;
	%lb.setText("<just:center><shadow:1:1><shadowcolor:000000><font:verdana bold:18><color:" @ %hex @ ">" @ %text);
}

// scale every node/label/connector/banner by %z; keep the canvas + catcher sized to match
function MRPG_treeApplyZoom(%z)
{
	if(!$TC_CanvasBuilt)
		return;
	$TC_Zoom = %z;
	$TC_CanW = mFloor($TC::CanvasW * %z);
	$TC_CanH = mFloor($TC::CanvasH * %z);
	if(isObject(TC_Canvas))      TC_Canvas.extent = $TC_CanW SPC $TC_CanH;
	if(isObject(TC_Wood))        TC_Wood.resize(0, 0, $TC_CanW, $TC_CanH);
	if(isObject(TC_CanvasMouse)) TC_CanvasMouse.resize(0, 0, $TC_CanW, $TC_CanH);

	for(%i = 0; %i < $TC::ConnN; %i++)
	{
		%s = $TC::Conn[%i];
		if(isObject(%s))
			%s.resize(mFloor(%s.bsx * %z), mFloor(%s.bsy * %z), mCeil(%s.bsw * %z), mCeil(%s.bsh * %z));
	}
	%ids = trim("hub " @ $TC::List);
	for(%i = 0; %i < getWordCount(%ids); %i++)
	{
		%id  = getWord(%ids, %i);
		%dot = "TC_Dot_" @ %id;
		if(isObject(%dot))
		{
			%s = mCeil(%dot.bsz * %z);
			%dot.resize(mFloor(%dot.bcx * %z - %s / 2), mFloor(%dot.bcy * %z - %s / 2), %s, %s);
		}
		%lb = "TC_Lbl_" @ %id;
		if(isObject(%lb))
		{
			%lb.resize(mFloor(%lb.blcx * %z - 74), mFloor(%lb.blty * %z), 148, 14);
			%lb.setVisible(%z >= 0.7);   // hide labels when zoomed far out to reduce clutter
		}
	}
	%brs = "steel ember hunt";
	for(%i = 0; %i < 3; %i++)
	{
		%h = "TC_BrHdr_" @ getWord(%brs, %i);
		if(isObject(%h))
			%h.resize(mFloor(%h.bhx * %z - 100), mFloor(%h.bhy * %z), 200, 20);
	}
}

function TC_panClampX(%x)
{
	if($TC_CanW <= $TC_ViewW) return mFloor(($TC_ViewW - $TC_CanW) / 2);
	return TC_clamp(%x, $TC_ViewW - $TC_CanW, 0);
}
function TC_panClampY(%y)
{
	if($TC_CanH <= $TC_ViewH) return mFloor(($TC_ViewH - $TC_CanH) / 2);
	return TC_clamp(%y, $TC_ViewH - $TC_CanH, 0);
}

// wheel / button zoom, keeping the view centre stable
// set the desired zoom (the tick eases toward it). Wheel + buttons both call this.
function MRPG_treeWheel(%dir)
{
	$TC_ZoomTarget = TC_clamp($TC_ZoomTarget + %dir * 0.14, $TC_ZoomMin, $TC_ZoomMax);
}
// snap zoom to an absolute value, keeping the view centre stable
function MRPG_treeZoomTo(%z)
{
	%old = $TC_Zoom;
	if(%z == %old)
		return;
	%cx = (-getWord(TC_Canvas.position, 0) + $TC_ViewW / 2) / %old;
	%cy = (-getWord(TC_Canvas.position, 1) + $TC_ViewH / 2) / %old;
	MRPG_treeApplyZoom(%z);
	%nx = TC_panClampX(-(%cx * %z - $TC_ViewW / 2));
	%ny = TC_panClampY(-(%cy * %z - $TC_ViewH / 2));
	TC_Canvas.position = mFloor(%nx) SPC mFloor(%ny);
}

function TC_seg(%ax, %ay, %bx, %by, %child, %parent)
{
	%x = getMin(%ax, %bx);  %y = getMin(%ay, %by);
	%w = mAbs(%bx - %ax);   %h = mAbs(%by - %ay);
	if(%w < 4) { %w = 4; %x -= 2; }
	if(%h < 4) { %h = 4; %y -= 2; }
	%s = new GuiSwatchCtrl()
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "1 1"; color = $TC::RGB::conn;
	};
	%s.tcChild = %child;  %s.tcParent = %parent;
	%s.bsx = %x;  %s.bsy = %y;  %s.bsw = %w;  %s.bsh = %h;   // base rect for zoom
	TC_Canvas.add(%s);
	$TC::Conn[$TC::ConnN] = %s;
	$TC::ConnN++;
}

function MRPG_buildTreeConfirm(%dlg)
{
	%cf = new GuiSwatchCtrl(TC_Confirm)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = "1024 768"; color = "0 0 0 155"; visible = "0";
	};
	%veil = new GuiMouseEventCtrl(TC_ConfirmVeil)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = "1024 768"; minExtent = "8 2"; lockMouse = "0";
	};
	%cf.add(%veil);

	%bx = 300;  %by = 280;  %bw = 424;  %bh = 208;
	%box = TC_goldFrame(%cf, TC_ConfirmBox, %bx, %by, %bw, %bh);
	TC_label(%box, "TC_ConfirmText", 20, 20, %bw - 40, %bh - 84, "GuiCustomMLTextProfile");
	TC_button(%cf, "TC_ConfirmYes", %bx + 34,        %by + %bh - 46, 160, 32, "Confirm", $TC::Btn @ "Button_middle",      $TC::Btn @ "Button_middle_Fr");
	TC_button(%cf, "TC_ConfirmNo",  %bx + %bw - 194, %by + %bh - 46, 160, 32, "Cancel",  $TC::Btn @ "Button_middle_dark", $TC::Btn @ "Button_middle_Fr");
	TC_studs(%cf, %bx, %by, %bw, %bh);
	%dlg.add(%cf);
}


//////////////////////////////////
//////////// RENDER //////////////
//////////////////////////////////

function MRPG_renderTree()
{
	if(!$TC_Built)
		return;

	if(isObject(MonsterRPGx_TreePts))
		MonsterRPGx_TreePts.setText("<font:verdana bold:13><color:F1ECC2>Ability Points: <color:FFE9A0>" @ $TC_Free @ " <color:8A8175>/ " @ $TC_Earned @ " earned");
	if(isObject(MonsterRPGx_TreeGold))
		MonsterRPGx_TreeGold.setText("<just:right><font:verdana bold:13><color:8A8175>Gold: <color:E7C766>" @ TC_comma($TC_Gold));
	if(isObject(MonsterRPGx_TreeClass))
		MonsterRPGx_TreeClass.setText("<just:right><font:verdana bold:13><color:F1ECC2>Class: <color:DAD9DD>" @ ($TC_Title $= "" ? "Classless (Lv " @ $TC_Level @ ")" : $TC_Title));

	// gem-orbs + collect frontier for the pulse loop
	$TC_FrontDots = "";
	%ids = trim("hub " @ $TC::List);
	for(%i = 0; %i < getWordCount(%ids); %i++)
	{
		%id  = getWord(%ids, %i);
		%dot = "TC_Dot_" @ %id;
		if(!isObject(%dot))
			continue;
		%dot.mColor = TC_stateColor(%id);
		if(TC_unlockable(%id))
			$TC_FrontDots = trim($TC_FrontDots SPC %dot);
	}

	// connector lines: owned path lit in branch colour, frontier pulses, rest dim
	$TC_FrontConns = "";
	for(%i = 0; %i < $TC::ConnN; %i++)
	{
		%s = $TC::Conn[%i];
		if(!isObject(%s))
			continue;
		%ch = %s.tcChild;  %pa = %s.tcParent;
		if(TC_has(%ch) && TC_has(%pa))
			%s.color = TC_brRGB($TC::N[%ch, "br"]) @ " 240";
		else if(TC_has(%pa) && TC_unlockable(%ch))
		{
			%s.color = "150 123 63 235";
			$TC_FrontConns = trim($TC_FrontConns SPC %s);
		}
		else
			%s.color = $TC::RGB::conn;
	}

	MRPG_renderAdvance();
}

// class-advancement dropdown + requirement line
function MRPG_renderAdvance()
{
	if($TC_OptStatus $= "OPEN")
	{
		if(isObject(TC_ClassDD))
		{
			TC_ClassDD.setVisible(1);
			TC_ClassDD.clear();
			for(%k = 0; %k < getWordCount($TC_Opts); %k++)
				TC_ClassDD.add(getWord($TC_Opts, %k), %k + 1);
			TC_ClassDD.setSelected(1);
		}
		if(isObject(TC_ChooseBtnPlate)) TC_ChooseBtnPlate.setVisible(1);
		if(isObject(MonsterRPGx_TreeGate))
			MonsterRPGx_TreeGate.setText("<font:verdana bold:12><color:FFE9A0>* " @ $TC_OptGate @ " advancement is ready.\n<color:8A8175>Choose a path, then press Choose.");
	}
	else
	{
		if(isObject(TC_ClassDD)) TC_ClassDD.setVisible(0);
		if(isObject(TC_ChooseBtnPlate)) TC_ChooseBtnPlate.setVisible(0);
		if(isObject(MonsterRPGx_TreeGate))
		{
			if($TC_OptStatus $= "WAIT")
				MonsterRPGx_TreeGate.setText("<font:verdana bold:12><color:DAD9DD>Next: " @ $TC_OptGate @ " class tier.\n<color:8A8175>Requires <color:DAD9DD>level " @ $TC_Opts @ "<color:8A8175> - you are level " @ $TC_Level @ ".");
			else
				MonsterRPGx_TreeGate.setText("<font:verdana bold:12><color:8A8175>Your class path is fully advanced.");
		}
	}
}


//////////////////////////////////
////// PULSE + HOVER TICK /////////
//////////////////////////////////

function MRPG_treeTick()
{
	cancel($TC_TickSch);
	//Gate, and do NOT reschedule when it is shut - see ServerGate.cs rule 3.
	if(!$TC_Open || !MRPG_isActive())
		return;

	// --- zoom: hold a +/- button to keep zooming; ease toward the target every tick ---
	if($TC_ZoomHold != 0)
		$TC_ZoomTarget = TC_clamp($TC_ZoomTarget + $TC_ZoomHold * 0.05, $TC_ZoomMin, $TC_ZoomMax);
	if(mAbs($TC_ZoomTarget - $TC_Zoom) > 0.002)
	{
		%nz = $TC_Zoom + ($TC_ZoomTarget - $TC_Zoom) * 0.45;
		if(mAbs($TC_ZoomTarget - %nz) < 0.006)
			%nz = $TC_ZoomTarget;
		MRPG_treeZoomTo(%nz);
	}

	// --- drag: SAFETY NET ONLY ---
	//The smooth path is TC_CanvasMouse::onMouseDragged, which runs per rendered frame. This
	//catches the case where the drag event stream stalls (cursor parked but button still
	//held, or a frame where no move event arrived) so the board can never be left behind
	//the cursor. TC_dragTo is absolute, so this cannot double-pan on frames where both run.
	if($TC_Dragging)
		TC_dragTo("");

	// triangle-wave gold pulse on the frontier (nodes you can spend into)
	$TC_Phase++;
	%f = mAbs(($TC_Phase % 24) - 12) / 12.0;
	%b = mFloor(150 + %f * 105);
	%pulse = %b SPC mFloor(%b * 0.80) SPC mFloor(%b * 0.40);
	%fd = $TC_FrontDots;
	for(%i = 0; %i < getWordCount(%fd); %i++)
	{
		%o = getWord(%fd, %i);
		if(isObject(%o)) %o.mColor = %pulse @ " 255";
	}
	%fc = $TC_FrontConns;
	for(%i = 0; %i < getWordCount(%fc); %i++)
	{
		%o = getWord(%fc, %i);
		if(isObject(%o)) %o.color = %pulse @ " 235";
	}

	// hover: poll the cursor (reliable, matches the Stats-menu tip loop)
	if(!$TC_Dragging && (!isObject(TC_Confirm) || !TC_Confirm.isVisible()))
	{
		%id = TC_nodeAtCursor();
		if(%id !$= $TC_Hover)
		{
			$TC_Hover = %id;
			if(%id !$= "")
				MRPG_treeShowInfo(%id);
		}
	}

	$TC_TickSch = schedule(30, 0, "MRPG_treeTick");
}


//////////////////////////////////
////////// INTERACTION ///////////
//////////////////////////////////

function TC_nodeAtCursor()
{
	%cur = Canvas.getCursorPos();
	%cx = getWord(%cur, 0);  %cy = getWord(%cur, 1);
	%ids = trim("hub " @ $TC::List);
	for(%i = 0; %i < getWordCount(%ids); %i++)
	{
		%id  = getWord(%ids, %i);
		%dot = "TC_Dot_" @ %id;
		if(!isObject(%dot))
			continue;
		%p = %dot.getCanvasPosition();  %px = getWord(%p, 0);  %py = getWord(%p, 1);
		%e = %dot.getExtent();          %pw = getWord(%e, 0);  %ph = getWord(%e, 1);
		if(%cx >= %px - 5 && %cx < %px + %pw + 5 && %cy >= %py - 5 && %cy < %py + %ph + 5)
			return %id;
	}
	return "";
}

function MRPG_treeShowInfo(%id)
{
	if(TC_has(%id))
		{ %state = "owned"; %sc = "9BE29B"; }
	else if($TC::N[%id, "type"] $= "hub")
		{ %state = "root"; %sc = "FFE9A0"; }
	else if(TC_unlockable(%id))
		{ %state = "available - 1 point"; %sc = "FFE9A0"; }
	else
		{ %state = "locked"; %sc = "9A948A"; }

	if(isObject(TC_DetailTitle))
		TC_DetailTitle.setText("<font:verdana bold:15><color:F1ECC2>" @ $TC::N[%id, "name"] @ "  <font:verdana bold:11><color:" @ %sc @ ">(" @ %state @ ")");
	if(isObject(TC_Detail))
		TC_Detail.setText("<font:verdana bold:10><color:C9A24E>" @ TC_typeTag(%id) @ "\n<font:verdana bold:12><color:DAD9DD>" @ $TC::N[%id, "eff"]);
}

// Move the board to follow a cursor position.
//
// ABSOLUTE, NOT INCREMENTAL: the offset is always measured from where the drag began, so
// calling this twice in one frame lands on the same place rather than panning twice. That
// is what lets the event path and the tick safety net below coexist without fighting.
//
// TWO ANCHORS, ONE PER SOURCE. The drag event reports its own point and the tick reads
// Canvas.getCursorPos(); each is measured against an anchor captured from that same source
// at mouse-down. They are believed to be the same coordinate space, but nothing here has to
// rely on that - if they ever differ, each path is still internally consistent instead of
// the board snapping whenever the safety net fires.
function TC_dragTo(%pt)
{
	if(!$TC_Dragging || !isObject(TC_Canvas))
		return;

	if(%pt $= "")
	{
		%pt = Canvas.getCursorPos();
		%ax = $TC_DownCX;  %ay = $TC_DownCY;
	}
	else
	{
		%ax = $TC_DownX;   %ay = $TC_DownY;
	}

	%dx = getWord(%pt, 0) - %ax;
	%dy = getWord(%pt, 1) - %ay;

	//past a few pixels this stops being a click and becomes a pan, so mouse-up must not
	//also select whatever node it happens to finish over
	if(mAbs(%dx) + mAbs(%dy) > 4)
		$TC_Moved = 1;

	TC_Canvas.position = TC_panClampX($TC_CanX + %dx) SPC TC_panClampY($TC_CanY + %dy);
}

function TC_CanvasMouse::onMouseDown(%this, %modifier, %mousePoint, %clicks)
{
	//One anchor per source - see TC_dragTo.
	%e = (%mousePoint $= "") ? Canvas.getCursorPos() : %mousePoint;
	$TC_DownX  = getWord(%e, 0);  $TC_DownY  = getWord(%e, 1);

	%c = Canvas.getCursorPos();
	$TC_DownCX = getWord(%c, 0);  $TC_DownCY = getWord(%c, 1);

	$TC_CanX  = getWord(TC_Canvas.position, 0);
	$TC_CanY  = getWord(TC_Canvas.position, 1);
	$TC_Dragging = 1;  $TC_Moved = 0;
}
// FRAME-SYNCED. onMouseDragged fires once per rendered frame the cursor moves, so the board
// tracks the mouse at the display's own rate (16ms at 60fps, 7ms at 144) instead of the
// fixed 30ms poll this used to use - a ~30Hz update against a 60Hz+ redraw, which aliases
// and is exactly what read as choppy. Same pattern as MRPG_titleDragMove in Equipment.cs.
// MRPG_treeTick still pans as a safety net; see TC_dragTo on why that is harmless.
function TC_CanvasMouse::onMouseDragged(%this, %modifier, %mousePoint, %clicks)
{
	TC_dragTo(%mousePoint);
}
function TC_CanvasMouse::onMouseWheelUp(%this)   { MRPG_treeWheel(1); }
function TC_CanvasMouse::onMouseWheelDown(%this) { MRPG_treeWheel(-1); }
function TC_CanvasMouse::onMouseUp(%this)
{
	%moved = $TC_Moved;
	$TC_Dragging = 0;
	if(%moved)
		return;
	%id = TC_nodeAtCursor();
	if(%id !$= "")
		MRPG_treeNodeClick(%id);
}
function TC_CanvasMouse::onMouseMove(%this)
{
	%id = TC_nodeAtCursor();
	if(%id $= $TC_Hover)
		return;
	$TC_Hover = %id;
	if(%id !$= "")
		MRPG_treeShowInfo(%id);
}

function MRPG_treeNodeClick(%id)
{
	MRPG_treeShowInfo(%id);
	if($TC::N[%id, "type"] $= "hub" || TC_has(%id))
		return;
	if(TC_unlockable(%id))
	{
		MRPG_treeConfirmOpen("node", %id);
		return;
	}
	if($TC_Free < 1)
		TC_Detail.setText("<font:verdana bold:12><color:8A8175>No ability points to spend. Level up or cross a class gate first.");
	else
		TC_Detail.setText("<font:verdana bold:12><color:8A8175>Locked - allocate a connected node first.\n<color:DAD9DD>" @ $TC::N[%id, "eff"]);
}

function MRPG_treeConfirmOpen(%mode, %id)
{
	$TC_PendMode = %mode;
	$TC_PendNode = %id;
	if(%mode $= "node")
		%txt = "<just:center><font:verdana bold:15><color:F1ECC2>Spend 1 Ability Point on\n<color:FFE9A0>" @ $TC::N[%id, "name"] @ "<color:F1ECC2>?\n\n<font:verdana bold:12><color:C8C2B4>" @ $TC::N[%id, "eff"];
	else
		%txt = "<just:center><font:verdana bold:15><color:F1ECC2>Respec the entire tree for\n<color:E7C766>100,000 gold<color:F1ECC2>?\n\n<font:verdana bold:12><color:C8C2B4>Every allocated point returns to you.";
	if(isObject(TC_ConfirmText))
		TC_ConfirmText.setText(%txt);
	TC_btnResetAll();
	if(isObject(TC_Confirm))
		TC_Confirm.setVisible(1);
}
function TC_ConfirmVeil::onMouseDown(%this) { }

// the actual actions, fired by each button's release-while-hovered
function TC_actConfirmYes()
{
	if(isObject(TC_Confirm)) TC_Confirm.setVisible(0);
	if($TC_PendMode $= "node")
		commandToServer('MRPG_AllocNode', $TC_PendNode);
	else if($TC_PendMode $= "respec")
		commandToServer('MRPG_TreeRespec');
}
function TC_actConfirmNo() { if(isObject(TC_Confirm)) TC_Confirm.setVisible(0); }
function TC_actChoose()
{
	if(!isObject(TC_ClassDD))
		return;
	%sel = TC_ClassDD.getText();
	if(%sel $= "")
		return;
	commandToServer('MRPG_ClassPick', %sel);
}
function TC_actRespec() { MRPG_treeConfirmOpen("respec", ""); }
function TC_actClose()  { MRPG_closeTree(); }

function TC_ConfirmYes::onMouseEnter(%t) { TC_btnEnter("TC_ConfirmYes"); }
function TC_ConfirmYes::onMouseLeave(%t) { TC_btnLeave("TC_ConfirmYes"); }
function TC_ConfirmYes::onMouseDown(%t)  { TC_btnDown("TC_ConfirmYes"); }
function TC_ConfirmYes::onMouseUp(%t)    { if(TC_btnUp("TC_ConfirmYes")) TC_actConfirmYes(); }

function TC_ConfirmNo::onMouseEnter(%t) { TC_btnEnter("TC_ConfirmNo"); }
function TC_ConfirmNo::onMouseLeave(%t) { TC_btnLeave("TC_ConfirmNo"); }
function TC_ConfirmNo::onMouseDown(%t)  { TC_btnDown("TC_ConfirmNo"); }
function TC_ConfirmNo::onMouseUp(%t)    { if(TC_btnUp("TC_ConfirmNo")) TC_actConfirmNo(); }

function TC_ChooseBtn::onMouseEnter(%t) { TC_btnEnter("TC_ChooseBtn"); }
function TC_ChooseBtn::onMouseLeave(%t) { TC_btnLeave("TC_ChooseBtn"); }
function TC_ChooseBtn::onMouseDown(%t)  { TC_btnDown("TC_ChooseBtn"); }
function TC_ChooseBtn::onMouseUp(%t)    { if(TC_btnUp("TC_ChooseBtn")) TC_actChoose(); }

function TC_RespecBtn::onMouseEnter(%t) { TC_btnEnter("TC_RespecBtn"); }
function TC_RespecBtn::onMouseLeave(%t) { TC_btnLeave("TC_RespecBtn"); }
function TC_RespecBtn::onMouseDown(%t)  { TC_btnDown("TC_RespecBtn"); }
function TC_RespecBtn::onMouseUp(%t)    { if(TC_btnUp("TC_RespecBtn")) TC_actRespec(); }

function TC_CloseBtn::onMouseEnter(%t) { TC_btnEnter("TC_CloseBtn"); }
function TC_CloseBtn::onMouseLeave(%t) { TC_btnLeave("TC_CloseBtn"); }
function TC_CloseBtn::onMouseDown(%t)  { TC_btnDown("TC_CloseBtn"); }
function TC_CloseBtn::onMouseUp(%t)    { if(TC_btnUp("TC_CloseBtn")) TC_actClose(); }

// zoom buttons are press-and-HOLD: down starts continuous zoom (tick), up/leave stops it
function TC_ZoomIn::onMouseEnter(%t) { TC_btnEnter("TC_ZoomIn"); }
function TC_ZoomIn::onMouseLeave(%t) { $TC_ZoomHold = 0; TC_btnLeave("TC_ZoomIn"); }
function TC_ZoomIn::onMouseDown(%t)  { TC_btnDown("TC_ZoomIn"); $TC_ZoomHold = 1;  MRPG_treeWheel(1); }
function TC_ZoomIn::onMouseUp(%t)    { $TC_ZoomHold = 0; TC_btnUp("TC_ZoomIn"); }

function TC_ZoomOut::onMouseEnter(%t) { TC_btnEnter("TC_ZoomOut"); }
function TC_ZoomOut::onMouseLeave(%t) { $TC_ZoomHold = 0; TC_btnLeave("TC_ZoomOut"); }
function TC_ZoomOut::onMouseDown(%t)  { TC_btnDown("TC_ZoomOut"); $TC_ZoomHold = -1; MRPG_treeWheel(-1); }
function TC_ZoomOut::onMouseUp(%t)    { $TC_ZoomHold = 0; TC_btnUp("TC_ZoomOut"); }

// reliable mouse-wheel capture: an ActionMap on the mouse Z-axis, pushed while the tree is open
function MRPG_treeEnsureZoomMap()
{
	if(isObject(TC_ZoomMap))
		return;
	%m = new ActionMap(TC_ZoomMap);
	%m.bind(mouse, "zaxis", MRPG_treeWheelAxis);
}
function MRPG_treeWheelAxis(%val)
{
	if(%val > 0)      MRPG_treeWheel(1);
	else if(%val < 0) MRPG_treeWheel(-1);
}


//////////////////////////////////
////////// OPEN / CLOSE //////////
//////////////////////////////////

function MRPG_openTree()
{
	MRPG_buildTreePanel();
	commandToServer('MRPG_TreeOpen');
	if(isObject(MonsterRPGx_TreeDlg))
		canvas.pushDialog(MonsterRPGx_TreeDlg);
	if(isObject(TC_Confirm))
		TC_Confirm.setVisible(0);
	// open zoomed IN, centred on the hub / starting area
	$TC_Zoom = 1.25;  $TC_ZoomTarget = 1.25;  $TC_ZoomHold = 0;
	MRPG_treeApplyZoom($TC_Zoom);
	if(isObject(TC_Canvas))
		TC_Canvas.position = TC_panClampX(-(980 * $TC_Zoom - $TC_ViewW / 2)) SPC TC_panClampY(0);
	$TC_Hover = "";  $TC_Phase = 0;  $TC_Dragging = 0;
	$TC_Open = 1;

	//Assert the mouse lock on every open rather than trusting the value baked in at build
	//time. MRPG_buildTreeCanvas early-returns on $TC_CanvasBuilt, and that global survives a
	//script re-exec, so a canvas built before this setting existed would otherwise keep the
	//old unlocked control - and the drag would silently stay on the choppy fallback path.
	if(isObject(TC_CanvasMouse))
		TC_CanvasMouse.lockMouse = 1;
	MRPG_treeEnsureZoomMap();
	TC_ZoomMap.push();          // capture the mouse wheel for zooming
	TC_btnResetAll();
	MRPG_renderTree();
	MRPG_treeTick();
}
function MRPG_closeTree()
{
	$TC_Open = 0;  $TC_ZoomHold = 0;  $TC_Dragging = 0;
	cancel($TC_TickSch);
	if(isObject(TC_ZoomMap))
		TC_ZoomMap.pop();       // give the wheel back to the game
	if(isObject(MonsterRPGx_TreeDlg))
		canvas.popDialog(MonsterRPGx_TreeDlg);
}
function MRPG_toggleTree(%val)
{
	//Key-DOWN edge AND "is this a MonsterRPG server". Off-server "k" is not bound
	//to this command at all - see AttributeScreen.cs and Keybinds.cs.
	if(!MRPG_gateKey(%val))
		return;
	if($TC_Open)
		MRPG_closeTree();
	else
		MRPG_openTree();
}


//////////////////////////////////
////////// SERVER FEED ///////////
//////////////////////////////////

function clientCmdMRPG_TreeState(%data)
{
	$TC_Free   = getField(%data, 0);
	$TC_Earned = getField(%data, 1);
	$TC_Title  = getField(%data, 2);
	$TC_Tier   = getField(%data, 3);
	$TC_Gold   = getField(%data, 4);
	$TC_Level  = getField(%data, 5);
}
function clientCmdMRPG_TreeAlloc(%data)
{
	$TC_Alloc = %data;
}
function clientCmdMRPG_TreeOpts(%data)
{
	$TC_OptStatus = getField(%data, 0);
	$TC_OptGate   = getField(%data, 1);
	$TC_Opts      = getField(%data, 2);
	if($TC_Open)
		MRPG_renderTree();
}

// NO BIND HERE. Every MonsterRPG key is borrowed on join and handed back on
// leave by the broker in SCRIPTS/Client/Keybinds.cs, which is the only place a
// MonsterRPG key is named. A bind at file scope - which is what used to be on
// this line - writes into the one global moveMap at game launch and has no way
// to ever give it back.
