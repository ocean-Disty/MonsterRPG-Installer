//////////////////////////////////////////////////////////////////////////////
// FormFX.cs  -  screen effects + HUD icon for the racial transformations
//////////////////////////////////////////////////////////////////////////////
//
// The server (Server/Core/Core_FormFX.cs) owns the truth and sends TARGETS about 8 times a
// second: how far into the form you are, the aura colour, the density, and the seconds
// left. This file runs its own 30ms tick and LERPS toward whatever it last heard, so the
// wash, the vignette and the icon all move smoothly no matter how coarse the updates are -
// and a dropped packet just means one slightly longer glide, never a pop.
//
// PRIMORDIAL is a sun being lit: a warm full-screen wash that blows out to white on the
// burst frame (the engine's own setWhiteOut does that half, server-side) and then settles
// into a permanent glare.
//
// RENDERMEN is the opposite - no glare at all. The screen DARKENS from the edges inward
// using the vignette bitmap tinted to the aura's void colour, so it reads as something
// closing in rather than something shining.

$FFX::Gfx  = "Add-Ons/Client_MonsterRPG/GUIs/";
$FFX::Btn  = "Add-Ons/Client_MonsterRPG/GUIs/Button_Elements/";
$FFX::Vig  = "Add-Ons/Client_MonsterRPG/GUIs/Vignette1";

$FFX::TickMS   = 30;     // matches the server's lerp tick
$FFX::Lerp     = 0.18;   // how hard we chase the target each tick
$FFX::WashMax["primordial"] = 0.20;   // warm full-screen tint
$FFX::WashMax["rendermen"]  = 0.10;
$FFX::VigMax["primordial"]  = 0.22;   // primordial glows more than it darkens
$FFX::VigMax["rendermen"]   = 0.62;   // rendermen is mostly this
$FFX::IconSize = 64;
$FFX::IconY    = 92;

$FFX_Built = 0;
$FFX_On    = 0;
$FFX_Tick  = "";

// live (lerped) vs target
$FFX_Rise = 0;   $FFX_RiseT = 0;
$FFX_R = 1;      $FFX_G = 1;     $FFX_B = 1;
$FFX_RT = 1;     $FFX_GT = 1;    $FFX_BT = 1;
$FFX_Bloom = 0;                  // one-shot flare on the burst frame
$FFX_Ang = 0;                    // pulse phase
$FFX_Race = "";  $FFX_Phase = 0; $FFX_Density = 0; $FFX_Name = ""; $FFX_Secs = 0;


//////////////////////////////////
////////// BUILD /////////////////
//////////////////////////////////

function FFX_swatch(%parent, %name, %x, %y, %w, %h, %col)
{
	%s = new GuiSwatchCtrl(%name)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "1 1"; color = %col;
	};
	%parent.add(%s);
	return %s;
}
function FFX_bmp(%parent, %name, %x, %y, %w, %h, %bitmap, %col)
{
	%b = new GuiBitmapCtrl(%name)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "1 1";
		bitmap = %bitmap; wrap = "0"; mColor = %col; mMultiply = "0";
	};
	%parent.add(%b);
	return %b;
}

function FFX_build()
{
	if($FFX_Built && isObject(FFX_Wash))
		return;
	if(!isObject(PlayGui))
		return;

	// full-screen layers. Neither is a mouse control, so nothing here can steal input.
	%w = new GuiSwatchCtrl(FFX_Wash)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = "1024 768"; minExtent = "1 1"; color = "0 0 0 0";
	};
	PlayGui.add(%w);
	%v = new GuiBitmapCtrl(FFX_Vig)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = "1024 768"; minExtent = "1 1";
		bitmap = $FFX::Vig; wrap = "0"; mColor = "0 0 0 0"; mMultiply = "0";
	};
	PlayGui.add(%v);

	// the icon: a tinted gem in a gold ring, with a drain bar under it
	%s = $FFX::IconSize;
	%x = 512 - %s / 2;
	FFX_bmp(PlayGui, "FFX_Glow", %x - 10, $FFX::IconY - 10, %s + 20, %s + 20, $FFX::Btn @ "Button_round", "0 0 0 0");
	FFX_bmp(PlayGui, "FFX_Gem",  %x, $FFX::IconY, %s, %s, $FFX::Btn @ "Button_round", "0 0 0 0");
	FFX_bmp(PlayGui, "FFX_Ring", %x, $FFX::IconY, %s, %s, $FFX::Btn @ "Button_round_Fr", "0 0 0 0");

	FFX_swatch(PlayGui, "FFX_BarBg", %x - 12, $FFX::IconY + %s + 6, %s + 24, 5, "0 0 0 0");
	FFX_swatch(PlayGui, "FFX_Bar",   %x - 12, $FFX::IconY + %s + 6, %s + 24, 5, "0 0 0 0");

	%t = new GuiMLTextCtrl(FFX_Name)
	{
		profile = "GuiMLTextProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = (512 - 130) SPC ($FFX::IconY + %s + 14); extent = "260 18"; minExtent = "8 2";
		allowColorChars = "1"; maxChars = "-1"; selectable = "0"; autoResize = "0";
	};
	PlayGui.add(%t);

	$FFX_Built = 1;
	FFX_setVisible(0);
}
function FFX_setVisible(%on)
{
	%names = "FFX_Wash FFX_Vig FFX_Glow FFX_Gem FFX_Ring FFX_BarBg FFX_Bar FFX_Name";
	for(%i = 0; %i < getWordCount(%names); %i++)
	{
		%o = getWord(%names, %i);
		if(isObject(%o))
			%o.setVisible(%on);
	}
}


//////////////////////////////////
////////// THE 30ms TICK /////////
//////////////////////////////////

function FFX_i(%f)   // 0..1 float -> 0..255 for a GUI colour field
{
	%v = mFloor(%f * 255 + 0.5);
	if(%v < 0) %v = 0;
	if(%v > 255) %v = 255;
	return %v;
}
function FFX_lerp(%cur, %target)
{
	return %cur + (%target - %cur) * $FFX::Lerp;
}

function FFX_tick()
{
	cancel($FFX_Tick);
	//Gate, and do NOT reschedule when it is shut - see ServerGate.cs rule 3.
	if(!$FFX_Built || !MRPG_isActive())
		return;

	// chase every target
	$FFX_Rise = FFX_lerp($FFX_Rise, $FFX_RiseT);
	$FFX_R    = FFX_lerp($FFX_R, $FFX_RT);
	$FFX_G    = FFX_lerp($FFX_G, $FFX_GT);
	$FFX_B    = FFX_lerp($FFX_B, $FFX_BT);
	$FFX_Bloom = $FFX_Bloom * 0.88;          // the burst flare decays on its own
	$FFX_Ang  += 0.12;

	// finished fading out -> tear the overlay down and stop ticking
	if(!$FFX_On && $FFX_Rise < 0.01 && $FFX_Bloom < 0.01)
	{
		$FFX_Rise = 0;
		FFX_setVisible(0);
		return;
	}

	%rise  = $FFX_Rise;
	%glow  = %rise + $FFX_Bloom;
	if(%glow > 1.6) %glow = 1.6;

	// ---- full-screen wash: the colour of the aura, faint ----
	%washA = $FFX::WashMax[$FFX_Race] * %glow;
	if(isObject(FFX_Wash))
		FFX_Wash.color = FFX_i($FFX_R) SPC FFX_i($FFX_G) SPC FFX_i($FFX_B) SPC FFX_i(%washA);

	// ---- vignette: primordial glows warm at the edges, rendermen closes in dark ----
	if(isObject(FFX_Vig))
	{
		%vigA = $FFX::VigMax[$FFX_Race] * %rise;
		if($FFX_Race $= "rendermen")
			FFX_Vig.mColor = FFX_i($FFX_R * 0.5) SPC FFX_i($FFX_G * 0.5) SPC FFX_i($FFX_B * 0.6) SPC FFX_i(%vigA);
		else
			FFX_Vig.mColor = FFX_i($FFX_R) SPC FFX_i($FFX_G) SPC FFX_i($FFX_B) SPC FFX_i(%vigA);
	}

	// ---- the gem: breathing pulse + the burst bloom ----
	%pulse = 1 + 0.07 * mSin($FFX_Ang) + $FFX_Bloom * 1.4;
	%s = mFloor($FFX::IconSize * %pulse);
	%x = 512 - %s / 2;
	%y = $FFX::IconY + ($FFX::IconSize - %s) / 2;

	if(isObject(FFX_Gem))
	{
		FFX_Gem.resize(%x, %y, %s, %s);
		FFX_Gem.mColor = FFX_i($FFX_R) SPC FFX_i($FFX_G) SPC FFX_i($FFX_B) SPC FFX_i(%rise);
	}
	if(isObject(FFX_Glow))
	{
		%g = mFloor(%s * 1.45);
		FFX_Glow.resize(512 - %g / 2, $FFX::IconY + ($FFX::IconSize - %g) / 2, %g, %g);
		FFX_Glow.mColor = FFX_i($FFX_R) SPC FFX_i($FFX_G) SPC FFX_i($FFX_B) SPC FFX_i(%glow * 0.30);
	}
	if(isObject(FFX_Ring))
	{
		FFX_Ring.resize(%x, %y, %s, %s);
		FFX_Ring.mColor = "222 196 120 " @ FFX_i(%rise * 0.95);
	}

	// ---- drain bar: how much of the form is left ----
	if(isObject(FFX_Bar) && isObject(FFX_BarBg))
	{
		%bw = $FFX::IconSize + 24;
		%bx = 512 - %bw / 2;
		%by = $FFX::IconY + $FFX::IconSize + 6;
		%frac = ($FFX_Secs > 0) ? ($FFX_Secs / 30) : 0;
		if(%frac > 1) %frac = 1;
		if(%frac < 0) %frac = 0;
		FFX_BarBg.color = "0 0 0 " @ FFX_i(%rise * 0.55);
		FFX_BarBg.resize(%bx, %by, %bw, 5);
		FFX_Bar.color   = FFX_i($FFX_R) SPC FFX_i($FFX_G) SPC FFX_i($FFX_B) SPC FFX_i(%rise);
		FFX_Bar.resize(%bx, %by, mFloor(%bw * %frac) + 1, 5);
	}
	if(isObject(FFX_Name))
		FFX_Name.setText("<just:center><font:verdana bold:13><color:F1ECC2>" @ $FFX_Name
			@ "  <color:8A8175>" @ $FFX_Secs @ "s");

	$FFX_Tick = schedule($FFX::TickMS, 0, "FFX_tick");
}


//////////////////////////////////
////////// SERVER FEED ///////////
//////////////////////////////////

// race / phase / rise / "r g b" / density / name / secondsLeft
function clientCmdMRPG_FormFX(%data)
{
	FFX_build();
	if(!$FFX_Built)
		return;

	$FFX_Race    = getField(%data, 0);
	$FFX_Phase   = getField(%data, 1);
	$FFX_RiseT   = getField(%data, 2);
	%rgb         = getField(%data, 3);
	$FFX_Density = getField(%data, 4);
	$FFX_Name    = getField(%data, 5);
	$FFX_Secs    = getField(%data, 6);

	$FFX_RT = getWord(%rgb, 0);  $FFX_GT = getWord(%rgb, 1);  $FFX_BT = getWord(%rgb, 2);

	if(!$FFX_On)
	{
		$FFX_On = 1;
		FFX_setVisible(1);
		// start the colour AT the target so the first frame isn't a white flash
		$FFX_R = $FFX_RT;  $FFX_G = $FFX_GT;  $FFX_B = $FFX_BT;
	}
	if(!isEventPending($FFX_Tick))
		FFX_tick();
}

// the one loud frame
function clientCmdMRPG_FormBurst()
{
	$FFX_Bloom = 1;
	if($FFX_Built && !isEventPending($FFX_Tick))
		FFX_tick();
}

// let it fade rather than vanish - the tick tears down once it reaches zero
function clientCmdMRPG_FormFXEnd()
{
	$FFX_On    = 0;
	$FFX_RiseT = 0;
	$FFX_Secs  = 0;
	if($FFX_Built && !isEventPending($FFX_Tick))
		FFX_tick();
}


//////////////////////////////////////////////////////////////////////////////
// SLASH ANGLE METER  -  how close you are to centering the arc
//////////////////////////////////////////////////////////////////////////////
//
// Core_SwingMomentum.cs pushes your live alignment (-1..1) a few times per swing. Without
// this you had no way to learn the timing - the window is ~21 degrees judged during the
// fast part of the arc, which is not something anyone finds by accident.
//
// The bar is a slanted track: sweep your view along the blade's arc and the marker walks
// right, toward the gold PERFECT band at the end. Sweep against your own swing and it goes
// left into the red. It fades out shortly after the swing ends.

$SA::W = 260;
$SA::H = 12;
$SA::Y = 300;      // just under the crosshair, where the eye already is
$SA_Built = 0;
$SA_Fade  = "";

function SA_build()
{
	if($SA_Built && isObject(SA_Track))
		return;
	if(!isObject(PlayGui))
		return;
	%x = 512 - $SA::W / 2;

	FFX_swatch(PlayGui, "SA_Track", %x, $SA::Y, $SA::W, $SA::H, "0 0 0 0");
	FFX_swatch(PlayGui, "SA_Good",  %x, $SA::Y, 1, $SA::H, "0 0 0 0");
	FFX_swatch(PlayGui, "SA_Perf",  %x, $SA::Y, 1, $SA::H, "0 0 0 0");
	FFX_swatch(PlayGui, "SA_Mark",  %x, $SA::Y - 3, 5, $SA::H + 6, "0 0 0 0");
	%t = new GuiMLTextCtrl(SA_Label)
	{
		profile = "GuiMLTextProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = (512 - 130) SPC ($SA::Y - 20); extent = "260 18"; minExtent = "8 2";
		allowColorChars = "1"; maxChars = "-1"; selectable = "0"; autoResize = "0";
	};
	PlayGui.add(%t);
	$SA_Built = 1;
	SA_show(0);
}
function SA_show(%on)
{
	%n = "SA_Track SA_Good SA_Perf SA_Mark SA_Label";
	for(%i = 0; %i < getWordCount(%n); %i++)
		if(isObject(%o = getWord(%n, %i)))
			%o.setVisible(%on);
}

// align / perfectDot / goodDot, straight from the server
function clientCmdMRPGSlashAngle(%align, %perfect, %good, %straight)
{
	SA_build();
	if(!$SA_Built)
		return;
	SA_show(1);
	cancel($SA_Fade);
	$SA_Fade = schedule(700, 0, "SA_show", 0);

	%x = 512 - $SA::W / 2;

	// the two scoring bands, drawn from their thresholds so they can never drift out of
	// step with the server's actual numbers
	%gx = %x + mFloor((%good + 1) / 2 * $SA::W);
	%px = %x + mFloor((%perfect + 1) / 2 * $SA::W);
	SA_Track.color = "0 0 0 150";
	SA_Track.resize(%x, $SA::Y, $SA::W, $SA::H);
	SA_Good.color  = "120 150 110 190";
	SA_Good.resize(%gx, $SA::Y, %px - %gx, $SA::H);
	SA_Perf.color  = "222 196 120 235";
	SA_Perf.resize(%px, $SA::Y, %x + $SA::W - %px, $SA::H);

	// where you actually are
	%mx = %x + mFloor((%align + 1) / 2 * $SA::W) - 2;
	SA_Mark.color = (%align >= %perfect) ? "255 235 150 255"
	              : ((%align >= %good) ? "190 230 170 255" : "220 120 110 255");
	SA_Mark.resize(%mx, $SA::Y - 3, 5, $SA::H + 6);

	//control is half the score - a wild spin can hit the angle by luck, so say so
	if(%straight !$= "" && %straight < 0.55)
		%txt = "<color:DC7870>too wild - one smooth sweep";
	else if(%align >= %perfect)
		%txt = "<color:FFE9A0>PERFECT ANGLE";
	else if(%align >= %good)
		%txt = "<color:BEE6AE>clean - keep sweeping";
	else if(%align < 0)
		%txt = "<color:DC7870>wrong way";
	else
		%txt = "<color:8A8175>sweep along your swing";
	SA_Label.setText("<just:center><font:verdana bold:12>" @ %txt);
}
