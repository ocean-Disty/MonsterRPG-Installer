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

	if(MRPGAudio_Connect(%ip, %port, %key, %blid, %manifestVer))
	{
		$MRPG::AudioNative::Linked = 1;
		if(isFunction("MRPGAudio_Log"))
			MRPGAudio_Log("connected to" SPC %ip @ ":" @ %port SPC "as blid" SPC %blid);

		//AFTER the link is up, not before: the DLL joins these paths against the
		//id -> name table the server is about to send, and there is no ordering
		//requirement between the two halves - whichever arrives second completes
		//the pair.
		MRPGAudioNative_MapProfiles();
		MRPGAudioNative_PushListener();
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

	%n = 0;
	%group = DataBlockGroup;
	if(!isObject(%group))
		return 0;

	for(%i = 0; %i < %group.getCount(); %i++)
	{
		%db = %group.getObject(%i);
		if(!isObject(%db))
			continue;
		if(%db.getClassName() !$= "AudioProfile")
			continue;

		//fileName is the AudioProfile's own field and is already relative to the
		//Blockland folder, which is this process's working directory - so the DLL
		//can open it as-is.
		%file = %db.fileName;
		if(%file $= "")
			continue;

		if(MRPGAudio_MapProfile(%db.getName(), %file))
			%n++;
	}

	if(isFunction("MRPGAudio_Log"))
		MRPGAudio_Log("mapped" SPC %n SPC "audio profiles to files");
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

	%control = ServerConnection.getControlObject();
	if(isObject(%control))
	{
		%eye = %control.getEyePoint();
		%vec = %control.getEyeVector();
		if(%eye !$= "" && %vec !$= "")
			MRPGAudio_Listener(getWord(%eye, 0), getWord(%eye, 1), getWord(%eye, 2),
			                   getWord(%vec, 0), getWord(%vec, 1), getWord(%vec, 2));
	}

	$MRPG::AudioNative::PoseSch = schedule(50, 0, MRPGAudioNative_PushListener);
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
	$MRPG::AudioNative::PoseSch = "";

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
			@ "  sounds loaded: " @ getWord(%a, 3)
			@ (getWord(%a, 4) > 0 ? " (+" @ getWord(%a, 4) @ " loading)" : "")
			@ "  voices: " @ getWord(%a, 2));
		echo("         played " @ getWord(%a, 5)
			@ ", missed " @ getWord(%a, 6)
			@ ", dropped " @ getWord(%a, 7)
			@ ", underruns " @ getWord(%a, 8));
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
