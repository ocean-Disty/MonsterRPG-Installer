//////////////////////////////////////////////////////////////////////////////
// SPELLS PANEL (modular companion to the Equip tab)
//////////////////////////////////////////////////////////////////////////////
//
// A scroll-list of the player's class spells (top) + a 2-slot hotbar (bottom) for
// the Jet and Light key binds, styled like the other menus. It is a child of
// MonsterRPGx_PlyrInv (the main window) sitting just RIGHT of the Equip content,
// laced to it by a chain, so it moves with the window and shows/hides with Equip
// (Support.cs useRpgTab -> MRPG_showSpells).
//
// Data is server-driven and modular:
//   MRPG_SpellClear / MRPG_SpellRow (<display> TAB <level> TAB <desc> TAB <internal>) / MRPG_SpellDone
//   MRPG_SpellBinds (<jetInternal> TAB <lightInternal>)   - current binds
//   MRPG_SpellCooldown (<totalSeconds>)                   - start a cooldown sweep
// Client requests the list with commandToServer('MRPG_GetSpells') when it opens.
//
// Mapping to the hotbar: click a spell row to select it, then click the Jet or
// Light slot to bind it (commandToServer bindJet/bindLight). A slot on cooldown
// grays over and the gray drains away (bottom-anchored, like the HP/Mana orbs).

$MRPG_SpellsBuilt   = 0;
$MRPG_SpellBufCount = 0;
$MRPG::Spells::Gfx  = "Add-Ons/Client_MonsterRPG/GUIs/";

// Placement relative to the main window (PlyrInv-local). Left edge sits on the Equip
// panel's right edge so the chain laces the two together. Tweak here.
$MRPG::Spells::X = 498;
$MRPG::Spells::Y = 13;
$MRPG::Spells::W = 220;
$MRPG::Spells::H = 548;

// Currently selected spell (for the next slot click).
$MRPG_SelSpell        = "";
$MRPG_SelSpellDisplay = "";

// Cooldown sweep state (client-side, driven by MRPG_SpellCooldown).
$MRPG_SpellCD_Total = 0;
$MRPG_SpellCD_Start = 0;
$MRPG_SpellCD_Sch   = "";


//////////////////////////////////
////////// BUILD / SHOW //////////
//////////////////////////////////

function MRPG_buildSpells()
{
	if($MRPG_SpellsBuilt && isObject(MonsterRPGx_SpellsPanel))
		return;
	if(!isObject(MonsterRPGx_Stats))
		return;

	%host = MonsterRPGx_Stats.getGroup();   // MonsterRPGx_PlyrInv (the main window)
	if(!isObject(%host))
		return;

	// The Spells layout lives in GUIs/MonsterRPGx_Spells.gui (free-floating - no chain).
	// Slot the panel onto the main window; the 12-slot bind grid (MRPG_BindSlot_1..12)
	// and the server-fed spell list are wired here, and the HUD hotbar (SpellBar.cs)
	// mirrors the binds + shows cooldowns.
	if(!isObject(MonsterRPGx_SpellsPanel))
		return; // MonsterRPGx_Spells.gui not loaded - nothing to slot in

	%host.add(MonsterRPGx_SpellsPanel); // idempotent reparent onto the main window

	MRPG_refreshBindGrid();       // reflect current binds on the menu grid
	MRPG_buildHudSpellBar();      // ensure the HUD hotbar exists (idempotent)

	$MRPG_SpellsBuilt = 1;
	MRPG_renderSpells();
}

// One hotbar slot: frame + icon + draining cooldown overlay + key label + bound name.
function MRPG_addSpellSlot(%parent, %key, %x, %y, %w, %h, %label)
{
	%slot = new GuiSwatchCtrl("MRPG_SpellSlot_" @ %key)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "8 2"; color = "0 0 0 0";
	};
	%parent.add(%slot);

	%frame = new GuiBitmapCtrl()
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "0 0"; extent = %w SPC %h; minExtent = "8 2";
		bitmap = $MRPG::Spells::Gfx @ "Bottom_Elements/icon_frame_brown"; wrap = "0"; mColor = "255 255 255 255"; mMultiply = "0";
	};
	%slot.add(%frame);

	%iw = %w - 8;
	%ih = %h - 8;
	%icon = new GuiBitmapCtrl("MRPG_SpellSlotIcon_" @ %key)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "4 4"; extent = %iw SPC %ih; minExtent = "8 2";
		bitmap = $MRPG::Spells::Gfx @ "noItem"; wrap = "0"; mColor = "255 255 255 255"; mMultiply = "0";
	};
	%slot.add(%icon);

	// Cooldown overlay (drains from the top down - see MRPG_setSlotCooldown).
	%cd = new GuiSwatchCtrl("MRPG_SpellSlotCD_" @ %key)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "4 4"; extent = %iw SPC %ih; minExtent = "1 1"; color = "18 18 26 200"; visible = "0";
	};
	%cd.baseX   = 4;
	%cd.baseW   = %iw;
	%cd.hPos    = 4;
	%cd.maxHExt = %ih;
	%slot.add(%cd);

	// Key label at the top of the slot.
	%kl = new GuiMLTextCtrl()
	{
		profile = "GuiMLTextProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "0 3"; extent = %w SPC "14"; minExtent = "8 2"; lineSpacing = "1";
		allowColorChars = "1"; maxChars = "-1"; selectable = "0"; autoResize = "0";
	};
	%kl.setText("<just:center><font:verdana bold:10><color:" @ $MRPG::UI::Gold @ ">" @ %label);
	%slot.add(%kl);

	// Bound spell name below the slot.
	%nm = new GuiMLTextCtrl("MRPG_SpellSlotName_" @ %key)
	{
		profile = "GuiMLTextProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "-6 74"; extent = ((%w) + 12) SPC "30"; minExtent = "8 2"; lineSpacing = "1";
		allowColorChars = "1"; maxChars = "-1"; selectable = "0"; autoResize = "0";
	};
	%nm.setText("<just:center><font:verdana bold:11><color:" @ $MRPG::UI::Lock @ ">(empty)");
	%slot.add(%nm);

	// Click-catcher to bind the selected spell here.
	%mouse = new GuiMouseEventCtrl("MRPG_SpellSlotMouse_" @ %key)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "0 0"; extent = %w SPC %h; minExtent = "8 2"; lockMouse = "0";
		slotKey = %key;
	};
	%slot.add(%mouse);
}

// Show/hide the panel + chain (from useRpgTab: shown with Equip, hidden otherwise).
function MRPG_showSpells(%show)
{
	MRPG_buildSpells();
	if(isObject(MonsterRPGx_SpellsPanel)) MonsterRPGx_SpellsPanel.setVisible(%show);
	if(%show)
	{
		commandToServer('MRPG_GetSpells');   // ask the server for this class's spells
		MRPG_renderSpells();
	}
}


//////////////////////////////////
///////////// RENDER /////////////
//////////////////////////////////

function MRPG_renderSpells()
{
	%content = MRPG_SpellsContent;
	if(!isObject(%content))
		return;

	MRPG_clearContent(%content);
	%w = getWord(%content.extent, 0);
	%y = 2;

	if($MRPG_SpellBufCount <= 0)
	{
		%band = MRPG_makeBand(%content, %y, %w, $MRPG::Panel::RowH, 0);
		%band.isRow = 0;
		MRPG_leftText(%band, 10, 6, %w - 16, "<font:verdana bold:12><color:" @ $MRPG::UI::Lock @ ">No spells for your class.");
		%y += $MRPG::Panel::RowH;
		MRPG_fitContent(MRPG_SpellsScroll, %content, %w, %y);
		return;
	}

	for(%i = 0; %i < $MRPG_SpellBufCount; %i++)
	{
		%band = MRPG_makeBand(%content, %y, %w, $MRPG::Panel::RowH, %i);
		%band.spellInternal = $MRPG_SpellBuf[%i, "internal"];
		%band.spellDisplay  = $MRPG_SpellBuf[%i, "display"];

		MRPG_leftText(%band, 10, 5, %w - 44, "<font:verdana bold:12><color:" @ $MRPG::UI::Name @ ">" @ $MRPG_SpellBuf[%i, "display"]);
		MRPG_rightText(%band, %w - 6, 5, 40, "<font:verdana bold:11><color:" @ $MRPG::UI::Value @ ">Lv " @ $MRPG_SpellBuf[%i, "level"]);

		%band.detail = "<font:verdana bold:13><color:" @ $MRPG::UI::Gold @ ">" @ $MRPG_SpellBuf[%i, "display"] @
			"\n<font:verdana bold:11><color:" @ $MRPG::UI::Value @ ">Requires Level <color:" @ $MRPG::UI::Name @ ">" @ $MRPG_SpellBuf[%i, "level"] @
			"\n<font:verdana bold:11><color:C8C8C8>" @ $MRPG_SpellBuf[%i, "desc"] @
			"\n<font:verdana bold:10><color:" @ $MRPG::UI::Gold @ ">Click, then pick a hotbar slot.";
		%band.detailBox = "MRPG_SpellsDetail";

		%y += $MRPG::Panel::RowH;
	}

	MRPG_fitContent(MRPG_SpellsScroll, %content, %w, %y);
}

// Set a hotbar slot's bound-spell name (used by binds + optimistic local update).
function MRPG_setSlotName(%key, %display)
{
	%nm = "MRPG_SpellSlotName_" @ %key;
	if(!isObject(%nm))
		return;
	if(%display $= "" || %display $= "None")
		%nm.setText("<just:center><font:verdana bold:11><color:" @ $MRPG::UI::Lock @ ">(empty)");
	else
		%nm.setText("<just:center><font:verdana bold:11><color:" @ $MRPG::UI::Name @ ">" @ %display);
}


//////////////////////////////////
//////// SELECT / BIND ///////////
//////////////////////////////////

// Click on the list: select the spell row under the cursor.
function MRPG_SpellsListMouse::onMouseDown(%this, %modifier, %mousePoint, %clicks)
{
	%band = MRPG_tipFind(MRPG_SpellsContent);
	if(!isObject(%band) || %band.spellInternal $= "")
		return;

	$MRPG_SelSpell        = %band.spellInternal;
	$MRPG_SelSpellDisplay = %band.spellDisplay;

	// Flash the detail strip as feedback.
	MRPG_showDetail("MRPG_SpellsDetail",
		"<font:verdana bold:12><color:" @ $MRPG::UI::Gold @ ">Selected: <color:" @ $MRPG::UI::Name @ ">" @ %band.spellDisplay @
		"\n<font:verdana bold:11><color:C8C8C8>Now click the Jet or Light slot to bind it.");
}

// Click a bind-grid slot: bind the selected spell to that hotbar key. The grid lives
// in the Spells menu (the full-screen menu covers the HUD, so the HUD slots can't be
// clicked while it's open - hence a mirror grid here). Binding is client-side; the
// number key sends the internal name to the server at cast time.
function MRPG_BindMouse_1::onMouseDown(%this)  { MRPG_bindGridClick(1);  }
function MRPG_BindMouse_2::onMouseDown(%this)  { MRPG_bindGridClick(2);  }
function MRPG_BindMouse_3::onMouseDown(%this)  { MRPG_bindGridClick(3);  }
function MRPG_BindMouse_4::onMouseDown(%this)  { MRPG_bindGridClick(4);  }
function MRPG_BindMouse_5::onMouseDown(%this)  { MRPG_bindGridClick(5);  }
function MRPG_BindMouse_6::onMouseDown(%this)  { MRPG_bindGridClick(6);  }
function MRPG_BindMouse_7::onMouseDown(%this)  { MRPG_bindGridClick(7);  }
function MRPG_BindMouse_8::onMouseDown(%this)  { MRPG_bindGridClick(8);  }
function MRPG_BindMouse_9::onMouseDown(%this)  { MRPG_bindGridClick(9);  }
function MRPG_BindMouse_10::onMouseDown(%this) { MRPG_bindGridClick(10); }
function MRPG_BindMouse_11::onMouseDown(%this) { MRPG_bindGridClick(11); }
function MRPG_BindMouse_12::onMouseDown(%this) { MRPG_bindGridClick(12); }

function MRPG_bindGridClick(%n)
{
	if($MRPG_SelSpell $= "")
	{
		MRPG_showDetail("MRPG_SpellsDetail",
			"<font:verdana bold:11><color:C8C8C8>Click a spell in the list first, then a slot.");
		return;
	}

	// Bind purely client-side (SpellBar.cs storage); the number key sends the internal
	// name to the server at cast time. Updates both the grid and the HUD hotbar.
	MRPG_hudBindSlot(%n, $MRPG_SelSpell, $MRPG_SelSpellDisplay);

	MRPG_showDetail("MRPG_SpellsDetail",
		"<font:verdana bold:12><color:" @ $MRPG::UI::Gold @ ">" @ $MRPG_SelSpellDisplay @
		"<color:" @ $MRPG::UI::Name @ "> bound to key <color:" @ $MRPG::UI::Gold @ ">" @ $MRPG_HudKeySym[%n] @ "<color:" @ $MRPG::UI::Name @ ">.");
	$MRPG_SelSpell = "";
	$MRPG_SelSpellDisplay = "";
}

// Refresh one bind-grid slot: gold key symbol + abbreviated spell name when bound,
// grey key symbol + no name when empty.
function MRPG_updateBindGridSlot(%n)
{
	%keyCtrl  = "MRPG_BindKey_"  @ %n;
	%nameCtrl = "MRPG_BindName_" @ %n;
	%bound    = ($MRPG_HudBind[%n] !$= "");

	if(isObject(%keyCtrl))
		%keyCtrl.setText("<just:center><font:verdana bold:12><color:" @
			(%bound ? $MRPG::UI::Gold : $MRPG::UI::Lock) @ ">" @ $MRPG_HudKeySym[%n]);

	if(isObject(%nameCtrl))
	{
		if(%bound)
			%nameCtrl.setText("<just:center><font:verdana bold:8><color:" @ $MRPG::UI::Name @ ">" @ MRPG_abbrevSpell($MRPG_HudBindDisplay[%n]));
		else
			%nameCtrl.setText("");
	}
}

// Refresh all 12 bind-grid slots (called when the panel opens).
function MRPG_refreshBindGrid()
{
	if($MRPG_HudKeySym[1] $= "")
		MRPG_initSpellBarConsts();   // key-symbol table (SpellBar.cs) not built yet
	for(%n = 1; %n <= 12; %n++)
		MRPG_updateBindGridSlot(%n);
}


//////////////////////////////////
///////// COOLDOWN SWEEP /////////
//////////////////////////////////
// Bottom-anchored drain, exactly like the HP/Mana orbs (ClientCommands "WaterLevel").

function MRPG_startSpellCooldown(%total)
{
	if(%total <= 0)
		return;
	$MRPG_SpellCD_Total = %total;
	$MRPG_SpellCD_Start = getSimTime();
	MRPG_spellCooldownTick();
}

function MRPG_spellCooldownTick()
{
	cancel($MRPG_SpellCD_Sch);

	//Gate, and do NOT reschedule when it is shut - see ServerGate.cs rule 3.
	if(!MRPG_isActive())
		return;

	%frac = 1.0;
	if($MRPG_SpellCD_Total > 0)
		%frac = 1.0 - ((getSimTime() - $MRPG_SpellCD_Start) / 1000) / $MRPG_SpellCD_Total;

	if(%frac <= 0)
	{
		MRPG_setSlotCooldown("Jet", 0);
		MRPG_setSlotCooldown("Light", 0);
		return; // done - overlays hidden
	}

	MRPG_setSlotCooldown("Jet", %frac);
	MRPG_setSlotCooldown("Light", %frac);
	$MRPG_SpellCD_Sch = schedule(33, 0, "MRPG_spellCooldownTick");
}

function MRPG_setSlotCooldown(%key, %frac)
{
	%cd = "MRPG_SpellSlotCD_" @ %key;
	if(!isObject(%cd))
		return;

	if(%frac <= 0)
	{
		%cd.setVisible(0);
		return;
	}

	%cd.setVisible(1);
	%fill = %cd.maxHExt * %frac;
	%cd.resize(%cd.baseX, %cd.hPos + (%cd.maxHExt - %fill), %cd.baseW, %fill);
}


//////////////////////////////////
///////// SERVER FEED ////////////
//////////////////////////////////

function clientCmdMRPG_SpellClear()
{
	$MRPG_SpellBufCount = 0;
}

function clientCmdMRPG_SpellRow(%data)
{
	%i = $MRPG_SpellBufCount;
	$MRPG_SpellBuf[%i, "display"]  = getField(%data, 0);
	$MRPG_SpellBuf[%i, "level"]    = getField(%data, 1);
	$MRPG_SpellBuf[%i, "desc"]     = getField(%data, 2);
	$MRPG_SpellBuf[%i, "internal"] = getField(%data, 3);
	$MRPG_SpellDisplay[getField(%data, 3)] = getField(%data, 0);   // internal -> display
	$MRPG_SpellBufCount++;
}

function clientCmdMRPG_SpellDone()
{
	if(isObject(MonsterRPGx_SpellsPanel) && MonsterRPGx_SpellsPanel.isVisible())
		MRPG_renderSpells();
}

// Current binds: <jetInternal> TAB <lightInternal>. Update the slot names.
function clientCmdMRPG_SpellBinds(%data)
{
	%jet   = getField(%data, 0);
	%light = getField(%data, 1);
	$MRPG_SpellBind["Jet"]   = %jet;
	$MRPG_SpellBind["Light"] = %light;
	MRPG_setSlotName("Jet",   $MRPG_SpellDisplay[%jet]);
	MRPG_setSlotName("Light", $MRPG_SpellDisplay[%light]);
}

// Legacy GLOBAL cooldown broadcast (still emitted by Player::coolDownSpell). Ignored now -
// cooldowns are PER-SPELL: the server sends MRPG_SpellCD <spell> <seconds> and SpellBar.cs
// drains only the slots bound to that spell. Kept as a no-op so the old broadcast can't
// sweep the whole bar.
function clientCmdMRPG_SpellCooldown(%total)
{
}


//////////////////////////////////
///////// DRAG HANDLERS //////////
//////////////////////////////////
// Reuse the shared, frame-synced drag helpers (Equipment.cs).

function MRPG_SpellsDragMouse::onMouseDown(%this, %modifier, %mousePoint, %clicks)
{
	MRPG_titleDragBegin(%this, %mousePoint);
}
function MRPG_SpellsDragMouse::onMouseDragged(%this, %modifier, %mousePoint, %clicks)
{
	MRPG_titleDragMove(%this, %mousePoint);
}
function MRPG_SpellsDragMouse::onMouseUp(%this, %modifier, %mousePoint)
{
	MRPG_titleDragEnd(%this);
}
