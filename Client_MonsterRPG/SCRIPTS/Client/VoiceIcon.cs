//////////////////////////////////////////////////////////////////////////////
// VoiceIcon.cs  -  a green microphone, shown only while you are transmitting
//
// WHAT IT IS FOR, and it is two things.
//
// The obvious one is feedback: push-to-talk without an indicator is guesswork,
// and the usual failure is talking for ten seconds having never pressed the key.
//
// ── THREE STATES, NOT TWO ────────────────────────────────────────────────────
//
//   absent  you are not holding the key
//   grey    you are holding it and nothing is reaching the microphone
//   green   audio is leaving this machine
//
// The grey state was added because two states could not tell "the key did not
// arrive" apart from "the microphone is silent" - both present as nothing
// happening, and the second is by far the more common. Windows routinely hands
// out a virtual cable (Voicemeeter, a webcam, a capture device) as the default
// communications endpoint; it opens, it captures, and it captures silence. Grey
// says the key worked and the device did not, which is the whole distinction.
//
// The settings screen carries a live input meter for the same reason, and is
// where the device gets changed. See Settings.cs.
//
// The one that matters more is that it is a PRIVACY INDICATOR. This add-on can
// open a microphone. Anything that does that should say so, visibly, in the
// moment it is happening - not in a config file the player set once and forgot.
// The icon is lit if and only if audio is actually leaving this machine, so
// "am I live?" is answered by looking rather than by trusting.
//
// ── PARENTED TO PlayGui, NOT MAIN_INTERFACE, AND THAT WAS PAID FOR ───────────
//
// MonsterRPGx_HUDBase is created by its .gui and then abandoned -
// clientCmdaddMonsterRPGGUI reparents MAIN_INTERFACE out of it - so HUDBase is
// never a parent. But MAIN_INTERFACE is wrong here too, and the reason is the one
// CampPanel.cs warns about at length:
//
//     clientCmdaddMonsterRPGGUI runs UIS_setOriginalValues() then
//     scaleNewCanvas(), so MAIN_INTERFACE and everything in it lives in a SCALED
//     1024x768 design space. A control added AFTERWARDS has no recorded original,
//     and UIS_applyScaling pairs its already-scaled position with an unscaled
//     extent - or scales a position that was computed in screen units.
//
// This icon is built on JOIN, which is always after that call, so it cannot be in
// that tree. MEASURED before the move, on a 1600x900 screen: extent 32 came out
// as 37, and the plate's canvas position was 2016,904 - 416px past the right edge
// and below the bottom. It was never invisible; setVisible(1) was being called on
// something drawn entirely off-screen, which is indistinguishable from a hidden
// control and cost three rounds of guessing.
//
// PlayGui is what Minimap.cs uses for exactly this reason: it is unscaled, it is
// in true screen pixels, and a control added to it after the HUD is drawn on top
// of it. The one thing PlayGui cannot do is take a CLICK - NewChatHud swallows
// those - and this icon takes no input, so that limit does not apply.
//
// ── WHERE IT SITS ────────────────────────────────────────────────────────────
//
// Directly above the mana orb in the bottom-right, ANCHORED TO THE ORB rather
// than to the corner. Right_Colb is the orb's container in
// GUIs/MonsterRPGx_HUDBase.gui; its canvas rect is read at runtime, so the icon
// follows the orb through every resolution and every HUD scale without this file
// knowing what either of them is. A corner offset in screen pixels would have to
// be re-tuned for each resolution and would drift on top of the orb at some of
// them.
//
// It never takes input. NewChatHud already eats every click over the world, and
// a decorative control that also swallowed clicks would be a bug nobody would
// connect back to a microphone icon.
//////////////////////////////////////////////////////////////////////////////

//////////////////////////////////////////////////////////////////////////////
// DIAGNOSTICS
//
// "I hold the key and no icon appears" has four possible breaks and they all look
// identical from the outside:
//
//   1. the key is not bound, so MRPG_VoicePTT never runs
//   2. it runs, but MRPGAudio_VoiceKey is missing, so the DLL never sees the key
//   3. the DLL sees it, but the icon's poll is not running
//   4. the poll is running, but the control was never built
//
// Guessing between them has already cost two rounds. These lines go to
// MonsterRPGAudio.log, which is the one file that survives a client crash and can
// be read afterwards - see [[client-console-log-overwrite]] for why console.log
// cannot be relied on for this.
//
// BOUNDED, because a per-frame diagnostic is a log nobody can read. Eight lines is
// enough to answer the question and small enough to leave switched on.
//////////////////////////////////////////////////////////////////////////////
$MRPGVoiceIcon::DiagLeft = 8;

function MRPGVoiceIcon_Say(%msg)
{
	if($MRPGVoiceIcon::DiagLeft <= 0)
		return;

	$MRPGVoiceIcon::DiagLeft--;

	if(isFunction("MRPGAudio_Log"))
		MRPGAudio_Log("icon: " @ %msg);

	echo("MRPG voice icon: " @ %msg);
}

$MRPGVoiceIcon::BmpLive = "Add-Ons/Client_MonsterRPG/GUIs/MicrophoneIconGreen";
$MRPGVoiceIcon::BmpIdle = "Add-Ons/Client_MonsterRPG/GUIs/MicrophoneIconDim";

//Big enough to read at a glance from the middle of a fight. The first pass was 26
//and would have been marginal even had it been on screen.
$MRPGVoiceIcon::Size    = 44;   // the glyph
$MRPGVoiceIcon::Pad     = 4;    // border of plate around it
$MRPGVoiceIcon::GapY    = 8;    // clearance between the plate and the orb below

//Where the orb sits inside its container, as fractions of the container - taken
//from GUIs/MonsterRPGx_HUDBase.gui, where Right_Colb is 232x116 and the orb
//bitmaps are at x=17 with a width of 97. Fractions rather than pixels because the
//container is scaled and these are not.
$MRPGVoiceIcon::OrbCtrX = 0.2823;   // (17 + 97/2) / 232

//Fallback only, for a HUD that has not been built yet: inset from the screen's
//bottom-right corner. The icon is re-placed against the orb the moment it exists.
$MRPGVoiceIcon::FallbackX = 40;
$MRPGVoiceIcon::FallbackY = 150;

// Polled rather than pushed. The transmit state lives in the DLL - it depends on
// the gate as well as the key, so the keypress alone is not the truth - and
// there is no callback out of it. 10 Hz is under a millisecond of script per
// second and is well inside the time it takes to notice a light.
$MRPGVoiceIcon::PollMs  = 100;

//Where is the control ACTUALLY drawn, and is anything above it hiding it?
//
//"visible=1 and not on screen" has two causes that a visibility flag cannot tell
//apart: the control is off-panel, or an ANCESTOR is hidden. Both present as an
//icon that exists, reports itself visible, and cannot be seen - so the chain is
//walked and every link reported. Bounded by the same budget as everything else.
function MRPGVoiceIcon_Where()
{
	if(!isObject(MRPG_VoiceIconBox))
		return "no control";

	%out = "plate at " @ MRPG_VoiceIconBox.getCanvasPosition()
		@ " ext " @ MRPG_VoiceIconBox.getExtent()
		@ " glyph=" @ (isObject(MRPG_VoiceIcon) ? 1 : 0)
		@ " anchored=" @ ($MRPGVoiceIcon::Anchored ? 1 : 0)
		@ " orb=" @ (isObject(Right_Colb) ? Right_Colb.getCanvasPosition() @ "/" @ Right_Colb.getExtent() : "none")
		@ " res " @ getRes() @ " chain";

	%o = MRPG_VoiceIconBox;
	for(%i = 0; %i < 8; %i++)
	{
		%nm = %o.getName();
		if(%nm $= "")
			%nm = "(" @ %o.getClassName() @ ")";

		%out = %out @ " " @ %nm @ ":v" @ (%o.isVisible() ? 1 : 0);

		%g = %o.getGroup();
		if(!isObject(%g))
			break;
		%o = %g;
	}
	return %out;
}

function MRPGVoiceIcon_Build()
{
	if(isObject(MRPG_VoiceIconBox))
		return;

	//PlayGui, not MonsterRPGx_MAIN_INTERFACE - see the header. It exists from the
	//moment the player is in a game, so unlike the old parent there is nothing to
	//wait for; the retry that used to be here was waiting on the wrong object.
	if(!isObject(PlayGui))
		return;

	%s = $MRPGVoiceIcon::Size;
	%b = %s + $MRPGVoiceIcon::Pad * 2;

	//── A PLATE BEHIND THE GLYPH, AND IT EARNS ITS PLACE TWICE ─────────────────
	//
	//Visually: this sits over the world, which can be a bright sky or a snowfield,
	//and a bare glyph on either is unreadable exactly when a player most wants to
	//check whether they are live.
	//
	//Diagnostically: a GuiSwatchCtrl needs no texture. If the plate appears and the
	//microphone does not, the PNG is the problem; if neither appears, the control is
	//mispositioned or an ancestor is hidden. Those two were indistinguishable while
	//the icon was a lone bitmap.
	//
	//VISIBILITY IS TOGGLED ON THE PLATE, not on the glyph: an invisible parent is
	//not rendered and neither are its children, so one write hides both.
	new GuiSwatchCtrl(MRPG_VoiceIconBox)
	{
		profile     = "GuiDefaultProfile";

		//NO AUTO-ANCHORING. This control is positioned by arithmetic against the
		//mana orb and re-placed whenever the screen changes; a sizing rule would be
		//a second, competing opinion about where it belongs.
		horizSizing = "right";
		vertSizing  = "bottom";

		position    = "0 0";
		extent      = %b SPC %b;
		minExtent   = "8 8";
		color       = "12 14 18 170";

		//Decoration only. It could not take a click in PlayGui even if it wanted
		//one - NewChatHud is above it and swallows every click over the world.
		enabled     = "0";
		visible     = "0";
	};

	//ADDED TO PlayGui AFTER THE HUD, so it draws on top of it rather than under.
	PlayGui.add(MRPG_VoiceIconBox);

	new GuiBitmapCtrl(MRPG_VoiceIcon)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = $MRPGVoiceIcon::Pad SPC $MRPGVoiceIcon::Pad;
		extent      = %s SPC %s;
		minExtent   = "8 8";
		bitmap      = $MRPGVoiceIcon::BmpIdle;
		enabled     = "0";
		visible     = "1";
	};

	MRPG_VoiceIconBox.add(MRPG_VoiceIcon);

	//Placed once here so it is never at 0,0 for a frame, but the TICK owns
	//placement from now on - see the note there about the HUD scaling landing
	//after the join.
	$MRPGVoiceIcon::Anchored = MRPGVoiceIcon_Place();
	$MRPGVoiceIcon::OrbRect  = "";
}

//Put the plate just above the mana orb.
//
//DERIVED FROM THE ORB'S LIVE CANVAS RECT, not from a corner offset. Right_Colb is
//scaled with the rest of the HUD, so its screen rect is the only thing that knows
//where the orb actually is at this resolution - and reading it means this file
//never has to be re-tuned when either changes.
//
//resize(), NOT setPosition(). GuiControl::setPosition DOES NOT EXIST in Blockland:
//the only setPosition in the binary belongs to PathCamera, so the call fails
//silently and the control simply never moves.
function MRPGVoiceIcon_Place()
{
	if(!isObject(MRPG_VoiceIconBox))
		return 0;

	%e = MRPG_VoiceIconBox.getExtent();
	%w = getWord(%e, 0);
	%h = getWord(%e, 1);

	%placed = 0;

	if(isObject(Right_Colb))
	{
		%p = Right_Colb.getCanvasPosition();
		%c = Right_Colb.getExtent();

		%px = getWord(%p, 0);
		%py = getWord(%p, 1);
		%cw = getWord(%c, 0);

		//Centred on the orb, sitting on top of it.
		%x = %px + mFloor(%cw * $MRPGVoiceIcon::OrbCtrX) - mFloor(%w / 2);
		%y = %py - %h - $MRPGVoiceIcon::GapY;

		%placed = 1;
	}

	//No HUD yet - park it in the corner rather than at 0,0, and let the next tick
	//try again. A control at the origin looks like a bug in a way that a control in
	//roughly the right place does not.
	if(!%placed)
	{
		%res = getRes();
		%x = getWord(%res, 0) - %w - $MRPGVoiceIcon::FallbackX;
		%y = getWord(%res, 1) - %h - $MRPGVoiceIcon::FallbackY;
	}

	//Never off-screen, whatever the arithmetic above produced. This is the exact
	//failure that hid the icon for three rounds - it was at 2016,904 on a 1600x900
	//screen - and a clamp is cheap insurance against it recurring in a form nobody
	//thought of.
	//
	//BUT A CLAMPED POSITION IS A SYMPTOM, NOT A FIX. It turned the next bug into
	//"the icon is stuck in the top-right corner" rather than "the icon is missing",
	//which is more visible but no more correct. If the plate ends up pinned to an
	//edge, something upstream computed nonsense - look at the orb rect in the
	//diagnostic line before adjusting anything here.
	%res = getRes();
	%maxX = getWord(%res, 0) - %w;
	%maxY = getWord(%res, 1) - %h;
	if(%x > %maxX) %x = %maxX;
	if(%y > %maxY) %y = %maxY;
	if(%x < 0) %x = 0;
	if(%y < 0) %y = 0;

	MRPG_VoiceIconBox.resize(%x, %y, %w, %h);

	$MRPGVoiceIcon::PlacedRes = getRes();
	return %placed;
}

//Shown if and only if audio is actually being transmitted.
//
//This reads the DLL's own transmit flag rather than the key state, and the
//difference is the point: holding the key in a silent room sends nothing, and an
//indicator that lit up anyway would be telling the player something untrue about
//their own microphone.
function MRPGVoiceIcon_Tick()
{
	cancel($MRPGVoiceIcon::Sch);

	//MRPG_isActive rather than the flag alone - the same two-condition test every
	//other module uses, because the flag can still be set while ServerConnection
	//has already gone.
	if(!MRPG_isActive())
	{
		//THIS RETURN IS PERMANENT: nothing restarts the poll. It was reachable from
		//MRPGVoiceIcon_Start when the audio invite arrived before the join gate had
		//been set, and the failure was completely silent - no icon, ever, with every
		//other part of the system working. MRPG_ClientEnter starts it now, so the
		//order cannot invert; the line is kept so a future inversion says so.
		MRPGVoiceIcon_Say("poll stopped - not on a MonsterRPG server");
		return;
	}

	//RETRIED EVERY TICK, not once. PlayGui exists early, but the join can still
	//land before it on a slow load.
	if(!isObject(MRPG_VoiceIconBox))
		MRPGVoiceIcon_Build();

	//RE-PLACED WHENEVER THE ORB ACTUALLY MOVES, which is not the same as "once".
	//
	//The first version anchored on the first tick that found Right_Colb and then
	//never looked again. That is wrong by one ordering: the icon is built on JOIN,
	//and clientCmdaddMonsterRPGGUI runs UIS_setOriginalValues() and
	//scaleNewCanvas() AFTERWARDS - so the orb reports its pre-scaling geometry at
	//the moment we ask, and the icon is placed against numbers that stop being
	//true a frame later. MEASURED: it landed at 1548,0 (the top-right corner,
	//where the clamp had put it) while the orb's settled rect gives 1154,707.
	//
	//So the orb's own canvas rect is the trigger. Comparing it as a STRING is
	//deliberate - it catches a move, a resize and a HUD rebuild with one test, and
	//there is no epsilon to get wrong. Two method calls and a string compare at
	//10 Hz is nothing; being wrong about this cost a round trip.
	if(isObject(MRPG_VoiceIconBox))
	{
		%rect = isObject(Right_Colb)
			? Right_Colb.getCanvasPosition() SPC Right_Colb.getExtent()
			: "none";

		if(%rect !$= $MRPGVoiceIcon::OrbRect || $MRPGVoiceIcon::PlacedRes !$= getRes())
		{
			$MRPGVoiceIcon::OrbRect  = %rect;
			$MRPGVoiceIcon::Anchored = MRPGVoiceIcon_Place();
		}
	}

	%live = 0;
	%held = 0;
	if(isFunction("MRPGAudio_VoiceStat"))
	{
		// 0 enabled  1 capturing  2 talking  3 made  4 taken  5 dropped
		// 6 rate     7 channels   8 ptt      9 openMic  10 level  11 gate
		//
		//READ BY INDEX, so this list has to match MrpgCapture::StatLine exactly.
		//Fields are only ever appended there for that reason.
		%v   = MRPGAudio_VoiceStat();
		%on  = (getWord(%v, 0) && getWord(%v, 1));
		%live = (%on && getWord(%v, 2));
		%held = (%on && (getWord(%v, 8) || getWord(%v, 9)));
	}

	//Shown while the key is held OR while transmitting - open mic has no key, so
	//keying the visibility off the key alone would hide the indicator for exactly
	//the mode in which an indicator matters most.
	%show = (%live || %held);

	//Once a second while the key is held or audio is going out, and only until the
	//budget above runs out. This is the line that says which of the four breaks it
	//is: if "held=1" never appears, the key is not reaching the DLL; if it appears
	//with "ctrl=0", the control was never built.
	if(%show && (getSimTime() - $MRPGVoiceIcon::LastSay) > 1000)
	{
		$MRPGVoiceIcon::LastSay = getSimTime();
		MRPGVoiceIcon_Say("live=" @ %live @ " held=" @ %held
			@ " stat=" @ %v
			@ " | " @ MRPGVoiceIcon_Where());
	}

	if(isObject(MRPG_VoiceIconBox))
	{
		if(MRPG_VoiceIconBox.visible != %show)
			MRPG_VoiceIconBox.setVisible(%show);

		//SET ONLY ON A CHANGE. setBitmap re-resolves a texture, and doing that ten
		//times a second for the whole time a key is held is a cost with no visible
		//effect whatsoever.
		if(%show && $MRPGVoiceIcon::WasLive != %live && isObject(MRPG_VoiceIcon))
		{
			$MRPGVoiceIcon::WasLive = %live;
			MRPG_VoiceIcon.setBitmap(%live ? $MRPGVoiceIcon::BmpLive : $MRPGVoiceIcon::BmpIdle);

			//The plate carries the state too, so it reads at a glance even if the
			//glyph itself never renders.
			MRPG_VoiceIconBox.color = %live ? "16 46 22 190" : "12 14 18 170";
		}
	}

	$MRPGVoiceIcon::Sch = schedule($MRPGVoiceIcon::PollMs, 0, MRPGVoiceIcon_Tick);
}

//Called from MRPG_ClientEnter, and again from the audio link coming up. Both are
//kept and neither is redundant: the gate is what guarantees the poll starts at all,
//and the second call costs nothing because MRPGVoiceIcon_Tick cancels the pending
//schedule before doing anything else.
function MRPGVoiceIcon_Start()
{
	MRPGVoiceIcon_Build();

	MRPGVoiceIcon_Say("start: playGui=" @ (isObject(PlayGui) ? 1 : 0) @ " orb=" @ (isObject(Right_Colb) ? 1 : 0)
		@ " ctrl=" @ (isObject(MRPG_VoiceIconBox) ? 1 : 0)
		@ " active=" @ MRPG_isActive()
		@ " stat=" @ (isFunction("MRPGAudio_VoiceStat") ? MRPGAudio_VoiceStat() : "no DLL")
		@ " bind=[" @ (isObject(moveMap) ? moveMap.getBinding("MRPG_VoicePTT") : "no moveMap") @ "]");

	MRPGVoiceIcon_Tick();
}

//Called from MRPG_ClientLeave. The control is deleted rather than hidden: this
//add-on loads on every server, and a stray microphone icon belonging to a server
//the player has left is exactly the kind of thing that outlives its welcome.
function MRPGVoiceIcon_Shutdown()
{
	cancel($MRPGVoiceIcon::Sch);
	$MRPGVoiceIcon::Sch = "";
	$MRPGVoiceIcon::WasLive   = "";
	$MRPGVoiceIcon::LastSay   = "";
	$MRPGVoiceIcon::Anchored  = 0;
	$MRPGVoiceIcon::PlacedRes = "";
	$MRPGVoiceIcon::OrbRect   = "";

	//The budget is refilled on the way OUT, not on the way in, so each join gets a
	//whole one and a mid-session re-Start cannot quietly grant a second.
	$MRPGVoiceIcon::DiagLeft = 8;

	//The plate owns the glyph, so deleting it takes both.
	if(isObject(MRPG_VoiceIconBox))
		MRPG_VoiceIconBox.delete();
}
