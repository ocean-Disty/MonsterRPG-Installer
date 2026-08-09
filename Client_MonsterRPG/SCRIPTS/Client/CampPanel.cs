//////////////////////////////////////////////////////////////////////////////
// CampPanel.cs  -  the settlement pool bar
//////////////////////////////////////////////////////////////////////////////
//
// Walk into a village or a bandit camp and this appears above the HUD: who lives here,
// how many of them are left, and one bar for the settlement's total remaining health.
// Every kill takes a visible bite out of it, and emptying it is the point.
//
// The server half is Server_MonsterRPG/Server/Tribes/Tribe_Sites.cs, and the design
// reasoning - why the pool is the simulated population and nothing resurrects - is in
// Server_MonsterRPG/CAMP_POOL_PLAN.md.
//
// -----------------------------------------------------------------------------
// WHY THE BAR IS SEGMENTED
// -----------------------------------------------------------------------------
//
// The pool's HP is latent members at full health plus live bodies at current health, so it
// drains CONTINUOUSLY while you fight and a death costs it almost nothing at the moment it
// lands - the victim was already near zero. That is arithmetically right and reads as mush.
//
// So the bar carries one tick per member. Continuous drain is the per-hit feedback;
// crossing a notch is the per-kill payoff. The kill flash is fired by its own message
// (MRPGCampKill) rather than inferred from the number moving, because two kills inside one
// push window would otherwise register as one.
//
// -----------------------------------------------------------------------------
// PARENTING - THREE TRAPS, ALL PREVIOUSLY PAID FOR
// -----------------------------------------------------------------------------
//
// 1. PARENT TO MonsterRPGx_MAIN_INTERFACE, NEVER MonsterRPGx_HUDBase. HUDBase is created by
//    its .gui and then abandoned - clientCmdaddMonsterRPGGUI reparents MAIN_INTERFACE out
//    of it into PlayGui and nothing ever adds HUDBase to the canvas. Controls added to it
//    are created, pass isObject(), and are never drawn.
//
// 2. BUILD BEFORE Parent::clientCmdaddMonsterRPGGUI. That call runs UIS_setOriginalValues()
//    then scaleNewCanvas(), and a control added afterwards has no recorded original, so
//    UIS_applyScaling pairs an already-scaled screen position with an unscaled extent and
//    throws it off-panel at every resolution except 1024x768.
//
//    (This is the OPPOSITE of Minimap.cs, which builds AFTER the parent call - it lives in
//    PlayGui rather than in the scaled tree and positions itself from getRes(). Both are
//    correct for where they live.)
//
// 3. GuiSwatchCtrl HAS NO setColor CONSOLE METHOD. Write .color directly.
//
// MAIN_INTERFACE is also the right home for a different reason: scaleNewCanvas anchors it
// at BOTTOM-CENTRE, which is exactly what this panel wants. The minimap had to leave that
// tree only because it wanted the true screen corner, which the anchoring cannot express.
//////////////////////////////////////////////////////////////////////////////

if($Camp::UI::Enabled $= ""){ $Camp::UI::Enabled = 1; }

//Authored against the HUD's 1024x768 space. The slot is the gap between MiddlePrintDlg
//(which ends at 546) and Bottom_UI (which starts at 662); the banner sits just above.
//
//360x44, CENTRED - down from 500x66. The first pass read as a box sitting on the HUD
//rather than as part of it: too wide to scan in one glance, and tall enough that the
//backdrop became the dominant shape on screen. The bar is the content; everything else is
//a label for it.
if($Camp::UI::PanelX $= ""){ $Camp::UI::PanelX = 332; }
if($Camp::UI::PanelY $= ""){ $Camp::UI::PanelY = 612; }
if($Camp::UI::PanelW $= ""){ $Camp::UI::PanelW = 360; }
if($Camp::UI::PanelH $= ""){ $Camp::UI::PanelH = 44;  }

//The bar, inside the panel. FULL WIDTH now - the old 14px side margins existed to inset it
//from a backdrop that is no longer drawn, and without the box they just made the bar look
//arbitrarily short.
$Camp::UI::BarX = 0;
$Camp::UI::BarY = 20;
$Camp::UI::BarW = 360;
$Camp::UI::BarH = 12;

//Ticks are allocated once at the largest pool we will segment and the surplus is hidden -
//the same allocate-big-and-hide pattern Minimap.cs uses for its grid. 48 is
//TribeConst::OUTPOST_CAPACITY; a settlement above that draws un-segmented rather than
//drawing a solid line of dividers.
$Camp::UI::MaxTicks = 48;

//Colour identity. Villages get the warmer, heavier treatment on purpose - you are the
//aggressor there and it should not feel like clearing a bandit camp.
$Camp::UI::VillageHex = "D9A441";
$Camp::UI::CampHex    = "C4472E";
$Camp::UI::VillageRGB = "217 164 65";
$Camp::UI::CampRGB    = "196 71 46";

//THE BACKDROP IS OFF BY DEFAULT, and that is the "too boxy" fix.
//
//A filled 500x66 rectangle at alpha 205 is a hard-edged slab, and nothing else on this HUD
//has one: the exp bar, the spell bar and the damage streak all float on their own art with
//no plate behind them. The bar bitmaps carry their own frame and every string in here is
//outlined (see MRPG_CampTextProfile), so the plate was buying contrast that was already
//paid for.
//
//Kept as a dial rather than deleted - raise BackdropA if a light biome ever needs it.
$Camp::UI::BackdropRGB = "10 12 16";
if($Camp::UI::BackdropA $= ""){ $Camp::UI::BackdropA = 0; }

//Animation. One 16ms loop drives everything; nothing here uses schedule(1,0,...) the way
//RPGExpLoop does - a per-millisecond reschedule is the most expensive thing on this HUD.
$Camp::UI::FrameMs   = 16;
$Camp::UI::RisePx    = 10;     //slide distance on enter
$Camp::UI::FadeInMs  = 200;
$Camp::UI::FadeOutMs = 250;

//////////////////////////////////////////////////////////////////////////////
// THE DAMAGE BAND  -  the two-speed bar
//
// The live fill drops to the new value almost immediately; a bright band is left standing
// where the bar USED to be, holds for a beat, and then slides down to meet it. The width of
// that band is the size of the hit, and it exists for exactly as long as it takes to read.
//
// This is the Exp_Fade_Percent / Exp_Percent idiom INVERTED. On the exp bar the ghost runs
// AHEAD to show what you gained; here it stays BEHIND to show what you just removed. Same
// two controls, opposite direction - copying that code verbatim animates backwards.
//
// THE TIMINGS ARE THE POINT AND THE FIRST PASS HAD THEM TOO SHORT. A 260ms hold with a
// 0.055 chase settles in about half a second, which is quick enough that the band is gone
// before the eye has left the bandit you are hitting - the animation was running but nobody
// could see it. Held for two thirds of a second and taken down over about a second and a
// half, the band is legible without ever being in the way.
//
//   FillRate   how fast the live fill chases the truth, per frame
//   GhostHold  ms the band stands still after a hit, before it starts falling
//   GhostRate  per-frame fraction of the remaining gap the band closes
$Camp::UI::FillRate   = 0.30;
$Camp::UI::GhostHold  = 650;
$Camp::UI::GhostRate  = 0.022;

//The band's own colour, and it is NOT the fill's. Drawn as the fill tone pushed most of the
//way to white, so it reads as the same bar lit up rather than as a second unrelated bar -
//and so it stays visible over both the village gold and the camp red without a per-kind
//colour of its own.
$Camp::UI::GhostMix   = 0.72;   //0 = the fill's colour, 1 = pure white
$Camp::UI::GhostAlpha = 235;

//A dead-zone. Below this fraction of the bar the band is not drawn at all: chip damage
//produces a one-pixel sliver that reads as a rendering artefact rather than as feedback.
$Camp::UI::GhostMinFrac = 0.004;

$Camp::UI::FlashMs    = 220;
$Camp::UI::PunchScale = 1.35;
$Camp::UI::PunchMs    = 260;
$Camp::UI::BannerMs   = 1600;

//Scaled down with the panel. The count stays the largest thing in the block because it is
//the number the player is actually driving to zero.
$Camp::UI::TitleSize = 14;
$Camp::UI::CountSize = 16;
$Camp::UI::ChipSize  = 10;
$Camp::UI::BannerSize = 24;


//////////////////////////////////////////////////////////////////////////////
// BUILD
//////////////////////////////////////////////////////////////////////////////
function MRPGCamp_Build()
{
	if(isObject(MRPG_CampPanel))
		return;

	if(!isObject(MonsterRPGx_MAIN_INTERFACE))
		return;

	if(!isObject(MRPG_CampTextProfile))
	{
		new GuiControlProfile(MRPG_CampTextProfile)
		{
			fontType  = "georgia bold";
			fontSize  = $Camp::UI::TitleSize;
			fontColor = "255 255 255 255";

			//Without an outline the text is unreadable the moment the backdrop is over
			//bright terrain - the same reason DamageStreak.cs outlines its numbers.
			fontOutline       = true;
			fontOutlineColor  = "0 0 0 255";
			fontOutlineOffset = "2 2";

			allowColorChars = 1;
			maxLength       = 255;
		};
	}

	%w = $Camp::UI::PanelW;
	%h = $Camp::UI::PanelH;

	%bx = $Camp::UI::BarX;
	%by = $Camp::UI::BarY;
	%bw = $Camp::UI::BarW;
	%bh = $Camp::UI::BarH;

	new GuiSwatchCtrl(MRPG_CampPanel)
	{
		profile     = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = $Camp::UI::PanelX SPC $Camp::UI::PanelY;
		extent      = %w SPC %h;
		minExtent   = "8 2";
		enabled     = "1";
		visible     = "0";
		color       = "0 0 0 0";

		new GuiSwatchCtrl(MRPG_CampBackdrop)
		{
			profile = "GuiDefaultProfile";
			horizSizing = "right";  vertSizing = "bottom";
			position = "0 0";       extent = %w SPC %h;
			minExtent = "8 2";      enabled = "1"; visible = "1";
			color = $Camp::UI::BackdropRGB SPC $Camp::UI::BackdropA;
		};

		new GuiMLTextCtrl(MRPG_CampTitle)
		{
			profile = "MRPG_CampTextProfile";
			horizSizing = "right";  vertSizing = "bottom";
			position = "2 1";       extent = "230 18";
			minExtent = "8 2";      enabled = "1"; visible = "1";
		};

		new GuiMLTextCtrl(MRPG_CampCount)
		{
			profile = "MRPG_CampTextProfile";
			horizSizing = "right";  vertSizing = "bottom";
			position = "232 0";     extent = "126 19";
			minExtent = "8 2";      enabled = "1"; visible = "1";
		};

		new GuiBitmapCtrl(MRPG_CampBarBG)
		{
			profile = "GuiDefaultProfile";
			horizSizing = "right";  vertSizing = "bottom";
			position = %bx SPC %by; extent = %bw SPC %bh;
			minExtent = "8 2";      enabled = "1"; visible = "1";
			bitmap = "./Bottom_Elements/HP_frame_bg.png";
			wrap = "0"; lockAspectRatio = "0"; alignLeft = "0"; alignTop = "0";
			overflowImage = "0"; keepCached = "1";
			mColor = "255 255 255 255"; mMultiply = "0";
		};

		//////////////////////////////////////////////////////////////////
		// clipToParent = "1" IS WHAT MAKES THIS A BAR AT ALL.
		//
		// The value is shown by narrowing the SWATCH while the bitmap inside stays at full
		// width; without clipping, the child draws its whole 472px regardless of how narrow
		// its parent is and the bar is permanently, convincingly full. Stock's exp bar sets
		// it on both the swatch and the bitmap (MonsterRPGx_HUDBase.gui:145-207) and this
		// mirrors that exactly rather than guessing which of the two carries it.
		//////////////////////////////////////////////////////////////////

		//GHOST FIRST, so the live fill draws over it. Sibling order is draw order.
		new GuiSwatchCtrl(MRPG_CampBarGhost)
		{
			profile = "GuiDefaultProfile";
			horizSizing = "right";  vertSizing = "bottom";
			position = %bx SPC %by; extent = %bw SPC %bh;
			minExtent = "1 2";      enabled = "1"; visible = "1";
			clipToParent = "1";
			color = "0 0 0 0";

			new GuiBitmapCtrl(MRPG_CampBarGhostFill)
			{
				profile = "GuiDefaultProfile";
				horizSizing = "right";  vertSizing = "bottom";
				position = "0 0";      extent = %bw SPC %bh;
				minExtent = "1 2";     enabled = "1"; visible = "1";
				clipToParent = "1";
				bitmap = "./Bottom_Elements/exp_fill.png";
				wrap = "0"; lockAspectRatio = "0"; alignLeft = "0"; alignTop = "0";
				overflowImage = "0"; keepCached = "1";
				mColor = "255 255 255 150"; mMultiply = "0";
			};
		};

		new GuiSwatchCtrl(MRPG_CampBarFill)
		{
			profile = "GuiDefaultProfile";
			horizSizing = "right";  vertSizing = "bottom";
			position = %bx SPC %by; extent = %bw SPC %bh;
			minExtent = "1 2";      enabled = "1"; visible = "1";
			clipToParent = "1";
			color = "0 0 0 0";

			new GuiBitmapCtrl(MRPG_CampBarFillImg)
			{
				profile = "GuiDefaultProfile";
				horizSizing = "right";  vertSizing = "bottom";
				position = "0 0";      extent = %bw SPC %bh;
				minExtent = "1 2";     enabled = "1"; visible = "1";
				clipToParent = "1";
				bitmap = "./Bottom_Elements/exp_fill.png";
				wrap = "0"; lockAspectRatio = "0"; alignLeft = "0"; alignTop = "0";
				overflowImage = "0"; keepCached = "1";
				mColor = "255 255 255 255"; mMultiply = "0";
			};
		};

		new GuiSwatchCtrl(MRPG_CampTickHolder)
		{
			profile = "GuiDefaultProfile";
			horizSizing = "right";  vertSizing = "bottom";
			position = "0 0";       extent = %w SPC %h;
			minExtent = "8 2";      enabled = "0"; visible = "1";
			color = "0 0 0 0";
		};

		new GuiBitmapCtrl(MRPG_CampBarFrame)
		{
			profile = "GuiDefaultProfile";
			horizSizing = "right";  vertSizing = "bottom";
			position = %bx SPC %by; extent = %bw SPC %bh;
			minExtent = "8 2";      enabled = "1"; visible = "1";
			bitmap = "./Bottom_Elements/exp_frame.png";
			wrap = "0"; lockAspectRatio = "0"; alignLeft = "0"; alignTop = "0";
			overflowImage = "0"; keepCached = "1";
			mColor = "255 255 255 255"; mMultiply = "0";
		};

		//THE RESERVED DETAIL SLOTS. Empty by design - the server can fill them with
		//anything (food, fortification, tribe relations, quest state) through
		//MRPGCampDetail without a client change. Building them now is what makes that
		//true later.
		//Empty by default, and an ML control with no text draws nothing - so the reserved
		//row costs no visible height until the server fills it.
		new GuiMLTextCtrl(MRPG_CampChip0)
		{
			profile = "MRPG_CampTextProfile";
			horizSizing = "right";  vertSizing = "bottom";
			position = "2 33";      extent = "116 12";
			minExtent = "8 2";      enabled = "1"; visible = "1";
		};
		new GuiMLTextCtrl(MRPG_CampChip1)
		{
			profile = "MRPG_CampTextProfile";
			horizSizing = "right";  vertSizing = "bottom";
			position = "122 33";    extent = "116 12";
			minExtent = "8 2";      enabled = "1"; visible = "1";
		};
		new GuiMLTextCtrl(MRPG_CampChip2)
		{
			profile = "MRPG_CampTextProfile";
			horizSizing = "right";  vertSizing = "bottom";
			position = "242 33";    extent = "116 12";
			minExtent = "8 2";      enabled = "1"; visible = "1";
		};
	};

	MonsterRPGx_MAIN_INTERFACE.add(MRPG_CampPanel);

	//THE TICKS. Allocated at the maximum and hidden, never created per settlement -
	//Minimap.cs:55 establishes the same rule for its grid, and creating 40 controls on
	//entering a village would hitch exactly when the panel is supposed to feel snappy.
	//
	//UIS_SkipScaling because these are laid out by hand from the bar's already-scaled
	//geometry (see MRPGCamp_LayoutTicks). Letting the scaler restore their authored
	//positions would undo that on every canvas rescale.
	for(%t = 0; %t < $Camp::UI::MaxTicks; %t++)
	{
		%tick = new GuiSwatchCtrl()
		{
			profile = "GuiDefaultProfile";
			horizSizing = "right";  vertSizing = "bottom";
			position = "0 0";       extent = "2" SPC $Camp::UI::BarH;
			minExtent = "1 2";      enabled = "0"; visible = "0";
			color = "0 0 0 150";
		};
		%tick.UIS_SkipScaling = true;
		MRPG_CampTickHolder.add(%tick);
		$MRPGCamp::Tick[%t] = %tick;
	}

	//THE BANNER IS A SIBLING, NOT A CHILD. The panel collapses and fades on a clear; the
	//banner has to survive that and outlive it, which it cannot do while inheriting the
	//panel's visibility.
	new GuiMLTextCtrl(MRPG_CampBanner)
	{
		profile     = "MRPG_CampTextProfile";
		horizSizing = "right";
		vertSizing  = "bottom";
		position    = $Camp::UI::PanelX SPC ($Camp::UI::PanelY - 28);
		extent      = $Camp::UI::PanelW SPC 26;
		minExtent   = "8 2";
		enabled     = "1";
		visible     = "0";
	};
	MonsterRPGx_MAIN_INTERFACE.add(MRPG_CampBanner);

	MRPGCamp_Reset();
}

function MRPGCamp_Reset()
{
	$MRPGCamp::Active     = 0;
	$MRPGCamp::Key        = "";
	$MRPGCamp::Kind       = 0;
	$MRPGCamp::HpMax      = 1;
	$MRPGCamp::HpTarget   = 0;
	$MRPGCamp::HpShown    = 0;
	$MRPGCamp::HpGhost    = 0;
	$MRPGCamp::GhostUntil = 0;
	$MRPGCamp::Pool       = 0;
	$MRPGCamp::PoolMax    = 0;
	$MRPGCamp::Alpha      = 0;
	$MRPGCamp::AlphaTarget = 0;
	$MRPGCamp::FlashUntil = 0;
	$MRPGCamp::PunchUntil = 0;
	$MRPGCamp::BannerUntil = 0;
	$MRPGCamp::LastGeom   = "";
}


//////////////////////////////////////////////////////////////////////////////
// GEOMETRY
//
// newScaledPos/newScaledExt are what UIS_applyScaling records, and they are the ONLY
// correct base for a runtime resize - the authored numbers are 1024x768 and would be wrong
// everywhere else. They do not exist until scaleNewCanvas has run once, hence the fallback.
//////////////////////////////////////////////////////////////////////////////
function MRPGCamp_BarGeom()
{
	if(!isObject(MRPG_CampBarBG))
		return "0 0 1 1";

	%pos = MRPG_CampBarBG.newScaledPos;
	%ext = MRPG_CampBarBG.newScaledExt;

	if(%pos $= "" || %ext $= "")
	{
		%pos = MRPG_CampBarBG.getPosition();
		%ext = MRPG_CampBarBG.getExtent();
	}

	return getWord(%pos, 0) SPC getWord(%pos, 1) SPC getWord(%ext, 0) SPC getWord(%ext, 1);
}

//One divider per member. Called on enter, whenever the pool count changes, and whenever
//the bar's scaled geometry moves under us (a resolution change mid-session).
function MRPGCamp_LayoutTicks()
{
	%geom = MRPGCamp_BarGeom();
	$MRPGCamp::LastGeom = %geom;

	%bx = getWord(%geom, 0);
	%by = getWord(%geom, 1);
	%bw = getWord(%geom, 2);
	%bh = getWord(%geom, 3);

	%n = $MRPGCamp::PoolMax;

	//Segmenting a very large settlement draws a solid line of dividers rather than a bar,
	//so past the ceiling it simply is not segmented.
	if(%n > $Camp::UI::MaxTicks || %n < 2)
		%n = 0;

	for(%t = 0; %t < $Camp::UI::MaxTicks; %t++)
	{
		%tick = $MRPGCamp::Tick[%t];
		if(!isObject(%tick))
			continue;

		//DIVIDERS SIT BETWEEN MEMBERS, so there are n-1 of them and none at either end.
		//
		//campActive is set HERE and nowhere else. It was also being set in
		//clientCmdMRPGCampEnter from the same expression, which is one derivation in two
		//places - and the loop's show/hide pass reads it every frame, so the two disagreeing
		//would leave a surplus divider drawn across an empty stretch of bar.
		%tick.campActive = (%t < %n - 1);

		if(!%tick.campActive)
		{
			%tick.setVisible(0);
			continue;
		}

		%x = %bx + mFloor(%bw * ((%t + 1) / %n));
		%tick.resize(%x, %by, 2, %bh);
		%tick.setVisible($MRPGCamp::Alpha > 0.85);
	}
}


//////////////////////////////////////////////////////////////////////////////
// TEXT
//////////////////////////////////////////////////////////////////////////////
function MRPGCamp_AccentHex()
{
	return ($MRPGCamp::Kind == 0) ? $Camp::UI::VillageHex : $Camp::UI::CampHex;
}

function MRPGCamp_RedrawText()
{
	if(!isObject(MRPG_CampTitle))
		return;

	%accent = MRPGCamp_AccentHex();
	%kindWord = ($MRPGCamp::Kind == 0) ? "Village" : "Camp";

	//A tight separator rather than three spaces: at 360 wide the name and the kind have to
	//read as one line, and the wide gap made them look like two labels that had collided.
	//
	//Plain ASCII on purpose - TorqueScript's lexer handles \n, \t, \r and the \cN colour
	//codes, NOT \xNN, so a hex escape for a middot would render as the literal "xb7".
	MRPG_CampTitle.setText("<font:georgia bold:" @ $Camp::UI::TitleSize @ ">"
		@ "<color:FFFFFF>" @ $MRPGCamp::Name
		@ "<font:georgia:" @ ($Camp::UI::TitleSize - 2) @ ">"
		@ "<color:" @ %accent @ "> - " @ %kindWord);

	//The count punches on a kill, so its size is animated rather than fixed.
	%size = $Camp::UI::CountSize;
	%now = getRealTime();
	if(%now < $MRPGCamp::PunchUntil)
	{
		%k = ($MRPGCamp::PunchUntil - %now) / $Camp::UI::PunchMs;
		%size = mFloor(%size * (1 + (($Camp::UI::PunchScale - 1) * %k)));
	}

	%numCol = ($MRPGCamp::Pool <= 0) ? "FFFFFF" : %accent;

	MRPG_CampCount.setText("<just:right><font:georgia bold:" @ %size @ ">"
		@ "<color:" @ %numCol @ ">" @ $MRPGCamp::Pool
		@ "<font:georgia:" @ ($Camp::UI::CountSize - 6) @ "><color:9A9A9A> / "
		@ $MRPGCamp::PoolMax);
}

function MRPGCamp_SetChip(%slot, %label, %value)
{
	%ctrl = "MRPG_CampChip" @ %slot;
	if(!isObject(%ctrl))
		return;

	if(%label $= "" && %value $= "")
	{
		%ctrl.setText("");
		return;
	}

	%ctrl.setText("<font:georgia:" @ $Camp::UI::ChipSize @ "><color:8A8A8A>" @ %label
		@ "  <color:D8D8D8>" @ %value);
}


//////////////////////////////////////////////////////////////////////////////
// THE ANIMATION LOOP
//
// ONE loop, 16ms, cancelled when the panel is fully hidden. Everything - slide, fade, fill,
// ghost, flash, punch, banner - is a function of the clock, so a dropped frame catches up
// instead of desynchronising.
//////////////////////////////////////////////////////////////////////////////
function MRPGCamp_Tick()
{
	cancel($MRPGCamp::Sched);

	//GATE FIRST. This is a 16ms loop and its only stop condition used to be "the
	//panel object is gone" - so once built it ran sixty times a second for the
	//rest of the session, including in the main menu. Returning without
	//rescheduling ends it at the first tick after the connection goes even if
	//MRPGCamp_Shutdown is never reached. See ServerGate.cs rule 3.
	if(!MRPG_isActive())
		return;

	if(!isObject(MRPG_CampPanel))
		return;

	%now = getRealTime();

	//---- alpha / slide ----------------------------------------------------
	%step = $Camp::UI::FrameMs / (($MRPGCamp::AlphaTarget > $MRPGCamp::Alpha)
	                              ? $Camp::UI::FadeInMs : $Camp::UI::FadeOutMs);

	if($MRPGCamp::Alpha < $MRPGCamp::AlphaTarget)
	{
		$MRPGCamp::Alpha += %step;
		if($MRPGCamp::Alpha > 1){ $MRPGCamp::Alpha = 1; }
	}
	else if($MRPGCamp::Alpha > $MRPGCamp::AlphaTarget)
	{
		$MRPGCamp::Alpha -= %step;
		if($MRPGCamp::Alpha < 0){ $MRPGCamp::Alpha = 0; }
	}

	%a = $MRPGCamp::Alpha;

	//Ease-out on the way in so it settles rather than arrives.
	%ease = 1 - ((1 - %a) * (1 - %a));

	if(%a <= 0 && $MRPGCamp::AlphaTarget <= 0)
	{
		MRPG_CampPanel.setVisible(0);
		if(%now >= $MRPGCamp::BannerUntil)
		{
			if(isObject(MRPG_CampBanner))
				MRPG_CampBanner.setVisible(0);
			return;                        //fully at rest - stop the loop
		}
	}
	else
	{
		MRPG_CampPanel.setVisible(1);
	}

	//---- the fill and its ghost -------------------------------------------
	%frac = ($MRPGCamp::HpMax > 0) ? ($MRPGCamp::HpTarget / $MRPGCamp::HpMax) : 0;
	if(%frac < 0){ %frac = 0; }
	if(%frac > 1){ %frac = 1; }

	$MRPGCamp::HpShown += (%frac - $MRPGCamp::HpShown) * $Camp::UI::FillRate;
	if(mAbs(%frac - $MRPGCamp::HpShown) < 0.0008)
		$MRPGCamp::HpShown = %frac;

	//The band holds where it was, then slides down to meet the fill. IT NEVER RISES: a band
	//above the fill is "what you just removed", and a band BELOW it would read as the bar
	//refilling, which is precisely the thing this design refuses to do.
	//
	//The hold is re-armed by every hit, not only by a kill - MRPGCampPool now arrives on a
	//landed swing (Tribe_Sites.cs Sites_NoteHit), so a flurry of hits keeps the band standing
	//at the health you had when the flurry started and drops it once in one piece afterwards.
	//That is far more legible than a band that restarts its fall on every packet.
	if($MRPGCamp::HpGhost < $MRPGCamp::HpShown)
		$MRPGCamp::HpGhost = $MRPGCamp::HpShown;
	else if(%now >= $MRPGCamp::GhostUntil)
	{
		$MRPGCamp::HpGhost -= ($MRPGCamp::HpGhost - $MRPGCamp::HpShown) * $Camp::UI::GhostRate;
		if(($MRPGCamp::HpGhost - $MRPGCamp::HpShown) < 0.0008)
			$MRPGCamp::HpGhost = $MRPGCamp::HpShown;
	}

	%geom = MRPGCamp_BarGeom();
	if(%geom !$= $MRPGCamp::LastGeom)
		MRPGCamp_LayoutTicks();

	%bx = getWord(%geom, 0);
	%by = getWord(%geom, 1);
	%bw = getWord(%geom, 2);
	%bh = getWord(%geom, 3);

	//A width of 0 makes the control vanish AND makes the next resize ambiguous, so the
	//floor is 1px - an empty bar reads as empty either way.
	%fw = mFloor(%bw * $MRPGCamp::HpShown);
	%gw = mFloor(%bw * $MRPGCamp::HpGhost);
	if(%fw < 1){ %fw = 1; }
	if(%gw < 1){ %gw = 1; }

	MRPG_CampBarFill.resize(%bx, %by, %fw, %bh);
	MRPG_CampBarGhost.resize(%bx, %by, %gw, %bh);

	//---- colour, flash, alpha --------------------------------------------
	%rgb = ($MRPGCamp::Kind == 0) ? $Camp::UI::VillageRGB : $Camp::UI::CampRGB;
	%r = getWord(%rgb, 0);
	%g = getWord(%rgb, 1);
	%b = getWord(%rgb, 2);

	//THE BAND IS MIXED FROM THE UNFLASHED TONE. Kept before the flash is applied because the
	//flash drives the fill toward white too - mixing the band from an already-white fill
	//would make both of them white for the couple of frames after a kill, which is the one
	//moment the band is largest and most worth seeing.
	%baseR = %r;
	%baseG = %g;
	%baseB = %b;

	//The flash whitens the fill for a couple of frames after a kill. Additive toward white
	//rather than a colour swap, so a flash on a nearly-empty bar still reads.
	%flash = 0;
	if(%now < $MRPGCamp::FlashUntil)
		%flash = ($MRPGCamp::FlashUntil - %now) / $Camp::UI::FlashMs;

	%r = mFloor(%r + ((255 - %r) * %flash));
	%g = mFloor(%g + ((255 - %g) * %flash));
	%b = mFloor(%b + ((255 - %b) * %flash));

	%fa = mFloor(255 * %ease);
	MRPG_CampBarFillImg.mColor  = %r SPC %g SPC %b SPC %fa;
	MRPG_CampBarBG.mColor       = "255 255 255" SPC %fa;
	MRPG_CampBarFrame.mColor    = "255 255 255" SPC %fa;

	//////////////////////////////////////////////////////////////////////
	// THE DAMAGE BAND'S COLOUR
	//
	// The fill tone pushed toward white by GhostMix, not flat white. Flat white made the band
	// look like a different bar that had appeared behind this one; keeping the hue means it
	// reads as the same bar still lit where the health used to be.
	//
	// It is also NOT dimmed relative to the fill. The old 140 alpha against the fill's 255
	// made the very thing being signalled the faintest element on the panel - a hit you have
	// to look for is not feedback.
	//////////////////////////////////////////////////////////////////////
	%m = $Camp::UI::GhostMix;
	%gr = mFloor(%baseR + ((255 - %baseR) * %m));
	%gg = mFloor(%baseG + ((255 - %baseG) * %m));
	%gb = mFloor(%baseB + ((255 - %baseB) * %m));

	//Hidden outright when the band is thinner than the dead-zone, so chip damage does not
	//leave a one-pixel line of a completely different colour on the end of the bar.
	%bandFrac = $MRPGCamp::HpGhost - $MRPGCamp::HpShown;
	%ga = (%bandFrac >= $Camp::UI::GhostMinFrac)
	    ? mFloor($Camp::UI::GhostAlpha * %ease) : 0;

	MRPG_CampBarGhostFill.mColor = %gr SPC %gg SPC %gb SPC %ga;

	MRPG_CampBackdrop.color = $Camp::UI::BackdropRGB SPC mFloor($Camp::UI::BackdropA * %ease);

	//---- the slide --------------------------------------------------------
	%pos = MRPG_CampPanel.newScaledPos;
	if(%pos $= "")
		%pos = $Camp::UI::PanelX SPC $Camp::UI::PanelY;

	%ext = MRPG_CampPanel.newScaledExt;
	if(%ext $= "")
		%ext = $Camp::UI::PanelW SPC $Camp::UI::PanelH;

	%rise = mFloor($Camp::UI::RisePx * (1 - %ease));
	MRPG_CampPanel.resize(getWord(%pos, 0), getWord(%pos, 1) + %rise,
		getWord(%ext, 0), getWord(%ext, 1));

	//---- text -------------------------------------------------------------
	//Only while something is moving. setText reflows an ML control, and doing it 60 times a
	//second for a string that has not changed is the one genuinely wasteful thing this loop
	//could do.
	if(%now < $MRPGCamp::PunchUntil || $MRPGCamp::TextDirty)
	{
		$MRPGCamp::TextDirty = (%now < $MRPGCamp::PunchUntil);
		MRPGCamp_RedrawText();
	}

	//---- banner -----------------------------------------------------------
	if(isObject(MRPG_CampBanner) && %now < $MRPGCamp::BannerUntil)
	{
		%k = ($MRPGCamp::BannerUntil - %now) / $Camp::UI::BannerMs;
		MRPG_CampBanner.setVisible(1);

		//Grows very slightly and fades toward the accent as it goes.
		%bs = mFloor($Camp::UI::BannerSize * (1 + (0.10 * %k)));
		MRPG_CampBanner.setText("<just:center><font:georgia bold italic:" @ %bs @ ">"
			@ "<color:" @ ((%k > 0.82) ? "FFFFFF" : MRPGCamp_AccentHex()) @ ">"
			@ $MRPGCamp::BannerText);
	}
	else if(isObject(MRPG_CampBanner) && %now >= $MRPGCamp::BannerUntil)
		MRPG_CampBanner.setVisible(0);

	//Ticks appear once the panel has essentially arrived - popping them in at the end reads
	//as the bar resolving rather than as a second element sliding in.
	%showTicks = (%a > 0.85);
	for(%t = 0; %t < $Camp::UI::MaxTicks; %t++)
	{
		%tick = $MRPGCamp::Tick[%t];
		if(!isObject(%tick))
			continue;
		if(%tick.campActive)
			%tick.setVisible(%showTicks);
	}

	$MRPGCamp::Sched = schedule($Camp::UI::FrameMs, 0, MRPGCamp_Tick);
}

function MRPGCamp_Wake()
{
	if(!isEventPending($MRPGCamp::Sched))
		MRPGCamp_Tick();
}


//////////////////////////////////////////////////////////////////////////////
// SERVER COMMANDS
//////////////////////////////////////////////////////////////////////////////
function clientCmdMRPGCampEnter(%key, %kind, %name, %tribe, %pool, %poolMax, %hp, %hpMax, %cleared)
{
	if(!$Camp::UI::Enabled)
		return;

	MRPGCamp_Build();
	if(!isObject(MRPG_CampPanel))
		return;

	$MRPGCamp::Active  = 1;
	$MRPGCamp::Key     = %key;
	$MRPGCamp::Kind    = %kind;
	$MRPGCamp::Name    = (%name $= "") ? "Settlement" : %name;
	$MRPGCamp::Tribe   = %tribe;
	$MRPGCamp::Pool    = %pool;
	$MRPGCamp::PoolMax = (%poolMax > 0) ? %poolMax : 1;
	$MRPGCamp::HpMax   = (%hpMax > 0) ? %hpMax : 1;
	$MRPGCamp::HpTarget = %hp;

	//FILL FROM ZERO ON ENTER. Snapping to the current value hides the one thing worth
	//showing at that moment - how big the fight you just walked into actually is.
	$MRPGCamp::HpShown = 0;
	$MRPGCamp::HpGhost = 0;
	$MRPGCamp::GhostUntil = 0;

	$MRPGCamp::AlphaTarget = 1;
	$MRPGCamp::TextDirty = 1;

	MRPGCamp_SetChip(0, "", "");
	MRPGCamp_SetChip(1, "", "");
	MRPGCamp_SetChip(2, "", "");

	MRPGCamp_RedrawText();

	//Also marks which dividers belong to this settlement - see the campActive note there.
	MRPGCamp_LayoutTicks();

	MRPGCamp_Wake();
}

//////////////////////////////////////////////////////////////////////////////
// THE DENOMINATORS ARRIVE WITH EVERY UPDATE  -  the "29 / 28" fix
//
// PoolMax and HpMax used to be set once, from MRPGCampEnter, and never touched again. The
// server raises both as high-water marks (a village that has a birth while you are standing
// in it goes from 28 to 29), so the panel could hold a denominator the server had already
// moved past - and print "29 / 28".
//
// Applying them here rather than only trusting the enter message also means a client that
// joined mid-fight, or missed a packet, self-corrects on the next one instead of carrying a
// wrong bar until it leaves and comes back.
//
// RAISE-ONLY, exactly as the server does. Letting the maximum fall would make the bar jump
// upward when someone dies, which is worse than a bar that simply never fills.
//////////////////////////////////////////////////////////////////////////////
function MRPGCamp_NoteMax(%hpMax, %poolMax)
{
	if(%poolMax > $MRPGCamp::PoolMax)
	{
		$MRPGCamp::PoolMax = %poolMax;
		$MRPGCamp::TextDirty = 1;

		//The dividers are one per member, so the roster growing re-lays them out.
		MRPGCamp_LayoutTicks();
	}

	if(%hpMax > $MRPGCamp::HpMax)
		$MRPGCamp::HpMax = %hpMax;
}

function clientCmdMRPGCampPool(%hp, %pool, %hpMax, %poolMax)
{
	if(!$MRPGCamp::Active)
		return;

	MRPGCamp_NoteMax(%hpMax, %poolMax);

	if(%pool != $MRPGCamp::Pool)
	{
		$MRPGCamp::Pool = %pool;
		$MRPGCamp::TextDirty = 1;
	}

	//THE BAND IS ARMED BY DAMAGE, NOT ONLY BY DEATH. This message now arrives on a landed
	//hit (Sites_NoteHit), so holding the band here is what makes "you took a bite out of the
	//camp" visible per swing rather than only per kill. Re-arming an already-held band just
	//extends the hold, which is what a flurry of hits should do.
	if(%hp < $MRPGCamp::HpTarget)
		$MRPGCamp::GhostUntil = getRealTime() + $Camp::UI::GhostHold;

	$MRPGCamp::HpTarget = %hp;
	MRPGCamp_Wake();
}

//THE FLASH. A separate message from the pool update on purpose - the panel needs an EVENT
//to react to, and "did the number go down since the last packet" collapses the moment two
//kills land inside one push window.
function clientCmdMRPGCampKill(%victim, %hp, %pool, %hpMax, %poolMax)
{
	if(!$MRPGCamp::Active)
		return;

	%now = getRealTime();

	MRPGCamp_NoteMax(%hpMax, %poolMax);

	$MRPGCamp::HpTarget = %hp;
	$MRPGCamp::Pool     = %pool;
	$MRPGCamp::TextDirty = 1;

	$MRPGCamp::FlashUntil = %now + $Camp::UI::FlashMs;
	$MRPGCamp::PunchUntil = %now + $Camp::UI::PunchMs;

	//Hold the ghost where it is so the band it leaves behind is the size of the kill.
	$MRPGCamp::GhostUntil = %now + $Camp::UI::GhostHold;

	MRPGCamp_Wake();
}

function clientCmdMRPGCampCleared(%kind, %name)
{
	if(!isObject(MRPG_CampBanner))
		return;

	$MRPGCamp::BannerText = (%kind == 0) ? "VILLAGE FALLEN" : "CAMP CLEARED";
	$MRPGCamp::BannerUntil = getRealTime() + $Camp::UI::BannerMs;

	//NO SOUND HERE. The server plays it with %client.play2D (Sites_PushCleared), which is
	//how every other cue in this game is played and is the only version that works for a
	//client who does not have Server_MonsterRPG's files on disk - which is every client
	//connecting to a real server.

	MRPGCamp_Wake();
}

function clientCmdMRPGCampLeave()
{
	$MRPGCamp::Active = 0;
	$MRPGCamp::AlphaTarget = 0;
	MRPGCamp_Wake();
}

//The extension point. Slot 0..2; an empty label and value clears the chip.
function clientCmdMRPGCampDetail(%slot, %label, %value)
{
	if(!isObject(MRPG_CampPanel) || %slot < 0 || %slot > 2)
		return;

	MRPGCamp_SetChip(%slot, %label, %value);
}


//////////////////////////////////////////////////////////////////////////////
// BUILD HOOK
//
// BEFORE Parent::, unlike Minimap.cs - see trap 2 in the header. This panel lives inside
// MAIN_INTERFACE and must exist when UIS_setOriginalValues walks the tree.
//////////////////////////////////////////////////////////////////////////////
package MRPGCampPanel
{
	function clientCmdaddMonsterRPGGUI()
	{
		if($Camp::UI::Enabled)
			MRPGCamp_Build();

		Parent::clientCmdaddMonsterRPGGUI();
	}
};

//activatePackage IS the guard. Blockland exports isPackage and activatePackage but not
//isActivePackage, and TGE's Namespace::activatePackage returns early when the package is
//already on (consoleInternal.cc:975), so calling it twice is a no-op.
activatePackage(MRPGCampPanel);


//////////////////////////////////////////////////////////////////////////////
// FALLBACK
//
// addMonsterRPGGUI is sent ONCE per client and is guarded server-side by
// %client.setMonsterRPGGUI (Core_OLDpackage.cs:2613), so a client that reconnects - or that
// exec'd this file after the command had already been and gone - would otherwise never get
// a panel and the first MRPGCampEnter would silently do nothing.
//
// Minimap.cs carries the same fallback for the same reason.
//////////////////////////////////////////////////////////////////////////////
function MRPGCamp_EnsureBuilt()
{
	//Left (or never joined) a MonsterRPG server while this was pending. Stop, and
	//do NOT re-arm - the retry below is a loop and this is its only exit.
	if(!MRPG_isActive())
		return;

	if(isObject(MRPG_CampPanel))
		return;

	if(isObject(MonsterRPGx_MAIN_INTERFACE))
	{
		MRPGCamp_Build();

		//Built late, so it missed UIS_setOriginalValues. Record and scale it now or it sits
		//at its authored 1024x768 coordinates on every other resolution.
		if(isObject(MRPG_CampPanel))
		{
			MRPG_CampPanel.UIS_setOriginalValues();
			MRPG_CampBanner.UIS_setOriginalValues();

			if(isFunction("scaleNewCanvas"))
				scaleNewCanvas(MonsterRPGx_MAIN_INTERFACE);
		}
		return;
	}

	$MRPG::Gate::CampSch = schedule(2000, 0, MRPGCamp_EnsureBuilt);
}


//////////////////////////////////////////////////////////////////////////////
// TEARDOWN
//
// HIDDEN, NOT DELETED - the opposite call to Minimap_Shutdown, and for a concrete
// reason. This panel is a child of MonsterRPGx_MAIN_INTERFACE, which is itself
// hidden on disconnect and only ever shown on a MonsterRPG server, so nothing
// here can appear on somebody else's screen. It is also measured by
// UIS_setOriginalValues during clientCmdaddMonsterRPGGUI; destroying it would mean
// rebuilding it after that measurement on every rejoin and re-running the
// late-build scaling dance below. The minimap has neither property - it lives in
// PlayGui and positions itself from getRes() - which is why that one goes.
//
// What actually has to stop is the 16ms tick.
//////////////////////////////////////////////////////////////////////////////
function MRPGCamp_Shutdown()
{
	cancel($MRPGCamp::Sched);
	$MRPGCamp::Sched = "";

	MRPGCamp_Reset();

	if(isObject(MRPG_CampPanel))
		MRPG_CampPanel.setVisible(0);
	if(isObject(MRPG_CampBanner))
		MRPG_CampBanner.setVisible(0);
}


//NOTHING IS ARMED AT FILE SCOPE. This file used to end with
//`schedule(6000, 0, MRPGCamp_EnsureBuilt)`, which built the panel and started the
//16ms animation tick six seconds after Blockland launched, on every client,
//forever. The fallback is armed from MRPG_ClientEnter() now - same six seconds,
//measured from the join. See ServerGate.cs.
