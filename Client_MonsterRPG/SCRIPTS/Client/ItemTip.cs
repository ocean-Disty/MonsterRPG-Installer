//////////////////////////////////////////////////////////////////////////////
// ItemTip.cs  -  hover an inventory cell, see the weapon's stats
//////////////////////////////////////////////////////////////////////////////
//
// Same shape as the party / leaderboard tooltips (RPGPanels.cs:305-360): poll the cursor,
// notice when it lands on something new, wait out a dwell, ask the server once, cache the
// answer, show it. Deliberately the same so the two feel identical - a tooltip that
// appears on a different rhythm from every other tooltip reads as a different system.
//
// WHY POLLING AND NOT onMouseEnter. The cells are built by MonsterRPGx_NewInvBtn
// (Support.cs:37) and there are ~70 container types' worth of them; attaching callbacks
// would mean either editing that shared builder - which every container in the game
// depends on - or hooking each cell after the fact and hoping nothing rebuilds them. The
// existing tooltips already poll, the loop is one hit-test over at most a few dozen
// rectangles, and it costs nothing while the inventory is closed because it early-outs.
//
// SLOT-KEYED, LIKE THE SERVER. Level, Rarity, Attack, Defense and the rolled Specials all
// live per inventory SLOT on the profile, not on the item datablock - two identical swords
// in two slots really are different weapons - so the request, the cache and the panel are
// all keyed on the slot index.
//////////////////////////////////////////////////////////////////////////////

if($ItemTip::DwellMs $= ""){ $ItemTip::DwellMs = 380; }   // snappier than the party tip;
                                                          // you sweep an inventory, you do
                                                          // not read down it
if($ItemTip::TickMs  $= ""){ $ItemTip::TickMs  = 60;  }
if($ItemTip::MaxCell $= ""){ $ItemTip::MaxCell = 46;  }   // player tool slots

$ItemTip_Slot  = -1;      // slot under the cursor right now
$ItemTip_Start = 0;       // when the cursor arrived on it
$ItemTip_Shown = -1;      // slot currently displayed, -1 = panel hidden


//////////////////////////////////////////////////////////////////////////////
// THE PANEL
//
// Built at runtime into MonsterRPGx_Main so it sits inside the same dialog as the
// inventory and is drawn above it. A GuiMLTextCtrl inside a swatch, exactly like the
// party detail box, so the server's markup renders the same way in both.
//////////////////////////////////////////////////////////////////////////////
function MRPG_buildItemTip()
{
	if(isObject(MRPG_ItemTipBox))
		return MRPG_ItemTipBox;
	if(!isObject(MonsterRPGx_Main))
		return 0;

	%box = new GuiSwatchCtrl(MRPG_ItemTipBox)
	{
		profile = "GuiDefaultProfile";
		horizSizing = "right"; vertSizing = "bottom";
		position = "0 0"; extent = "260 150"; minExtent = "8 2";
		visible = "0";
		color = "12 9 6 242";
	};

	//gold hairline, matching the other detail panels
	%box.add(new GuiSwatchCtrl()
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "bottom";
		position = "0 0"; extent = "260 1"; minExtent = "1 1"; color = "150 123 63 255";
	});

	%box.add(new GuiMLTextCtrl(MRPG_ItemTipText)
	{
		profile = "GuiMLTextProfile";
		horizSizing = "width"; vertSizing = "height";
		position = "8 7"; extent = "244 136"; minExtent = "8 2";
		lineSpacing = "2"; selectable = "0"; autoResize = "1";
	});

	MonsterRPGx_Main.add(%box);
	return %box;
}


//////////////////////////////////////////////////////////////////////////////
// WHICH CELL IS UNDER THE CURSOR
//
// Only the player's own inventory. Container cells (a corpse, a chest) hold items whose
// rolled stats live on whoever owned them, not on this profile, so asking the server for
// "slot N of MY inventory" would describe the wrong item entirely. Better to show nothing
// than to show a confident lie about a weapon you are deciding whether to take.
//////////////////////////////////////////////////////////////////////////////
function MRPG_itemTipCellAt()
{
	%cur = Canvas.getCursorPos();
	%cx = getWord(%cur, 0);  %cy = getWord(%cur, 1);

	for(%i = 0; %i < $ItemTip::MaxCell; %i++)
	{
		%cell = "MonsterRPGx_PlyrInv_ItemBGColor_" @ %i;
		if(!isObject(%cell) || !%cell.isVisible())
			continue;

		%p = %cell.getCanvasPosition();  %e = %cell.getExtent();
		%px = getWord(%p, 0);  %py = getWord(%p, 1);
		if(%cx >= %px && %cx < %px + getWord(%e, 0)
		&& %cy >= %py && %cy < %py + getWord(%e, 1))
			return %i;
	}
	return -1;
}

//Place the panel next to a cell without letting it run off the screen.
function MRPG_itemTipPlace(%slot)
{
	%cell = "MonsterRPGx_PlyrInv_ItemBGColor_" @ %slot;
	if(!isObject(%cell) || !isObject(MRPG_ItemTipBox))
		return;

	%p = %cell.getCanvasPosition();
	%e = %cell.getExtent();
	%be = MRPG_ItemTipBox.getExtent();
	%bw = getWord(%be, 0);  %bh = getWord(%be, 1);

	%x = getWord(%p, 0) + getWord(%e, 0) + 8;
	%y = getWord(%p, 1);

	//flip to the other side rather than hang off the right edge
	%res = getRes();
	if(%x + %bw > getWord(%res, 0) - 4)
		%x = getWord(%p, 0) - %bw - 8;
	if(%x < 4)
		%x = 4;
	if(%y + %bh > getWord(%res, 1) - 4)
		%y = getWord(%res, 1) - %bh - 4;
	if(%y < 4)
		%y = 4;

	//The panel is parented to MonsterRPGx_Main, so canvas coordinates need converting
	//back into the parent's space or it lands wherever Main happens to sit.
	%mp = MonsterRPGx_Main.getCanvasPosition();
	MRPG_ItemTipBox.setVisible(1);
	MRPG_ItemTipBox.resize(%x - getWord(%mp, 0), %y - getWord(%mp, 1), %bw, %bh);
}

function MRPG_hideItemTip()
{
	if(isObject(MRPG_ItemTipBox))
		MRPG_ItemTipBox.setVisible(0);
	$ItemTip_Shown = -1;
}


//////////////////////////////////////////////////////////////////////////////
// THE TICK
//////////////////////////////////////////////////////////////////////////////
function MRPG_itemTipTick()
{
	cancel($ItemTip_Sch);

	//GATE FIRST, AND DO NOT RE-ARM. This poll was started from client.cs at boot
	//and re-armed unconditionally, so it ran every 60ms for the whole session on
	//every server. It early-outs on a closed inventory so it never made noise -
	//but "cheap" is not "free", and an inventory tooltip has no business polling
	//the cursor in the main menu. See ServerGate.cs rule 3.
	if(!MRPG_isActive())
	{
		if($ItemTip_Shown >= 0)
			MRPG_hideItemTip();
		$ItemTip_Slot = -1;
		return;
	}

	$ItemTip_Sch = schedule($ItemTip::TickMs, 0, MRPG_itemTipTick);

	//Closed inventory: nothing to hover, and nothing to pay for.
	if(!isObject(MonsterRPGx_Main) || !MonsterRPGx_Main.isAwake())
	{
		if($ItemTip_Shown >= 0)
			MRPG_hideItemTip();
		$ItemTip_Slot = -1;
		return;
	}

	//DRAGGING MUST NOT POP TOOLTIPS. MonsterRPGx_Main.currCell is set while a cell is
	//picked up; a panel appearing under the cursor mid-drag would cover the slot being
	//dragged to.
	if(MonsterRPGx_Main.currCell !$= "")
	{
		if($ItemTip_Shown >= 0)
			MRPG_hideItemTip();
		return;
	}

	%slot = MRPG_itemTipCellAt();

	if(%slot != $ItemTip_Slot)
	{
		//moved to a different cell (or off the grid) - restart the dwell
		$ItemTip_Slot = %slot;
		$ItemTip_Start = getSimTime();
		if($ItemTip_Shown >= 0)
			MRPG_hideItemTip();
		return;
	}

	if(%slot < 0 || $ItemTip_Shown == %slot)
		return;

	if((getSimTime() - $ItemTip_Start) < $ItemTip::DwellMs)
		return;

	//Cached? "" is a real cached answer meaning "this item has no stats worth showing" -
	//distinct from "not asked yet", so a stack of bread is not re-requested on every pass.
	if($ItemTip_Cache[%slot] $= "")
	{
		if(!$ItemTip_Asked[%slot])
		{
			$ItemTip_Asked[%slot] = 1;
			commandToServer('MRPG_ItemTip', %slot);
		}
		return;
	}
	if($ItemTip_Cache[%slot] $= "none")
		return;

	MRPG_ItemTipText.setText($ItemTip_Cache[%slot]);

	//FIT THE PANEL TO THE TEXT. Tooltips vary a lot - a plain dagger is four lines, a
	//Mythic sword with three rolled specials is a dozen - and a fixed height either clips
	//the long ones or leaves a slab of empty background under the short ones. autoResize
	//on the GuiMLTextCtrl gives us the real height once the text is set, so read it back
	//and size the frame to match.
	%th = getWord(MRPG_ItemTipText.getExtent(), 1);
	if(%th < 20) %th = 20;
	MRPG_ItemTipBox.resize(getWord(MRPG_ItemTipBox.position, 0),
	                       getWord(MRPG_ItemTipBox.position, 1),
	                       260, %th + 14);

	MRPG_itemTipPlace(%slot);
	$ItemTip_Shown = %slot;
}

function clientCmdMRPG_ItemTip(%slot, %text)
{
	//"none" rather than "" so the cache can distinguish an answered-but-empty slot from
	//one that has never been asked - see the tick.
	$ItemTip_Cache[%slot] = (%text $= "") ? "none" : %text;
}

//THE CACHE MUST DIE WHENEVER THE INVENTORY CHANGES.
//
//Slots are reused constantly - move a sword, drop it, loot a new one - and a stale entry
//would describe the item that used to be there. MonsterRPGx already announces every
//inventory rebuild through MonsterRPGx_ResetInvs, so that is the honest signal to clear on
//rather than a timer that guesses.
package MRPGItemTipCache
{
	function CLIENTCMDMonsterRPGx_ResetInvs(%invName, %invSize, %clearInv)
	{
		if(%invName $= "PlyrInv" || %invName $= "Initialize")
		{
			for(%i = 0; %i < $ItemTip::MaxCell; %i++)
			{
				$ItemTip_Cache[%i] = "";
				$ItemTip_Asked[%i] = 0;
			}
			MRPG_hideItemTip();
		}
		return Parent::CLIENTCMDMonsterRPGx_ResetInvs(%invName, %invSize, %clearInv);
	}
};
activatePackage(MRPGItemTipCache);

//Also clear one slot when the server updates just that cell, which is the common case
//after a transfer.
function MRPG_itemTipInvalidate(%slot)
{
	$ItemTip_Cache[%slot] = "";
	$ItemTip_Asked[%slot] = 0;
	if($ItemTip_Shown == %slot)
		MRPG_hideItemTip();
}

// Called from MRPG_ClientEnter(), NOT from client.cs. The panel is built here
// rather than at boot for the same reason the poll is started here: neither is
// wanted on a server that is not running MonsterRPG.
function MRPG_initItemTip()
{
	MRPG_buildItemTip();
	cancel($ItemTip_Sch);
	MRPG_itemTipTick();
}

// The other half. The panel itself is a child of MonsterRPGx_Main - a dialog that
// is only ever pushed on a MonsterRPG server - so hiding is enough; what has to
// stop is the poll.
function MRPG_itemTipShutdown()
{
	cancel($ItemTip_Sch);
	$ItemTip_Sch = "";

	MRPG_hideItemTip();
	$ItemTip_Slot  = -1;
	$ItemTip_Start = 0;

	//The per-slot cache describes the LAST server's inventory. Slots are reused
	//constantly, so carrying it across a rejoin would show the previous
	//character's weapon stats on this one's sword - the same staleness
	//MRPGItemTipCache clears on every inventory rebuild.
	for(%i = 0; %i < $ItemTip::MaxCell; %i++)
	{
		$ItemTip_Cache[%i] = "";
		$ItemTip_Asked[%i] = 0;
	}
}
