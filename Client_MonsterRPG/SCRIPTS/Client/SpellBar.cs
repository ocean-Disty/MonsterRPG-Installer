//////////////////////////////////////////////////////////////////////////////
// HUD SPELL BAR  -  number-key hotbar + cooldown drain
//////////////////////////////////////////////////////////////////////////////
//
// The HUD (MonsterRPGx_HUDBase.gui, Bottom_UI) already ships an inert spell bar:
// Spell_BG + Spell_Border_1..10 between the HP orb (Left_Colb) and Mana orb
// (Right_Colb). This module turns it into a working 12-slot hotbar:
//
//   * keys 1-9,0 -> slots 1-10, "-" -> slot 11, "=" -> slot 12  (per-user choice,
//     these keys CAST instead of switching tool slots WHILE YOU ARE ON THE SERVER -
//     they are borrowed on join and handed back on leave; see Keybinds.cs)
//   * each slot shows its key symbol; a bound slot also shows the spell name
//   * casting sends commandToServer('MRPG_CastSpell', internal) -> Core_Spells.cs,
//     which validates class/level and dispatches to the real spell method (that
//     method self-guards mana + cooldown and broadcasts MRPG_SpellCooldown)
//   * cooldown is shown as an orb-style drain (dark overlay clipped from the top,
//     like the HP/Mana colbs) swept in 32ms ticks
//
// BINDING happens in the Spells menu (MonsterRPGx_Spells.gui bind grid): the main
// menu is a full-screen dialog that covers the HUD, so the HUD slots can't be
// clicked while it is open. You click a spell in the list, then a bind-grid slot;
// that calls MRPG_hudBindSlot, which updates both the menu grid and this HUD bar.
// Binds live client-side ($MRPG_HudBind[]); the server only needs the internal name
// at cast time. So the hotbar is only editable while the Spells menu is open.

$MRPG_HudBarBuilt   = 0;
$MRPG_HudSlotCount  = 12;
$MRPG::SpellBar::Gfx = "Add-Ons/Client_MonsterRPG/GUIs/";

// Slot X positions inside Bottom_UI. 1-10 match the shipped Spell_Border_1..10
// exactly; 11-12 continue the ~51px stride (frames added at build time).
$MRPG_HudSlotXArr = "151 202 253 304 355 405 456 507 558 609 660 711";
$MRPG::SpellBar::Y = 54;   // Spell_Border top (Bottom_UI-relative) - fallback only
$MRPG::SpellBar::S = 44;   // frame size - fallback only
// Icon fit inside each socket. The build reads each Spell_Border frame's LIVE geometry,
// so these are the only knobs you need: symmetric border + a small downward nudge.
$MRPG::SpellBar::IconInset = 5;   // px border on all sides (bigger = smaller icon)
$MRPG::SpellBar::IconDrop  = 2;   // extra px down inside the socket

// One-time: key names (for moveMap) + printed symbols per slot.
function MRPG_initSpellBarConsts()
{
	for(%n = 1; %n <= 9; %n++)
	{
		$MRPG_HudKeyName[%n] = %n;   // "1".."9"
		$MRPG_HudKeySym[%n]  = %n;
	}
	$MRPG_HudKeyName[10] = "0";      $MRPG_HudKeySym[10] = "0";
	$MRPG_HudKeyName[11] = "minus";  $MRPG_HudKeySym[11] = "-";
	$MRPG_HudKeyName[12] = "equals"; $MRPG_HudKeySym[12] = "=";
}


//////////////////////////////////
////////// BUILD (HUD) ///////////
//////////////////////////////////

function MRPG_buildHudSpellBar()
{
	//Already built. There is nothing to re-bind: the broker took 1-0 at join and
	//holds them until leave, so config.cs cannot re-take them mid-session the way
	//it could when this bound at HUD-build time. See Keybinds.cs.
	if($MRPG_HudBarBuilt && isObject(MRPG_HudKey_1))
		return;
	if(!isObject(Bottom_UI))
		return;

	MRPG_initSpellBarConsts();

	// The HUD shows 10 slots (keys 1-0) - that is how many 44px frames fit between the
	// HP orb (Left_Colb) and Mana orb (Right_Colb). Keys "-" and "=" (slots 11-12) stay
	// bindable + castable (bind grid + the broker in Keybinds.cs) but have no HUD
	// frame; they are the "extras" that don't fit between the orbs.
	for(%n = 1; %n <= 10; %n++)
	{
		// Anchor to the ACTUAL Spell_Border frame so the icon sits dead-centre in the real
		// socket at its true position/size (self-correcting vs. hardcoded coordinates).
		%frame = "Spell_Border_" @ %n;
		if(isObject(%frame))
		{
			%fp = %frame.getPosition();  %fx = getWord(%fp, 0);  %fy = getWord(%fp, 1);
			%fe = %frame.getExtent();    %fw = getWord(%fe, 0);  %fh = getWord(%fe, 1);
		}
		else
		{
			%fx = getWord($MRPG_HudSlotXArr, %n - 1);  %fy = $MRPG::SpellBar::Y;
			%fw = $MRPG::SpellBar::S;                  %fh = $MRPG::SpellBar::S;
		}
		%in   = $MRPG::SpellBar::IconInset;
		%drop = $MRPG::SpellBar::IconDrop;

		// Placeholder spell icon, symmetric inset inside the socket (temp MissingItem art).
		%icon = new GuiBitmapCtrl("MRPG_HudIcon_" @ %n)
		{
			profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
			position = (%fx + %in) SPC (%fy + %in + %drop); extent = (%fw - 2 * %in) SPC (%fh - 2 * %in);
			minExtent = "8 2"; bitmap = $MRPG::SpellBar::Gfx @ "MissingItem"; wrap = "0";
			mColor = "255 255 255 70"; mMultiply = "0";   // dim until a spell is bound
		};
		Bottom_UI.add(%icon);

		// Cooldown overlay (orb-style drain) sized exactly to the socket, hidden by default.
		%cd = new GuiSwatchCtrl("MRPG_HudCD_" @ %n)
		{
			profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
			position = %fx SPC %fy; extent = %fw SPC %fh; minExtent = "1 1"; color = "18 18 26 200"; visible = "0";
		};
		%cd.baseX = %fx;  %cd.baseW = %fw;  %cd.hPos = %fy;  %cd.maxHExt = %fh;
		Bottom_UI.add(%cd);

		// Modern keybind badge: a small dark tab in the socket's TOP-LEFT corner with the
		// number on it (the MMO/ARPG convention). Added after the cooldown overlay so it
		// stays readable over the icon and through a cooldown sweep.
		%kbg = new GuiSwatchCtrl("MRPG_HudKeyBg_" @ %n)
		{
			profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
			position = (%fx + 4) SPC (%fy + 4); extent = "15 14"; minExtent = "1 1"; color = "0 0 0 165";
		};
		Bottom_UI.add(%kbg);

		%kl = new GuiMLTextCtrl("MRPG_HudKey_" @ %n)
		{
			profile = "GuiMLTextProfile"; horizSizing = "right"; vertSizing = "bottom";
			position = (%fx + 3) SPC (%fy + 3); extent = "17 14"; minExtent = "8 2"; lineSpacing = "1";
			allowColorChars = "1"; maxChars = "-1"; selectable = "0"; autoResize = "0";
		};
		%kl.setText("<just:center><font:verdana bold:12><color:F1ECC2>" @ $MRPG_HudKeySym[%n]);
		Bottom_UI.add(%kl);

		MRPG_updateHudSlot(%n);   // brighten the icon if already bound
	}

	$MRPG_HudBarBuilt = 1;
}

// Icon art for a spell's internal name: GUIs/Icons/<Class>/<Spell>.png. Returns "" for
// spells with no art yet (Gunman spells + the not-yet-authored tree spells) so those
// fall back to the MissingItem placeholder.
function MRPG_spellIconPath(%internal)
{
	%dir = $MRPG::SpellBar::Gfx @ "Icons/";
	switch$(%internal)
	{
		case "Dash":           return %dir @ "Warrior/Dash";
		case "SuperJump":      return %dir @ "Warrior/SuperJump";
		case "DanceOfSwords":  return %dir @ "Warrior/DanceOfSwords";
		case "SelfHeal":       return %dir @ "Cleric/SelfHeal";
		case "Heal":           return %dir @ "Cleric/Heal";
		case "Absorb":         return %dir @ "Cleric/Absorb";
		case "RapidFire":      return %dir @ "Archer/RapidFire";
		case "ColdArrow":      return %dir @ "Archer/ColdArrow";
		case "ImplosionArrow": return %dir @ "Archer/ImplosionArrow";
		default:               return "";
	}
}

// Set a HUD slot's icon to its bound spell's art (full-bright), or the dim MissingItem
// placeholder when the slot is empty or the bound spell has no art yet. setBitmap keeps
// the control's fixed extent (like the equip icons), so the art stretches to fit the socket.
function MRPG_updateHudSlot(%n)
{
	%icon = "MRPG_HudIcon_" @ %n;
	if(!isObject(%icon))
		return;

	%bind = $MRPG_HudBind[%n];
	if(%bind $= "")
	{
		%icon.setBitmap($MRPG::SpellBar::Gfx @ "MissingItem");
		%icon.mColor = "255 255 255 70";     // dim placeholder = empty slot
		return;
	}

	%path = MRPG_spellIconPath(%bind);
	if(%path $= "")
		%path = $MRPG::SpellBar::Gfx @ "MissingItem";   // bound but no art yet
	%icon.setBitmap(%path);
	%icon.mColor = "255 255 255 255";        // full-bright = bound
}

// Squeeze a spell name into the ~44px slot: first word, capped at 7 chars.
function MRPG_abbrevSpell(%display)
{
	%w = getWord(%display, 0);
	if(%w $= "")
		%w = %display;
	if(strLen(%w) > 7)
		%w = getSubStr(%w, 0, 7);
	return %w;
}


//////////////////////////////////
///////// KEYS / CASTING /////////
//////////////////////////////////
// Per the user's choice, 1-0 now CAST (they no longer switch tool slots). Re-bound
// every HUD build so the mod wins over config.cs's slot binds.

// THE TWELVE HOTBAR KEYS ARE NOT BOUND HERE ANY MORE.
//
// They were the clearest case for the rewrite in Keybinds.cs: 1-0 belong to
// useBricks and useSecondSlot..useTenthSlot in stock, which is most of how a
// player interacts with Blockland when they are not on this server. Binding them
// at HUD-build time claimed them for the rest of the install's life.
//
// The broker borrows them on join and hands them back on leave. Nothing to do
// here; the HUD only draws the key symbols.

// NO FORWARD TO THE STOCK SLOTS HERE.
//
// An earlier pass had these fall through to useBricks / useSecondSlot / ... when
// off-server, because the bind was permanent and the alternative was ten dead
// number keys. That forward was also subtly wrong - it sent slot 1 to useFirstSlot,
// which stock does not bind; "1" is useBricks.
//
// The broker in Keybinds.cs gives the keys back to whatever actually held them, so
// these commands are not bound at all off-server and there is nothing to forward.
// MRPG_gateKey is the key-DOWN edge plus the server test; it only catches strays.
function MRPG_Cast1(%val)  { if(MRPG_gateKey(%val)) MRPG_castHudSlot(1);  }
function MRPG_Cast2(%val)  { if(MRPG_gateKey(%val)) MRPG_castHudSlot(2);  }
function MRPG_Cast3(%val)  { if(MRPG_gateKey(%val)) MRPG_castHudSlot(3);  }
function MRPG_Cast4(%val)  { if(MRPG_gateKey(%val)) MRPG_castHudSlot(4);  }
function MRPG_Cast5(%val)  { if(MRPG_gateKey(%val)) MRPG_castHudSlot(5);  }
function MRPG_Cast6(%val)  { if(MRPG_gateKey(%val)) MRPG_castHudSlot(6);  }
function MRPG_Cast7(%val)  { if(MRPG_gateKey(%val)) MRPG_castHudSlot(7);  }
function MRPG_Cast8(%val)  { if(MRPG_gateKey(%val)) MRPG_castHudSlot(8);  }
function MRPG_Cast9(%val)  { if(MRPG_gateKey(%val)) MRPG_castHudSlot(9);  }
function MRPG_Cast10(%val) { if(MRPG_gateKey(%val)) MRPG_castHudSlot(10); }
function MRPG_Cast11(%val) { if(MRPG_gateKey(%val)) MRPG_castHudSlot(11); }
function MRPG_Cast12(%val) { if(MRPG_gateKey(%val)) MRPG_castHudSlot(12); }

function MRPG_castHudSlot(%n)
{
	// The 1-0/-/= hotbar keys are bound on every server. $MRPG_HudBind[] would
	// normally be empty off-server and stop this on the next line, but it is filled
	// from the server and nothing used to clear it on disconnect - so the twelve
	// number keys stayed live, firing MRPG_BeginIncant at whoever the player joined
	// next. Gate the action, not the bind. See ServerGate.cs rule 4.
	if(!MRPG_isActive())
		return;

	%internal = $MRPG_HudBind[%n];
	if(%internal $= "")
		return;   // empty slot - do nothing
	if($Inc_Active)
		return;   // an incantation is already running - ignore other slot presses
	// Begin the incantation QTE instead of casting directly; the server fires the spell
	// only after the sequence is completed correctly (Core_Incantation.cs / Incantation.cs).
	commandToServer('MRPG_BeginIncant', %internal);
}


//////////////////////////////////
///////////// BIND ///////////////
//////////////////////////////////
// Called from the Spells-menu bind grid (Spells.cs). Stores the bind client-side and
// refreshes both the menu grid and the HUD bar.

function MRPG_hudBindSlot(%n, %internal, %display)
{
	if(%n < 1 || %n > $MRPG_HudSlotCount)
		return;
	$MRPG_HudBind[%n]        = %internal;
	$MRPG_HudBindDisplay[%n] = %display;
	MRPG_updateHudSlot(%n);        // HUD bar
	MRPG_updateBindGridSlot(%n);   // menu grid (Spells.cs owns the grid controls)
}

function MRPG_clearHudBind(%n)
{
	MRPG_hudBindSlot(%n, "", "");
}


//////////////////////////////////
//////// COOLDOWN (32ms) /////////
//////////////////////////////////
// PER-SPELL cooldown -> only the slot(s) bound to the spell you just cast drain, each on
// its own timer, orb-style at 32ms. The server sends MRPG_SpellCD <spell> <seconds>; unused
// spells stay ready. (Was global before: any cast drained every slot.)

$MRPG_HudCD_Sch = "";   // shared 32ms tick handle; per-slot state is $MRPG_SlotCD_Start/Total[n]

// Put every slot bound to %spell on its own cooldown (usually one slot).
function MRPG_startSpellCD(%spell, %total)
{
	if(%total <= 0 || %spell $= "")
		return;
	%now = getSimTime();
	for(%n = 1; %n <= $MRPG_HudSlotCount; %n++)
	{
		if($MRPG_HudBind[%n] $= %spell)
		{
			$MRPG_SlotCD_Start[%n] = %now;
			$MRPG_SlotCD_Total[%n] = %total;
		}
	}
	MRPG_hudCooldownTick();
}

// One 32ms sweep - each slot drains on its OWN timer, so other spells stay usable.
function MRPG_hudCooldownTick()
{
	cancel($MRPG_HudCD_Sch);

	//Gate, and do NOT reschedule when it is shut - see ServerGate.cs rule 3.
	if(!MRPG_isActive())
		return;
	%now = getSimTime();
	%anyActive = 0;

	for(%n = 1; %n <= $MRPG_HudSlotCount; %n++)
	{
		%tot = $MRPG_SlotCD_Total[%n];
		if(%tot <= 0)
			continue;
		%frac = 1.0 - ((%now - $MRPG_SlotCD_Start[%n]) / 1000) / %tot;
		if(%frac <= 0)
		{
			$MRPG_SlotCD_Total[%n] = 0;
			MRPG_setHudSlotCooldown(%n, 0);
		}
		else
		{
			MRPG_setHudSlotCooldown(%n, %frac);
			%anyActive = 1;
		}
	}

	if(%anyActive)
		$MRPG_HudCD_Sch = schedule(32, 0, "MRPG_hudCooldownTick");
}

// Server: cool down a single spell. Slots bound to it drain; every other slot stays ready.
function clientCmdMRPG_SpellCD(%data)
{
	MRPG_startSpellCD(getField(%data, 0), getField(%data, 1));
}

function MRPG_setHudSlotCooldown(%n, %frac)
{
	%cd = "MRPG_HudCD_" @ %n;
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
