//////////////////////////////////////////////////////////////////////////////
// CorpseWindow.cs  -  the loot window for a body
//////////////////////////////////////////////////////////////////////////////
//
// The server registers a "Corpse" storage group with MonsterRPGx (Core_Corpse.cs) and
// hands the client a container whose cellType is "CorpseInv". MonsterRPGx then opens the
// window purely by NAME:
//
//     CLIENTCMDMonsterRPGx_ToggleInvWndw -> ("MonsterRPGx_" @ %wndw).setVisible(true)
//     (ClientCommands.cs:973)
//
// so if no control called MonsterRPGx_CorpseInv exists, the loot arrives, the cells are
// filled server-side, and absolutely nothing appears on screen. That is this file's whole
// job: make sure the control exists.
//
// BUILT AT RUNTIME rather than added to MonsterRPGx_Containers.gui, which is 126,736 lines
// holding ~70 of these. One more hand-written window there is ~500 lines of duplicated
// boilerplate that nobody will ever diff.
//
// THE CELLS ARE NOT HAND-BUILT EITHER. MonsterRPGx_NewInvBtn (Support.cs:37) already
// constructs a complete cell - background swatch, icon, stack-amount swatch and text, the
// info overlay with weight/stack/health readouts, and the GuiBitmapButtonCtrl wired to
// MonsterRPGx_SelectCell / MonsterRPGx_AuxSelect - with exactly the names the client cell
// code looks up (InvCellFunctions.cs:30,92). Re-deriving that here would be a second copy
// of a naming contract that is already load-bearing in ~70 places.
//
// It ends by adding the cell to MonsterRPGx_VehSpwnInvBG, which is not where we want it -
// so we re-home each cell afterwards. add() reparents in Torque, which is the same
// mechanism MonsterRPGx_MergeContainers relies on (Support.cs:29).
//////////////////////////////////////////////////////////////////////////////

//6 slots, matching $Corpse::Slots on the server. If these ever disagree the extra cells
//simply never fill - the server sends mxSlots and the client draws what it is given - but
//keep them in step so the window is not full of dead squares.
$CorpseWnd::Slots = 6;
$CorpseWnd::Cols  = 3;
$CorpseWnd::Cell  = 68;    // 64px cell + 4px gutter
$CorpseWnd::Pad   = 6;
$CorpseWnd::TitleH = 26;   // GuiWindowCtrl title bar

//Gap between the inventory's right edge and the corpse window, and the margin the window is
//kept inside the canvas by.
$CorpseWnd::Gap    = 8;
$CorpseWnd::Margin = 4;


//////////////////////////////////////////////////////////////////////////////
// WHERE THE WINDOW GOES
//
// It used to be `position = "300 240"`, hardcoded, which put it ON TOP OF THE INVENTORY.
// That is not a near miss - MonsterRPGx_BigInventory (the big grid) is a child of
// MonsterRPGx_PlyrInv at "-27 13" extent "535 548", so in canvas space it covers x 146..681,
// y 69..617. 300,240 is dead centre of it.
//
// Two things replace it, in priority order:
//
//   1. WHERE YOU LEFT IT. $pref::MRPG::CorpseWndPos is written every time the inventory
//      closes and wins on every subsequent open. Blockland exports $pref::* to
//      config/client/prefs.cs on quit, so it survives restarts with no file handling here.
//
//   2. A COMPUTED DEFAULT, for the first open ever and for a stored position that no longer
//      fits (someone dragged it to the corner at 1600x900 and then played at 1024x768).
//      Measured off the real controls rather than written down, because these are numbers in
//      a 126k-line .gui that this file does not own and cannot keep in step by hand.
//
// setPosition() DOES NOT EXIST on GuiControl in this engine - resize(x, y, w, h) is the only
// way to move a control, and calling the one that does not exist fails silently. Every move
// below goes through MRPG_corpseWndMoveTo for that reason.
//////////////////////////////////////////////////////////////////////////////

//Canvas-space top-left of a control. There is no getGlobalPosition, so walk the parent
//chain; a control's position is always relative to its immediate parent.
function MRPG_guiGlobalPos(%ctrl)
{
	if(!isObject(%ctrl))
		return "0 0";

	%x = 0;
	%y = 0;

	//Bounded rather than while(isObject(...)): a malformed tree would otherwise hang the
	//client, and no GUI in this game is 64 deep.
	for(%i = 0; %i < 64 && isObject(%ctrl); %i++)
	{
		// GuiGroup is a SimGroup and has no getPosition() method in Blockland.
		// GUI controls expose the same coordinate through their script field.
		%p = %ctrl.position;
		%x += getWord(%p, 0);
		%y += getWord(%p, 1);

		%ctrl = %ctrl.getGroup();

		//Canvas is the root and contributes no offset of its own.
		if(!isObject(%ctrl) || %ctrl.getName() $= "Canvas")
			break;
	}

	return %x SPC %y;
}

//Right edge of the player's inventory furniture, in canvas space.
//
//BOTH PANELS, not just the grid. MonsterRPGx_PlyrInvBitmap is the equipment column and sits
//immediately right of MonsterRPGx_BigInventory (x 681..833), so anchoring off the grid alone
//would drop the corpse window straight onto the equipment slots - the same complaint one
//panel to the right.
function MRPG_invRightEdge()
{
	%right = 0;

	%panels = "MonsterRPGx_BigInventory" TAB "MonsterRPGx_PlyrInvBitmap" TAB "MonsterRPGx_PlyrInv";

	for(%i = 0; %i < 3; %i++)
	{
		%c = getField(%panels, %i);
		if(!isObject(%c))
			continue;

		%edge = getWord(MRPG_guiGlobalPos(%c), 0) + getWord(%c.getExtent(), 0);
		if(%edge > %right)
			%right = %edge;
	}

	return %right;
}

//Clamp a wanted top-left so the whole window stays on screen.
function MRPG_corpseWndClamp(%pos, %w, %h)
{
	%cv = Canvas.getExtent();
	%cw = getWord(%cv, 0);
	%ch = getWord(%cv, 1);

	//Canvas not up yet - nothing sensible to clamp against, so take the request as-is.
	if(%cw <= 0 || %ch <= 0)
		return %pos;

	%x = getWord(%pos, 0);
	%y = getWord(%pos, 1);
	%m = $CorpseWnd::Margin;

	if(%x + %w > %cw - %m){ %x = %cw - %w - %m; }
	if(%y + %h > %ch - %m){ %y = %ch - %h - %m; }
	if(%x < %m){ %x = %m; }
	if(%y < %m){ %y = %m; }

	return %x SPC %y;
}

//The first-open position: to the right of everything the inventory owns, vertically lined up
//with the top of the grid, clamped into the canvas.
function MRPG_corpseWndDefaultPos(%w, %h)
{
	%x = MRPG_invRightEdge() + $CorpseWnd::Gap;

	%y = 69;
	if(isObject(MonsterRPGx_BigInventory))
		%y = getWord(MRPG_guiGlobalPos(MonsterRPGx_BigInventory), 1);

	//Nothing measurable (the inventory GUI never loaded) - the right third of the canvas is
	//still a better guess than the middle.
	if(%x <= $CorpseWnd::Gap)
	{
		%cw = getWord(Canvas.getExtent(), 0);
		%x = (%cw > 0) ? (%cw * 0.66) : 690;
	}

	return MRPG_corpseWndClamp(mFloor(%x) SPC mFloor(%y), %w, %h);
}

//THE ONLY WAY THIS FILE MOVES THE WINDOW. resize(), never setPosition().
function MRPG_corpseWndMoveTo(%pos)
{
	if(!isObject(MonsterRPGx_CorpseInv))
		return 0;

	%e = MonsterRPGx_CorpseInv.getExtent();
	%w = getWord(%e, 0);
	%h = getWord(%e, 1);

	%pos = MRPG_corpseWndClamp(%pos, %w, %h);

	MonsterRPGx_CorpseInv.resize(getWord(%pos, 0), getWord(%pos, 1), %w, %h);
	return 1;
}

//Called when the window is about to be shown. A stored position wins; anything unusable
//falls back to the computed default, which also repairs a position saved at a resolution
//this client is no longer running.
function MRPG_corpseWndRestore()
{
	if(!isObject(MonsterRPGx_CorpseInv))
		return 0;

	%e = MonsterRPGx_CorpseInv.getExtent();
	%pos = $pref::MRPG::CorpseWndPos;

	if(getWordCount(%pos) != 2 || (getWord(%pos,0) + 0) !$= getWord(%pos,0)
	   || (getWord(%pos,1) + 0) !$= getWord(%pos,1))
		%pos = MRPG_corpseWndDefaultPos(getWord(%e,0), getWord(%e,1));

	return MRPG_corpseWndMoveTo(%pos);
}

//Called when the inventory closes. Whatever the window's top-left is at that moment IS the
//remembered position - GuiWindowCtrl's drag writes straight into the control's bounds, so
//reading it back is reading where the player put it.
function MRPG_corpseWndRemember()
{
	if(!isObject(MonsterRPGx_CorpseInv))
		return 0;

	$pref::MRPG::CorpseWndPos = MonsterRPGx_CorpseInv.getPosition();
	return 1;
}


//////////////////////////////////////////////////////////////////////////////
// THE TWO HOOKS
//
// Packaged rather than edited into GUIFunctions.cs / ClientCommands.cs so the corpse window
// stays one self-contained file, the same discipline the server side uses for activateStuff.
//
// PARENT FIRST ON OPEN: CLIENTCMDMonsterRPGx_ToggleInvWndw is what calls setVisible(true),
// and moving a control before it is shown is fine but moving it after is what guarantees the
// position survives anything the parent does on the way.
//////////////////////////////////////////////////////////////////////////////
package MRPGCorpseWindow
{
	function CLIENTCMDMonsterRPGx_ToggleInvWndw(%action, %wndw)
	{
		%ret = Parent::CLIENTCMDMonsterRPGx_ToggleInvWndw(%action, %wndw);

		if(%wndw $= "CorpseInv" && %action $= "Open")
			MRPG_corpseWndRestore();

		return %ret;
	}

	//The dialog going to sleep is the one event that covers every way the inventory can
	//close - the X, escape, the keybind, and the server pushing "Close All".
	function MonsterRPGx_Main::onSleep(%obj)
	{
		MRPG_corpseWndRemember();
		return Parent::onSleep(%obj);
	}
};


function MRPG_buildCorpseWindow()
{
	//Already built, and rebuilding would orphan the cells the server is talking to.
	if(isObject(MonsterRPGx_CorpseInv))
		return MonsterRPGx_CorpseInv;

	//The window has to live inside the dialog MonsterRPGx actually pushes, or it will
	//never be drawn no matter how visible it is.
	if(!isObject(MonsterRPGx_Main))
	{
		error("MRPG_buildCorpseWindow: MonsterRPGx_Main not loaded - corpse looting will"
			SPC "have no window. Is System_MonsterRPGx enabled and its GUI exec'd?");
		return 0;
	}
	//...and specifically inside MonsterRPGx_SwGUIPar, which is where every other container
	//window lives. Nothing draws differently - SwGUIPar is "0 0" / "1024 768" inside Main -
	//but it is the set that CLIENTCMDMonsterRPGx_ResetInvs "Initialize" iterates to hide
	//stale windows, so a corpse parented straight to Main stays on screen after the
	//inventory that opened it has been closed and reopened. See MonsterRPGx_MergeContainers.
	%host = MonsterRPGx_SwGUIPar;
	if(!isObject(%host))
	{
		error("MRPG_buildCorpseWindow: MonsterRPGx_SwGUIPar missing - parenting the corpse"
			SPC "window to MonsterRPGx_Main instead. It will not auto-close on reopen.");
		%host = MonsterRPGx_Main;
	}
	if(!isFunction("MonsterRPGx_NewInvBtn"))
	{
		error("MRPG_buildCorpseWindow: MonsterRPGx_NewInvBtn missing - cannot build cells.");
		return 0;
	}

	%rows = mCeil($CorpseWnd::Slots / $CorpseWnd::Cols);
	%innerW = $CorpseWnd::Cols * $CorpseWnd::Cell;
	%innerH = %rows * $CorpseWnd::Cell;
	%w = %innerW + $CorpseWnd::Pad * 2;
	%h = %innerH + $CorpseWnd::Pad * 2 + $CorpseWnd::TitleH;

	//Same profile the stock container windows use, so a corpse looks like every other
	//container rather than like a debug panel.
	//Position is restored when the server opens this inventory. During client boot the canvas
	//is still assembling, so measuring and clamping it here is unsafe. Until then the hidden
	//window can sit at this harmless placeholder position.
	%wnd = new GuiWindowCtrl(MonsterRPGx_CorpseInv)
	{
		profile = "MonsterRPGx_BurntGlassBlue_WindowProfile";
		horizSizing = "right"; vertSizing = "bottom";
		position = "0 0"; extent = %w SPC %h; minExtent = "8 2";
		visible = "0";                      // the server decides when it opens
		clipToParent = "1";
		text = "Corpse";
		maxLength = "255";
		resizeWidth = "0"; resizeHeight = "0";
		canMove = "1"; canClose = "1"; canMinimize = "0"; canMaximize = "0";
		minSize = "50 50";

		//CLOSE EXACTLY THE WAY EVERY OTHER CONTAINER WINDOW DOES: pop the whole inventory
		//dialog. MonsterRPGx_Main::onSleep (GUIFunctions.cs:283) then sends closeInv, which
		//is what releases MonsterRPGx_AccInv and clears the corpse's parStorInv /
		//relStorInv / hasMStor fields server-side.
		//
		//An earlier version here hid just this window and sent closeInv itself. That looks
		//tidier but desynchronises the state machine: AccInv would be cleared while the
		//player's own inventory was still on screen and interactive, and several MonsterRPGx
		//paths treat !AccInv as "no inventory session" and refuse to act (Crafting.cs:142,
		//Displays.cs:209). Matching the stock path is worth more than the nicer behaviour.
		command = "canvas.popDialog(MonsterRPGx_Main);";
		closeCommand = "canvas.popDialog(MonsterRPGx_Main);";
		accelerator = "escape";
	};

	%bg = new GuiSwatchCtrl(MonsterRPGx_CorpseInvBG)
	{
		profile = "GuiDefaultProfile";
		horizSizing = "right"; vertSizing = "bottom";
		position = $CorpseWnd::Pad SPC ($CorpseWnd::TitleH);
		extent = %innerW SPC %innerH; minExtent = "8 2";
		color = "16 13 9 235";
	};
	%wnd.add(%bg);
	%host.add(%wnd);

	for(%n = 0; %n < $CorpseWnd::Slots; %n++)
	{
		%cx = (%n % $CorpseWnd::Cols) * $CorpseWnd::Cell + 2;
		%cy = mFloor(%n / $CorpseWnd::Cols) * $CorpseWnd::Cell + 2;

		//Builds the whole cell under its own hardcoded parent...
		MonsterRPGx_NewInvBtn("CorpseInv", %n, %cx, %cy, 0);

		//...and this brings it home. add() reparents.
		%cell = "MonsterRPGx_CorpseInv_ItemBGColor_" @ %n;
		if(isObject(%cell))
			%bg.add(%cell);
		else
			error("MRPG_buildCorpseWindow: cell" SPC %n SPC "was not created -"
				SPC "MonsterRPGx_NewInvBtn may have changed its naming.");
	}

	//MRPG_corpseWndRestore runs from the Open CorpseInv hook above, after the active GUI and
	//canvas dimensions are available.
	return %wnd;
}

//Build once the GUIs are loaded. MonsterRPGx_Main comes from System_MonsterRPGx, and
//add-on load order is not something this file can assume, so this is called from
//client.cs after the merge rather than at exec time.
function MRPG_initCorpseWindow()
{
	//NOT AT FILE SCOPE. This add-on is loaded on every server the player joins, so anything
	//that runs at exec time runs whether or not this is a MonsterRPG server. client.cs calls
	//this on line 81, after ClientCommands.cs and GUIFunctions.cs have defined the two
	//functions the package wraps - which is what makes Parent:: resolve to them rather than
	//to nothing.
	//
	//activatePackage IS the idempotence guard: Namespace::activatePackage returns early when
	//the package is already active (consoleInternal.cc:975), so a re-exec is a no-op rather
	//than a second copy on the stack. Never deactivate it to "refresh" - activation is a
	//stack and pulling one out of the middle reorders every wrapper above it.
	activatePackage(MRPGCorpseWindow);

	MRPG_buildCorpseWindow();
}
