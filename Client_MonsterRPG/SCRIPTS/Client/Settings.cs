//////////////////////////////////////////////////////////////////////////////
// Settings.cs  -  the player's own settings, and the gear that opens them
//////////////////////////////////////////////////////////////////////////////
//
// One screen for everything a player is allowed to set: which speakers and which
// microphone MonsterRPG's audio engine uses, four volumes, and whether the
// microphone is live at all. A gear sits in the top-left corner with the key
// printed under it, so it is discoverable without anyone having to be told.
//
// -----------------------------------------------------------------------------
// WHY THIS EXISTS AT ALL, RATHER THAN A CONFIG FILE
// -----------------------------------------------------------------------------
//
// The audio now plays out of MonsterRPGAudio.dll, not out of Blockland - so
// Options > Audio does not touch it, and Windows' "default device" is exactly the
// wrong answer for the players who most need a right one: a headset that enumerates
// third, a webcam microphone that stole the default, a virtual cable left behind by
// streaming software. Before this screen the only cure was editing
// MonsterRPGAudio.cfg by hand, which is not a cure.
//
// THE MICROPHONE TOGGLE IS THE IMPORTANT ROW. `Voice=0` in the cfg is the shipped
// default, and the whole reason holding V appeared to do nothing. A player must be
// able to turn their own microphone on - and off - from inside the game, in one
// click, without trusting a file. VoiceIcon.cs is the other half of that promise:
// it lights only while audio is actually leaving the machine.
//
// -----------------------------------------------------------------------------
// PARENTING - THE GEAR LIVES IN NewChatHud, THE PANEL IS A DIALOG
// -----------------------------------------------------------------------------
//
// The gear is CLICKABLE, so it must be inside NewChatHud and nowhere else.
// GuiCanvas::maintainSizing stretches every canvas child to the full screen each
// frame, and rootMouseDown walks those children last-to-first and stops at the
// first modal hit - NewChatHud is a pushed dialog on a modal profile, so it wins
// every click over the world and PlayGui never sees one. A gear parented to
// PlayGui, or to MAIN_INTERFACE, would draw perfectly and be completely dead.
// (VoiceIcon.cs may live in MAIN_INTERFACE precisely because it takes no input.)
//
// Being in NewChatHud also gives real screen pixels, which is what lets the gear
// sit in the true top-left corner - MAIN_INTERFACE is anchored bottom-centre in a
// scaled 1024x768 space and cannot express a screen corner at all.
//
// The panel itself is pushed with Canvas.pushDialog, which appends it as the last
// canvas child: above the HUD, above the chat, above everything. That is the
// "overlay over all" the design asked for, and it comes for free rather than from
// any z-order of our own.
//
// -----------------------------------------------------------------------------
// WIDGETS ARE DRAWN, NOT BORROWED
// -----------------------------------------------------------------------------
//
// The sliders and buttons are swatches with a single GuiMouseEventCtrl catcher
// underneath, driven by a 40ms tick - the idiom CharacterScreen.cs and ChatPanel.cs
// already use here. Two reasons, and the second is the one that decided it:
//
//   * GuiSliderCtrl and GuiPopUpMenuCtrl render out of profile bitmap arrays that
//     GuiDefaultProfile does not carry, so they come out as bare engine primitives
//     in the middle of a hand-drawn medieval panel.
//   * Hit-testing stops at the first MODAL child, and GuiDefaultProfile is modal.
//     Every swatch on this panel eats clicks, so the catcher HAS TO BE ADDED LAST
//     or nothing on the screen can ever be pressed. That trap is identical whether
//     the widgets are stock or ours; drawing them ourselves at least makes it one
//     trap instead of two.
//
// -----------------------------------------------------------------------------
// PERSISTENCE
// -----------------------------------------------------------------------------
//
// Everything lands in $Pref::Client::MRPG::*, which the engine exports to
// config/client/prefs.cs - OUR file. The same reasoning as Keybinds.cs: config.cs
// belongs to the engine and to every other add-on, and this add-on must leave no
// trace in it. Devices are remembered by ENDPOINT ID, never by list position;
// indices renumber the moment a USB headset is plugged in, so a saved index points
// at a different device tomorrow. A saved device that has gone is not an error -
// it means the headset was unplugged - and it falls back to the system default.
//////////////////////////////////////////////////////////////////////////////


//The four volume categories, in the DLL's own order. Do not reorder: these are the
//integers MRPGAudio_SetVolume takes, and the cfg file uses the same numbering.
//    0 master   1 other sounds (sfx)   2 music   3 voice
$MRPGSet::VolName[0] = "Master";
$MRPGSet::VolName[1] = "Other sounds";
$MRPGSet::VolName[2] = "Music";
$MRPGSet::VolName[3] = "Voice";

//Shown top to bottom in this order, which is not the DLL's - master first because
//it is the one people reach for, and the three it scales underneath it.
$MRPGSet::VolOrder = "0 2 3 1";

$MRPGSet::VolDefault[0] = 1.0;
$MRPGSet::VolDefault[1] = 1.0;
$MRPGSet::VolDefault[2] = 0.6;   //music sits under everything else by default
$MRPGSet::VolDefault[3] = 1.0;

$MRPGSet::PanelW = 720;
$MRPGSet::PanelH = 540;

//The gear, in true screen pixels from the top-left corner.
$MRPGSet::GearSize   = 34;
$MRPGSet::GearMargin = 14;

//THE KNOB LOOP. 32ms, and it is the drag that sets the number rather than any
//idea of what a HUD "needs": a knob that only moves 25 times a second visibly
//lags the cursor it is supposed to be stuck to, which reads as the slider being
//heavy rather than as a frame rate. The server ticks at 125 Hz; this is a client
//poll that samples the cursor and touches at most four controls, so it is
//nowhere near either budget.
$MRPGSet::TickMs = 32;

//Palette, taken from the attribute screen so the two read as one product.
$MRPGSet::BG     = "0 0 0 190";
$MRPGSet::Panel  = "16 13 9 255";
$MRPGSet::Header = "22 18 12 255";
$MRPGSet::Gold   = "150 123 63 255";
$MRPGSet::Row    = "22 18 12 255";
$MRPGSet::Well   = "12 9 6 255";
$MRPGSet::Plate  = "38 31 21 255";
$MRPGSet::PlateH = "58 47 30 255";


//////////////////////////////////////////////////////////////////////////////
// IS THE AUDIO ENGINE ACTUALLY THERE?
//
// MonsterRPGAudio.dll is injected at launch by its own launcher, so a player who
// started Blockland the ordinary way has none of these functions. That is a
// supported state, not a failure: the screen still opens, the rows say why they
// are empty, and nothing throws. Testing one function rather than a global also
// means a DLL that failed halfway is caught by the same guard.
//////////////////////////////////////////////////////////////////////////////
function MRPGSettings_HasAudio()
{
	return isFunction("MRPGAudio_SetVolume") ? 1 : 0;
}


//////////////////////////////////////////////////////////////////////////////
// PREFS -> DLL
//
// Called on join and after every change. It is written to be safe to call when the
// DLL is absent, when it is present but the link is down, and twice in a row.
//////////////////////////////////////////////////////////////////////////////
function MRPGSettings_Vol(%cat)
{
	%v = $Pref::Client::MRPG::Vol[%cat];
	if(%v $= "")
		%v = $MRPGSet::VolDefault[%cat];

	//Clamped HERE as well as in the DLL. A prefs.cs edited by hand is the one input
	//to this whole file that nothing else validates.
	if(%v < 0) %v = 0;
	if(%v > 1) %v = 1;
	return %v;
}

function MRPGSettings_VoiceOn()
{
	//Default OFF. An add-on that opens a microphone the first time it loads, without
	//being asked, is not a thing this project ships - the player turns it on here.
	%v = $Pref::Client::MRPG::VoiceOn;
	return (%v $= "") ? 0 : (%v ? 1 : 0);
}

function MRPGSettings_Apply()
{
	if(!MRPGSettings_HasAudio())
		return 0;

	for(%c = 0; %c < 4; %c++)
		MRPGAudio_SetVolume(%c, MRPGSettings_Vol(%c));

	//DEVICES BEFORE VOICE. Turning capture on and then moving it to another endpoint
	//would open the wrong microphone for as long as it took the second call to land -
	//brief, but it is a microphone, and "brief" is not the standard for those.
	%out = $Pref::Client::MRPG::AudioOutId;
	if(%out !$= "")
		MRPGAudio_SetDevice(0, %out);

	%in = $Pref::Client::MRPG::AudioInId;
	if(%in !$= "")
		MRPGAudio_SetDevice(1, %in);

	MRPGAudio_VoiceEnable(MRPGSettings_VoiceOn());
	return 1;
}


//////////////////////////////////////////////////////////////////////////////
// THE GEAR
//////////////////////////////////////////////////////////////////////////////

//"ctrl o" -> "Ctrl+O". Read from the LIVE binding rather than from the default, so
//a player who moved the key sees the key they moved it to.
function MRPGSettings_KeyLabel()
{
	if(!isObject(moveMap) || !isFunction("MRPG_keyOfBinding"))
		return "";

	%k = MRPG_keyOfBinding(moveMap.getBinding("MRPG_ToggleSettings"));
	if(%k $= "")
		return "";

	%out = "";
	for(%i = 0; %i < getWordCount(%k); %i++)
	{
		%w = getWord(%k, %i);
		%out = %out @ (%i > 0 ? "+" : "")
			@ strupr(getSubStr(%w, 0, 1)) @ getSubStr(%w, 1, strlen(%w) - 1);
	}
	return %out;
}

function MRPGSettings_BuildGear()
{
	if(isObject(MRPG_SettingsGear))
		return 1;

	//See the header: clickable means NewChatHud, and nothing else will do. It is
	//pushed by PlayGui::onWake and by LoadingGui::onWake, so it is normally up well
	//before the join - MRPGSettings_Start retries for the case where it is not.
	if(!isObject(NewChatHud))
		return 0;

	%s = $MRPGSet::GearSize;
	%m = $MRPGSet::GearMargin;

	%gear = new GuiBitmapButtonCtrl(MRPG_SettingsGear)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = %m SPC %m;
		extent      = %s SPC %s;
		minExtent   = "8 8";
		//SettingsGear.png is GUIs/SettingsIcon.png cropped to its content and box-
		//filtered down to 64px. The original is 1007x1007 with the two gears on a
		//diagonal inside a wide empty margin - drawn straight into a 34px button the
		//gears would have occupied about half of it and read as a smudge.
		bitmap      = "Add-Ons/Client_MonsterRPG/GUIs/SettingsGear";
		command     = "MRPGSettings_Open();";
	};
	NewChatHud.add(%gear);

	//TWO LINES UNDER THE GEAR: what it is, then how to open it without the mouse.
	//
	//Under rather than beside, because the corner is the one place on this HUD with
	//nothing to collide with - a label to the right runs into the chat panel's tab
	//strip at narrow resolutions.
	//
	//Both are muted on purpose. This is a signpost, not information: it is read once
	//when a player first joins and then never again, and anything bright enough to
	//be noticed on the hundredth join is too bright for the first ninety-nine.
	%cap = new GuiMLTextCtrl(MRPG_SettingsCap)
	{
		profile     = "GuiMLTextProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = (%m - 4) SPC (%m + %s + 2);
		extent      = (%s + 70) SPC 16;
		minExtent   = "8 2";
	};
	NewChatHud.add(%cap);
	MRPG_SettingsCap.setText("<font:verdana bold:11><color:9A948A>Settings");

	%hint = new GuiMLTextCtrl(MRPG_SettingsHint)
	{
		profile     = "GuiMLTextProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = (%m - 4) SPC (%m + %s + 16);
		extent      = (%s + 70) SPC 16;
		minExtent   = "8 2";
	};
	NewChatHud.add(%hint);

	MRPGSettings_RefreshHint();
	return 1;
}

//Separate from the build because the key can move mid-session: the stock remap
//dialog rebinds it, and Keybinds.cs hands everything back and takes it again around
//optionsDlg::onSleep. Re-read rather than remembered.
function MRPGSettings_RefreshHint()
{
	if(!isObject(MRPG_SettingsHint))
		return;

	%k = MRPGSettings_KeyLabel();
	if(%k $= "")
	{
		//No key bound - the gear still works, so say nothing rather than print an
		//instruction the player cannot follow. The "Settings" caption stays: it
		//names the icon, which is true whether or not a key reaches it.
		MRPG_SettingsHint.setText("");
		return;
	}

	MRPG_SettingsHint.setText("<font:verdana bold:11><color:7E786E>Press " @ %k);
}

function MRPGSettings_Start()
{
	cancel($MRPGSet::GearSch);
	$MRPGSet::GearSch = "";

	if(!MRPG_isActive())
		return;

	//Retry rather than give up: NewChatHud may not be pushed yet on a very early
	//join. Half a second is invisible and the loop stops itself the moment it wins.
	if(!MRPGSettings_BuildGear())
	{
		$MRPGSet::GearSch = schedule(500, 0, MRPGSettings_Start);
		return;
	}

	MRPGSettings_Apply();
}

//Called from MRPG_ClientLeave. Deleted, not hidden - the same rule VoiceIcon.cs
//follows. This add-on loads on every server, and a gear belonging to a server the
//player has left is exactly the kind of thing that outlives its welcome.
function MRPGSettings_Shutdown()
{
	cancel($MRPGSet::GearSch);   $MRPGSet::GearSch = "";
	cancel($MRPGSet::TickSch);   $MRPGSet::TickSch = "";

	MRPGSettings_Close();

	if(isObject(MRPG_SettingsGear)) MRPG_SettingsGear.delete();
	if(isObject(MRPG_SettingsCap))  MRPG_SettingsCap.delete();
	if(isObject(MRPG_SettingsHint)) MRPG_SettingsHint.delete();
	if(isObject(MRPG_SettingsDlg))  MRPG_SettingsDlg.delete();

	$MRPGSet::Built = 0;
}


//////////////////////////////////////////////////////////////////////////////
// SMALL BUILDERS
//////////////////////////////////////////////////////////////////////////////
function MRPGS_swatch(%parent, %x, %y, %w, %h, %col)
{
	%s = new GuiSwatchCtrl()
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "1 1"; color = %col;
	};
	%parent.add(%s);
	return %s;
}

//%text IS A PARAMETER RATHER THAN A CHAINED CALL, and that is a language limit
//rather than a style: TorqueScript's object expression must be a variable or an
//identifier, so MRPGS_label(..., ...) is a syntax error, not a shortcut.
function MRPGS_label(%parent, %name, %x, %y, %w, %h, %text)
{
	%t = new GuiMLTextCtrl(%name)
	{
		profile = "GuiMLTextProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "8 2"; lineSpacing = "2";
	};
	%parent.add(%t);

	if(%text !$= "")
		%t.setText(%text);

	return %t;
}

//A button is a plate, a caption, and a row in the registry the catcher consults.
//
//%mode is which screen it belongs to (0 the settings panel, 1 the device chooser).
//Hit-testing filters on it rather than on isVisible(), because isVisible() reports
//a control's OWN flag - a button inside a hidden parent still answers true, and the
//chooser's buttons would keep firing underneath the panel that replaced them.
function MRPGS_btn(%parent, %mode, %x, %y, %w, %h, %text, %cmd)
{
	%i = $MRPGS_BtnN;

	%plate = MRPGS_swatch(%parent, %x, %y, %w, %h, $MRPGSet::Plate);
	MRPGS_swatch(%plate, 0, 0, %w, 1, $MRPGSet::Gold);

	%lbl = MRPGS_label(%plate, "", 0, mFloor((%h - 16) / 2), %w, 18);
	%lbl.setText("<just:center><font:verdana bold:12><color:F1ECC2>" @ %text);

	$MRPGS_BtnPlate[%i] = %plate;
	$MRPGS_BtnLbl[%i]   = %lbl;
	$MRPGS_BtnCmd[%i]   = %cmd;
	$MRPGS_BtnMode[%i]  = %mode;
	$MRPGS_BtnText[%i]  = %text;
	$MRPGS_BtnN++;
	return %plate;
}

//A slider is a groove, a fill, a knob and a readout. %cat is the volume category,
//which is also the key everything else looks it up by.
function MRPGS_slider(%parent, %cat, %x, %y, %w)
{
	%i = $MRPGS_SldN;

	MRPGS_label(%parent, "", %x, %y, 150, 18, "<font:verdana bold:12><color:C6BEA8>" @ $MRPGSet::VolName[%cat]);

	%gx = %x + 160;
	%gw = %w - 160 - 70;

	$MRPGS_SldGroove[%i] = MRPGS_swatch(%parent, %gx, %y + 7, %gw, 6, $MRPGSet::Well);
	$MRPGS_SldFill[%i]   = MRPGS_swatch(%parent, %gx, %y + 7, 1, 6, "170 138 72 255");
	$MRPGS_SldKnob[%i]   = MRPGS_swatch(%parent, %gx - 5, %y, 10, 20, "222 196 120 255");
	$MRPGS_SldVal[%i]    = MRPGS_label(%parent, "", %x + %w - 64, %y + 1, 64, 18);
	$MRPGS_SldCat[%i]    = %cat;
	$MRPGS_SldN++;
	return %i;
}


//////////////////////////////////////////////////////////////////////////////
// BUILD
//////////////////////////////////////////////////////////////////////////////
function MRPGSettings_Build()
{
	//BUILT FRESH EVERY TIME IT OPENS, and that is deliberate rather than lazy.
	//
	//The panel is sized and centred from getRes() at build time, so a dialog kept
	//from an earlier open would be off-centre - or off-screen - after a resolution
	//change, and a settings screen is exactly where somebody goes right after
	//changing their resolution. Rebuilding also picks up a re-exec'd copy of this
	//file, which is routine during development and would otherwise leave a stale
	//screen on the canvas while the source looked correct.
	//
	//Sixty-odd controls is nothing next to a screen the player opened by hand. The
	//one case that must NOT rebuild is a panel that is currently up: its controls
	//are what the catcher registry points at, and deleting them mid-session would
	//leave every registered id dangling.
	if(isObject(MRPG_SettingsDlg))
	{
		if($MRPGSet::Open)
			return MRPG_SettingsDlg;
		MRPG_SettingsDlg.delete();
	}

	$MRPGS_BtnN = 0;
	$MRPGS_SldN = 0;
	$MRPGS_Mode = 0;
	$MRPGS_Press = -1;
	$MRPGS_Hover = -1;
	$MRPGS_SldDrag = -1;

	%res = getRes();
	%sw = getWord(%res, 0);  %sh = getWord(%res, 1);

	%dlg = new GuiControl(MRPG_SettingsDlg)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = %sw SPC %sh;
	};

	//A DIM, NOT A COVER. Unlike the attribute screen this is not somewhere a player
	//spends time - they came to move one slider and leave - and being able to see
	//what the world is doing while you drag a volume is the entire point of a volume
	//slider. It is dark enough that the panel is unambiguously in front.
	%bg = MRPGS_swatch(%dlg, 0, 0, %sw, %sh, $MRPGSet::BG);
	%bg.horizSizing = "width";  %bg.vertSizing = "height";

	%pw = $MRPGSet::PanelW;  %ph = $MRPGSet::PanelH;
	%px = mFloor((%sw - %pw) / 2);
	%py = mFloor((%sh - %ph) / 2);

	%panel = new GuiSwatchCtrl(MRPG_SettingsPanel)
	{
		profile = "GuiDefaultProfile"; horizSizing = "center"; vertSizing = "center";
		position = %px SPC %py; extent = %pw SPC %ph; minExtent = "8 2";
		color = $MRPGSet::Panel;
	};
	%dlg.add(%panel);

	MRPGS_swatch(%panel, 0, 0, %pw, 2, $MRPGSet::Gold);
	MRPGS_swatch(%panel, 0, %ph - 2, %pw, 2, $MRPGSet::Gold);
	MRPGS_swatch(%panel, 0, 2, %pw, 60, $MRPGSet::Header);

	MRPGS_label(%panel, "", 0, 18, %pw, 26, "<just:center><font:verdana bold:19><color:F1ECC2>SETTINGS");

	// ---- devices ------------------------------------------------------------
	%rowX = 26;
	%rowW = %pw - 52;

	MRPGS_deviceRow(%panel, 0, %rowX, 84,  %rowW, "Speakers",   "MRPG_SetOutName");
	MRPGS_deviceRow(%panel, 1, %rowX, 130, %rowW, "Microphone", "MRPG_SetInName");
	MRPGS_buildMeter(%panel, %rowX, 158, %rowW);

	// ---- the microphone switch ----------------------------------------------
	//
	//Its own row with its own rule above it, because it is the only control on this
	//screen that changes what LEAVES the machine rather than what arrives at it.
	MRPGS_swatch(%panel, %rowX, 180, %rowW, 1, "60 50 32 255");

	MRPGS_label(%panel, "MRPG_SetVoiceLbl", %rowX, 194, %rowW - 200, 36, "");
	MRPGS_btn(%panel, 0, %rowX + %rowW - 190, 192, 190, 30, "Voice: off",
		"MRPGSettings_ToggleVoice();");
	$MRPGS_VoiceBtn = $MRPGS_BtnN - 1;

	// ---- volumes ------------------------------------------------------------
	MRPGS_swatch(%panel, %rowX, 240, %rowW, 1, "60 50 32 255");

	MRPGS_label(%panel, "", %rowX, 252, %rowW, 18, "<font:verdana bold:12><color:8A8175>VOLUME");

	%vy = 280;
	for(%i = 0; %i < 4; %i++)
	{
		MRPGS_slider(%panel, getWord($MRPGSet::VolOrder, %i), %rowX, %vy, %rowW);
		%vy = %vy + 34;
	}

	// ---- footer -------------------------------------------------------------
	MRPGS_label(%panel, "MRPG_SetStatus", %rowX, 424, %rowW, 40, "");

	MRPGS_btn(%panel, 0, %rowX, %ph - 62, 180, 34, "Rescan devices",
		"MRPGSettings_Rescan();");
	MRPGS_btn(%panel, 0, %rowX + %rowW - 180, %ph - 62, 180, 34, "Close",
		"MRPGSettings_Close();");

	// ---- the device chooser, built hidden -----------------------------------
	MRPGS_buildChooser(%panel, %pw, %ph);

	// ---- the catcher, LAST ---------------------------------------------------
	//
	//THIS HAS TO BE THE LAST CHILD ADDED TO THE PANEL. Hit-testing runs last child
	//to first and stops at the first modal hit; GuiDefaultProfile is modal, so every
	//swatch above would sit on top of the catcher and eat every click. The panel
	//would draw perfectly and be completely dead - see the header.
	%cat = new GuiMouseEventCtrl(MRPG_SettingsCatch)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = %pw SPC %ph; minExtent = "8 2"; lockMouse = "0";
	};
	%panel.add(%cat);

	$MRPGSet::Built = 1;
	return %dlg;
}

//////////////////////////////////////////////////////////////////////////////
// THE INPUT METER
//
// This is the answer to "I hold V and nothing happens", which has exactly two
// causes that look identical from the outside: the key is not arriving, or the
// microphone is silent. The meter separates them in one glance - if the bar moves
// when you speak, the microphone is fine and the problem is the key; if it does
// not, the device above is wrong.
//
// It is not a decoration. The default communications endpoint on this machine
// enumerated as "Voicemeeter Out B1 (VB-Audio Voicemeeter VAIO)" - a virtual cable
// carrying silence - which opens cleanly, captures cleanly, and transmits nothing.
// Windows hands out that answer to anyone with streaming software, a webcam, or a
// USB interface installed, and no amount of correct code downstream survives it.
//
// THE GATE THRESHOLD IS DRAWN ON THE BAR. Push-to-talk still requires the level to
// beat it, so a microphone that reads below the notch is one that will not
// transmit however hard the key is held - and that is worth showing rather than
// leaving as a number in a cfg file.
//////////////////////////////////////////////////////////////////////////////

//RMS is not linear in anything a person perceives, and speech sits between about
//0.02 and 0.15 of full scale - drawn linearly the bar would never leave the first
//tenth of the groove. The square root spreads that range across most of the bar
//while still bottoming out at true silence.
$MRPGSet::MeterFull = 0.35;

function MRPGS_meterFrac(%rms)
{
	if(%rms <= 0)
		return 0;

	%f = mSqrt(%rms / $MRPGSet::MeterFull);
	return (%f > 1) ? 1 : %f;
}

function MRPGS_buildMeter(%panel, %x, %y, %w)
{
	MRPGS_label(%panel, "", %x, %y - 2, 150, 18,
		"<font:verdana bold:11><color:8A8175>Input level");

	%mx = %x + 160;
	%mw = %w - 160 - 130;

	MRPGS_swatch(%panel, %mx, %y, %mw, 12, $MRPGSet::Well);
	$MRPGSet::MeterW = %mw;

	//Built at one pixel and resized by the tick. A zero-width GuiSwatchCtrl is not
	//drawn at all, so it would look like a missing control rather than like silence.
	$MRPGS_MeterFill = MRPGS_swatch(%panel, %mx, %y, 1, 12, "110 190 120 255");

	//The gate notch, positioned by the tick from the DLL's own threshold rather
	//than from a copy of it here - a second copy is a second thing to get wrong.
	$MRPGS_MeterMark = MRPGS_swatch(%panel, %mx, %y - 2, 2, 16, "222 196 120 255");

	MRPGS_label(%panel, "MRPG_SetLevelTxt", %x + %w - 124, %y - 2, 124, 18, "");
}

//Called from the 32ms tick while the panel is open, and only then.
function MRPGS_renderMeter()
{
	if(!isObject($MRPGS_MeterFill))
		return;

	%mx = getWord($MRPGS_MeterFill.position, 0);
	%my = getWord($MRPGS_MeterFill.position, 1);

	//The groove is the meter's own parent-relative geometry; its width is what the
	//fill is measured against, and it is read off the control rather than kept in a
	//global so the two cannot drift.
	%mw = $MRPGSet::MeterW;

	%lvl  = 0;
	%gate = 0;
	%cap  = 0;
	%old  = 0;

	if(isFunction("MRPGAudio_VoiceStat"))
	{
		// 0 enabled  1 capturing  2 talking ... 10 level  11 gate
		%v    = MRPGAudio_VoiceStat();
		%cap  = getWord(%v, 1);
		%lvl  = getWord(%v, 10);
		%gate = getWord(%v, 11);

		//A DLL OLDER THAN THE METER READS AS A DEAD BAR, and a dead bar is exactly
		//what a broken microphone looks like - so it has to be told apart from one.
		//getWord past the end returns "", which numifies to 0 and is indistinguishable
		//from silence; the field COUNT is the only thing that can tell them apart.
		//This cost a debugging round the first time and would have cost one every
		//time the DLL and the scripts got out of step.
		%old = (getWordCount(%v) < 12);
	}

	%px = mFloor(MRPGS_meterFrac(%lvl) * %mw);
	$MRPGS_MeterFill.resize(%mx, %my, (%px < 1 ? 1 : %px), 12);

	if(isObject($MRPGS_MeterMark))
	{
		%gx = mFloor(MRPGS_meterFrac(%gate) * %mw);
		$MRPGS_MeterMark.resize(%mx + %gx, %my - 2, 2, 16);
		$MRPGS_MeterMark.setVisible(%cap && %gate > 0);
	}

	if(isObject(MRPG_SetLevelTxt))
	{
		if(%old)
			MRPG_SetLevelTxt.setText("<just:right><font:verdana bold:11><color:FF8A80>DLL too old");
		else if(!%cap)
			MRPG_SetLevelTxt.setText("<just:right><font:verdana bold:11><color:8A8175>mic closed");
		else if(%lvl >= %gate && %gate > 0)
			MRPG_SetLevelTxt.setText("<just:right><font:verdana bold:11><color:9BE29B>hearing you");
		else
			MRPG_SetLevelTxt.setText("<just:right><font:verdana bold:11><color:8A8175>say something");
	}
}


//One device row: a caption, a well showing the current device, and a Change button.
function MRPGS_deviceRow(%panel, %kind, %x, %y, %w, %caption, %nameCtrl)
{
	MRPGS_label(%panel, "", %x, %y, 150, 18, "<font:verdana bold:12><color:C6BEA8>" @ %caption);

	%wellX = %x + 160;
	%wellW = %w - 160 - 130;

	%well = MRPGS_swatch(%panel, %wellX, %y - 4, %wellW, 28, $MRPGSet::Well);
	MRPGS_label(%well, %nameCtrl, 8, 5, %wellW - 16, 18, "");

	MRPGS_btn(%panel, 0, %x + %w - 120, %y - 4, 120, 28, "Change",
		"MRPGSettings_OpenChooser(" @ %kind @ ");");
}


//////////////////////////////////////////////////////////////////////////////
// THE DEVICE CHOOSER
//
// A page of eight endpoints laid over the panel. Eight because that is what fits
// without the rows shrinking below a comfortable click target, and because past
// about eight the answer is "search", which a HUD panel is the wrong place for.
//
// The rows are built ONCE and re-captioned per page. Building them per page would
// mean deleting controls the catcher's registry still points at, and a registry
// full of dangling ids is a click that fires the wrong command.
//////////////////////////////////////////////////////////////////////////////
$MRPGSet::ChooserRows = 8;

function MRPGS_buildChooser(%panel, %pw, %ph)
{
	%c = new GuiSwatchCtrl(MRPG_SettingsChooser)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "0 62"; extent = %pw SPC (%ph - 64); minExtent = "8 2";
		color = $MRPGSet::Panel;
		visible = "0";
	};
	%panel.add(%c);

	%x = 26;
	%w = %pw - 52;

	MRPGS_label(%c, "MRPG_SetChooseTitle", %x, 14, %w, 22, "");

	%ry = 48;
	for(%i = 0; %i < $MRPGSet::ChooserRows; %i++)
	{
		MRPGS_btn(%c, 1, %x, %ry, %w, 34, "-", "MRPGSettings_Pick(" @ %i @ ");");
		$MRPGS_ChooseBtn[%i] = $MRPGS_BtnN - 1;
		%ry = %ry + 38;
	}

	%by = %ry + 8;
	MRPGS_btn(%c, 1, %x,             %by, 120, 30, "Previous", "MRPGSettings_Page(-1);");
	$MRPGS_PrevBtn = $MRPGS_BtnN - 1;
	MRPGS_btn(%c, 1, %x + 130,       %by, 120, 30, "Next",     "MRPGSettings_Page(1);");
	$MRPGS_NextBtn = $MRPGS_BtnN - 1;

	//Deliberately the same geometry as the panel's own Close, so the button under
	//the cursor when the chooser opens is the one that gets you back out.
	MRPGS_btn(%c, 1, %x + %w - 180, (%ph - 64) - 46, 180, 34, "Back",
		"MRPGSettings_CloseChooser();");
}

function MRPGSettings_OpenChooser(%kind)
{
	if(!MRPGSettings_HasAudio())
		return;

	$MRPGSet::ChooseKind = %kind;
	$MRPGSet::ChoosePage = 0;

	//Rescan on open. The list is only ever shown right after this call, so there is
	//no cheaper moment to be correct at - and a player opening this list is quite
	//often a player who has just plugged something in.
	$MRPGSet::ChooseCount = MRPGAudio_DeviceCount(%kind);

	$MRPGS_Mode = 1;
	$MRPGS_Press = -1;
	$MRPGS_SldDrag = -1;

	if(isObject(MRPG_SettingsChooser))
		MRPG_SettingsChooser.setVisible(1);

	MRPGSettings_RenderChooser();
}

function MRPGSettings_CloseChooser()
{
	$MRPGS_Mode = 0;
	$MRPGS_Press = -1;

	if(isObject(MRPG_SettingsChooser))
		MRPG_SettingsChooser.setVisible(0);

	MRPGSettings_Render();
}

function MRPGSettings_Page(%dir)
{
	%pages = mFloor(($MRPGSet::ChooseCount + $MRPGSet::ChooserRows - 1) / $MRPGSet::ChooserRows);
	if(%pages < 1)
		%pages = 1;

	%p = $MRPGSet::ChoosePage + %dir;
	if(%p < 0) %p = 0;
	if(%p > %pages - 1) %p = %pages - 1;

	$MRPGSet::ChoosePage = %p;
	MRPGSettings_RenderChooser();
}

//%row is the row on screen, not the device index - the page offset is added here so
//no caller has to know the paging exists.
function MRPGSettings_Pick(%row)
{
	%idx = $MRPGSet::ChoosePage * $MRPGSet::ChooserRows + %row;
	if(%idx < 0 || %idx >= $MRPGSet::ChooseCount)
		return;

	%kind = $MRPGSet::ChooseKind;
	%id   = MRPGAudio_DeviceId(%kind, %idx);
	if(%id $= "")
		return;

	//SAVE THE ID, NOT THE INDEX. See the header - indices renumber, ids do not.
	if(%kind)
		$Pref::Client::MRPG::AudioInId = %id;
	else
		$Pref::Client::MRPG::AudioOutId = %id;

	//ASK THE DLL, THEN READ BACK WHAT IT ACTUALLY DID. Opening an endpoint can fail
	//- exclusive-mode software holding it, a format nothing can negotiate - and in
	//that case the DLL keeps the device it had. Rendering the name we requested
	//would tell the player they are on a device they are not on.
	%ok = MRPGAudio_SetDevice(%kind, %id);

	if(!%ok)
	{
		//The pref is rolled back too. A device that cannot be opened now is unlikely
		//to open on the next join either, and a saved-but-broken choice would be a
		//fault that reappears every session with nothing on screen to explain it.
		if(%kind)
			$Pref::Client::MRPG::AudioInId = "";
		else
			$Pref::Client::MRPG::AudioOutId = "";

		$MRPGSet::Msg = "Windows would not open that device. Still using "
			@ MRPGAudio_CurrentDevice(%kind) @ ".";
	}
	else
		$MRPGSet::Msg = "";

	MRPGSettings_CloseChooser();
}

function MRPGSettings_RenderChooser()
{
	if(!isObject(MRPG_SettingsChooser))
		return;

	%kind  = $MRPGSet::ChooseKind;
	%count = $MRPGSet::ChooseCount;
	%page  = $MRPGSet::ChoosePage;

	%pages = mFloor((%count + $MRPGSet::ChooserRows - 1) / $MRPGSet::ChooserRows);
	if(%pages < 1)
		%pages = 1;

	if(isObject(MRPG_SetChooseTitle))
		MRPG_SetChooseTitle.setText("<font:verdana bold:15><color:F1ECC2>"
			@ (%kind ? "Choose a microphone" : "Choose speakers")
			@ "  <font:verdana bold:11><color:8A8175>"
			@ %count @ " found"
			@ (%pages > 1 ? "  -  page " @ (%page + 1) @ " of " @ %pages : ""));

	%cur = ""; //what the DLL is on right now, so the list can mark it
	if(MRPGSettings_HasAudio())
		%cur = MRPGAudio_CurrentDevice(%kind);

	for(%r = 0; %r < $MRPGSet::ChooserRows; %r++)
	{
		%b = $MRPGS_ChooseBtn[%r];
		%plate = $MRPGS_BtnPlate[%b];
		if(!isObject(%plate))
			continue;

		%idx = %page * $MRPGSet::ChooserRows + %r;
		if(%idx >= %count)
		{
			%plate.setVisible(0);
			continue;
		}

		%plate.setVisible(1);

		%name = MRPGAudio_DeviceName(%kind, %idx);
		%tag  = "";
		if(MRPGAudio_DeviceIsDefault(%kind, %idx))
			%tag = "   <color:8A8175>(Windows default)";
		if(%name $= %cur)
			%tag = %tag @ "   <color:9BE29B>(in use)";

		$MRPGS_BtnLbl[%b].setText("<font:verdana bold:12><color:F1ECC2>  "
			@ %name @ %tag);
	}

	//Paging controls only when there is more than one page. A greyed pair of buttons
	//would be two more things to read on a list whose whole job is to be scanned.
	if(isObject($MRPGS_BtnPlate[$MRPGS_PrevBtn]))
		$MRPGS_BtnPlate[$MRPGS_PrevBtn].setVisible(%pages > 1);
	if(isObject($MRPGS_BtnPlate[$MRPGS_NextBtn]))
		$MRPGS_BtnPlate[$MRPGS_NextBtn].setVisible(%pages > 1);
}


//////////////////////////////////////////////////////////////////////////////
// RENDER  -  the settings panel
//////////////////////////////////////////////////////////////////////////////
function MRPGSettings_Render()
{
	if(!$MRPGSet::Built)
		return;

	%have = MRPGSettings_HasAudio();

	// ---- device names -------------------------------------------------------
	%outName = %have ? MRPGAudio_CurrentDevice(0) : "";
	%inName  = %have ? MRPGAudio_CurrentDevice(1) : "";

	if(isObject(MRPG_SetOutName))
		MRPG_SetOutName.setText("<font:verdana bold:12><color:"
			@ (%outName $= "" ? "8A8175>-" : "F1ECC2>" @ %outName));

	if(isObject(MRPG_SetInName))
		MRPG_SetInName.setText("<font:verdana bold:12><color:"
			@ (%inName $= "" ? "8A8175>-" : "F1ECC2>" @ %inName));

	// ---- the microphone switch ----------------------------------------------
	%on = MRPGSettings_VoiceOn();
	%b  = $MRPGS_VoiceBtn;

	if(isObject($MRPGS_BtnLbl[%b]))
	{
		$MRPGS_BtnText[%b] = %on ? "Voice: ON" : "Voice: off";
		$MRPGS_BtnLbl[%b].setText("<just:center><font:verdana bold:12><color:"
			@ (%on ? "9BE29B>" : "C6BEA8>") @ $MRPGS_BtnText[%b]);
	}

	if(isObject(MRPG_SetVoiceLbl))
	{
		//moveMap is gone in some teardown orders, and this render can be reached from
		//a schedule that outlives it by a frame.
		%key = "";
		if(isObject(moveMap))
			%key = MRPG_keyOfBinding(moveMap.getBinding("MRPG_VoicePTT"));
		%key = (%key $= "") ? "your push-to-talk key" : strupr(%key);

		MRPG_SetVoiceLbl.setText("<font:verdana bold:12><color:C6BEA8>Voice chat"
			@ "<br><font:verdana bold:11><color:8A8175>"
			@ (%on ? "Hold " @ %key @ " to talk. The green microphone shows when you are live."
			       : "Your microphone is off. Nothing is captured or sent."));
	}

	// ---- volumes ------------------------------------------------------------
	for(%i = 0; %i < $MRPGS_SldN; %i++)
	{
		%cat = $MRPGS_SldCat[%i];
		%g   = $MRPGS_SldGroove[%i];
		if(!isObject(%g))
			continue;

		%gx = getWord(%g.position, 0);
		%gy = getWord(%g.position, 1);
		%gw = getWord(%g.getExtent(), 0);

		%v = MRPGSettings_Vol(%cat);
		%px = mFloor(%v * %gw);

		//A one-pixel fill rather than a zero-pixel one: GuiSwatchCtrl at width 0 is
		//not drawn at all, and the groove then looks like it has no fill control
		//rather than like a volume of zero.
		if(isObject($MRPGS_SldFill[%i]))
			$MRPGS_SldFill[%i].resize(%gx, %gy, (%px < 1 ? 1 : %px), 6);

		if(isObject($MRPGS_SldKnob[%i]))
			$MRPGS_SldKnob[%i].resize(%gx + %px - 5, %gy - 7, 10, 20);

		if(isObject($MRPGS_SldVal[%i]))
			$MRPGS_SldVal[%i].setText("<just:right><font:verdana bold:12><color:"
				@ (%v <= 0 ? "8A8175>Off" : "F1ECC2>" @ mFloor(%v * 100 + 0.5) @ "%"));
	}

	// ---- the status line ----------------------------------------------------
	if(isObject(MRPG_SetStatus))
	{
		if(!%have)
			MRPG_SetStatus.setText("<font:verdana bold:11><color:FF8A80>"
				@ "MonsterRPG's audio engine is not running, so nothing on this screen "
				@ "can be applied.<br><color:8A8175>"
				@ "Start the game with MonsterRPGAudio.bat to enable it.");
		else if($MRPGSet::Msg !$= "")
			MRPG_SetStatus.setText("<font:verdana bold:11><color:FF8A80>" @ $MRPGSet::Msg);
		else
			MRPG_SetStatus.setText("<font:verdana bold:11><color:8A8175>"
				@ "These control MonsterRPG's own audio. Blockland's music and sound "
				@ "sliders are still in Options > Audio.<br>"
				@ "Your choices are saved and used again the next time you join.");
	}
}


//////////////////////////////////////////////////////////////////////////////
// ACTIONS
//////////////////////////////////////////////////////////////////////////////
function MRPGSettings_ToggleVoice()
{
	%on = MRPGSettings_VoiceOn() ? 0 : 1;

	//ASK BEFORE CLAIMING. Opening the microphone can fail - no capture endpoint at
	//all, or one another program holds exclusively - and the switch must show what
	//happened rather than what was requested. This is the same rule the device
	//chooser follows, and it is the rule a privacy control has to follow.
	if(MRPGSettings_HasAudio())
	{
		if(!MRPGAudio_VoiceEnable(%on) && %on)
		{
			$MRPGSet::Msg = "No microphone could be opened. Check the device above.";
			MRPGSettings_Render();
			return;
		}
	}

	$Pref::Client::MRPG::VoiceOn = %on;
	$MRPGSet::Msg = "";
	MRPGSettings_Render();
}

function MRPGSettings_Rescan()
{
	if(!MRPGSettings_HasAudio())
		return;

	%o = MRPGAudio_DeviceCount(0);
	%i = MRPGAudio_DeviceCount(1);

	$MRPGSet::Msg = "";
	MRPGSettings_Render();

	echo("MonsterRPG settings: " @ %o @ " playback device(s), " @ %i @ " recording device(s).");
}

function MRPGSettings_Open()
{
	if(!MRPG_isActive())
		return;

	MRPGSettings_Build();
	if(!isObject(MRPG_SettingsDlg))
		return;

	$MRPGSet::Open = 1;
	$MRPGS_Mode = 0;
	$MRPGSet::Msg = "";

	if(isObject(MRPG_SettingsChooser))
		MRPG_SettingsChooser.setVisible(0);

	//Devices are enumerated on open rather than on build: the list is only true at
	//the moment it is read, and a panel built at join time would show whatever was
	//plugged in then.
	if(MRPGSettings_HasAudio())
	{
		MRPGAudio_DeviceCount(0);
		MRPGAudio_DeviceCount(1);
	}

	Canvas.pushDialog(MRPG_SettingsDlg);
	MRPGSettings_Render();
	MRPGSettings_Tick();
}

function MRPGSettings_Close()
{
	$MRPGSet::Open = 0;
	$MRPGS_Press = -1;
	$MRPGS_SldDrag = -1;

	cancel($MRPGSet::TickSch);
	$MRPGSet::TickSch = "";

	if(isObject(MRPG_SettingsDlg))
		Canvas.popDialog(MRPG_SettingsDlg);

	//Free mouse is cancelled by GuiCanvas::checkCursor when a dialog pops - the
	//cursor goes off unless a child lacks noCursor, and PlayGui sets it. Keybinds.cs
	//re-asserts it a frame later for exactly this case.
	if(isFunction("MRPG_RestoreCursor"))
		MRPG_RestoreCursor();
}

function MRPG_ToggleSettings(%val)
{
	//Key-DOWN edge, and only on this server. Off-server the key is not bound to this
	//command at all - the broker in Keybinds.cs hands it back on leave - so this
	//guard only catches a stray call.
	if(!MRPG_gateKey(%val))
		return;

	if($MRPGSet::Open)
	{
		//Escape out of the chooser first if that is where we are. Two presses to get
		//out of two screens is what everything else on this HUD does.
		if($MRPGS_Mode == 1)
			MRPGSettings_CloseChooser();
		else
			MRPGSettings_Close();
	}
	else
		MRPGSettings_Open();
}


//////////////////////////////////////////////////////////////////////////////
// INPUT  -  one catcher, one tick
//////////////////////////////////////////////////////////////////////////////

//Which registered button is under the cursor, or -1. Filtered on mode as well as on
//geometry: see the note on MRPGS_btn about isVisible() and hidden parents.
function MRPGS_btnAt()
{
	%cur = Canvas.getCursorPos();
	%cx = getWord(%cur, 0);  %cy = getWord(%cur, 1);

	for(%i = 0; %i < $MRPGS_BtnN; %i++)
	{
		if($MRPGS_BtnMode[%i] != $MRPGS_Mode)
			continue;

		%pl = $MRPGS_BtnPlate[%i];
		if(!isObject(%pl) || !%pl.isVisible())
			continue;

		%p = %pl.getCanvasPosition();  %e = %pl.getExtent();
		%px = getWord(%p, 0);  %py = getWord(%p, 1);
		%pw = getWord(%e, 0);  %ph = getWord(%e, 1);

		if(%cx >= %px && %cx < %px + %pw && %cy >= %py && %cy < %py + %ph)
			return %i;
	}
	return -1;
}

//Which slider groove is under the cursor. The grab band is padded well past the 6px
//bar - a groove you have to hit exactly is a groove that feels broken.
function MRPGS_sldAt()
{
	if($MRPGS_Mode != 0)
		return -1;

	%cur = Canvas.getCursorPos();
	%cx = getWord(%cur, 0);  %cy = getWord(%cur, 1);

	for(%i = 0; %i < $MRPGS_SldN; %i++)
	{
		%g = $MRPGS_SldGroove[%i];
		if(!isObject(%g))
			continue;

		%p = %g.getCanvasPosition();  %e = %g.getExtent();
		%px = getWord(%p, 0);  %py = getWord(%p, 1);  %pw = getWord(%e, 0);

		if(%cx >= %px - 8 && %cx <= %px + %pw + 8 && %cy >= %py - 11 && %cy <= %py + 17)
			return %i;
	}
	return -1;
}

function MRPGS_sldTrack()
{
	%i = $MRPGS_SldDrag;
	if(%i < 0 || !isObject($MRPGS_SldGroove[%i]))
		return;

	%g = $MRPGS_SldGroove[%i];
	%p = %g.getCanvasPosition();  %e = %g.getExtent();
	%gw = getWord(%e, 0);
	if(%gw <= 0)
		return;

	%v = (getWord(Canvas.getCursorPos(), 0) - getWord(%p, 0)) / %gw;
	if(%v < 0) %v = 0;
	if(%v > 1) %v = 1;

	//Snapped to whole percent. Sub-percent precision on a volume is not something a
	//player can hear or reproduce, and the readout would flicker through values they
	//never chose.
	%v = mFloor(%v * 100 + 0.5) / 100;

	%cat = $MRPGS_SldCat[%i];
	if($Pref::Client::MRPG::Vol[%cat] == %v)
		return;

	$Pref::Client::MRPG::Vol[%cat] = %v;

	//APPLIED ON EVERY PIXEL OF TRAVEL, not on release. A volume slider you cannot
	//hear while you drag it is a volume slider you have to guess at.
	if(MRPGSettings_HasAudio())
		MRPGAudio_SetVolume(%cat, %v);

	MRPGSettings_Render();
}

function MRPGS_press()
{
	$MRPGS_SldDrag = MRPGS_sldAt();
	if($MRPGS_SldDrag >= 0)
	{
		MRPGS_sldTrack();   // a click anywhere on the groove jumps the knob there
		return;
	}

	$MRPGS_Press = MRPGS_btnAt();
	if($MRPGS_Press >= 0 && isObject($MRPGS_BtnPlate[$MRPGS_Press]))
		$MRPGS_BtnPlate[$MRPGS_Press].color = $MRPGSet::PlateH;
}

function MRPGS_release()
{
	if($MRPGS_SldDrag >= 0)
	{
		$MRPGS_SldDrag = -1;
		return;
	}

	%i = $MRPGS_Press;
	$MRPGS_Press = -1;

	if(%i < 0)
		return;

	if(isObject($MRPGS_BtnPlate[%i]))
		$MRPGS_BtnPlate[%i].color = $MRPGSet::Plate;

	//FIRE ONLY IF THE CURSOR IS STILL ON THE BUTTON IT WENT DOWN ON. Press-and-slide-
	//off is how every other button on this HUD lets you change your mind, and one of
	//these buttons opens a microphone.
	if(MRPGS_btnAt() != %i)
		return;

	eval($MRPGS_BtnCmd[%i]);
}

function MRPG_SettingsCatch::onMouseDown(%this) { MRPGS_press();   }
function MRPG_SettingsCatch::onMouseUp(%this)   { MRPGS_release(); }

//The tick exists for the drag: GuiMouseEventCtrl does send onMouseDragged, but the
//catcher only receives it while the cursor is inside the catcher's own rect, and a
//player dragging a volume to zero routinely runs off the left edge of the panel.
//Sampling the cursor instead means the drag survives leaving the control - which is
//how every slider anywhere behaves.
function MRPGSettings_Tick()
{
	cancel($MRPGSet::TickSch);
	$MRPGSet::TickSch = "";

	if(!$MRPGSet::Open || !MRPG_isActive())
		return;

	//The meter first, and it runs in EVERY mode - the chooser is exactly where a
	//player is testing microphones, and a meter that froze the moment they opened
	//the list would be useless at the one job it has. It is hidden behind the
	//chooser, but the settings panel is one key away and shows the answer.
	MRPGS_renderMeter();

	if($MRPGS_SldDrag >= 0)
		MRPGS_sldTrack();
	else
	{
		//Hover highlight. Cheap, and it is the only thing telling the player which of
		//these plates is a button.
		%h = MRPGS_btnAt();
		if(%h != $MRPGS_Hover)
		{
			if($MRPGS_Hover >= 0 && isObject($MRPGS_BtnPlate[$MRPGS_Hover]))
				$MRPGS_BtnPlate[$MRPGS_Hover].color = $MRPGSet::Plate;

			if(%h >= 0 && isObject($MRPGS_BtnPlate[%h]))
				$MRPGS_BtnPlate[%h].color = $MRPGSet::PlateH;

			$MRPGS_Hover = %h;
		}
	}

	$MRPGSet::TickSch = schedule($MRPGSet::TickMs, 0, MRPGSettings_Tick);
}


//////////////////////////////////////////////////////////////////////////////
// MRPGSettings_Dump() - what is the audio engine on right now?
//
// A console function, not a slash command: type it into the console (~). The same
// argument as MRPG_dumpKeys - the claims this file makes about devices and volumes
// should be checkable rather than assumed, and the saved pref is printed next to
// the live value so "it did not stick" and "it never applied" are different lines.
//////////////////////////////////////////////////////////////////////////////
function MRPGSettings_Dump()
{
	if(!MRPGSettings_HasAudio())
	{
		echo("MonsterRPG audio: the DLL is not loaded in this process.");
		return;
	}

	echo("MonsterRPG audio:");
	echo("  speakers   " @ MRPGAudio_CurrentDevice(0)
		@ "   (saved: " @ ($Pref::Client::MRPG::AudioOutId $= "" ? "system default" : $Pref::Client::MRPG::AudioOutId) @ ")");
	echo("  microphone " @ MRPGAudio_CurrentDevice(1)
		@ "   (saved: " @ ($Pref::Client::MRPG::AudioInId $= "" ? "system default" : $Pref::Client::MRPG::AudioInId) @ ")");
	echo("  voice      " @ (MRPGSettings_VoiceOn() ? "on" : "off"));

	for(%c = 0; %c < 4; %c++)
		echo("  vol[" @ %c @ "] " @ $MRPGSet::VolName[%c] @ " = "
			@ MRPGAudio_GetVolume(%c) @ "   (pref " @ MRPGSettings_Vol(%c) @ ")");
}
