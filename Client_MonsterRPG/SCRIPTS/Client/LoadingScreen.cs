//////////////////////////////////////////////////////////////////////////////
// LoadingScreen.cs  -  cover the world until it is actually ready to be seen
//////////////////////////////////////////////////////////////////////////////
//
// WHAT IT FIXES. Mission load finishing does NOT mean the world is ready here. When the
// client's loading bar disappears the player is dropped into an observer camera over
// terrain that is still being planted, with no body, no character screen yet, and no
// explanation - which reads exactly like the game has hung. This holds an opaque screen
// over all of that until the server says the world is genuinely ready for them.
//
// IT IS A COVER, NOT A GATE. The authority over whether the player gets a body is the
// server's character gate (Core_CharacterGate.cs); this only decides what is on screen.
// Deliberately so - a client-side gate is not a gate at all, and a client that fails to
// load this file must still be able to play.
//
// WHY A DIALOG RATHER THAN A GUI SWAP. Pushing a dialog leaves PlayGui underneath, running
// and rendering, so nothing about the mission's state changes. Swapping the content control
// tears down PlayGui and takes the camera and HUD with it - much more disruptive, and it
// would have to be undone in exactly the right order.
//
// NO 3D CONTENT ON THIS SCREEN. Flat colour and text only. A GuiObjectView here would load
// a DTS while the renderer is still relinking shaders after mission load, which is the
// documented way to crash Blockland.exe on join - see the note in Core_CharacterGate.cs.
//
// INPUT IS BLOCKED by the dialog itself: a dialog with a full-screen opaque control eats
// mouse and keyboard, so the RPG panels underneath cannot be opened or clicked. The hotkey
// handlers also check $MRPG_Loading, because a bound key is dispatched by the input map
// rather than the GUI and would otherwise still fire.
//////////////////////////////////////////////////////////////////////////////

$MRPG_Loading = 0;

//Safety valve. If the server never sends the ready signal - old server build, add-on
//disabled, dropped packet - the screen lifts itself rather than locking the player out of
//their own game. A cover that can strand someone is worse than no cover.
if($MRPG::Loading::MaxMs $= ""){ $MRPG::Loading::MaxMs = 45000; }

//The status line cycles while waiting so it is visibly alive rather than apparently frozen.
if($MRPG::Loading::TickMs $= ""){ $MRPG::Loading::TickMs = 400; }


//////////////////////////////////////////////////////////////////////////////
// BUILD
//////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////
// THE MODAL PROFILE  -  what actually blocks input
//
// A pushed dialog only swallows mouse and keyboard if its profile has modal = 1. The first
// version used GuiDefaultProfile, which does NOT - so the cover would have been purely
// cosmetic and every RPG panel would still have opened and been clickable behind it, which
// is precisely what this is supposed to prevent. Confirmed against the decompiled stock
// profiles (allClientScripts.cs:19102), where every real dialog sets modal = 1.
//////////////////////////////////////////////////////////////////////////////
if(!isObject(MRPG_LoadingProfile))
new GuiControlProfile(MRPG_LoadingProfile)
{
	opaque = 1;
	fillColor = "8 10 14 255";
	border = 0;
	modal = 1;
	justify = "center";
	fontType = "Palatino Linotype";
	fontSize = 16;
	fontColor = "200 200 200";
};


function MRPG_buildLoadingScreen()
{
	//REBUILD IF THE EXISTING DIALOG PREDATES THE BAR.
	//
	//This used to return on isObject(MRPG_LoadingDlg) alone. Re-exec the client scripts in a
	//running session - which happens constantly during development - and the dialog from
	//before this file gained a progress bar survives, so the bar silently never appears and
	//the change looks like it did not work. Checking for a part that only the current
	//version builds is what makes a re-exec pick up the new screen.
	if(isObject(MRPG_LoadingDlg))
	{
		if(isObject(MRPG_LoadingBarBG))
			return MRPG_LoadingDlg;
		if($MRPG_Loading)          // never tear it down while it is covering the world
			return MRPG_LoadingDlg;
		MRPG_LoadingDlg.delete();
	}

	%res = getWord(getRes(), 0) SPC getWord(getRes(), 1);

	new GuiControl(MRPG_LoadingDlg)
	{
		profile = "MRPG_LoadingProfile";
		horizSizing = "width";
		vertSizing = "height";
		position = "0 0";
		extent = %res;

		//The backdrop. Opaque and full-screen: this is the thing that actually hides the
		//half-built world and swallows the clicks.
		new GuiBitmapCtrl(MRPG_LoadingBG)
		{
			profile = "GuiDefaultProfile";
			horizSizing = "width";
			vertSizing = "height";
			position = "0 0";
			extent = %res;
			bitmap = $MRPG::Loading::Bitmap;
			wrap = "0";
		};

		new GuiSwatchCtrl(MRPG_LoadingTint)
		{
			profile = "GuiDefaultProfile";
			horizSizing = "width";
			vertSizing = "height";
			position = "0 0";
			extent = %res;
			//Dark enough to be opaque on its own if the bitmap is missing, which is the
			//failure that would otherwise leave the world visible through the "cover".
			color = "8 10 14 255";
		};

		new GuiMLTextCtrl(MRPG_LoadingTitle)
		{
			profile = "MonsterRPGx_BurntGlassBlue_TextProfile";
			horizSizing = "center";
			vertSizing = "center";
			position = "0 0";
			extent = %res;
		};
	};

	//------------------------------------------------------------------
	// THE BAR
	//
	// Three plain swatches - groove, fill, and a highlight along the top of the fill so it
	// reads as lit rather than as a flat rectangle. Swatches only: no bitmaps to fail to
	// load, and nothing 3D, which this screen must never contain (see the header).
	//
	// Centred by arithmetic AND by horizSizing "center", so it stays put at any resolution.
	//------------------------------------------------------------------
	%bw = mFloor(getWord(%res, 0) * 0.42);
	if(%bw < 320) %bw = 320;
	%bh = 18;
	%bx = mFloor((getWord(%res, 0) - %bw) / 2);
	%by = mFloor(getWord(%res, 1) * 0.62);

	%groove = new GuiSwatchCtrl(MRPG_LoadingBarBG)
	{
		profile = "GuiDefaultProfile"; horizSizing = "center"; vertSizing = "center";
		position = %bx SPC %by; extent = %bw SPC %bh; minExtent = "8 2";
		color = "20 24 32 255";
	};
	MRPG_LoadingDlg.add(%groove);

	//a hairline inside the groove so the empty part still has an edge to read against
	%inner = new GuiSwatchCtrl()
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "1 1"; extent = (%bw - 2) SPC (%bh - 2); minExtent = "2 2";
		color = "10 12 17 255";
	};
	%groove.add(%inner);

	//The fill starts at ZERO WIDTH. A bar that appears already part-full is a lie about
	//progress before a single byte has been accounted for.
	%fill = new GuiSwatchCtrl(MRPG_LoadingBarFill)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "height";
		position = "1 1"; extent = "0" SPC (%bh - 2); minExtent = "0 2";
		color = "196 150 74 255";
	};
	%groove.add(%fill);

	%gloss = new GuiSwatchCtrl(MRPG_LoadingBarGloss)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "1 1"; extent = "0 6"; minExtent = "0 1";
		color = "236 206 140 255";
	};
	%groove.add(%gloss);

	//Percentage, under the bar. Its own control rather than part of the title, because the
	//title carries the phase text and the two update on different rhythms.
	%pct = new GuiMLTextCtrl(MRPG_LoadingPct)
	{
		profile = "GuiMLTextProfile"; horizSizing = "center"; vertSizing = "center";
		position = %bx SPC (%by + %bh + 8); extent = %bw SPC 20; minExtent = "8 2";
	};
	MRPG_LoadingDlg.add(%pct);

	$MRPG_LoadingBarW = %bw - 2;

	//Title above tint so the tint cannot hide it; the swatch is added after the bitmap so
	//it darkens rather than replaces it.
	MRPG_LoadingDlg.pushToBack(MRPG_LoadingBG);

	return MRPG_LoadingDlg;
}


//Optional custom background. A .png/.jpg beside this add-on; absent is fine - the tint
//alone is already opaque.
if($MRPG::Loading::Bitmap $= "")
	$MRPG::Loading::Bitmap = "Add-Ons/Client_MonsterRPG/GUIs/loading";


//////////////////////////////////////////////////////////////////////////////
// PREDICTIVE PROGRESS
//
// The server sends PHASE TEXT, never a percentage - so a bar driven only by what arrives
// would sit dead still between messages and then jump. This predicts instead, from three
// sources, in order of how much they can be trusted:
//
//   1  PHASE. Each known status maps to the percentage that phase ENDS at. Arriving at a
//      phase snaps the floor up to where the previous one finished, so real progress
//      always wins over any guess made below.
//
//   2  TIME, against how long the LAST load actually took. That figure is kept in a pref
//      and re-measured every load, so the pacing fits this machine and this world size
//      rather than a number picked once. First ever run has no history and falls back to
//      $MRPG::Loading::GuessMs.
//
//   3  CREEP. Even with both of the above wrong, the bar eases toward - but never reaches
//      - the end of the current phase. It cannot stall and it cannot overtake real
//      progress, which is the pair of failures that make a loading bar untrustworthy.
//
// IT NEVER GOES BACKWARDS. A bar that retreats reads as the game having failed, so the
// drawn value is a floor that only ever rises.
//////////////////////////////////////////////////////////////////////////////

//Smooth enough to read as motion rather than stepping. The text tick stays slow.
if($MRPG::Loading::BarMs $= ""){ $MRPG::Loading::BarMs = 33; }

//Used only until this client has ever completed one load.
if($MRPG::Loading::GuessMs $= ""){ $MRPG::Loading::GuessMs = 20000; }

//How much of the gap to the next phase the creep is allowed to eat. Below 1 by definition:
//the bar must never arrive somewhere the server has not confirmed.
if($MRPG::Loading::CreepFrac $= ""){ $MRPG::Loading::CreepFrac = 0.80; }

//Phase table. Matched by SUBSTRING so wording can change server-side without silently
//falling back to time-only pacing. Values are where each phase ENDS.
function MRPG_loadingPhasePct(%status)
{
	%s = strlwr(%status);
	if(strstr(%s, "connect") >= 0)    return 8;
	if(strstr(%s, "download") >= 0)   return 18;
	if(strstr(%s, "mission") >= 0)    return 28;
	if(strstr(%s, "terrain") >= 0)    return 45;
	if(strstr(%s, "world") >= 0)      return 55;
	if(strstr(%s, "brick") >= 0)      return 68;
	if(strstr(%s, "plant") >= 0)      return 68;
	if(strstr(%s, "dungeon") >= 0)    return 78;
	if(strstr(%s, "spawn") >= 0)      return 88;
	if(strstr(%s, "character") >= 0)  return 94;
	if(strstr(%s, "ready") >= 0)      return 99;
	return -1;   // unknown phase - leave the floor alone and let time/creep carry it
}

function MRPG_loadingResetProgress()
{
	$MRPG_LoadingShown  = 0;    // what is drawn
	$MRPG_LoadingFloor  = 0;    // confirmed by a phase message
	$MRPG_LoadingCeil   = 0;    // end of the current phase
	$MRPG_LoadingPhaseAt = getRealTime();
}

//A phase message landed: raise the confirmed floor and open the next ceiling.
function MRPG_loadingNoteStatus(%status)
{
	%pct = MRPG_loadingPhasePct(%status);
	if(%pct < 0)
		return;

	if(%pct > $MRPG_LoadingFloor)
	{
		$MRPG_LoadingFloor = $MRPG_LoadingCeil;   // previous phase is genuinely finished
		$MRPG_LoadingCeil  = %pct;
		$MRPG_LoadingPhaseAt = getRealTime();
	}
	if($MRPG_LoadingShown < $MRPG_LoadingFloor)
		$MRPG_LoadingShown = $MRPG_LoadingFloor;
}

function MRPG_loadingDraw()
{
	%p = $MRPG_LoadingShown;
	if(%p < 0) %p = 0;
	if(%p > 100) %p = 100;

	if(isObject(MRPG_LoadingBarFill))
	{
		%w = mFloor($MRPG_LoadingBarW * %p / 100);
		%h = getWord(MRPG_LoadingBarFill.getExtent(), 1);
		MRPG_LoadingBarFill.resize(1, 1, %w, %h);
		if(isObject(MRPG_LoadingBarGloss))
			MRPG_LoadingBarGloss.resize(1, 1, %w, 6);
	}
	if(isObject(MRPG_LoadingPct))
		MRPG_LoadingPct.setText("<just:center><font:verdana bold:12><color:C9A24E>"
			@ mFloor(%p) @ "%");
}

function MRPG_loadingBarTick()
{
	cancel($MRPG_LoadingBarSch);
	if(!$MRPG_Loading)
		return;

	%elapsed = getRealTime() - $MRPG_LoadingStart;

	//TIME-BASED ESTIMATE, paced by what the last load on this machine really took.
	%expect = $pref::MRPG::LastLoadMs;
	if(%expect $= "" || %expect < 2000)
		%expect = $MRPG::Loading::GuessMs;
	%byTime = (%elapsed / %expect) * 100;

	//CREEP toward the end of the current phase without touching it.
	%ceil = ($MRPG_LoadingCeil > 0) ? $MRPG_LoadingCeil : 100;
	%room = %ceil - $MRPG_LoadingFloor;
	if(%room < 0) %room = 0;
	//asymptotic: fast at first, slower the closer it gets - so a long phase still moves
	%since = getRealTime() - $MRPG_LoadingPhaseAt;
	%k = 1 - 1 / (1 + %since / 2200);
	%byCreep = $MRPG_LoadingFloor + %room * $MRPG::Loading::CreepFrac * %k;

	%want = (%byTime > %byCreep) ? %byTime : %byCreep;

	//never past the phase ceiling, and never backwards
	if(%want > %ceil) %want = %ceil;
	if(%want < $MRPG_LoadingShown) %want = $MRPG_LoadingShown;

	//ease so the bar glides instead of snapping when a phase lands
	$MRPG_LoadingShown = $MRPG_LoadingShown + (%want - $MRPG_LoadingShown) * 0.18;

	MRPG_loadingDraw();
	$MRPG_LoadingBarSch = schedule($MRPG::Loading::BarMs, 0, MRPG_loadingBarTick);
}

//Run the bar out to 100 before the screen lifts. Snapping from 60% to gone is the moment
//that makes the whole bar feel fake, so the finish is always drawn.
//
//%reason is carried through to MRPG_hideLoading so the log still says why it came down.
function MRPG_loadingFinish(%reason)
{
	cancel($MRPG_LoadingBarSch);

	$MRPG_LoadingShown = $MRPG_LoadingShown + (100 - $MRPG_LoadingShown) * 0.34;
	if($MRPG_LoadingShown > 99.4)
		$MRPG_LoadingShown = 100;
	MRPG_loadingDraw();

	if($MRPG_LoadingShown >= 100)
	{
		MRPG_hideLoading(%reason);
		return;
	}
	$MRPG_LoadingBarSch = schedule($MRPG::Loading::BarMs, 0, MRPG_loadingFinish, %reason);
}


//////////////////////////////////////////////////////////////////////////////
// SHOW / HIDE
//////////////////////////////////////////////////////////////////////////////
function MRPG_showLoading(%reason)
{
	MRPG_buildLoadingScreen();

	if(!$MRPG_Loading)
	{
		Canvas.pushDialog(MRPG_LoadingDlg);
		$MRPG_Loading = 1;
		$MRPG_LoadingStart = getRealTime();
		MRPG_loadingResetProgress();
		MRPG_loadingDraw();
		echo("MRPG loading screen: shown (" @ %reason @ ")");
	}

	MRPG_setLoadingText(%reason);
	MRPG_loadingNoteStatus(%reason);

	cancel($MRPG_LoadingSch);
	$MRPG_LoadingSch = schedule($MRPG::Loading::TickMs, 0, MRPG_loadingTick);

	cancel($MRPG_LoadingBarSch);
	$MRPG_LoadingBarSch = schedule($MRPG::Loading::BarMs, 0, MRPG_loadingBarTick);
}


function MRPG_hideLoading(%reason)
{
	cancel($MRPG_LoadingSch);
	cancel($MRPG_LoadingBarSch);

	if(!$MRPG_Loading)
		return;

	//LEARN THE PACE. Record how long this load really took so the next one is predicted
	//from this machine and this world rather than a fixed guess. A timeout is NOT a
	//measurement - recording it would teach the bar to expect a failure - so only a normal
	//finish is kept, and it is blended with the stored value so one odd load does not
	//swing the estimate.
	if(%reason !$= "timeout")
	{
		%took = getRealTime() - $MRPG_LoadingStart;
		if(%took > 1500 && %took < 600000)
		{
			%prev = $pref::MRPG::LastLoadMs;
			if(%prev $= "" || %prev < 2000)
				$pref::MRPG::LastLoadMs = %took;
			else
				$pref::MRPG::LastLoadMs = mFloor(%prev * 0.6 + %took * 0.4);
		}
	}

	$MRPG_Loading = 0;
	if(isObject(MRPG_LoadingDlg))
		Canvas.popDialog(MRPG_LoadingDlg);

	echo("MRPG loading screen: hidden after "
		@ (getRealTime() - $MRPG_LoadingStart) @ "ms (" @ %reason @ ")");
}


function MRPG_setLoadingText(%status)
{
	if(!isObject(MRPG_LoadingTitle))
		return;

	MRPG_LoadingTitle.setText(
		"<just:center><font:Palatino Linotype:34><color:C9A227>MonsterRPG\n"
		@ "<font:Palatino Linotype:16><color:8FA6B8>" @ %status);
}


//////////////////////////////////////////////////////////////////////////////
// THE WAIT
//
// Two jobs: keep the status line moving, and enforce the safety valve.
//////////////////////////////////////////////////////////////////////////////
function MRPG_loadingTick()
{
	cancel($MRPG_LoadingSch);

	if(!$MRPG_Loading)
		return;

	%elapsed = getRealTime() - $MRPG_LoadingStart;

	if(%elapsed > $MRPG::Loading::MaxMs)
	{
		//NEVER STRAND THE PLAYER. If the ready signal never arrives the screen lifts
		//anyway and says why, because a cover that can lock someone out of their own game
		//is worse than no cover at all.
		warn("MRPG loading screen: no ready signal from the server after "
			@ $MRPG::Loading::MaxMs @ "ms - lifting anyway. The server add-on may be older"
			SPC "than this client, or MRPG_LoadingDone was never sent.");
		MRPG_hideLoading("timeout");
		return;
	}

	%dots = "";
	%n = mFloor(%elapsed / 400) % 4;
	for(%i = 0; %i < %n; %i++)
		%dots = %dots @ ".";

	MRPG_setLoadingText($MRPG_LoadingStatus @ %dots);

	$MRPG_LoadingSch = schedule($MRPG::Loading::TickMs, 0, MRPG_loadingTick);
}


//////////////////////////////////////////////////////////////////////////////
// SERVER SIGNALS
//////////////////////////////////////////////////////////////////////////////

//Raise the screen. Sent as soon as the client connects, before it can see anything.
function clientCmdMRPG_LoadingShow(%status)
{
	if(%status $= "")
		%status = "Entering the world";
	$MRPG_LoadingStatus = %status;
	MRPG_showLoading(%status);
}

//Progress text only - does not raise or lower the screen.
function clientCmdMRPG_LoadingStatus(%status)
{
	$MRPG_LoadingStatus = %status;
	if($MRPG_Loading)
	{
		MRPG_setLoadingText(%status);
		MRPG_loadingNoteStatus(%status);   // advances the confirmed floor
	}
}

//The world is ready. THE ONLY NORMAL WAY THE SCREEN COMES DOWN.
//
//Goes through the finish animation rather than popping instantly, so the bar is always
//seen to reach 100. MRPG_loadingFinish calls MRPG_hideLoading once it gets there.
function clientCmdMRPG_LoadingDone()
{
	if(!$MRPG_Loading)
		return;
	$MRPG_LoadingFloor = 100;  $MRPG_LoadingCeil = 100;
	MRPG_setLoadingText("Ready");
	MRPG_loadingFinish("server ready");
}


//////////////////////////////////////////////////////////////////////////////
// RAISE IT THE MOMENT WE CONNECT
//
// Client-side rather than waiting to be told, because the gap this covers STARTS before
// the server's first message can arrive - the client finishes mission load and is looking
// at the world already. The server then keeps it up until it is genuinely ready.
//////////////////////////////////////////////////////////////////////////////
package MRPGLoadingScreen
{
	function GameConnection::initialControlSet(%this)
	{
		Parent::initialControlSet(%this);

		//Only on a server that actually runs MonsterRPG. On any other server this file is
		//still loaded but must do nothing at all.
		if($MRPG::ServerHasMonsterRPG)
			MRPG_showLoading("Entering the world");
	}

	//Leaving must always tear it down, or it survives into the main menu and covers it.
	//
	//$MRPG::ServerHasMonsterRPG is cleared by MRPG_ClientLeave now, not here - one
	//owner for the flag. This still hides the cover directly because it must happen
	//whether or not the gate ever opened (a client can be showing the loading screen
	//when the connection drops).
	function disconnectedCleanup()
	{
		MRPG_hideLoading("disconnected");
		Parent::disconnectedCleanup();
	}
};
activatePackage(MRPGLoadingScreen);


//Set by the server's hello so this add-on stays inert on non-MonsterRPG servers.
//
//THE SECOND OF THE TWO HELLOS - sent from GameConnection::onClientEnterGame
//(Core_LoadingScreen.cs), so it lands AFTER addMRPGClientToServer. It goes through
//the same gate rather than setting $MRPG::ServerHasMonsterRPG on its own, because
//two independent "are we on a MonsterRPG server" flags is how the client ended up
//with modules that disagreed about it. MRPG_ClientEnter sets both and is
//idempotent. See ServerGate.cs.
function clientCmdMRPG_Hello()
{
	MRPG_ClientEnter();
	MRPG_showLoading("Entering the world");
}


//////////////////////////////////////////////////////////////////////////////
// SUPPRESS THE RPG UI WHILE COVERED
//
// The dialog eats mouse and keyboard, but a BOUND KEY is dispatched by the input map, not
// by the GUI stack - so the panels would still open behind the screen and be waiting when
// it lifts. Every opener checks this.
//////////////////////////////////////////////////////////////////////////////
function MRPG_uiBlocked()
{
	return $MRPG_Loading ? 1 : 0;
}
