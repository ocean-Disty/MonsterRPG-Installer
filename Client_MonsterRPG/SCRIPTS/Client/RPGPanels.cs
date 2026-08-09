//////////////////////////////////////////////////////////////////////////////
// RPG PANELS - Stats / Skills / Traits scroll lists
//////////////////////////////////////////////////////////////////////////////
//
// The Main GUI ships the Stats and Skills tabs as a handful of fixed, non-
// scrolling text slots. This script replaces those slots with real scroll
// containers that are rebuilt from server data every time a tab opens, so the
// lists can hold any number of stats, skills or traits and scroll with the
// mouse wheel.
//
// It works purely as an overlay: the original fixed slots are hidden (never
// deleted) and a GuiScrollCtrl + content pane are added on top of the same
// bitmap. Nothing in the .gui file has to change.
//
// Data comes from Core_Stats.cs / Core_Skills.cs / Core_Traits.cs via the
// MRPG_Stat* / MRPG_Skill* / MRPG_Trait* client commands. Each feed is a
// Clear -> Row... -> Done sequence buffered here and drawn on Done.
//
//   Skills tab : left  = Skills (Combat then Specialty, each with an XP bar)
//                right = Traits (owned bright, locked greyed)
//   Stats  tab : left  = Attributes (STR/DEX/VIT/INT/WIS/CHA, "+" to allocate)
//                right = Leaderboard (untouched, still from the .gui)
//


//////////////////////////////////
////////// LAYOUT CONSTS /////////
//////////////////////////////////

$MRPG::Panel::RowH   = 28; // height of a data row
$MRPG::Panel::HdrH   = 26; // height of a section header (label + hairline)
$MRPG::Panel::GfxDir = "Add-Ons/Client_MonsterRPG/GUIs/";

// Unified palette - one warm gold accent to match the panels' gold frame.
// Hex values feed <color:> tags; "r g b a" values feed GuiSwatchCtrl colours.
$MRPG::UI::Gold     = "F1ECC2"; // headers / accents (pale cream-gold)
$MRPG::UI::GoldSoft = "F1ECC2"; // "points available"
$MRPG::UI::Name     = "DAD9DD"; // primary text (whiteish silver)
$MRPG::UI::Value    = "F1ECC2"; // level / value text
$MRPG::UI::Lock     = "8A8175"; // locked name
$MRPG::UI::LockDim  = "6E6A60"; // locked secondary
$MRPG::UI::BandEven = "255 255 255 10"; // zebra light stripe
$MRPG::UI::BandOdd  = "0 0 0 34";        // zebra dark stripe
$MRPG::UI::Hover    = "232 184 92 46";   // gold hover wash
$MRPG::UI::Divider  = "138 106 47 150";  // header hairline
$MRPG::UI::XPTrack  = "0 0 0 80";
$MRPG::UI::XPFill   = "217 164 65 235";

// Re-run the party re-skin / title cleanup after every (re)load so they
// re-apply on a live update instead of being blocked by a stale flag.
$MRPG_PartyStyled  = 0;
$MRPG_TitlesCleaned = 0;

// Hover-tooltip polling state (see MRPG_tipTick).
cancel($MRPG_TipSch);
$MRPG_TipSch       = "";
$MRPG_TipBand      = "";
$MRPG_TipBox       = "";
$MRPG_TipShown     = 0;
$MRPG_TipRequested = 0;

// Quest panel state.
$MRPG_QuestBuilt     = 0;
$MRPG_QuestRenderSch = "";

// Leaderboard change-signature: only rebuild the board when the data actually
// changed, so a periodic server refresh doesn't nuke the row being hovered.
$MRPG_LeaderSig = "";

// Party panel state.
$MRPG_PartyBuilt      = 0;   // rebuild the party scroll lists on (re)load
$MRPG_Party_PlayerSig = "";  // change-signatures so we don't re-render every tick
$MRPG_Party_MemberSig = "";
$MRPG_Party_SelPlayer = "";  // last-hovered names (Invite / Kick targets)
$MRPG_Party_SelMember = "";


//////////////////////////////////
////////// BUILD / SETUP /////////
//////////////////////////////////

// Create the scroll containers once and hide the old fixed slots. Guarded so
// it is cheap to call before every refresh.
function MRPG_buildPanels()
{
	if(!isObject(MonsterRPGx_StatsBitmap) || !isObject(MonsterRPGx_SkillsBitmap))
		return;

	// The tabs are re-mapped from the .gui's original intent:
	//   new "Stats" tab       -> the SkillsBitmap swatch (Attributes + Combat +
	//                            Specialty on the left, Traits on the right)
	//   new "Leaderboard" tab -> the StatsBitmap swatch  (one full-width board)

	// --- new Stats tab (SkillsBitmap) ---
	if(!isObject(MRPG_StatsScroll))
	{
		MRPG_hidePanelChildren(MonsterRPGx_SkillsBitmap, "MonsterRPGx_PSPanel"); // old skill slots
		MRPG_hidePanelChildren(MonsterRPGx_SkillsBitmap, "MonsterRPGx_LDBPanel"); // old trait slots
		MRPG_makeScroll("MRPG_StatsScroll",  "MRPG_StatsContent",  MonsterRPGx_SkillsBitmap, 14,  40, 205, 356);
		MRPG_makeScroll("MRPG_TraitsScroll", "MRPG_TraitsContent", MonsterRPGx_SkillsBitmap, 240, 40, 200, 356);

		// Shared hover-detail strip below both lists.
		MRPG_makeDetailBox("MRPG_StatsDetail", MonsterRPGx_SkillsBitmap, 12, 398, 430, 54);
	}

	// --- new Leaderboard tab (StatsBitmap) ---
	if(!isObject(MRPG_LeaderScroll))
	{
		MRPG_hidePanelChildren(MonsterRPGx_StatsBitmap, "MonsterRPGx_PSPanel"); // old attribute slots
		MRPG_hidePanelChildren(MonsterRPGx_StatsBitmap, "MonsterRPGx_LDBPanel"); // old fixed leaderboard rows

		// Replace the two separate wood side panels (the two silver borders) with
		// ONE big framed wood panel that encapsulates the whole board.
		if(isObject(MonsterRPGx_StatsSidePanel))       MonsterRPGx_StatsSidePanel.setVisible(0);
		if(isObject(MonsterRPGx_LeaderboardSidePanel)) MonsterRPGx_LeaderboardSidePanel.setVisible(0);
		MRPG_addWoodPanel("MRPG_LeaderPanel", MonsterRPGx_StatsBitmap, 4, 0, 444, 450);

		// Inset ~10px each side so the category/name text clears the wood frame.
		MRPG_makeScroll("MRPG_LeaderScroll", "MRPG_LeaderContent", MonsterRPGx_StatsBitmap, 24, 40, 411, 356);
	}

	// Hover-detail strip at the bottom, same as the Stats tab, so the shared tooltip
	// loop has somewhere to draw each row's detail. Guarded on its own so it is added
	// even when the scroll above already existed from an earlier build.
	if(!isObject(MRPG_LeaderDetail) && isObject(MonsterRPGx_StatsBitmap))
		MRPG_makeDetailBox("MRPG_LeaderDetail", MonsterRPGx_StatsBitmap, 12, 398, 430, 54);

	MRPG_cleanTitles();
}

// Hide every direct child of %parent whose object name matches %name. Used to
// tuck away the original fixed panels (which all share a name) without deleting
// anything, so the change is fully reversible.
function MRPG_hidePanelChildren(%parent, %name)
{
	if(!isObject(%parent))
		return;

	for(%i = 0; %i < %parent.getCount(); %i++)
	{
		%c = %parent.getObject(%i);
		if(%c.getName() $= %name)
			%c.setVisible(0);
	}
}

// Build a scroll control holding a transparent content pane and parent it to
// %parent. Returns nothing; reference the content pane by its name afterwards.
function MRPG_makeScroll(%scrollName, %contentName, %parent, %x, %y, %w, %h)
{
	%pos      = %x SPC %y;
	%ext      = %w SPC %h;
	%innerW   = %w - 16;             // leave room for the scrollbar
	%innerExt = %innerW SPC %h;

	%scroll = new GuiScrollCtrl(%scrollName)
	{
		profile             = "MRPG_PanelScrollProfile";
		horizSizing         = "right";
		vertSizing          = "bottom";
		position            = %pos;
		extent              = %ext;
		minExtent           = "8 2";
		hScrollBar          = "alwaysOff";
		vScrollBar          = "dynamic";
		constantThumbHeight = "0";
		willFirstRespond    = "1";
	};

	%content = new GuiSwatchCtrl(%contentName)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = "0 0";
		extent      = %innerExt;
		minExtent   = "8 2";
		color       = "0 0 0 0";
	};

	%scroll.add(%content);
	%parent.add(%scroll);
}

// Add one framed wood (RTS_bar) panel - used to encapsulate the leaderboard in a
// single border instead of the two side panels.
function MRPG_addWoodPanel(%name, %parent, %x, %y, %w, %h)
{
	%pos = %x SPC %y;
	%ext = %w SPC %h;
	%p = new GuiBitmapCtrl(%name)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = %pos;
		extent      = %ext;
		minExtent   = "8 2";
		bitmap      = $MRPG::Panel::GfxDir @ "Popup_Elements/RTS_bar";
		wrap        = "0";
		mColor      = "255 255 255 255";
		mMultiply   = "0";
	};
	%parent.add(%p);
}

// Build the hover detail box: a dark panel with a text control named %name@"Text".
function MRPG_makeDetailBox(%name, %parent, %x, %y, %w, %h)
{
	%pos = %x SPC %y;
	%ext = %w SPC %h;

	%box = new GuiSwatchCtrl(%name)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = %pos;
		extent      = %ext;
		minExtent   = "8 2";
		color       = "18 14 10 240";
		visible     = "0";
	};

	%tw  = %w - 16;
	%th  = %h - 8;
	%tex = %tw SPC %th;
	%txt = new GuiMLTextCtrl(%name @ "Text")
	{
		profile         = "GuiCustomMLTextProfile";
		horizSizing     = "right";
		vertSizing      = "bottom";
		position        = "8 4";
		extent          = %tex;
		minExtent       = "8 2";
		lineSpacing     = "1";
		allowColorChars = "1";
		maxChars        = "-1";
		selectable      = "0";
		autoResize      = "0";
	};

	%box.add(%txt);
	%parent.add(%box);
}

//////////////////////////////////
///////// HOVER TOOLTIP ///////////
//////////////////////////////////
//
// A GuiMouseEventCtrl buried inside a GuiScrollCtrl does not reliably get
// onMouseEnter, so hover is detected by polling the cursor against each row's
// on-screen rectangle. Dwell ~0.8s on a row -> the detail strip shows and stays
// until the cursor moves to another row or off the list.

// Start/refresh the polling loop (called when a Stats/Leaderboard tab opens).
function MRPG_tipStart()
{
	cancel($MRPG_TipSch);
	MRPG_tipTick();
}

function MRPG_tipTick()
{
	cancel($MRPG_TipSch);

	//Gate, and do NOT reschedule when it is shut - see ServerGate.cs rule 3.
	if(!MRPG_isActive())
	{
		MRPG_tipClear();
		return;
	}

	%onStats  = isObject(MonsterRPGx_Skills) && MonsterRPGx_Skills.isVisible();
	%onBoard  = isObject(MonsterRPGx_Stats)  && MonsterRPGx_Stats.isVisible();
	%onParty  = isObject(MonsterRPGx_Party)  && MonsterRPGx_Party.isVisible();
	%onQuest  = isObject(MonsterRPGx_Quest)  && MonsterRPGx_Quest.isVisible();
	%onSpells = isObject(MonsterRPGx_SpellsPanel) && MonsterRPGx_SpellsPanel.isVisible();
	if(!%onStats && !%onBoard && !%onParty && !%onQuest && !%onSpells)
	{
		MRPG_tipClear();
		return; // no relevant panel open - stop polling until a tab reopens
	}

	// Which row (if any) is under the cursor?
	%band = "";
	if(%onStats)
	{
		%band = MRPG_tipFind(MRPG_StatsContent);
		if(!isObject(%band))
			%band = MRPG_tipFind(MRPG_TraitsContent);
	}
	else if(%onSpells)
	{
		%band = MRPG_tipFind(MRPG_SpellsContent);
	}
	else if(%onBoard)
	{
		%band = MRPG_tipFind(MRPG_LeaderContent);
	}
	else if(%onQuest)
	{
		%band = MRPG_tipFind(MRPG_QuestContent);
	}
	else
	{
		%band = MRPG_tipFind(MRPG_PartyPlayersContent);
		if(!isObject(%band))
			%band = MRPG_tipFind(MRPG_PartyMembersContent);
	}

	if(isObject(%band))
	{
		if($MRPG_TipBand != %band)
		{
			// Moved onto a new row: restore the old one, highlight this one,
			// hide any open detail and start the dwell timer.
			if(isObject($MRPG_TipBand))
				$MRPG_TipBand.setColor($MRPG_TipBandBase);
			MRPG_hideDetail($MRPG_TipBox);

			$MRPG_TipBand     = %band;
			$MRPG_TipBandBase = %band.baseColor;
			$MRPG_TipBox      = %band.detailBox;
			$MRPG_TipStart     = getSimTime();
			$MRPG_TipShown     = 0;
			$MRPG_TipRequested = 0;
			%band.setColor($MRPG::UI::Hover);

			// Party rows remember the selection per list (for Invite / Kick).
			if(%band.playerName !$= "")
			{
				$MRPG_TipHoverName = %band.playerName;
				if(%band.partyList $= "server")
					$MRPG_Party_SelPlayer = %band.playerName;
				else if(%band.partyList $= "member")
					$MRPG_Party_SelMember = %band.playerName;
			}
		}
		else if((getSimTime() - $MRPG_TipStart) >= 800)
		{
			if(%band.playerName !$= "")
			{
				// Party row: request the attributes once, then (re)show them the
				// moment they cache - no reliance on an exact reply-name match.
				if(!$MRPG_TipRequested)
				{
					$MRPG_TipRequested = 1;
					if($MRPG_PlayerStats[%band.playerName] $= "")
						commandToServer('MRPG_GetPlayerStats', %band.playerName);
				}

				if($MRPG_PlayerStats[%band.playerName] !$= "")
					MRPG_showDetail("MRPG_PartyDetail", $MRPG_PlayerStats[%band.playerName]);
				else
					MRPG_showDetail("MRPG_PartyDetail",
						"<font:verdana bold:13><color:" @ $MRPG::UI::Name @ ">" @ %band.playerName @
						"\n<font:verdana bold:11><color:C8C8C8>Loading attributes...");
			}
			else if(%band.leaderValKey !$= "")
			{
				// Leaderboard row: detail is rebuilt live so the number is current.
				if(!$MRPG_TipShown)
				{
					MRPG_showDetail(%band.detailBox, MRPG_buildLeaderDetail(%band));
					$MRPG_TipShown = 1;
				}
			}
			else if(!$MRPG_TipShown && %band.detail !$= "")
			{
				MRPG_showDetail(%band.detailBox, %band.detail);
				$MRPG_TipShown = 1;
			}
		}
	}
	else
	{
		MRPG_tipClear();
	}

	$MRPG_TipSch = schedule(120, 0, MRPG_tipTick);
}

// Server reply with another player's attributes; cache it (the poll loop shows
// it on its next tick if the cursor is still on that row).
function clientCmdMRPG_PlayerStats(%data)
{
	%name = getField(%data, 0);

	$MRPG_PlayerStats[%name] = "<font:verdana bold:13><color:" @ $MRPG::UI::Gold @ ">" @ %name @
		"  <color:" @ $MRPG::UI::Value @ ">Lv " @ getField(%data, 1) @
		"\n<font:verdana bold:11><color:" @ $MRPG::UI::Name @ ">STR " @ getField(%data, 2) @
		"   DEX " @ getField(%data, 3) @ "   VIT " @ getField(%data, 4) @
		"\n<color:" @ $MRPG::UI::Name @ ">INT " @ getField(%data, 5) @
		"   WIS " @ getField(%data, 6) @ "   CHA " @ getField(%data, 7);
}

// Un-highlight the tracked row and hide the detail.
function MRPG_tipClear()
{
	if(isObject($MRPG_TipBand))
		$MRPG_TipBand.setColor($MRPG_TipBandBase);
	MRPG_hideDetail($MRPG_TipBox);

	$MRPG_TipBand  = "";
	$MRPG_TipShown = 0;
}

// Return the data row in %content whose on-screen rect contains the cursor.
function MRPG_tipFind(%content)
{
	if(!isObject(%content))
		return "";

	%cur = Canvas.getCursorPos();
	%cx  = getWord(%cur, 0);
	%cy  = getWord(%cur, 1);

	%count = %content.getCount();
	for(%i = 0; %i < %count; %i++)
	{
		%band = %content.getObject(%i);
		if(!%band.isRow)
			continue;

		%bp = %band.getCanvasPosition();
		%bx = getWord(%bp, 0);
		%by = getWord(%bp, 1);
		%bw = getWord(%band.extent, 0);
		%bh = getWord(%band.extent, 1);

		if(%cx >= %bx && %cx < %bx + %bw && %cy >= %by && %cy < %by + %bh)
			return %band;
	}
	return "";
}

function MRPG_showDetail(%box, %text)
{
	if(!isObject(%box))
		return;

	%tc = %box @ "Text";
	%tc.setText(%text);
	%box.setVisible(1);
}

function MRPG_hideDetail(%box)
{
	if(isObject(%box))
		%box.setVisible(0);
}

// Delete all rows in a content pane before a redraw.
function MRPG_clearContent(%content)
{
	if(!isObject(%content))
		return;

	while(%content.getCount() > 0)
		%content.getObject(0).delete();
}

// Grow the content pane to fit %usedH (or the viewport, whichever is taller)
// and reset the scroll to the top. Resizing the pane is what makes the scroll
// control recompute its range and show/hide the bar.
function MRPG_fitContent(%scroll, %content, %w, %usedH)
{
	%viewH = getWord(%scroll.extent, 1);
	%h = %usedH;
	if(%h < %viewH)
		%h = %viewH;

	%content.resize(0, 0, %w, %h);
	%scroll.scrollToTop();
}


//////////////////////////////////
////////// ROW BUILDERS //////////
//////////////////////////////////

// A flat zebra band: alternating translucent stripe on the dark wood, no frame.
// Its base colour is stashed so hover highlight can restore it.
function MRPG_makeBand(%content, %y, %w, %rowH, %index)
{
	// parity without the modulo operator (avoids any %-sigil ambiguity)
	%isOdd = %index - (mFloor(%index / 2) * 2);
	if(%isOdd == 0)
		%col = $MRPG::UI::BandEven;
	else
		%col = $MRPG::UI::BandOdd;

	%pos = "0" SPC %y;
	%ext = %w SPC %rowH;
	%band = new GuiSwatchCtrl()
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = %pos;
		extent      = %ext;
		minExtent   = "8 2";
		color       = %col;
	};
	%band.baseColor  = %col;
	%band.isRow      = 1;   // marks a hoverable data row (headers/dividers are not)
	%band.detail     = "";  // precomputed tooltip (stats/skills/traits)
	%band.detailBox  = "";
	%band.playerName = "";  // set on party rows; tooltip is fetched on demand
	%band.partyList  = "";
	%content.add(%band);
	return %band;
}

// Left-aligned text control inside a row.
function MRPG_leftText(%parent, %x, %y, %w2, %text)
{
	%pos = %x SPC %y;
	%ext = %w2 SPC "16";
	%c = new GuiMLTextCtrl()
	{
		profile         = "GuiCustomMLTextProfile";
		horizSizing     = "right";
		vertSizing      = "bottom";
		position        = %pos;
		extent          = %ext;
		minExtent       = "8 2";
		lineSpacing     = "2";
		allowColorChars = "1";
		maxChars        = "-1";
		selectable      = "0";
		autoResize      = "0";
	};
	%c.setText(%text);
	%parent.add(%c);
}

// Right-aligned text; its right edge sits at %rightX.
function MRPG_rightText(%parent, %rightX, %y, %w2, %text)
{
	%x   = %rightX - %w2;
	%pos = %x SPC %y;
	%ext = %w2 SPC "16";
	%c = new GuiMLTextCtrl()
	{
		profile         = "GuiCustomMLTextProfile";
		horizSizing     = "right";
		vertSizing      = "bottom";
		position        = %pos;
		extent          = %ext;
		minExtent       = "8 2";
		lineSpacing     = "2";
		allowColorChars = "1";
		maxChars        = "-1";
		selectable      = "0";
		autoResize      = "0";
	};
	%c.setText("<just:right>" @ %text);
	%parent.add(%c);
}

// Slim amber XP underline along the bottom of a skill row.
function MRPG_addXPBar(%band, %w, %rowH, %pct)
{
	if(%pct > 1) %pct = 1;
	if(%pct < 0) %pct = 0;

	%trackW = %w - 20;
	%trackY = %rowH - 4;
	%tpos   = "10" SPC %trackY;
	%text   = %trackW SPC "2";

	%track = new GuiSwatchCtrl()
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %tpos; extent = %text; minExtent = "2 2"; color = $MRPG::UI::XPTrack;
	};
	%band.add(%track);

	%fillW = mCeil(%trackW * %pct);
	if(%fillW > 0)
	{
		%fext = %fillW SPC "2";
		%fill = new GuiSwatchCtrl()
		{
			profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
			position = %tpos; extent = %fext; minExtent = "2 2"; color = $MRPG::UI::XPFill;
		};
		%band.add(%fill);
	}
}

// A section header: gold small-caps label with a hairline divider beneath.
function MRPG_addHeaderRow(%content, %y, %w, %label, %suffix)
{
	%tpos = "10" SPC %y;
	%tw   = %w - 20;
	%text = %tw SPC "18";
	%hdr = new GuiMLTextCtrl()
	{
		profile         = "GuiCustomMLTextProfile";
		horizSizing     = "right";
		vertSizing      = "bottom";
		position        = %tpos;
		extent          = %text;
		minExtent       = "8 2";
		lineSpacing     = "2";
		allowColorChars = "1";
		maxChars        = "-1";
		selectable      = "0";
		autoResize      = "0";
	};
	%hdr.setText("<font:verdana bold:13><color:" @ $MRPG::UI::Gold @ ">" @ strUpr(%label) @ %suffix);
	%content.add(%hdr);

	%ly   = %y + 19;
	%lpos = "10" SPC %ly;
	%lw   = %w - 20;
	%lext = %lw SPC "1";
	%line = new GuiSwatchCtrl()
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %lpos; extent = %lext; minExtent = "2 1"; color = $MRPG::UI::Divider;
	};
	%content.add(%line);
}

// Skill row: name left, level right, slim amber XP underline. Exact XP is in hover.
function MRPG_addSkillRow(%content, %index, %y, %w, %display, %level, %exp, %max, %desc)
{
	%rowH = $MRPG::Panel::RowH;
	%band = MRPG_makeBand(%content, %y, %w, %rowH, %index);

	%nameW = %w - 124;
	MRPG_leftText(%band, 12, 5, %nameW, "<font:verdana bold:12><color:" @ $MRPG::UI::Name @ ">" @ %display);
	MRPG_rightText(%band, %w - 12, 5, 96, "<font:verdana bold:12><color:" @ $MRPG::UI::Value @ ">Lv " @ %level);

	%pct = 0;
	if(%max > 0)
		%pct = %exp / %max;
	MRPG_addXPBar(%band, %w, %rowH, %pct);

	%pctI = mFloor(%pct * 100);
	if(%pctI > 100) %pctI = 100;
	%detail = "<font:verdana bold:13><color:" @ $MRPG::UI::Gold @ ">" @ %display @
		"  <color:" @ $MRPG::UI::Value @ ">Level " @ %level @
		"\n<font:verdana bold:11><color:" @ $MRPG::UI::Name @ ">XP " @ %exp @ " / " @ %max @ " (" @ %pctI @ "%)" @
		"\n<color:C8C8C8>" @ %desc;
	MRPG_attachRowHover(%band, %w, %rowH, %detail, "MRPG_StatsDetail");
}

// Attribute row: "STR Strength" left, value right, "+" to allocate when able.
function MRPG_addStatRow(%content, %index, %y, %w, %abbr, %display, %value, %desc, %hasPoints)
{
	%rowH = $MRPG::Panel::RowH;
	%band = MRPG_makeBand(%content, %y, %w, %rowH, %index);

	%nameW = %w - 110;
	MRPG_leftText(%band, 12, 5, %nameW,
		"<font:verdana bold:12><color:" @ $MRPG::UI::Name @ ">" @ %display);
	MRPG_rightText(%band, %w - 34, 5, 60, "<font:verdana bold:12><color:" @ $MRPG::UI::Value @ ">" @ %value);

	%detail = "<font:verdana bold:13><color:" @ $MRPG::UI::Name @ ">" @ %display @
		"  <color:" @ $MRPG::UI::Value @ ">" @ %value @
		"\n<font:verdana bold:11><color:C8C8C8>" @ %desc;
	MRPG_attachRowHover(%band, %w, %rowH, %detail, "MRPG_StatsDetail");

	// "+" button added last so it stays clickable above the hover region.
	if(%hasPoints)
	{
		%btnX   = %w - 28;
		%btnPos = %btnX SPC "4";
		%btn = new GuiBitmapButtonCtrl()
		{
			profile     = "GuiDefaultProfile";
			horizSizing = "right";
			vertSizing  = "bottom";
			position    = %btnPos;
			extent      = "20 20";
			minExtent   = "8 2";
			command     = "MRPG_allocStat(\"" @ %abbr @ "\");";
			buttonType  = "PushButton";
			bitmap      = $MRPG::Panel::GfxDir @ "buttons/Increase";
		};
		%band.add(%btn);
	}
}

// Trait/perk row. Owned = name + rank; locked = desaturated with "Locked".
function MRPG_addTraitRow(%content, %index, %y, %w, %name, %owned, %ranks, %lvlReq, %goldCost, %desc)
{
	%display = strReplace(%name, "Trait_", "");
	%rowH = $MRPG::Panel::RowH;
	%band = MRPG_makeBand(%content, %y, %w, %rowH, %index);

	%nameW = %w - 124;
	if(%owned > 0)
	{
		MRPG_leftText(%band, 12, 5, %nameW, "<font:verdana bold:12><color:" @ $MRPG::UI::Name @ ">" @ %display);
		MRPG_rightText(%band, %w - 12, 5, 96,
			"<font:verdana bold:12><color:" @ $MRPG::UI::Gold @ ">" @ MRPG_roman(%owned) @
			" <color:" @ $MRPG::UI::Value @ ">" @ %owned @ "/" @ %ranks);

		%detail = "<font:verdana bold:13><color:" @ $MRPG::UI::Gold @ ">" @ %display @ " " @ MRPG_roman(%owned) @
			"  <color:" @ $MRPG::UI::Value @ ">(rank " @ %owned @ "/" @ %ranks @ ")" @
			"\n<font:verdana bold:11><color:C8C8C8>" @ %desc;
	}
	else
	{
		MRPG_leftText(%band, 12, 5, %nameW, "<font:verdana bold:12><color:" @ $MRPG::UI::Lock @ ">" @ %display);
		MRPG_rightText(%band, %w - 12, 5, 96, "<font:verdana bold:12><color:" @ $MRPG::UI::LockDim @ ">Locked");

		%detail = "<font:verdana bold:13><color:" @ $MRPG::UI::Name @ ">" @ %display @
			" <color:" @ $MRPG::UI::LockDim @ ">[Locked]" @
			"\n<font:verdana bold:11><color:" @ $MRPG::UI::Value @ ">Requires Level " @ %lvlReq @ ", " @ %goldCost @ "g" @
			"\n<color:C8C8C8>" @ %desc;
	}

	// Traits live on the Skills tab, so their hover feeds the skills detail strip.
	MRPG_attachRowHover(%band, %w, %rowH, %detail, "MRPG_StatsDetail");
}

// Record a row's tooltip so the polling loop (MRPG_tipTick) can show it on dwell.
function MRPG_attachRowHover(%band, %rowW, %rowH, %detail, %detailBox)
{
	%band.detail    = %detail;
	%band.detailBox = %detailBox;
}

// 1..6 -> roman numeral, anything else passes through.
function MRPG_roman(%n)
{
	switch(%n)
	{
		case 1: return "I";
		case 2: return "II";
		case 3: return "III";
		case 4: return "IV";
		case 5: return "V";
		case 6: return "VI";
	}
	return %n;
}


//////////////////////////////////
//////////// RENDERERS ///////////
//////////////////////////////////

// The new Stats tab's left list: Attributes, then Combat, then Specialty, all
// in one scroll. Reads both the stat buffer and the skill buffer (they arrive
// on separate feeds, so this is called from each feed's Done handler).
function MRPG_renderStatsTab()
{
	%content = MRPG_StatsContent;
	if(!isObject(%content))
		return;

	MRPG_clearContent(%content);
	%w = getWord(%content.extent, 0);
	%y = 2;
	%rowIndex = 0;

	// Attributes (allocatable)
	%hasPoints = ($MRPG_StatPoints > 0);
	%suffix = "";
	if(%hasPoints)
		%suffix = "   <font:verdana bold:11><color:" @ $MRPG::UI::GoldSoft @ ">(" @ $MRPG_StatPoints @ " pts)";
	MRPG_addHeaderRow(%content, %y, %w, "Attributes", %suffix);
	%y += $MRPG::Panel::HdrH;

	for(%i = 0; %i < $MRPG_StatBufCount; %i++)
	{
		MRPG_addStatRow(%content, %rowIndex, %y, %w,
			$MRPG_StatBuf[%i, "abbr"],
			$MRPG_StatBuf[%i, "display"],
			$MRPG_StatBuf[%i, "value"],
			$MRPG_StatBuf[%i, "desc"],
			%hasPoints);
		%rowIndex++;
		%y += $MRPG::Panel::RowH;
	}

	// Skills, grouped Combat then Specialty (buffer order).
	%lastCat = "";
	for(%i = 0; %i < $MRPG_SkillBufCount; %i++)
	{
		%cat = $MRPG_SkillBuf[%i, "cat"];
		if(%cat !$= %lastCat)
		{
			MRPG_addHeaderRow(%content, %y, %w, %cat, "");
			%y += $MRPG::Panel::HdrH;
			%lastCat = %cat;
		}

		MRPG_addSkillRow(%content, %rowIndex, %y, %w,
			$MRPG_SkillBuf[%i, "display"],
			$MRPG_SkillBuf[%i, "lvl"],
			$MRPG_SkillBuf[%i, "exp"],
			$MRPG_SkillBuf[%i, "max"],
			$MRPG_SkillBuf[%i, "desc"]);
		%rowIndex++;
		%y += $MRPG::Panel::RowH;
	}

	MRPG_fitContent(MRPG_StatsScroll, %content, %w, %y);
}

// The new Leaderboard tab: one full-width list of the top player per category,
// read from the same $MonsterRPG::Client::RPGData the old fixed board used.
function MRPG_renderLeaderboard()
{
	%content = MRPG_LeaderContent;
	if(!isObject(%content))
		return;

	%cats  = "Top Rebirth" TAB "Top Level" TAB "Top Harvest" TAB "Top Fisherman" TAB "Monster Kills" TAB "Player Kills" TAB "Lowest Karma" TAB "Highest Karma" TAB "Most Deaths" TAB "Most Playtime" TAB "Most Quests" TAB "Most Bounties" TAB "Most Famous";
	%keys  = "TopRebirthName" TAB "TopLevelName" TAB "TopHarvestName" TAB "TopFishermanName" TAB "TopMKName" TAB "TopPKName" TAB "LowKarmaName" TAB "HighKarmaName" TAB "MostDeathsName" TAB "MostPlayedTimeName" TAB "MostQuestsCompletedName" TAB "MostBountiesCompletedName" TAB "MostFamousName";
	%descs = "The most reborn - highest rebirth count." TAB "The highest character level reached." TAB "The most materials harvested." TAB "The most fish caught." TAB "The most monsters slain." TAB "The most players defeated in combat." TAB "The most notorious - lowest karma." TAB "The most virtuous - highest karma." TAB "Fell in battle the most times." TAB "The most time played on the server." TAB "The most quests completed." TAB "The most bounties collected." TAB "The most renowned - highest fame.";
	// Value keys (into RPGData.var) + the label each number is shown under on hover.
	%vals  = "TopRebirth" TAB "TopLevel" TAB "TopHarvest" TAB "TopFishcaught" TAB "TopMK" TAB "TopPK" TAB "LowKarma" TAB "HighKarma" TAB "MostDeaths" TAB "MostPlayedTime" TAB "MostQuestsCompleted" TAB "MostBountiesCompleted" TAB "TopFame";
	%stats = "Rebirths" TAB "Level" TAB "Materials harvested" TAB "Fish caught" TAB "Monster kills" TAB "Player kills" TAB "Karma" TAB "Karma" TAB "Deaths" TAB "Playtime" TAB "Quests completed" TAB "Bounties completed" TAB "Fame";

	%count = getFieldCount(%cats);

	// Signature of the board data. Skip the whole rebuild when nothing changed so
	// the periodic server refresh doesn't delete the row you're hovering (flicker).
	%sig = "";
	for(%i = 0; %i < %count; %i++)
	{
		%nm = "";
		if(isObject($MonsterRPG::Client::RPGData))
			%nm = $MonsterRPG::Client::RPGData.var[getField(%keys, %i)];
		%sig = %sig @ %nm @ "|";
	}
	if(%content.getCount() > 0 && %sig $= $MRPG_LeaderSig)
		return;
	$MRPG_LeaderSig = %sig;

	MRPG_clearContent(%content);
	%w = getWord(%content.extent, 0);
	%y = 2;

	MRPG_addHeaderRow(%content, %y, %w, "Leaderboard", "");
	%y += $MRPG::Panel::HdrH;

	%rowIndex = 0;
	for(%i = 0; %i < %count; %i++)
	{
		%name = "";
		if(isObject($MonsterRPG::Client::RPGData))
			%name = $MonsterRPG::Client::RPGData.var[getField(%keys, %i)];
		if(%name $= "N/A" || %name $= "")
			continue;

		%band  = MRPG_makeBand(%content, %y, %w, $MRPG::Panel::RowH, %rowIndex);
		%nameW = %w - 190;
		MRPG_leftText(%band, 12, 5, %nameW, "<font:verdana bold:12><color:" @ $MRPG::UI::Gold @ ">" @ getField(%cats, %i));
		MRPG_rightText(%band, %w - 12, 5, 180, "<font:verdana bold:12><color:" @ $MRPG::UI::Name @ ">" @ %name);

		// Store the pieces for a LIVE hover detail (built by MRPG_buildLeaderDetail
		// at show-time) so the number stays current without re-rendering the list -
		// the signature above is names-only, so a rising kill count won't flicker it.
		%band.leaderCat    = getField(%cats, %i);
		%band.leaderName   = %name;
		%band.leaderStat   = getField(%stats, %i);
		%band.leaderValKey = getField(%vals, %i);
		%band.leaderDesc   = getField(%descs, %i);
		%band.detailBox    = "MRPG_LeaderDetail";

		%rowIndex++;
		%y += $MRPG::Panel::RowH;
	}

	MRPG_fitContent(MRPG_LeaderScroll, %content, %w, %y);
}

// Build a leaderboard row's tooltip from the CURRENT stored value, so the number
// is fresh each time it is shown (the row list itself only rebuilds on a name change).
function MRPG_buildLeaderDetail(%band)
{
	%val = "";
	if(isObject($MonsterRPG::Client::RPGData))
		%val = $MonsterRPG::Client::RPGData.var[%band.leaderValKey];
	if(%val $= "")
		%val = "?";

	return "<font:verdana bold:13><color:" @ $MRPG::UI::Gold @ ">" @ %band.leaderCat @
		"\n<font:verdana bold:12><color:" @ $MRPG::UI::Value @ ">Leader: <color:" @ $MRPG::UI::Name @ ">" @ %band.leaderName @
		"\n<font:verdana bold:12><color:" @ $MRPG::UI::Value @ ">" @ %band.leaderStat @ ": <color:" @ $MRPG::UI::Name @ ">" @ %val @
		"\n<font:verdana bold:11><color:C8C8C8>" @ %band.leaderDesc;
}

function MRPG_renderTraits()
{
	%content = MRPG_TraitsContent;
	if(!isObject(%content))
		return;

	MRPG_clearContent(%content);
	%w = getWord(%content.extent, 0);
	%y = 2;

	MRPG_addHeaderRow(%content, %y, %w, "Traits", "");
	%y += $MRPG::Panel::HdrH;

	%rowIndex = 0;
	for(%i = 0; %i < $MRPG_TraitBufCount; %i++)
	{
		MRPG_addTraitRow(%content, %rowIndex, %y, %w,
			$MRPG_TraitBuf[%i, "name"],
			$MRPG_TraitBuf[%i, "owned"],
			$MRPG_TraitBuf[%i, "ranks"],
			$MRPG_TraitBuf[%i, "lvl"],
			$MRPG_TraitBuf[%i, "gold"],
			$MRPG_TraitBuf[%i, "desc"]);
		%rowIndex++;
		%y += $MRPG::Panel::RowH;
	}

	MRPG_fitContent(MRPG_TraitsScroll, %content, %w, %y);
}


//////////////////////////////////
////////// PARTY PANELS //////////
//////////////////////////////////
//
// Converts the Party tab to the same look as Stats/Leaderboard: two framed wood
// columns (Players | Members) holding zebra scroll lists with in-list headers,
// replacing the old MLText lists and the three ornate banners. Hovering a row
// shows that player's attributes (fetched on demand).

function MRPG_buildParty()
{
	if($MRPG_PartyBuilt)
		return;
	if(!isObject(MonsterRPGx_Party))
		return;

	%rts = MonsterRPGx_Party.getObject(1); // the RTS_bar overlay that holds the lists
	if(!isObject(%rts))
		return;

	// Hide the old paper list windows (and their MLText/lines/thumbs) and the
	// three ornate title banners.
	MRPG_partyHide(MonsterRPGx_Party);

	// Two framed wood columns, each with a scroll list.
	MRPG_addWoodPanel("MRPG_PartyPlayersPanel", %rts,   8, 6, 216, 300);
	MRPG_makeScroll("MRPG_PartyPlayersScroll", "MRPG_PartyPlayersContent", %rts,  18, 34, 198, 258);
	MRPG_addWoodPanel("MRPG_PartyMembersPanel", %rts, 226, 6, 216, 300);
	MRPG_makeScroll("MRPG_PartyMembersScroll", "MRPG_PartyMembersContent", %rts, 236, 34, 198, 258);

	// Shared hover-detail strip below the columns (above the button bar).
	MRPG_makeDetailBox("MRPG_PartyDetail", %rts, 8, 312, 434, 52);

	$MRPG_PartyBuilt = 1;
}

// Hide the old paper windows and ornate banners under the party swatch.
function MRPG_partyHide(%parent)
{
	for(%i = 0; %i < %parent.getCount(); %i++)
	{
		%c = %parent.getObject(%i);

		if(strStr(%c.bitmap, "Popup_Window_paper") >= 0 || strStr(%c.bitmap, "Title_frame_medium_classic") >= 0)
		{
			%c.setVisible(0);
			continue;
		}

		if(%c.getCount() > 0)
			MRPG_partyHide(%c);
	}
}

// One party row: name (+ [L] for the leader) on the left, level on the right.
function MRPG_addPartyRow(%content, %index, %y, %w, %name, %lvl, %isLeader, %list)
{
	%band = MRPG_makeBand(%content, %y, %w, $MRPG::Panel::RowH, %index);

	%tag = "";
	if(%isLeader)
		%tag = "  <color:" @ $MRPG::UI::GoldSoft @ ">[L]";

	MRPG_leftText(%band, 12, 5, %w - 60, "<font:verdana bold:12><color:" @ $MRPG::UI::Name @ ">" @ %name @ %tag);
	MRPG_rightText(%band, %w - 12, 5, 52, "<font:verdana bold:12><color:" @ $MRPG::UI::Value @ ">Lv " @ %lvl);

	%band.playerName = %name;
	%band.partyList  = %list;
	%band.detailBox  = "MRPG_PartyDetail";
}

function MRPG_renderPartyPlayers()
{
	%content = MRPG_PartyPlayersContent;
	if(!isObject(%content))
		return;

	MRPG_clearContent(%content);
	%w = getWord(%content.extent, 0);
	%y = 2;

	MRPG_addHeaderRow(%content, %y, %w, "Players", "");
	%y += $MRPG::Panel::HdrH;

	for(%i = 0; %i < $MonsterRPG::PlayerCount; %i++)
	{
		MRPG_addPartyRow(%content, %i, %y, %w,
			$MonsterRPG::Player[%i], $MonsterRPG::Player::Level[%i], 0, "server");
		%y += $MRPG::Panel::RowH;
	}

	MRPG_fitContent(MRPG_PartyPlayersScroll, %content, %w, %y);
}

function MRPG_renderPartyMembers()
{
	%content = MRPG_PartyMembersContent;
	if(!isObject(%content))
		return;

	MRPG_clearContent(%content);
	%w = getWord(%content.extent, 0);
	%y = 2;

	// Header shows the party size and the active bonus, replacing the Bonuses banner.
	%suffix = "  <font:verdana bold:11><color:" @ $MRPG::UI::GoldSoft @ ">(" @
		$MonsterRPG::Party::MembersCount @ "/" @ $MonsterRPG::Party::MaxMembers @ ")";
	MRPG_addHeaderRow(%content, %y, %w, "Members", %suffix);
	%y += $MRPG::Panel::HdrH;

	for(%i = 0; %i < $MonsterRPG::Party::MembersCount; %i++)
	{
		%name = $MonsterRPG::Party::Member[%i];
		MRPG_addPartyRow(%content, %i, %y, %w,
			%name, $MonsterRPG::Party::Member::Level[%i],
			(%name $= $MonsterRPG::Party::Leader), "member");
		%y += $MRPG::Panel::RowH;
	}

	MRPG_fitContent(MRPG_PartyMembersScroll, %content, %w, %y);
}

// Called from the party data client commands; re-renders only when the list
// actually changed (they arrive several times a second).
function MRPG_partyMaybeRender(%which)
{
	if(!isObject(MonsterRPGx_Party) || !MonsterRPGx_Party.isVisible())
		return;

	MRPG_buildParty();

	if(%which $= "players")
	{
		%sig = $MonsterRPG::PlayerCount;
		for(%i = 0; %i < $MonsterRPG::PlayerCount; %i++)
			%sig = %sig @ "|" @ $MonsterRPG::Player[%i] @ $MonsterRPG::Player::Level[%i];
		if(%sig $= $MRPG_Party_PlayerSig)
			return;
		$MRPG_Party_PlayerSig = %sig;
		MRPG_renderPartyPlayers();
	}
	else
	{
		%sig = $MonsterRPG::Party::MembersCount @ "/" @ $MonsterRPG::Party::MaxMembers;
		for(%i = 0; %i < $MonsterRPG::Party::MembersCount; %i++)
			%sig = %sig @ "|" @ $MonsterRPG::Party::Member[%i] @ $MonsterRPG::Party::Member::Level[%i];
		if(%sig $= $MRPG_Party_MemberSig)
			return;
		$MRPG_Party_MemberSig = %sig;
		MRPG_renderPartyMembers();
	}
}


//////////////////////////////////
///////////// QUESTS /////////////
//////////////////////////////////
//
// The Quest tab, styled like the Leaderboard (one framed wood panel + scroll)
// with a hover-detail strip for each quest's description / location / rewards.
// MonsterRPGx_Quest never existed in the .gui, so it is built here as a sibling
// swatch of the other tabs.

function MRPG_buildQuest()
{
	if($MRPG_QuestBuilt && isObject(MonsterRPGx_Quest))
		return;
	if(!isObject(MonsterRPGx_Stats))
		return;

	// The Quest layout now lives in GUIs/MonsterRPGx_Quest.gui (exec'd in client.cs,
	// editable in the GUI editor). Instead of building it from scratch we slot that
	// swatch next to the other tab swatches and set the palette-styled title text.
	// The whole frame (bg, wood panel, MRPG_QuestScroll+MRPG_QuestContent, detail box,
	// MRPG_QuestDragMouse) is in the .gui; only the title text + server-fed quest rows
	// (MRPG_renderQuests into MRPG_QuestContent) are set at runtime.
	if(!isObject(MonsterRPGx_Quest))
		return; // MonsterRPGx_Quest.gui not loaded - nothing to slot in

	%host = MonsterRPGx_Stats.getGroup();
	if(!isObject(%host))
		return;
	%host.add(MonsterRPGx_Quest); // idempotent reparent next to Stats/Skills/etc.

	// The .gui carries the same title text so the editor preview reads right, but this
	// setText() is the runtime source of truth (an editor save can strip .gui text).
	if(isObject(MonsterRPGx_QuestTitle))
		MonsterRPGx_QuestTitle.setText("<just:center><color:" @ $MRPG::UI::Name @ "><font:verdana bold:38>Quests");

	$MRPG_QuestBuilt = 1;
}

function MRPG_renderQuests()
{
	%content = MRPG_QuestContent;
	if(!isObject(%content))
		return;

	MRPG_clearContent(%content);
	%w = getWord(%content.extent, 0);
	%y = 2;

	MRPG_addHeaderRow(%content, %y, %w, "Quests", "");
	%y += $MRPG::Panel::HdrH;

	%rowIndex = 0;
	for(%i = 0; %i < $MonsterRPG::Client::QuestCount; %i++)
	{
		%qname = $MonsterRPG::Client::Quest[%i, "questName"];
		if(%qname $= "")
			continue;

		%stage = $MonsterRPG::Client::Quest[%i, "Stage"];
		%desc  = $MonsterRPG::Client::Quest[%i, "Description"];
		%loc   = $MonsterRPG::Client::Quest[%i, "Location"];
		%gold  = $MonsterRPG::Client::Quest[%i, "goldReward"];
		%exp   = $MonsterRPG::Client::Quest[%i, "expReward"];

		%band = MRPG_makeBand(%content, %y, %w, $MRPG::Panel::RowH, %rowIndex);
		MRPG_leftText(%band, 12, 5, %w - 74, "<font:verdana bold:12><color:" @ $MRPG::UI::Name @ ">" @ %qname);
		MRPG_rightText(%band, %w - 12, 5, 64, "<font:verdana bold:11><color:" @ $MRPG::UI::Value @ ">Stage " @ %stage);

		%band.detail = "<font:verdana bold:13><color:" @ $MRPG::UI::Gold @ ">" @ %qname @
			"\n<font:verdana bold:11><color:" @ $MRPG::UI::Name @ ">" @ %desc @
			"\n<color:" @ $MRPG::UI::Value @ ">Location: <color:" @ $MRPG::UI::Name @ ">" @ %loc @
			"   <color:" @ $MRPG::UI::Value @ ">Reward: <color:" @ $MRPG::UI::Name @ ">" @ %exp @ " XP, " @ %gold @ "g";
		%band.detailBox = "MRPG_QuestDetail";

		%rowIndex++;
		%y += $MRPG::Panel::RowH;
	}

	if(%rowIndex == 0)
	{
		%band = MRPG_makeBand(%content, %y, %w, $MRPG::Panel::RowH, 0);
		%band.isRow = 0;
		MRPG_leftText(%band, 12, 6, %w - 20, "<font:verdana bold:12><color:" @ $MRPG::UI::Lock @ ">No active quests.");
		%y += $MRPG::Panel::RowH;
	}

	MRPG_fitContent(MRPG_QuestScroll, %content, %w, %y);
}


//////////////////////////////////
///////// REFRESH / INPUT ////////
//////////////////////////////////

// Called from useRPGTab when the matching tab is opened.
// The Stats tab holds attributes + skills + traits, so it pulls all three feeds.
function MRPG_refreshStats()
{
	MRPG_buildPanels();
	commandToServer('MRPG_GetStats');
	commandToServer('MRPG_GetSkills');
	commandToServer('MRPG_GetTraits');
	MRPG_tipStart();
}

// The Leaderboard tab renders straight from the client's stored board data.
function MRPG_refreshLeaderboard()
{
	MRPG_buildPanels();
	MRPG_renderLeaderboard();
	MRPG_tipStart();
}

// The Quest tab requests fresh data and renders the cached quests immediately.
function MRPG_refreshQuests()
{
	MRPG_buildQuest();
	$MonsterRPG::Client::QuestsCounted = 0;   // next reply resets the buffer
	commandToServer('GetMainQuestData');
	MRPG_renderQuests();
	MRPG_tipStart();
}

// "+" button on a stat row. The server answers with a fresh stat feed, which
// re-renders the panel, so there is nothing to update locally here.
function MRPG_allocStat(%abbr)
{
	commandToServer('MRPG_AllocStat', %abbr);
}


//////////////////////////////////
///////// PARTY RESTYLE //////////
//////////////////////////////////
//
// The Party tab keeps its existing (frequently-updated) MLText list system, but
// its two lists sit on light "Popup_Window_paper" windows. We swap that light
// texture for the same dark RTS_bar wood the Skills/Traits lists use. The paper
// control stays OPAQUE (only its bitmap changes) so the list text layered on top
// still renders - making it transparent instead dropped the lists entirely.
// Runs once, when the Party tab is first opened.

function MRPG_stylePartyDark()
{
	if($MRPG_PartyStyled)
		return;
	if(!isObject(MonsterRPGx_Party))
		return;

	// Re-skin every paper window to the dark wood.
	MRPG_darkenPaper(MonsterRPGx_Party);

	// Darken the two scrollbars (were a light beige tint).
	for(%i = 1; %i <= 2; %i++)
	{
		%track = "MonsterRPGx_ScrollVerticalTrack_" @ %i;
		%thumb = "MonsterRPGx_ScrollVerticalThumbBitmap_" @ %i;
		if(isObject(%track)) %track.setColor("110 88 66 220");
		if(isObject(%thumb)) %thumb.setColor("150 120 90 240");
	}

	$MRPG_PartyStyled = 1;
}

// Re-skin any Popup_Window_paper backgrounds under %parent to the dark RTS_bar
// wood, kept fully opaque so the list controls on top keep rendering. Recurses
// but only ever touches paper bitmaps.
function MRPG_darkenPaper(%parent)
{
	for(%i = 0; %i < %parent.getCount(); %i++)
	{
		%c = %parent.getObject(%i);

		if(strStr(%c.bitmap, "Popup_Window_paper") >= 0)
		{
			%c.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/Popup_Elements/RTS_bar");
			%c.setColor("255 255 255 255");
		}

		if(%c.getCount() > 0)
			MRPG_darkenPaper(%c);
	}
}


//////////////////////////////////
///////// TITLE CLEANUP //////////
//////////////////////////////////
//
// The Skills/Stats column titles sit in an ornate silver frame
// (Title_frame_medium_classic) that is squished out of aspect and is a
// frame-inside-a-frame. Hide that frame and drop a thin hairline under the
// label instead, matching the in-list section headers.
//
// The Party tab is intentionally NOT cleaned: its title frames are wider (near
// their native aspect, so they don't squish) and its layout is tight - the
// titles butt right against the list windows and rely on the frame for
// separation, so stripping them just leaves the labels floating over the lists.
function MRPG_cleanTitles()
{
	if($MRPG_TitlesCleaned)
		return;

	%swatches = "MonsterRPGx_Skills" TAB "MonsterRPGx_Stats";
	for(%i = 0; %i < 2; %i++)
	{
		%sw = getField(%swatches, %i);
		if(isObject(%sw))
			MRPG_cleanTitlesWalk(%sw);
	}

	$MRPG_TitlesCleaned = 1;
}

function MRPG_cleanTitlesWalk(%parent)
{
	for(%i = 0; %i < %parent.getCount(); %i++)
	{
		%c = %parent.getObject(%i);

		if(strStr(%c.bitmap, "Title_frame_medium_classic") >= 0 && %c.isVisible())
		{
			// Every column now has its own in-list header (ATTRIBUTES / COMBAT /
			// SPECIALTY / TRAITS / LEADERBOARD) and the lists are raised into this
			// space, so drop the redundant ornate column title entirely.
			%c.setVisible(0);
			continue;
		}

		if(%c.getCount() > 0)
			MRPG_cleanTitlesWalk(%c);
	}
}


//////////////////////////////////
///////// SERVER FEEDS ///////////
//////////////////////////////////

// --- Skills ---
function clientCmdMRPG_SkillClear()
{
	$MRPG_SkillBufCount = 0;
}

function clientCmdMRPG_SkillRow(%data)
{
	%i = $MRPG_SkillBufCount;
	$MRPG_SkillBuf[%i, "display"] = getField(%data, 0);
	$MRPG_SkillBuf[%i, "cat"]     = getField(%data, 1);
	$MRPG_SkillBuf[%i, "lvl"]     = getField(%data, 2);
	$MRPG_SkillBuf[%i, "exp"]     = getField(%data, 3);
	$MRPG_SkillBuf[%i, "max"]     = getField(%data, 4);
	$MRPG_SkillBuf[%i, "desc"]    = getField(%data, 5);
	$MRPG_SkillBufCount++;
}

function clientCmdMRPG_SkillDone()
{
	// Skills render into the new Stats tab (the Skills swatch). A background exp
	// gain can arrive while the tab is closed; only rebuild when it's showing.
	if(isObject(MonsterRPGx_Skills) && !MonsterRPGx_Skills.isVisible())
		return;

	MRPG_buildPanels();
	MRPG_renderStatsTab();
}

// --- Stats ---
// Meta arrives first and doubles as the buffer reset.
function clientCmdMRPG_StatMeta(%points)
{
	$MRPG_StatPoints   = %points;
	$MRPG_StatBufCount = 0;
}

function clientCmdMRPG_StatRow(%data)
{
	%i = $MRPG_StatBufCount;
	$MRPG_StatBuf[%i, "abbr"]    = getField(%data, 0);
	$MRPG_StatBuf[%i, "display"] = getField(%data, 1);
	$MRPG_StatBuf[%i, "value"]   = getField(%data, 2);
	$MRPG_StatBuf[%i, "desc"]    = getField(%data, 3);
	$MRPG_StatBufCount++;
}

function clientCmdMRPG_StatDone()
{
	// OTHER SCREENS CONSUME THIS PUSH TOO, and they must be served BEFORE the early-out
	// below. Both the character screen and the attribute screen spend real SkillPoints on
	// the server and redraw from whatever comes back - so if this returned early because
	// the Stats tab happened to be closed, pressing "+" would spend the point and the
	// number on screen would never move.
	if($CS_Open && isFunction("CS_renderAttr"))
		CS_renderAttr();
	if($MRPG_AttrOpen && isFunction("MRPG_renderAttrScreen"))
		MRPG_renderAttrScreen();

	// Level-up notice. Watches the point count for an increase; deliberately does not
	// open anything by itself (see AttributeScreen.cs).
	if(isFunction("MRPG_attrNotePoints"))
		MRPG_attrNotePoints();

	// Attributes render into the new Stats tab (the Skills swatch).
	if(isObject(MonsterRPGx_Skills) && !MonsterRPGx_Skills.isVisible())
		return;

	MRPG_buildPanels();
	MRPG_renderStatsTab();
}

// --- Traits ---
function clientCmdMRPG_TraitClear()
{
	$MRPG_TraitBufCount = 0;
}

function clientCmdMRPG_TraitRow(%data)
{
	%i = $MRPG_TraitBufCount;
	$MRPG_TraitBuf[%i, "name"]  = getField(%data, 0);
	$MRPG_TraitBuf[%i, "owned"] = getField(%data, 1);
	$MRPG_TraitBuf[%i, "ranks"] = getField(%data, 2);
	$MRPG_TraitBuf[%i, "lvl"]   = getField(%data, 3);
	$MRPG_TraitBuf[%i, "gold"]  = getField(%data, 4);
	$MRPG_TraitBuf[%i, "desc"]  = getField(%data, 5);
	$MRPG_TraitBufCount++;
}

function clientCmdMRPG_TraitDone()
{
	// Traits live on the right half of the Skills tab.
	if(isObject(MonsterRPGx_Skills) && !MonsterRPGx_Skills.isVisible())
		return;

	MRPG_buildPanels();
	MRPG_renderTraits();
}
