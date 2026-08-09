//////////////////////////////////////////////////////////////////////////////
// ChatPanel.cs  -  the MonsterRPG chat window (Global / Local / Events)
//////////////////////////////////////////////////////////////////////////////
//
// Replaces Blockland's top-left chat feed with a framed panel in the bottom-left
// corner, and takes the paint-can box out of that corner so the two do not fight
// over the same pixels.
//
// IT ONLY EXISTS ON THIS SERVER. Everything here is inert until the server sends
// 'addMRPGClientToServer' (Core_OLDpackage.cs -> clientCmdaddMRPGClientToServer),
// and MRPGChat_Disable() puts the stock chat, the paint box and the input line
// back exactly as they were the moment the client leaves. Joining any other
// server afterwards is stock Blockland with no trace of this file.
//
//
// THE THREE TABS ARE STRICTLY SEPARATE - a line lands in exactly one of them:
//
//   Global   player chat sent to the whole server
//   Local    player chat from within $MRPGChat::LocalRadius units
//   Events   everything the server says that is not player chat: level-ups, loot,
//            quests, kills, joins/leaves, admin output, other add-ons
//
// Global and Local arrive pre-split on their own client command (clientCmdMRPGChat,
// fed by Core_ChatRouter.cs). Events is the CATCH-ALL, fed from onServerMessage /
// onChatMessage - so an add-on that has never heard of this system still gets its
// text on screen instead of silently vanishing.
//
//
// THE STOCK CHAT IS MOVED OFFSCREEN, NOT HIDDEN, AND THE PARENT CALLS STILL RUN.
//
// setVisible(0) on newChatText looks like the obvious move and is the wrong one:
// newChatHud_AddLine -> NewChatSO::displayLatest calls forceReflow(), the engine
// refuses it on an invisible control ("GuiMLTextCtrl::forceReflow can only be
// called on visible controls"), and every chat line becomes a console error.
// isAwake() - which is what the stock code guards on - stays true either way, so
// the stock guard does not save us.
//
// Parking it at -9000 keeps every stock code path running untouched and simply
// draws it where nobody can see it. That also means:
//   * $NewChatSO's ring buffer stays correct, so nothing is lost if the panel is
//     ever torn down mid-session;
//   * Client_Chatlogs, which packages newChatHud_addLine, keeps logging.
// Which is why the packages below call Parent:: as well as MRPGChat_Push().
//
//
// PARENT IS NewChatHud, AND THAT IS WHAT MAKES ANY OF IT CLICKABLE.
//
// The first two attempts parented to PlayGui, following Minimap.cs. The panel
// drew perfectly and NOTHING in it could be clicked - not a tab, not a name -
// no matter what the cursor was doing. The reason is two engine details that
// only bite together:
//
//   1. GuiCanvas::maintainSizing() resizes EVERY canvas child to the full screen
//      rect on every frame, with the comment "this is necessary for passing
//      mouse events accurately". So NewChatHud, a 640x480 control in the .gui,
//      is actually full-screen at runtime.
//
//   2. GuiCanvas::rootMouseDown walks the canvas children LAST TO FIRST and
//      stops at the first hit that is active or modal. NewChatHud is a pushed
//      DIALOG, so it sits above PlayGui in that list, and its profile is
//      GuiDefaultProfile - which is modal = 1.
//
// Put together: NewChatHud hit-tests the whole screen, is modal, and is above
// PlayGui, so it swallows every click over the world and PlayGui never sees one.
// Stock Blockland never notices because the only clickable thing in chat -
// newChatText - is INSIDE NewChatHud.
//
// So we go inside it too. That is not a workaround, it is the right home: the
// canvas force-resizes it every frame, so its child coordinates ARE screen
// pixels (the exact property Minimap.cs went to PlayGui for), it renders above
// the whole HUD, and it is the chat layer by definition. It also carries
// noCursor = 1, so living there does not pin the cursor on.
//
// MonsterRPGx_MAIN_INTERFACE was never an option: it is scaled by height and
// anchored bottom-CENTRE, so its left edge sits 240px inside the screen at
// 1080p and nothing parented there can hug a corner.
//
// The panel's BOTTOM is held above the HUD bar rather than at the screen edge,
// because at 4:3 the HP orb does reach the corner. Left_Colb is authored at
// y=655 of the 1024x768 HUD space and the HUD is scaled by scrH/768 anchored at
// the bottom, so the orb's top is always at exactly (1 - 113/768) * scrH. That
// fraction is $MRPGChat::HudBandFrac and it holds at every resolution.
//////////////////////////////////////////////////////////////////////////////


$MRPGChat::Gfx = "Add-Ons/Client_MonsterRPG/GUIs/";
$MRPGChat::Btn = "Add-Ons/Client_MonsterRPG/GUIs/Button_Elements/";

// Tab identity. Index order is also the cycle order.
$MRPGChat::TAB_GLOBAL = 0;
$MRPGChat::TAB_LOCAL  = 1;
$MRPGChat::TAB_EVENTS = 2;
$MRPGChat::TabCount   = 3;

$MRPGChat_TabName[0] = "Global";
$MRPGChat_TabName[1] = "Local";
$MRPGChat_TabName[2] = "Events";

// Tab accent colours. Used for the tab label and the input badge, so the two
// places that tell you which channel you are in always agree.
$MRPGChat_TabHue[0] = "9FC4E8";   // global - cool blue
$MRPGChat_TabHue[1] = "9BD98A";   // local  - green
$MRPGChat_TabHue[2] = "E8B44C";   // events - gold

// Palette, kept in step with RPGPanels.cs / CharacterScreen.cs so the window
// reads as part of the same set.
$MRPGChat::Col::Name   = "F1E6C2";  // speaker name
$MRPGChat::Col::Prefix = "9A8F78";  // clan prefix / fame title, deliberately quiet
$MRPGChat::Col::Body   = "DCDAD2";  // the message itself
$MRPGChat::Col::Event  = "C6C1B2";  // event text
$MRPGChat::Col::Bullet = "8A7B55";  // the event bullet
$MRPGChat::Col::Hint   = "6E6656";  // "press T to chat"
$MRPGChat::Col::Gold   = "C9A24E";
$MRPGChat::Col::Whisper= "C79BD6";  // private messages - the one violet in the set
$MRPGChat::Col::Value  = "F1ECC2";  // right-hand numbers on the player card

// Panel surfaces, as "r g b a" for GuiSwatchCtrl. Same warm near-black and gold
// the RPG windows use - the frame is drawn now rather than skinned, but the
// palette did not change.
$MRPGChat::Col::Fill      = "13 10 7 234";    // log field
$MRPGChat::Col::InputFill = "26 21 14 244";   // typing box - lifted off the log
$MRPGChat::Col::Line      = "138 106 47 190"; // hairline / brackets
$MRPGChat::Col::LineHot   = "216 176 96 255"; // same hairline while typing

// Clickable names. fontColorLink is what a name is drawn in AT REST and
// fontColorLinkHL what it turns while the button is held on it - guiMLTextCtrl
// picks between exactly those two for any atom inside an <a:> and IGNORES the
// <color:> tag around it, so the resting colour has to be set here rather than in
// the markup.
$MRPGChat::Col::Link   = "F5D98A";  // warm gold: reads as "you can click this"
$MRPGChat::Col::LinkHL = "FFFFFF";

// Entries kept per tab. This is the panel's own scrollback and is independent of
// $Pref::Chat::CacheLines. The whole tab is re-flowed on every incoming line, so
// this is also the cost knob: 120 entries is a few thousand characters, which is
// nothing per message, and dropping it is the first thing to try if a very busy
// server ever feels the chat.
if($MRPGChat::Scrollback $= ""){ $MRPGChat::Scrollback = 120; }

// Local chat radius in Torque units. This copy only drives the help text - the
// SERVER owns the real cut, in Core_ChatRouter.cs. Keep the two in step.
if($MRPGChat::LocalRadius $= ""){ $MRPGChat::LocalRadius = 100; }

// Fraction of screen height the MonsterRPG HUD bar occupies at the bottom.
// 113/768: Left_Colb's authored top edge is y=655 in the 1024x768 HUD space.
if($MRPGChat::HudBandFrac $= ""){ $MRPGChat::HudBandFrac = 0.1471; }

// Layout in DESIGN pixels - what these mean at 1080p. Everything is multiplied
// by MRPGChat_UiScale() before it reaches a control.
if($MRPGChat::PanelW   $= ""){ $MRPGChat::PanelW   = 452; }
if($MRPGChat::BodyH    $= ""){ $MRPGChat::BodyH    = 186; }
if($MRPGChat::TabH     $= ""){ $MRPGChat::TabH     = 25; }
if($MRPGChat::TabW     $= ""){ $MRPGChat::TabW     = 98; }
if($MRPGChat::TabGap   $= ""){ $MRPGChat::TabGap   = 3; }
if($MRPGChat::InputH   $= ""){ $MRPGChat::InputH   = 26; }
if($MRPGChat::Margin   $= ""){ $MRPGChat::Margin   = 12; }
if($MRPGChat::FontSize $= ""){ $MRPGChat::FontSize = 15; }
if($MRPGChat::LineGap  $= ""){ $MRPGChat::LineGap  = 4; }
if($MRPGChat::Indent   $= ""){ $MRPGChat::Indent   = 18; } // hanging indent, design px

// Reference height for the UI scale, and the clamp either side of it.
if($MRPGChat::RefHeight $= ""){ $MRPGChat::RefHeight = 1080; }
if($MRPGChat::ScaleMin  $= ""){ $MRPGChat::ScaleMin  = 0.70; }
if($MRPGChat::ScaleMax  $= ""){ $MRPGChat::ScaleMax  = 1.90; }

// Hover / resolution poll. One cursor read and one string compare.
if($MRPGChat::TickMs $= ""){ $MRPGChat::TickMs = 60; }

//////////////////////////////////////////////////////////////////////////////
// VISIBILITY MODES
//
// Three stages on one key, cycling FULL -> NOTIFY -> HIDDEN -> FULL. Same shape
// as $Minimap::MODE_* next door, deliberately, so the two HUD toggles behave
// alike.
//
//   FULL    everything: tabs, log, typing box. The default, and what you get
//           back on every join.
//   NOTIFY  a single small bar with one badge per channel. Tells you THAT
//           something arrived and whether any of it names you, without any of
//           the text - which is the point of turning chat down rather than off.
//   HIDDEN  nothing at all. Messages still accumulate; the counts are waiting
//           when you come back.
//////////////////////////////////////////////////////////////////////////////
$MRPGChat::MODE_FULL   = 0;
$MRPGChat::MODE_NOTIFY = 1;
$MRPGChat::MODE_HIDDEN = 2;
$MRPGChat::ModeCount   = 3;

// Compact-bar geometry, design px.
if($MRPGChat::NotifyW $= ""){ $MRPGChat::NotifyW = 206; }
if($MRPGChat::NotifyH $= ""){ $MRPGChat::NotifyH = 28; }
if($MRPGChat::BadgeW  $= ""){ $MRPGChat::BadgeW  = 21; }
if($MRPGChat::BadgeH  $= ""){ $MRPGChat::BadgeH  = 18; }
if($MRPGChat::BadgeGap $= ""){ $MRPGChat::BadgeGap = 5; }

// One letter per channel for the badges. Kept short on purpose - anything longer
// stops being an icon and starts being a label.
$MRPGChat_TabGlyph[0] = "G";
$MRPGChat_TabGlyph[1] = "L";
$MRPGChat_TabGlyph[2] = "E";

$MRPGChat_Mode = $MRPGChat::MODE_FULL;

// Runtime state.
$MRPGChat_Built    = 0;
$MRPGChat_On       = 0;   // is the panel live (i.e. are we on the RPG server)?
$MRPGChat_Tab      = 0;   // selected tab
$MRPGChat_Hover    = -1;
$MRPGChat_ScrollPx = 0;   // pixels lifted off the bottom; 0 = pinned to newest
$MRPGChat_Saved    = 0;   // have the stock control positions been captured yet?
$MRPGChat_Typing   = 0;   // is newMessageHud currently open on our input strip?
cancel($MRPGChat_TickSch);
$MRPGChat_TickSch  = "";


//////////////////////////////////
///////// SMALL HELPERS //////////
//////////////////////////////////

function MRPGChat_UiScale()
{
	%h = getWord(getRes(), 1);
	if(%h <= 0)
		return 1;

	%s = %h / $MRPGChat::RefHeight;
	if(%s < $MRPGChat::ScaleMin){ %s = $MRPGChat::ScaleMin; }
	if(%s > $MRPGChat::ScaleMax){ %s = $MRPGChat::ScaleMax; }
	return %s;
}

// Design pixels -> real pixels.
function MRPGChat_Px(%design)
{
	%v = mFloor(%design * MRPGChat_UiScale());
	if(%v < 1){ %v = 1; }
	return %v;
}

// THE PANEL IS DRAWN, NOT SKINNED, and RTS_Bar is why.
//
// A GuiBitmapCtrl with wrap=0 STRETCHES its bitmap to the control. RTS_Bar is a
// 512x512 plate with an ornate border and corner filigree, so putting it behind a
// 452x186 chat log squashes that border to 5% of each dimension INDEPENDENTLY -
// about 22px wide and 9px tall. The ornament ends up crushed flat vertically,
// which is exactly the "scrunched" look, and no amount of padding fixes it
// because the art itself is being distorted.
//
// So the frame is built from swatches instead: a one-pixel gold hairline, a flat
// dark fill, an accent bar tying the tabs to the body, and four small corner
// brackets. Same palette, nothing stretched, and it stays crisp at any panel
// size - which a stretched bitmap cannot.

// A bordered box = two swatches: the outer one IS the border colour, and the
// inner one is inset by the line width. Cheaper and sharper than four edge
// swatches, and it scales without any of them drifting apart.
function MRPGChat_box(%parent, %name, %x, %y, %w, %h, %fill, %line)
{
	%outer = MRPGChat_swatch(%parent, %name @ "Edge", %x, %y, %w, %h, %line);
	return MRPGChat_swatch(%outer, %name, 1, 1, %w - 2, %h - 2, %fill);
}

function MRPGChat_boxResize(%name, %x, %y, %w, %h)
{
	%outer = %name @ "Edge";
	%outer.resize(%x, %y, %w, %h);
	%name.resize(1, 1, %w - 2, %h - 2);
}

// One L-shaped corner bracket, as two swatches. %dx / %dy are -1 or 1 and say
// which way the arms run, so the same call builds all four corners.
//
// This is the whole medieval cue now that the filigree is gone: brackets read as
// a frame at any size, where a stretched ornament reads as a smear.
function MRPGChat_bracket(%parent, %name, %x, %y, %dx, %dy, %len, %th, %col)
{
	%hx = (%dx > 0) ? %x : (%x - %len + 1);
	MRPGChat_swatch(%parent, %name @ "H", %hx, %y, %len, %th, %col);

	%vy = (%dy > 0) ? %y : (%y - %len + 1);
	MRPGChat_swatch(%parent, %name @ "V", %x, %vy, %th, %len, %col);
}

function MRPGChat_bracketResize(%name, %x, %y, %dx, %dy, %len, %th)
{
	%h = %name @ "H";
	%v = %name @ "V";
	%h.resize((%dx > 0) ? %x : (%x - %len + 1), %y, %len, %th);
	%v.resize(%x, (%dy > 0) ? %y : (%y - %len + 1), %th, %len);
}

function MRPGChat_swatch(%parent, %name, %x, %y, %w, %h, %col)
{
	%s = new GuiSwatchCtrl(%name)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = %x SPC %y;
		extent      = %w SPC %h;
		minExtent   = "1 1";
		color       = %col;
	};
	%parent.add(%s);
	return %s;
}

function MRPGChat_bmp(%parent, %name, %x, %y, %w, %h, %bitmap)
{
	%b = new GuiBitmapCtrl(%name)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = %x SPC %y;
		extent      = %w SPC %h;
		minExtent   = "2 2";
		bitmap      = %bitmap;
		wrap        = "0";
		mColor      = "255 255 255 255";
		mMultiply   = "0";
	};
	%parent.add(%b);
	return %b;
}

function MRPGChat_text(%parent, %name, %x, %y, %w, %h, %autoResize)
{
	%t = new GuiMLTextCtrl(%name)
	{
		profile         = "MRPGChat_TextProfile";
		horizSizing     = "right";
		vertSizing      = "bottom";
		position        = %x SPC %y;
		extent          = %w SPC %h;
		minExtent       = "8 2";
		lineSpacing     = $MRPGChat::LineGap;
		allowColorChars = "1";
		maxChars        = "-1";
		selectable      = "0";
		autoResize      = %autoResize;
	};
	%parent.add(%t);

	//////////////////////////////////////////////////////////////////////
	// RE-ASSERT THE PROFILE AS AN OBJECT, NOT A STRING.
	//
	// `profile = "MRPGChat_TextProfile"` above is resolved by NAME when the
	// control is constructed. If the profile object does not exist at that
	// moment the engine falls back to the default profile and never looks
	// again - the control keeps a profile whose fontColor is BLACK.
	//
	// The symptom is specific and was reported as such: the FIRST line of chat
	// is black and everything after it is readable. That is because
	// allowColorChars is on, so any line carrying a \c code still draws in a
	// colour, and MLText carries the last colour forward across appends - only
	// the opening text, which has no code in front of it, ever falls back to
	// the profile's base colour.
	//
	// setProfile takes the object, so this cannot silently no-op the way a
	// name lookup can.
	//////////////////////////////////////////////////////////////////////
	if(isObject(MRPGChat_TextProfile))
		%t.setProfile(MRPGChat_TextProfile);

	return %t;
}


//////////////////////////////////
/////////// BUILD ////////////////
//////////////////////////////////
//
// Built once, lazily, then only ever re-laid-out. The tabs are art plates with a
// hidden hover frame and no mouse control of their own - one named
// GuiMouseEventCtrl hit-tests them, which is the pattern CharacterScreen.cs and
// TreeClient.cs settled on after per-button class callbacks were found not to
// fire in this build.

function MRPGChat_Build()
{
	if($MRPGChat_Built)
		return 1;

	// NewChatHud, not PlayGui - see the parenting note in the file header. It is
	// pushed by PlayGui::onWake and by LoadingGui::onWake, so it is up well
	// before the server sends the join command; the retry in MRPGChat_Enable
	// covers the case where it is not.
	if(!isObject(NewChatHud))
		return 0;

	if(!isObject(MRPGChat_TextProfile))
	{
		// Plain Verdana, not Bold. The panel backdrop is dark and opaque, so the
		// extra weight is not buying contrast - it is only costing legibility at
		// 15px, which is the opposite of what this panel is for.
		new GuiControlProfile(MRPGChat_TextProfile)
		{
			fontType        = "Verdana";
			fontSize        = $MRPGChat::FontSize;
			fontColor       = "220 218 210 255";
			allowColorChars = 1;
			maxLength       = 8192;
			justify         = "Left";

			//////////////////////////////////////////////////////////////
			// allowColorChars = 1 above turns \cN ON. This table is what
			// those escapes actually index, and WITHOUT IT THEY ARE
			// UNDEFINED - the profile inherits nothing, so a \c6 lands on an
			// empty slot and draws in whatever the engine falls back to.
			//
			// That matters here more than it looks: essentially every server
			// message in this project is written in colour codes (\c6 body,
			// \c3 values, \c0 errors), so an undefined table affects most
			// lines in the log rather than a rare one.
			//
			// Chosen to stay legible on the dark panel this control sits on.
			// Stock's equivalent table is tuned for a light background and
			// puts BLACK at [9], which is unreadable here.
			//////////////////////////////////////////////////////////////
			fontColors[0] = "255 138 128";   // errors / warnings
			fontColors[1] = "255 176  96";
			fontColors[2] = "126 204 255";   // party
			fontColors[3] = "245 217 138";   // values, matches the name links
			fontColors[4] = "150 226 150";
			fontColors[5] = "200 160 255";
			fontColors[6] = "220 218 210";   // body text - same as fontColor
			fontColors[7] = "180 180 180";
			fontColors[8] = "255 255 255";
			fontColors[9] = "220 218 210";   // NOT black - stock uses black here

			// Player-name links. These two ARE the only colours a name can take:
			// drawAtomText branches on atom->url before it ever looks at the
			// style colour, so a <color:> tag around a name has no effect.
			fontColorLink   = "245 217 138 255";
			fontColorLinkHL = "255 255 255 255";
		};
	}

	%panelW = MRPGChat_Px($MRPGChat::PanelW);
	%tabH   = MRPGChat_Px($MRPGChat::TabH);
	%bodyH  = MRPGChat_Px($MRPGChat::BodyH);
	%inputH = MRPGChat_Px($MRPGChat::InputH);
	// The Px(3) is the gap between the log box and the typing box - they are two
	// separate framed boxes now, not one strip with a divider, so the panel is
	// that much taller than the sum of its parts.
	%panelH = %tabH + %bodyH + MRPGChat_Px(3) + %inputH;

	%root = new GuiSwatchCtrl(MRPGChatPanel)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = "0 0";
		extent      = %panelW SPC %panelH;
		minExtent   = "8 2";
		color       = "0 0 0 0";
		visible     = "0";
	};
	NewChatHud.add(%root);

	// ---- tab strip -------------------------------------------------------
	// A transparent rail the plates sit on, so the row moves as one.
	MRPGChat_swatch(%root, "MRPGChatTabRail", 0, 0, %panelW, %tabH, "0 0 0 0");

	%tabW = MRPGChat_Px($MRPGChat::TabW);
	%gap  = MRPGChat_Px($MRPGChat::TabGap);
	for(%i = 0; %i < $MRPGChat::TabCount; %i++)
	{
		// Inactive plate is the dark slate; the selected tab swaps to the lit
		// blue one in MRPGChat_SetTab. Both are the art set the RPG menu buttons
		// already use, so the tab bar reads as part of that family.
		%plate = MRPGChat_bmp(MRPGChatTabRail, "MRPGChatTab" @ %i,
			%i * (%tabW + %gap), 0, %tabW, %tabH,
			$MRPGChat::Btn @ "Button_middle_dark");

		%frm = MRPGChat_bmp(%plate, "MRPGChatTabFr" @ %i, 0, 0, %tabW, %tabH,
			$MRPGChat::Btn @ "Button_middle_Fr");
		%frm.setVisible(0);

		MRPGChat_text(%plate, "MRPGChatTabLbl" @ %i, 0,
			mFloor((%tabH - MRPGChat_Px(14)) / 2), %tabW, MRPGChat_Px(16), 0);

		// Unread pip: sits in the plate's top-right bevel, and only appears on a
		// tab you are not currently looking at.
		%d   = MRPGChat_Px(6);
		%dot = MRPGChat_swatch(%plate, "MRPGChatTabDot" @ %i,
			%tabW - %d - MRPGChat_Px(7), MRPGChat_Px(5), %d, %d, "232 180 76 255");
		%dot.setVisible(0);
	}

	// ---- body ------------------------------------------------------------
	// Flat dark field inside a one-pixel gold hairline. See the note above
	// MRPGChat_box for why this is drawn rather than skinned.
	%field = MRPGChat_box(%root, "MRPGChatField", 0, %tabH, %panelW, %bodyH,
		$MRPGChat::Col::Fill, $MRPGChat::Col::Line);

	%fw = %panelW - 2;
	%fh = %bodyH - 2;

	// Accent bar directly under the tabs, in the selected tab's colour. It is
	// what makes the tab row and the log read as one object rather than a strip
	// of buttons that happens to sit above a box.
	MRPGChat_swatch(%field, "MRPGChatAccent", 0, 0, %fw, MRPGChat_Px(2),
		"201 162 78 210");

	// THE CLIP WINDOW IS WHY THERE IS NO GuiScrollCtrl HERE.
	// MRPGChatText is given a NEGATIVE y inside this fixed-size swatch, so the
	// newest line sits on the bottom edge and older lines run off the top.
	// GuiControl::renderChildControls intersects every child against its parent's
	// rect, so the overflow is clipped for free. A scroll control would anchor a
	// short log to the TOP - wrong for chat, and awkward to undo.
	%padL = MRPGChat_Px(12);
	%padR = MRPGChat_Px(16);   // wider: the thumb track lives in this gutter
	%padT = MRPGChat_Px(9);
	%padB = MRPGChat_Px(8);
	%clip = MRPGChat_swatch(%field, "MRPGChatClip", %padL, %padT,
		%fw - %padL - %padR, %fh - %padT - %padB, "0 0 0 0");

	// autoResize = 1: the height is owned by the reflow, and MRPGChat_Anchor
	// reads it back to work out how far to lift the control.
	MRPGChat_text(%clip, "MRPGChatText", 0, 0,
		getWord(%clip.getExtent(), 0), MRPGChat_Px(20), 1);

	// Scroll thumb: a thin gold bar in the right gutter, hidden while the whole
	// log fits.
	%thumb = MRPGChat_swatch(%field, "MRPGChatThumb", %fw - MRPGChat_Px(9),
		%padT, MRPGChat_Px(3), MRPGChat_Px(20), "201 162 78 190");
	%thumb.setVisible(0);

	// "more below" marker: a chevron at the FOOT OF THE THUMB TRACK, in that same
	// gutter. It lived over the bottom of the log at first, which put it straight
	// on top of the newest line - the one message you most want to read while
	// deciding whether to scroll back down. The gutter is empty by definition.
	%more = MRPGChat_text(%field, "MRPGChatMore", %fw - MRPGChat_Px(13),
		%fh - MRPGChat_Px(18), MRPGChat_Px(12), MRPGChat_Px(16), 0);
	%more.setVisible(0);

	// Corner brackets last, so they sit over the fill. Added to the FIELD rather
	// than the border swatch so they inset cleanly off the hairline.
	%bl = MRPGChat_Px(9);
	%bt = MRPGChat_Px(2);
	MRPGChat_bracket(%field, "MRPGChatBrTL", 0,        0,         1, 1, %bl, %bt, $MRPGChat::Col::Line);
	MRPGChat_bracket(%field, "MRPGChatBrTR", %fw - %bt, 0,       -1, 1, %bl, %bt, $MRPGChat::Col::Line);
	MRPGChat_bracket(%field, "MRPGChatBrBL", 0,        %fh - %bt, 1, -1, %bl, %bt, $MRPGChat::Col::Line);
	MRPGChat_bracket(%field, "MRPGChatBrBR", %fw - %bt, %fh - %bt, -1, -1, %bl, %bt, $MRPGChat::Col::Line);

	// ---- input strip -----------------------------------------------------
	// A REAL FIELD, not a bare strip. What you are typing used to sit on the same
	// flat colour as everything else with nothing marking where the box started
	// or ended, which is most of why it was hard to read back. It is now an inset
	// box with its own hairline that brightens while the box is open, and
	// MRPGChat_InputProfile gives the text itself a bigger, warmer face than the
	// stock HUD chat profile.
	%iy = %tabH + %bodyH + MRPGChat_Px(3);
	MRPGChat_box(%root, "MRPGChatInput", 0, %iy, %panelW, %inputH,
		$MRPGChat::Col::InputFill, $MRPGChat::Col::Line);

	MRPGChat_text(MRPGChatInput, "MRPGChatHint", MRPGChat_Px(10),
		mFloor((%inputH - 2 - MRPGChat_Px(15)) / 2),
		%panelW - MRPGChat_Px(22), MRPGChat_Px(18), 0);

	// ---- notify bar ------------------------------------------------------
	// The middle stage. A sibling of the full layout rather than a reskin of it,
	// so switching modes is a visibility flip and never a rebuild.
	%nw = MRPGChat_Px($MRPGChat::NotifyW);
	%nh = MRPGChat_Px($MRPGChat::NotifyH);
	%bar = MRPGChat_box(%root, "MRPGChatNotify", 0, 0, %nw, %nh,
		$MRPGChat::Col::Fill, $MRPGChat::Col::Line);
	MRPGChatNotifyEdge.setVisible(0);

	%bw = MRPGChat_Px($MRPGChat::BadgeW);
	%bh = MRPGChat_Px($MRPGChat::BadgeH);
	%bg = MRPGChat_Px($MRPGChat::BadgeGap);
	%by = mFloor((%nh - 2 - %bh) / 2);

	for(%i = 0; %i < $MRPGChat::TabCount; %i++)
	{
		%bx = MRPGChat_Px(8) + %i * (%bw + %bg);

		// Ring first so it sits BEHIND the badge - it is drawn as a slightly
		// larger square peeking out on all four sides, which is the cheapest
		// "this one is about you" marker that still reads at 21px.
		%ring = MRPGChat_swatch(%bar, "MRPGChatBadgeRing" @ %i, %bx - 2, %by - 2,
			%bw + 4, %bh + 4, "232 184 92 255");
		%ring.setVisible(0);

		MRPGChat_swatch(%bar, "MRPGChatBadge" @ %i, %bx, %by, %bw, %bh, "26 21 14 255");
		MRPGChat_text(("MRPGChatBadge" @ %i), "MRPGChatBadgeLbl" @ %i, 0,
			mFloor((%bh - MRPGChat_Px(13)) / 2), %bw, MRPGChat_Px(15), 0);
	}

	%sx = MRPGChat_Px(8) + $MRPGChat::TabCount * (%bw + %bg) + MRPGChat_Px(4);
	MRPGChat_text(%bar, "MRPGChatNotifyText", %sx,
		mFloor((%nh - 2 - MRPGChat_Px(14)) / 2), %nw - %sx - MRPGChat_Px(8),
		MRPGChat_Px(16), 0);

	// ---- mouse catchers --------------------------------------------------
	// Covers the tab rail ONLY. Anything larger would swallow clicks over the log
	// for no gain - and the log needs its own clicks for the name links.
	%cat = new GuiMouseEventCtrl(MRPGChatCatch)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = "0 0";
		extent      = %panelW SPC %tabH;
		minExtent   = "8 2";
		lockMouse   = "0";
	};
	%root.add(%cat);

	// Clicking the notify bar goes straight back to FULL - the shortest path from
	// "something happened" to reading it, and it means the middle stage is not a
	// dead end for anyone who has forgotten the key.
	%catN = new GuiMouseEventCtrl(MRPGChatCatchNotify)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = "0 0";
		extent      = %nw SPC %nh;
		minExtent   = "8 2";
		lockMouse   = "0";
		visible     = "0";
	};
	%root.add(%catN);

	$MRPGChat_Built = 1;
	MRPGChat_SetTab($MRPGChat_Tab);
	return 1;
}


//////////////////////////////////
/////////// LAYOUT ///////////////
//////////////////////////////////
//
// Every number here is a real screen pixel derived from getRes(), so a resolution
// or window change is a re-run of this function and nothing else.

function MRPGChat_Layout()
{
	if(!$MRPGChat_Built || !isObject(MRPGChatPanel))
		return;

	%res  = getRes();
	%scrW = getWord(%res, 0);
	%scrH = getWord(%res, 1);
	$MRPGChat_LastRes = %res;

	%panelW = MRPGChat_Px($MRPGChat::PanelW);
	%tabH   = MRPGChat_Px($MRPGChat::TabH);
	%bodyH  = MRPGChat_Px($MRPGChat::BodyH);
	%inputH = MRPGChat_Px($MRPGChat::InputH);
	%margin = MRPGChat_Px($MRPGChat::Margin);

	// Never wider than half the screen: on a small window the panel would
	// otherwise cover the play area it is meant to sit beside.
	%maxW = mFloor(%scrW * 0.5);
	if(%panelW > %maxW){ %panelW = %maxW; }

	// The Px(3) is the gap between the log box and the typing box - they are two
	// separate framed boxes now, not one strip with a divider, so the panel is
	// that much taller than the sum of its parts.
	%fullH = %tabH + %bodyH + MRPGChat_Px(3) + %inputH;

	%nw = MRPGChat_Px($MRPGChat::NotifyW);
	%nh = MRPGChat_Px($MRPGChat::NotifyH);
	if(%nw > %panelW){ %nw = %panelW; }

	// THE PANEL SHRINKS TO THE ACTIVE MODE, and the BOTTOM-LEFT corner is what
	// stays put. Anchoring the top instead would leave the notify bar floating in
	// the middle of the screen where the full panel's top edge used to be.
	//
	// The WIDTH has to shrink too, and that one is not cosmetic: the panel is
	// transparent, so a full-width panel in notify mode would look identical -
	// but it is modal, so the empty strip beside the bar would silently swallow
	// clicks meant for the world behind it.
	%isNotify  = ($MRPGChat_Mode == $MRPGChat::MODE_NOTIFY);
	%panelH    = %isNotify ? %nh : %fullH;
	%panelUseW = %isNotify ? %nw : %panelW;

	// Bottom edge rides just above the HUD bar (see $MRPGChat::HudBandFrac), so
	// the HP orb is never covered at any aspect ratio.
	%bottom = mFloor(%scrH * (1 - $MRPGChat::HudBandFrac)) - MRPGChat_Px(6);
	%y      = %bottom - %panelH;
	if(%y < %margin){ %y = %margin; }

	MRPGChatPanel.resize(%margin, %y, %panelUseW, %panelH);

	// The notify bar and its catcher always sit at the panel's top-left; in
	// NOTIFY mode the panel is exactly bar-sized, so that IS the corner.
	MRPGChat_boxResize("MRPGChatNotify", 0, 0, %nw, %nh);
	MRPGChatCatchNotify.resize(0, 0, %nw, %nh);

	%bw = MRPGChat_Px($MRPGChat::BadgeW);
	%bh = MRPGChat_Px($MRPGChat::BadgeH);
	%bg = MRPGChat_Px($MRPGChat::BadgeGap);
	%by = mFloor((%nh - 2 - %bh) / 2);
	for(%i = 0; %i < $MRPGChat::TabCount; %i++)
	{
		%bx    = MRPGChat_Px(8) + %i * (%bw + %bg);
		%badge = "MRPGChatBadge"     @ %i;
		%ring  = "MRPGChatBadgeRing" @ %i;
		%blbl  = "MRPGChatBadgeLbl"  @ %i;

		%ring.resize(%bx - 2, %by - 2, %bw + 4, %bh + 4);
		%badge.resize(%bx, %by, %bw, %bh);
		%blbl.resize(0, mFloor((%bh - MRPGChat_Px(13)) / 2), %bw, MRPGChat_Px(15));
	}

	%sx = MRPGChat_Px(8) + $MRPGChat::TabCount * (%bw + %bg) + MRPGChat_Px(4);
	MRPGChatNotifyText.resize(%sx, mFloor((%nh - 2 - MRPGChat_Px(14)) / 2),
		%nw - %sx - MRPGChat_Px(8), MRPGChat_Px(16));

	// Tabs share whatever width the panel actually got, so they stay inside it on
	// a narrow window instead of running off the right edge.
	%tabW  = MRPGChat_Px($MRPGChat::TabW);
	%gap   = MRPGChat_Px($MRPGChat::TabGap);
	%avail = %panelW - MRPGChat_Px(60);
	%fit   = mFloor((%avail - %gap * ($MRPGChat::TabCount - 1)) / $MRPGChat::TabCount);
	if(%fit < %tabW){ %tabW = %fit; }
	if(%tabW < MRPGChat_Px(46)){ %tabW = MRPGChat_Px(46); }

	MRPGChatTabRail.resize(0, 0, %panelW, %tabH);
	MRPGChatCatch.resize(0, 0, %panelW, %tabH);

	%lblH = MRPGChat_Px(16);
	%lblY = mFloor((%tabH - MRPGChat_Px(14)) / 2);
	%d    = MRPGChat_Px(6);
	for(%i = 0; %i < $MRPGChat::TabCount; %i++)
	{
		%plate = "MRPGChatTab"    @ %i;
		%frm   = "MRPGChatTabFr"  @ %i;
		%lbl   = "MRPGChatTabLbl" @ %i;
		%dot   = "MRPGChatTabDot" @ %i;

		%plate.resize(%i * (%tabW + %gap), 0, %tabW, %tabH);
		%frm.resize(0, 0, %tabW, %tabH);
		%lbl.resize(0, %lblY, %tabW, %lblH);
		%dot.resize(%tabW - %d - MRPGChat_Px(7), MRPGChat_Px(5), %d, %d);
	}

	MRPGChat_boxResize("MRPGChatField", 0, %tabH, %panelW, %bodyH);
	%fw = %panelW - 2;
	%fh = %bodyH - 2;

	MRPGChatAccent.resize(0, 0, %fw, MRPGChat_Px(2));

	%bl = MRPGChat_Px(9);
	%bt = MRPGChat_Px(2);
	MRPGChat_bracketResize("MRPGChatBrTL", 0,         0,          1,  1, %bl, %bt);
	MRPGChat_bracketResize("MRPGChatBrTR", %fw - %bt, 0,         -1,  1, %bl, %bt);
	MRPGChat_bracketResize("MRPGChatBrBL", 0,         %fh - %bt,  1, -1, %bl, %bt);
	MRPGChat_bracketResize("MRPGChatBrBR", %fw - %bt, %fh - %bt, -1, -1, %bl, %bt);

	%padL  = MRPGChat_Px(12);
	%padR  = MRPGChat_Px(16);
	%padT  = MRPGChat_Px(9);
	%padB  = MRPGChat_Px(8);
	%clipW = %fw - %padL - %padR;
	%clipH = %fh - %padT - %padB;
	MRPGChatClip.resize(%padL, %padT, %clipW, %clipH);

	// Only the width is set here. The height belongs to autoResize, and stomping
	// it would collapse the log for a frame.
	MRPGChatText.resize(0, getWord(MRPGChatText.getPosition(), 1),
		%clipW, getWord(MRPGChatText.getExtent(), 1));

	// lineSpacing is a raw control field, so unlike the <font:> tags in the text
	// it does NOT come along for the ride when the UI scale changes - leave it and
	// the lines crowd together at 4K under a 26px font.
	MRPGChatText.lineSpacing = MRPGChat_Px($MRPGChat::LineGap);

	MRPGChatThumb.resize(%fw - MRPGChat_Px(9), %padT, MRPGChat_Px(3), MRPGChat_Px(20));
	MRPGChatMore.resize(%fw - MRPGChat_Px(13), %fh - MRPGChat_Px(18),
		MRPGChat_Px(12), MRPGChat_Px(16));

	%iy = %tabH + %bodyH + MRPGChat_Px(3);
	MRPGChat_boxResize("MRPGChatInput", 0, %iy, %panelW, %inputH);
	MRPGChatHint.resize(MRPGChat_Px(10),
		mFloor((%inputH - 2 - MRPGChat_Px(15)) / 2),
		%panelW - MRPGChat_Px(22), MRPGChat_Px(18));

	MRPGChat_ShowHint();
	MRPGChat_Render();

	// The typing box is placed over our input strip, so it moves with the panel.
	if($MRPGChat_Typing)
		MRPGChat_PlaceInput();
}


//////////////////////////////////
//////// ENABLE / DISABLE ////////
//////////////////////////////////
//
// The two halves of the whole feature. Enable is called from the packaged
// clientCmdaddMRPGClientToServer; Disable from every way a client can leave a
// server, which is the same set Package.cs already unwinds the rest of the HUD on.

function MRPGChat_Enable()
{
	if($MRPGChat_On)
		return;

	//Both retries below re-arm this function, so it needs the same gate as any
	//other repeating tick: a client that disconnects while the panel is still
	//waiting for PlayGui would otherwise retry every 250ms for the rest of the
	//session, and then bring our chat panel up on somebody else's server the
	//moment it succeeded. See ServerGate.cs rule 3.
	if(!MRPG_isActive())
		return;

	if(!MRPGChat_Build())
	{
		// PlayGui is always up by the time the server sends the join command, but
		// the retry costs one schedule and removes the race entirely.
		schedule(250, 0, MRPGChat_Enable);
		return;
	}

	// Do not go live until the stock feed's home position is safely recorded.
	// Showing both at once - ours AND an unparked stock feed - is the one bad
	// outcome, and it is exactly what would happen if this were allowed to run
	// with nothing captured.
	MRPGChat_SaveStockLayout();
	if(!$MRPGChat_Saved)
	{
		schedule(250, 0, MRPGChat_Enable);
		return;
	}

	MRPGChat_ParkStockChat(1);
	MRPGChat_ShowPaintBox(0);

	$MRPGChat_On = 1;

	// FULL on every join, by design. Someone who hid the chat to concentrate
	// during one session should not log back in tomorrow to a server that looks
	// like it has no chat at all - that reads as broken, not as a setting.
	$MRPGChat_Mode = $MRPGChat::MODE_FULL;

	MRPGChatPanel.setVisible(1);
	MRPGChat_ApplyMode();
	//No bind call here - MRPG_borrowKeys has already taken ] [ and \ for the
	//session, and will hand them back on leave. See Keybinds.cs.
	MRPGChat_Tick();

	MRPGChat_PushEvent("<color:" @ $MRPGChat::Col::Gold @ ">Chat channels ready."
		@ "  <color:" @ $MRPGChat::Col::Hint @ ">]  and  [  switch channel."
		@ "  /g and /l send to Global or Local, /w to whisper."
		@ "  Free the mouse and click a name for their profile.");

	// Tell the router this client can take split lines. Until it hears this the
	// server falls back to plain messageClient, so a player WITHOUT this add-on
	// still sees chat - just in the stock feed.
	commandToServer('MRPGChatHello', 1);
}

function MRPGChat_Disable()
{
	cancel($MRPGChat_TickSch);
	$MRPGChat_TickSch = "";

	if($MRPGChat_On)
	{
		MRPGChat_ParkStockChat(0);
		MRPGChat_ShowPaintBox(1);
		MRPGChat_RestoreInputProfile();
	}

	$MRPGChat_On     = 0;
	$MRPGChat_Typing = 0;
	$MRPGChat_Hover  = -1;

	if(isObject(MRPGChatPanel))
		MRPGChatPanel.setVisible(0);

	MRPGChat_ClearAll();

	// Put the stock input box back under the stock feed. updatePosition reads
	// newChatText's position, which MRPGChat_ParkStockChat has just restored.
	if(isObject(newMessageHud))
		newMessageHud.updatePosition();
}


//////////////////////////////////
///// STOCK CHAT / PAINT BOX /////
//////////////////////////////////

// Capture the stock positions ONCE, on the first join, before anything of ours
// has touched them. Re-capturing on every join would eventually record our own
// offscreen values and turn the restore into a no-op.
function MRPGChat_SaveStockLayout()
{
	if($MRPGChat_Saved)
		return;

	// newChatText is the one that matters, and NOT capturing it is the only
	// failure worth guarding: parking without an original to go back to would
	// restore the stock feed to 0,0 - top-left corner, over the scoreboard -
	// which looks like a bug that came from nowhere three servers later. If it
	// is somehow not up yet, leave $MRPGChat_Saved clear so the next Enable
	// tries again, and skip parking entirely this time round.
	if(!isObject(newChatText))
		return;

	$MRPGChat_OrigChatPos = newChatText.getPosition();

	if(isObject(chatWhosTalkingText))
		$MRPGChat_OrigTalkPos = chatWhosTalkingText.getPosition();
	if(isObject(chatScrollDownIndicator))
		$MRPGChat_OrigScrollPos = chatScrollDownIndicator.getPosition();

	$MRPGChat_Saved = 1;
}

// MOVE WITH resize(), NEVER setPosition().
//
// GuiControl::setPosition DOES NOT EXIST IN BLOCKLAND. The only setPosition in
// the binary belongs to PathCamera; the console call just fails and returns, so
// the control never moves and there is no visible error to chase. This cost a
// round trip: the stock feed stayed in the top-left corner and the chat log
// rendered top-aligned, and both were the same dead call.
//
// resize(x, y, w, h) is the real API - 124 uses across this add-on alone -
// so geometry changes go through it and the extent is carried over unchanged.
//
// A restore with no captured original is refused rather than guessed; see
// MRPGChat_SaveStockLayout for why a guess of 0,0 is the bad outcome here.
function MRPGChat_MoveTo(%ctrl, %x, %y)
{
	if(!isObject(%ctrl))
		return;

	%e = %ctrl.getExtent();
	%ctrl.resize(%x, %y, getWord(%e, 0), getWord(%e, 1));
}

function MRPGChat_ParkOne(%ctrl, %park, %orig)
{
	if(!isObject(%ctrl))
		return;

	if(%park)
	{
		MRPGChat_MoveTo(%ctrl, -9000, -9000);
		return;
	}

	if(%orig $= "")
		return;

	MRPGChat_MoveTo(%ctrl, getWord(%orig, 0), getWord(%orig, 1));
}

// %park = 1 moves the stock feed offscreen, 0 puts it back. See the file header
// for why this is a move and not a setVisible.
function MRPGChat_ParkStockChat(%park)
{
	if(!$MRPGChat_Saved)
		return;

	MRPGChat_ParkOne(newChatText,             %park, $MRPGChat_OrigChatPos);
	MRPGChat_ParkOne(chatWhosTalkingText,     %park, $MRPGChat_OrigTalkPos);
	MRPGChat_ParkOne(chatScrollDownIndicator, %park, $MRPGChat_OrigScrollPos);

	// MouseToolTip is a PlayGui child, not a chat child, so parking the feed does
	// not take it along - it would keep popping the "press M for links" hint over
	// an empty corner. displayLatest re-shows it on its own, hence the re-hide in
	// MRPGChat_Push.
	if(%park && isObject(MouseToolTip))
		MouseToolTip.setVisible(0);
}

// The paint box lives in the bottom-left corner this panel now owns. It is
// rebuilt wholesale by PlayGui::loadPaint and un-hidden by
// BrickSelectorDlg::onSleep, so both are packaged below to re-apply this.
function MRPGChat_ShowPaintBox(%show)
{
	if(isObject(HUD_PaintBox))
		HUD_PaintBox.setVisible(%show);
	if(isObject(HUD_PaintNameBG))
		HUD_PaintNameBG.setVisible(%show);
	if(isObject(HUD_PaintName))
		HUD_PaintName.setVisible(%show);

	// ToolTip_Paint is a child of HUD_PaintBox so it follows it down, but it is
	// also independently owned by $pref::HUD::showToolTips - leave that alone on
	// the way back up so we never force a tip the player turned off.
	if(!%show && isObject(ToolTip_Paint))
		ToolTip_Paint.setVisible(0);
}


//////////////////////////////////
////////// THE BUFFERS ///////////
//////////////////////////////////
//
// One ring per tab. Ptr is the next write slot; Count saturates at Scrollback.
//
// EACH ENTRY IS STORED IN TWO HALVES, and that is not arbitrary - it is what
// makes the hanging indent work. <lmargin:N> only moves the CURRENT line if the
// cursor is still left of N, so a margin set at the very start of an entry would
// indent the first line too and there would be no hang at all. Setting it after
// the speaker's name means the first line is already past N and only the wrapped
// continuation lines pick the margin up.
//
//   A = "(Fame Title) Alexander"      <- emitted at margin 0
//   B = ": the rest of the sentence"  <- emitted after <lmargin:18>
//
// Keeping the halves apart (rather than baking the tag in at push time) also
// means a resolution change just re-renders at the new indent.

function MRPGChat_ClearAll()
{
	for(%t = 0; %t < $MRPGChat::TabCount; %t++)
	{
		$MRPGChat_Ptr[%t]     = 0;
		$MRPGChat_Count[%t]   = 0;
		$MRPGChat_Unread[%t]  = 0;
		$MRPGChat_UnreadN[%t] = 0;
		$MRPGChat_Mention[%t] = 0;
		for(%i = 0; %i < $MRPGChat::Scrollback; %i++)
		{
			$MRPGChat_LineA[%t, %i] = "";
			$MRPGChat_LineB[%t, %i] = "";
		}
	}
	$MRPGChat_ScrollPx = 0;

	if($MRPGChat_Built)
	{
		MRPGChat_Render();
		MRPGChat_MarkUnread();
		MRPGChat_RenderNotify();
	}
}

function MRPGChat_Push(%tab, %a, %b, %mention)
{
	if(!$MRPGChat_On)
		return;
	if(%a $= "" && %b $= "")
		return;
	if(%tab < 0 || %tab >= $MRPGChat::TabCount)
		%tab = $MRPGChat::TAB_EVENTS;

	// A newline inside one entry would break the per-entry indent. The stock feed
	// strips them for the same reason.
	%a = strReplace(%a, "\n", " ");
	%b = strReplace(%b, "\n", " ");

	%p = $MRPGChat_Ptr[%tab];
	$MRPGChat_LineA[%tab, %p] = %a;
	$MRPGChat_LineB[%tab, %p] = %b;
	$MRPGChat_Ptr[%tab] = (%p + 1) % $MRPGChat::Scrollback;
	if($MRPGChat_Count[%tab] < $MRPGChat::Scrollback)
		$MRPGChat_Count[%tab]++;

	// SEEN means the line actually landed in front of the player: the right tab
	// AND the chat actually on screen. Without the mode test, anything arriving
	// on the active tab while chat was hidden would be silently marked read and
	// the notify bar would stay empty - which defeats the middle stage entirely.
	%seen = (%tab == $MRPGChat_Tab)
	     && ($MRPGChat_Mode == $MRPGChat::MODE_FULL);

	if(%seen)
	{
		// If the player is scrolled back, leave them there. Yanking the view out
		// from under someone mid-scrollback is the single most irritating thing a
		// chat window can do; MRPGChatMore tells them there is new text below.
		MRPGChat_Render();
	}
	else
	{
		$MRPGChat_Unread[%tab]  = 1;
		$MRPGChat_UnreadN[%tab] = $MRPGChat_UnreadN[%tab] + 1;
		if(%mention)
			$MRPGChat_Mention[%tab] = 1;

		MRPGChat_MarkUnread();
		MRPGChat_RenderNotify();

		// Still rebuild the log for the active tab even when it is not on screen,
		// so switching back to FULL shows the backlog immediately rather than one
		// message later.
		if(%tab == $MRPGChat_Tab)
			MRPGChat_Render();
	}

	if(isObject(MouseToolTip))
		MouseToolTip.setVisible(0);
}

// Events arrive as one opaque, already-coloured string, so they get a quiet
// bullet as their first half - which both gives the indent something to hang
// from and makes a run of events read as a list rather than a wall.
function MRPGChat_PushEvent(%line)
{
	MRPGChat_Push($MRPGChat::TAB_EVENTS,
		"<color:" @ $MRPGChat::Col::Bullet @ ">-  ",
		"<color:" @ $MRPGChat::Col::Event @ ">" @ %line,
		MRPGChat_IsMention(%line));
}


//////////////////////////////////
////////// RENDERING /////////////
//////////////////////////////////

function MRPGChat_Render()
{
	if(!$MRPGChat_Built || !isObject(MRPGChatText))
		return;

	%tab   = $MRPGChat_Tab;
	%count = $MRPGChat_Count[%tab];
	%ptr   = $MRPGChat_Ptr[%tab];
	%ind   = MRPGChat_Px($MRPGChat::Indent);
	%size  = MRPGChat_Px($MRPGChat::FontSize);

	%buff = "<font:verdana:" @ %size @ ">";
	for(%i = %count - 1; %i >= 0; %i--)
	{
		%pos = %ptr - 1 - %i;
		while(%pos < 0)
			%pos = %pos + $MRPGChat::Scrollback;

		%a = $MRPGChat_LineA[%tab, %pos];
		%b = $MRPGChat_LineB[%tab, %pos];
		if(%a $= "" && %b $= "")
			continue;

		// lmargin is running parser state, not a per-line attribute, so it has to
		// be reset to 0 at the head of every entry or the indent compounds.
		%buff = %buff @ "<lmargin:0>" @ %a @ "<lmargin:" @ %ind @ ">" @ %b @ "\n";
	}

	if(%count == 0)
		%buff = %buff @ "<lmargin:0><color:" @ $MRPGChat::Col::Hint @ ">"
			@ MRPGChat_EmptyText(%tab);

	MRPGChatText.setText(%buff);
	MRPGChat_Anchor();
}

function MRPGChat_EmptyText(%tab)
{
	if(%tab == $MRPGChat::TAB_LOCAL)
		return "Nobody has spoken nearby. Local reaches " @ $MRPGChat::LocalRadius @ " units.";
	if(%tab == $MRPGChat::TAB_EVENTS)
		return "Nothing has happened yet.";
	return "No messages yet. Press T to say something.";
}

// Pin the newest line to the BOTTOM of the clip window by giving the text control
// a negative y, then let $MRPGChat_ScrollPx lift it from there.
function MRPGChat_Anchor()
{
	if(!$MRPGChat_Built || !isObject(MRPGChatText))
		return;

	// forceReflow is what makes autoResize update the extent we are about to
	// read. The engine rejects it on a control that is not showing, so this is
	// guarded - and a panel that is not up has nothing to anchor anyway.
	if($MRPGChat_On && MRPGChatText.isAwake())
		MRPGChatText.forceReflow();

	%textH = getWord(MRPGChatText.getExtent(), 1);
	%clipH = getWord(MRPGChatClip.getExtent(), 1);
	%over  = %textH - %clipH;
	if(%over < 0){ %over = 0; }

	// HOLD THE READER'S PLACE WHEN NEW TEXT ARRIVES.
	//
	// ScrollPx is measured UP FROM THE BOTTOM of the log, so a new line growing
	// the log by one line height also moves everything above it up by that much -
	// someone reading scrollback would be dragged forward a line per message,
	// which is worse than either pinning or not scrolling at all. Absorbing the
	// growth into ScrollPx keeps the same text under their eyes; the chevron in
	// the gutter is what tells them there is something new below.
	//
	// Only while they are actually scrolled back. At ScrollPx 0 the view is
	// pinned to the newest line and must stay there.
	if($MRPGChat_ScrollPx > 0 && $MRPGChat_LastOver !$= "" && %over > $MRPGChat_LastOver)
		$MRPGChat_ScrollPx = $MRPGChat_ScrollPx + (%over - $MRPGChat_LastOver);

	$MRPGChat_LastOver = %over;

	if($MRPGChat_ScrollPx > %over){ $MRPGChat_ScrollPx = %over; }
	if($MRPGChat_ScrollPx < 0){ $MRPGChat_ScrollPx = 0; }

	// TWO CASES, and the first one is the one that was missing.
	//
	// Log SHORTER than the window: there is nothing to clip, so the text has to
	// be pushed DOWN to sit on the bottom edge. %over is 0 here, so the overflow
	// formula below would leave it at y=0 - the top - which is what put a single
	// "Disty: Test" at the top of an otherwise empty panel.
	//
	// Log LONGER than the window: lift it by the overflow so the newest line
	// lands on the bottom edge, then let ScrollPx raise it further.
	if(%over <= 0)
	{
		MRPGChat_MoveTo(MRPGChatText, 0, %clipH - %textH);
		MRPGChatThumb.setVisible(0);
		MRPGChatMore.setVisible(0);
		return;
	}

	MRPGChat_MoveTo(MRPGChatText, 0, $MRPGChat_ScrollPx - %over);

	// Thumb height is proportional to how much of the log is on screen; it runs
	// the opposite way to ScrollPx, which counts UP from the bottom.
	// Read the clip's own y rather than re-deriving the padding: the thumb and the
	// clip are siblings inside MRPGChatField, so this cannot fall out of step with
	// MRPGChat_Layout the way a second copy of the constant just did.
	%padV = getWord(MRPGChatClip.getPosition(), 1);
	%thH  = mFloor(%clipH * (%clipH / %textH));
	if(%thH < MRPGChat_Px(14)){ %thH = MRPGChat_Px(14); }

	%frac = 1 - ($MRPGChat_ScrollPx / %over);
	MRPGChatThumb.resize(getWord(MRPGChatThumb.getPosition(), 0),
		%padV + mFloor((%clipH - %thH) * %frac),
		getWord(MRPGChatThumb.getExtent(), 0), %thH);
	MRPGChatThumb.setVisible(1);

	if($MRPGChat_ScrollPx > 0)
	{
		MRPGChatMore.setText("<just:center><font:verdana bold:" @ MRPGChat_Px(12)
			@ "><color:" @ $MRPGChat::Col::Gold @ ">v");
		MRPGChatMore.setVisible(1);
	}
	else
		MRPGChatMore.setVisible(0);
}

// One "line" is a font line plus its spacing, both at the current UI scale - so a
// PgUp moves the same proportion of the window at every resolution.
function MRPGChat_Scroll(%lines)
{
	if(!$MRPGChat_On)
		return;

	$MRPGChat_ScrollPx = $MRPGChat_ScrollPx
		+ MRPGChat_Px($MRPGChat::FontSize + $MRPGChat::LineGap) * %lines;
	MRPGChat_Anchor();
}


//////////////////////////////////
//////////// TABS ////////////////
//////////////////////////////////

function MRPGChat_SetTab(%i)
{
	if(!$MRPGChat_Built)
		return;
	if(%i < 0){ %i = $MRPGChat::TabCount - 1; }
	if(%i >= $MRPGChat::TabCount){ %i = 0; }

	$MRPGChat_Tab      = %i;
	$MRPGChat_ScrollPx = 0;
	MRPGChat_ClearUnread(%i);

	for(%t = 0; %t < $MRPGChat::TabCount; %t++)
	{
		%plate = "MRPGChatTab"    @ %t;
		%lbl   = "MRPGChatTabLbl" @ %t;
		%sel   = (%t == %i);

		%plate.setBitmap($MRPGChat::Btn @ (%sel ? "Button_middle" : "Button_middle_dark"));

		// Selection is already carried by the plate art, so the colour here is
		// reinforcement rather than the only signal.
		%col = %sel ? $MRPGChat_TabHue[%t] : $MRPGChat::Col::Prefix;
		%lbl.setText("<just:center><font:verdana bold:" @ MRPGChat_Px(13)
			@ "><color:" @ %col @ ">" @ $MRPGChat_TabName[%t]);
	}

	// The accent bar under the tabs carries the selected channel's colour, so the
	// log itself says which channel you are reading even when the tab row is
	// clipped or you are looking at the bottom of the panel.
	if(isObject(MRPGChatAccent))
		MRPGChat_TintAccent($MRPGChat_TabHue[%i]);

	MRPGChat_MarkUnread();
	MRPGChat_Render();
	MRPGChat_ShowHint();

	// Keep the badge on an open input box in step, so switching tabs mid-sentence
	// still shows where the sentence is going.
	if($MRPGChat_Typing)
		MRPGChat_SetChannelBadge();
}

// The tab hues are hex (they feed <color:> tags); a GuiSwatchCtrl wants "r g b a".
// Converting here keeps ONE definition of each channel's colour instead of a
// second table that would quietly fall out of step with the first.
function MRPGChat_TintAccent(%hex)
{
	MRPGChatAccent.setColor(MRPGChat_RGB(%hex, 210));
}

// "RRGGBB" + alpha -> the "r g b a" a GuiSwatchCtrl wants.
function MRPGChat_RGB(%hex, %alpha)
{
	return MRPGChat_HexByte(getSubStr(%hex, 0, 2))
	   SPC MRPGChat_HexByte(getSubStr(%hex, 2, 2))
	   SPC MRPGChat_HexByte(getSubStr(%hex, 4, 2))
	   SPC %alpha;
}

function MRPGChat_HexByte(%pair)
{
	return MRPGChat_HexDigit(getSubStr(%pair, 0, 1)) * 16
	     + MRPGChat_HexDigit(getSubStr(%pair, 1, 1));
}

function MRPGChat_HexDigit(%c)
{
	%i = strpos("0123456789ABCDEF", strupr(%c));
	return (%i < 0) ? 0 : %i;
}

function MRPGChat_MarkUnread()
{
	if(!$MRPGChat_Built)
		return;
	for(%t = 0; %t < $MRPGChat::TabCount; %t++)
	{
		%dot = "MRPGChatTabDot" @ %t;
		%dot.setVisible($MRPGChat_Unread[%t] && %t != $MRPGChat_Tab);
	}
}

function MRPGChat_CycleTab(%dir)
{
	if(!$MRPGChat_On)
		return;

	// Cycling a tab you cannot see would silently mark it read and throw the
	// notification away, which is the opposite of what the other two stages are
	// for. Bring the chat back first.
	if($MRPGChat_Mode != $MRPGChat::MODE_FULL)
	{
		MRPGChat_SetMode($MRPGChat::MODE_FULL);
		return;
	}

	MRPGChat_SetTab($MRPGChat_Tab + %dir);
}


//////////////////////////////////
////////// THE MODES /////////////
//////////////////////////////////

function MRPGChat_ToggleMode(%val)
{
	if(!%val || !$MRPGChat_On)
		return;
	MRPGChat_SetMode(($MRPGChat_Mode + 1) % $MRPGChat::ModeCount);
}

function MRPGChat_SetMode(%mode)
{
	if(!$MRPGChat_Built)
		return;
	if(%mode < 0 || %mode >= $MRPGChat::ModeCount)
		%mode = $MRPGChat::MODE_FULL;

	$MRPGChat_Mode = %mode;
	MRPGChat_ApplyMode();
}

// Visibility flip only - nothing is created or destroyed here, so switching
// stages costs a handful of setVisible calls and one re-layout.
function MRPGChat_ApplyMode()
{
	if(!$MRPGChat_Built)
		return;

	%full   = ($MRPGChat_Mode == $MRPGChat::MODE_FULL);
	%notify = ($MRPGChat_Mode == $MRPGChat::MODE_NOTIFY);

	MRPGChatTabRail.setVisible(%full);
	MRPGChatFieldEdge.setVisible(%full);
	MRPGChatInputEdge.setVisible(%full);
	MRPGChatCatch.setVisible(%full);

	MRPGChatNotifyEdge.setVisible(%notify);
	MRPGChatCatchNotify.setVisible(%notify);

	MRPGChatPanel.setVisible($MRPGChat_On
		&& $MRPGChat_Mode != $MRPGChat::MODE_HIDDEN);

	// Coming back to FULL means you are looking at the current tab again, so its
	// backlog is read. The other two keep counting.
	if(%full)
		MRPGChat_ClearUnread($MRPGChat_Tab);

	MRPGChat_Layout();
	MRPGChat_RenderNotify();
}

function MRPGChat_ClearUnread(%t)
{
	$MRPGChat_Unread[%t]   = 0;
	$MRPGChat_UnreadN[%t]  = 0;
	$MRPGChat_Mention[%t]  = 0;
	MRPGChat_MarkUnread();
	MRPGChat_RenderNotify();
}

// Does this line name the player? Whispers count on their own (they are
// addressed to you by definition), everything else has to actually say the name.
//
// Deliberately NOT "any event about you": most Events lines start with "You",
// so treating those as mentions would light the badge permanently and the gold
// state would stop meaning anything.
function MRPGChat_IsMention(%text)
{
	%me = $Pref::Player::NetName;
	if(%me $= "" || %text $= "")
		return 0;
	return stripos(%text, %me) >= 0;
}

function MRPGChat_RenderNotify()
{
	if(!$MRPGChat_Built)
		return;

	%total    = 0;
	%mentions = 0;

	for(%t = 0; %t < $MRPGChat::TabCount; %t++)
	{
		%n   = $MRPGChat_UnreadN[%t];
		%hot = $MRPGChat_Mention[%t];
		%total = %total + %n;
		if(%hot){ %mentions++; }

		%badge = "MRPGChatBadge"    @ %t;
		%lbl   = "MRPGChatBadgeLbl" @ %t;
		%ring  = "MRPGChatBadgeRing" @ %t;

		%ring.setVisible(%hot);

		if(%n > 0)
		{
			// Lit: the channel's own colour, with the glyph knocked out dark so it
			// stays legible against it.
			%badge.setColor(MRPGChat_RGB($MRPGChat_TabHue[%t], 235));
			%col = "1A150E";
		}
		else
		{
			%badge.setColor("26 21 14 235");
			%col = $MRPGChat::Col::Hint;
		}

		%lbl.setText("<just:center><font:verdana bold:" @ MRPGChat_Px(12)
			@ "><color:" @ %col @ ">" @ $MRPGChat_TabGlyph[%t]);
	}

	if(%mentions > 0)
		%txt = "<color:" @ $MRPGChat::Col::Gold @ ">you were mentioned";
	else if(%total > 0)
		%txt = "<color:" @ $MRPGChat::Col::Name @ ">" @ %total
			@ "<color:" @ $MRPGChat::Col::Hint @ ">" @ ((%total == 1) ? " new message" : " new messages");
	else
		%txt = "<color:" @ $MRPGChat::Col::Hint @ ">no new messages";

	MRPGChatNotifyText.setText("<font:verdana:" @ MRPGChat_Px(12) @ ">" @ %txt);
}

// The channel a message typed right now would go to. Events is a read-only view,
// so it falls through to Global rather than swallowing what you type.
function MRPGChat_SendChannel()
{
	if($MRPGChat_Tab == $MRPGChat::TAB_LOCAL)
		return "Local";
	return "Global";
}

function MRPGChat_ShowHint()
{
	if(!$MRPGChat_Built || $MRPGChat_Typing)
		return;

	%ch  = MRPGChat_SendChannel();
	%hue = (%ch $= "Local") ? $MRPGChat_TabHue[1] : $MRPGChat_TabHue[0];

	// The free-mouse key is LOOKED UP, not hardcoded: it is remappable, and a hint
	// line that confidently names the wrong key is worse than no hint at all.
	%mouseKey = strupr(getWords(moveMap.getBinding("MRPG_FreeMouse"), 1,
		getWordCount(moveMap.getBinding("MRPG_FreeMouse")) - 1));
	%mouseHint = (%mouseKey $= "") ? "" : ("   " @ %mouseKey @ " mouse");

	%hideKey = strupr(getWords(moveMap.getBinding("MRPGChat_ToggleMode"), 1,
		getWordCount(moveMap.getBinding("MRPGChat_ToggleMode")) - 1));
	%hideHint = (%hideKey $= "") ? "" : ("   " @ %hideKey @ " hide");

	MRPGChatHint.setText("<font:verdana:" @ MRPGChat_Px(12)
		@ "><color:" @ $MRPGChat::Col::Hint @ ">T to speak in <color:" @ %hue @ ">"
		@ %ch @ "<color:" @ $MRPGChat::Col::Hint
		@ ">   [ ] channel" @ %mouseHint @ %hideHint);
}


//////////////////////////////////
////// HOVER / RESOLUTION ////////
//////////////////////////////////

function MRPGChat_TabAt()
{
	if(!$MRPGChat_On || !isObject(MRPGChatPanel))
		return -1;

	%cur = Canvas.getCursorPos();
	%cx  = getWord(%cur, 0);
	%cy  = getWord(%cur, 1);

	for(%i = 0; %i < $MRPGChat::TabCount; %i++)
	{
		%pl = "MRPGChatTab" @ %i;
		if(!isObject(%pl))
			continue;

		%p = %pl.getCanvasPosition();   // script helper, Support.cs
		%e = %pl.getExtent();
		%x = getWord(%p, 0);
		%y = getWord(%p, 1);
		if(%cx >= %x && %cx < %x + getWord(%e, 0)
		&& %cy >= %y && %cy < %y + getWord(%e, 1))
			return %i;
	}
	return -1;
}

// Hover is POLLED, not event-driven: onMouseMove on a GuiMouseEventCtrl was not
// reliable in this build - the same finding CharacterScreen.cs documents - and
// the poll doubles as the resolution watch, which Blockland gives no client-side
// callback for either.
function MRPGChat_Tick()
{
	cancel($MRPGChat_TickSch);
	//Gate, and do NOT reschedule when it is shut - see ServerGate.cs rule 3.
	if(!$MRPGChat_On || !MRPG_isActive())
		return;

	$MRPGChat_TickSch = schedule($MRPGChat::TickMs, 0, MRPGChat_Tick);

	if(getRes() !$= $MRPGChat_LastRes)
		MRPGChat_Layout();

	%i = Canvas.isCursorOn() ? MRPGChat_TabAt() : -1;
	if(%i != $MRPGChat_Hover)
	{
		if($MRPGChat_Hover >= 0)
		{
			%old = "MRPGChatTabFr" @ $MRPGChat_Hover;
			if(isObject(%old))
				%old.setVisible(0);
		}
		$MRPGChat_Hover = %i;
		if(%i >= 0)
		{
			// NOT %new - "new" is a TorqueScript keyword and is not worth finding
			// out the hard way whether the lexer minds it behind a %.
			%hot = "MRPGChatTabFr" @ %i;
			%hot.setVisible(1);
		}
	}
}

function MRPGChatCatchNotify::onMouseDown(%this)
{
	MRPGChat_SetMode($MRPGChat::MODE_FULL);
}

function MRPGChatCatch::onMouseDown(%this)
{
	%i = MRPGChat_TabAt();
	if(%i >= 0)
		MRPGChat_SetTab(%i);
}


//////////////////////////////////
////////// KEYBINDS //////////////
//////////////////////////////////
//
// ] and [ are the only adjacent pair left free. Stock takes w/a/s/d, space,
// lshift, c, f, z, tab, t, y, q, e, m, 0-9 and pageup/pagedown, and MonsterRPG
// has since claimed j (attributes), k (tree), m (map) and n (character).
// Registered in $remap so both show up in Options > Keyboard and can be moved.
//
// SCROLLING IS PgUp / PgDn, NOT THE WHEEL, and that is a hard engine limit rather
// than a choice: GuiMouseEventCtrl sends onMouseDown/Up/Move/Dragged/Enter/Leave
// and their right-button twins, and nothing else - there is no wheel callback to
// hook (verified against the binary; no onMouseWheel* symbol exists). The wheel
// stays on the inventory, where the player expects it.

function MRPGChat_NextTab(%val) { if(%val) MRPGChat_CycleTab(1);  }
function MRPGChat_PrevTab(%val) { if(%val) MRPGChat_CycleTab(-1); }

// THE THREE CHAT KEYS ARE BOUND BY THE BROKER, not here. ] [ and \ are borrowed
// on join and handed back on leave with the rest of the MonsterRPG set - see
// SCRIPTS/Client/Keybinds.cs, which is the only place a MonsterRPG key is named.


//////////////////////////////////
////////// THE INPUT /////////////
//////////////////////////////////
//
// The stock newMessageHud is REUSED rather than replaced. It is a dialog, so it
// owns the keyboard while it is up and hands WASD back when it closes - which an
// always-focused text box parented into the HUD could never do without eating
// movement.  All we change is where it sits and where its text is sent.
//
// NMH_Box's coordinates are canvas pixels (its parent is stretched to the
// canvas), so the panel's canvas position drops straight in.

// Lay the stock input box over our typing field.
//
// It is placed against MRPGChatInput's REAL canvas rect rather than derived from
// the panel's height, so the two cannot drift apart when the layout constants
// change - which they already did once when the input gained its own box.
function MRPGChat_PlaceInput()
{
	if(!$MRPGChat_Built || !isObject(NMH_Box) || !isObject(MRPGChatInput))
		return;

	%p = MRPGChatInput.getCanvasPosition();
	%e = MRPGChatInput.getExtent();
	%w = getWord(%e, 0);
	%h = getWord(%e, 1);

	NMH_Box.resize(getWord(%p, 0), getWord(%p, 1), %w, %h);

	// Channel badge on the left, edit field filling the rest. The badge is sized
	// to the longest label it can hold ("Global:" / "Party:") rather than
	// measured, because NMH_Channel's pixel width is only valid after a render
	// and updateTypePosition's whole reason for existing was to wait for that.
	%pad    = MRPGChat_Px(9);
	%badgeW = MRPGChat_Px(62);
	NMH_Channel.resize(%pad, 0, %badgeW, %h);
	NMH_Type.resize(%pad + %badgeW, 0, %w - %badgeW - %pad * 2, %h);
}

// Bright hairline while the box is open, dim when it is not.
//
// This is the cheapest legibility win available: it tells you at a glance that
// the game is taking your keystrokes, which on a HUD with no other focus
// indicator is otherwise pure guesswork.
function MRPGChat_InputFocus(%on)
{
	if(!$MRPGChat_Built || !isObject(MRPGChatInputEdge))
		return;

	MRPGChatInputEdge.setColor(%on ? $MRPGChat::Col::LineHot : $MRPGChat::Col::Line);
}

// A bigger, warmer face for the text being typed.
//
// THE FONT SIZE IS BAKED INTO THE PROFILE, so it has to be rebuilt when the UI
// scale changes: GuiControlProfile loads its GFont in incRefCount() and caches
// it, and writing fontSize afterwards does nothing on its own. Bouncing the
// control off the profile and back drops the refcount to zero, which frees the
// cached font and forces a reload at the new size.
function MRPGChat_ApplyInputProfile()
{
	if(!isObject(NMH_Type))
		return;

	%size = MRPGChat_Px(17);

	//////////////////////////////////////////////////////////////////////
	// WHY THIS RE-CREATES THE PROFILE INSTEAD OF EDITING IT.
	//
	// A GuiControlProfile is REFCOUNTED, and the engine destroys it when the
	// last control lets go. The size-change path below deliberately parks
	// NMH_Type on GuiDefaultProfile so the old font is freed - and that drop
	// can take the profile object with it.
	//
	// The previous version then did setProfile(MRPGChat_InputProfile) on the
	// next line regardless. When the object really had been destroyed, that
	// assigned nothing, the control fell back to the default profile, and
	// GuiTextEditProfile's default fontColor is BLACK - typed chat became
	// unreadable against the dark input box.
	//
	// It only ever bit on the size-change branch, which is why it was
	// intermittent: MRPGChat_Px scales with resolution, so it needed a
	// resolution change or a chat-size pref change to fire at all.
	//
	// So: release, delete deterministically rather than trusting the refcount,
	// then rebuild. After this block the profile is guaranteed to exist AND to
	// carry the right size, which is what the caller actually needs.
	//////////////////////////////////////////////////////////////////////
	if(isObject(MRPGChat_InputProfile) && MRPGChat_InputProfile.fontSize != %size)
	{
		NMH_Type.setProfile(GuiDefaultProfile);   // refcount -> 0, font freed

		if(isObject(MRPGChat_InputProfile))
			MRPGChat_InputProfile.delete();
	}

	if(!isObject(MRPGChat_InputProfile))
	{
		new GuiControlProfile(MRPGChat_InputProfile : GuiTextEditProfile)
		{
			fontType    = "Verdana Bold";
			fontSize    = %size;
			fontColor   = "255 250 236 255";
			fontColorHL = "24 18 10 255";
			fontColorSEL= "201 162 78 255";

			// fontColorNA WAS THE ONE SLOT LEFT INHERITED, and it is the slot a
			// GuiTextEditCtrl draws with whenever the control is not active -
			// drawText picks mFontColorNA over mFontColor on exactly that test.
			// Inherited from GuiTextEditProfile it is a light grey meant for a
			// WHITE field, and stock's own edit profiles put black there. Either
			// way it is not a colour this dark box can show, and leaving it to
			// inheritance is what made the colour depend on which profile the
			// control happened to come from. Pinned to the active colour: the
			// text is legible whatever the engine decides about mActive.
			fontColorNA = "255 250 236 255";

			cursorColor = "232 184 92 255";
			justify     = "Left";
			opaque      = 0;      // the box behind it is ours
			border      = 0;
			textOffset  = "4 0";
		};
	}

	NMH_Type.setProfile(MRPGChat_InputProfile);

	//The channel badge (Global: / Local: / Party:) is a SEPARATE control and was
	//never given a profile of ours - it kept whatever stock left on it, which is
	//sized for the stock chat and coloured for a light background. It sits on the
	//same dark input box as the text above, so it gets the same treatment.
	MRPGChat_ApplyChannelProfile(%size);
}

//////////////////////////////////////////////////////////////////////////////
// The channel badge profile. Same refcount rule as above - see that comment.
//
// Separate profile rather than sharing the input's: this control is a plain
// GuiTextCtrl, and handing it a GuiTextEditProfile brings along caret and
// selection colours that mean nothing here and a background it should not draw.
//////////////////////////////////////////////////////////////////////////////
function MRPGChat_ApplyChannelProfile(%size)
{
	if(!isObject(NMH_Channel))
		return;

	if(isObject(MRPGChat_ChannelProfile) && MRPGChat_ChannelProfile.fontSize != %size)
	{
		NMH_Channel.setProfile(GuiDefaultProfile);

		if(isObject(MRPGChat_ChannelProfile))
			MRPGChat_ChannelProfile.delete();
	}

	if(!isObject(MRPGChat_ChannelProfile))
	{
		new GuiControlProfile(MRPGChat_ChannelProfile : GuiTextProfile)
		{
			fontType  = "Verdana Bold";
			fontSize  = %size;

			//////////////////////////////////////////////////////////////
			// THIS IS THE OTHER HALF OF THE BLACK-TEXT BUG, and it is not
			// the same cause as the input box above.
			//
			// Stock BlockChatChannelProfile - what this control was left on -
			// sets fontColor = "0 0 0". Literally black. It gets away with it
			// because it also sets doFontOutline with a black outline, which
			// reads on the light stock chat background. On MonsterRPG's dark
			// input box that is black on black.
			//
			// AND \c CODES ARE PER-PROFILE. fontColors[0..9] is the table the
			// \cN escapes index, and stock defines only [0] and [1]. The badge
			// text uses \c0, \c2 and \c3 - so two of the three channels were
			// indexing an entry that does not exist.
			//
			// Both are fixed here: a light base colour, and the whole table
			// defined so no escape can land on an undefined slot.
			//////////////////////////////////////////////////////////////
			fontColor = "255 250 236 255";

			// Same inherited-slot hole the input profile had - GuiTextCtrl falls
			// back to mFontColorNA the moment the control is inactive, and stock
			// leaves black there. Pinned rather than inherited.
			fontColorNA = "255 250 236 255";

			fontColors[0] = "255 250 236";   // GLOBAL - the default, near white
			fontColors[1] = "232 184  92";   // gold, matches the input caret
			fontColors[2] = "126 204 255";   // PARTY - cool blue, reads as "us"
			fontColors[3] = "150 226 150";   // LOCAL - green, reads as "here"
			fontColors[4] = "255 170  90";
			fontColors[5] = "255 120 120";
			fontColors[6] = "200 160 255";
			fontColors[7] = "180 180 180";
			fontColors[8] = "255 255 255";
			fontColors[9] = "255 250 236";   // NOT black - stock uses black here

			justify   = "Right";
			opaque    = 0;
			border    = 0;
		};
	}

	NMH_Channel.setProfile(MRPGChat_ChannelProfile);
}

// Put the stock chat profile back so the input looks normal off this server.
function MRPGChat_RestoreInputProfile()
{
	if(!isObject(NMH_Type))
		return;

	%n = $Pref::Gui::ChatSize;
	if(%n $= "" || %n < 0 || %n > 10)
		%n = 1;

	NMH_Type.setProfile("HUDChatTextEditSize" @ %n @ "Profile");

	if(isObject(NMH_Channel))
		NMH_Channel.setProfile("BlockChatChannelSize" @ %n @ "Profile");

	//Drop ours now that nothing references them. Not strictly required - the
	//refcount would do it - but doing it here means the next Apply always
	//rebuilds from scratch rather than inheriting a profile the engine may or
	//may not have collected, which is the ambiguity that produced black text in
	//the first place.
	if(isObject(MRPGChat_InputProfile))
		MRPGChat_InputProfile.delete();

	if(isObject(MRPGChat_ChannelProfile))
		MRPGChat_ChannelProfile.delete();
}

function MRPGChat_SetChannelBadge()
{
	if(!isObject(NMH_Channel))
		return;

	// TEAM is party chat here (see newMessageHud::open), and it does not belong
	// to any of the three tabs - label it for what it is rather than lying about
	// where the line is going.
	if(newMessageHud.channel $= "TEAM")
	{
		NMH_Channel.setText("\c2Party:");
		return;
	}

	// NMH_Channel is a GuiTextCtrl, so this is \c-code territory rather than
	// <color:>. \c0 and \c3 are the profile's blue and green slots - close enough
	// to the tab hues to read as the same channel without a profile of its own.
	%ch = MRPGChat_SendChannel();
	NMH_Channel.setText((%ch $= "Local" ? "\c3" : "\c0") @ %ch @ ":");
}


//////////////////////////////////
///////// INCOMING LINES /////////
//////////////////////////////////

// Player chat, split by the server router so the client owns the presentation.
//   %chan   "Global" or "Local"
//   %prefix clan prefix / fame title, may be empty
//   %name   speaker
//   %msg    the message
function clientCmdMRPGChat(%chan, %prefix, %name, %suffix, %msg)
{
	%plain = %prefix @ %name @ %suffix @ ": " @ %msg;

	if(!$MRPGChat_On)
	{
		// Panel is not up - fall back to the stock feed so nothing is ever lost.
		newChatHud_AddLine(%plain);
		return;
	}

	// Client-side ignore, from the chat name menu. Deliberately checked AFTER the
	// panel test and BEFORE the stock buffer: an ignored line should not reach the
	// log or the chat file.
	if($MRPGChat_Ignore[%name])
		return;

	%tab = (%chan $= "Local") ? $MRPGChat::TAB_LOCAL : $MRPGChat::TAB_GLOBAL;

	// The split is what produces the hanging indent - see the buffer notes above.
	MRPGChat_Push(%tab,
		"<color:" @ $MRPGChat::Col::Prefix @ ">" @ %prefix
			@ MRPGChat_NameLink(%name)
			@ "<color:" @ $MRPGChat::Col::Prefix @ ">" @ %suffix,
		"<color:" @ $MRPGChat::Col::Body @ ">: " @ %msg,
		MRPGChat_IsMention(%msg));

	// Still feed the stock ring buffer: it costs nothing (that feed is parked
	// offscreen), it keeps Client_Chatlogs logging, and it means the stock pageup
	// history is intact if this panel is ever torn down mid-session.
	newChatHud_AddLine(%plain);
}

// Wrap a player name in a clickable MLText link.
//
// THE "gamelink" PREFIX IS LOad-BEARING, not decoration: guiMLTextCtrl.cc only
// suppresses the underline when the URL's first eight characters are "gamelink"
// (mCurURL->noUnderline). Without it every name in the log gets underlined, which
// on a chat feed reads as damage rather than as an affordance.
//
// '>' is stripped because the tag parser scans to the first '>' to end the URL -
// a name containing one would truncate the link and leak markup into the log.
function MRPGChat_NameLink(%name)
{
	%safe = strReplace(%name, ">", "");
	return "<a:gamelink|p|" @ %safe @ ">" @ %name @ "</a>";
}

// Private message, from the name menu or "/w name text".
function clientCmdMRPGChatWhisper(%dir, %who, %text)
{
	if(!$MRPGChat_On)
	{
		newChatHud_AddLine("[" @ %dir @ %who @ "]: " @ %text);
		return;
	}

	if($MRPGChat_Ignore[%who])
		return;

	// Filed into Global rather than a tab of its own: a whisper is server-wide,
	// and a fourth tab that is empty for most of a session is worse than the
	// unread pip doing its job on this one.
	// A whisper is addressed to you by definition, so it is always a mention -
	// no name matching needed, and none would work for one sent as "hey".
	MRPGChat_Push($MRPGChat::TAB_GLOBAL,
		"<color:" @ $MRPGChat::Col::Whisper @ ">[" @ %dir
			@ MRPGChat_NameLink(%who) @ "<color:" @ $MRPGChat::Col::Whisper @ ">]",
		"<color:" @ $MRPGChat::Col::Whisper @ ">: " @ %text,
		(%dir $= "From "));

	newChatHud_AddLine("[" @ %dir @ %who @ "]: " @ %text);
}

// The server's authoritative Local radius, pushed in reply to MRPGChatHello. Our
// own $MRPGChat::LocalRadius is only ever help text, so taking the server's value
// here is what stops the panel promising a range the router does not apply.
function clientCmdMRPGChatConfig(%localRadius)
{
	if(%localRadius > 0)
		$MRPGChat::LocalRadius = %localRadius;

	if($MRPGChat_On)
	{
		MRPGChat_Render();      // the empty-Local placeholder quotes the radius
		MRPGChat_ShowHint();
	}
}

// Server-driven channel switch, used by the router when it moves a player between
// channels or refuses one.
function clientCmdMRPGChatChannel(%chan)
{
	if(!$MRPGChat_On)
		return;

	if(%chan $= "Local")
		MRPGChat_SetTab($MRPGChat::TAB_LOCAL);
	else if(%chan $= "Events")
		MRPGChat_SetTab($MRPGChat::TAB_EVENTS);
	else
		MRPGChat_SetTab($MRPGChat::TAB_GLOBAL);
}


//////////////////////////////////
////////// PACKAGES //////////////
//////////////////////////////////

if(isPackage(MRPGChatPackage))
	deactivatePackage(MRPGChatPackage);

package MRPGChatPackage
{
	// ---- join / leave ----------------------------------------------------

	function clientCmdaddMRPGClientToServer()
	{
		Parent::clientCmdaddMRPGClientToServer();
		MRPGChat_Enable();
	}

	// NO clientCmdaddMonsterRPGGUI WRAPPER ANY MORE.
	//
	// While the panel lived in PlayGui it had to be re-added after that command
	// ran, because PlayGui.add("MonsterRPGx_MAIN_INTERFACE") moves a full-screen
	// swatch to the end of PlayGui's children and buries anything already there.
	// The panel is a child of NewChatHud now - a different canvas child entirely,
	// and one that renders after PlayGui - so the HUD cannot get on top of it and
	// re-adding would only move it back down into the layer that eats clicks.

	// The same three exits Package.cs unwinds the rest of the HUD on. All three
	// are needed: disconnect covers leaving from the menu, disconnectedCleanup
	// covers a kick or timeout, onExit covers quitting straight from the server.
	function disconnect(%a)
	{
		MRPGChat_Disable();
		return Parent::disconnect(%a);
	}

	function disconnectedCleanup(%this)
	{
		%r = Parent::disconnectedCleanup(%this);
		MRPGChat_Disable();
		return %r;
	}

	function onExit()
	{
		MRPGChat_Disable();
		Parent::onExit();
	}

	// ---- incoming text ---------------------------------------------------
	//
	// Everything that is NOT tagged player chat lands in Events. Parent:: still
	// runs so the stock buffer and the chat logger stay correct.

	function onServerMessage(%message)
	{
		if($MRPGChat_On && strLen(%message) > 0)
			MRPGChat_PushEvent(%message);

		Parent::onServerMessage(%message);
	}

	function onChatMessage(%message, %voice, %pitch)
	{
		// Only reachable for chat the router did NOT send - another add-on
		// calling chatMessageAll, say. Filed as an event rather than dropped: an
		// unattributed line in Events beats no line at all.
		if($MRPGChat_On && strLen(%message) > 0)
			MRPGChat_PushEvent(%message);

		Parent::onChatMessage(%message, %voice, %pitch);
	}

	// ---- the input line --------------------------------------------------

	function newMessageHud::open(%this, %channel)
	{
		if(!$MRPGChat_On)
			return Parent::open(%this, %channel);

		//////////////////////////////////////////////////////////////////////
		// THE PROFILE GOES ON BEFORE THE DIALOG IS PUSHED. THIS IS THE
		// BLACK-FIRST-MESSAGE FIX.
		//
		// The symptom was that the FIRST message of a session typed black and
		// every message after it typed correctly. That asymmetry is the whole
		// clue, because the profile values were never in doubt - the second
		// open uses the very same MRPGChat_InputProfile object as the first.
		//
		// What differed was WHEN it was assigned relative to the wake:
		//
		//   open #1  Parent::open pushes the dialog -> NMH_Type wakes still
		//            carrying whatever profile it was left on (its .gui
		//            profile, whose fontColor is black), the profile is
		//            refcounted and its font loaded, and only THEN did
		//            MRPGChat_ApplyInputProfile swap ours in underneath an
		//            already-awake control.
		//   open #2+ the control was popped and slept on OUR profile, so it
		//            wakes on it. Nothing is swapped mid-flight. Correct.
		//
		// So the fix is to make the first open look like the second: assign
		// while the dialog is still asleep, and let it wake already wearing
		// our profile. A sleeping control is also the SAFE moment for the
		// size-change branch below, which deletes and rebuilds the profile -
		// asleep it cannot render from the old pointer in between.
		//
		// The call after Parent::open is deliberately KEPT. It is a no-op when
		// nothing changed (setProfile returns early when the profile is already
		// the control's own), and it means that if any stock path inside open()
		// ever does re-profile the input, the old behaviour still wins it back
		// rather than this becoming a regression.
		//////////////////////////////////////////////////////////////////////
		MRPGChat_ApplyInputProfile();

		// TEAM IS PASSED STRAIGHT THROUGH, NOT FOLDED INTO SAY.
		// On this server serverCmdTeamMessageSent is PARTY chat, not minigame
		// chat (Core_OLDpackage.cs re-points it at %client.party), so forcing
		// SAY here would quietly kill the Y key for every party in the game.
		// Only the placement and the badge are ours; the channel stays whatever
		// the caller asked for, and NMH_Type::send hands TEAM back to stock.
		Parent::open(%this, %channel);

		// Deciding to talk is deciding to see the conversation. Opening the input
		// over a hidden panel would put the typing box on screen with no log
		// behind it and no visible channel - worse than either stage on its own.
		if($MRPGChat_Mode != $MRPGChat::MODE_FULL)
			MRPGChat_SetMode($MRPGChat::MODE_FULL);

		$MRPGChat_Typing = 1;
		MRPGChat_ApplyInputProfile();
		MRPGChat_SetChannelBadge();
		MRPGChat_PlaceInput();
		MRPGChat_InputFocus(1);

		if(isObject(MRPGChatHint))
			MRPGChatHint.setText("");
	}

	function newMessageHud::onSleep(%this)
	{
		Parent::onSleep(%this);
		$MRPGChat_Typing = 0;
		MRPGChat_InputFocus(0);
		MRPGChat_ShowHint();
	}

	// newChatHud_AddLine calls this on every line. Left alone it would drag the
	// input box back under the (now offscreen) stock feed.
	function newMessageHud::updatePosition(%this)
	{
		if(!$MRPGChat_On)
			return Parent::updatePosition(%this);

		if(%this.isAwake())
			MRPGChat_PlaceInput();
	}

	// updateTypePosition re-derives the edit field's x from the channel label's
	// measured pixel width, which would undo MRPGChat_PlaceInput a frame later.
	function newMessageHud::updateTypePosition(%this)
	{
		if(!$MRPGChat_On)
			return Parent::updateTypePosition(%this);

		MRPGChat_PlaceInput();
	}

	function NMH_Type::send(%this)
	{
		if(!$MRPGChat_On)
			return Parent::send(%this);

		// Party chat (the Y key) keeps its own server command - it is not one of
		// the three tabs and must not be rewritten into one.
		if(newMessageHud.channel $= "TEAM")
			return Parent::send(%this);

		%text = trim(%this.getValue());
		if(strLen(%text) <= 0)
		{
			Canvas.popDialog(newMessageHud);
			return;
		}

		%chan  = "";
		%lower = strlwr(getWord(%text, 0));

		// Whisper takes a name before the message, so it cannot go through the
		// channel path below - it has its own server command.
		if(%lower $= "/w" || %lower $= "/whisper" || %lower $= "/msg" || %lower $= "/tell")
		{
			%rest   = trim(restWords(%text));
			%target = getWord(%rest, 0);
			%body   = trim(restWords(%rest));

			if(%target $= "" || %body $= "")
				MRPGChat_PushEvent("<color:" @ $MRPGChat::Col::Gold
					@ ">Usage: /w <name> <message>");
			else
				commandToServer('MRPGChatWhisper', %target, %body);

			Canvas.popDialog(newMessageHud);
			return;
		}

		if(%lower $= "/g" || %lower $= "/global" || %lower $= "/say" || %lower $= "/s")
			%chan = "Global";
		else if(%lower $= "/l" || %lower $= "/local")
			%chan = "Local";

		if(%chan !$= "")
		{
			%text = trim(restWords(%text));
			if(strLen(%text) <= 0)
			{
				// "/l" on its own is a channel switch, not an empty message.
				MRPGChat_SetTab(%chan $= "Local"
					? $MRPGChat::TAB_LOCAL : $MRPGChat::TAB_GLOBAL);
				Canvas.popDialog(newMessageHud);
				return;
			}
		}
		else if(getSubStr(%text, 0, 1) $= "/")
		{
			// Any other slash word is a server command. Hand it back to stock,
			// which already knows how to turn it into a commandToServer.
			return Parent::send(%this);
		}
		else
			%chan = MRPGChat_SendChannel();

		commandToServer('MRPGChatSend', %chan, %text);
		Canvas.popDialog(newMessageHud);
	}

	// ---- scrollback ------------------------------------------------------

	function PageUpNewChatHud(%val)
	{
		if(!$MRPGChat_On)
			return Parent::PageUpNewChatHud(%val);
		if(%val)
			MRPGChat_Scroll(4);
	}

	function PageDownNewChatHud(%val)
	{
		if(!$MRPGChat_On)
			return Parent::PageDownNewChatHud(%val);
		if(%val)
			MRPGChat_Scroll(-4);
	}

	// ---- keeping the paint box down --------------------------------------
	//
	// loadPaint destroys and rebuilds every paint control from scratch (it re-runs
	// on every colourset change) and BrickSelectorDlg::onSleep unconditionally
	// setVisible(1)s them. Either would put the box back in the corner this panel
	// now occupies.

	function PlayGui::loadPaint(%this)
	{
		Parent::loadPaint(%this);
		if($MRPGChat_On)
			MRPGChat_ShowPaintBox(0);
	}

	function BrickSelectorDlg::onSleep(%this)
	{
		Parent::onSleep(%this);
		if($MRPGChat_On)
			MRPGChat_ShowPaintBox(0);
	}
};

activatePackage(MRPGChatPackage);

MRPGChat_ClearAll();
