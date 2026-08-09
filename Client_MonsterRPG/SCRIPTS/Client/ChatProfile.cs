//////////////////////////////////////////////////////////////////////////////
// ChatProfile.cs  -  click a name in chat: menu, then the player card
//////////////////////////////////////////////////////////////////////////////
//
// ChatPanel.cs wraps every speaker's name in an MLText <a:> link. This file is
// what happens when one is clicked: a small menu at the cursor (View Profile /
// Whisper / Invite to Party / Mute), and the card that "View Profile" opens.
//
//
// THE CURSOR HAS TO BE ON, AND THAT IS THE WHOLE REASON FREE MOUSE EXISTS.
// GuiMLTextCtrl::onMouseDown is the only route to onURL, and no control sees a
// mouse event while the canvas cursor is off. Left Alt (Keybinds.cs) is what
// makes every name in the log reachable.
//
//
// WHAT THE ENGINE GIVES US, AND WHAT IT DOES NOT
//
// Names are drawn in fontColorLink at rest and fontColorLinkHL while the button
// is HELD on them - guiMLTextCtrl.cc drawAtomText branches on atom->url and picks
// between exactly those two, ignoring any <color:> tag around the name.
//
// There is NO hover state for links in this engine. drawAtomText only knows
// url->mouseDown, which onMouseDown sets and onMouseUp clears; onMouseMove is not
// even overridden on GuiMLTextCtrl. Getting a true mouse-over highlight would
// mean knowing each name's pixel rectangle, and the atom geometry is not exposed
// to script at all - so names are given a distinct colour (they read as
// clickable at rest) and they flash white on press.
//
// The MENU is a different matter: this file owns those rectangles, so its rows do
// get a real hover highlight, polled the same way CharacterScreen.cs polls its
// buttons.
//////////////////////////////////////////////////////////////////////////////


$MRPGCM::RowH    = 26;   // design px, scaled by MRPGChat_Px
$MRPGCM::Width   = 172;
$MRPGCM::PadX    = 12;
$MRPGCM::HeadH   = 24;

$MRPGCM_Built  = 0;
$MRPGCM_Open   = 0;
$MRPGCM_Name   = "";
$MRPGCM_Rows   = 0;
$MRPGCM_Hover  = -1;
cancel($MRPGCM_TickSch);
$MRPGCM_TickSch = "";


//////////////////////////////////
////////// LINK CLICK ////////////
//////////////////////////////////

// Fired by the engine on mouse-up over an <a:> link. The URL is whatever sits
// between "<a:" and the next ">", which MRPGChat_NameLink builds as
// "gamelink|p|<player name>" - the gamelink prefix is what suppresses the
// underline, see that function.
function MRPGChatText::onURL(%this, %url)
{
	if(getSubStr(%url, 0, 11) $= "gamelink|p|")
	{
		MRPGCM_Open(getSubStr(%url, 11, 512));
		return;
	}

	// Anything else is a real hyperlink the server put in the message. Hand it to
	// the stock handler so http:// links in chat still work.
	if(isFunction("gotoWebPage"))
		gotoWebPage(%url);
}


//////////////////////////////////
////////// THE MENU //////////////
//////////////////////////////////

function MRPGCM_Build()
{
	if($MRPGCM_Built)
		return;

	// MRPGChat_TextProfile and the shared control helpers live in ChatPanel.cs,
	// and the menu can only be reached through a chat link - but building the
	// panel first makes that an invariant rather than a coincidence.
	MRPGChat_Build();

	// A full-screen dialog, so the menu is above the HUD and dismissable by
	// clicking anywhere off it. noCursor is deliberately NOT set: this dialog
	// wants the pointer, and GuiCanvas::checkCursor turns it on for any canvas
	// child that does not opt out.
	%dlg = new GuiControl(MRPGCM_Dlg)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "width";
		vertSizing  = "height";
		position    = "0 0";
		extent      = getWord(getRes(), 0) SPC getWord(getRes(), 1);
		minExtent   = "8 2";
	};

	%w = MRPGChat_Px($MRPGCM::Width);

	// Same drawn box as the chat panel and the card, so all three surfaces of
	// this feature are one look.
	MRPGChat_box(%dlg, "MRPGCM_Panel", 0, 0, %w, 10,
		$MRPGChat::Col::InputFill, $MRPGChat::Col::Line);

	MRPGChat_text(MRPGCM_Panel, "MRPGCM_Head", MRPGChat_Px($MRPGCM::PadX), MRPGChat_Px(5),
		%w - MRPGChat_Px($MRPGCM::PadX) * 2, MRPGChat_Px(18), 0);

	MRPGChat_swatch(MRPGCM_Panel, "MRPGCM_Rule", MRPGChat_Px(6),
		MRPGChat_Px($MRPGCM::HeadH), %w - MRPGChat_Px(14), 1, $MRPGChat::Col::Line);

	// Rows are allocated once at the maximum and hidden individually - the menu
	// never allocates while it is being opened.
	for(%i = 0; %i < 6; %i++)
	{
		%hl = MRPGChat_swatch(MRPGCM_Panel, "MRPGCM_Hl" @ %i, 2, 0, %w - 4,
			MRPGChat_Px($MRPGCM::RowH), "232 184 92 46");
		%hl.setVisible(0);

		MRPGChat_text(MRPGCM_Panel, "MRPGCM_Row" @ %i, MRPGChat_Px($MRPGCM::PadX), 0,
			%w - MRPGChat_Px($MRPGCM::PadX) * 2, MRPGChat_Px(18), 0);
	}

	// THE CATCHER GOES ON LAST, AND THAT ORDERING IS THE WHOLE THING.
	//
	// GuiControl::findHitControl walks its children LAST TO FIRST and returns the
	// first hit whose profile is modal - and GuiDefaultProfile has modal = 1, so
	// every swatch, bitmap and label in this menu is a modal hit. Add the catcher
	// first and the row swatches sit on top of it, eat every click, and do
	// nothing with them: the menu would draw perfectly and be completely dead.
	//
	// Full-screen on purpose: clicking anywhere off the menu closes it, which is
	// the same call with a row index of -1.
	%catch = new GuiMouseEventCtrl(MRPGCM_Catch)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "width";
		vertSizing  = "height";
		position    = "0 0";
		extent      = getWord(getRes(), 0) SPC getWord(getRes(), 1);
		minExtent   = "8 2";
		lockMouse   = "0";
	};
	%dlg.add(%catch);

	$MRPGCM_Built = 1;
}

function MRPGCM_AddRow(%label, %cmd)
{
	%i = $MRPGCM_Rows;
	if(%i >= 6)
		return;

	$MRPGCM_RowCmd[%i] = %cmd;

	%rowH = MRPGChat_Px($MRPGCM::RowH);
	%y    = MRPGChat_Px($MRPGCM::HeadH) + 1 + %i * %rowH;

	%lbl = "MRPGCM_Row" @ %i;
	%hl  = "MRPGCM_Hl"  @ %i;

	%lbl.resize(MRPGChat_Px($MRPGCM::PadX), %y + mFloor((%rowH - MRPGChat_Px(15)) / 2),
		getWord(%lbl.getExtent(), 0), MRPGChat_Px(18));
	%lbl.setText("<font:verdana:" @ MRPGChat_Px(13) @ "><color:"
		@ $MRPGChat::Col::Name @ ">" @ %label);
	%lbl.setVisible(1);

	%hl.resize(2, %y, getWord(%hl.getExtent(), 0), %rowH);
	%hl.setVisible(0);

	$MRPGCM_Rows++;
}

function MRPGCM_Open(%name)
{
	if(%name $= "")
		return;

	MRPGCM_Build();
	MRPGCM_Close();

	$MRPGCM_Name = %name;
	$MRPGCM_Rows = 0;

	for(%i = 0; %i < 6; %i++)
	{
		("MRPGCM_Row" @ %i).setVisible(0);
		("MRPGCM_Hl"  @ %i).setVisible(0);
	}

	// View Profile is always first - it is the one the menu exists for.
	MRPGCM_AddRow("View Profile", "profile");

	if(%name !$= $Pref::Player::NetName)
	{
		MRPGCM_AddRow("Whisper",         "whisper");
		MRPGCM_AddRow("Invite to Party", "invite");
		MRPGCM_AddRow($MRPGChat_Ignore[%name] ? "Unmute" : "Mute", "mute");
	}

	%w     = MRPGChat_Px($MRPGCM::Width);
	%h     = MRPGChat_Px($MRPGCM::HeadH) + 1 + $MRPGCM_Rows * MRPGChat_Px($MRPGCM::RowH)
	         + MRPGChat_Px(4);

	MRPGCM_Head.setText("<font:verdana bold:" @ MRPGChat_Px(13) @ "><color:"
		@ $MRPGChat::Col::Gold @ ">" @ %name);

	// Open at the cursor, then pull back inside the screen. A menu that opens
	// half off the bottom edge is the usual failure here, and the chat panel this
	// is launched from lives near the bottom-left corner by design.
	%cur = Canvas.getCursorPos();
	%res = getRes();
	%x   = getWord(%cur, 0);
	%y   = getWord(%cur, 1);

	// %w by %h exactly - the box helper draws its border INSIDE those bounds, so
	// there is no bevel overhang to allow for the way the old three-swatch frame
	// needed.
	if(%x + %w > getWord(%res, 0) - 2){ %x = getWord(%res, 0) - %w - 2; }
	if(%y + %h > getWord(%res, 1) - 2){ %y = getWord(%res, 1) - %h - 2; }
	if(%x < 2){ %x = 2; }
	if(%y < 2){ %y = 2; }

	MRPGChat_boxResize("MRPGCM_Panel", %x, %y, %w, %h);
	MRPGCM_Rule.resize(MRPGChat_Px(6), MRPGChat_Px($MRPGCM::HeadH),
		%w - MRPGChat_Px(14), 1);

	Canvas.pushDialog(MRPGCM_Dlg);
	$MRPGCM_Open  = 1;
	$MRPGCM_Hover = -1;
	MRPGCM_Tick();
}

function MRPGCM_Close()
{
	cancel($MRPGCM_TickSch);
	$MRPGCM_TickSch = "";

	if($MRPGCM_Open && isObject(MRPGCM_Dlg))
	{
		Canvas.popDialog(MRPGCM_Dlg);
		MRPG_RestoreCursor();
	}

	$MRPGCM_Open  = 0;
	$MRPGCM_Hover = -1;
}

function MRPGCM_RowAt()
{
	if(!$MRPGCM_Open)
		return -1;

	%cur = Canvas.getCursorPos();
	%cx  = getWord(%cur, 0);
	%cy  = getWord(%cur, 1);

	for(%i = 0; %i < $MRPGCM_Rows; %i++)
	{
		%hl = "MRPGCM_Hl" @ %i;
		%p  = %hl.getCanvasPosition();
		%e  = %hl.getExtent();
		%x  = getWord(%p, 0);
		%y  = getWord(%p, 1);
		if(%cx >= %x && %cx < %x + getWord(%e, 0)
		&& %cy >= %y && %cy < %y + getWord(%e, 1))
			return %i;
	}
	return -1;
}

function MRPGCM_Tick()
{
	cancel($MRPGCM_TickSch);
	//Gate, and do NOT reschedule when it is shut - see ServerGate.cs rule 3.
	if(!$MRPGCM_Open || !MRPG_isActive())
		return;

	$MRPGCM_TickSch = schedule(40, 0, MRPGCM_Tick);

	%i = MRPGCM_RowAt();
	if(%i == $MRPGCM_Hover)
		return;

	if($MRPGCM_Hover >= 0)
		("MRPGCM_Hl" @ $MRPGCM_Hover).setVisible(0);

	$MRPGCM_Hover = %i;
	if(%i >= 0)
		("MRPGCM_Hl" @ %i).setVisible(1);
}

function MRPGCM_Catch::onMouseDown(%this)
{
	%i = MRPGCM_RowAt();
	%name = $MRPGCM_Name;

	MRPGCM_Close();

	if(%i < 0)
		return;   // clicked off the menu - closing was the whole action

	switch$($MRPGCM_RowCmd[%i])
	{
		case "profile":
			MRPGProf_Request(%name);

		case "whisper":
			// Open the chat input already addressed, so the player only types the
			// message. newMessageHud::open pushes the dialog itself - doing it here
			// as well would stack the dialog twice on the canvas.
			//
			// The value is set AFTER open() because open() clears NMH_Type when the
			// box was not already awake. GuiTextEditCtrl::setText leaves the caret
			// at the end of the string, so the player types straight on.
			newMessageHud.open("SAY");
			NMH_Type.setValue("/w " @ %name @ " ");

		case "invite":
			commandToServer('PartyInvite', %name);

		case "mute":
			$MRPGChat_Ignore[%name] = !$MRPGChat_Ignore[%name];
			MRPGChat_PushEvent($MRPGChat_Ignore[%name]
				? ("Muted " @ %name @ ". Their messages are hidden until you unmute them.")
				: ("Unmuted " @ %name @ "."));
	}
}


//////////////////////////////////
///////// THE PLAYER CARD ////////
//////////////////////////////////
//
// Fed by serverCmdMRPGChatProfile (Core_ChatRouter.cs) as a Head / Row.. / Done
// sequence, which is the same shape every other MonsterRPG data feed uses and
// keeps each command well inside the arg limit.

$MRPGProf::W = 348;
$MRPGProf::H = 430;

$MRPGProf_Built = 0;
$MRPGProf_Open  = 0;
$MRPGProf_Buf   = "";
$MRPGProf_Head  = "";

function MRPGProf_Request(%name)
{
	$MRPGProf_Buf  = "";
	$MRPGProf_Head = "";
	MRPGProf_Build();

	MRPGProf_Center();

	MRPGProf_Body.setText("<font:verdana:" @ MRPGChat_Px(14) @ "><color:"
		@ $MRPGChat::Col::Hint @ ">Loading " @ %name @ "...");
	MRPGProf_Title.setText("<just:center><font:verdana bold:" @ MRPGChat_Px(17)
		@ "><color:" @ $MRPGChat::Col::Gold @ ">" @ %name);

	if(!$MRPGProf_Open)
	{
		Canvas.pushDialog(MRPGProf_Dlg);
		$MRPGProf_Open = 1;
	}

	commandToServer('MRPGChatProfile', %name);
}

function MRPGProf_Build()
{
	if($MRPGProf_Built)
		return;

	MRPGChat_Build();   // MRPGChat_TextProfile and the control helpers

	%w = MRPGChat_Px($MRPGProf::W);
	%h = MRPGChat_Px($MRPGProf::H);
	%res = getRes();
	%x = mFloor((getWord(%res, 0) - %w) / 2);
	%y = mFloor((getWord(%res, 1) - %h) / 2);

	%dlg = new GuiControl(MRPGProf_Dlg)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "width";
		vertSizing  = "height";
		position    = "0 0";
		extent      = getWord(%res, 0) SPC getWord(%res, 1);
		minExtent   = "8 2";
	};

	// Drawn, not skinned - same reasoning as the chat panel (see MRPGChat_box).
	// RTS_Bar stretched to a tall narrow card squashes its border horizontally
	// instead of vertically, but it is the same distortion.
	%field = MRPGChat_box(%dlg, "MRPGProf_Card", %x, %y, %w, %h,
		$MRPGChat::Col::Fill, $MRPGChat::Col::Line);

	%iw = %w - 2;
	%ih = %h - 2;

	// Title band, so the name reads as a header rather than as the first row.
	MRPGChat_swatch(%field, "MRPGProf_Band", 0, 0, %iw, MRPGChat_Px(34), "26 21 14 255");

	%bl = MRPGChat_Px(11);
	%bt = MRPGChat_Px(2);
	MRPGChat_bracket(%field, "MRPGProf_BrTL", 0,         0,          1,  1, %bl, %bt, $MRPGChat::Col::Line);
	MRPGChat_bracket(%field, "MRPGProf_BrTR", %iw - %bt, 0,         -1,  1, %bl, %bt, $MRPGChat::Col::Line);
	MRPGChat_bracket(%field, "MRPGProf_BrBL", 0,         %ih - %bt,  1, -1, %bl, %bt, $MRPGChat::Col::Line);
	MRPGChat_bracket(%field, "MRPGProf_BrBR", %iw - %bt, %ih - %bt, -1, -1, %bl, %bt, $MRPGChat::Col::Line);

	MRPGChat_text(MRPGProf_Card, "MRPGProf_Title", 0, MRPGChat_Px(8), %iw, MRPGChat_Px(24), 0);
	MRPGChat_swatch(MRPGProf_Card, "MRPGProf_Rule", MRPGChat_Px(10), MRPGChat_Px(36),
		%iw - MRPGChat_Px(20), 1, "138 106 47 160");

	// One MLText for the whole card. The rows are a fixed, small set (six
	// attributes plus twelve skills), so there is nothing to scroll and a single
	// reflow is cheaper than a scroll control plus a content pane.
	MRPGChat_text(MRPGProf_Card, "MRPGProf_Body", MRPGChat_Px(14), MRPGChat_Px(44),
		%iw - MRPGChat_Px(28), %ih - MRPGChat_Px(80), 0);

	%btn = MRPGChat_bmp(MRPGProf_Card, "MRPGProf_CloseBtn",
		mFloor((%iw - MRPGChat_Px(96)) / 2), %ih - MRPGChat_Px(32),
		MRPGChat_Px(96), MRPGChat_Px(26), $MRPGChat::Btn @ "Button_middle");

	MRPGChat_text(%btn, "MRPGProf_CloseLbl", 0, MRPGChat_Px(5), MRPGChat_Px(96),
		MRPGChat_Px(18), 0);
	MRPGProf_CloseLbl.setText("<just:center><font:verdana bold:" @ MRPGChat_Px(13)
		@ "><color:F6EFCB>Close");

	// Catcher LAST so it is on top of the card and gets the clicks. It covers the
	// whole canvas: inside the Close button closes, and so does anywhere outside
	// the card, which is what people expect from a popup like this.
	%cat = new GuiMouseEventCtrl(MRPGProf_Catch)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "width";
		vertSizing  = "height";
		position    = "0 0";
		extent      = getWord(%res, 0) SPC getWord(%res, 1);
		minExtent   = "8 2";
		lockMouse   = "0";
	};
	%dlg.add(%cat);

	$MRPGProf_Built = 1;
}

// Re-centre on every open: the card is built once, but the window can be resized
// between two viewings and a card stranded half off the screen is a bug report.
function MRPGProf_Center()
{
	if(!$MRPGProf_Built)
		return;

	// The OUTER box is the positioned one; the inner field sits at 1,1 inside it.
	%e   = MRPGProf_CardEdge.getExtent();
	%res = getRes();
	MRPGProf_CardEdge.resize(
		mFloor((getWord(%res, 0) - getWord(%e, 0)) / 2),
		mFloor((getWord(%res, 1) - getWord(%e, 1)) / 2),
		getWord(%e, 0), getWord(%e, 1));
}

function MRPGProf_Close()
{
	if($MRPGProf_Open && isObject(MRPGProf_Dlg))
	{
		Canvas.popDialog(MRPGProf_Dlg);
		MRPG_RestoreCursor();
	}
	$MRPGProf_Open = 0;
}

// Close on any click that is not on the card, and on the Close button.
function MRPGProf_Catch::onMouseDown(%this)
{
	%cur = Canvas.getCursorPos();
	%cx  = getWord(%cur, 0);
	%cy  = getWord(%cur, 1);

	%p = MRPGProf_CardEdge.getCanvasPosition();
	%e = MRPGProf_CardEdge.getExtent();
	%inside = (%cx >= getWord(%p, 0) && %cx < getWord(%p, 0) + getWord(%e, 0)
	        && %cy >= getWord(%p, 1) && %cy < getWord(%p, 1) + getWord(%e, 1));

	if(!%inside)
	{
		MRPGProf_Close();
		return;
	}

	%bp = MRPGProf_CloseBtn.getCanvasPosition();
	%be = MRPGProf_CloseBtn.getExtent();
	if(%cx >= getWord(%bp, 0) && %cx < getWord(%bp, 0) + getWord(%be, 0)
	&& %cy >= getWord(%bp, 1) && %cy < getWord(%bp, 1) + getWord(%be, 1))
		MRPGProf_Close();
}


//////////////////////////////////
////////// THE FEED //////////////
//////////////////////////////////

function clientCmdMRPGChatProfileFail(%name)
{
	if(!$MRPGProf_Open)
		return;
	MRPGProf_Body.setText("<font:verdana:" @ MRPGChat_Px(14) @ "><color:"
		@ $MRPGChat::Col::Hint @ ">" @ %name @ " is no longer online.");
}

function clientCmdMRPGChatProfileHead(%name, %blid, %level, %rebirth,
	%covenant, %title, %karma, %exp, %maxExp)
{
	MRPGProf_Build();

	MRPGProf_Title.setText("<just:center><font:verdana bold:" @ MRPGChat_Px(17)
		@ "><color:" @ $MRPGChat::Col::Gold @ ">" @ %name);

	%line = "<color:" @ $MRPGChat::Col::Name @ ">Level " @ %level;
	if(%rebirth > 0)
		%line = %line @ "  <color:" @ $MRPGChat::Col::Gold @ ">(Rebirth " @ %rebirth @ ")";

	%sub = "";
	if(%title    !$= ""){ %sub = %title; }
	if(%covenant !$= ""){ %sub = (%sub $= "") ? %covenant : (%sub @ " of " @ %covenant); }

	$MRPGProf_Head = %line
		@ "\n<color:" @ $MRPGChat::Col::Prefix @ ">" @ %sub
		@ "\n<color:" @ $MRPGChat::Col::Prefix @ ">BL_ID " @ %blid
			@ "     Karma " @ %karma
		@ "\n<color:" @ $MRPGChat::Col::Prefix @ ">XP " @ mFloor(%exp) @ " / " @ mFloor(%maxExp);

	$MRPGProf_Buf     = "";
	$MRPGProf_LastCat = "";
}

// %data is  category TAB label TAB value.
function clientCmdMRPGChatProfileRow(%data)
{
	%cat   = getField(%data, 0);
	%label = getField(%data, 1);
	%value = getField(%data, 2);

	if(%cat !$= $MRPGProf_LastCat)
	{
		$MRPGProf_Buf = $MRPGProf_Buf @ "\n<color:" @ $MRPGChat::Col::Gold @ ">"
			@ strupr(%cat) @ "\n";
		$MRPGProf_LastCat = %cat;
	}

	// TAB STOP, not <just:right>. Justification in a GuiMLTextCtrl is a LINE
	// property - emitNewLine applies mCurJustify to the whole assembled line - so
	// "label<just:right>value" right-aligns the label along with it and you get
	// one ragged column instead of two. A tab advances mCurX to the next <tab:>
	// stop, and mCurTabStop resets on every newline, which is exactly a
	// two-column row.
	$MRPGProf_Buf = $MRPGProf_Buf
		@ "<color:" @ $MRPGChat::Col::Name @ ">" @ %label
		@ "\t<color:" @ $MRPGChat::Col::Value @ ">" @ %value @ "\n";
}

function clientCmdMRPGChatProfileDone()
{
	if(!isObject(MRPGProf_Body))
		return;

	// The stop sits ~62% across the text column, which clears "Intelligence" and
	// "Woodcutting" - the two longest labels in the set - and still lands every
	// value on the same edge.
	%stop = mFloor(getWord(MRPGProf_Body.getExtent(), 0) * 0.62);

	MRPGProf_Body.setText("<font:verdana:" @ MRPGChat_Px(14) @ "><tab:0," @ %stop @ ">"
		@ $MRPGProf_Head @ "\n" @ $MRPGProf_Buf);
}
