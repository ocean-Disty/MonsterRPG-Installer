////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////
//////////////////// SAMURAI-STYLE DAMAGE STREAK COUNTER ///////////////////
////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////

// The per-hit numbers live in the world as shape names (server side, see
// displayDamageText in Core_Overrides.cs). This is the running streak total that
// rides along with them - hit count + accumulated damage, punching on every hit
// and easing out when the streak lapses.
//
// This is a GuiMLTextCtrl rather than a plain text ctrl on purpose: <font:face:size>
// and <color:> markup are parsed by ML controls, which is what lets the size animate
// per-frame. A plain GuiTextCtrl would be locked to whatever its profile says.

$Pref::Client::MonsterRPGx::StreakFont = "georgia bold italic";

// Where the counter sits, as a fraction of the screen - so it lands in the same
// spot at any resolution. These are the CENTRE of the block, not its corner.
// X 0.5 is dead centre, 1.0 is the right edge. Y 0.5 is the crosshair line.
// Nudge live from the console with e.g.  MRPGStreak_SetPos(0.5, 0.62);
if($Pref::Client::MonsterRPGx::StreakX $= "")
    $Pref::Client::MonsterRPGx::StreakX = 0.5;

// Sits below the crosshair on purpose - dead centre would cover the enemy
// you're actually hitting, and the bottom HUD bars start around 0.85.
if($Pref::Client::MonsterRPGx::StreakY $= "")
    $Pref::Client::MonsterRPGx::StreakY = 0.64;

$MRPG::Streak::HitSize   = 44;   // the big number
$MRPG::Streak::LabelSize = 17;   // "HITS"
$MRPG::Streak::TotalSize = 26;   // accumulated damage

$MRPG::Streak::PunchHit  = 1.42; // scale spike on a normal hit
$MRPG::Streak::PunchCrit = 1.75; // ...and on a crit

$MRPG::Streak::FrameMS   = 16;

// --- Perfect parry ---------------------------------------------------------
// Rides the same profile and the same screen anchor as the streak counter, so
// the two read as one system. Sits just above the streak block.
$MRPG::Parry::Size    = 33;
$MRPG::Parry::Flash   = "FFFFFF";  // first few frames - the "hit" of the flash
$MRPG::Parry::Color   = "9FE8FF";  // settles to ice blue: precise, not loud
// Sits BELOW the streak block, not above it. Above would put the text straight
// through the crosshair at the default anchor, and it keeps the whole stack
// travelling together if StreakY gets moved.
$MRPG::Parry::GapPx   = 12;

// Kept deliberately short. A parry reward that lingers becomes visual noise
// during a fight, so it flashes, holds briefly, and clears out.
$MRPG::Parry::PeakMS  = 140;   // grow to overshoot
$MRPG::Parry::SetMS   = 280;   // overshoot settles back to 1.0
$MRPG::Parry::HoldMS  = 640;   // steady
$MRPG::Parry::LifeMS  = 1150;  // gone

function MRPGStreak_Build()
{
    if(isObject(MRPG_StreakHUD))
        return;

    if(!isObject(MRPG_StreakProfile))
    {
        new GuiControlProfile(MRPG_StreakProfile)
        {
            fontType  = $Pref::Client::MonsterRPGx::StreakFont;
            fontSize  = $MRPG::Streak::HitSize;
            fontColor = "255 255 255 255";

            // Without an outline the numbers vanish against bright terrain.
            fontOutline       = true;
            fontOutlineColor  = "0 0 0 255";
            fontOutlineOffset = "2 2";

            allowColorChars = 1;
            maxLength       = 255;
            justify         = "Center";
        };
    }

    %w = 460;
    %h = 130;

    new GuiControl(MRPG_StreakHUD)
    {
        profile     = "GuiDefaultProfile";
        horizSizing = "center";
        vertSizing  = "center";
        position    = "0 0";
        extent      = %w SPC %h;
        minExtent   = "8 2";
        enabled     = "1";
        visible     = "0";

        new GuiMLTextCtrl(MRPG_StreakHits)
        {
            profile     = "MRPG_StreakProfile";
            horizSizing = "width";
            vertSizing  = "bottom";
            position    = "0 0";
            extent      = %w SPC 70;
            minExtent   = "8 2";
            enabled     = "1";
            visible     = "1";
        };

        new GuiMLTextCtrl(MRPG_StreakTotal)
        {
            profile     = "MRPG_StreakProfile";
            horizSizing = "width";
            vertSizing  = "bottom";
            position    = "0 72";
            extent      = %w SPC 46;
            minExtent   = "8 2";
            enabled     = "1";
            visible     = "1";
        };
    };

    PlayGui.add(MRPG_StreakHUD);

    // Sibling rather than a child of MRPG_StreakHUD on purpose - the streak block
    // drifts and dims on its own schedule, and the parry must not inherit that.
    new GuiMLTextCtrl(MRPG_ParryPopup)
    {
        profile     = "MRPG_StreakProfile";
        horizSizing = "center";
        vertSizing  = "center";
        position    = "0 0";
        extent      = %w SPC 54;
        minExtent   = "8 2";
        enabled     = "1";
        visible     = "0";
    };

    PlayGui.add(MRPG_ParryPopup);

    MRPGStreak_Layout();
}

// Kept separate from Build so it can re-run on every streak - that way a
// resolution change mid-session doesn't strand the counter off its mark.
function MRPGStreak_Layout()
{
    if(!isObject(MRPG_StreakHUD))
        return;

    %canvasExt = Canvas.getExtent();
    %cw = getWord(%canvasExt, 0);
    %ch = getWord(%canvasExt, 1);

    %ext = MRPG_StreakHUD.getExtent();
    %w = getWord(%ext, 0);
    %h = getWord(%ext, 1);

    MRPG_StreakHUD.baseX = mFloor((%cw * $Pref::Client::MonsterRPGx::StreakX) - (%w / 2));
    MRPG_StreakHUD.baseY = mFloor((%ch * $Pref::Client::MonsterRPGx::StreakY) - (%h / 2));

    MRPG_StreakHUD.resize(MRPG_StreakHUD.baseX, MRPG_StreakHUD.baseY, %w, %h);

    if(isObject(MRPG_ParryPopup))
    {
        %pExt = MRPG_ParryPopup.getExtent();
        %pw = getWord(%pExt, 0);
        %ph = getWord(%pExt, 1);

        MRPG_ParryPopup.baseX = mFloor((%cw * $Pref::Client::MonsterRPGx::StreakX) - (%pw / 2));
        MRPG_ParryPopup.baseY = MRPG_StreakHUD.baseY + %h + $MRPG::Parry::GapPx;

        MRPG_ParryPopup.resize(MRPG_ParryPopup.baseX, MRPG_ParryPopup.baseY, %pw, %ph);
    }
}

// Console helper for dialling the position in without editing the file.
function MRPGStreak_SetPos(%x, %y)
{
    $Pref::Client::MonsterRPGx::StreakX = %x;
    $Pref::Client::MonsterRPGx::StreakY = %y;

    MRPGStreak_Layout();
    echo("Streak counter at X" SPC %x SPC "Y" SPC %y);
}

// Musou-style escalation - the longer you hold the streak the hotter it reads.
function MRPGStreak_TierColor(%hits)
{
    if(%hits >= 100) return "FF2D6F";
    if(%hits >= 50)  return "FF4E4E";
    if(%hits >= 25)  return "FF9A3C";
    if(%hits >= 10)  return "FFD75E";
    return "FFF4D6";
}

// Scales an RRGGBB hex toward black. Used for the fade-out: GuiMLTextCtrl colour
// tags have no dependable alpha channel across builds, so dimming the colour and
// shrinking the text together is what sells the fade instead.
function MRPGStreak_DimColor(%hex, %factor)
{
    %r = mFloor(MRPGStreak_HexToInt(getSubStr(%hex, 0, 2)) * %factor);
    %g = mFloor(MRPGStreak_HexToInt(getSubStr(%hex, 2, 2)) * %factor);
    %b = mFloor(MRPGStreak_HexToInt(getSubStr(%hex, 4, 2)) * %factor);

    return MRPGStreak_Hex2(%r) @ MRPGStreak_Hex2(%g) @ MRPGStreak_Hex2(%b);
}

// Deliberately avoids the % modulo operator - nothing else in this codebase uses it
// and it reads ambiguously against TorqueScript's %variable prefix.
function MRPGStreak_Hex2(%v)
{
    %digits = "0123456789ABCDEF";
    %v  = mClamp(mFloor(%v), 0, 255);
    %hi = mFloor(%v / 16);
    %lo = %v - (%hi * 16);

    return getSubStr(%digits, %hi, 1) @ getSubStr(%digits, %lo, 1);
}

// Blends two RRGGBB hexes. Used for the parry's white-hot flash easing down
// into its resting colour rather than snapping between the two.
function MRPGStreak_LerpColor(%a, %b, %k)
{
    %k = mClamp(%k, 0, 1);
    %out = "";

    for(%i = 0; %i < 3; %i++)
    {
        %ca = MRPGStreak_HexToInt(getSubStr(%a, %i * 2, 2));
        %cb = MRPGStreak_HexToInt(getSubStr(%b, %i * 2, 2));
        %out = %out @ MRPGStreak_Hex2(%ca + ((%cb - %ca) * %k));
    }

    return %out;
}

function MRPGStreak_HexToInt(%hex)
{
    %digits = "0123456789ABCDEF";
    %hex = strUpr(%hex);
    %out = 0;

    for(%i = 0; %i < strLen(%hex); %i++)
        %out = (%out * 16) + strPos(%digits, getSubStr(%hex, %i, 1));

    return %out;
}

function MRPGStreak_Commas(%n)
{
    %n = mFloor(%n);
    %out = "";

    while(strLen(%n) > 3)
    {
        %out = "," @ getSubStr(%n, strLen(%n) - 3, 3) @ %out;
        %n = getSubStr(%n, 0, strLen(%n) - 3);
    }

    return %n @ %out;
}

function MRPGStreak_Redraw()
{
    if(!isObject(MRPG_StreakHUD))
        return;

    %scale = $MRPG::Streak::Punch * $MRPG::Streak::Grow;
    %dim   = $MRPG::Streak::Fade;

    %font  = $Pref::Client::MonsterRPGx::StreakFont;
    %color = MRPGStreak_DimColor(MRPGStreak_TierColor($MRPG::Streak::Hits), %dim);
    %white = MRPGStreak_DimColor("FFFFFF", %dim * 0.85);

    %hitSize   = mFloor($MRPG::Streak::HitSize   * %scale);
    %labelSize = mFloor($MRPG::Streak::LabelSize * %scale);
    %totalSize = mFloor($MRPG::Streak::TotalSize * %scale);

    MRPG_StreakHits.setText(
        "<just:center><color:" @ %color @ "><font:" @ %font @ ":" @ %hitSize @ ">" @
        $MRPG::Streak::Hits @
        "<font:" @ %font @ ":" @ %labelSize @ "> HITS"
    );

    MRPG_StreakTotal.setText(
        "<just:center><color:" @ %white @ "><font:" @ %font @ ":" @ %totalSize @ ">" @
        MRPGStreak_Commas($MRPG::Streak::Total) @ " DAMAGE"
    );

    // Drift upward as it dies out.
    %lift = mFloor((1.0 - %dim) * 26);
    MRPG_StreakHUD.resize(
        MRPG_StreakHUD.baseX,
        MRPG_StreakHUD.baseY - %lift,
        getWord(MRPG_StreakHUD.getExtent(), 0),
        getWord(MRPG_StreakHUD.getExtent(), 1)
    );
}

function MRPGStreak_Loop()
{
    cancel($MRPG::Streak::LoopSch);

    //Gate, and do NOT reschedule when it is shut - see ServerGate.cs rule 3.
    if(!MRPG_isActive())
        return;

    if(!isObject(MRPG_StreakHUD))
        return;

    %active = 0;

    // Punch decays back toward 1.0 after every hit.
    if($MRPG::Streak::Punch > 1.0)
    {
        $MRPG::Streak::Punch -= 0.055;
        if($MRPG::Streak::Punch < 1.0)
            $MRPG::Streak::Punch = 1.0;
        %active = 1;
    }

    if($MRPG::Streak::Phase $= "in")
    {
        // Fast scale-up from nothing so the counter snaps into existence.
        $MRPG::Streak::Grow += 0.14;
        $MRPG::Streak::Fade += 0.22;

        if($MRPG::Streak::Grow >= 1.0)
            $MRPG::Streak::Grow = 1.0;

        if($MRPG::Streak::Fade >= 1.0)
        {
            $MRPG::Streak::Fade = 1.0;
            $MRPG::Streak::Phase = "hold";
        }
        %active = 1;
    }
    else if($MRPG::Streak::Phase $= "out")
    {
        $MRPG::Streak::Fade -= 0.045;
        $MRPG::Streak::Grow -= 0.012;

        if($MRPG::Streak::Fade <= 0)
        {
            $MRPG::Streak::Fade = 0;
            MRPG_StreakHUD.setVisible(0);
            return;
        }
        %active = 1;
    }

    MRPGStreak_Redraw();

    if(%active)
        $MRPG::Streak::LoopSch = schedule($MRPG::Streak::FrameMS, 0, MRPGStreak_Loop);
}

function clientCmdMRPGDamageStreak(%hits, %total, %lastHit, %isCrit)
{
    MRPGStreak_Build();

    // First hit of a new streak - start from scratch and play the intro.
    if($MRPG::Streak::Phase $= "out" || $MRPG::Streak::Phase $= "" || %hits <= 1)
    {
        MRPGStreak_Layout();

        $MRPG::Streak::Grow  = 0.35;
        $MRPG::Streak::Fade  = 0.0;
        $MRPG::Streak::Phase = "in";
    }
    else
    {
        // Mid-streak: a late hit while fading out should snap it back to full.
        $MRPG::Streak::Fade  = 1.0;
        $MRPG::Streak::Grow  = 1.0;
        $MRPG::Streak::Phase = "hold";
    }

    $MRPG::Streak::Hits  = %hits;
    $MRPG::Streak::Total = %total;
    $MRPG::Streak::Punch = %isCrit ? $MRPG::Streak::PunchCrit : $MRPG::Streak::PunchHit;

    MRPG_StreakHUD.setVisible(1);
    MRPGStreak_Loop();
}

function MRPGParry_Loop()
{
    cancel($MRPG::Parry::LoopSch);

    //Gate, and do NOT reschedule when it is shut - see ServerGate.cs rule 3.
    if(!MRPG_isActive())
        return;

    if(!isObject(MRPG_ParryPopup))
        return;

    $MRPG::Parry::T += $MRPG::Streak::FrameMS;
    %t = $MRPG::Parry::T;

    if(%t >= $MRPG::Parry::LifeMS)
    {
        MRPG_ParryPopup.setVisible(0);
        return;
    }

    %lift = 0;

    if(%t < $MRPG::Parry::PeakMS)
    {
        // Snap out from small, overshooting past full size.
        %k = %t / $MRPG::Parry::PeakMS;
        %scale  = 0.55 + (0.60 * %k);
        %bright = %k;
    }
    else if(%t < $MRPG::Parry::SetMS)
    {
        // Ease the overshoot back down - this is what reads as "weight".
        %k = (%t - $MRPG::Parry::PeakMS) / ($MRPG::Parry::SetMS - $MRPG::Parry::PeakMS);
        %scale  = 1.15 - (0.15 * %k);
        %bright = 1.0;
    }
    else if(%t < $MRPG::Parry::HoldMS)
    {
        %scale  = 1.0;
        %bright = 1.0;
    }
    else
    {
        %k = (%t - $MRPG::Parry::HoldMS) / ($MRPG::Parry::LifeMS - $MRPG::Parry::HoldMS);
        %scale  = 1.0 - (0.08 * %k);
        %bright = 1.0 - %k;
        %lift   = mFloor(%k * 18);
    }

    // White-hot on impact, cooling into the resting colour over the settle.
    %mix   = mClamp(%t / $MRPG::Parry::SetMS, 0, 1);
    %color = MRPGStreak_LerpColor($MRPG::Parry::Flash, $MRPG::Parry::Color, %mix);
    %color = MRPGStreak_DimColor(%color, %bright);

    %size = mFloor($MRPG::Parry::Size * %scale);

    MRPG_ParryPopup.setText(
        "<just:center><color:" @ %color @ "><font:" @ $Pref::Client::MonsterRPGx::StreakFont @ ":" @ %size @ ">" @
        "PERFECT PARRY"
    );

    %ext = MRPG_ParryPopup.getExtent();
    MRPG_ParryPopup.resize(
        MRPG_ParryPopup.baseX,
        MRPG_ParryPopup.baseY - %lift,
        getWord(%ext, 0),
        getWord(%ext, 1)
    );

    $MRPG::Parry::LoopSch = schedule($MRPG::Streak::FrameMS, 0, MRPGParry_Loop);
}

function clientCmdMRPGPerfectParry()
{
    MRPGStreak_Build();
    MRPGStreak_Layout();

    // Restart from zero so back-to-back parries re-flash instead of the second
    // one landing mid-fade and looking like a dropped input.
    $MRPG::Parry::T = 0;

    MRPG_ParryPopup.setVisible(1);
    MRPGParry_Loop();
}

// Hard reset - used on disconnect so a streak can't be left frozen on screen.
function MRPGStreak_Reset()
{
    cancel($MRPG::Streak::LoopSch);
    cancel($MRPG::Parry::LoopSch);

    $MRPG::Parry::T = $MRPG::Parry::LifeMS;

    if(isObject(MRPG_ParryPopup))
        MRPG_ParryPopup.setVisible(0);

    $MRPG::Streak::Hits  = 0;
    $MRPG::Streak::Total = 0;
    $MRPG::Streak::Punch = 1.0;
    $MRPG::Streak::Grow  = 1.0;
    $MRPG::Streak::Fade  = 0.0;
    $MRPG::Streak::Phase = "";

    if(isObject(MRPG_StreakHUD))
        MRPG_StreakHUD.setVisible(0);
}

function clientCmdMRPGDamageStreakEnd(%hits, %total)
{
    if(!isObject(MRPG_StreakHUD))
        return;

    $MRPG::Streak::Hits  = %hits;
    $MRPG::Streak::Total = %total;
    $MRPG::Streak::Phase = "out";

    MRPGStreak_Loop();
}


//////////////////////////////////////////////////////////////////////////////
// PERFECT SLASH  -  Core_SwingMomentum.cs fires this when the angle lands
//////////////////////////////////////////////////////////////////////////////
//
// The window is deliberately tight (~21 degrees, judged at the single frame the blade is
// moving fastest), so it has to announce itself or nobody would ever know they hit it.
// Reuses the parry popup's own build/layout/loop so it matches the rest of the combat
// feedback instead of introducing a second style.
function clientCmdMRPGPerfectSlash(%momentum, %addedKg)
{
	MRPGStreak_Build();
	MRPGStreak_Layout();

	if(isObject(MRPG_ParryPopup))
	{
		if(isObject(MRPG_ParryText))
			MRPG_ParryText.setText("<just:center><font:verdana bold:26><color:FFE9A0>PERFECT SLASH"
				@ "<font:verdana bold:14><color:8A8175>  " @ %momentum @ " kg\u00b7m/s   +"
				@ %addedKg @ "kg behind it");
		$MRPG::Parry::T = 0;
		MRPG_ParryPopup.setVisible(1);
		MRPGParry_Loop();
	}
}
