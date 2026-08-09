//////////////////////////////////////////////////////////////////////////////
// CharacterScreen.cs  -  medieval character screen (view avatar + pick options)
//////////////////////////////////////////////////////////////////////////////
//
// A gilded, wood-board character sheet. LEFT: a framed portrait viewport holding a live
// 3D Blockhead on a wood backdrop - drag it to spin, wheel to zoom. RIGHT: horizontal
// < value > pickers (Race locked to Human; Covenant / Clothing / Skin Tone / Hair / Face)
// and an ATTRIBUTES panel with a spendable point pool.
//
// Everything the player picks is sent to the server on Confirm, stored on the profile, and
// re-applied to the real in-world player from then on (Server/Core/Core_Character.cs). The
// attribute allocation is also folded into the real Stat system there, so the Stats menu
// (skills/traits) shows it.
//
// Open it:  MRPG_openCharacter();   (also bound to the "N" key at the bottom).
//
// THE PORTRAIT (this is the part that used to fight us)
// It is a GuiObjectView, NOT the in-world player. Earlier builds orbited the real camera
// around the real player, which always centres the player on screen (so he sat behind the
// menu) and shows the live world (so there was no backdrop). A GuiObjectView is just a
// control: it lands exactly where we position it and renders over whatever bitmap we put
// behind it. The engine API (verified against Blockland.exe) is:
//     view.setObject(name, model, skin, lod)
//     view.mountObject(name, model, skin, parentName, nodeName, lod)
//     view.unMountObject(name, node)
//     view.hideNode / unHideNode(name, node)      view.setNodeColor(name, node, color)
//     view.setIflFrame(name, iflName, frame)      view.setSequence(name, thread, seq, time)
//     view.setOrbitDist(d)  view.setCameraRot(x,y,z)  view.setMouse(canZoom, canSpin)
// mountObject is why hair works here: HatMod mounts hats at $HeadSlot (= 5), which is the
// "Mount5" node of m.dts, so we mount the hair .dts on that same node. (The 0.1 "scale" in
// HatMod's datablock is a dead dynamic field - the engine ignores it - and the hair models
// really are head-sized, so they mount at native scale.)
//
// Buttons use the shared CS_Btn pattern (bitmap plate + hover frame + click command) with
// ONE named catcher hit-testing them, because per-button class callbacks don't fire in
// this build. The catcher covers the panel only, so the portrait keeps its own mouse.

$CS::Gfx  = "Add-Ons/Client_MonsterRPG/GUIs/";
$CS::Btn  = "Add-Ons/Client_MonsterRPG/GUIs/Button_Elements/";
$CS::Wood = $CS::Gfx @ "avatar_wood_bg";
$CS::Body = "base/data/shapes/player/m.dts";
$CS::UI::Accent = "C9A24E";

// ---- portrait framing dials (tweak these if the camera sits wrong) ----
$CS::ViewFOV   = 35;                      // vertical field of view
$CS::ViewDist  = 4.34;                    // orbit distance (bigger = further away)
$CS::ViewRot   = "0.25 0 4.25";           // camera pitch / roll / yaw, radians (facing)
$CS::ViewLight = "0.721277 0.57735 0.57735";
$CS::Lod       = 100;                     // body detail size (m.dts has one detail: 100)
$CS::HairLod   = 100;                     // hair detail size (hair .dts use "detail2";
                                          //   if hair never appears, try 2 then 0 here)
$CS::HairNode  = "Mount5";                // $HeadSlot == 5 -> the m.dts hat mount node

$CS_Built = 0;
$CS_Open = 0;
$CS_AttrPoolMax = 15;
$CS_HairOn = 0;   // is a hair model currently mounted on the portrait?

// button registry (one shared catcher hit-tests these) + hover/press/tick state
$CS_BtnN = 0;
$CS_Hover = -1;
$CS_Press = -1;
$CS_TickSch = "";
$CS_Locked = 0;   // character is created once per save; locks the pickers afterwards

// every accessory node m.dts can show - hidden up front so only our clean body remains
// (names verified against base/data/shapes/player/m.dts)
$CS::HideNodes = "plume triPlume septPlume Visor helmet pointyHelmet flareHelmet scoutHat bicorn copHat knitHat femChest skirtHip armor bucket cape pack quiver tank epaulets epauletsRankA epauletsRankB epauletsRankC epauletsRankD ShoulderPads LArmSlim LHook RArmSlim RHook LPeg RPeg SkirtTrimLeft SkirtTrimRight LSki RSki";
// the clean medieval body we DO show
$CS::ShowNodes = "HeadSkin chest LArm RArm LHand RHand pants LShoe RShoe";


//////////////////////////////////////////////////////////////////////////////
// LAYOUT  -  scaling the design box to the screen, and centring it
//////////////////////////////////////////////////////////////////////////////
//
// The whole screen is authored against a fixed 1024x768 design box and every control in
// it uses horizSizing "right" / vertSizing "bottom", which is Torque for "do not move,
// do not resize". That is the right way to author a dialog - the alternative is fighting
// per-control anchors for a layout this dense - but it means NOTHING adapts on its own.
// CS_layout is the one place that adapts it.
//
// THE SCALE IS UNIFORM AND IT MATTERS THAT IT IS. Fitting each axis separately would put
// the design at 1.87 x 1.41 on a 1920x1080 screen: the gold frames become rectangles of
// two different thicknesses, the corner studs become oblongs, and the portrait - which is
// a live 3D render in a GuiObjectView, not a bitmap - gets visibly stretched. One factor,
// letterboxed by the centring, keeps all of it honest.
$CS::BaseW = 1024;
$CS::BaseH = 768;
// Room to grow, but not without limit. Past ~2.5 the design stops gaining anything: the
// art is authored at 1x and simply gets soft, and a 2.5x panel already fills most of a 4K
// screen. Below 0.5 nothing would be readable anyway, and the floor stops a bad getRes()
// from collapsing the screen to nothing.
$CS::MinScale = 0.5;
$CS::MaxScale = 2.5;
$CS_Scale = 1;

// Font sizes are baked into markup strings ("<font:verdana bold:12>") and markup is NOT
// affected by resizing the control it sits in, so text has to be scaled separately or a
// 2.5x panel ends up with 12px type rattling around inside it. Every size in this file
// goes through here.
//
// Clamped at 4 because Torque renders nothing at all below a few pixels, and the failure
// mode is a silently blank label rather than small text.
function CS_fs(%size)
{
	%s = mFloor(%size * $CS_Scale);
	if(%s < 4)
		%s = 4;
	return %s;
}

// DESIGN GEOMETRY IS CAPTURED ONCE AND NEVER RE-READ FROM THE LIVE CONTROL.
//
// The temptation is to read position/extent at layout time and multiply. That compounds:
// a second pass would scale the first pass's output, so two resolution changes in a row
// would leave the screen at scale^2. Keeping the 1x design values in CS_oPos/CS_oExt
// means every pass derives from the same source and passes are idempotent.
function GuiControl::CS_capture(%this)
{
	if(!%this.CS_captured)
	{
		%this.CS_oPos = %this.position;
		%this.CS_oExt = %this.extent;
		%this.CS_captured = 1;
	}
	for(%i = %this.getCount() - 1; %i >= 0; %i--)
		%this.getObject(%i).CS_capture();
}

function GuiControl::CS_scaleTo(%this, %s)
{
	%w = mFloor(getWord(%this.CS_oExt, 0) * %s);
	%h = mFloor(getWord(%this.CS_oExt, 1) * %s);
	//resize() truncates, and a control that rounds to zero stops rendering AND stops
	//hit-testing - which on a button plate reads as the button having vanished.
	if(%w < 1) %w = 1;
	if(%h < 1) %h = 1;

	%this.resize(mFloor(getWord(%this.CS_oPos, 0) * %s),
	             mFloor(getWord(%this.CS_oPos, 1) * %s), %w, %h);

	for(%i = %this.getCount() - 1; %i >= 0; %i--)
		%this.getObject(%i).CS_scaleTo(%s);
}

// SET THE FACTOR BEFORE ANYTHING IS BUILT, NOT AFTER.
//
// CS_fs() is called during the build - every static caption bakes its size into a markup
// string at the moment the control is created - so if the factor were only settled by
// CS_layout at the END of the build, every one of those captions would already have been
// written at 1x while the boxes around them got scaled. MRPG_buildCharacter calls this
// first for exactly that reason.
//
// KNOWN LIMIT: a resolution change AFTER the screen has been built rescales all the
// geometry and re-renders the dynamic text (sliders, attributes, status line), but the
// static captions keep the font size they were built with - re-running them would mean
// storing every markup template, which is a lot of machinery for a case that only shows
// up if you change resolution mid-session with the screen already open once. The screen
// is correct at any resolution it is FIRST opened at, which is the case that matters.
function CS_computeScale()
{
	%res = getRes();
	%rw  = getWord(%res, 0);
	%rh  = getWord(%res, 1);
	if(%rw < 1 || %rh < 1)
		return 0;

	%s = getMin(%rw / $CS::BaseW, %rh / $CS::BaseH);
	if(%s < $CS::MinScale) %s = $CS::MinScale;
	if(%s > $CS::MaxScale) %s = $CS::MaxScale;
	$CS_Scale = %s;
	return 1;
}

function CS_layout()
{
	if(!isObject(CS_Frame) || !CS_computeScale())
		return;

	%res = getRes();
	%rw  = getWord(%res, 0);
	%rh  = getWord(%res, 1);
	%s   = $CS_Scale;

	//Children only - CS_Frame's own placement is the centring below, not a scale of its
	//design position, and running CS_scaleTo on the frame itself would overwrite it.
	CS_Frame.CS_capture();
	for(%i = CS_Frame.getCount() - 1; %i >= 0; %i--)
		CS_Frame.getObject(%i).CS_scaleTo(%s);

	%w = mFloor($CS::BaseW * %s);
	%h = mFloor($CS::BaseH * %s);
	%x = mFloor((%rw - %w) / 2);
	%y = mFloor((%rh - %h) / 2);
	CS_Frame.resize(%x, %y, %w, %h);

	//The furniture wraps around wherever the sheet just landed, so it has to be placed
	//AFTER the frame and from the frame's final rectangle.
	CS_placeBackdrop(%rw, %rh, %x, %y, %w, %h);

	//The sliders and the attribute rows draw themselves from hardcoded pixel sizes at
	//refresh time, so they have to be re-run against the new factor or the knob and fill
	//snap back to design size the moment anything touches them. Both are cheap and both
	//are safe before the data arrives - they no-op on missing controls.
	if($CS_SldN > 0)
		CS_sldRefresh();
	if($CS_Built)
		CS_renderAttr();
}

//////////////////////////////////////////////////////////////////////////////
// BACKDROP  -  what sits behind the sheet
//////////////////////////////////////////////////////////////////////////////
//
// This used to be one flat "8 10 14" swatch over the whole screen. It did its job (the
// half-loaded world must not show through - see MRPG_CharBg) but on anything wider than
// the design box that job was ALL it did: a big empty void with the sheet parked in the
// corner. Now the void is dressed, in layers, back to front:
//
//   MRPG_CharBg   flat opaque fill      - the actual cover, and the only load-bearing
//                                         layer. Everything below is decoration drawn on
//                                         top of it, so if any bitmap fails to load the
//                                         screen degrades to exactly what it was before
//                                         rather than to a see-through dialog.
//   CS_BgTex      dark leather          - genericBG_bpB.jpg, a 2058px near-black grain.
//                                         Big enough to stretch to any screen without
//                                         going soft, and dark enough not to compete
//                                         with the panels.
//   CS_BgWood     wood band             - the same board the portrait stands on, run
//                                         behind the whole sheet so the two panels read
//                                         as lying ON something.
//   gold rules    two hairlines         - the panel frames' own accent, top and bottom of
//                                         the board, tying the furniture to the sheet.
//   edge shading  stacked swatches      - a hand-built vignette. NOT Vignette1.png: that
//                                         is a white-centre multiply MASK for the 3D
//                                         view, and a GuiBitmapCtrl has no multiply
//                                         blend, so drawing it here would put a large
//                                         white blob behind the sheet.
//
// Everything here is positioned by CS_layout, because all of it is relative to where the
// scaled sheet actually lands.
$CS::BgTex = $CS::Gfx @ "genericBG_bpB";

// one edge-shading band; kept as its own helper so the four edges cannot drift apart
function CS_edgeBand(%parent, %name, %alpha)
{
	%s = new GuiSwatchCtrl(%name)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "0 0"; extent = "8 8"; minExtent = "1 1"; color = "6 5 4" SPC %alpha;
	};
	%parent.add(%s);
	return %s;
}

function CS_buildBackdrop(%dlg)
{
	%tex = new GuiBitmapCtrl(CS_BgTex)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = "1024 768"; minExtent = "8 2";
		bitmap = $CS::BgTex; wrap = "0";
	};
	%dlg.add(%tex);

	//WRAPPED, NOT STRETCHED. The board is 372x720 and the band is as wide as the screen -
	//stretching it 6x horizontally turns the grain into smears. Wrapping tiles it, and
	//because the plank pattern runs vertically the seams fall on plank edges where they
	//read as more planks.
	%wood = new GuiBitmapCtrl(CS_BgWood)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "0 0"; extent = "1024 768"; minExtent = "8 2";
		bitmap = $CS::Wood; wrap = "1";
	};
	%dlg.add(%wood);

	//Knock the board back so the sheet still reads as the brightest thing on screen.
	//Named in a second statement rather than chained off the call - TorqueScript does not
	//accept a method call on a function's return value.
	%dim = CS_swatch(%dlg, 0, 0, 8, 8, "10 7 4 130");
	%dim.setName("CS_BgWoodDim");

	%rt = CS_swatch(%dlg, 0, 0, 8, 2, "170 138 72 190");
	%rt.setName("CS_BgRuleT");
	%rb = CS_swatch(%dlg, 0, 0, 8, 2, "170 138 72 190");
	%rb.setName("CS_BgRuleB");

	//Three steps a side rather than one, so the falloff reads as a gradient instead of a
	//hard edge. Alphas climb outward.
	CS_edgeBand(%dlg, "CS_BgEdgeL1",  70);  CS_edgeBand(%dlg, "CS_BgEdgeL2", 120);  CS_edgeBand(%dlg, "CS_BgEdgeL3", 180);
	CS_edgeBand(%dlg, "CS_BgEdgeR1",  70);  CS_edgeBand(%dlg, "CS_BgEdgeR2", 120);  CS_edgeBand(%dlg, "CS_BgEdgeR3", 180);
	CS_edgeBand(%dlg, "CS_BgEdgeT1",  70);  CS_edgeBand(%dlg, "CS_BgEdgeT2", 130);
	CS_edgeBand(%dlg, "CS_BgEdgeB1",  70);  CS_edgeBand(%dlg, "CS_BgEdgeB2", 130);
}

// Place the backdrop around wherever the scaled sheet ended up. Called from CS_layout,
// which owns %fx/%fy/%fw/%fh - the frame's final screen rectangle.
function CS_placeBackdrop(%rw, %rh, %fx, %fy, %fw, %fh)
{
	if(!isObject(CS_BgWood))
		return;

	//The board runs the full width of the screen and a little past the sheet top and
	//bottom, so the sheet sits ON it with a margin rather than being flush with its edge.
	%pad = mFloor(28 * $CS_Scale);
	%wy  = %fy - %pad;
	%wh  = %fh + %pad * 2;
	if(%wy < 0){ %wh = %wh + %wy;  %wy = 0; }
	if(%wy + %wh > %rh) %wh = %rh - %wy;

	CS_BgWood.resize(0, %wy, %rw, %wh);
	CS_BgWoodDim.resize(0, %wy, %rw, %wh);
	CS_BgRuleT.resize(0, %wy, %rw, mFloor(2 * $CS_Scale) + 1);
	CS_BgRuleB.resize(0, %wy + %wh - mFloor(2 * $CS_Scale) - 1, %rw, mFloor(2 * $CS_Scale) + 1);

	//SIDE SHADING IS SIZED OFF THE GAP, NOT OFF THE SCREEN. On a 4:3 display the sheet
	//fills the width and there is no gap at all - a fixed-width band would then be drawn
	//straight over the panel. Each step takes a third of whatever room is actually there.
	%gapL = %fx;
	%gapR = %rw - (%fx + %fw);
	%stepL = mFloor(%gapL / 3);
	%stepR = mFloor(%gapR / 3);

	CS_BgEdgeL3.resize(0,             0, %stepL, %rh);
	CS_BgEdgeL2.resize(%stepL,        0, %stepL, %rh);
	CS_BgEdgeL1.resize(%stepL * 2,    0, %gapL - %stepL * 2, %rh);
	CS_BgEdgeR1.resize(%fx + %fw,     0, %gapR - %stepR * 2, %rh);
	CS_BgEdgeR2.resize(%rw - %stepR * 2, 0, %stepR, %rh);
	CS_BgEdgeR3.resize(%rw - %stepR,  0, %stepR, %rh);

	%gapT = %wy;
	%gapB = %rh - (%wy + %wh);
	CS_BgEdgeT2.resize(0, 0,                          %rw, mFloor(%gapT / 2));
	CS_BgEdgeT1.resize(0, mFloor(%gapT / 2),          %rw, %gapT - mFloor(%gapT / 2));
	CS_BgEdgeB1.resize(0, %wy + %wh,                  %rw, %gapB - mFloor(%gapB / 2));
	CS_BgEdgeB2.resize(0, %rh - mFloor(%gapB / 2),    %rw, mFloor(%gapB / 2));

	//A ZERO GAP IS NOT A ZERO BAND. minExtent is "1 1", so resizing a band to nothing
	//leaves a 1px sliver of near-opaque dark pinned to the screen edge - four thin lines
	//boxing the screen in on any display the sheet happens to fill exactly. Hide them
	//instead; a 4:3 display hits the horizontal case every time.
	%onH = (%gapL > 3 && %gapR > 3);
	CS_BgEdgeL1.setVisible(%onH);  CS_BgEdgeL2.setVisible(%onH);  CS_BgEdgeL3.setVisible(%onH);
	CS_BgEdgeR1.setVisible(%onH);  CS_BgEdgeR2.setVisible(%onH);  CS_BgEdgeR3.setVisible(%onH);
	CS_BgEdgeT1.setVisible(%gapT > 3);  CS_BgEdgeT2.setVisible(%gapT > 3);
	CS_BgEdgeB1.setVisible(%gapB > 3);  CS_BgEdgeB2.setVisible(%gapB > 3);
}

//////////////////////////////////
///////// BUILD HELPERS //////////
//////////////////////////////////

function CS_label(%parent, %name, %x, %y, %w, %h)
{
	%t = new GuiMLTextCtrl(%name)
	{
		profile = "GuiMLTextProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "8 2"; lineSpacing = "2";
		allowColorChars = "1"; maxChars = "-1"; selectable = "0"; autoResize = "0";
	};
	%parent.add(%t);
	return %t;
}
function CS_swatch(%parent, %x, %y, %w, %h, %col)
{
	%s = new GuiSwatchCtrl() { profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "1 1"; color = %col; };
	%parent.add(%s);
	return %s;
}
function CS_goldFrame(%parent, %name, %x, %y, %w, %h, %inner)
{
	CS_swatch(%parent, %x - 6, %y - 6, %w + 12, %h + 12, "30 22 12 255");
	CS_swatch(%parent, %x - 3, %y - 3, %w + 6,  %h + 6,  "170 138 72 255");
	CS_swatch(%parent, %x - 1, %y - 1, %w + 2,  %h + 2,  "214 184 108 255");
	%box = new GuiSwatchCtrl(%name) { profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "8 2"; color = %inner; };
	%parent.add(%box);
	return %box;
}
function CS_studs(%parent, %x, %y, %w, %h)
{
	CS_swatch(%parent, %x - 5, %y - 5, 14, 14, "222 196 120 255");
	CS_swatch(%parent, %x + %w - 9, %y - 5, 14, 14, "222 196 120 255");
	CS_swatch(%parent, %x - 5, %y + %h - 9, 14, 14, "222 196 120 255");
	CS_swatch(%parent, %x + %w - 9, %y + %h - 9, 14, 14, "222 196 120 255");
}

// A medieval button: art plate + hidden "_fr" hover frame + centered label. It has NO
// mouse control of its own - the single named catcher CS_Mouse hit-tests every plate and
// drives hover/press/click. (Per-button `class`-routed callbacks did NOT fire in this
// build; a named catcher that hit-tests, like the tree canvas, is the reliable pattern.)
function CS_btn(%parent, %x, %y, %w, %h, %base, %fr, %cmd, %label, %fontSize)
{
	%plate = new GuiBitmapCtrl() { profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = %x SPC %y; extent = %w SPC %h; minExtent = "4 4"; bitmap = %base; };
	%parent.add(%plate);
	%frm = 0;
	if(%fr !$= "")
	{
		%frm = new GuiBitmapCtrl() { profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
			position = "0 0"; extent = %w SPC %h; minExtent = "2 2"; bitmap = %fr; visible = "0"; };
		%plate.add(%frm);
	}
	%lbl = 0;  %lblY = 0;
	if(%label !$= "")
	{
		if(%fontSize $= "") %fontSize = 13;
		//%lblY and the label's extent stay in DESIGN pixels - CS_scaleTo multiplies them
		//later, and pre-scaling here would scale them twice. Only the markup size goes
		//through CS_fs, because markup is not geometry and nothing else will touch it.
		%lblY = (%h - %fontSize - 3) / 2;
		%lbl = CS_label(%plate, "", 0, %lblY, %w, %fontSize + 4);
		%lbl.setText("<just:center><font:verdana bold:" @ CS_fs(%fontSize) @ "><color:F6EFCB>" @ %label);
	}
	$CS_BtnPlate[$CS_BtnN] = %plate;  $CS_BtnFrame[$CS_BtnN] = %frm;
	$CS_BtnLbl[$CS_BtnN]   = %lbl;    $CS_BtnLblY[$CS_BtnN]   = %lblY;  $CS_BtnCmd[$CS_BtnN] = %cmd;
	$CS_BtnN++;
	return %plate;
}

// which registered button is under the cursor (-1 = none)
function CS_btnAt()
{
	%cur = Canvas.getCursorPos();
	%cx = getWord(%cur, 0);  %cy = getWord(%cur, 1);
	for(%i = 0; %i < $CS_BtnN; %i++)
	{
		%pl = $CS_BtnPlate[%i];
		if(!isObject(%pl) || !%pl.isVisible())
			continue;
		%p = %pl.getCanvasPosition();  %e = %pl.getExtent();
		%px = getWord(%p, 0);  %py = getWord(%p, 1);  %pw = getWord(%e, 0);  %ph = getWord(%e, 1);
		if(%cx >= %px && %cx < %px + %pw && %cy >= %py && %cy < %py + %ph)
			return %i;
	}
	return -1;
}
function CS_btnPress()   // press state (also grabs a slider if the cursor is on one)
{
	if(!$CS_Locked)
	{
		$CS_SldDrag = CS_sldAt();
		if($CS_SldDrag >= 0)
		{
			CS_sldTrack();   // click anywhere on the groove jumps the knob there
			return;
		}
	}
	$CS_Press = CS_btnAt();
	//$CS_BtnLblY is a DESIGN pixel, but the label control has been scaled by CS_layout -
	//writing the raw value back would jump the caption to the unscaled offset and leave it
	//there. Both this and the release below go through the same factor so press and
	//un-press land on the same two positions whatever the screen size.
	if($CS_Press >= 0 && isObject($CS_BtnLbl[$CS_Press]))
		$CS_BtnLbl[$CS_Press].position = "0 " @ mFloor(($CS_BtnLblY[$CS_Press] + 2) * $CS_Scale);
}
function CS_btnRelease()  // release -> fire only if still over the pressed button
{
	if($CS_SldDrag >= 0)
	{
		$CS_SldDrag = -1;
		return;
	}
	%p = $CS_Press;
	if(%p >= 0 && isObject($CS_BtnLbl[%p]))
		$CS_BtnLbl[%p].position = "0 " @ mFloor($CS_BtnLblY[%p] * $CS_Scale);
	%i = CS_btnAt();
	$CS_Press = -1;
	if(%i >= 0 && %i == %p)
		eval($CS_BtnCmd[%i]);
}
// two catchers, because they must not overlap the portrait viewport (it needs its own
// mouse for drag-spin): CS_Mouse covers the sheet, CS_MouseP covers the portrait's
// caption strip where the spin buttons live.
function CS_Mouse::onMouseDown(%this)  { CS_btnPress(); }
function CS_Mouse::onMouseUp(%this)    { CS_btnRelease(); }
function CS_MouseP::onMouseDown(%this) { CS_btnPress(); }
function CS_MouseP::onMouseUp(%this)   { CS_btnRelease(); }

//////////////////////////////////
///////// SLIDERS ////////////////
//////////////////////////////////
//
// Hand-rolled, for the same reason the buttons are: this build does not route GUI callbacks
// reliably. A slider is just a groove + fill + knob; the shared catcher starts the drag and
// CS_tick (40ms) follows the cursor. Values live as a 0..1 position in $CS_SldPos[key].

$CS_SldN = 0;
$CS_SldDrag = -1;

function CS_slider(%parent, %key, %title, %x, %y, %w)
{
	%i = $CS_SldN;
	%tl = CS_label(%parent, "", %x, %y + 3, 74, 16);
	%tl.setText("<font:verdana bold:" @ CS_fs(12) @ "><color:" @ $CS::UI::Accent @ ">" @ strUpr(%title));

	%tx = %x + 78;  %tw = %w - 78 - 86;
	$CS_SldGroove[%i] = CS_swatch(%parent, %tx, %y + 9, %tw, 6, "16 12 8 255");
	$CS_SldFill[%i]   = CS_swatch(%parent, %tx, %y + 9, 1, 6, "170 138 72 255");
	$CS_SldKnob[%i]   = CS_swatch(%parent, %tx - 5, %y + 2, 10, 20, "222 196 120 255");
	$CS_SldVal[%i]    = CS_label(%parent, "", %x + %w - 84, %y + 3, 84, 16);
	$CS_SldKey[%i]    = %key;
	$CS_SldN++;
	return %i;
}
// which slider groove is under the cursor (-1 = none); the grab band is padded
// vertically so you don't have to hit a 6px bar
function CS_sldAt()
{
	%cur = Canvas.getCursorPos();
	%cx = getWord(%cur, 0);  %cy = getWord(%cur, 1);
	for(%i = 0; %i < $CS_SldN; %i++)
	{
		%g = $CS_SldGroove[%i];
		if(!isObject(%g) || !%g.isVisible())
			continue;
		%p = %g.getCanvasPosition();  %e = %g.getExtent();
		%px = getWord(%p, 0);  %py = getWord(%p, 1);  %pw = getWord(%e, 0);
		if(%cx >= %px - 6 && %cx <= %px + %pw + 6 && %cy >= %py - 10 && %cy <= %py + 16)
			return %i;
	}
	return -1;
}
// map the cursor onto the dragged slider and push the new value everywhere
function CS_sldTrack()
{
	%i = $CS_SldDrag;
	if(%i < 0 || !isObject($CS_SldGroove[%i]))
		return;
	%cur = Canvas.getCursorPos();
	%g = $CS_SldGroove[%i];
	%p = %g.getCanvasPosition();  %e = %g.getExtent();
	%pw = getWord(%e, 0);
	if(%pw <= 0)
		return;
	%pos = (getWord(%cur, 0) - getWord(%p, 0)) / %pw;
	if(%pos < 0) %pos = 0;
	if(%pos > 1) %pos = 1;
	$CS_SldPos[$CS_SldKey[%i]] = %pos;
	CS_sldRefresh();
	CS_applyScale();

	// The age slider also changes the SKIN, and only when it crosses a band
	// boundary. That used to be one cheap setIflFrame, so it re-applied on every
	// pixel of travel; the skin is a whole SHAPE now, so CS_applySkinTex tracks the
	// last band and only reloads the body when the band actually changes. Calling
	// it per-pixel would reload the model ~70 times across one drag.
	if($CS_SldKey[%i] $= "Age")
		CS_applySkinTex();
}
function CS_round2(%v) { return mFloor(%v * 100 + 0.5) / 100; }
function CS_sldRefresh()
{
	for(%i = 0; %i < $CS_SldN; %i++)
	{
		%key = $CS_SldKey[%i];
		%g   = $CS_SldGroove[%i];
		if(!isObject(%g))
			continue;
		%gx = getWord(%g.position, 0);  %gy = getWord(%g.position, 1);
		%gw = getWord(%g.getExtent(), 0);
		%px = mFloor($CS_SldPos[%key] * %gw);

		//THE GROOVE IS ALREADY SCALED, THE CONSTANTS ARE NOT.
		//
		//%gx/%gy/%gw are read live off the groove, so they arrive in screen pixels at
		//whatever CS_layout scaled the screen to. The fill height and the knob's size and
		//offsets below are design pixels written by hand, so they have to be put through
		//the same factor or the knob renders at a fixed 10x20 on a 2.5x panel - a tiny
		//chip sliding along a groove three times its height. This is the one place in the
		//file that mixes the two coordinate spaces, which is why it is spelled out.
		%s = $CS_Scale;
		$CS_SldFill[%i].resize(%gx, %gy, %px + 1, mFloor(6 * %s));
		$CS_SldKnob[%i].resize(%gx + %px - mFloor(5 * %s), %gy - mFloor(7 * %s),
		                       mFloor(10 * %s), mFloor(20 * %s));

		// The Age slider carries the LIFE STAGE readout inline. It used to have a
		// bar of its own in the right-hand picker column; Eye Colour took that slot,
		// and the stage plus its height ceiling read perfectly well next to the age
		// they are derived from - arguably better than two panels apart.
		if(%key $= "Age")
			%txt = CS_age() SPC "yrs   <color:8A8175>" @ CS_ageBandName(CS_age())
				@ " - max " @ CS_ftIn(CS_ageMaxHeightIn());
		else if(%key $= "Z")
			%txt = CS_ftIn(CS_heightInches());
		else if(%key $= "X")
			%txt = CS_round2(CS_axisX());
		else
			%txt = CS_round2(CS_axisY());
		$CS_SldVal[%i].setText("<just:right><font:verdana bold:" @ CS_fs(13) @ "><color:F1ECC2>" @ %txt);
	}
	CS_showPhysique();    // mass follows the frame, so it moves with every slider
	CS_showLifeStage();   // and the stage/ceiling readout follows the age slider
}

// hover polled every tick (reliable, unlike onMouseMove) while the screen is open
function CS_tick()
{
	cancel($CS_TickSch);
	//Gate, and do NOT reschedule when it is shut - see ServerGate.cs rule 3.
	if(!$CS_Open || !MRPG_isActive())
		return;
	if($CS_SldDrag >= 0)
		CS_sldTrack();
	%i = CS_btnAt();
	if(%i != $CS_Hover)
	{
		if($CS_Hover >= 0 && isObject($CS_BtnFrame[$CS_Hover]))
			$CS_BtnFrame[$CS_Hover].setVisible(0);
		$CS_Hover = %i;
		if(%i >= 0 && isObject($CS_BtnFrame[%i]))
			$CS_BtnFrame[%i].setVisible(1);
	}
	$CS_TickSch = schedule(40, 0, "CS_tick");
}


//////////////////////////////////
//////// DATA: CLOTHING //////////
//////////////////////////////////

// each outfit = a medieval torso print (decal from Decal_Default) + top/bottom colours
function CS_defCloth(%i, %name, %decal, %top, %bot)
{
	$CS_ClothName[%i] = %name;  $CS_ClothDecal[%i] = %decal;
	$CS_ClothTop[%i] = %top;    $CS_ClothBot[%i] = %bot;
}
function CS_initClothing()
{
	CS_defCloth(0, "Peasant Tunic", "Medieval-Tunic", "0.46 0.33 0.20 1", "0.30 0.22 0.15 1");
	CS_defCloth(1, "Forest Rider",  "Medieval-Rider", "0.26 0.40 0.23 1", "0.20 0.28 0.16 1");
	CS_defCloth(2, "Crimson Lion",  "Medieval-Lion",  "0.58 0.16 0.16 1", "0.30 0.13 0.13 1");
	CS_defCloth(3, "Azure Eagle",   "Medieval-Eagle", "0.20 0.30 0.58 1", "0.15 0.18 0.36 1");
	CS_defCloth(4, "Royal Tunic",   "Medieval-Tunic", "0.42 0.20 0.52 1", "0.26 0.15 0.33 1");
	CS_defCloth(5, "Steel Rider",   "Medieval-Rider", "0.46 0.48 0.53 1", "0.28 0.30 0.34 1");
	CS_defCloth(6, "Golden Lion",   "Medieval-Lion",  "0.62 0.48 0.20 1", "0.35 0.28 0.14 1");
	CS_defCloth(7, "Night Eagle",   "Medieval-Eagle", "0.16 0.16 0.20 1", "0.10 0.10 0.13 1");
	CS_defCloth(8, "Wine Tunic",    "Medieval-Tunic", "0.46 0.13 0.24 1", "0.22 0.12 0.16 1");
	CS_defCloth(9, "Emerald Lion",  "Medieval-Lion",  "0.16 0.42 0.28 1", "0.12 0.26 0.18 1");
	$CS_ClothCount = 10;

	%list = "";
	for(%i = 0; %i < $CS_ClothCount; %i++)
		%list = (%i == 0) ? $CS_ClothName[%i] : (%list TAB $CS_ClothName[%i]);
	$CS_SelOpts["Clothing"] = %list;  $CS_SelIdx["Clothing"] = 0;
}


//////////////////////////////////
////////// DATA: RACES ///////////
//////////////////////////////////
//
// Two races, each with its own height range: the base Blockhead tops out at 4'4" (the stock
// model's height), Humans run 4'10"-6'8". Race also carries stat modifiers, applied ONCE to
// the real Stat system at creation - Core_Character.cs holds the authoritative copy of that
// table, this one only draws the RACIAL TRAITS line.
//
// A blockhead is 4'4" (52") at scale 1.0, so height scale is simply inches/52.
//
// WIDTH IS DERIVED, NOT PICKED FLAT. Real bodies are allometric: something 38% taller is
// only ~15% wider, so scaling x/y with z (isotropic) makes tall characters look like
// inflated barrels, and holding x/y constant makes them look like noodles. The natural
// width for a height is therefore
//        natural = bodyBase(race, bodyType) * (inches / 52) ^ 0.45
// and the X/Y sliders only trim +-12% AROUND that. That is the "smart lock": the sliders
// hold an OFFSET, so changing height or body type re-derives x/y and the character stays
// proportional no matter where the sliders sit. Absolute values can never go silly.
//
// bodyBase is the width a member of that race would have AT 4'4", so it is directly
// comparable to the stock blockhead's 1.0 - and every entry is under it, because the stock
// blockhead reads a little wide.
$CS::BaseInches = 52;
$CS::WidthExp   = 0.45;   // allometric exponent: 1.0 = barrel, 0.0 = noodle
// The X/Y trim band is NO LONGER FLAT - it widens with age, so it lives with the
// rest of the age model as $CS::Age::AdjMin/Max{Young,Old} and is read through
// CS_adjMin() / CS_adjMax(). Nothing should use a constant here.
$CS::Bodies     = "Lean" TAB "Normal" TAB "Big";

function CS_defRace(%i, %id, %name, %minIn, %maxIn, %defIn, %lean, %normal, %big, %mods)
{
	$CS_RaceId[%i]    = %id;     $CS_RaceName[%i]  = %name;
	$CS_RaceMinIn[%i] = %minIn;  $CS_RaceMaxIn[%i] = %maxIn;  $CS_RaceDefIn[%i] = %defIn;
	$CS_RaceBody[%i, 0] = %lean; $CS_RaceBody[%i, 1] = %normal;  $CS_RaceBody[%i, 2] = %big;
	$CS_RaceMods[%i]  = %mods;
}
function CS_initRaces()
{
	//          id           name         min max def   lean  norm  big   stat modifiers
	CS_defRace(0, "blockhead", "Blockhead", 42, 52, 48, 0.86, 0.94, 1.06, "VIT 3 WIS 2 DEX -2 CHA -3");
	CS_defRace(1, "human",     "Human",     58, 80, 69, 0.82, 0.90, 1.00, "STR 3 CHA 1 INT -2 WIS -2");
	$CS_RaceCount = 2;

	%list = "";
	for(%i = 0; %i < $CS_RaceCount; %i++)
		%list = (%i == 0) ? $CS_RaceName[%i] : (%list TAB $CS_RaceName[%i]);
	$CS_SelOpts["Race"] = %list;  $CS_SelIdx["Race"] = 1;   // default: Human

	$CS_SelOpts["Body"] = $CS::Bodies;  $CS_SelIdx["Body"] = 1;   // default: Normal
}
function CS_raceIdx()
{
	%r = $CS_SelIdx["Race"];
	if(%r < 0 || %r >= $CS_RaceCount)
		%r = 0;
	return %r;
}
function CS_bodyIdx()
{
	%b = $CS_SelIdx["Body"];
	if(%b < 0 || %b > 2)
		%b = 1;
	return %b;
}
// 64 -> 5'4"
function CS_ftIn(%in)
{
	%ft = mFloor(%in / 12);
	return %ft @ "'" @ (%in - %ft * 12) @ "\"";
}

// ---- the four slider values, each held as a 0..1 position ----
//
// AGE GATES HEIGHT. The top of the height slider is CS_ageMaxHeightIn(), not the
// race's flat maximum, so dragging Age down past 32 pulls the height range in
// under you. Because the slider holds a 0..1 POSITION rather than an absolute
// height, that re-derives automatically - a character sitting at the top of the
// range simply stays at the top of the new, shorter range instead of becoming
// illegal. That is the same "smart lock" the X/Y trim already used.
function CS_heightInches()
{
	%r = CS_raceIdx();
	%min = $CS_RaceMinIn[%r];  %max = CS_ageMaxHeightIn();
	if(%max < %min)
		%max = %min;
	return mFloor(%min + $CS_SldPos["Z"] * (%max - %min) + 0.5);
}
function CS_naturalWidth()
{
	return $CS_RaceBody[CS_raceIdx(), CS_bodyIdx()] * mPow(CS_heightInches() / $CS::BaseInches, $CS::WidthExp);
}
function CS_adj(%key)   // slider position -> the age's trim on the natural width
{
	return CS_adjMin() + $CS_SldPos[%key] * (CS_adjMax() - CS_adjMin());
}
function CS_axisX() { return CS_naturalWidth() * CS_adj("X"); }
function CS_axisY() { return CS_naturalWidth() * CS_adj("Y"); }
function CS_axisZ() { return CS_heightInches() / $CS::BaseInches; }

// "x y z" for setScale, on the player and on the portrait
function CS_scaleVec()
{
	return CS_axisX() SPC CS_axisY() SPC CS_axisZ();
}
// put the sliders back to this race's natural default (called on race/body change)
//
// The default height is clamped into the AGE's ceiling before it is turned into
// a slider position - an old blockhead's default of 48" can sit above what its
// age allows, and an unclamped position above 1.0 would put the knob off the end
// of the groove.
function CS_resetProportions()
{
	%r = CS_raceIdx();
	%min = $CS_RaceMinIn[%r];
	%max = CS_ageMaxHeightIn();
	%def = $CS_RaceDefIn[%r];
	if(%def > %max) %def = %max;
	if(%def < %min) %def = %min;
	$CS_SldPos["Z"] = (%max > %min) ? ((%def - %min) / (%max - %min)) : 0;
	$CS_SldPos["X"] = 0.5;
	$CS_SldPos["Y"] = 0.5;
}
// pretty-print the current race's stat modifiers under the Race picker
function CS_showRaceMods()
{
	if(!isObject(CS_RaceMods))
		return;
	%mods = $CS_RaceMods[CS_raceIdx()];
	%up = "";  %down = "";
	for(%i = 0; %i < getWordCount(%mods); %i += 2)
	{
		%abbr = getWord(%mods, %i);
		%val  = getWord(%mods, %i + 1);
		if(%val > 0)
			%up = %up @ (%up $= "" ? "" : "  ") @ "+" @ %val SPC %abbr;
		else if(%val < 0)
			%down = %down @ (%down $= "" ? "" : "  ") @ %val SPC %abbr;
	}
	// right-aligned, and it carries its own caption: this line shares the PROPORTIONS
	// header row now that the Skin Type picker owns the fourth picker slot
	CS_RaceMods.setText("<just:right><font:verdana bold:" @ CS_fs(12) @ "><color:" @ $CS::UI::Accent
		@ ">TRAITS  <color:9BE29B>" @ %up @ "  <color:E09A9A>" @ %down);
}


//////////////////////////////////
//////// PHYSIQUE READOUT ////////
//////////////////////////////////
//
// Mirrors Server/Core/Core_Physique.cs so the screen shows mass and punch energy live while
// you drag the sliders and spend points. Anchored on a real average adult male: 5'9", 80 kg,
// ~100 J punch.
//
// The split shown here is the point of the whole mechanic: the FRAME half is what your body
// size buys you, fixed the moment you confirm; the STATS half keeps growing forever from
// STR and VIT without the character ever getting bigger. Size is a head start, stats are
// the climb.
$CS::Phys::RefKg       = 80;
$CS::Phys::RefVolume   = 1.3865;
$CS::Phys::MassPerStr  = 0.6;
$CS::Phys::MassPerVit  = 0.6;
$CS::Phys::BaseSpeed   = 7.0;
$CS::Phys::SpeedPerStr = 0.27;
$CS::Phys::EffMassFrac = 0.05;
$CS::Phys::SpeedPerVit = 0.05;   // VIT nudges the hit a little, mirroring STR nudging weight
$CS::Phys::HpPerKg     = 0.006;  // +0.6% max HP per kg over the reference 80
$CS::Phys::HpPerVit    = 0.060;  // +6%   max HP per VIT point over base

// the attribute the character will actually END UP with: allocation + racial modifier
function CS_effStat(%abbr)
{
	//ONCE LOCKED, THE SERVER'S NUMBER IS THE WHOLE ANSWER - and the race mods must NOT be
	//re-added on top of it. What the server pushes is the stat as it actually stands
	//(MRPG_statOf, which already folds in any racial transformation), so adding the
	//creation-time modifier again would count it twice and inflate every physique readout.
	//Without this the panel would also compute mass and strike energy from the values you
	//picked at creation while displaying the current ones directly above - two numbers on
	//one screen disagreeing about the same stat.
	if($CS_Locked)
	{
		%sv = CS_serverStat(%abbr);
		if(%sv !$= "")
			return (%sv < 1) ? 1 : %sv;
		//fall through if the push has not arrived yet
	}

	%v = $CS_Attr[%abbr];
	%mods = $CS_RaceMods[CS_raceIdx()];
	for(%i = 0; %i < getWordCount(%mods); %i += 2)
		if(getWord(%mods, %i) $= %abbr)
			%v += getWord(%mods, %i + 1);
	return (%v < 1) ? 1 : %v;
}
// what the body you picked weighs - fixed the moment you confirm
function CS_frameKg()
{
	return $CS::Phys::RefKg * (CS_axisX() * CS_axisY() * CS_axisZ()) / $CS::Phys::RefVolume;
}
// what STR and VIT pack into that same body - this half never stops growing
function CS_statKg()
{
	return $CS::Phys::MassPerStr * (CS_effStat("STR") - 5)
	     + $CS::Phys::MassPerVit * (CS_effStat("VIT") - 5);
}
function CS_bodyKg()
{
	%kg = CS_frameKg() + CS_statKg();
	return (%kg < 20) ? 20 : %kg;
}
function CS_strikeJoules()
{
	%v = $CS::Phys::BaseSpeed + $CS::Phys::SpeedPerStr * (CS_effStat("STR") - 5)
	                          + $CS::Phys::SpeedPerVit * (CS_effStat("VIT") - 5);
	if(%v < 1) %v = 1;
	return 0.5 * ($CS::Phys::EffMassFrac * CS_bodyKg()) * %v * %v;
}
// max HP this physique is worth, as a % over base - full weight AND vitality, combined
function CS_hpPercent()
{
	%f = $CS::Phys::HpPerKg * (CS_bodyKg() - $CS::Phys::RefKg)
	   + $CS::Phys::HpPerVit * (CS_effStat("VIT") - 5);
	if(%f < 0) %f = 0;
	return mFloor(%f * 100 + 0.5);
}
function CS_showPhysique()
{
	if(!isObject(CS_Physique))
		return;
	CS_Physique.setText("<font:verdana bold:" @ CS_fs(12) @ "><color:" @ $CS::UI::Accent @ ">WEIGHT <color:F1ECC2>"
		@ CS_round2(CS_bodyKg()) @ " kg  <color:8A8175>= " @ CS_round2(CS_frameKg()) @ " body + "
		@ CS_round2(CS_statKg()) @ " stats    <color:" @ $CS::UI::Accent @ ">PUNCH <color:F1ECC2>"
		@ mFloor(CS_strikeJoules()) @ " J    <color:" @ $CS::UI::Accent @ ">HP <color:F1ECC2>+"
		@ CS_hpPercent() @ "%");
	if(isObject(CS_PhysNote))
		CS_PhysNote.setText("<font:verdana bold:" @ CS_fs(11) @ "><color:8A8175>Body size sets your starting weight only - STR and VIT keep adding mass inside that same body. Max HP comes from that full weight plus VIT together.");
}


//////////////////////////////////
////////// DATA: SKIN ////////////
//////////////////////////////////
//
// Natural skin tones only, defined by HEX. The hex is what gets saved (readable in the
// profile) - both sides convert it to Torque's "r g b a" float colour when applying.

function CS_defSkin(%i, %name, %hex)
{
	$CS_SkinName[%i] = %name;  $CS_SkinHex[%i] = %hex;
}
function CS_initSkin()
{
	CS_defSkin(0,  "Porcelain", "F7DDC6");
	CS_defSkin(1,  "Ivory",     "F1D2B4");
	CS_defSkin(2,  "Fair",      "EEC8A3");
	CS_defSkin(3,  "Sand",      "E7BC91");
	CS_defSkin(4,  "Honey",     "DDA26A");
	CS_defSkin(5,  "Golden",    "CE9A5D");
	CS_defSkin(6,  "Olive",     "B98A54");
	CS_defSkin(7,  "Tan",       "B07A47");
	CS_defSkin(8,  "Bronze",    "9C6644");
	CS_defSkin(9,  "Chestnut",  "8A5A3B");
	CS_defSkin(10, "Umber",     "6F4530");
	CS_defSkin(11, "Espresso",  "54331F");
	$CS_SkinCount = 12;

	%list = "";
	for(%i = 0; %i < $CS_SkinCount; %i++)
		%list = (%i == 0) ? $CS_SkinName[%i] : (%list TAB $CS_SkinName[%i]);
	$CS_SelOpts["Skin"] = %list;  $CS_SelIdx["Skin"] = 2;   // default: Fair
}
//////////////////////////////////
///////// AGE ////////////////////
//////////////////////////////////
//
// AGE REPLACED THE SKIN TYPE PICKER. It is one value, 18-87, and it drives three
// things at once:
//
//   1. the body TEXTURE - age selects a band, the band selects the skin;
//   2. the HEIGHT CEILING - you stop growing at 32 and shrink from there;
//   3. the WIDTH/DEPTH allowance - an older frame may be broader.
//
// It is a SLIDER, not a picker. Seventy values behind a pair of arrows is not a
// control, and the value is continuous on the profile even though the texture is
// banded - so a slider is also the honest representation.
//
// The texture is banded because IFL frames are not loaded lazily: every frame in
// skin.ifl is pushed into the shape's material list and loaded eagerly, so one
// 2048 texture per year per race would be about 2 GB resident. Five bands per
// race is ten frames. The Skin Tone multiply runs over the top and hides the
// steps, exactly as it did for the old surface types.
//
// KEEP IN STEP with $MRPG::Age::* in Server/Core/Core_Character.cs. That copy is
// authoritative: it re-clamps the age, re-derives the skin and rebuilds the
// scale, so a disagreement here shows up as the confirmed character not matching
// the preview.
$CS::Age::Min   = 18;
$CS::Age::Max   = 87;
// MUST match $MRPG::Age::Bands on the server and AGE_BANDS in
// Tools/make_race_skins.py. Dense through youth where the wear curve is flat,
// yearly across the 28-31 drop-off, widening after.
$CS::Age::Bands = "18 20 22 24 26 28 29 30 31 34 37 41 45 50 55 61 68 76 87";
$CS::Age::BandName = "Young" TAB "Adult" TAB "Middle-aged" TAB "Senior" TAB "Elder";

// Past this age the height ceiling drops, by DropPerDecade inches every ten
// years. Per-race because a blockhead tops out at 52" against a human's 74": a
// flat inch per decade would cost a blockhead nearly twice as much of its range,
// so 0.7 is the same PROPORTION of stature as the human's 1.0.
$CS::Age::Peak = 32;
$CS::Age::PeakMaxIn["blockhead"]     = 52;
$CS::Age::DropPerDecade["blockhead"] = 0.7;
$CS::Age::PeakMaxIn["human"]         = 74;   // 6'2"
$CS::Age::DropPerDecade["human"]     = 1.0;

// The width/depth trim band, widening with age. These replace the old flat
// $CS::AdjMin / $CS::AdjMax.
$CS::Age::AdjMinYoung = 0.88;
$CS::Age::AdjMinOld   = 0.92;
$CS::Age::AdjMaxYoung = 1.12;
$CS::Age::AdjMaxOld   = 1.22;

function CS_initAge()
{
	// default to the middle of the first band - a character most people would
	// call "a young adult", and it needs no height re-clamp on open
	if($CS_SldPos["Age"] $= "")
		$CS_SldPos["Age"] = (24 - $CS::Age::Min) / ($CS::Age::Max - $CS::Age::Min);
}

// the age the slider is sitting on
function CS_age()
{
	%p = $CS_SldPos["Age"];
	if(%p $= "")
		%p = 0;
	return mFloor($CS::Age::Min + %p * ($CS::Age::Max - $CS::Age::Min) + 0.5);
}
function CS_ageBandCount() { return getWordCount($CS::Age::Bands); }

// which band an age falls in (0-based)
function CS_ageBand(%age)
{
	%n = CS_ageBandCount();
	%b = 0;
	for(%i = 0; %i < %n; %i++)
		if(%age >= getWord($CS::Age::Bands, %i))
			%b = %i;
	return %b;
}
function CS_ageBandName(%age)
{
	return getField($CS::Age::BandName, CS_ageBand(%age));
}

// The skin root for the current gender + race + age. Gender-major over race-major
// over ascending age, matching $MRPGSkin::Order line for line - the frame is
// arithmetic, not a lookup, so this and SkinDeploy.cs must agree on the layout of
// that list, and both must agree with MRPG_skinFrameFor on the server.
//
// The server IGNORES this on Confirm and re-derives it from the clamped gender,
// race and age. It is sent because MRPG_applyLookToPlayer reads field 7 when
// dressing a player or a bot, and because the portrait should show the root the
// character will actually be recorded with.
function CS_skinTexRoot()
{
	%r = CS_raceIdx();
	%g = CS_isFemale() ? 1 : 0;
	%f = (%g * $CS_RaceCount + %r) * CS_ageBandCount() + CS_ageBand(CS_age());
	return getWord($MRPGSkin::Order, %f);
}

// 0 at the youngest, 1 at the oldest
function CS_ageT()
{
	%span = $CS::Age::Max - $CS::Age::Min;
	if(%span <= 0)
		return 0;
	return (CS_age() - $CS::Age::Min) / %span;
}
function CS_adjMin() { return $CS::Age::AdjMinYoung + ($CS::Age::AdjMinOld - $CS::Age::AdjMinYoung) * CS_ageT(); }
function CS_adjMax() { return $CS::Age::AdjMaxYoung + ($CS::Age::AdjMaxOld - $CS::Age::AdjMaxYoung) * CS_ageT(); }

// The tallest this race may be at this age. The height slider's top end.
function CS_ageMaxHeightIn()
{
	%race = $CS_RaceId[CS_raceIdx()];
	%peak = $CS::Age::PeakMaxIn[%race];
	if(%peak $= "")
		%peak = 74;
	%drop = $CS::Age::DropPerDecade[%race];
	if(%drop $= "")
		%drop = 1.0;

	%age = CS_age();
	%max = %peak;
	if(%age > $CS::Age::Peak)
		%max = %peak - %drop * (%age - $CS::Age::Peak) / 10;

	// keep at least an inch of slider range whatever the age
	%min = $CS_RaceMinIn[CS_raceIdx()];
	if(%max < %min + 1)
		%max = %min + 1;
	return mFloor(%max);
}

// The LIFE STAGE bar - the freed picker slot where Skin Type used to sit.
// Read-only, and it exists to answer the question the height slider raises the
// moment age starts capping it: "why can I not be taller?"
// Kept as a no-op guard rather than deleted: the stage now rides the Age slider's
// own value label (see CS_sldRefresh), but if the standalone bar is ever put back
// this keeps working without hunting down the call sites.
function CS_showLifeStage()
{
	if(!isObject(CS_LifeStage))
		return;
	%age = CS_age();
	%max = CS_ageMaxHeightIn();
	CS_LifeStage.setText("<just:center><font:verdana bold:" @ CS_fs(14) @ "><color:F1ECC2>"
		@ CS_ageBandName(%age) @ "   <color:8A8175>max " @ CS_ftIn(%max));
}

// "RRGGBB" -> "r g b 1" (0..1 floats), the form setNodeColor wants
function CS_hexToColor(%hex)
{
	%hex = strUpr(%hex);
	if(strLen(%hex) < 6)
		%hex = "EEC8A3";   // "Fair" - never hand getSubStr a short string
	%r = CS_hexByte(getSubStr(%hex, 0, 2));
	%g = CS_hexByte(getSubStr(%hex, 2, 2));
	%b = CS_hexByte(getSubStr(%hex, 4, 2));
	return (%r / 255) SPC (%g / 255) SPC (%b / 255) SPC "1";
}
// same thing as 0..255 ints, which is what a GuiSwatchCtrl's `color` field wants
function CS_hexToColor255(%hex)
{
	%hex = strUpr(%hex);
	if(strLen(%hex) < 6)
		%hex = "EEC8A3";
	return CS_hexByte(getSubStr(%hex, 0, 2)) SPC CS_hexByte(getSubStr(%hex, 2, 2))
	   SPC CS_hexByte(getSubStr(%hex, 4, 2)) SPC "255";
}
function CS_hexByte(%pair)
{
	return CS_hexDigit(getSubStr(%pair, 0, 1)) * 16 + CS_hexDigit(getSubStr(%pair, 1, 1));
}
function CS_hexDigit(%c)
{
	%i = strPos("0123456789ABCDEF", %c);
	return (%i < 0) ? 0 : %i;
}
// hex of the current pick (index kept in range so a bad save can't break it)
function CS_skinHex()
{
	%i = $CS_SelIdx["Skin"];
	if(%i < 0 || %i >= $CS_SkinCount)
		%i = 2;
	return $CS_SkinHex[%i];
}


//////////////////////////////////
////////// SELECTORS /////////////
//////////////////////////////////

function CS_defSel(%name, %opts)
{
	$CS_SelOpts[%name] = %opts;  $CS_SelIdx[%name] = 0;
}
// FACES / DECALS - read straight out of the .ifl files.
//
// Do NOT use $face[] / $numFace / $decal[] / $numDecal here. The base game only fills those
// inside AvatarGui_CreatePartMenuFACE, which runs from the Avatar menu's onWake behind an
// $AvatarHasLoaded guard - so unless the player happened to open the Avatar menu since
// launching the game they are EMPTY, and the Face picker had exactly one bogus entry. That
// was the "faces don't work" bug, not a missing add-on.
//
// base/data/shapes/player/face.ifl (and decal.ifl) are regenerated by buildIFLs() from
// every installed Add-Ons/Face_* and Add-Ons/Decal_* folder, and LINE N IS IFL FRAME N -
// which is exactly what setIflFrame wants and what setFaceName resolves by base name.
// (getIflFrame does not exist in this build.)
$CS::FaceIfl  = "base/data/shapes/player/face.ifl";
$CS::DecalIfl = "base/data/shapes/player/decal.ifl";
// Which face pack to offer, best first ("" would mean every installed face).
//
// Face_MonsterRPG holds the same faces with the body skin's head shading baked
// into their alpha, generated by Tools/make_skin_faces.py. It is how the skin
// reaches the FRONT of the head at all: that side of the head cube is the face
// material itself (m.dts HeadSkin, material slot 1), it is opaque, and there is
// no skin surface behind it - so without these the head's front renders as flat
// colour while the other five sides carry the skin.
//
// KEEP IN STEP with $MRPG::Look::FaceDirs in Server/Core/Core_Character.cs.
$CS::FaceDirs = "Add-Ons/Face_MonsterRPG/" TAB "Add-Ons/Face_Brickadia/";

// Fill $CS_FaceFrame[] / return the TAB list of names for the faces under %dirFilter.
// Stops at the first blank line, exactly like the game's own reader, so our index into the
// file stays in step with the engine's frame numbering.
function CS_scanFaceIfl(%dirFilter)
{
	$CS_FaceN = 0;
	%list = "";
	%f = new FileObject();
	%f.openForRead($CS::FaceIfl);          // check isEOF, like the game's own readers do
	if(%f.isEOF())
	{
		%f.delete();
		return "";
	}
	%frame = 0;
	while(!%f.isEOF())
	{
		%line = trim(%f.readLine());
		if(%line $= "")
			break;
		%name = fileBase(%line);

		// EVERY line gets a name->frame entry, not just the filtered ones. The eye
		// colour variants live in their own Face_MRPGEye_* packs, which are
		// deliberately NOT in $CS::FaceDirs (they must not appear as 424 extra
		// entries in the face picker) - but the portrait still has to be able to
		// resolve one to a frame. Stored as frame+1 so an absent key, which reads
		// as "" and compares equal to 0, is distinguishable from frame 0.
		$CS_FrameOf[%name] = %frame + 1;

		if(%dirFilter $= "" || strPos(%line, %dirFilter) == 0)
		{
			$CS_FaceFrame[$CS_FaceN] = %frame;
			%list = ($CS_FaceN == 0) ? %name : (%list TAB %name);
			$CS_FaceN++;
		}
		%frame++;
	}
	%f.close();
	%f.delete();
	return %list;
}
function CS_loadFaces()
{
	if(isFunction("buildIFLs"))
		buildIFLs();                       // self-guarded; keeps the .ifl in step with installed add-ons

	// skin-composited pack first, then the raw pack it was built from
	$CS_FaceDirUsed = "";
	for(%i = 0; %i < getFieldCount($CS::FaceDirs); %i++)
	{
		$CS_FaceDirUsed = getField($CS::FaceDirs, %i);
		%list = CS_scanFaceIfl($CS_FaceDirUsed);
		if($CS_FaceN > 0)
			break;
	}
	if($CS_FaceN <= 0)                     // neither installed -> use whatever is
		%list = CS_scanFaceIfl("");        //   installed rather than showing nothing
	if($CS_FaceN <= 0)                     // no face.ifl at all -> the stock face
	{
		$CS_FaceFrame[0] = 0;  $CS_FaceN = 1;  %list = "smiley";
	}

	// MASTER COPY for the gender filter. $CS_FaceFrame[] is the LIVE array indexed
	// by picker position, so the filter rebuilds it from this - see
	// CS_applyGenderLists. Without a pristine copy the first gender switch would
	// discard the other gender's faces for the rest of the session.
	$CS_FaceAll  = %list;
	$CS_FaceAllN = $CS_FaceN;
	for(%i = 0; %i < $CS_FaceN; %i++)
		$CS_FaceAllFrame[%i] = $CS_FaceFrame[%i];

	$CS_SelOpts["Face"] = %list;  $CS_SelIdx["Face"] = 0;

	CS_checkFacePacks();
}

//////////////////////////////////////////////////////////////////////////////
// ARE THE FACE PACKS EVEN INSTALLED HERE?
//////////////////////////////////////////////////////////////////////////////
//
// A face is networked as a NAME. setFaceName on the server sends "mrpgFace1Hap"; every
// client then resolves that against ITS OWN base/data/shapes/player/face.ifl, which
// buildIFLs() generates from the Add-Ons/Face_* folders THAT MACHINE has installed.
//
// Blockland does not transfer add-ons. So a player who joins without Face_MonsterRPG has
// no line for that name, the lookup fails, and the engine leaves the stock smiley on -
// silently. Nothing in the log, nothing on screen, and the character screen's own
// portrait still looks right because it is driven by the same missing-pack fallback list
// rather than by what the server sent.
//
// That is indistinguishable from "the face system is broken", so it gets said out loud.
// The eye-colour packs are checked separately: they are a much larger download and a
// player missing only those still gets a correct face, just always violet-eyed.
function CS_checkFacePacks()
{
	//A representative file rather than the folder - a folder left behind by an
	//uninstall would pass an isDirectory-style test and still resolve nothing.
	%haveBase = isFile("Add-Ons/Face_MonsterRPG/mrpgFace1Hap.png");
	%haveEyes = isFile("Add-Ons/Face_MRPGEye_Blue/mrpgFace1Hap_eblue.png");

	$CS_FacePacksOK = %haveBase;

	if(!%haveBase)
	{
		error("MRPG faces: Add-Ons/Face_MonsterRPG is NOT installed on this client."
			SPC "Faces are networked BY NAME and resolved locally, so every character"
			SPC "- yours and everyone else's - will render with the stock smiley."
			SPC "This is a client-side install problem, not a server one.");
		if(isObject(ServerConnection))
			MRPG_chatWarn("Your face will not show in game: the MonsterRPG face pack is missing from this client.");
	}
	else if(!%haveEyes)
	{
		warn("MRPG faces: base pack present but the eye-colour packs"
			SPC "(Add-Ons/Face_MRPGEye_*) are not - every character will render"
			SPC "violet-eyed regardless of the colour they picked.");
	}
	return %haveBase;
}

// One-line client-side notice in the MonsterRPG chat's Events tab, falling back to the
// console when the panel has not been built yet (this can fire before the HUD exists).
function MRPG_chatWarn(%msg)
{
	if($MRPGChat_Built && isFunction("MRPGChat_PushEvent"))
		MRPGChat_PushEvent(%msg);
	else
		error("MRPG: " @ %msg);
}
// IFL frame of the currently picked face
//////////////////////////////////
///////// EYE COLOUR /////////////
//////////////////////////////////
//
// The engine cannot tint an iris - there is no console method for it, and the face
// plate's RGB is composited over the head's node colour by its own alpha, so a
// coloured pixel renders at its literal value with no per-region input. So an eye
// colour IS a different face file: Tools/make_eye_colors.py recolours the iris of
// every face into Add-Ons/Face_MRPGEye_<Colour>/<face>_e<colour>.png, and choosing
// a colour just picks that file instead.
//
// MUST match $MRPG::Eye::Colors in Server/Core/Core_Character.cs, in this order -
// the packet carries the NAME, but a mismatch means the server clamps to the
// default and the preview disagrees with the spawned character.
$CS::Eye::Colors = "violet blue cyan green hazel amber brown red grey";

function CS_initEye()
{
	// Display-cased for the picker; the packet sends the lower-case form.
	CS_defSel("Eye", "Violet" TAB "Blue" TAB "Cyan" TAB "Green" TAB "Hazel"
	                 TAB "Amber" TAB "Brown" TAB "Red" TAB "Grey");
}

function CS_eyeColor()
{
	%i = $CS_SelIdx["Eye"];
	if(%i < 0 || %i >= getWordCount($CS::Eye::Colors))
		%i = 0;
	return getWord($CS::Eye::Colors, %i);
}

// The face name actually worn - base face, or its eye-colour variant. Mirrors
// MRPG_faceWithEye on the server; the fallback to the plain face when no variant
// frame exists mirrors its isFile check (the white-square, visor and sunglasses
// faces have no iris, so they have no variants).
function CS_faceWorn()
{
	%i = $CS_SelIdx["Face"];
	if(%i < 0 || %i >= $CS_FaceN)
		%i = 0;
	%base = getField($CS_SelOpts["Face"], %i);
	%eye  = CS_eyeColor();
	if(%eye $= "violet")
		return %base;
	%v = %base @ "_e" @ %eye;
	return ($CS_FrameOf[%v] > 0) ? %v : %base;
}

function CS_faceFrame()
{
	%f = $CS_FrameOf[CS_faceWorn()];
	if(%f > 0)
		return %f - 1;          // stored as frame+1

	%i = $CS_SelIdx["Face"];
	if(%i < 0 || %i >= $CS_FaceN)
		%i = 0;
	return $CS_FaceFrame[%i];
}

// decal.ifl, same rules: $CS_DecalName[frame] == that frame's base name
function CS_loadDecals()
{
	if(isFunction("buildIFLs"))
		buildIFLs();
	$CS_DecalN = 0;
	%f = new FileObject();
	%f.openForRead($CS::DecalIfl);
	if(%f.isEOF())
	{
		%f.delete();
		return;
	}
	while(!%f.isEOF())
	{
		%line = trim(%f.readLine());
		if(%line $= "")
			break;
		$CS_DecalName[$CS_DecalN] = fileBase(%line);
		$CS_DecalN++;
	}
	%f.close();
	%f.delete();
}
// frame index of a decal by name (-1 if that decal add-on isn't installed)
function CS_decalFrame(%name)
{
	for(%i = 0; %i < $CS_DecalN; %i++)
		if($CS_DecalName[%i] $= %name)
			return %i;
	return -1;
}
// Hair models come from the hatmod hair packs. The picker option is the hat NAME (what the
// server needs for mountHat - HatMod names a hat after its FOLDER); $CS_HairPath[i] is the
// .dts the portrait mounts. Index 0 is always "None".
function CS_loadHair()
{
	$CS_HairPath[0] = "";
	$CS_HairN = 1;
	%list = "None";
	%list = CS_addHairDir("Add-Ons/Hatmod_Hair/*/*.dts",  %list);   // hair .dts live one folder deep
	%list = CS_addHairDir("Add-Ons/Hatmod_Hair2/*/*.dts", %list);

	// MASTER LIST, kept whole. The picker shows a gender-filtered view of it and
	// gender can change at any time, so the unfiltered list and its parallel paths
	// have to survive - filtering in place would make the first gender switch
	// permanent and there would be no way back to the male styles.
	//
	// $CS_HairPath[] stays the LIVE array the portrait mounts from, so the filter
	// writes into it; this is the pristine copy it rebuilds from.
	$CS_HairAll  = %list;
	$CS_HairAllN = $CS_HairN;
	for(%i = 0; %i < $CS_HairN; %i++)
		$CS_HairPathAll[%i] = $CS_HairPath[%i];

	$CS_SelOpts["Hair"] = %list;  $CS_SelIdx["Hair"] = 0;
}

//////////////////////////////////
////////////  GENDER  ////////////
//////////////////////////////////
//
// MUST match $MRPG::Look::FemaleFaces / FemaleHair in Server/Core/Core_Character.cs.
// The server re-derives against its own copy and substitutes anything this gender
// may not wear, so a mismatch here does not let a bad choice through - it just
// means the picker offers something that gets quietly swapped on Confirm.
//
// Female is the LISTED set and male is everything else, the same way round as the
// server, so a newly installed face or hair pack defaults to male rather than
// appearing in both lists.
$CS::FemaleFaces =
	"mrpgbrickadiaface13 mrpgbrickadiaface16 mrpgbrickadiaface17" SPC
	"mrpgbrickadiaface20 mrpgbrickadiaface21 mrpgbrickadiaface23" SPC
	"mrpgbrickadiaface25 mrpgbrickadiaface29";

$CS::FemaleHair =
	"GirlsHair LongHair PonytailHair BobcutHair BunnHair" SPC
	"GrandmaHair MomHair OldWomanHair VelmaHair LayeredHair";

function CS_inWordList(%list, %name)
{
	if(%name $= "" || %name $= "None")
		return 0;
	for(%i = 0; %i < getWordCount(%list); %i++)
		if(getWord(%list, %i) $= %name)
			return 1;
	return 0;
}

function CS_faceIsFemales(%face, %female)
{
	%isFem = CS_inWordList($CS::FemaleFaces, %face);
	return %female ? %isFem : !%isFem;
}

function CS_hairIsFemales(%hair, %female)
{
	if(%hair $= "" || %hair $= "None")
		return 1;                       // bald suits anyone
	%isFem = CS_inWordList($CS::FemaleHair, %hair);
	return %female ? %isFem : !%isFem;
}

// Rebuild the Face and Hair pickers for the current gender.
//
// THE PARALLEL ARRAYS ARE THE WHOLE DIFFICULTY. A face's IFL frame lives in
// $CS_FaceFrame[i] and a hair's .dts in $CS_HairPath[i], both indexed by the
// PICKER position - so dropping entries from the visible list without rebuilding
// those two would leave every remaining option pointing at another one's asset.
// Both are re-emitted here from the masters, in step.
//
// Falls back to the unfiltered list if a gender ends up with nothing: the female
// face names are specific to Face_MonsterRPG, and if that pack is missing the
// strict rule would leave women with an empty face picker.
function CS_applyGenderLists()
{
	%female = CS_isFemale();

	// ---- faces
	%keepFace = getField($CS_SelOpts["Face"], $CS_SelIdx["Face"]);
	%list = "";  %n = 0;
	for(%i = 0; %i < $CS_FaceAllN; %i++)
	{
		%name = getField($CS_FaceAll, %i);
		if(!CS_faceIsFemales(%name, %female))
			continue;
		$CS_FaceFrame[%n] = $CS_FaceAllFrame[%i];
		%list = (%n == 0) ? %name : %list TAB %name;
		%n++;
	}
	if(%n <= 0)
	{
		// nothing for this gender - show everything rather than an empty picker
		for(%i = 0; %i < $CS_FaceAllN; %i++)
			$CS_FaceFrame[%i] = $CS_FaceAllFrame[%i];
		%list = $CS_FaceAll;
		%n = $CS_FaceAllN;
	}
	$CS_FaceN = %n;
	$CS_SelOpts["Face"] = %list;
	%at = CS_fieldIndexOf(%list, %keepFace);
	$CS_SelIdx["Face"] = (%at >= 0) ? %at : 0;

	// ---- hair. Index 0 is always "None", so the scan starts at 1.
	%keepHair = getField($CS_SelOpts["Hair"], $CS_SelIdx["Hair"]);
	%hlist = "None";  %hn = 1;
	$CS_HairPath[0] = "";
	for(%i = 1; %i < $CS_HairAllN; %i++)
	{
		%name = getField($CS_HairAll, %i);
		if(!CS_hairIsFemales(%name, %female))
			continue;
		// Reads the pristine copy, writes the live array the portrait mounts from.
		// Safe in one pass because %hn <= %i always - the filtered list can only be
		// shorter, so this never overwrites an entry it has yet to read.
		$CS_HairPath[%hn] = $CS_HairPathAll[%i];
		%hlist = %hlist TAB %name;
		%hn++;
	}
	$CS_HairN = %hn;
	$CS_SelOpts["Hair"] = %hlist;
	%at = CS_fieldIndexOf(%hlist, %keepHair);
	$CS_SelIdx["Hair"] = (%at >= 0) ? %at : 0;
}

// Index of a field value in a TAB list, or -1. Used to hold a selection across a
// gender switch when the same option exists in both.
function CS_fieldIndexOf(%list, %want)
{
	if(%want $= "")
		return -1;
	for(%i = 0; %i < getFieldCount(%list); %i++)
		if(getField(%list, %i) $= %want)
			return %i;
	return -1;
}
// Last index of %search in %str, or -1.
//
// NOT strLastPos. That one is defined in Add-Ons/Server_HatMod/server.cs, which a
// CLIENT never executes, so here it was ALWAYS missing. An undefined call returns
// empty, which is 0 as a number, so the cut point below was always 1 and every
// hair got listed as the whole path minus its first character:
//     dd-Ons/Hatmod_Hair/ThinningHair
// That mangled name went into the look packet, got saved to the profile, and the
// server's isHat() rejected it - which is why no hat ever mounted.
function CS_lastPosOf(%str, %search)
{
	%last = -1;
	%i = strPos(%str, %search);
	while(%i >= 0)
	{
		%last = %i;
		%i = strPos(%str, %search, %i + 1);
	}
	return %last;
}

function CS_addHairDir(%mask, %list)
{
	for(%f = findFirstFile(%mask); %f !$= ""; %f = findNextFile(%mask))
	{
		// HatMod names the hat after the containing FOLDER, so derive the name the same way
		%dir  = strReplace(filePath(%f), "\\", "/");
		%cut  = CS_lastPosOf(%dir, "/") + 1;
		%name = (%cut > 0) ? getSubStr(%dir, %cut, strLen(%dir) - %cut) : %dir;
		if(%name $= "")
			%name = fileBase(%f);
		$CS_HairPath[$CS_HairN] = %f;
		$CS_HairN++;
		%list = %list TAB %name;
	}
	return %list;
}
function CS_initSelectors()
{
	CS_initRaces();
	// AGE BEFORE PROPORTIONS. CS_resetProportions asks CS_ageMaxHeightIn for the
	// top of the height range, and that reads the age slider - so the age has to
	// have a value first or the default height is derived against age 18's
	// ceiling regardless of where the slider actually ends up.
	CS_initAge();
	CS_resetProportions();
	CS_defSel("Covenant", "None" TAB "Adventure" TAB "Kingdom" TAB "Monster");            // the real covenants
	CS_initClothing();
	CS_initSkin();
	// GENDER BEFORE HAIR AND FACES so the masters exist before anything reads a
	// filtered list. The filter itself runs at the end, once both masters are built.
	CS_defSel("Gender", "Male" TAB "Female");
	CS_loadHair();
	CS_loadDecals();
	CS_loadFaces();
	// AFTER CS_loadFaces - CS_faceWorn reads $CS_FrameOf, which that fills in.
	CS_initEye();
	// AFTER both masters: narrows Face and Hair to the starting gender.
	CS_applyGenderLists();
}

// CATEGORY  /  < left | value | right >
function CS_makeSelector(%p, %name, %cat, %x, %y, %w, %locked)
{
	%cl = CS_label(%p, "", %x + 2, %y, %w, 14);
	%cl.setText("<font:verdana bold:" @ CS_fs(12) @ "><color:" @ $CS::UI::Accent @ ">" @ strUpr(%cat));

	%by = %y + 17;
	if(!%locked)
	{
		CS_btn(%p, %x, %by, 36, 32, $CS::Btn @ "Arrow_button_left",  $CS::Btn @ "Arrow_button_left_fr",  "CS_cycle(\"" @ %name @ "\", -1);", "", "");
		CS_btn(%p, %x + %w - 36, %by, 36, 32, $CS::Btn @ "Arrow_button_right", $CS::Btn @ "Arrow_button_right_fr", "CS_cycle(\"" @ %name @ "\", 1);", "", "");
	}
	%vb = CS_goldFrame(%p, "CS_Bar_" @ %name, %x + 44, %by + 1, %w - 88, 30, "22 17 12 235");
	CS_label(%vb, "CS_Val_" @ %name, 0, 6, %w - 88, 16);
	// the Skin bar carries a live colour chip so the hex is not just a number
	if(%name $= "Skin")
		$CS_SkinChip = CS_swatch(%vb, 8, 6, 18, 18, "255 255 255 255");
	CS_setSelLabel(%name);
}
function CS_setSelLabel(%name)
{
	%lbl = "CS_Val_" @ %name;
	if(!isObject(%lbl))
		return;
	if(%name $= "Face")
		%txt = "Face " @ ($CS_SelIdx["Face"] + 1);
	else if(%name $= "Skin")
	{
		%txt = getField($CS_SelOpts[%name], $CS_SelIdx[%name]) @ "   <color:8A8175>#" @ CS_skinHex();
		// GuiSwatchCtrl has no setColor console method - write the field (that IS mColor)
		if(isObject($CS_SkinChip))
			$CS_SkinChip.color = CS_hexToColor255(CS_skinHex());
	}
	else
		%txt = getField($CS_SelOpts[%name], $CS_SelIdx[%name]);
	%lbl.setText("<just:center><font:verdana bold:" @ CS_fs(14) @ "><color:F1ECC2>" @ %txt);
}
function CS_cycle(%name, %dir)
{
	if($CS_Locked)
		return;
	%n = getFieldCount($CS_SelOpts[%name]);
	if(%n <= 0)
		return;
	$CS_SelIdx[%name] = ($CS_SelIdx[%name] + %dir + %n) % %n;
	CS_setSelLabel(%name);
	if(%name $= "Race")
	{
		CS_resetProportions();   // new height range + natural width band
		CS_showRaceMods();
		CS_showPhysique();
		CS_sldRefresh();
		CS_applyScale();
		CS_applySkinTex();       // each race has its own set of age skins
	}
	else if(%name $= "Body")
	{
		CS_sldRefresh();         // the X/Y sliders hold an offset, so only the natural width moved
		CS_applyScale();
	}
	else if(%name $= "Gender")
	{
		// Re-narrow Face and Hair, then repaint everything gender touches: the torso
		// set (femChest + slim arms), the face, and the notional skin frame.
		//
		// The labels have to be re-set explicitly because the FILTER may have moved
		// those two selections - holding the same option where it exists in both
		// lists, falling back to index 0 where it does not.
		CS_applyGenderLists();
		CS_setSelLabel("Face");
		CS_setSelLabel("Hair");
		CS_applyLook();          // torso set + face + skin frame
		CS_applyHair();          // the hair list changed under the selection
		CS_applySkinTex();
	}
	else if(%name $= "Hair")
		CS_applyHair();
	// Eye belongs here with Face: it only changes which face frame is shown, and
	// CS_applyLook is what pushes that frame onto the portrait.
	else if(%name $= "Clothing" || %name $= "Face" || %name $= "Skin"
	        || %name $= "Eye")
		CS_applyLook();
}


//////////////////////////////////
//////// ATTRIBUTES //////////////
//////////////////////////////////

$CS_AttrList = "STR DEX VIT INT WIS CHA";
function CS_initAttr()
{
	for(%i = 0; %i < getWordCount($CS_AttrList); %i++)
		$CS_Attr[getWord($CS_AttrList, %i)] = 5;
	$CS_AttrSpent = 0;
}
//////////////////////////////////////////////////////////////////////////////
// TWO ATTRIBUTE SYSTEMS SHARE THESE BUTTONS
//
// Before the character exists, points come from a LOCAL creation pool: the picks are
// provisional, freely taken back, and committed in one batch by MRPG_CharSet. Nothing has
// been spent until Confirm.
//
// Afterwards, points come from the profile's SkillPoints and are spent ONE AT A TIME on
// the server (ServerCmdMRPG_AllocStat, Core_Stats.cs). Those are immediate and permanent.
//
// This used to `return` outright when locked, so once your character existed the +/-
// buttons were dead - which is why levelling up had nowhere to spend anything. Same
// buttons, different system underneath, chosen here.
//
// THE CLIENT DOES NOT PREDICT THE RESULT. It asks, and redraws from whatever the server
// pushes back (MRPG_pushStats -> clientCmdMRPG_StatMeta/StatRow/StatDone). Incrementing
// locally first would show a point spent even when the server refused - and the server is
// the only side that knows the real SkillPoints balance.
//////////////////////////////////////////////////////////////////////////////
function CS_attrAdj(%abbr, %dir)
{
	if($CS_Locked)
	{
		//no take-backs on a server spend - the minus button is hidden in this mode
		if(%dir < 0)
			return;
		if($MRPG_StatPoints < 1)
		{
			if(isObject(CS_Status))
				CS_Status.setText("<just:center><font:verdana bold:" @ CS_fs(12) @ "><color:8A8175>No attribute points to spend. Level up to earn more.");
			return;
		}
		commandToServer('MRPG_AllocStat', %abbr);
		return;
	}

	if(%dir > 0)
	{
		if($CS_AttrSpent >= $CS_AttrPoolMax || $CS_Attr[%abbr] >= 20)
			return;
		$CS_Attr[%abbr]++;  $CS_AttrSpent++;
	}
	else
	{
		if($CS_Attr[%abbr] <= 5)
			return;
		$CS_Attr[%abbr]--;  $CS_AttrSpent--;
	}
	CS_renderAttr();
}

// The live value of one attribute as the SERVER sees it, from the stat push buffer that
// RPGPanels.cs fills. "" when the buffer has not arrived yet, so callers can fall back.
function CS_serverStat(%abbr)
{
	for(%i = 0; %i < $MRPG_StatBufCount; %i++)
		if($MRPG_StatBuf[%i, "abbr"] $= %abbr)
			return $MRPG_StatBuf[%i, "value"];
	return "";
}
function CS_renderAttr()
{
	//LOCKED READS THE SERVER, NOT THE CREATION POOL. $CS_Attr[] is a snapshot of what was
	//chosen at creation and never moves again, so showing it after the fact would freeze
	//the display at your starting numbers - a spent point would appear to do nothing.
	for(%i = 0; %i < getWordCount($CS_AttrList); %i++)
	{
		%a = getWord($CS_AttrList, %i);
		%v = "CS_AV_" @ %a;
		if(!isObject(%v))
			continue;

		%show = $CS_Attr[%a];
		if($CS_Locked)
		{
			%sv = CS_serverStat(%a);
			if(%sv !$= "")            // "" = the push has not landed yet; keep the old text
				%show = %sv;
		}
		%v.setText("<just:center><font:verdana bold:" @ CS_fs(15) @ "><color:F1ECC2>" @ %show);
	}
	CS_showPhysique();   // STR/VIT feed body mass and strike energy

	//Same split for the counter: the creation pool before, real SkillPoints after.
	%left = $CS_Locked ? $MRPG_StatPoints : ($CS_AttrPoolMax - $CS_AttrSpent);
	if(isObject(CS_AttrPoints))
		CS_AttrPoints.setText("<just:right><font:verdana bold:" @ CS_fs(12) @ "><color:" @ $CS::UI::Accent @ ">Points: <color:F1ECC2>" @ %left);

	//Hide the take-backs once spending is live and permanent.
	for(%i = 0; %i < getWordCount($CS_AttrList); %i++)
	{
		%mb = $CS_AttrMinus[getWord($CS_AttrList, %i)];
		if(isObject(%mb))
			%mb.setVisible(!$CS_Locked);
	}

	%ready = (%left <= 0 && !$CS_Locked);

	// Confirm STAYS VISIBLE whether or not it is usable. It used to be hidden until
	// every point was spent, which is the same thing as broken from the player's side:
	// a button that is not there cannot explain itself, and CS_btnAt() skips invisible
	// plates so there was nothing to click and nothing to read. Now it is always
	// clickable and MRPG_charConfirm answers with the actual reason.
	if(isObject($CS_ConfirmPlate))
		$CS_ConfirmPlate.setVisible(1);
	if(isObject(CS_Status))
	{
		if($CS_Locked && %left > 0)
			CS_Status.setText("<just:center><font:verdana bold:" @ CS_fs(12) @ "><color:FFE9A0>You have " @ %left @ " attribute point" @ (%left == 1 ? "" : "s") @ " to spend. <color:8A8175>Spending is permanent.");
		else if($CS_Locked)
			CS_Status.setText("<just:center><font:verdana bold:" @ CS_fs(12) @ "><color:9BE29B>Character finalized. <color:8A8175>Earn more attribute points by levelling up.");
		else if(%ready)
			CS_Status.setText("<just:center><font:verdana bold:" @ CS_fs(12) @ "><color:FFE9A0>All points spent - press Confirm to finalize (this is permanent).");
		else
			CS_Status.setText("<just:center><font:verdana bold:" @ CS_fs(12) @ "><color:8A8175>Spend all " @ $CS_AttrPoolMax @ " attribute points to continue (" @ %left @ " left).");
	}
}


//////////////////////////////////
///////// THE PORTRAIT ///////////
//////////////////////////////////
// A GuiObjectView showing a clean Blockhead wearing the current picks. Everything here is
// client-side; nothing touches the real player until Confirm.

// (re)load the body into the view. Safe to call every time the screen opens.
// %keepYaw != "" holds the current spin across a reload.
function CS_setupAvatar(%keepYaw)
{
	if(!isObject(CS_View))
		return;
	CS_View.setEmpty();
	$CS_HairOn = 0;

	// The 3rd argument of setObject(name, model, skin, lod) is a reSkin root, and
	// reSkin is unusable on this shape - it reloads every material from the shape's
	// own folder and destroys the face and decal IFLs. Pass "".
	//
	// ONE SHAPE, deliberately. Per-band shape files were tried and reverted: the
	// player's shapeFile is read by sixteen add-ons to decide "is this a
	// blockhead?", eleven of them comparing the full path, and stock's own
	// applyBodyParts bails before hideAllNodes for anything else - which showed up
	// as every hat and body part rendering at once. The portrait must show what
	// everyone else will see, so it stays on the same single shape the server uses.
	CS_View.setObject("CS_Body", $CS::Body, "", $CS::Lod);

	// stand still, head level - "root" is the idle pose, "headup" cancels the look-down
	CS_View.setSequence("CS_Body", 0, "headup", 0);
	CS_View.setThreadPos("CS_Body", 0, 0);
	CS_View.setSequence("CS_Body", 1, "root", 0.85);
	$CS_Yaw = (%keepYaw $= "") ? getWord($CS::ViewRot, 2) : %keepYaw;
	CS_View.setCameraRot(getWord($CS::ViewRot, 0), getWord($CS::ViewRot, 1), $CS_Yaw);
	CS_View.setMouse(1, 1);   // drag to spin, wheel to zoom
	CS_applyScale();
	CS_applyLook();
	CS_applyHair();           // setEmpty dropped the hair with everything else
}

// The portrait's body texture does not change with age any more.
//
// Three mechanisms were tried and all three are recorded in the SKIN TYPE section
// of Server/Core/Core_Character.cs. The short version: setIflFrame is server-side
// only so it never reached other clients; per-band shape files DID work but broke
// sixteen add-ons that identify a blockhead by its shapeFile; and reSkin destroys
// this shape's face and decal IFLs.
//
// Age still drives HEIGHT, WIDTH and skin TONE, all of which are live in the
// portrait. Kept as a named no-op rather than deleted because the age slider and
// the race picker both call it, and a stub with this note attached is what stops
// the next person re-adding a mechanism that has already been ruled out.
function CS_applySkinTex()
{
}

// The band index for the current picks: race-major, ascending age - identical
// arithmetic to MRPG_skinFrameForAge on the server. Still used by the life-stage
// readout and by anything reporting which complexion band a character is in.
function CS_skinBand()
{
	return CS_raceIdx() * CS_ageBandCount() + CS_ageBand(CS_age());
}

// Push the current proportions onto the portrait, and pull the camera back by the RACE's
// tallest member (not the current height) - so within a race you actually SEE yourself get
// taller, while the tallest possible character still fits in the frame.
function CS_applyScale()
{
	if(!isObject(CS_View))
		return;
	CS_View.setScale(CS_axisX(), CS_axisY(), CS_axisZ());
	%ref = $CS_RaceMaxIn[CS_raceIdx()] / $CS::BaseInches;
	CS_View.setOrbitDist($CS::ViewDist * %ref);

	// THE HAIR HAS TO BE RE-MOUNTED AFTER A SCALE CHANGE.
	//
	// setScale is view-wide - the engine's own usage string is
	// "ObjectView.setScale(x, y, z)", with no object name, unlike every other
	// method on this control - so there is no way to transform the mounted hair
	// on its own. A mounted shape hangs off the parent's node transform, which
	// is captured from the body when mountObject runs; changing the height
	// afterwards moved the head and left the hair where it was, which is the
	// "hair isn't lined up with the head" the character screen was showing.
	//
	// Re-mounting is cheap (one .dts, already in the resource cache) and it is
	// the only lever this API gives us.
	//
	// If it is STILL off after this, the mount is not picking up the scale at
	// all rather than picking up a stale one, and the next thing to look at is
	// CS_dump() -> CS_View.dumpView(), which prints the view's own idea of
	// where each object sits.
	if($CS_HairOn)
		CS_applyHair();
}

// Spin buttons under the portrait. The view's own setMouse(1,1) already allows dragging;
// these are the belt-and-braces version (and are nicer for fine adjustment).
function CS_spin(%dir)
{
	if(!isObject(CS_View))
		return;
	$CS_Yaw += %dir * 0.3;
	CS_View.setCameraRot(getWord($CS::ViewRot, 0), getWord($CS::ViewRot, 1), $CS_Yaw);
}
// console helper for tuning / diagnosing content that "doesn't show"
function CS_dump()
{
	if(isObject(CS_View))
		CS_View.dumpView();
	echo("CS: yaw" SPC $CS_Yaw SPC "| hair mounted" SPC $CS_HairOn SPC "->" SPC $CS_HairPath[$CS_SelIdx["Hair"]]);
	echo("CS: faces" SPC $CS_FaceN SPC "(from" SPC $CS_FaceDirUsed @ "), current frame" SPC CS_faceFrame()
	 SPC "=" SPC getField($CS_SelOpts["Face"], $CS_SelIdx["Face"]));
	echo("CS: decals" SPC $CS_DecalN SPC "| current" SPC $CS_ClothDecal[$CS_SelIdx["Clothing"]]
	 SPC "-> frame" SPC CS_decalFrame($CS_ClothDecal[$CS_SelIdx["Clothing"]]));
	echo("CS: skin" SPC CS_skinHex() SPC "->" SPC CS_skinColor());
}

// the chosen natural skin tone, as a Torque float colour
function CS_skinColor()
{
	return CS_hexToColor(CS_skinHex());
}

// push the current picks (outfit colours + torso print + face) onto the portrait
function CS_applyLook()
{
	MRPG_applyCharLookToView(CS_View, "CS_Body");
	CS_applyHair();
}

//////////////////////////////////////////////////////////////////////////////
// THE ONE PLACE A MONSTERRPG CHARACTER IS STYLED ONTO A 3D VIEW
//
// Every screen that draws the player must come through here. There used to be two
// independent implementations and they showed different characters:
//
//   character selection  CS_View          + this logic
//   equipment            MonsterRPGx_Avatar + adjustAvatarColors/Nodes, which read the
//                        STOCK BLOCKLAND avatar prefs ($pref::Avatar::HeadColor,
//                        $Pref::Avatar::FaceName) and knew nothing about MonsterRPG at all
//
// So the equipment screen was drawing your Blockland blockhead while character selection
// drew your MonsterRPG character - not a drift between two similar renderers, but two
// entirely different sources of truth. Anything new (gender torso, skin tone, decals)
// would have had to be written twice to stay in step, which is how they diverged.
//
// Parameterised on the view and the object name because those are the only two things
// that differ between the screens; everything below is identical for both.
//////////////////////////////////////////////////////////////////////////////
function MRPG_applyCharLookToView(%view, %obj)
{
	if(!isObject(%view) || %obj $= "")
		return 0;

	//No character data yet - say so rather than painting a half-built default over
	//whatever the caller already had on screen.
	if(!$CS_LookKnown && $CS_SelOpts["Clothing"] $= "")
		return 0;

	for(%i = 0; %i < getWordCount($CS::HideNodes); %i++)
		%view.hideNode(%obj, getWord($CS::HideNodes, %i));
	for(%i = 0; %i < getWordCount($CS::ShowNodes); %i++)
		%view.unHideNode(%obj, getWord($CS::ShowNodes, %i));

	%skin = CS_skinColor();
	%top  = $CS_ClothTop[$CS_SelIdx["Clothing"]];
	%bot  = $CS_ClothBot[$CS_SelIdx["Clothing"]];

	// TORSO SET - has to come AFTER the $CS::HideNodes sweep above, which lists
	// femChest, LArmSlim and RArmSlim among the parts it force-hides. That sweep is
	// what kept the female torso invisible on this screen; overriding it here rather
	// than removing those three from the list keeps the list's meaning intact ("hide
	// everything that is not part of a plain character") for the male case.
	//
	// femChest is narrower than chest, so the slim arms come with it as a set - the
	// boxy arms on a female chest leave a step at the shoulder. Same pairing the
	// server applies in MRPG_applyLookToPlayer.
	%chestNode = "chest";
	%armL = "LArm";
	%armR = "RArm";
	if(CS_isFemale())
	{
		%chestNode = "femChest";
		%armL = "LArmSlim";
		%armR = "RArmSlim";
		%view.hideNode(%obj, "chest");
		%view.hideNode(%obj, "LArm");
		%view.hideNode(%obj, "RArm");
		%view.unHideNode(%obj, "femChest");
		%view.unHideNode(%obj, "LArmSlim");
		%view.unHideNode(%obj, "RArmSlim");
	}

	%view.setNodeColor(%obj, "HeadSkin", %skin);
	%view.setNodeColor(%obj, "LHand", %skin);
	%view.setNodeColor(%obj, "RHand", %skin);
	// The arms are SLEEVES, not skin - the top colour, same as the chest.
	%view.setNodeColor(%obj, %chestNode, %top);
	%view.setNodeColor(%obj, %armL, %top);
	%view.setNodeColor(%obj, %armR, %top);
	%view.setNodeColor(%obj, "pants", %bot);
	%view.setNodeColor(%obj, "LShoe", %bot);
	%view.setNodeColor(%obj, "RShoe", %bot);

	%view.setIflFrame(%obj, "face", CS_faceFrame());
	%df = CS_decalFrame($CS_ClothDecal[$CS_SelIdx["Clothing"]]);
	if(%df >= 0)
		%view.setIflFrame(%obj, "decal", %df);

	// The body skin is NOT a third IFL any more - it is which shape file the
	// portrait loaded, so there is nothing to set here. CS_applySkinTex reloads the
	// body when the age band changes; face and decal above are still real IFLs and
	// still drive per-frame.
	//
	// Deliberately NOT calling CS_applySkinTex from here. This function runs on
	// every colour and clothing change, and CS_setupAvatar calls it - routing a
	// model reload through it would recurse.

	//HAIR IS THE CALLER'S JOB, not this function's. It is mounted per view (CS_applyHair
	//tracks a single $CS_HairOn flag against CS_View), so styling one view must not
	//unmount the other's hair. The character screen calls CS_applyHair right after this.
	return 1;
}

// mount / swap / remove the hair model on the portrait's head node
function CS_applyHair()
{
	if(!isObject(CS_View))
		return;
	if($CS_HairOn)
	{
		CS_View.unMountObject("CS_Hair", $CS::HairNode);
		$CS_HairOn = 0;
	}
	%path = $CS_HairPath[$CS_SelIdx["Hair"]];
	if(%path $= "" || !isFile(%path))
		return;
	CS_View.mountObject("CS_Hair", %path, "", "CS_Body", $CS::HairNode, $CS::HairLod);
	$CS_HairOn = 1;
}

// "face TAB decal TAB top TAB bot TAB hair TAB skinHex TAB scale TAB skinTex TAB age"
// - what the SERVER applies to the real player on Confirm (and re-applies on
// every spawn).
//
// The scale and skin root are still sent, but the server IGNORES both on Confirm
// and rebuilds them from the age, height and trim. They are here because
// MRPG_applyLookToPlayer reads fields 6 and 7 when dressing a player or a bot,
// and that path is shared with the server's own bot roller.
function CS_lookPacket()
{
	%face  = getField($CS_SelOpts["Face"], $CS_SelIdx["Face"]);
	%decal = $CS_ClothDecal[$CS_SelIdx["Clothing"]];
	%top   = $CS_ClothTop[$CS_SelIdx["Clothing"]];
	%bot   = $CS_ClothBot[$CS_SelIdx["Clothing"]];
	%hair  = getField($CS_SelOpts["Hair"], $CS_SelIdx["Hair"]);
	// Field 9 is the eye colour NAME. The server rebuilds the worn face from it
	// (MRPG_faceWithEye) rather than trusting a composed face name from the client,
	// so a tampered value clamps to the default instead of becoming a filename.
	// Field 10 is the GENDER. Like the age it is clamped server-side, and the
	// server re-derives the face and hair against it - a face or hair this gender
	// may not wear is substituted rather than refused, so a stale client cannot
	// wedge character creation.
	return %face TAB %decal TAB %top TAB %bot TAB %hair TAB CS_skinHex() TAB CS_scaleVec()
	       TAB CS_skinTexRoot() TAB CS_age() TAB CS_eyeColor() TAB CS_gender();
}

// The chosen gender as the id the server speaks: "male" / "female".
//
// The PICKER shows "Male"/"Female" because that is what belongs on screen, so this
// lowercases rather than letting the label be the protocol. $MRPG::Look::GenderId
// on the server is the authority for the spelling.
function CS_gender()
{
	%g = getField($CS_SelOpts["Gender"], $CS_SelIdx["Gender"]);
	return %g $= "Female" ? "female" : "male";
}

function CS_isFemale()
{
	return CS_gender() $= "female";
}


//////////////////////////////////
////////// BUILD SCREEN //////////
//////////////////////////////////

function MRPG_buildCharacter()
{
	if($CS_Built && isObject(MRPG_CharDlg))
		return;
	//BEFORE the first control exists. Every caption built below calls CS_fs() to size its
	//font, and CS_fs reads $CS_Scale - so the factor has to be settled here or the whole
	//screen is typeset at 1x and then scaled around the text. See CS_computeScale.
	CS_computeScale();
	$CS_BtnN = 0;  $CS_SldN = 0;
	CS_initSelectors();
	CS_initAttr();

	%dlg = new GuiControl(MRPG_CharDlg)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = "1024 768";
	};
	//FULLY OPAQUE, AND THE SAME COLOUR THE LOADING SCREEN USES.
	//
	//This was "0 0 0 185" - a dim, not a cover. At 185/255 the half-built world, the
	//observer camera's view and the first-person held item all still rendered through it,
	//so character creation played out over scenery sliding about behind the panel.
	//
	//Matching MRPG_LoadingProfile's fillColor exactly (8 10 14) is what makes the handoff
	//clean: the loading screen lifts and this is already behind it in the identical shade,
	//so there is no flash of world and no colour pop between the two screens. If either
	//colour is ever changed, change BOTH or the seam becomes visible again.
	%bg = new GuiSwatchCtrl(MRPG_CharBg)
	{
		profile = "GuiDefaultProfile"; horizSizing = "width"; vertSizing = "height";
		position = "0 0"; extent = "1024 768"; minExtent = "8 2"; color = "8 10 14 255";
	};
	%dlg.add(%bg);

	//THE BACKDROP, IN LAYERS - see CS_buildBackdrop for what each one is and why the flat
	//fill above is still underneath all of it. These are full-screen furniture, NOT part
	//of the 1024x768 design box, so they are added to the DIALOG and positioned from
	//getRes() by CS_layout rather than being scaled with the sheet.
	CS_buildBackdrop(%dlg);

	//EVERYTHING ELSE GOES IN HERE, NOT DIRECTLY ON THE DIALOG.
	//
	//The dialog and the background above are the only two controls that stretch
	//(horizSizing "width"/"height"); every control built below uses "right"/"bottom",
	//which in Torque means FIXED - they keep their design position and size no matter
	//how big their parent gets. So on anything wider than 1024x768 the panel and the
	//portrait stayed pinned to the top-left corner of a full-screen near-black fill,
	//which is what "black background and not centered" was.
	//
	//A stretching background cannot fix that, because the children do not follow their
	//parent's resize. The layout has to be scaled and moved explicitly, and that is
	//what CS_layout does - it needs ONE container holding the whole design so it has
	//something to scale and something to centre. MRPG_CharBg deliberately stays OUTSIDE
	//it: the cover has to span the whole screen at any resolution (see the note above),
	//while the frame is exactly the 1024x768 design box.
	%frame = new GuiSwatchCtrl(CS_Frame)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "0 0"; extent = "1024 768"; minExtent = "8 2"; color = "0 0 0 0";
	};
	%dlg.add(%frame);

	// ---------------- LEFT: the portrait ----------------
	%vx = 44;  %vy = 46;  %vw = 372;  %vh = 676;
	%port = CS_goldFrame(%frame, MRPG_CharPortrait, %vx, %vy, %vw, %vh, "18 14 10 255");

	// wood backdrop (native 372x720, so wrap instead of stretching it)
	%wood = new GuiBitmapCtrl(CS_Wood)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "0 0"; extent = %vw SPC %vh; minExtent = "8 2";
		bitmap = $CS::Wood; wrap = "1";
	};
	%port.add(%wood);
	// soften the board so the character reads against it
	CS_swatch(%port, 0, 0, %vw, %vh, "12 9 6 90");
	CS_swatch(%port, 0, %vh - 92, %vw, 92, "12 9 6 120");

	// the live 3D character
	%ov = new GuiObjectView(CS_View)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "16 20"; extent = (%vw - 32) SPC (%vh - 96); minExtent = "8 2";
		clipToParent = "1"; cameraZRot = "0"; forceFOV = $CS::ViewFOV;
		lightDirection = $CS::ViewLight; lightColor = "1.000000 1.000000 1.000000 1.000000";
		ambientColor = "0.500000 0.500000 0.500000 1.000000";
	};
	%port.add(%ov);

	// caption strip along the bottom of the frame, with spin buttons flanking it
	%pd = (%vw - 300) / 2;
	CS_swatch(%port, %pd, %vh - 76, 300, 2, "170 138 72 200");
	%pt = CS_label(%port, "CS_PortraitName", 62, %vh - 62, %vw - 124, 22);
	%pt.setText("<just:center><font:verdana bold:" @ CS_fs(18) @ "><color:F1ECC2>Your Character");
	%ph = CS_label(%port, "", 62, %vh - 38, %vw - 124, 18);
	%ph.setText("<just:center><font:verdana bold:" @ CS_fs(11) @ "><color:8A8175>drag to spin");
	CS_btn(%port, 18, %vh - 62, 36, 32, $CS::Btn @ "Arrow_button_left",  $CS::Btn @ "Arrow_button_left_fr",  "CS_spin(-1);", "", "");
	CS_btn(%port, %vw - 54, %vh - 62, 36, 32, $CS::Btn @ "Arrow_button_right", $CS::Btn @ "Arrow_button_right_fr", "CS_spin(1);", "", "");
	// catcher for those two buttons only - it must NOT cover the viewport above it
	%pcat = new GuiMouseEventCtrl(CS_MouseP)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "0 " @ (%vh - 76); extent = %vw SPC 76; minExtent = "8 2"; lockMouse = "0";
	};
	%port.add(%pcat);

	// ---------------- RIGHT: the sheet ----------------
	// TALLER AND HIGHER THAN IT WAS (46/676 -> 22/726) to fit a FIFTH picker row for
	// Gender. The four-row grid was completely full and the panel had about 12px of
	// slack at the bottom, so the row had to come out of the panel's own height.
	// 22 + 726 = 748 against a 768 dialog, which keeps a 20px bottom margin.
	%px = 452;  %py = 22;  %pw = 536;  %ph = 726;
	%panel = CS_goldFrame(%frame, MRPG_CharPanel, %px, %py, %pw, %ph, "26 21 16 252");

	// fancy, integrated title
	%tl = CS_label(%panel, "", 0, 14, %pw, 26);
	%tl.setText("<just:center><font:verdana bold:" @ CS_fs(22) @ "><color:F1ECC2>Character");
	%dx = (%pw - 340) / 2;
	CS_swatch(%panel, %dx, 46, 340, 2, "170 138 72 220");
	CS_swatch(%panel, %dx - 9, 43, 8, 8, "214 184 108 255");
	CS_swatch(%panel, %dx + 341, 43, 8, 8, "214 184 108 255");

	// ---- pickers: two columns, five rows ----
	// GENDER LEADS, next to Race: the two together are the character's identity, and
	// gender gates the Hair and Face lists below it the same way age gates the
	// proportion sliders. The rest reflowed forward one slot to make room.
	%rx = 24;  %rw = 488;
	%cw = 232;  %c2 = %rx + 256;
	CS_makeSelector(%panel, "Gender",   "Gender",    %rx, 58,  %cw, 0);
	CS_makeSelector(%panel, "Race",     "Race",      %c2, 58,  %cw, 0);
	CS_makeSelector(%panel, "Covenant", "Covenant",  %rx, 108, %cw, 0);
	CS_makeSelector(%panel, "Body",     "Body Type", %c2, 108, %cw, 0);
	CS_makeSelector(%panel, "Clothing", "Clothing",  %rx, 158, %cw, 0);
	CS_makeSelector(%panel, "Skin",     "Skin Tone", %c2, 158, %cw, 0);
	CS_makeSelector(%panel, "Hair",     "Hair",      %rx, 208, %cw, 0);
	CS_makeSelector(%panel, "Face",     "Face",      %c2, 208, %cw, 0);
	// EYE COLOUR sits next to Face because that is literally what it is - a
	// recoloured copy of the chosen face (see MRPG_faceWithEye on the server; the
	// engine has no way to tint an iris). It took the LIFE STAGE bar's slot, and
	// that readout moved onto the Age slider's own label.
	CS_makeSelector(%panel, "Eye",      "Eye Colour", %rx, 258, %cw, 0);

	// ---- proportions ----
	// RACIAL TRAITS shares this header row (right-aligned) rather than taking a
	// picker slot of its own. The header text is shortened to leave it room.
	//
	// AGE LEADS THE GROUP because it gates the three below it: the height
	// slider's top end and the breadth/depth trim are both derived from it.
	// Everything from here down shifted +50 for the Gender picker row above.
	%ph2 = CS_label(%panel, "", %rx + 2, 312, 250, 14);
	%ph2.setText("<font:verdana bold:" @ CS_fs(12) @ "><color:" @ $CS::UI::Accent @ ">PROPORTIONS <color:8A8175>- age sets the limits");
	CS_label(%panel, "CS_RaceMods", %rx + 252, 312, %rw - 252, 14);
	CS_slider(%panel, "Age", "Age",     %rx, 328, %rw);
	CS_slider(%panel, "Z",   "Height",  %rx, 356, %rw);
	CS_slider(%panel, "X",   "Breadth", %rx, 384, %rw);
	CS_slider(%panel, "Y",   "Depth",   %rx, 412, %rw);
	// live mass / strike energy - STR and VIT move these, never the scale
	CS_label(%panel, "CS_Physique", %rx, 442, %rw, 16);
	CS_label(%panel, "CS_PhysNote", %rx, 460, %rw, 28);

	// ---- attributes ----
	// Everything below shifted down 14px to make room for the age slider; the
	// panel is 676 tall and the Confirm/Close row now ends at 664.
	%ah = CS_label(%panel, "", %rx + 2, 488, 200, 14);
	%ah.setText("<font:verdana bold:" @ CS_fs(12) @ "><color:" @ $CS::UI::Accent @ ">ATTRIBUTES");
	CS_label(%panel, "CS_AttrPoints", %rx + %rw - 200, 488, 200, 14);
	%abox = CS_goldFrame(%panel, "CS_AttrBox", %rx, 506, %rw, 148, "22 17 12 235");
	%half = %rw / 2;
	for(%i = 0; %i < 6; %i++)
	{
		%a  = getWord($CS_AttrList, %i);
		%cx = (%i < 3) ? 24 : %half + 20;
		%ry = 8 + (%i % 3) * 45;
		%al = CS_label(%abox, "", %cx, %ry + 6, 50, 16);
		%al.setText("<font:verdana bold:" @ CS_fs(15) @ "><color:DAD9DD>" @ %a);
		//The minus plate is kept so locked mode can HIDE it. After creation, points are
		//spent one at a time on the server and cannot be taken back, so a "-" would be a
		//button that silently does nothing. CS_btnAt() skips invisible plates, so hiding
		//it removes it from hit-testing too - no dead click target.
		$CS_AttrMinus[%a] = CS_btn(%abox, %cx + 50, %ry, 28, 28, $CS::Btn @ "Button_square", $CS::Btn @ "Button_square_Fr", "CS_attrAdj(\"" @ %a @ "\", -1);", "-", 18);
		%vb = CS_goldFrame(%abox, "", %cx + 86, %ry + 1, 52, 26, "18 14 10 235");
		CS_label(%vb, "CS_AV_" @ %a, 0, 5, 52, 16);
		CS_btn(%abox, %cx + 146, %ry, 28, 28, $CS::Btn @ "Button_square", $CS::Btn @ "Button_square_Fr", "CS_attrAdj(\"" @ %a @ "\", 1);", "+", 18);
	}

	// ---- status line + confirm / close ----
	//
	// %btnRowY and %btnRowH are LOCALS, not literals, because the mouse catcher below
	// has to be sized from them. See the bug note on the catcher.
	%btnRowY = 678;
	%btnRowH = 36;

	CS_label(%panel, "CS_Status", %rx, 660, %rw, 16);
	$CS_ConfirmPlate = CS_btn(%panel, %rx + 22,  %btnRowY, 200, %btnRowH, $CS::Btn @ "Button_long", $CS::Btn @ "Button_long_Fr", "MRPG_charConfirm();",   "Confirm", 14);
	CS_btn(%panel, %rx + 244, %btnRowY, 200, %btnRowH, $CS::Btn @ "Button_long", $CS::Btn @ "Button_long_Fr", "MRPG_closeCharacter();", "Close",   14);

	// ONE catcher over the panel controls drives every button. It deliberately stops at the
	// panel edge so the portrait keeps its own mouse (spin/zoom).
	//
	// THIS WAS THE "CONFIRM DOES NOTHING" BUG, and it is worth spelling out because the
	// symptom points at the wrong place entirely.
	//
	// There are no per-button callbacks in this build - CS_btn only REGISTERS a plate
	// and its command string, and the single catcher is what turns a click into
	// eval($CS_BtnCmd[i]) via CS_btnPress / CS_btnRelease. So a button outside the
	// catcher is not "slow" or "misrouted": it is completely inert, with no error, no
	// console line, and a perfectly normal-looking highlight-free plate.
	//
	// The catcher was a hardcoded 620 tall from 50, ending at panel-y 670, sized for a
	// button row that used to sit at 664. The row later moved to 678..714 and the
	// catcher was never extended, so the ENTIRE Confirm/Close row sat 8px below the
	// catcher's bottom edge and neither button could ever fire. The comment here even
	// documented the invariant ("has to reach the BOTTOM of the Confirm/Close row")
	// while the code no longer met it.
	//
	// So it is now COMPUTED from the row, with a couple of pixels of slack, and cannot
	// drift again: move the row and the catcher follows.
	%catTop = 50;
	%catBot = %btnRowY + %btnRowH + 2;          // 716 - just past the buttons
	if(%catBot > %ph - 4){ %catBot = %ph - 4; } // never past the panel's own edge

	%cat = new GuiMouseEventCtrl(CS_Mouse)
	{
		profile = "GuiDefaultProfile"; horizSizing = "right"; vertSizing = "bottom";
		position = "12" SPC %catTop; extent = (%pw - 24) SPC (%catBot - %catTop);
		minExtent = "8 2"; lockMouse = "0";
	};
	%panel.add(%cat);

	CS_studs(%frame, %vx, %vy, %vw, %vh);
	CS_studs(%frame, %px, %py, %pw, %ph);
	$CS_Built = 1;

	//Scale and centre it for THIS resolution. Has to run after every control exists -
	//CS_layout captures each one's design geometry on its first pass and derives from
	//that copy forever after, so a control built later would never be scaled at all.
	CS_layout();
}

// finalize (only reachable once all points are spent) -> persist on the server + lock
//
// EVERY REFUSAL HERE SAYS WHY. This used to be a bare `return`, so a player who had
// not spent every point clicked Confirm and got absolutely nothing - no message, no
// console line - which is indistinguishable from the button being broken. It was
// worth an explicit branch each: "won't let me confirm" should always come with a
// reason.
function MRPG_charConfirm()
{
	if($CS_Locked)
	{
		if(isObject(CS_Status))
			CS_Status.setText("<just:center><font:verdana bold:" @ CS_fs(12) @ "><color:9BE29B>Already finalized - this character is permanent.");
		echo("CSDBG: confirm refused - already locked");
		return;
	}

	%left = $CS_AttrPoolMax - $CS_AttrSpent;
	if(%left > 0)
	{
		if(isObject(CS_Status))
			CS_Status.setText("<just:center><font:verdana bold:" @ CS_fs(12) @ "><color:FF9A8A>Cannot confirm yet - spend your last " @ %left @ " attribute point" @ (%left == 1 ? "" : "s") @ " first.");
		echo("CSDBG: confirm refused - " @ %left @ " of " @ $CS_AttrPoolMax @ " points unspent");
		return;
	}

	if(%left < 0)
	{
		//Overspend should be impossible (CS_attrAdj caps it), so if it happens the
		//pool bookkeeping is wrong and silently confirming would bake it in.
		if(isObject(CS_Status))
			CS_Status.setText("<just:center><font:verdana bold:" @ CS_fs(12) @ "><color:FF9A8A>Attribute bookkeeping error - reopen the screen.");
		error("CSDBG: confirm refused - overspent by " @ (-%left) @ " points");
		return;
	}

	echo("CSDBG: confirm sending MRPG_CharSet");
	%attrs = $CS_Attr["STR"] TAB $CS_Attr["DEX"] TAB $CS_Attr["VIT"] TAB $CS_Attr["INT"] TAB $CS_Attr["WIS"] TAB $CS_Attr["CHA"];
	commandToServer('MRPG_CharSet',
		$CS_RaceId[CS_raceIdx()],
		getField($CS_SelOpts["Covenant"], $CS_SelIdx["Covenant"]),
		$CS_SelIdx["Clothing"],
		CS_lookPacket(),
		%attrs,
		CS_bodyIdx(),
		CS_heightInches(),
		CS_adj("X"),      // the slider OFFSETS, so the exact sliders can be restored later
		CS_adj("Y"));
	$CS_Locked = 1;

	//The gate releases us server-side on this same command, so the screen stops being
	//modal and Close becomes a real exit again.
	$CS_GateHeld = 0;

	CS_renderAttr();

	//Close it for them. Holding a full-screen dialog open over a body that has just
	//been created means the player is standing in the world unable to see it, which
	//reads as the confirm having hung.
	MRPG_closeCharacter();
}

// set a picker to the option matching a stored value
function CS_selByValue(%name, %val)
{
	%opts = $CS_SelOpts[%name];  %n = getFieldCount(%opts);
	for(%i = 0; %i < %n; %i++)
		if(getField(%opts, %i) $= %val)
		{
			$CS_SelIdx[%name] = %i;
			break;
		}
	CS_setSelLabel(%name);
}

// pick the skin entry whose hex matches a saved value
function CS_skinByHex(%hex)
{
	%hex = strUpr(%hex);
	for(%i = 0; %i < $CS_SkinCount; %i++)
		if(strUpr($CS_SkinHex[%i]) $= %hex)
		{
			$CS_SelIdx["Skin"] = %i;
			break;
		}
	CS_setSelLabel("Skin");
}

// Select the gender from the id the server stores ("male"/"female") and re-narrow
// the gendered pickers. "" is a pre-gender save and resolves to male, matching
// $MRPG::Look::GenderLegacy on the server.
function CS_genderById(%id)
{
	$CS_SelIdx["Gender"] = (%id $= "female") ? 1 : 0;
	CS_setSelLabel("Gender");
	CS_applyGenderLists();
	CS_setSelLabel("Face");
	CS_setSelLabel("Hair");
}

// select a race by its stable save id ("human"), not its display name
function CS_raceById(%id)
{
	for(%i = 0; %i < $CS_RaceCount; %i++)
		if($CS_RaceId[%i] $= %id || $CS_RaceName[%i] $= %id)   // old saves stored the name
		{
			$CS_SelIdx["Race"] = %i;
			break;
		}
	CS_setSelLabel("Race");
}

// server tells us whether this save already made a character (and what it chose)
// packet: created / race / covenant / clothIdx / hair / face / skinHex
//         / bodyIdx / heightInches / adjX / adjY / STR..CHA
function clientCmdMRPG_CharState(%data)
{
	if(!$CS_Built)
		return;
	if(getField(%data, 0))   // created -> show the saved look + lock everything
	{
		CS_raceById(getField(%data, 1));
		CS_selByValue("Covenant", getField(%data, 2));
		$CS_SelIdx["Clothing"] = getField(%data, 3);  CS_setSelLabel("Clothing");

		// GENDER BEFORE HAIR AND FACE. Field 20, after the eye colour. Both of those
		// pickers are gender-filtered, so restoring them against the wrong gender's
		// list would look up a saved choice that is not in it and fall back to index
		// 0 - a created female character would reopen wearing the first male face.
		//
		// Empty on a pre-gender save, and CS_genderById treats that as male, which is
		// what the server's own legacy default resolves to.
		CS_genderById(getField(%data, 20));

		CS_selByValue("Hair",  getField(%data, 4));
		CS_selByValue("Face",  getField(%data, 5));
		if(getField(%data, 6) !$= "")
			CS_skinByHex(getField(%data, 6));

		// AGE BEFORE PROPORTIONS, for the same reason CS_initSelectors does it in
		// that order: the height ceiling and the trim band are both derived from
		// the age, so restoring height first would clamp it against the wrong
		// limits and land the knob somewhere the character never was.
		//
		// Age rides after the attributes and the skin root: field 11 + 6 is the
		// root, 11 + 7 the age. Skin type no longer has a picker to restore - it
		// falls out of the age - so the root is only used as a cross-check below.
		%age = getField(%data, 18);
		if(%age >= $CS::Age::Min && %age <= $CS::Age::Max)
			$CS_SldPos["Age"] = (%age - $CS::Age::Min) / ($CS::Age::Max - $CS::Age::Min);

		// Eye colour, field 19. Matched by NAME rather than by index so that adding
		// a colour to the middle of the palette later cannot silently re-colour
		// every existing character's eyes.
		%eye = getField(%data, 19);
		if(%eye !$= "")
		{
			for(%i = 0; %i < getWordCount($CS::Eye::Colors); %i++)
			{
				if(getWord($CS::Eye::Colors, %i) $= %eye)
				{
					$CS_SelIdx["Eye"] = %i;
					CS_setSelLabel("Eye");
					break;
				}
			}
		}

		// proportions: race defaults first, then the saved values on top
		CS_resetProportions();
		%body = getField(%data, 7);
		if(%body >= 0 && %body <= 2)
		{
			$CS_SelIdx["Body"] = %body;
			CS_setSelLabel("Body");
		}
		%r = CS_raceIdx();
		%hin = getField(%data, 8);
		if(%hin > 0)
		{
			%hmin = $CS_RaceMinIn[%r];
			%hmax = CS_ageMaxHeightIn();       // the AGE's ceiling, not the race's flat max
			if(%hin < %hmin) %hin = %hmin;
			if(%hin > %hmax) %hin = %hmax;
			$CS_SldPos["Z"] = (%hmax > %hmin) ? ((%hin - %hmin) / (%hmax - %hmin)) : 0;
		}
		%amin = CS_adjMin();
		%amax = CS_adjMax();
		%ax = getField(%data, 9);
		%ay = getField(%data, 10);
		if(%ax > 0 && %amax > %amin) $CS_SldPos["X"] = (%ax - %amin) / (%amax - %amin);
		if(%ay > 0 && %amax > %amin) $CS_SldPos["Y"] = (%ay - %amin) / (%amax - %amin);
		if($CS_SldPos["X"] < 0) $CS_SldPos["X"] = 0;   if($CS_SldPos["X"] > 1) $CS_SldPos["X"] = 1;
		if($CS_SldPos["Y"] < 0) $CS_SldPos["Y"] = 0;   if($CS_SldPos["Y"] > 1) $CS_SldPos["Y"] = 1;

		%al = "STR DEX VIT INT WIS CHA";
		for(%i = 0; %i < 6; %i++)
			$CS_Attr[getWord(%al, %i)] = getField(%data, 11 + %i);

		// The server also sends the root it derived (field 17). If our band table
		// disagrees with the server's, the portrait would show a different age's
		// skin from the one the player is actually wearing - which is exactly the
		// kind of silent drift the frame-order comments warn about, so say so.
		%tex = getField(%data, 17);
		if(%tex !$= "" && %tex !$= CS_skinTexRoot())
			warn("CS age: server says skin '" @ %tex @ "', this client derives '"
				@ CS_skinTexRoot() @ "' for age " @ CS_age()
				@ " - $CS::Age::Bands is out of step with the server.");

		$CS_AttrSpent = $CS_AttrPoolMax;
		$CS_Locked = 1;

		//The selectors now hold this character's real look, so any screen can style a view
		//from them - see MRPG_applyCharLookToView. The equipment panel keys off this to
		//know whether it can draw the MonsterRPG character or must leave the stock avatar
		//alone. Only set on the created branch: before that these are defaults, not a
		//character, and painting them onto the equip doll would be inventing an appearance.
		$CS_LookKnown = 1;
		if(isFunction("MRPG_refreshEquipAvatar"))   // lives in GUIFunctions.cs
			MRPG_refreshEquipAvatar();
	}
	else
		$CS_Locked = 0;
	CS_showRaceMods();
	CS_sldRefresh();
	CS_applyScale();
	CS_applyLook();          // includes the skin IFL frame
	CS_renderAttr();
}


//////////////////////////////////
////////// OPEN / CLOSE //////////
//////////////////////////////////

function MRPG_openCharacter()
{
	MRPG_buildCharacter();
	//Re-fit before it goes up. The resetCanvas hook only fires while we are on a
	//MonsterRPG server, so a resolution changed at the main menu - or on another server -
	//would otherwise reach this screen unnoticed. Idempotent, and a no-op when nothing
	//has moved.
	CS_layout();
	if(isObject(MRPG_CharDlg))
		canvas.pushDialog(MRPG_CharDlg);
	$CS_Hover = -1;  $CS_Press = -1;  $CS_SldDrag = -1;
	$CS_Open = 1;
	CS_showRaceMods();
	CS_sldRefresh();
	CS_setupAvatar();                  // build the portrait with the current picks
	commandToServer('MRPG_CharGet');   // locks + restores picks if already created

	//Live attributes and the real SkillPoints balance. Needed because once locked this
	//screen displays and spends the SERVER's numbers, not the creation pool - without
	//this the buffer could be empty or stale from an older session and the panel would
	//show starting values while claiming points were available.
	commandToServer('MRPG_GetStats');

	CS_renderAttr();
	CS_tick();
}
function MRPG_closeCharacter()
{
	//WHILE THE SERVER IS HOLDING YOU ON THE OBSERVER CAMERA, Close is not an exit.
	//There is no body to go back to - the server refuses to spawn one until the
	//character exists (Core_CharacterGate.cs) - so popping the dialog would leave the
	//player staring at scenery with no menu and no way to reopen it if the keybind is
	//unbound. Say so and stay put.
	if($CS_GateHeld && !$CS_Locked)
	{
		if(isObject(CS_Status))
			CS_Status.setText("<just:center><font:verdana bold:" @ CS_fs(12) @ "><color:FF9A8A>You cannot enter the world until your character is created.");
		return;
	}

	$CS_Open = 0;
	cancel($CS_TickSch);
	if(isObject(MRPG_CharDlg))
		canvas.popDialog(MRPG_CharDlg);
}

//////////////////////////////////////////////////////////////////////////////
// THE SERVER IS HOLDING US ON A CAMERA
//
// Sent by Core_CharacterGate.cs when it refuses to spawn a body. $CS_GateHeld makes the
// screen modal: Close is refused (above), and MRPG_charConfirm clears the flag on success
// since the server creates the body in the same breath.
//
// THIS DOES NOT OPEN ANYTHING IMMEDIATELY, AND THAT IS THE WHOLE POINT.
//
// The first version did (`clientCmdMRPG_ForceOpenCharacter` called MRPG_openCharacter
// straight away) and it CRASHED Blockland.exe on join. MRPG_openCharacter builds a
// GuiObjectView, loads m.dts into it, applies face/decal IFLs and pushes a dialog. The
// server's notice arrives during mission load, while the client is regenerating shadow-map
// FBOs and linking GLSL programs - the client log ended mid-"Linking GLSL program...".
//
// So the open WAITS for $CS_Preloaded, which MRPG_preloadCharacter sets only after the
// staged warm-up has finished (itself 2500ms after onMissionDownloadComplete). That flag is
// the only signal here that actually means "the renderer can take a 3D view".
//////////////////////////////////////////////////////////////////////////////
function clientCmdMRPG_GateHeld()
{
	$CS_GateHeld = 1;
	echo("CSDBG: server reports gate held - waiting for preload before opening");
	MRPG_gateOpenWhenReady();
}

//The server says we are no longer gated (character exists, or the gate is off).
function clientCmdMRPG_GateReleased()
{
	$CS_GateHeld = 0;
	cancel($CS_GateWaitSch);
	echo("CSDBG: gate released by server");
}

//Polls our own readiness, then opens. Never opens during mission load.
function MRPG_gateOpenWhenReady()
{
	cancel($CS_GateWaitSch);

	//A 1s poll that re-arms itself, so it gets the same treatment as every other
	//tick: gate, and do NOT re-arm when it is shut. A survivor would open a modal
	//character screen on the NEXT server the player joined. See ServerGate.cs rule 3.
	if(!MRPG_isActive())
		return;

	if(!$CS_GateHeld)
		return;

	if($CS_Open)
		return;                     // already looking at it

	//$CS_Preloaded is set by MRPG_preloadCharacter's last stage. Until then the GUI, the
	//shape and the IFL scans have not been warmed and the renderer may still be mid-load.
	if(!$CS_Preloaded)
	{
		$CS_GateWaitSch = schedule(1000, 0, MRPG_gateOpenWhenReady);
		return;
	}

	echo("CSDBG: preload ready - opening the character screen for the gate");
	MRPG_openCharacter();

	if(isObject(CS_Status))
		CS_Status.setText("<just:center><font:verdana bold:" @ CS_fs(12) @ "><color:FFE9A0>Create your character to enter the world.");

	//Ask the server to re-send our stored state, so the screen opens populated rather than
	//on defaults.
	commandToServer('MRPG_GateRequestScreen');
}
function MRPG_toggleCharacter(%val)
{
	//MRPG_gateKey is the key-DOWN edge test and the "are we on a MonsterRPG
	//server" test in one - see ServerGate.cs. The bind itself stays registered on
	//every server so Options > Keyboard can list it and config.cs can remap it;
	//it is the ACTION that is gated, not the binding.
	if(!MRPG_gateKey(%val))
		return;
	if($CS_Open)
		MRPG_closeCharacter();
	else
		MRPG_openCharacter();
}

// NO BIND HERE. Every MonsterRPG key is borrowed on join and handed back on
// leave by the broker in SCRIPTS/Client/Keybinds.cs, which is the only place a
// MonsterRPG key is named. A bind at file scope - which is what used to be on
// this line - writes into the one global moveMap at game launch and has no way
// to ever give it back.


//////////////////////////////////
///////// PRELOAD ////////////////
//////////////////////////////////
//
// Everything the character screen needs is loaded on the FIRST OPEN otherwise, and
// that first open is expensive enough to be a visible freeze:
//
//   * CS_initSelectors reads face.ifl line by line (487 lines now that the eight
//     eye-colour packs are in), decal.ifl, and globs every hair folder;
//   * MRPG_buildCharacter creates ~100 GUI controls, each with a bitmap that has
//     to be loaded and uploaded;
//   * CS_setupAvatar hands m.dts to the GuiObjectView, and THAT is the big one -
//     TSShape::readIflMaterials pushes every face.ifl and decal.ifl frame into the
//     shape's material list and MaterialList::load walks all of it eagerly. One
//     call, several hundred textures.
//
// WHY THE PRELOAD IS THE SHAPE AND NOT A LIST OF PNGs. The obvious approach - warm
// each texture through a hidden GuiBitmapCtrl - does not work here.
// TextureDictionary::find matches on name AND TYPE AND clamp
// (gTexManager.cc:205), so a GUI-side BitmapTexture entry never satisfies the
// MeshTexture lookup the material list performs. The textures have to be pulled in
// as mesh textures, which means loading the shape. The GUI bitmaps DO benefit from
// building the dialog, because those are looked up as GUI textures.
//
// Staged across schedules rather than done in one call: the point is to stop a
// freeze, and doing all of it in a single frame just moves the freeze to mission
// load. Each stage is timed and logged so it is possible to tell from the console
// whether this actually paid off, instead of assuming it did.
$CS::Preload::Delay = 2500;   // ms after mission load before starting
$CS::Preload::Gap   = 200;    // ms between stages
$CS::Preload::HairPerTick = 3;

$CS_Preloaded = 0;

function MRPG_preloadCharacter(%stage, %arg)
{
	if(%stage $= "")
		%stage = 0;

	// Left the server mid-warm. This runs as a chain of schedules across several
	// seconds, so it must re-check rather than trust the check at stage 0 - and it
	// must return WITHOUT arming the next stage, or the chain outlives the
	// connection that started it. See ServerGate.cs rule 3.
	if(!MRPG_isActive())
		return;

	// Never fight the real screen. If the player opened it manually before the
	// preload finished, everything is loaded anyway and continuing would only
	// tear down the avatar they are looking at.
	if($CS_Open)
	{
		echo("CS preload: character screen already open, nothing left to warm.");
		$CS_Preloaded = 1;
		return;
	}

	switch$(%stage)
	{
		case "0":
			$CS_PreloadT0 = getRealTime();
			// The dialog is built but NOT pushed, so none of this is visible.
			%t = getRealTime();
			MRPG_buildCharacter();
			echo("CS preload 1/3: GUI + face.ifl/decal.ifl/hair scan -"
				SPC (getRealTime() - %t) @ "ms"
				SPC "(" @ $CS_FaceN @ " faces, " @ $CS_HairN @ " hair, "
				@ $CS_DecalN @ " decals)");
			$CS_PreloadSch = schedule($CS::Preload::Gap, 0, MRPG_preloadCharacter, 1);

		case "1":
			// THE EXPENSIVE ONE: the shape drags in every mesh texture the screen
			// can show. If the first manual open is still slow after this, it is
			// because GuiObjectView defers its load until it first renders - in
			// which case the numbers below will look fast and the open will not,
			// and that is the thing to check.
			%t = getRealTime();
			CS_setupAvatar();
			echo("CS preload 2/3: m.dts + material list (every face/decal frame) -"
				SPC (getRealTime() - %t) @ "ms");
			$CS_PreloadSch = schedule($CS::Preload::Gap, 0, MRPG_preloadCharacter, 2, 0);

		case "2":
			// Hair models are separate .dts files, mounted one at a time, so they
			// are warmed a few per tick rather than all at once.
			//
			// CLEAR THE REAL HAIR FIRST. CS_setupAvatar (stage 1) ends by mounting
			// the selected hair as "CS_Hair" on $CS::HairNode, and the warming
			// below mounts onto that same node - two objects on one mount point is
			// exactly the sort of thing that half-works and leaves the portrait
			// wrong. CS_applyHair puts it back when the warming finishes.
			%i = %arg;
			if(%i == 0 && $CS_HairOn)
			{
				CS_View.unMountObject("CS_Hair", $CS::HairNode);
				$CS_HairOn = 0;
			}
			%t = getRealTime();
			%n = 0;
			while(%i < $CS_HairN && %n < $CS::Preload::HairPerTick)
			{
				%p = $CS_HairPath[%i];
				if(%p !$= "" && isFile(%p))
				{
					CS_View.mountObject("CS_HairWarm", %p, "", "CS_Body",
					                    $CS::HairNode, $CS::HairLod);
					CS_View.unMountObject("CS_HairWarm", $CS::HairNode);
					%n++;
				}
				%i++;
			}
			if(%i < $CS_HairN)
			{
				$CS_PreloadSch = schedule(60, 0, MRPG_preloadCharacter, 2, %i);
				return;
			}
			echo("CS preload 3/3: hair models warmed -" SPC (getRealTime() - %t) @ "ms");

			// Put the portrait back to the player's own picks. The hair warming
			// mounted and unmounted other models on it.
			CS_applyHair();
			$CS_Preloaded = 1;
			echo("CS preload: DONE in" SPC (getRealTime() - $CS_PreloadT0) @ "ms total."
				SPC "Opening the character screen should no longer hitch.");
	}
}

// Hooked on the client's own mission-load-complete callback - the moment the user
// asked for, and the earliest point at which the mission is settled enough that a
// few hundred ms of texture loading will not be noticed.
//
// PACKAGED, not redefined: onMissionDownloadComplete is a stock function that
// loads brick menus, wrench menus and the minigame GUI. Replacing it would break
// all of that, so parent:: runs first and the preload is scheduled after it.
if(isPackage("MRPG_CharPreload"))
	deactivatePackage("MRPG_CharPreload");

package MRPG_CharPreload
{
	function onMissionDownloadComplete()
	{
		parent::onMissionDownloadComplete();

		//ONLY ON A MonsterRPG SERVER. This kicks off several hundred face and hair
		//texture loads through a chain of 60-200ms schedules; running it on every
		//server the player joins is a texture-cache flush and a stutter they get
		//nothing for.
		//
		//The ordering works out: addMRPGClientToServer is sent from
		//GameConnection::autoAdminCheck, which stock calls inside
		//GameConnection::startLoad - well before the mission download finishes - so
		//the gate is already open by the time this fires. MRPG_preloadCharacter
		//re-checks anyway, since a slow connect could reorder them.
		if(!MRPG_isActive())
			return;

		cancel($CS_PreloadSch);
		$CS_Preloaded = 0;
		$CS_PreloadSch = schedule($CS::Preload::Delay, 0, MRPG_preloadCharacter, 0);
	}

	// A disconnect invalidates the avatar view and the schedules; leaving one
	// running would fire against a dead GUI on the next connect.
	function disconnect(%bool)
	{
		cancel($CS_PreloadSch);
		$CS_Preloaded = 0;

		// The gate's wait loop too. It polls $CS_Preloaded every second and opens the
		// screen when it flips - a survivor would fire on the NEXT server, opening a modal
		// character screen on a server that never gated us.
		cancel($CS_GateWaitSch);
		$CS_GateHeld = 0;

		parent::disconnect(%bool);
	}
};
activatePackage(MRPG_CharPreload);

// /csopen timing - prove the preload worked rather than assume it. Prints how long
// the open actually took; compare a run with $CS_Preloaded 1 against one with 0.
function MRPG_timeCharacterOpen()
{
	%pre = $CS_Preloaded;
	%t = getRealTime();
	MRPG_openCharacter();
	echo("CS open took" SPC (getRealTime() - %t) @ "ms  (preloaded=" @ %pre @ ")");
}
