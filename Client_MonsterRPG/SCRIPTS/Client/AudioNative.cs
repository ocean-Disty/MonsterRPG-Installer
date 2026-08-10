//////////////////////////////////////////////////////////////////////////////
// AudioNative.cs  -  answering the server's invitation to play audio in-game
//
// The server ray-traces every sound against the real geometry of the world. Up
// to now the only way to hear that was to keep a browser tab open. A player
// running MonsterRPGAudio.dll hears it in the game itself instead - lower
// latency, no second window, and panned against their live camera rather than a
// 20 Hz pose, so sounds stop swimming when they turn their head.
//
// THIS FILE IS ALMOST ENTIRELY A NO-OP. The DLL is what does the work, and the
// DLL is not shipped with this add-on - it cannot be, because Blockland has no
// way to send a client an executable. A player without it simply never has
// MRPGAudio_Connect defined, every function here returns immediately, and they
// hear stock audio exactly as before.
//
// ── THE BORROWED-KEY RULE APPLIES HERE TOO ───────────────────────────────────
//
// This add-on loads on EVERY server, including ones that have never heard of
// MonsterRPG. So nothing starts at file scope, no socket is opened until a
// server asks, and everything is handed back on the way out - see
// MRPGAudioNative_Shutdown, which MRPG_ClientLeave calls.
//////////////////////////////////////////////////////////////////////////////

// ---------------------------------------------------------------------------
// Is the DLL there at all?
//
// A Phase 1 build of MonsterRPGAudio defines _Version but NOT _Connect, on
// purpose: script has to be able to tell a DLL that can talk from one that can
// also carry audio, and the presence of a name is the only signal it gets. So
// this tests the function it is actually about to call.
// ---------------------------------------------------------------------------
function MRPGAudioNative_Present()
{
	return isFunction("MRPGAudio_Connect") && isFunction("MRPGAudio_Release");
}

// ---------------------------------------------------------------------------
// clientCmdMRPGAudioInvite(%port, %key, %manifestVer)
//
// The server tells us a UDP port and a session key. It does NOT tell us its
// address, and that is deliberate: it does not reliably know its own. This
// server answers on a public IP and also on a LAN address, and one that
// announced "connect to me at 192.168.1.107" would work at home and nowhere
// else.
//
// WE ALREADY KNOW THE ADDRESS THAT WORKS - it is the one we used to reach the
// game. So we take it from our own connection.
//
// PARSE IT ON THE COLON. ServerConnection.getAddress() returns "1.2.3.4:28000"
// with NO "IP:" prefix, so getWord(%addr, 1) hands you the PORT, not the
// address. That exact mistake has already shipped once in this project and
// silently mislabelled every LAN player.
//
// THE KEY IS THE SECURITY OF THE WHOLE LINK. It arrives over the game's own
// connection, which is what proves it came from the server we are actually
// playing on. Never echo it, never log it, never put it in a variable that
// outlives the call.
// ---------------------------------------------------------------------------
function clientCmdMRPGAudioInvite(%port, %key, %manifestVer, %blid)
{
	if(!MRPGAudioNative_Present())
		return;

	if(%port <= 0 || %key $= "")
		return;

	if(!isObject(ServerConnection))
		return;

	%addr = ServerConnection.getAddress();
	if(%addr $= "")
		return;

	//strpos, and NOT getWord. getAddress() returns "1.2.3.4:28000" with no "IP:"
	//prefix, so getWord(%addr, 1) hands you the PORT - a mistake that has already
	//shipped once in this project and silently mislabelled every LAN player.
	//
	//(strrpos does not exist in this engine. Checked against Blockland.exe rather
	//than assumed, after writing it once and finding out.)
	%colon = strpos(%addr, ":");
	if(%colon < 0)
		%ip = %addr;
	else
		%ip = getSubStr(%addr, 0, %colon);

	//"local" is what a listen server reports when you host it yourself. The
	//loopback address is the one that reaches it.
	if(%ip $= "" || %ip $= "local")
		%ip = "127.0.0.1";

	//THE SERVER TELLS US OUR OWN BL_ID rather than us asking the engine for it.
	//It already knows - it is the id it minted the session key against - and the
	//server's answer is the one that must match, because it is what the DLL looks
	//the key up by. Asking locally would introduce a second source for a value
	//that has exactly one correct answer.
	if(%blid <= 0)
		return;

	//Kept so a failed candidate can be retried against another address without a
	//new invite. The KEY is held only for the length of this attempt sequence and
	//is cleared by MRPGAudioNative_Shutdown.
	$MRPG::AudioNative::Port     = %port;
	$MRPG::AudioNative::Key      = %key;
	$MRPG::AudioNative::Blid     = %blid;
	$MRPG::AudioNative::Manifest = %manifestVer;
	$MRPG::AudioNative::Cand     = 0;
	$MRPG::AudioNative::CandIp   = %ip;

	if(MRPGAudio_Connect(%ip, %port, %key, %blid, %manifestVer))
	{
		$MRPG::AudioNative::Linked = 1;
		if(isFunction("MRPGAudio_Log"))
			MRPGAudio_Log("connected to" SPC %ip @ ":" @ %port SPC "as blid" SPC %blid);

		//AFTER the link is up, not before: the DLL joins these paths against the
		//id -> name table the server is about to send, and there is no ordering
		//requirement between the two halves - whichever arrives second completes
		//the pair.
		$MRPG::AudioNative::PoseOK    = 0;
		$MRPG::AudioNative::PoseTries = 0;
		MRPGAudioNative_MapProfiles();
		MRPGAudioNative_PushListener();
		if(isFunction("MRPGVoiceIcon_Start"))
			MRPGVoiceIcon_Start();

		//SCHEDULED, not called. Calling it here would test the link before a
		//WELCOME could physically have arrived and fail over instantly on a
		//perfectly good address. Six seconds is comfortably longer than a
		//round trip and shorter than a player's patience.
		cancel($MRPG::AudioNative::LinkSch);
		$MRPG::AudioNative::LinkSch = schedule(6000, 0, MRPGAudioNative_CheckLink);
	}
	else
	{
		$MRPG::AudioNative::Linked = 0;
		//Not a warning to the player. A failure here means they hear stock audio,
		//which is what they were going to hear anyway if they had never installed
		//the DLL - MonsterRPGAudio.log says why, and that is the right place for it.
	}
}

// ---------------------------------------------------------------------------
// Telling the DLL where our sounds live
//
// The wire carries a 16-bit id; the server tells the DLL what NAME each id
// means; this tells it where that name's FILE is on this machine. The server
// never sends a path, because it has no idea where this computer keeps its
// add-ons - and a client missing an add-on simply has no path for that name and
// skips the sound, rather than being told to open a file that is not there.
//
// Done once per join. ~1000 AudioProfiles is a few milliseconds of native calls,
// against a join that already takes seconds.
// ---------------------------------------------------------------------------
function MRPGAudioNative_MapProfiles()
{
	if(!isFunction("MRPGAudio_MapProfile"))
		return 0;

	//getDataBlockGroupSize() / getDataBlock(), NOT the DataBlockGroup SimGroup.
	//
	//The first version walked DataBlockGroup.getObject() and mapped exactly ZERO
	//profiles on a live client. That object is the SERVER's group; a client
	//receives its datablocks by ghosting and reaches them only through these two
	//engine calls, which is what every stock client script uses.
	%count = getDataBlockGroupSize();

	%n = 0;
	%audio = 0;
	%noFile = 0;

	for(%i = 0; %i < %count; %i++)
	{
		%db = getDataBlock(%i);
		if(!isObject(%db))
			continue;
		if(%db.getClassName() !$= "AudioProfile")
			continue;

		%audio++;

		//fileName is relative to the Blockland folder, which is this process's
		//working directory, so the DLL can open it as-is.
		%file = %db.fileName;
		if(%file $= "")
		{
			%noFile++;
			continue;
		}

		if(MRPGAudio_MapProfile(%db.getName(), %file))
			%n++;
	}

	//EVERY NUMBER, not just the answer. A bare "mapped 0" says nothing about
	//WHICH of the three ways this can fail actually happened: no datablocks at
	//all, none of them audio, or audio profiles whose filename did not survive
	//the trip to the client. Each needs a different fix.
	if(isFunction("MRPGAudio_Log"))
		MRPGAudio_Log("profiles: " @ %count @ " datablocks, " @ %audio @ " audio, "
			@ %noFile @ " no filename, " @ %n @ " mapped");

	return %n;
}


// ---------------------------------------------------------------------------
// The listener, pushed at 20 Hz
//
// PHASE 4 DELETES THIS. The whole point of native audio is that the client pans
// against its own live camera at audio rate; a script push is still a sampled
// pose and still lags a turn by up to 50 ms. It exists so Phase 3 can prove a
// sound arrives from roughly the right direction before Phase 4 proves it
// arrives from exactly the right one.
//
// EYE POINT, NOT getPosition(). getPosition() on a Player is the FEET, and a
// listener at floor level panned from there is subtly wrong for everything
// nearby. Core_VoiceChat.cs learned this server-side.
// ---------------------------------------------------------------------------
function MRPGAudioNative_PushListener()
{
	cancel($MRPG::AudioNative::PoseSch);

	if(!$MRPG::AudioNative::Linked || !isFunction("MRPGAudio_Listener"))
		return;

	$MRPG::AudioNative::PoseTries++;

	//MRPG_GetCameraTransform() rather than getControlObject().getEyePoint().
	//
	//That helper already exists in Support.cs, is used by NamePlates, and walks a
	//fallback chain this file would otherwise have to duplicate: the connection's
	//own camera first (third-person offset already baked in), then the control
	//object's eye transform, then its plain transform. It is also the CAMERA,
	//which is what a listener actually is.
	//
	//The first attempt happens at invite time, when the client reliably has NO
	//control object yet - measured: "listener push EMPTY: conn=1 control=none".
	//So this retries every 50 ms and simply starts working once the player
	//spawns; the point is that it must not go quiet about failing meanwhile.
	%xform = "";
	if(isFunction("MRPG_GetCameraTransform"))
		%xform = MRPG_GetCameraTransform();

	if(%xform $= "" && isObject(ServerConnection))
	{
		%obj = ServerConnection.getControlObject();
		if(isObject(%obj))
			%xform = %obj.getEyeTransform();
	}

	if(%xform !$= "")
	{
		%pos = getWords(%xform, 0, 2);
		%fwd = MatrixMulVector(%xform, "0 1 0");

		MRPGAudio_Listener(getWord(%pos, 0), getWord(%pos, 1), getWord(%pos, 2),
		                   getWord(%fwd, 0), getWord(%fwd, 1), getWord(%fwd, 2));

		if(!$MRPG::AudioNative::PoseOK && isFunction("MRPGAudio_Log"))
			MRPGAudio_Log("listener push OK after " @ $MRPG::AudioNative::PoseTries
				@ " tries: pos=" @ %pos @ " fwd=" @ %fwd);
		$MRPG::AudioNative::PoseOK++;
	}
	else
	{
		//Every 100 tries = 5 s, not once. A one-shot message cannot tell a retry
		//loop that is still running from one that died, and that distinction is
		//the whole question when the listener never arrives.
		if(($MRPG::AudioNative::PoseTries % 100) == 1 && isFunction("MRPGAudio_Log"))
			MRPGAudio_Log("listener push still empty after " @ $MRPG::AudioNative::PoseTries
				@ " tries: conn=" @ (isObject(ServerConnection) ? 1 : 0)
				@ " control=" @ (isObject(ServerConnection.getControlObject())
					? ServerConnection.getControlObject().getClassName() : "none"));
	}

	//16 ms, not 50. The mixer now re-projects every voice against the listener
	//once per audio block (~100 Hz at a 10 ms buffer), so the pose feeding it is
	//the limiting factor - a 20 Hz pose would cap head tracking at 20 Hz however
	//fast the mixer runs. One native call per push; the cost is nil.
	$MRPG::AudioNative::PoseSch = schedule(16, 0, MRPGAudioNative_PushListener);
}



// ---------------------------------------------------------------------------
// Address failover
//
// ONE ADDRESS IS NOT ENOUGH, and this is measured rather than defensive.
//
// The client aims the audio link at whatever ServerConnection.getAddress()
// reports, which is right for a remote player and wrong when the server is on
// the SAME MACHINE: joining via the public address makes the client send to its
// own public IP, and the reply has to be hairpinned by the router back to the
// box it came from. The HELLOs arrive - the server's counter climbed past 1400 -
// and not one answer came back. It worked twice and then stopped, which is
// exactly how unreliable hairpin behaves.
//
// So the address is treated as a CANDIDATE. If no WELCOME has arrived a few
// seconds after connecting, the next candidate is tried. 127.0.0.1 is the second
// one because the case that fails is precisely the case where the server is
// local, and loopback reaches a socket bound to 0.0.0.0 without touching the
// router at all.
//
// A remote server simply never needs the fallback: its first candidate answers.
// ---------------------------------------------------------------------------
function MRPGAudioNative_CheckLink()
{
	cancel($MRPG::AudioNative::LinkSch);

	if(!$MRPG::AudioNative::Linked || !isFunction("MRPGAudio_Stat"))
		return;

	//Field 0 of the stat line is "connected", set only once a WELCOME has been
	//authenticated. Anything else means the endpoint never answered.
	if(getWord(MRPGAudio_Stat(), 0) == 1)
	{
		if(isFunction("MRPGAudio_Log"))
			MRPGAudio_Log("link confirmed on candidate " @ $MRPG::AudioNative::Cand
				@ " (" @ $MRPG::AudioNative::CandIp @ ")");
		return;
	}

	%next = "";
	switch($MRPG::AudioNative::Cand)
	{
		//The server is on this machine, so loopback bypasses the router entirely.
		case 0: %next = "127.0.0.1";
	}

	if(%next $= "")
	{
		if(isFunction("MRPGAudio_Log"))
			MRPGAudio_Log("no audio link: no candidate address answered. Sound stays on the engine.");
		return;
	}

	$MRPG::AudioNative::Cand++;
	$MRPG::AudioNative::CandIp = %next;

	if(isFunction("MRPGAudio_Log"))
		MRPGAudio_Log("no answer from the first address; trying " @ %next);

	MRPGAudio_Connect(%next, $MRPG::AudioNative::Port, $MRPG::AudioNative::Key,
	                  $MRPG::AudioNative::Blid, $MRPG::AudioNative::Manifest);

	MRPGAudioNative_MapProfiles();
	$MRPG::AudioNative::LinkSch = schedule(6000, 0, MRPGAudioNative_CheckLink);
}

// ---------------------------------------------------------------------------
// Push to talk
//
// Bound by Keybinds.cs, which is where every key this add-on uses is declared.
// That file already borrows keys on join, returns them to whatever held them on
// leave, publishes a row in Options > Keyboard, and remembers a player's remap
// in $Pref::Client::MRPG::Key[] for the next visit - so none of that had to be
// built here, and a second keybind system would have been a second thing to get
// wrong.
//
// %val is 1 on press and 0 on release; Torque calls a bound command for both.
// ---------------------------------------------------------------------------
function MRPG_VoicePTT(%val)
{
	//BOUNDED DIAGNOSTIC. If these lines never appear in MonsterRPGAudio.log after a
	//player holds the key, the break is upstream of here - the bind - and no amount
	//of looking at the DLL or the icon will find it. Four lines is two presses.
	if($MRPG::Voice::PttSeen < 4)
	{
		$MRPG::Voice::PttSeen++;
		if(isFunction("MRPGAudio_Log"))
			MRPGAudio_Log("script: PTT key " @ (%val ? "down" : "up")
				@ ", VoiceKey=" @ (isFunction("MRPGAudio_VoiceKey") ? "present" : "MISSING"));
	}

	if(isFunction("MRPGAudio_VoiceKey"))
		MRPGAudio_VoiceKey(%val ? 1 : 0);
}

// ---------------------------------------------------------------------------
// Leaving
//
// Called from MRPG_ClientLeave, which is the outermost disconnect wrapper and
// therefore runs before any other module's teardown.
//
// Release sends a goodbye so the server drops our routing flag NOW rather than
// waiting out its three-second expiry - and those three seconds would be three
// seconds of the next server's audio going nowhere.
// ---------------------------------------------------------------------------
function MRPGAudioNative_Shutdown()
{
	$MRPG::AudioNative::Linked = 0;
	cancel($MRPG::AudioNative::PoseSch);
	cancel($MRPG::AudioNative::LinkSch);
	$MRPG::AudioNative::PoseSch = "";
	$MRPG::AudioNative::LinkSch = "";
	//The session key dies with the session.
	$MRPG::AudioNative::Key     = "";

	//Key up before anything else. Leaving while holding the key must not leave
	//the microphone latched open in the DLL.
	if(isFunction("MRPGAudio_VoiceKey"))
		MRPGAudio_VoiceKey(0);

	if(isFunction("MRPGAudio_Release"))
		MRPGAudio_Release("left the server");
}

// ---------------------------------------------------------------------------
// MRPGAudioNative_Status()  -  what the local DLL thinks is going on
//
// A CONSOLE function, not a slash command: type MRPGAudioNative_Status(); into
// the console (the tilde key). Nothing registers a chat command for it, and
// saying "/mrpgaudio" here would promise something that does not exist.
//
// Client side and available to everyone, deliberately. This is what a player
// runs when they ask "is this thing even working?", and putting it behind admin
// would mean the answer always has to come through somebody else.
// ---------------------------------------------------------------------------
function MRPGAudioNative_Status()
{
	if(!isFunction("MRPGAudio_State"))
	{
		echo("MonsterRPGAudio is not loaded. Start the game with MonsterRPGAudio.bat.");
		return;
	}

	// "state gpuVerdict rayQuery policyPass vendorId deviceId adapterName..."
	// The adapter name is LAST because it is the only field that can contain
	// spaces, so it is read with getWords rather than getWord.
	%s = MRPGAudio_State();
	echo("MonsterRPGAudio " @ MRPGAudio_Version() @ "  state: " @ getWord(%s, 0));
	echo("  GPU: " @ getWords(%s, 6) @ "  (" @ getWord(%s, 1) @ ")");
	if(isFunction("MRPGAudio_GpuWhy"))
		echo("       " @ MRPGAudio_GpuWhy());
	echo("  Note: the GPU check does NOT decide whether you get ray-traced audio.");
	echo("        The server traces it. This only affects a future option to do");
	echo("        that work on your own machine.");

	if(isFunction("MRPGAudio_AudioStat"))
	{
		// "running device voices loaded pending played missed dropped underruns"
		%a = MRPGAudio_AudioStat();
		echo("  audio: device " @ (getWord(%a, 1) ? "open" : "CLOSED")
			@ "  sounds loaded: " @ getWord(%a, 3) @ " (" @ getWord(%a, 9) @ " MB)"
			@ (getWord(%a, 4) > 0 ? " (+" @ getWord(%a, 4) @ " loading)" : "")
			@ "  voices: " @ getWord(%a, 2));
		echo("         played " @ getWord(%a, 5)
			@ ", missed " @ getWord(%a, 6)
			@ ", dropped " @ getWord(%a, 7)
			@ ", underruns " @ getWord(%a, 8));

		//BOTH HALVES OF THE BANK, always. id->name comes from the SERVER,
		//name->path from THIS machine, and a sound loads only when both exist.
		//When "sounds loaded" is 0 the only useful question is which half is
		//missing, and one combined number cannot answer it.
		echo("         sound ids from server: " @ getWord(%a, 11)
			@ ", local audio profiles: " @ getWord(%a, 12));
		if(getWord(%a, 11) <= 0)
			echo("         no ids yet - the server has not sent its sound table.");
		if(getWord(%a, 12) <= 0)
			echo("         no local profiles - this client found no AudioProfile datablocks.");
		if(getWord(%a, 10) > 0)
			echo("         " @ getWord(%a, 10) @ " sound(s) skipped: memory budget full.");

		//FIELD MAP (count against the format string in Audio.cpp before editing):
		// 0 running  3 loaded   6 missed    9 bankMB  12 profiles  15 listenerSet
		// 1 device   4 pending  7 dropped  10 skipped 13 culledDist 16 lx
		// 2 voices   5 played   8 underrun 11 ids     14 noListener 17 ly  18 lz
		echo("         listener: " @ (getWord(%a, 15) ? "set (" @ getWord(%a, 16)
			SPC getWord(%a, 17) SPC getWord(%a, 18) @ ")" : "NEVER SET"));
		if(getWord(%a, 13) > 0)
			echo("         " @ getWord(%a, 13) @ " sound(s) culled: further than 150u from the listener.");
		if(getWord(%a, 14) > 0)
			echo("         " @ getWord(%a, 14) @ " sound(s) played without a listener position.");
	}

	if(isFunction("MRPGAudio_VoiceStat"))
	{
		// "enabled capturing talking made taken dropped rate ch ptt openMic"
		%v = MRPGAudio_VoiceStat();
		if(getWord(%v, 0) == 0)
			echo("  voice: off (set Voice=1 in MonsterRPGAudio.cfg to enable)");
		else
			echo("  voice: mic " @ (getWord(%v, 1) ? "open" : "CLOSED")
				@ ", " @ (getWord(%v, 9) ? "open mic" : "push-to-talk")
				@ ", key " @ (getWord(%v, 8) ? "DOWN" : "up")
				@ ", frames sent " @ getWord(%v, 4));
	}

	if(isFunction("MRPGAudio_Stat"))
	{
		// "connected ageMs hellos sfxDgrams sfxRecords mus bad foreign forged"
		%n = MRPGAudio_Stat();
		echo("  link: " @ (getWord(%n, 0) ? "up" : "down")
			@ "  sounds received: " @ getWord(%n, 4)
			@ "  last packet: " @ getWord(%n, 1) @ " ms ago");

		// Zero forever on a healthy link. Anything else means somebody is
		// injecting traffic at this port, or the two sides disagree about the key.
		if(getWord(%n, 8) > 0)
			echo("  \c0" @ getWord(%n, 8) @ " packet(s) FAILED AUTHENTICATION and were dropped.");
	}
}
