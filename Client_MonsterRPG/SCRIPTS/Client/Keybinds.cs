//////////////////////////////////////////////////////////////////////////////
// Keybinds.cs  -  MonsterRPG BORROWS keys. It never keeps them.
//////////////////////////////////////////////////////////////////////////////
//
// THE RULE: after a player leaves a MonsterRPG server, their moveMap must look
// EXACTLY as it did before they joined, and config/client/config.cs must never
// contain a single MonsterRPG binding. Not a dead key, not a leftover row in
// Options > Keyboard, nothing. The next server they join - and any other client
// mod they use - must be unable to tell this add-on exists.
//
// -----------------------------------------------------------------------------
// WHY THE OLD APPROACH COULD NOT DELIVER THAT
// -----------------------------------------------------------------------------
//
// Every key used to be claimed at FILE SCOPE, at game launch, by MRPG_bindDefault:
//
//     MRPG_bindDefault("j", "MRPG_toggleAttrScreen");   // AttributeScreen.cs
//     MRPG_bindDefault("k", "MRPG_toggleTree");         // TreeClient.cs
//     MRPG_bindDefault("n", "MRPG_toggleCharacter");    // CharacterScreen.cs
//
// That refuses to overwrite a binding the player chose, which solved the ORIGINAL
// bug (add-ons load after config.cs, so a raw moveMap.bind erased their remap on
// every launch). But it still wrote MonsterRPG commands into the one global
// moveMap on every client, on every server, from the moment the game started -
// and it had no way to ever give them back.
//
// It got worse from there, because moveMap IS SAVED. optionsDlg::onSleep calls
//
//     moveMap.save("config/client/config.cs");     allClientScripts.cs:3001
//
// so a player who opened and closed the Options dialog once, on a MonsterRPG
// server, had our binds written into their config permanently. From then on
// "m" was the map and not the cursor, on every server they ever joined, and the
// only trace of why was a file they had no reason to read.
//
// -----------------------------------------------------------------------------
// THE MODEL: BORROW ON JOIN, RETURN ON LEAVE
// -----------------------------------------------------------------------------
//
//   MRPG_borrowKeys()   from MRPG_ClientEnter. For each command: work out which
//                       key it wants, RECORD WHAT IS ALREADY THERE, bind ours.
//                       Then add our rows to Options > Keyboard.
//
//   MRPG_returnKeys()   from MRPG_ClientLeave. Unbind every command of ours,
//                       remember where the player had moved it, put back exactly
//                       what we displaced, and remove our rows again.
//
// WE RECORD WHAT WAS THERE RATHER THAN A TABLE OF STOCK DEFAULTS. That is the
// difference between "restore the key" and "restore what I assumed the key was" -
// it puts back another add-on's binding just as faithfully as a stock one, and it
// cannot be wrong about the player's own layout. Writing that table by hand was
// tried and it was already wrong: stock binds "1" to useBricks, not useFirstSlot.
//
// A REMAP MID-SESSION STILL STICKS, which is the one thing the old code did get
// right and must not regress. $Pref::Client::MRPG::Key[cmd] holds the key the
// player chose; MRPG_returnKeys reads the live binding before unbinding it, so
// moving the map to "g" in Options is remembered and re-borrowed on the next join.
// Prefs are exported to config/client/prefs.cs, which is OUR file to write in -
// unlike config.cs, which belongs to the engine and to every other add-on.
//
// -----------------------------------------------------------------------------
// THREE THINGS THIS FILE HAS TO GET RIGHT BEYOND THE OBVIOUS
// -----------------------------------------------------------------------------
//
//   1. THE OPTIONS DIALOG SAVES moveMap AS IT CLOSES. MRPGKeyBroker wraps
//      optionsDlg::onSleep to hand every key back BEFORE Parent:: runs the save,
//      and take them again after. config.cs therefore never sees a MonsterRPG
//      bind even if the player opens Options mid-game.
//
//   2. CONFIGS THAT ARE ALREADY POLLUTED. Players who ran an earlier build have
//      our binds saved in config.cs right now, and it has already been exec'd by
//      the time this file loads. MRPG_reclaimSavedBinds() runs at boot, strips
//      them, and hands the well-known ones back to stock.
//
//   3. SUPER SHIFT. Free mouse wants lalt, which stock uses for toggleSuperShift.
//      Rather than leave super shift homeless for the whole session it is parked
//      on ralt while we play - and that parking is undone on leave like everything
//      else. See MRPG_parkSuperShift.
//////////////////////////////////////////////////////////////////////////////


//////////////////////////////////////////////////////////////////////////////
// THE TABLE
//
// ONE list, and it is the only place a MonsterRPG key is named. Every screen used
// to bind its own key from its own file, which is how three of them ended up
// binding at file scope: there was no single place that could be called at the
// right moment instead.
//
// A command with an empty default is registered for Options > Keyboard but not
// bound - the player has to choose a key for it. MonsterRPGx_GUI_Toggle is the
// only one; it has been that way since before this rewrite.
//////////////////////////////////////////////////////////////////////////////
function MRPG_defKey(%cmd, %label, %key)
{
	$MRPG_Key_Cmd[$MRPG_Key_N]   = %cmd;
	$MRPG_Key_Label[$MRPG_Key_N] = %label;
	$MRPG_Key_Def[$MRPG_Key_N]   = %key;
	$MRPG_Key_N++;
}

function MRPG_buildKeyTable()
{
	if($MRPG_Key_N > 0)
		return;

	$MRPG_Key_N = 0;

	MRPG_defKey("MonsterRPGx_GUI_Toggle", "Menu",                          "");
	MRPG_defKey("MRPG_toggleCharacter",   "Character screen",              "n");
	MRPG_defKey("MRPG_toggleAttrScreen",  "Attributes screen",             "j");
	MRPG_defKey("MRPG_toggleTree",        "Ability tree",                  "k");
	MRPG_defKey("MRPG_ToggleMap",         "Map (cycle large/corner/off)",  "m");

	//Free mouse. lalt, the key WoW and most of the genre use for "let go of the
	//camera and give me a pointer" - it is what makes the chat tabs, the name
	//links and every other clickable thing in the HUD reachable without opening a
	//menu first. See MRPG_parkSuperShift for what happens to the key's tenant.
	MRPG_defKey("MRPG_FreeMouse",         "Free mouse (cursor toggle)",    "lalt");

	//Chat. backslash sits next to ] on every layout this ships on, so the three
	//chat keys end up physically grouped.
	MRPG_defKey("MRPGChat_NextTab",       "Chat - next channel",           "rbracket");
	MRPG_defKey("MRPGChat_PrevTab",       "Chat - previous channel",       "lbracket");
	MRPG_defKey("MRPGChat_ToggleMode",    "Chat - full / notify / hide",   "backslash");

	//The hotbar. These twelve are the ones that made the old model's cost obvious:
	//they take 1-0 off useBricks and useSecondSlot..useTenthSlot, which is most of
	//how a player interacts with Blockland when they are not on this server.
	%keys = "1 2 3 4 5 6 7 8 9 0 minus equals";
	for(%i = 1; %i <= 12; %i++)
		MRPG_defKey("MRPG_Cast" @ %i, "Hotbar slot " @ %i, getWord(%keys, %i - 1));

	//PUSH TO TALK. A bare key, because it is held rather than tapped and a
	//modifier combination cannot be held comfortably while also moving.
	//
	//"v" is the genre convention and is not claimed by anything in this table.
	//Like every other row here it is BORROWED: whatever held it before the join
	//gets it back on the way out, and a player who rebinds it has that choice
	//remembered for next time - see MRPG_returnKeys and MRPG_reclaimSavedBinds.
	MRPG_defKey("MRPG_VoicePTT",          "Push to talk",                  "v");

	//THE SETTINGS MENU on ctrl+o, and a modifier for the same reason music uses one:
	//every bare key worth having is already in this table or is one a player rests a
	//finger on, and a full-screen panel that opens because someone brushed a key
	//while fighting is worse than one that takes two keys to reach.
	//
	//It is also the least important key here to have memorised - the gear in the
	//top-left corner opens the same screen with a click, and prints this binding
	//underneath itself so nobody has to be told what it is. See Settings.cs.
	MRPG_defKey("MRPG_ToggleSettings",    "Settings menu",                 "ctrl o");

	//MUSIC MUTE on ctrl+m, not a bare key. Every single key worth having is either
	//in this table already or is one a player rests a finger on, and a modifier
	//combination cannot be hit by accident while moving or fighting - music that
	//cuts out mid-fight because someone brushed a key is worse than music that
	//cannot be turned off at all.
	MRPG_defKey("MRPG_toggleMusic",       "Music on / off",                "ctrl m");
}

MRPG_buildKeyTable();


//////////////////////////////////////////////////////////////////////////////
// HELPERS
//////////////////////////////////////////////////////////////////////////////

//"keyboard ctrl m" -> "ctrl m". The key half can be several words, so getWords,
//not getWord - a single-word read here silently turns "ctrl m" into "ctrl" and
//unbinds the wrong thing.
function MRPG_keyOfBinding(%bind)
{
	if(%bind $= "")
		return "";
	return getWords(%bind, 1, getWordCount(%bind) - 1);
}

//Is %cmd one of ours? Used to make sure we never record a MonsterRPG command as
//the thing to "restore" when two of our own commands touch one key.
function MRPG_isOurCmd(%cmd)
{
	if(%cmd $= "")
		return 0;
	for(%i = 0; %i < $MRPG_Key_N; %i++)
		if($MRPG_Key_Cmd[%i] $= %cmd)
			return 1;
	return 0;
}

//Which key a command should take: what the player last chose, else the default.
function MRPG_wantedKey(%i)
{
	%cmd = $MRPG_Key_Cmd[%i];
	%key = $Pref::Client::MRPG::Key[%cmd];
	if(%key $= "")
		%key = $MRPG_Key_Def[%i];
	return %key;
}

//Kept because other files call it. Is %key currently driving %cmd?
function MRPG_keyIs(%key, %cmd)
{
	return MRPG_keyOfBinding(moveMap.getBinding(%cmd)) $= %key;
}


//////////////////////////////////////////////////////////////////////////////
// BORROW
//////////////////////////////////////////////////////////////////////////////
function MRPG_borrowKeys()
{
	if($MRPG_KeysBorrowed || !isObject(moveMap))
		return;

	//SET BEFORE THE LOOP. moveMap.bind can fire callbacks in other add-ons'
	//packages, and a re-entrant borrow would record OUR OWN binding as the thing
	//to give back - which is how a "restore" turns into a permanent claim.
	$MRPG_KeysBorrowed = 1;

	for(%i = 0; %i < $MRPG_Key_N; %i++)
	{
		%cmd = $MRPG_Key_Cmd[%i];
		%key = MRPG_wantedKey(%i);

		$MRPG_Held_Key[%cmd]  = "";
		$MRPG_Held_Prev[%cmd] = "";

		if(%key $= "")
			continue;   // registered for remapping, no default - nothing to take

		//WHAT IS THERE RIGHT NOW is what we owe back. It may be a stock command, it
		//may be another add-on's, it may be nothing at all; we do not need to know
		//which, and not needing to know is the point.
		%prev = moveMap.getCommand(keyboard, %key);
		if(MRPG_isOurCmd(%prev))
			%prev = "";

		$MRPG_Held_Key[%cmd]  = %key;
		$MRPG_Held_Prev[%cmd] = %prev;

		moveMap.bind(keyboard, %key, %cmd);
	}

	MRPG_parkSuperShift();
	MRPG_regAllRemaps();

	//The gear in the corner prints the settings key underneath itself, and this is
	//the moment that key becomes true - both on join and on the re-borrow that
	//follows the Options dialog closing, which is where a remap actually lands.
	if(isFunction("MRPGSettings_RefreshHint"))
		MRPGSettings_RefreshHint();
}


//////////////////////////////////////////////////////////////////////////////
// RETURN
//
// TWO PASSES, AND THEY CANNOT BE MERGED. Every one of our commands has to be
// unbound before any restore is written, or a key that one MonsterRPG command
// took from another gets "restored" into a slot we have not let go of yet, and
// the player is left with a MonsterRPG bind they can never see or remove.
//////////////////////////////////////////////////////////////////////////////
function MRPG_returnKeys()
{
	if(!$MRPG_KeysBorrowed || !isObject(moveMap))
		return;

	$MRPG_KeysBorrowed = 0;

	// ---- pass 1: let go, and remember where the player had put it -----------
	for(%i = 0; %i < $MRPG_Key_N; %i++)
	{
		%cmd  = $MRPG_Key_Cmd[%i];
		%pref = "";

		//A LOOP, NOT ONE READ, BECAUSE ONE COMMAND CAN BE ON TWO KEYS.
		//
		//ActionMap::processBind keys its node table on (device, event) and never
		//touches other nodes (actionMap.cc:1033) - and stock's remap dialog just
		//calls moveMap.bind for the new key without unbinding the old one
		//(OptRemapInputCtrl::onInputEvent -> allClientScripts.cs:3804). So a player
		//who remaps the map from "m" to "g" leaves MRPG_ToggleMap bound to BOTH, and
		//a single getBinding/unbind here would release one and leave the other in
		//their config forever. Bounded at 8 so a getBinding that disagrees with
		//unbind cannot spin.
		for(%n = 0; %n < 8; %n++)
		{
			%key = MRPG_keyOfBinding(moveMap.getBinding(%cmd));
			if(%key $= "")
				break;

			//PREFER A KEY WE DID NOT TAKE. If the command is on two keys, the one
			//that is not the key we borrowed is the one the player chose - remembering
			//the borrowed default instead would silently throw their remap away.
			if(%pref $= "" || %key !$= $MRPG_Held_Key[%cmd])
				%pref = %key;

			moveMap.unbind(keyboard, %key);
		}

		//Their choice, remembered for the next join. Written to a $Pref:: global so
		//it lands in config/client/prefs.cs rather than in config.cs.
		if(%pref !$= "")
			$Pref::Client::MRPG::Key[%cmd] = %pref;
	}

	// ---- pass 2: give back exactly what was displaced -----------------------
	for(%i = 0; %i < $MRPG_Key_N; %i++)
	{
		%cmd  = $MRPG_Key_Cmd[%i];
		%key  = $MRPG_Held_Key[%cmd];
		%prev = $MRPG_Held_Prev[%cmd];

		$MRPG_Held_Key[%cmd]  = "";
		$MRPG_Held_Prev[%cmd] = "";

		if(%key $= "" || %prev $= "")
			continue;

		//Only into a key that is now free. If something else has claimed it in the
		//meantime - the player, or another add-on - that claim is newer than ours
		//and taking it back would be the same rudeness this rewrite is undoing.
		if(moveMap.getCommand(keyboard, %key) $= "")
			moveMap.bind(keyboard, %key, %prev);
	}

	MRPG_unparkSuperShift();
	MRPG_unregAllRemaps();
}


//////////////////////////////////////////////////////////////////////////////
// SUPER SHIFT
//
// Free mouse wants lalt and stock keeps toggleSuperShift there (Windows; the Mac
// build uses "ropt", which is why this only fires when lalt really was its home).
// The generic borrow above already records toggleSuperShift as the thing to give
// back, so leaving it at that would be CORRECT - it would just mean super shift
// has no key at all for the length of the session, which is a real loss for
// anyone who builds between fights.
//
// So it is parked on ralt instead, and the parking is undone on the way out like
// everything else. Windows leaves ralt unbound, and if it does not we do nothing
// rather than evict a second key to make room for the first.
//////////////////////////////////////////////////////////////////////////////
function MRPG_parkSuperShift()
{
	$MRPG_SuperShiftParked = 0;

	//Only if free mouse actually ended up on the key super shift was using, and
	//only if super shift has nowhere else to be.
	if($MRPG_Held_Prev["MRPG_FreeMouse"] !$= "toggleSuperShift")
		return;

	if(moveMap.getBinding("toggleSuperShift") !$= "")
		return;   // it has another home already - leave it alone

	if(moveMap.getCommand(keyboard, "ralt") !$= "")
		return;   // ralt is spoken for; super shift goes without rather than evict it

	moveMap.bind(keyboard, "ralt", "toggleSuperShift");
	$MRPG_SuperShiftParked = 1;
}

function MRPG_unparkSuperShift()
{
	if(!$MRPG_SuperShiftParked)
		return;

	$MRPG_SuperShiftParked = 0;

	//Only if it is still OUR parking. If the player rebound ralt during the
	//session, that is theirs now.
	if(moveMap.getCommand(keyboard, "ralt") $= "toggleSuperShift")
		moveMap.unbind(keyboard, "ralt");
}


//////////////////////////////////////////////////////////////////////////////
// OPTIONS > KEYBOARD ROWS
//
// Added on join and REMOVED on leave. A row is not a binding, but a MonsterRPGx
// section sitting in the keyboard options of a player on a build server is still
// this add-on being "a factor" - and worse, it would let them bind a MonsterRPG
// command off-server, putting back by hand exactly the claim we just released.
//
// THE DIVISION HEADER GOES ON THE FIRST ENTRY ONLY. OptRemapList::fillList emits
// a title plus a separator for every index whose $RemapDivision is set
// (allClientScripts.cs:3700), so setting it on all of them draws "MonsterRPGx"
// and a rule in front of every single row.
//////////////////////////////////////////////////////////////////////////////
function MRPG_regAllRemaps()
{
	if($MRPG_RemapsRegistered)
		return;
	$MRPG_RemapsRegistered = 1;

	for(%i = 0; %i < $MRPG_Key_N; %i++)
	{
		%cmd = $MRPG_Key_Cmd[%i];

		//Never twice, even if something outside this file registered it.
		%dup = 0;
		for(%j = 0; %j < $RemapCount; %j++)
			if($RemapCmd[%j] $= %cmd)
				%dup = 1;
		if(%dup)
			continue;

		//ON THE FIRST ROW WE ACTUALLY ADD, not on %i == 0. If entry zero were ever
		//skipped as a duplicate, keying the header off the loop index would drop the
		//"MonsterRPGx" heading entirely and leave our rows loose in whatever section
		//happened to precede them.
		if(!%headerDone)
		{
			$RemapDivision[$RemapCount] = "MonsterRPGx";
			%headerDone = 1;
		}

		$RemapName[$RemapCount] = $MRPG_Key_Label[%i];
		$RemapCmd[$RemapCount]  = %cmd;
		$RemapCount++;
	}
}

//COMPACTED, not blanked. fillList walks 0..$RemapCount and uses the index as the
//row id, so a hole left in the middle would draw an empty row that doRemap then
//looks up by that id and finds nothing behind. Rebuilding the arrays packed keeps
//every other add-on's entries contiguous and correctly divided.
function MRPG_unregAllRemaps()
{
	if(!$MRPG_RemapsRegistered)
		return;
	$MRPG_RemapsRegistered = 0;

	%out = 0;
	for(%i = 0; %i < $RemapCount; %i++)
	{
		if(MRPG_isOurCmd($RemapCmd[%i]))
		{
			//A division header sitting on a row we are deleting has to move to the
			//next surviving row, or the section it titles loses its heading.
			if($RemapDivision[%i] !$= "" && $RemapDivision[%i] !$= "MonsterRPGx")
				%carry = $RemapDivision[%i];
			continue;
		}

		%div = $RemapDivision[%i];
		if(%carry !$= "")
		{
			if(%div $= "")
				%div = %carry;
			%carry = "";
		}

		$RemapName[%out]     = $RemapName[%i];
		$RemapCmd[%out]      = $RemapCmd[%i];
		$RemapDivision[%out] = %div;
		%out++;
	}

	//Clear the tail so nothing stale is reachable if $RemapCount ever grows again.
	for(%i = %out; %i < $RemapCount; %i++)
	{
		$RemapName[%i]     = "";
		$RemapCmd[%i]      = "";
		$RemapDivision[%i] = "";
	}

	$RemapCount = %out;
}


//////////////////////////////////////////////////////////////////////////////
// MIGRATION - CONFIGS THAT ARE ALREADY POLLUTED
//
// Runs at boot, and it HAS to: client/init.cs execs config/client/config.cs
// before it calls loadClientAddOns(), so by the time this file loads, any
// MonsterRPG binding an earlier build saved is already live in moveMap.
//
// This is the one piece of work in the add-on that legitimately happens at file
// scope. It is not "running MonsterRPG code off-server" - it is removing it.
//
// WHERE THE KEY GOES BACK TO. We have no record of what we displaced (that record
// is the thing this whole rewrite adds), so the key can only be handed back where
// stock's own default is unconditional - allClientScripts.cs:15192-15216:
//
//     m    ToggleCursor          1    useBricks          2-9  useSecondSlot..useNinthSlot
//     0    useTenthSlot          lalt toggleSuperShift (Windows only)
//
// j and k are DELIBERATELY not in that list. Stock only binds them - to
// shiftBrickLeft / shiftBrickTowards - on the IJKL build layout; on the numpad
// layout they are free. Guessing wrong there would hand the player a binding they
// never had, so they are simply released.
//////////////////////////////////////////////////////////////////////////////
function MRPG_stockDefaultFor(%key)
{
	switch$(%key)
	{
		case "m":    return "ToggleCursor";
		case "1":    return "useBricks";
		case "2":    return "useSecondSlot";
		case "3":    return "useThirdSlot";
		case "4":    return "useFourthSlot";
		case "5":    return "useFifthSlot";
		case "6":    return "useSixthSlot";
		case "7":    return "useSeventhSlot";
		case "8":    return "useEighthSlot";
		case "9":    return "useNinthSlot";
		case "0":    return "useTenthSlot";
		case "lalt": return isWindows() ? "toggleSuperShift" : "";
		default:     return "";
	}
}

function MRPG_reclaimSavedBinds()
{
	if(!isObject(moveMap))
		return 0;

	%found = 0;

	for(%i = 0; %i < $MRPG_Key_N; %i++)
	{
		%cmd = $MRPG_Key_Cmd[%i];

		//Same loop as MRPG_returnKeys pass 1, and for the same reason: a saved config
		//can have one command on more than one key.
		for(%n = 0; %n < 8; %n++)
		{
			%key = MRPG_keyOfBinding(moveMap.getBinding(%cmd));
			if(%key $= "")
				break;

			%found++;

			//HONOUR THE REMAP WE ARE ABOUT TO DELETE. If they had moved this command
			//to a key of their own, that choice survives into the pref and the next
			//join puts it back there. Only when we have nothing stored - the saved
			//config is the older source of truth.
			if($Pref::Client::MRPG::Key[%cmd] $= "")
				$Pref::Client::MRPG::Key[%cmd] = %key;

			moveMap.unbind(keyboard, %key);

			//Hand the key back to stock, but ONLY if stock's command has nowhere else
			//to live. A player who moved ToggleCursor somewhere of their own before
			//this add-on ever took "m" would otherwise end up with it on two keys, and
			//the second one would look like something we invented.
			%stock = MRPG_stockDefaultFor(%key);
			if(%stock !$= ""
			&& moveMap.getCommand(keyboard, %key) $= ""
			&& moveMap.getBinding(%stock) $= "")
				moveMap.bind(keyboard, %key, %stock);
		}
	}

	//The old MRPG_setupFreeMouse parked super shift on ralt permanently. If lalt
	//has just gone back to toggleSuperShift above, the ralt copy is a duplicate
	//this add-on left behind and should go with the rest of it.
	if(isWindows()
	&& moveMap.getCommand(keyboard, "ralt")  $= "toggleSuperShift"
	&& moveMap.getCommand(keyboard, "lalt")  $= "toggleSuperShift")
	{
		moveMap.unbind(keyboard, "ralt");
		%found++;
	}

	//Our Options rows may have been registered by an older build too.
	MRPG_unregAllRemaps();

	if(%found)
		echo("MonsterRPG keybinds: reclaimed " @ %found @ " binding(s) left in "
			@ "config/client/config.cs by an earlier version. Your keys are back to "
			@ "what they were; MonsterRPG borrows them only while you are on the server.");

	return %found;
}

//$MRPG_RemapsRegistered starts unknown, and an older build's rows may be sitting
//in $Remap* right now - so claim "registered" before the reclaim so the unregister
//inside it actually runs.
$MRPG_RemapsRegistered = 1;
MRPG_reclaimSavedBinds();


//////////////////////////////////////////////////////////////////////////////
// THE SAVE HOOK
//
// optionsDlg::onSleep runs moveMap.save("config/client/config.cs")
// (allClientScripts.cs:3001). Hand everything back BEFORE that line runs and take
// it again after, and the file the engine writes is the file the player would
// have had without this add-on - whether they opened Options in the main menu, on
// a build server, or in the middle of a MonsterRPG session.
//
// Pass 1 of MRPG_returnKeys reads the LIVE binding, so a key the player has just
// remapped inside this very dialog is captured into their prefs on the way past.
//////////////////////////////////////////////////////////////////////////////
package MRPGKeyBroker
{
	function optionsDlg::onSleep(%this)
	{
		%had = $MRPG_KeysBorrowed;

		MRPG_returnKeys();

		Parent::onSleep(%this);   // <- moveMap.save() lives in here

		//Only re-take them if we were holding them a moment ago. Closing Options in
		//the main menu must not hand this add-on a keyboard it is not entitled to.
		if(%had && MRPG_isActive())
			MRPG_borrowKeys();
	}
};
activatePackage(MRPGKeyBroker);


//////////////////////////////////
///////// FREE MOUSE /////////////
//////////////////////////////////

function MRPG_FreeMouse(%val)
{
	//Off-server this is unreachable - MRPG_returnKeys has given lalt back and
	//nothing is bound to this command. The guard is here for the one case that
	//survives: a stray call from an older config or a console test.
	if(!MRPG_isActive() || !%val)
		return;

	// cursorOn/cursorOff rather than Canvas.cursorOn/Off: the wrappers also call
	// lockMouse, which is what actually releases mouse-look. Going straight to
	// the Canvas gives you a pointer that still spins the camera.
	if(Canvas.isCursorOn())
	{
		$MRPG_FreeMouseOn = 0;
		cursorOff();
		if(isObject($NewChatSO))
			$NewChatSO.update();
		return;
	}

	$MRPG_FreeMouseOn = 1;
	cursorOn();

	// Use the RPG cursor the rest of the UI uses, at the size the player picked.
	// cursorOn() resets it to DefaultCursor unconditionally.
	Canvas.setCursor(MRPG_CursorName());
}

// Put the pointer back after a dialog closes.
//
// GuiCanvas::checkCursor (packaged as CanvasCursor in the stock scripts) walks
// the canvas after every dialog push/pop and turns the cursor OFF unless some
// child lacks noCursor - and PlayGui and NewChatHud both set it. So popping the
// chat name menu, or the player card, silently cancels free mouse and the player
// is back in mouse-look with no warning.
//
// Scheduled rather than called straight: popDialog triggers checkCursor itself,
// and re-asserting inside that same frame just gets overwritten.
function MRPG_RestoreCursor()
{
	if($MRPG_FreeMouseOn)
		schedule(0, 0, MRPG_ReassertCursor);
}

function MRPG_ReassertCursor()
{
	//MRPG_ClientLeave clears $MRPG_FreeMouseOn, so this is belt to that's braces:
	//a re-assert that survived the disconnect would turn a cursor on in the main
	//menu, or on the next server, every time a dialog closed.
	if(!MRPG_isActive())
		return;

	if(!$MRPG_FreeMouseOn || Canvas.isCursorOn())
		return;

	cursorOn();
	Canvas.setCursor(MRPG_CursorName());
}

// Mirrors the sizing switch in Package.cs so the pointer matches every other
// place the RPG turns a cursor on.
function MRPG_CursorName()
{
	%h = getWord(getRes(), 1);
	if(%h >= 1800)
		return "MonsterRPGCursor4k";
	if(%h <= 900)
		return "MonsterRPGCursorSmall";
	return "MonsterRPGCursorStandard";
}


//////////////////////////////////
///////////// MUSIC //////////////
//////////////////////////////////
//
//Music lives entirely in the browser, so there is nothing to toggle client-side
//- the server decides whether to send a track at all. One serverCmd, and the
//server answers in chat.
function MRPG_toggleMusic(%val)
{
	//Key-DOWN only: without the edge test the toggle fires twice per press and
	//lands back where it started, which reads as the key doing nothing.
	if(!MRPG_gateKey(%val))
		return;

	commandToServer('MusicToggle');
}


//////////////////////////////////////////////////////////////////////////////
// /mrpgkeys - what are we holding right now?
//
// The whole point of this file is that the answer should be "nothing" whenever
// the player is not on a MonsterRPG server, and that is a claim worth being able
// to check rather than assume.
//////////////////////////////////////////////////////////////////////////////
function MRPG_dumpKeys()
{
	echo("MonsterRPG keys: borrowed=" @ ($MRPG_KeysBorrowed ? "yes" : "no")
		@ "  rows=" @ ($MRPG_RemapsRegistered ? "in Options" : "removed")
		@ "  superShift parked=" @ ($MRPG_SuperShiftParked ? "ralt" : "no"));

	for(%i = 0; %i < $MRPG_Key_N; %i++)
	{
		%cmd = $MRPG_Key_Cmd[%i];
		%live = MRPG_keyOfBinding(moveMap.getBinding(%cmd));
		echo("  " @ %cmd
			@ "  live=" @ (%live $= "" ? "-" : %live)
			@ "  owed=" @ ($MRPG_Held_Key[%cmd] $= "" ? "-"
			                : $MRPG_Held_Key[%cmd] @ "->" @ ($MRPG_Held_Prev[%cmd] $= "" ? "(free)" : $MRPG_Held_Prev[%cmd]))
			@ "  pref=" @ ($Pref::Client::MRPG::Key[%cmd] $= "" ? "-" : $Pref::Client::MRPG::Key[%cmd]));
	}
}
