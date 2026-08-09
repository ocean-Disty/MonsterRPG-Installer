//////////////////////////////////////////////////////////////////////////////
// Incantation.cs  -  client side of the spell incantation QTE
//////////////////////////////////////////////////////////////////////////////
//
// TIER 1 (most spells): a rune box shows one big letter. It MATERIALISES in, you
//   press it (q w e a s d z x c), and it BREAKS AWAY (swells + fades to ash) as the
//   next one forms. Miss/time out -> it goes red and shatters.
// TIER 2 (Dance of Swords): the chant sits centre-screen; you TYPE it one physical
//   key at a time (paste-proof) and each spoken letter BURNS to ember behind the
//   flame-gold cursor. Finish before the bar empties = >=70 wpm; beat 90 for a refund.
//
// No screen flashes - the "power" comes from the letters forming and dissolving. The
// server drives both tiers and is the only thing that fires a spell.
//
// Test without the server:  MRPG_incantTest();  (keys)   MRPG_incantTestType();  (chant)

$Inc_Active = 0;
$Inc_Built  = 0;
$Inc_Tier   = 1;
$Inc_CDSch  = "";
$Inc_SetSch = "";
$Inc_Debug  = 1;
$Inc_ShakeSeq = "14 -11 8 -6 4 -2 0";
$Inc_TransMs  = 72;

// letter animation ramps (colour + font, dark->light to form; light->ash to dissolve)
$Inc_FormCol = "39332C 8A8175 EDE7D6";   $Inc_FormFnt = "46 62 74";
$Inc_BreakCol = "F2FFE8 7EA870 3A4838";  $Inc_BreakFnt = "78 92 104";
$Inc_ChantFade = "FFE9A0 C89A50 6A4A2A 33261A";

function Inc_dbg(%m) { if($Inc_Debug) echo("[Incant] " @ %m); }
function MRPG_incNoop(%v) { }   // swallow a key (blocks jump/crouch during the QTE)


//////////////////////////////////
//////// INPUT - TIER 1 //////////
//////////////////////////////////

function MRPG_incantEnsureMap()
{
	if(isObject(MRPG_IncantMap))
		return;
	%m = new ActionMap(MRPG_IncantMap);
	%m.bind(keyboard, "q", MRPG_incK_q);   %m.bind(keyboard, "w", MRPG_incK_w);
	%m.bind(keyboard, "e", MRPG_incK_e);   %m.bind(keyboard, "a", MRPG_incK_a);
	%m.bind(keyboard, "s", MRPG_incK_s);   %m.bind(keyboard, "d", MRPG_incK_d);
	%m.bind(keyboard, "z", MRPG_incK_z);   %m.bind(keyboard, "x", MRPG_incK_x);
	%m.bind(keyboard, "c", MRPG_incK_c);
	%m.bind(keyboard, "space", MRPG_incNoop);      // no jumping mid-incantation
	%m.bind(keyboard, "lcontrol", MRPG_incNoop);   // no crouching
	%m.bind(keyboard, "rcontrol", MRPG_incNoop);
}
function MRPG_incK_q(%v){ if(%v) MRPG_incantPress("q"); }
function MRPG_incK_w(%v){ if(%v) MRPG_incantPress("w"); }
function MRPG_incK_e(%v){ if(%v) MRPG_incantPress("e"); }
function MRPG_incK_a(%v){ if(%v) MRPG_incantPress("a"); }
function MRPG_incK_s(%v){ if(%v) MRPG_incantPress("s"); }
function MRPG_incK_d(%v){ if(%v) MRPG_incantPress("d"); }
function MRPG_incK_z(%v){ if(%v) MRPG_incantPress("z"); }
function MRPG_incK_x(%v){ if(%v) MRPG_incantPress("x"); }
function MRPG_incK_c(%v){ if(%v) MRPG_incantPress("c"); }
function MRPG_incantPress(%key)
{
	if(!$Inc_Active || $Inc_Tier != 1)
		return;
	commandToServer('MRPG_IncantKey', $Inc_Token, %key);
}


//////////////////////////////////
//////// INPUT - TIER 2 //////////
//////////////////////////////////

function MRPG_incTypeEnsureMap()
{
	if(isObject(MRPG_IncTypeMap))
		return;
	%m = new ActionMap(MRPG_IncTypeMap);
	%m.bind(keyboard, "a", MRPG_incT_a); %m.bind(keyboard, "b", MRPG_incT_b); %m.bind(keyboard, "c", MRPG_incT_c);
	%m.bind(keyboard, "d", MRPG_incT_d); %m.bind(keyboard, "e", MRPG_incT_e); %m.bind(keyboard, "f", MRPG_incT_f);
	%m.bind(keyboard, "g", MRPG_incT_g); %m.bind(keyboard, "h", MRPG_incT_h); %m.bind(keyboard, "i", MRPG_incT_i);
	%m.bind(keyboard, "j", MRPG_incT_j); %m.bind(keyboard, "k", MRPG_incT_k); %m.bind(keyboard, "l", MRPG_incT_l);
	%m.bind(keyboard, "m", MRPG_incT_m); %m.bind(keyboard, "n", MRPG_incT_n); %m.bind(keyboard, "o", MRPG_incT_o);
	%m.bind(keyboard, "p", MRPG_incT_p); %m.bind(keyboard, "q", MRPG_incT_q); %m.bind(keyboard, "r", MRPG_incT_r);
	%m.bind(keyboard, "s", MRPG_incT_s); %m.bind(keyboard, "t", MRPG_incT_t); %m.bind(keyboard, "u", MRPG_incT_u);
	%m.bind(keyboard, "v", MRPG_incT_v); %m.bind(keyboard, "w", MRPG_incT_w); %m.bind(keyboard, "x", MRPG_incT_x);
	%m.bind(keyboard, "y", MRPG_incT_y); %m.bind(keyboard, "z", MRPG_incT_z); %m.bind(keyboard, "space", MRPG_incT_space);
	%m.bind(keyboard, "lcontrol", MRPG_incNoop);   // no crouching while typing
	%m.bind(keyboard, "rcontrol", MRPG_incNoop);
}
function MRPG_incT_a(%v){ if(%v) MRPG_incType("a"); }  function MRPG_incT_b(%v){ if(%v) MRPG_incType("b"); }
function MRPG_incT_c(%v){ if(%v) MRPG_incType("c"); }  function MRPG_incT_d(%v){ if(%v) MRPG_incType("d"); }
function MRPG_incT_e(%v){ if(%v) MRPG_incType("e"); }  function MRPG_incT_f(%v){ if(%v) MRPG_incType("f"); }
function MRPG_incT_g(%v){ if(%v) MRPG_incType("g"); }  function MRPG_incT_h(%v){ if(%v) MRPG_incType("h"); }
function MRPG_incT_i(%v){ if(%v) MRPG_incType("i"); }  function MRPG_incT_j(%v){ if(%v) MRPG_incType("j"); }
function MRPG_incT_k(%v){ if(%v) MRPG_incType("k"); }  function MRPG_incT_l(%v){ if(%v) MRPG_incType("l"); }
function MRPG_incT_m(%v){ if(%v) MRPG_incType("m"); }  function MRPG_incT_n(%v){ if(%v) MRPG_incType("n"); }
function MRPG_incT_o(%v){ if(%v) MRPG_incType("o"); }  function MRPG_incT_p(%v){ if(%v) MRPG_incType("p"); }
function MRPG_incT_q(%v){ if(%v) MRPG_incType("q"); }  function MRPG_incT_r(%v){ if(%v) MRPG_incType("r"); }
function MRPG_incT_s(%v){ if(%v) MRPG_incType("s"); }  function MRPG_incT_t(%v){ if(%v) MRPG_incType("t"); }
function MRPG_incT_u(%v){ if(%v) MRPG_incType("u"); }  function MRPG_incT_v(%v){ if(%v) MRPG_incType("v"); }
function MRPG_incT_w(%v){ if(%v) MRPG_incType("w"); }  function MRPG_incT_x(%v){ if(%v) MRPG_incType("x"); }
function MRPG_incT_y(%v){ if(%v) MRPG_incType("y"); }  function MRPG_incT_z(%v){ if(%v) MRPG_incType("z"); }
function MRPG_incT_space(%v){ if(%v) MRPG_incType("space"); }
function MRPG_incType(%char)
{
	if(!$Inc_Active || $Inc_Tier != 2)
		return;
	commandToServer('MRPG_IncantType', $Inc_Token, %char);
}


//////////////////////////////////
////////// BUILD OVERLAY /////////
//////////////////////////////////

function MRPG_incantBuild()
{
	if($Inc_Built && isObject(MRPG_IncBox))
		return;
	if(!isObject(PlayGui))
	{
		Inc_dbg("BUILD FAILED - PlayGui missing");
		return;
	}

	%nm = new GuiMLTextCtrl(MRPG_IncName)
	{
		profile = "GuiMLTextProfile"; horizSizing = "center"; vertSizing = "center";
		position = "0 0"; extent = "600 24"; minExtent = "8 2"; lineSpacing = "1";
		allowColorChars = "1"; maxChars = "-1"; selectable = "0"; autoResize = "0"; visible = "0";
	};
	PlayGui.add(%nm);

	%fr = new GuiSwatchCtrl(MRPG_IncFrame)
	{
		profile = "GuiDefaultProfile"; horizSizing = "center"; vertSizing = "center";
		position = "0 0"; extent = "142 142"; minExtent = "1 1"; color = "120 96 48 200"; visible = "0";
	};
	PlayGui.add(%fr);

	%box = new GuiSwatchCtrl(MRPG_IncBox)
	{
		profile = "GuiDefaultProfile"; horizSizing = "center"; vertSizing = "center";
		position = "0 0"; extent = "130 130"; minExtent = "8 2"; color = "20 17 24 235"; visible = "0";
	};
	PlayGui.add(%box);

	%lt = new GuiMLTextCtrl(MRPG_IncLetter)
	{
		profile = "GuiMLTextProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 22"; extent = "130 104"; minExtent = "8 2"; lineSpacing = "1";
		allowColorChars = "1"; maxChars = "-1"; selectable = "0"; autoResize = "0"; visible = "1";
	};
	%box.add(%lt);

	%ch = new GuiMLTextCtrl(MRPG_IncChant)
	{
		profile = "GuiMLTextProfile"; horizSizing = "center"; vertSizing = "center";
		position = "0 0"; extent = "720 130"; minExtent = "8 2"; lineSpacing = "6";
		allowColorChars = "1"; maxChars = "-1"; selectable = "0"; autoResize = "0"; visible = "0";
	};
	PlayGui.add(%ch);

	%bbg = new GuiSwatchCtrl(MRPG_IncBarBg)
	{
		profile = "GuiDefaultProfile"; horizSizing = "center"; vertSizing = "center";
		position = "0 0"; extent = "220 4"; minExtent = "1 1"; color = "0 0 0 110"; visible = "0";
	};
	PlayGui.add(%bbg);
	%bar = new GuiSwatchCtrl(MRPG_IncBar)
	{
		profile = "GuiDefaultProfile"; horizSizing = "center"; vertSizing = "center";
		position = "0 0"; extent = "220 4"; minExtent = "1 1"; color = "150 118 66 200"; visible = "0";
	};
	PlayGui.add(%bar);

	$Inc_Built = 1;
	Inc_dbg("overlay built on PlayGui");
}

function MRPG_incantSetVis(%v)
{
	if(isObject(MRPG_IncName))  MRPG_IncName.setVisible(%v);
	if(isObject(MRPG_IncFrame)) MRPG_IncFrame.setVisible(%v);
	if(isObject(MRPG_IncBox))   MRPG_IncBox.setVisible(%v);
	if(isObject(MRPG_IncChant)) MRPG_IncChant.setVisible(%v);
	if(isObject(MRPG_IncBarBg)) MRPG_IncBarBg.setVisible(%v);
	if(isObject(MRPG_IncBar))   MRPG_IncBar.setVisible(%v);
}


//////////////////////////////////
///////// TIER 1 (KEYS) //////////
//////////////////////////////////

function MRPG_incantLayout()
{
	%e  = Canvas.getExtent();
	%cx = getWord(%e, 0) / 2;
	%cy = getWord(%e, 1) / 2;

	$Inc_BW = 130;
	$Inc_BX = mFloor(%cx - $Inc_BW / 2);
	$Inc_BY = mFloor(%cy - $Inc_BW / 2 - 10);
	$Inc_BarW = 220;
	$Inc_BarX = mFloor(%cx - $Inc_BarW / 2);
	$Inc_BarY = $Inc_BY + $Inc_BW + 18;

	if(isObject(MRPG_IncName)) MRPG_IncName.resize(%cx - 300, $Inc_BY - 34, 600, 24);
	MRPG_incBoxAt($Inc_BX, $Inc_BW);
	if(isObject(MRPG_IncBarBg)) MRPG_IncBarBg.resize($Inc_BarX, $Inc_BarY, $Inc_BarW, 4);
	if(isObject(MRPG_IncBar))   MRPG_IncBar.resize($Inc_BarX, $Inc_BarY, $Inc_BarW, 4);
}

function MRPG_incBoxAt(%x, %w)
{
	%cx = $Inc_BX + $Inc_BW / 2;
	%bx = mFloor(%cx - %w / 2);
	%by = mFloor($Inc_BY + ($Inc_BW - %w) / 2);
	if(isObject(MRPG_IncBox))   MRPG_IncBox.resize(%bx, %by, %w, %w);
	if(isObject(MRPG_IncFrame)) MRPG_IncFrame.resize(%bx - 6, %by - 6, %w + 12, %w + 12);
}

function MRPG_incSetLetter(%letter, %color, %font)
{
	if(isObject(MRPG_IncLetter))
		MRPG_IncLetter.setText("<just:center><font:verdana bold:" @ %font @ "><color:" @ %color @ ">" @ strupr(%letter));
}

function MRPG_incantShow(%spell, %window, %token, %seq)
{
	Inc_dbg("show(keys) spell=" @ %spell @ " seq=[" @ %seq @ "]");
	MRPG_incantEnsureMap();
	MRPG_IncantMap.push();

	$Inc_Active = 1;  $Inc_Tier = 1;
	$Inc_Spell = %spell;  $Inc_Window = %window;  $Inc_Token = %token;  $Inc_Seq = %seq;  $Inc_Pos = 0;

	MRPG_incantBuild();
	MRPG_incantLayout();
	MRPG_incantSetVis(1);
	if(isObject(MRPG_IncChant)) MRPG_IncChant.setVisible(0);

	if(isObject(MRPG_IncName))
		MRPG_IncName.setText("<just:center><font:verdana bold:16><color:F1ECC2>" @ %spell);
	MRPG_incLetterShow(getWord(%seq, 0));

	$Inc_CDStart = getSimTime();
	MRPG_incantCDTick();
}

// letter MATERIALISES in (dark+small -> light+full)
function MRPG_incLetterShow(%letter)
{
	if(!isObject(MRPG_IncBox))
		return;
	$Inc_Cur = %letter;
	if(isObject(MRPG_IncFrame)) MRPG_IncFrame.color = "150 118 60 220";
	MRPG_IncBox.color = "20 17 24 235";
	MRPG_incBoxAt($Inc_BX, $Inc_BW);
	cancel($Inc_SetSch);
	MRPG_incForm(0);
}
function MRPG_incForm(%i)
{
	if(!isObject(MRPG_IncLetter))
		return;
	if(%i >= getWordCount($Inc_FormCol))
		return;
	MRPG_incSetLetter($Inc_Cur, getWord($Inc_FormCol, %i), getWord($Inc_FormFnt, %i));
	$Inc_SetSch = schedule(24, 0, "MRPG_incForm", %i + 1);
}

// correct press -> the letter BREAKS AWAY (swells + fades to ash)
function MRPG_incBreakAway(%i, %letter)
{
	if(!isObject(MRPG_IncBox))
		return;
	if(%i == 0 && isObject(MRPG_IncFrame)) MRPG_IncFrame.color = "150 220 150 235";
	if(%i >= getWordCount($Inc_BreakCol))
		return;
	MRPG_incSetLetter(%letter, getWord($Inc_BreakCol, %i), getWord($Inc_BreakFnt, %i));
	schedule(23, 0, "MRPG_incBreakAway", %i + 1, %letter);
}

// miss -> red shatter
function MRPG_incShatter(%letter)
{
	if(!isObject(MRPG_IncBox)) return;
	cancel($Inc_SetSch);
	if(isObject(MRPG_IncFrame)) MRPG_IncFrame.color = "220 60 60 235";
	MRPG_IncBox.color = "80 24 24 238";
	MRPG_incSetLetter(%letter, "FF6A6A", 74);
	MRPG_incShake(0);
}
function MRPG_incShake(%i)
{
	if(!isObject(MRPG_IncBox)) return;
	if(%i >= getWordCount($Inc_ShakeSeq)) { MRPG_incantSetVis(0); return; }
	%dx = getWord($Inc_ShakeSeq, %i);
	MRPG_IncBox.resize($Inc_BX + %dx, $Inc_BY, $Inc_BW, $Inc_BW);
	if(isObject(MRPG_IncFrame)) MRPG_IncFrame.resize($Inc_BX - 6 + %dx, $Inc_BY - 6, $Inc_BW + 12, $Inc_BW + 12);
	schedule(26, 0, "MRPG_incShake", %i + 1);
}


//////////////////////////////////
///////// TIER 2 (TYPE) //////////
//////////////////////////////////

function MRPG_incTypeLayout()
{
	%e  = Canvas.getExtent();
	%cx = getWord(%e, 0) / 2;
	%cy = getWord(%e, 1) / 2;

	$Inc_BarW = 420;
	$Inc_BarX = mFloor(%cx - $Inc_BarW / 2);
	$Inc_BarY = mFloor(%cy + 78);

	if(isObject(MRPG_IncName))  MRPG_IncName.resize(%cx - 320, %cy - 96, 640, 22);
	if(isObject(MRPG_IncChant)) MRPG_IncChant.resize(%cx - 360, %cy - 58, 720, 130);
	if(isObject(MRPG_IncBarBg)) MRPG_IncBarBg.resize($Inc_BarX, $Inc_BarY, $Inc_BarW, 4);
	if(isObject(MRPG_IncBar))   MRPG_IncBar.resize($Inc_BarX, $Inc_BarY, $Inc_BarW, 4);
}

function MRPG_incTypeShow(%spell, %window, %token, %chant)
{
	Inc_dbg("show(type) spell=" @ %spell);
	MRPG_incTypeEnsureMap();
	MRPG_IncTypeMap.push();

	$Inc_Active = 1;  $Inc_Tier = 2;
	$Inc_Spell = %spell;  $Inc_Window = %window;  $Inc_Token = %token;  $Inc_Chant = %chant;  $Inc_Pos = 0;

	MRPG_incantBuild();
	MRPG_incTypeLayout();
	MRPG_incantSetVis(1);
	if(isObject(MRPG_IncFrame)) MRPG_IncFrame.setVisible(0);
	if(isObject(MRPG_IncBox))   MRPG_IncBox.setVisible(0);

	if(isObject(MRPG_IncName))
		MRPG_IncName.setText("<just:center><font:verdana bold:15><color:F1ECC2>" @ %spell @ " <color:8A8175>- speak the chant (70+ wpm; beat 90 for a mana refund)");
	MRPG_incTypeRender();

	$Inc_CDStart = getSimTime();
	MRPG_incantCDTick();
}

// spoken letters BURN to ember; the current letter is a flame-gold cursor; rest is cold grey
function MRPG_incTypeRender()
{
	if(!isObject(MRPG_IncChant))
		return;
	%chant = $Inc_Chant;
	%len   = strLen(%chant);
	%pos   = $Inc_Pos;
	%done  = getSubStr(%chant, 0, %pos);
	%cur   = getSubStr(%chant, %pos, 1);
	%rest  = getSubStr(%chant, %pos + 1, %len - %pos - 1);
	MRPG_IncChant.setText("<just:center><font:verdana bold:22><color:4A3626>" @ %done @
		"<color:FFCF5A>" @ %cur @ "<color:6E6A60>" @ %rest);
}


//////////////////////////////////
////////// SHARED / END //////////
//////////////////////////////////

function MRPG_incantCDTick()
{
	cancel($Inc_CDSch);
	//Gate, and do NOT reschedule when it is shut - see ServerGate.cs rule 3.
	if(!$Inc_Active || !MRPG_isActive())
		return;
	%frac = 1.0 - ((getSimTime() - $Inc_CDStart) / 1000) / $Inc_Window;
	if(%frac < 0) %frac = 0;
	if(isObject(MRPG_IncBar))
		MRPG_IncBar.resize($Inc_BarX, $Inc_BarY, $Inc_BarW * %frac, 4);
	if(%frac <= 0)
	{
		MRPG_incantFinish("timeout");
		return;
	}
	$Inc_CDSch = schedule(40, 0, "MRPG_incantCDTick");
}

//////////////////////////////////////////////////////////////////////////////
// THE HARD STOP
//
// CALLED FROM MRPG_ClientLeave, AND IT IS NOT OPTIONAL.
//
// The tier-2 QTE PUSHES MRPG_IncTypeMap, an ActionMap that binds every letter key
// so the player can type the chant. A pushed map wins over moveMap, so if the
// connection goes while an incantation is running and nothing pops it, EVERY LETTER
// KEY IS CAPTURED for the rest of the session - on the next server, in the main
// menu, everywhere. No error, no visible cause: the player just cannot use their
// keyboard any more.
//
// This used to be papered over by MRPG_incantCDTick eventually timing out and
// calling MRPG_incantFinish. That tick is gated now (it has to be - it is a 40ms
// self-rescheduling loop), which removes the accident that was doing the cleanup,
// so the cleanup has to be explicit.
//////////////////////////////////////////////////////////////////////////////
function MRPG_incantAbort()
{
	cancel($Inc_CDSch);  $Inc_CDSch  = "";
	cancel($Inc_SetSch); $Inc_SetSch = "";

	//Unconditional, not `if($Inc_Tier == 2)`. push/pop is a stack and the tier is
	//just a global - if it has drifted, popping a map that is not on the stack is
	//harmless while failing to pop one that is costs the player their keyboard.
	if(isObject(MRPG_IncTypeMap))
		MRPG_IncTypeMap.pop();

	$Inc_Active = 0;
	$Inc_Pos    = 0;

	MRPG_incantSetVis(0);
}

function MRPG_incantFinish(%result)
{
	if(!$Inc_Active)
		return;
	Inc_dbg("finish " @ %result @ " (tier " @ $Inc_Tier @ ")");
	$Inc_Active = 0;
	cancel($Inc_CDSch);
	cancel($Inc_SetSch);

	if($Inc_Tier == 2)
	{
		if(isObject(MRPG_IncTypeMap)) MRPG_IncTypeMap.pop();
		if(%result $= "success")
		{
			if(isObject(MRPG_IncChant))
				MRPG_IncChant.setText("<just:center><font:verdana bold:27><color:FFE9A0>" @ $Inc_Chant);
			schedule(110, 0, "MRPG_incChantFade", 0);   // the whole chant flares gold then burns away
			schedule(560, 0, "MRPG_incantSetVis", 0);
		}
		else
		{
			if(isObject(MRPG_IncChant))
				MRPG_IncChant.setText("<just:center><font:verdana bold:22><color:FF6A6A>" @ $Inc_Chant);
			schedule(560, 0, "MRPG_incantSetVis", 0);
		}
		return;
	}

	if(isObject(MRPG_IncantMap)) MRPG_IncantMap.pop();
	if(%result $= "success")
	{
		MRPG_incBreakAway(0, getWord($Inc_Seq, getWordCount($Inc_Seq) - 1));   // last letter dissolves
		schedule(300, 0, "MRPG_incantSetVis", 0);
	}
	else
		MRPG_incShatter(getWord($Inc_Seq, $Inc_Pos));
}

// tier-2 success: the completed chant fades from gold down to ash
function MRPG_incChantFade(%i)
{
	if(!isObject(MRPG_IncChant))
		return;
	if(%i >= getWordCount($Inc_ChantFade))
		return;
	MRPG_IncChant.setText("<just:center><font:verdana bold:27><color:" @ getWord($Inc_ChantFade, %i) @ ">" @ $Inc_Chant);
	schedule(70, 0, "MRPG_incChantFade", %i + 1);
}


//////////////////////////////////
///////// SERVER FEED ////////////
//////////////////////////////////

function clientCmdMRPG_IncantStart(%data)
{
	MRPG_incantShow(getField(%data, 0), getField(%data, 1), getField(%data, 2), getField(%data, 3));
}
function clientCmdMRPG_IncantProgress(%pos)
{
	if(!$Inc_Active || $Inc_Tier != 1) return;
	cancel($Inc_SetSch);
	MRPG_incBreakAway(0, getWord($Inc_Seq, %pos - 1));       // dissolve the letter you hit
	$Inc_Pos = %pos;
	schedule($Inc_TransMs, 0, "MRPG_incLetterShow", getWord($Inc_Seq, %pos));   // then form the next
}

function clientCmdMRPG_IncantStartType(%data)
{
	MRPG_incTypeShow(getField(%data, 0), getField(%data, 1), getField(%data, 2), getField(%data, 3));
}
function clientCmdMRPG_IncantTypeProgress(%pos)
{
	if(!$Inc_Active || $Inc_Tier != 2) return;
	$Inc_Pos = %pos;
	MRPG_incTypeRender();
}

function clientCmdMRPG_IncantEnd(%result)
{
	MRPG_incantFinish(%result);
}


//////////////////////////////////
///////// LOCAL SELF-TEST ////////
//////////////////////////////////
function MRPG_incantTest()
{
	MRPG_incantShow("Test Spell", 5, 424242, "q w e a s");
	echo("[Incant] TEST(keys) - press q w e a s.");
}
function MRPG_incantTestType()
{
	MRPG_incTypeShow("Dance of Swords", 15, 424242,
		"I call the splitting of skies, the man who wields cold steel, to desolate my enemies before me. Dance of swords!");
	echo("[Incant] TEST(type) - type the chant.");
}
