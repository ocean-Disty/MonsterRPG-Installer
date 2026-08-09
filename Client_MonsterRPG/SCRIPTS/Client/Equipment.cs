//////////////////////////////////////////////////////////////////////////////
// EQUIPMENT SCREEN (foundation)
//////////////////////////////////////////////////////////////////////////////
//
// The Equip tab had no GUI and no useRPGTab case, so it did nothing. This builds
// a panel (a sibling swatch of the other tabs) with a live avatar preview on the
// left and labelled equipment slots on the right. The avatar reuses the existing
// MonsterRPGx_Equipment::adjustAvatar helpers (GUIFunctions.cs).
//
// The slots are structural for now - the game has no slotted-gear data model yet
// (armor is GC mounted-image defense, weapons are tools), so they read "(empty)"
// until that backend is built. MRPG_setEquipSlot() is the hook to fill them.

$MRPG_EquipBuilt   = 0;
$MRPG::Equip::Gfx  = "Add-Ons/Client_MonsterRPG/GUIs/";

function MRPG_equipSlotName(%i)
{
	switch(%i)
	{
		case 0: return "Head";
		case 1: return "Body";
		case 2: return "Hands";
		case 3: return "Weapon";
		case 4: return "Ring";
		case 5: return "Amulet";
	}
	return "";
}

// Build the panel once. It is parented next to the other tab swatches so
// useRPGTab can show/hide it the same way.
function MRPG_buildEquip()
{
	if($MRPG_EquipBuilt && isObject(MonsterRPGx_Equipment))
		return;
	if(!isObject(MonsterRPGx_Stats))
		return;

	// The Equip layout now lives in GUIs/MonsterRPGx_Equip.gui (exec'd in client.cs,
	// editable in the GUI editor). Instead of building it from scratch we just slot
	// that swatch next to the other tab swatches and fill in the script-controlled
	// text. Structure (backdrop, avatar frame, six slots, drag catcher) is all in
	// the .gui; only the palette-styled text + live avatar/icons are set here.
	if(!isObject(MonsterRPGx_Equipment))
		return; // MonsterRPGx_Equip.gui not loaded - nothing to slot in

	%parent = MonsterRPGx_Stats.getGroup();
	if(!isObject(%parent))
		return;
	%parent.add(MonsterRPGx_Equipment); // idempotent reparent next to Stats/Skills/etc.

	// Title + slot labels + empty-item text (styled with the UI palette globals).
	// The .gui carries the same text so the editor preview reads right, but these
	// setText()s are the runtime source of truth (an editor save can strip .gui text).
	if(isObject(MonsterRPGx_EquipTitle))
		MonsterRPGx_EquipTitle.setText("<just:center><color:" @ $MRPG::UI::Name @ "><font:verdana bold:38>Equipment");
	for(%i = 0; %i < 6; %i++)
	{
		%lbl = "MonsterRPGx_EquipLabel" @ %i;
		if(isObject(%lbl))
			%lbl.setText("<font:verdana bold:13><color:" @ $MRPG::UI::Gold @ ">" @ MRPG_equipSlotName(%i));
		%item = "MRPG_EquipItem" @ %i;
		if(isObject(%item))
			%item.setText("<font:verdana bold:11><color:" @ $MRPG::UI::Lock @ ">(empty)");
	}

	$MRPG_EquipBuilt = 1;
}

// One equipment slot row: icon frame + label + item name.
function MRPG_addEquipSlot(%parent, %index, %x, %y, %w, %h)
{
	%pos = %x SPC %y;
	%ext = %w SPC %h;
	%band = new GuiSwatchCtrl("MRPG_EquipSlot" @ %index)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %pos; extent = %ext; minExtent = "8 2"; color = "255 255 255 10";
	};
	%parent.add(%band);

	%frame = new GuiBitmapCtrl()
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "3 3"; extent = "36 36"; minExtent = "8 2";
		bitmap = $MRPG::Equip::Gfx @ "Bottom_Elements/icon_frame_brown"; wrap = "0"; mColor = "255 255 255 255"; mMultiply = "0";
	};
	%band.add(%frame);
	%icon = new GuiBitmapCtrl("MRPG_EquipIcon" @ %index)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "6 6"; extent = "30 30"; minExtent = "8 2";
		bitmap = $MRPG::Equip::Gfx @ "noItem"; wrap = "0"; mColor = "255 255 255 255"; mMultiply = "0";
	};
	%band.add(%icon);

	%lbl = new GuiMLTextCtrl()
	{
		profile = "GuiCustomMLTextProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "48 6"; extent = "116 16"; minExtent = "8 2"; lineSpacing = "2";
		allowColorChars = "1"; maxChars = "-1"; selectable = "0"; autoResize = "0";
	};
	%lbl.setText("<font:verdana bold:13><color:" @ $MRPG::UI::Gold @ ">" @ MRPG_equipSlotName(%index));
	%band.add(%lbl);

	%item = new GuiMLTextCtrl("MRPG_EquipItem" @ %index)
	{
		profile = "GuiCustomMLTextProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "48 24"; extent = "116 14"; minExtent = "8 2"; lineSpacing = "2";
		allowColorChars = "1"; maxChars = "-1"; selectable = "0"; autoResize = "0";
	};
	%item.setText("<font:verdana bold:11><color:" @ $MRPG::UI::Lock @ ">(empty)");
	%band.add(%item);
}

// Fill a slot with an item (name + optional icon path). The hook for the future
// gear system. %iconPath "" leaves the empty-slot icon.
function MRPG_setEquipSlot(%index, %itemName, %iconPath)
{
	%item = "MRPG_EquipItem" @ %index;
	%icon = "MRPG_EquipIcon" @ %index;

	if(%itemName $= "")
	{
		if(isObject(%item)) %item.setText("<font:verdana bold:11><color:" @ $MRPG::UI::Lock @ ">(empty)");
		if(isObject(%icon)) %icon.setBitmap($MRPG::Equip::Gfx @ "noItem");
		return;
	}

	if(isObject(%item)) %item.setText("<font:verdana bold:12><color:" @ $MRPG::UI::Name @ ">" @ %itemName);
	if(%iconPath !$= "" && isObject(%icon)) %icon.setBitmap(%iconPath);
}

// (Re)build + refresh the avatar. Called when the Equip tab opens.
function MRPG_refreshEquip()
{
	MRPG_buildEquip();

	if(isObject(MonsterRPGx_Avatar) && isObject(MonsterRPGx_Equipment))
	{
		MonsterRPGx_Avatar.setEmpty();
		MonsterRPGx_Avatar.dynamicObject = "MonsterRPGx_Avatar_Object";
		MonsterRPGx_Avatar.setObject(MonsterRPGx_Avatar.dynamicObject, "base/data/shapes/player/m.dts", "", 0);
		MonsterRPGx_Equipment.adjustAvatar();
	}

	//ASK FOR THE CHARACTER IF WE HAVE NOT GOT IT. adjustAvatar above styles from the
	//MonsterRPG look when one is loaded and falls back to the stock Blockland avatar when
	//it is not - so on a session where the character screen was never opened, this panel
	//would show the wrong person until it was. The reply (clientCmdMRPG_CharData) sets
	//$CS_LookKnown and calls MRPG_refreshEquipAvatar, which restyles the doll in place.
	if(!$CS_LookKnown)
		commandToServer('MRPG_CharGet');
}


//////////////////////////////////////////////////////////////////////////////
// TITLE-BAR DRAG (for the runtime-built Equip / Quest tabs)
//////////////////////////////////////////////////////////////////////////////
//
// The built-in tabs are dragged by one shared catcher, MonsterRPGx_PlayInvMouse
// (GUIFunctions.cs), which is the last child of MonsterRPGx_PlyrInv and so sits
// on top of their title bars. The Equip and Quest swatches are added to that same
// group at runtime, landing *above* that catcher and covering it - so their titles
// wouldn't drag. Rather than reorder the shared catcher (which affects every tab),
// each runtime swatch gets its own top-most catcher over the banner. It drives the
// exact same window drag (moving MonsterRPGx_PlyrInv).

// Add a named, transparent drag handle over the title banner as %swatch's top-most
// child. A NAMED object (not a class= binding) is used so the mouse callbacks fire
// exactly like the proven MonsterRPGx_PlayInvMouse. %pos/%ext default to the Equip/
// Quest banner rect; pass them for a differently-sized title (e.g. the Spells panel).
function MRPG_addTitleDrag(%swatch, %name, %pos, %ext)
{
	if(%pos $= "") %pos = "64 20";
	if(%ext $= "") %ext = "469 67";

	if(isObject(%name))
		%name.delete();

	%m = new GuiMouseEventCtrl(%name)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = %pos;
		extent      = %ext;
		minExtent   = "8 2";
		lockMouse   = "1";
	};
	%swatch.add(%m);
	return %m;
}

// Shared drag logic. Frame-synced via onMouseDragged - the window is repositioned
// once per rendered frame the cursor moves, so it tracks the mouse smoothly with no
// timer-vs-framerate aliasing (a fixed ~33ms tick loop is what felt jittery). The
// cursor position comes straight from the event, so nothing polls on a timer.
function MRPG_titleDragBegin(%ctrl, %mousePoint)
{
	%ctrl.dragging    = true;
	%ctrl.clickOffset = VectorSub(MonsterRPGx_PlyrInv.getPosition(), %mousePoint);
}

function MRPG_titleDragMove(%ctrl, %mousePoint)
{
	if(!%ctrl.dragging)
		return;

	%x = getWord(%mousePoint, 0) + getWord(%ctrl.clickOffset, 0);
	%y = getWord(%mousePoint, 1) + getWord(%ctrl.clickOffset, 1);
	%w = getWord(MonsterRPGx_PlyrInv.getExtent(), 0);
	%h = getWord(MonsterRPGx_PlyrInv.getExtent(), 1);
	MonsterRPGx_PlyrInv.resize(%x, %y, %w, %h);
}

function MRPG_titleDragEnd(%ctrl)
{
	%ctrl.dragging = false;
}

function MRPG_EquipDragMouse::onMouseDown(%this, %modifier, %mousePoint, %clicks)
{
	MRPG_titleDragBegin(%this, %mousePoint);
}
function MRPG_EquipDragMouse::onMouseDragged(%this, %modifier, %mousePoint, %clicks)
{
	MRPG_titleDragMove(%this, %mousePoint);
}
function MRPG_EquipDragMouse::onMouseUp(%this, %modifier, %mousePoint)
{
	MRPG_titleDragEnd(%this);
}

function MRPG_QuestDragMouse::onMouseDown(%this, %modifier, %mousePoint, %clicks)
{
	MRPG_titleDragBegin(%this, %mousePoint);
}
function MRPG_QuestDragMouse::onMouseDragged(%this, %modifier, %mousePoint, %clicks)
{
	MRPG_titleDragMove(%this, %mousePoint);
}
function MRPG_QuestDragMouse::onMouseUp(%this, %modifier, %mousePoint)
{
	MRPG_titleDragEnd(%this);
}
