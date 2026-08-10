//////////////////////////////////////////////////////////////////////////////
// ServerGate.cs  -  the one switch that decides whether ANY of this runs
//////////////////////////////////////////////////////////////////////////////
//
// Client_MonsterRPG is loaded on EVERY server the player joins. Blockland execs
// every client add-on once at startup and never unloads one, so "am I on a
// MonsterRPG server?" is a question this add-on has to answer before it does
// anything at all - and it has to answer it correctly the moment the player
// LEAVES, not just the moment they arrive.
//
// -----------------------------------------------------------------------------
// THE FAULT THIS EXISTS TO KILL
// -----------------------------------------------------------------------------
//
//     Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Minimap.cs (587): Unable to find
//     object: 'ServerConnection' attempting to call function 'getControlObject'
//     BackTrace: ->Minimap_HeadingTick->Minimap_HeadingNow
//
// Sixteen of those a second, forever, from the moment the player left a server -
// because Minimap.cs built its panel from a file-scope schedule six seconds after
// the GAME started and then ran a 60ms heading tick that nothing ever stopped.
//
// One missing guard, but the SHAPE of the fault is the thing worth naming: a
// repeating schedule that no connection owns outlives the connection it was
// started for. Guarding line 587 alone would have silenced this instance and left
// the loop running, on every server, for every player, forever.
//
// -----------------------------------------------------------------------------
// THE RULES
// -----------------------------------------------------------------------------
//
//   1. NOTHING STARTS AT FILE SCOPE. File scope may set constants, define
//      functions, define and activate a package, and register a keybind. It may
//      not build a GUI, arm a schedule, or talk to a server.
//
//   2. Work starts in MRPG_ClientEnter() and stops in MRPG_ClientLeave(). One
//      pair, called from one place each, so there is exactly one answer to
//      "what is running right now".
//
//   3. EVERY self-rescheduling tick re-checks MRPG_isActive() at its top and
//      returns WITHOUT rescheduling when it is false. This is the belt to (2)'s
//      braces: if a shutdown is ever missed, the leak costs one tick period
//      instead of the rest of the session.
//
//   4. Anything that reads ServerConnection or calls commandToServer is gated
//      too. "Connected" and "connected to US" are different questions and only
//      the second one is safe to act on - a relay left live sends MonsterRPG
//      commands to somebody else's server on every keypress.
//
//   5. A key this add-on BINDS OVER stays bound - Options > Keyboard has to list
//      it and config.cs has to be able to remap it - but off a MonsterRPG server
//      the action FORWARDS TO WHAT IT REPLACED rather than swallowing the press.
//      m -> ToggleCursor, lalt -> toggleSuperShift, 1-0 -> useFirst..TenthSlot,
//      j/k -> shiftBrickLeft/Towards. Silently eating those keys everywhere else
//      is a worse bug than the panel the gate is suppressing: the player gets a
//      dead keyboard with nothing to explain it.
//
// -----------------------------------------------------------------------------
// WHAT IS DELIBERATELY *NOT* GATED
// -----------------------------------------------------------------------------
//
// Rule 3 is about loops that can outlive a connection. These repeat but cannot,
// and gating them would be noise or actively wrong:
//
//   MRPG_loadingTick / BarTick / Finish   stop on $MRPG_Loading, which
//                                         MRPG_hideLoading clears - and the leave
//                                         path calls it. Gating on MRPG_isActive
//                                         would strand the cover on a drop.
//   MRPG_incForm / incBreakAway /         index-driven animation chains that end
//   incChantFade / incShake               after a fixed number of steps (<1s).
//                                         MRPG_incantAbort stops the QTE itself.
//   updateDragPosition                    stops on %this.dragging, cleared by the
//                                         mouse-up on our own scrollbar.
//   initScrollableText                    a bounded retry (100 tries) waiting for
//                                         MonsterRPGx_Main.gui, which execs a few
//                                         hundred ms after this file.
//
// If you add a repeating schedule, it belongs in one of those two lists. Decide
// which before you write it.
//
// -----------------------------------------------------------------------------
// TWO HELLOS, EITHER WILL DO
// -----------------------------------------------------------------------------
//
// The server announces itself twice, from two different points in the connect
// sequence:
//
//   addMRPGClientToServer   GameConnection::autoAdminCheck    Core_OLDpackage.cs
//   MRPG_Hello              GameConnection::onClientEnterGame Core_LoadingScreen.cs
//
// autoAdminCheck is called from stock's GameConnection::startLoad, so the first
// lands well BEFORE the mission download finishes; onClientEnterGame lands after
// it. Both route through MRPG_ClientEnter and it is idempotent, so whichever
// arrives first opens the gate and a server that sends only one of them still
// works. Neither is ever sent by a server without the add-on, which is the whole
// point.
//////////////////////////////////////////////////////////////////////////////


//////////////////////////////////////////////////////////////////////////////
// THE QUESTION
//
// TWO CONDITIONS, AND THE SECOND IS NOT REDUNDANT. stock's disconnect() deletes
// ServerConnection BEFORE it calls disconnectedCleanup(), which is where our
// teardown hangs - so there is a window where the flag is still set and the
// connection is already gone. That window is exactly where the error above came
// from, and testing the object as well as the flag closes it without depending
// on our own cleanup having run yet.
//////////////////////////////////////////////////////////////////////////////
function MRPG_isActive()
{
	return ($MonsterRPG::Client::inMonsterRPGServer && isObject(ServerConnection)) ? 1 : 0;
}

// For bound keys: the action may run only if this client is on a MonsterRPG
// server AND the press is the key-DOWN edge. Every MonsterRPG bind is a toggle,
// and without the edge test each one fires twice per press and lands back where
// it started, which reads as the key doing nothing.
function MRPG_gateKey(%val)
{
	return (%val && MRPG_isActive()) ? 1 : 0;
}


//////////////////////////////////////////////////////////////////////////////
// ENTER
//////////////////////////////////////////////////////////////////////////////
function MRPG_ClientEnter()
{
	if($MonsterRPG::Client::inMonsterRPGServer)
		return;

	$MonsterRPG::Client::inMonsterRPGServer = 1;
	$MRPG::ServerHasMonsterRPG              = 1;

	//THE KEYS ARE BORROWED, NOT KEPT. Every MonsterRPG bind is written into moveMap
	//here and handed back in MRPG_ClientLeave, so a player who is not on this
	//server has a keyboard with no trace of the add-on on it. Keybinds.cs has the
	//full derivation, including why config/client/config.cs must never see one.
	if(isFunction("MRPG_borrowKeys"))
		MRPG_borrowKeys();

	//THE LAZY PANELS, RE-ARMED. Both of these used to arm themselves six seconds
	//after the game launched; they arm six seconds after the JOIN now, which is
	//the same delay measured from the event that actually matters.
	//
	//SCHEDULED, NOT CALLED. Both are fallbacks for a server that never sends
	//addMonsterRPGGUI - that command is what builds them in the right draw order,
	//and building here would land BEFORE it and put the panel underneath the HUD.
	//See the build hooks in Minimap.cs and CampPanel.cs.
	cancel($MRPG::Gate::MinimapSch);
	cancel($MRPG::Gate::CampSch);

	if(isFunction("Minimap_EnsureBuilt"))
		$MRPG::Gate::MinimapSch = schedule(6000, 0, Minimap_EnsureBuilt);

	if(isFunction("MRPGCamp_EnsureBuilt"))
		$MRPG::Gate::CampSch = schedule(6000, 0, MRPGCamp_EnsureBuilt);

	//The inventory tooltip poll. Cheap, but it is still a 60ms schedule and it has
	//no business running in the main menu.
	if(isFunction("MRPG_initItemTip"))
		MRPG_initItemTip();

	//The settings gear, and - more importantly - the player's saved audio devices
	//and volumes pushed into the DLL. AFTER MRPG_borrowKeys, because the gear prints
	//the settings key underneath itself and that key is not bound until the borrow
	//has run. It retries on its own if NewChatHud is not up yet.
	if(isFunction("MRPGSettings_Start"))
		MRPGSettings_Start();

	//THE MICROPHONE INDICATOR'S POLL, STARTED FROM THE GATE.
	//
	//It used to be started only by the audio link coming up, and that is one
	//ordering away from never starting at all: MRPGVoiceIcon_Tick returns without
	//rescheduling when this client is not on a MonsterRPG server, so an audio invite
	//that arrived before this function ran killed the poll permanently and silently.
	//Started here the order cannot invert. The link still calls it too, which costs
	//nothing - the tick cancels its own pending schedule first.
	if(isFunction("MRPGVoiceIcon_Start"))
		MRPGVoiceIcon_Start();
}


//////////////////////////////////////////////////////////////////////////////
// LEAVE
//
// Called from disconnectedCleanup (which stock's disconnect(), onConnectionDropped,
// onConnectionTimedOut and onConnectionError ALL funnel into) and from onExit.
// Idempotent, because more than one of those can fire for a single departure.
//////////////////////////////////////////////////////////////////////////////
function MRPG_ClientLeave()
{
	//Also what keeps this file free for a player who never joins a MonsterRPG
	//server: every disconnect they ever make returns on this line.
	if(!$MonsterRPG::Client::inMonsterRPGServer && !$MRPG::ServerHasMonsterRPG)
		return;

	//CLEARED FIRST, before anything that could fail. Every tick and every relay
	//asks MRPG_isActive(), so this single write stops all of them at their next
	//edge even if a shutdown below hits a missing object and aborts the rest.
	$MonsterRPG::Client::inMonsterRPGServer = 0;
	$MRPG::ServerHasMonsterRPG              = 0;

	cancel($MRPG::Gate::MinimapSch);  $MRPG::Gate::MinimapSch = "";
	cancel($MRPG::Gate::CampSch);     $MRPG::Gate::CampSch    = "";

	//---- modules that own their own teardown --------------------------------
	//isFunction on each: this file must not be the reason a join breaks when a
	//script is removed from client.cs.
	//
	//THE STOCK HUD RESTORE LIVES HERE, not in the caller, and that is a correctness
	//requirement rather than a preference. This package is activated LAST so it is
	//the OUTERMOST disconnectedCleanup wrapper - the flags are down by the time
	//Parent:: reaches Package.cs's handler, so anything that tests them there would
	//early-out and the player would land in the main menu still wearing the RPG
	//cursor and a hidden stock HUD.
	//GIVE THE KEYBOARD BACK FIRST. Every key this add-on took goes back to whatever
	//held it before we joined, our rows come out of Options > Keyboard, and the
	//player's own remaps are saved to prefs for the next join. See Keybinds.cs.
	if(isFunction("MRPG_returnKeys"))             MRPG_returnKeys();

	if(isFunction("MonsterRPGx_ClearData"))       MonsterRPGx_ClearData();
	if(isFunction("MonsterRPGx_RestoreStockHud")) MonsterRPGx_RestoreStockHud();

	//BEFORE the rest: it sends a goodbye over a live socket, and the sooner the
	//server drops our routing flag the shorter the window in which it is still
	//sending audio to a client that has gone.
	if(isFunction("MRPGAudioNative_Shutdown")) MRPGAudioNative_Shutdown();
	if(isFunction("MRPGVoiceIcon_Shutdown"))   MRPGVoiceIcon_Shutdown();
	if(isFunction("MRPGSettings_Shutdown"))    MRPGSettings_Shutdown();

	if(isFunction("Minimap_Shutdown"))     Minimap_Shutdown();
	if(isFunction("MRPGCamp_Shutdown"))    MRPGCamp_Shutdown();
	if(isFunction("MRPG_itemTipShutdown")) MRPG_itemTipShutdown();
	if(isFunction("MRPGChat_Disable"))     MRPGChat_Disable();
	if(isFunction("MRPGPlates_Stop"))      MRPGPlates_Stop();
	if(isFunction("MRPGStreak_Reset"))     MRPGStreak_Reset();
	if(isFunction("MRPG_tipClear"))        MRPG_tipClear();
	if(isFunction("MRPG_hideItemTip"))     MRPG_hideItemTip();
	if(isFunction("MRPG_hideLoading"))     MRPG_hideLoading("disconnected");

	//THE ONE THAT COSTS THE PLAYER THEIR KEYBOARD IF IT IS MISSED. The tier-2
	//incantation QTE pushes an ActionMap that binds every letter key, and a pushed
	//map outranks moveMap - so left on the stack it captures every letter for the
	//rest of the session, on every server and in the main menu. It used to be
	//popped by accident, when MRPG_incantCDTick eventually timed out; that tick is
	//gated now, so the pop has to be explicit. See MRPG_incantAbort.
	if(isFunction("MRPG_incantAbort"))     MRPG_incantAbort();

	//---- schedules whose module has no teardown of its own -------------------
	cancel($MRPG_TipSch);          $MRPG_TipSch          = "";
	cancel($MRPG_QuestRenderSch);  $MRPG_QuestRenderSch  = "";
	cancel($MRPG_SpellCD_Sch);     $MRPG_SpellCD_Sch     = "";
	cancel($MRPG_HudCD_Sch);       $MRPG_HudCD_Sch       = "";
	cancel($CS_TickSch);           $CS_TickSch           = "";
	cancel($CS_PreloadSch);        $CS_PreloadSch        = "";
	cancel($CS_GateWaitSch);       $CS_GateWaitSch       = "";
	cancel($TC_TickSch);           $TC_TickSch           = "";
	cancel($Dlg_Sch);              $Dlg_Sch              = "";
	cancel($MRPGCM_TickSch);       $MRPGCM_TickSch       = "";
	cancel($Inc_CDSch);            $Inc_CDSch            = "";
	cancel($Inc_SetSch);           $Inc_SetSch           = "";
	cancel($FFX_Tick);             $FFX_Tick             = "";
	cancel($SA_Fade);              $SA_Fade              = "";
	cancel($doExpSch);             $doExpSch             = "";
	cancel($doExpFadeSch);         $doExpFadeSch         = "";
	cancel($CS_LayoutSch);         $CS_LayoutSch         = "";
	cancel($MonsterRPG::Client::UnhighlightSchedule);
	//The exp fill re-arms itself off this flag, so cancelling the schedule without
	//clearing it would let the next updateExpBitmap think a loop was already running and
	//never start one - a bar that silently stops animating for the rest of the session.
	$MonsterRPG::Client::inExpLoop = 0;

	//---- the "this screen is open" flags those ticks read --------------------
	//
	//CLEARED AS WELL AS CANCELLED, and that ordering matters. Most of these ticks
	//re-arm themselves from a flag rather than from a caller, so a cancel alone
	//leaves them one stray call away from running again for the rest of the
	//session. Clearing the flag is what makes the cancel final.
	$CS_Open       = 0;
	$TC_Open       = 0;
	$Dlg_Open      = 0;
	$MRPGCM_Open   = 0;
	$MRPGProf_Open = 0;
	$MRPG_AttrOpen = 0;
	$Inc_Active    = 0;
	$CS_Preloaded  = 0;
	$CS_GateHeld   = 0;

	//---- server-driven INPUT state ------------------------------------------
	//
	// THE ONES THAT ACTUALLY HURT. These are set by the MonsterRPG server and read
	// by packages that wrap useBricks / useTools / useSprayCan / moveForward, so a
	// value left behind does not idle quietly - it disables building, or redirects
	// movement keys, on the NEXT server the player joins, with nothing on screen
	// to explain why. Resetting them here is not tidiness, it is the difference
	// between leaving a server and leaving a server with a broken game.
	$Player_BricksActive    = 0;
	$Player_ToolsActive     = 0;
	$Player_PaintsActive    = 0;
	$Player_EyeVector       = "";
	$Player_ForwardVector   = "";
	$Hata_MoveInputOverride = 0;
	$Hata_MoveForward = 0;  $Hata_MoveBackward = 0;
	$Hata_MoveLeft    = 0;  $Hata_MoveRight    = 0;
	$Hata_MoveUp      = 0;  $Hata_MoveDown     = 0;
	$camSyncOverride  = 0;
	cancel($KeyInputCamSyncSched);  $KeyInputCamSyncSched = "";

	//The free-mouse pointer, which is player-visible: left set, MRPG_RestoreCursor
	//re-asserts a cursor on the next server every time a dialog closes.
	$MRPG_FreeMouseOn = 0;
}


//////////////////////////////////////////////////////////////////////////////
// THE HOOKS
//
// disconnectedCleanup IS THE COMPLETE SET ON ITS OWN. Stock funnels every way a
// client can lose a server into it - disconnect() calls it directly, and so do
// onConnectionDropped, onConnectionTimedOut and onConnectionError
// (allClientScripts.cs:2524-2540, 2627-2656). onExit is the one path that does
// not go through it: quitting straight from a server.
//
// This package is activated LAST (client.cs) so it sits at the top of the stack
// and MRPG_ClientLeave runs BEFORE every other module's cleanup. That order is
// deliberate - clearing the flag first means anything downstream that re-enters
// a tick finds the gate already shut.
//////////////////////////////////////////////////////////////////////////////
package MRPGServerGate
{
	function disconnectedCleanup(%doReconnect)
	{
		MRPG_ClientLeave();
		return Parent::disconnectedCleanup(%doReconnect);
	}

	function onExit()
	{
		MRPG_ClientLeave();
		Parent::onExit();
	}
};

//Armed from the bottom of client.cs rather than here, so it activates after every
//other package in the add-on and therefore wraps them. Blockland exports isPackage
//and activatePackage but not isActivePackage, and TGE's activatePackage returns
//early when the package is already on, so calling it twice is a no-op - but it is
//still called exactly once.
function MRPG_ArmServerGate()
{
	activatePackage(MRPGServerGate);
}
