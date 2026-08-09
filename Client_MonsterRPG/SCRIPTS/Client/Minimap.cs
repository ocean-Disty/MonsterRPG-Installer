//////////////////////////////////////////////////////////////////////////////
// Minimap.cs  -  the 2D top-down dungeon map, with fog of war
//////////////////////////////////////////////////////////////////////////////
//
// WHAT IT DRAWS. One coloured square per dungeon CELL (4 voxels = 8 units), top-down,
// north up, centred near the player. Cells the player has never been near are not drawn at
// all, so the map fills in as they explore and the shape of what they have walked is
// visible at a glance.
//
// THE CLIENT DRAWS AND NOTHING ELSE. The server owns the explored set (World_Map.cs) and
// pushes cells down; this file has no way to ask for territory the player has not earned,
// which is the whole reason the fog is worth anything.
//
// -----------------------------------------------------------------------------
// WHY A GRID OF SWATCHES, AND WHY IT DOES NOT SCROLL SMOOTHLY
// -----------------------------------------------------------------------------
//
// TGE has no canvas to draw into from script. The options are a bitmap per cell (needs
// 16 wall-pattern PNGs and a texture bind per cell) or a plain coloured rectangle per cell.
// A GuiSwatchCtrl is the cheapest thing on screen - a solid rect, no texture, no text - so
// the map is a fixed grid of them, built once at boot and only ever recoloured.
//
// THE COST THAT DICTATES THE DESIGN is not drawing, it is TorqueScript. Recolouring the
// whole grid is MAP_CELLS^2 field writes, and at 25x25 that is 625 - a visible hitch if it
// happened every time the player walked eight units.
//
// So the map DOES NOT RE-CENTRE CONTINUOUSLY. The grid holds a window on the world, and
// the player marker moves inside it; only when the player reaches the RECENTER_MARGIN ring
// near the edge is the window moved and the grid repainted. Walking in a straight line
// that is one full repaint every ~8 cells (64 units) instead of one every cell.
//
// This is also why the marker is a separate control rather than a cell colour: it has to
// move every tick, and moving one control is free.
//
// -----------------------------------------------------------------------------
// THE MAP BYTE, matching DungeonMapBit in WorldDungeon.hpp
// -----------------------------------------------------------------------------
//     1  wall W        16  floor   (carved; absent = solid rock)
//     2  wall E        32  room
//     4  wall S        64  stair
//     8  wall N       128  artery  (a through-corridor)
//
// The wall bits are not drawn at cell resolution - a wall between two cells has nowhere to
// go when a cell is one square. They are kept because the fog does the job instead: a
// player only ever reveals cells they were beside, so the revealed SHAPE traces the
// corridors, and a maze reads as a maze without a single wall being drawn.
//////////////////////////////////////////////////////////////////////////////

// -----------------------------------------------------------------------------
// THREE MODES, ONE GRID
// -----------------------------------------------------------------------------
//
//   0 CORNER   small, bottom-right, always up while you play
//   1 LARGE    big, centred, more cells and bigger ones - the "open the map" view
//   2 HIDDEN   gone
//
// M cycles LARGE -> CORNER -> HIDDEN -> LARGE.
//
// ONE GRID, REFLOWED - not two grids. The alternative (a small grid and a big one, both
// alive) doubles the control count permanently to save a reflow that happens only on a
// keypress. The grid is allocated at the LARGE size and the outer ring is simply hidden in
// CORNER mode, so switching costs one pass of position/extent writes and nothing at rest.
$Minimap::MODE_CORNER = 0;
$Minimap::MODE_LARGE  = 1;
$Minimap::MODE_HIDDEN = 2;

//Cells across each window. Odd, so there is a true centre cell.
if($Minimap::CellsCorner $= ""){ $Minimap::CellsCorner = 25; }
if($Minimap::CellsLarge  $= ""){ $Minimap::CellsLarge  = 31; }

//Pixels per cell, at the 1024x768 base resolution the HUD is authored against.
if($Minimap::CellPxCorner $= ""){ $Minimap::CellPxCorner = 8; }
if($Minimap::CellPxLarge  $= ""){ $Minimap::CellPxLarge  = 16; }

//How close to the edge the player may get before the window is moved, in cells.
if($Minimap::RecenterMargin $= ""){ $Minimap::RecenterMargin = 4; }

//////////////////////////////////////////////////////////////////////////////
// THE PANEL IS POSITIONED IN REAL SCREEN PIXELS, AND PARENTED TO PlayGui.
//
// It is NOT a child of MonsterRPGx_MAIN_INTERFACE like the rest of the HUD, and that is
// the only way "hug the bottom-right corner" can actually be true.
//
// scaleNewCanvas (GUIFunctions.cs:3614) scales the HUD by HEIGHT and anchors it at
// BOTTOM-CENTRE:
//
//     %ExtAdj = %scrH / 768;
//     %this.UIS_applyScaling(%ExtAdj, %scrW / 2 SPC %scrH);
//
// So the 1024-wide authoring space maps to a 1024 * (scrH/768) wide band centred
// horizontally - and on any widescreen that band stops well short of the screen edge:
//
//     1920x1080  HUD right edge x=1680, screen 1920  -> 240px gap
//     2560x1440  HUD right edge x=2240, screen 2560  -> 320px gap
//     1024x768   HUD right edge x=1024, screen 1024  ->   0px gap
//
// A panel anchored at the far right of HUD space therefore floats 240px inside the real
// corner at 1080p, however large its X coordinate is. Only leaving that coordinate space
// fixes it.
//
// PlayGui is full-screen and is where MAIN_INTERFACE itself is added, so it is definitely
// rendered - and DamageStreak.cs already parents MRPG_StreakHUD there for the same reason.
//////////////////////////////////////////////////////////////////////////////

//Gap from the screen edges in CORNER mode, in real pixels. Small on purpose - the request
//was "hugging", and a minimap that floats is harder to read at a glance.
if($Minimap::CornerMargin $= ""){ $Minimap::CornerMargin = 6; }

//LARGE sits above centre by this fraction of screen height, so it does not bury the HUD.
if($Minimap::LargeRise $= ""){ $Minimap::LargeRise = 0.05; }

//The map keeps the same share of the screen at every resolution. 1080p is the reference
//because that is the floor this was designed against; the clamp keeps it sane on a very
//small window or a 4K display.
if($Minimap::RefHeight $= ""){ $Minimap::RefHeight = 1080; }
if($Minimap::ScaleMin  $= ""){ $Minimap::ScaleMin  = 0.70; }
if($Minimap::ScaleMax  $= ""){ $Minimap::ScaleMax  = 2.00; }

function Minimap_UiScale()
{
	%h = getWord(getRes(), 1);
	if(%h <= 0)
		return 1;

	%s = %h / $Minimap::RefHeight;
	if(%s < $Minimap::ScaleMin){ %s = $Minimap::ScaleMin; }
	if(%s > $Minimap::ScaleMax){ %s = $Minimap::ScaleMax; }
	return %s;
}

//The default a new player gets, and what M cycles back to.
if($Pref::Client::MRPG::MinimapMode $= ""){ $Pref::Client::MRPG::MinimapMode = 0; }

if($Minimap::Enabled $= ""){ $Minimap::Enabled = 1; }

//How often the heading indicator is refreshed, in ms. Client-side and single-control, so
//this is cheap; 60ms is smooth without being a per-frame schedule.
if($Minimap::HeadingMs $= ""){ $Minimap::HeadingMs = 60; }


//The live geometry for the current mode. Everything else reads these rather than branching
//on the mode, so adding a fourth mode later is a table entry and not a hunt.
function Minimap_Cells()
{
	return ($Minimap::Mode == $Minimap::MODE_LARGE)
	     ? $Minimap::CellsLarge : $Minimap::CellsCorner;
}

//In REAL PIXELS, already scaled for the current resolution. mFloor so a cell is a whole
//number of pixels and the 1px inset does not drift between rows.
function Minimap_CellPx()
{
	%base = ($Minimap::Mode == $Minimap::MODE_LARGE)
	      ? $Minimap::CellPxLarge : $Minimap::CellPxCorner;

	%px = mFloor(%base * Minimap_UiScale());
	if(%px < 3){ %px = 3; }
	return %px;
}


//////////////////////////////////////////////////////////////////////////////
// COLOURS
//
// Deliberately low-saturation for everything except the things a player is LOOKING for.
// A map where the corridors are as loud as the stairs is a map you have to read rather
// than glance at.
//////////////////////////////////////////////////////////////////////////////
// THE SPREAD IS THE POINT, and the first version got this wrong.
//
// Rock 38, corridor 88, room 120, artery 150 - four greys inside a 112-point band, on
// 8x8 pixel tiles. Individually sensible, collectively unreadable: the map came out as one
// grey mass and you could not tell a wall from a floor at a glance, which is most of what
// "too voxel" actually meant.
//
// Now the four classes are separated by VALUE AND HUE, not value alone:
//
//   rock      very dark, cold          you can see it is stone and it is not where you walk
//   corridor  mid, neutral warm        the default "you have been here"
//   room      lighter, warmer          reads as a place rather than a passage
//   artery    bright, clearly warmest  the spine you navigate by
//   stair     saturated gold           the only saturated colour on the map, so it pops
//
// Unexplored is the panel backdrop (near-black, cold) and rock is deliberately well clear
// of it: confusing "solid stone I have seen" with "I have never been here" would make the
// map lie about where you have been.
function Minimap_ColorFor(%byte)
{
	if(!(%byte & 16))
		return "30 30 38 255";                   //rock - cold and dark, but not the void

	if(%byte & 64)   return "255 206 70 255";    //stairs: the one thing worth spotting
	if(%byte & 128)  return "196 180 148 255";   //artery: the spine, brightest floor
	if(%byte & 32)   return "150 132 106 255";   //room
	return "104 94 80 255";                      //ordinary corridor
}


//////////////////////////////////////////////////////////////////////////////
// BUILD
//
// One swatch per cell, created once. Named by index so a repaint is an array lookup
// rather than a search of the control tree.
//////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////
// THE PARENT IS PlayGui - and NEVER MonsterRPGx_HUDBase.
//
// HUDBase is the trap here, and it cost a whole debug cycle because the wrong answer looks
// completely correct: the panel is created, every control reports isObject(), the colours
// are set, and nothing is ever drawn.
//
// MonsterRPGx_HUDBase.gui declares HUDBase as the root with MAIN_INTERFACE inside it, so
// HUDBase reads like the HUD. It is not. clientCmdaddMonsterRPGGUI (GUIFunctions.cs:1775)
// does
//
//     PlayGui.add("MonsterRPGx_MAIN_INTERFACE");
//
// which REPARENTS the interface out of HUDBase into PlayGui. HUDBase is left an empty
// orphan that nothing ever adds to the canvas - and `MonsterRPGx_HUD`, which four files
// call setVisible on, does not exist as an object anywhere in the install.
//
// MAIN_INTERFACE was the parent for a while and does render, but it cannot hug a screen
// corner: it is scaled by height and anchored bottom-centre, so its right edge sits 240px
// inside the screen at 1080p. See the note on $Minimap::CornerMargin above.
//
// PlayGui is full-screen, is definitely rendered, and its coordinates ARE screen pixels -
// which is exactly what a corner-hugging map needs. DamageStreak.cs parents
// MRPG_StreakHUD there for the same reason.
//////////////////////////////////////////////////////////////////////////////
function Minimap_Build()
{
	//ONLY ON A MonsterRPG SERVER. The panel is a child of PlayGui, which is
	//full-screen and always rendered, so a map built anywhere else is a black box
	//sitting in the corner of somebody else's server - and that is exactly what
	//the old file-scope `schedule(6000, 0, Minimap_EnsureBuilt)` did on every
	//client from six seconds after launch.
	if(!MRPG_isActive())
		return 0;

	if(isObject(MinimapPanel))
		return 1;

	if(!isObject(PlayGui))
		return 0;

	//ALLOCATED AT THE LARGE SIZE, ALWAYS. CORNER mode hides the outer ring rather than
	//destroying and rebuilding controls, so switching modes never allocates.
	//
	//Sized at the MAXIMUM ui scale so the controls are never too small when the player
	//switches to a bigger resolution; Minimap_ApplyMode resizes them to the real scale.
	%n  = $Minimap::CellsLarge;
	%px = mFloor($Minimap::CellPxLarge * $Minimap::ScaleMax);
	%side = %n * %px;

	//The frame sits under the grid and is 3px larger all round, so the map has a visible
	//edge against the world behind it.
	//horizSizing "left" / vertSizing "top" anchor the control to the parent's RIGHT and
	//BOTTOM edges, so it keeps hugging the corner if the window is resized under it.
	//Minimap_ApplyMode re-positions from getRes() anyway; this makes the in-between frames
	//correct too.
	%panel = new GuiSwatchCtrl(MinimapPanel)
	{
		profile = "GuiDefaultProfile";
		horizSizing = "left";
		vertSizing = "top";
		position = "0 0";
		extent = (%side + 6) SPC (%side + 6);
		minExtent = "8 2";
		enabled = "1";
		visible = "1";
		clipToParent = "1";
		color = "14 14 20 226";
	};

	PlayGui.add(%panel);

	//The unexplored backdrop. Every cell that has never been revealed simply is not drawn,
	//so this is what shows through - one control instead of hundreds of black swatches.
	%bg = new GuiSwatchCtrl(MinimapBG)
	{
		profile = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing = "bottom";
		position = "3 3";
		extent = %side SPC %side;
		color = "6 6 10 255";
	};
	%panel.add(%bg);

	//THE GRID IS INDEXED IN LARGE-GRID COORDINATES for its whole life. CORNER mode uses the
	//centred sub-square of it, so a cell's index never changes meaning between modes.
	for(%i = 0; %i < %n * %n; %i++)
	{
		%sw = new GuiSwatchCtrl()
		{
			profile = "GuiDefaultProfile";
			horizSizing = "right";
			vertSizing = "bottom";
			position = "0 0";
			extent = %px SPC %px;
			color = "0 0 0 0";
			visible = "0";
		};
		%panel.add(%sw);
		$Minimap::Cell[%i] = %sw;
	}

	//COMPASS. Static, because the map never rotates - and that is the feature, not a
	//shortcut. A north-up map you can trust is far easier to navigate by than one that
	//spins with the camera, but only if it SAYS which way is up.
	//WRITTEN OUT RATHER THAN LOOPED. `new GuiMLTextCtrl(%someArray[%i])` depends on the
	//object-name field evaluating an expression, which is a behaviour worth not betting
	//four controls on - and if it silently produced unnamed objects, Minimap_ApplyMode's
	//MinimapN.position writes would fail with no error and the compass would simply never
	//appear. Four explicit blocks cost nothing and cannot do that.
	%cn = new GuiMLTextCtrl(MinimapN) { profile = "GuiMLTextProfile"; horizSizing = "right";
		vertSizing = "bottom"; position = "0 0"; extent = "16 14"; lineSpacing = "0";
		allowColorChars = "1"; maxChars = "-1"; };
	%panel.add(%cn);

	%cs = new GuiMLTextCtrl(MinimapS) { profile = "GuiMLTextProfile"; horizSizing = "right";
		vertSizing = "bottom"; position = "0 0"; extent = "16 14"; lineSpacing = "0";
		allowColorChars = "1"; maxChars = "-1"; };
	%panel.add(%cs);

	%ce = new GuiMLTextCtrl(MinimapE) { profile = "GuiMLTextProfile"; horizSizing = "right";
		vertSizing = "bottom"; position = "0 0"; extent = "16 14"; lineSpacing = "0";
		allowColorChars = "1"; maxChars = "-1"; };
	%panel.add(%ce);

	%cw = new GuiMLTextCtrl(MinimapW) { profile = "GuiMLTextProfile"; horizSizing = "right";
		vertSizing = "bottom"; position = "0 0"; extent = "16 14"; lineSpacing = "0";
		allowColorChars = "1"; maxChars = "-1"; };
	%panel.add(%cw);

	//The player marker, and its NOSE - the heading indicator.
	//
	//TWO CONTROLS RATHER THAN A ROTATED SPRITE because TGE cannot rotate a GuiControl and
	//pre-rendering 8 or 16 arrow PNGs is art to maintain for something a 4px square says
	//just as clearly. The nose sits one step from the marker centre along the facing
	//vector, so it reads as "pointing" at any angle, not just the eight compass points.
	%mk = new GuiSwatchCtrl(MinimapMarker)
	{
		profile = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing = "bottom";
		position = "0 0";
		extent = "8 8";
		color = "255 255 255 255";
		visible = "0";
	};
	%panel.add(%mk);

	%nose = new GuiSwatchCtrl(MinimapNose)
	{
		profile = "GuiDefaultProfile";
		horizSizing = "right";
		vertSizing = "bottom";
		position = "0 0";
		extent = "5 5";
		color = "90 220 255 255";
		visible = "0";
	};
	%panel.add(%nose);

	//Level readout, so a player knows which floor they are looking at. Three stacked
	//floors that all look like corridors are otherwise indistinguishable.
	%lbl = new GuiMLTextCtrl(MinimapLabel)
	{
		profile = "GuiMLTextProfile";
		horizSizing = "right";
		vertSizing = "bottom";
		position = "6 4";
		extent = (%side - 12) SPC 16;
		lineSpacing = "0";
		allowColorChars = "1";
		maxChars = "-1";
	};
	%panel.add(%lbl);

	//LARGE-ONLY extras. Created here rather than on demand so a mode switch only ever
	//toggles visibility - see Minimap_ApplyMode.
	%legend = new GuiMLTextCtrl(MinimapLegend)
	{
		profile = "GuiMLTextProfile";
		horizSizing = "right";
		vertSizing = "bottom";
		position = "6" SPC (%side - 16);
		extent = (%side - 12) SPC 16;
		lineSpacing = "0";
		allowColorChars = "1";
		maxChars = "-1";
		visible = "0";
	};
	%panel.add(%legend);

	$Minimap::Built    = 1;
	$Minimap::HaveView = 0;

	//Restore whatever the player last chose, and lay the grid out for it.
	$Minimap::Mode = $Pref::Client::MRPG::MinimapMode;
	Minimap_ApplyMode();
	Minimap_HeadingTick();

	return 1;
}


//////////////////////////////////////////////////////////////////////////////
// MODE LAYOUT
//
// Reflows the one grid for the current mode: panel size and position, per-cell size and
// position, which cells exist at all, and the large-only extras.
//
// Only runs on a mode change, so the cost (a position + extent write per cell) is paid on
// a keypress and never while playing.
//////////////////////////////////////////////////////////////////////////////
function Minimap_ApplyMode()
{
	if(!$Minimap::Built || !isObject(MinimapPanel))
		return;

	if($Minimap::Mode == $Minimap::MODE_HIDDEN)
	{
		MinimapPanel.setVisible(0);
		return;
	}

	%large = ($Minimap::Mode == $Minimap::MODE_LARGE);
	%n     = Minimap_Cells();
	%px    = Minimap_CellPx();
	%side  = %n * %px;
	%max   = $Minimap::CellsLarge;

	MinimapPanel.setVisible(1);
	MinimapPanel.extent = (%side + 6) SPC (%side + 6);

	//REAL SCREEN PIXELS. The panel is a child of PlayGui, not of the bottom-centre-anchored
	//HUD, so these coordinates are the screen's own - see the note on $Minimap::CornerMargin.
	%res  = getRes();
	%scrW = getWord(%res, 0);
	%scrH = getWord(%res, 1);
	%pw   = %side + 6;

	if(%large)
	{
		//Centred, lifted a little so it does not bury the HUD bar along the bottom.
		MinimapPanel.position = mFloor((%scrW - %pw) / 2)
			SPC mFloor((%scrH - %pw) / 2 - %scrH * $Minimap::LargeRise);
	}
	else
	{
		//HUGGING THE BOTTOM-RIGHT. One margin from each screen edge and nothing else.
		%m = $Minimap::CornerMargin;
		MinimapPanel.position = (%scrW - %pw - %m) SPC (%scrH - %pw - %m);
	}

	//Remembered so the heading tick can notice a resolution change and re-run this.
	$Minimap::LastRes = %res;

	MinimapBG.extent = %side SPC %side;

	//THE CENTRED SUB-SQUARE. Cell indices stay in large-grid space, so CORNER shows the
	//middle %n of the %max grid and hides the rest - which is what keeps a cell's index
	//meaning the same thing in both modes.
	%off = mFloor((%max - %n) / 2);

	for(%gy = 0; %gy < %max; %gy++)
	{
		for(%gx = 0; %gx < %max; %gx++)
		{
			%sw = $Minimap::Cell[%gy * %max + %gx];
			if(!isObject(%sw))
				continue;

			if(%gx < %off || %gx >= %off + %n || %gy < %off || %gy >= %off + %n)
			{
				%sw.setVisible(0);
				%sw.outside = 1;
				continue;
			}
			%sw.outside = 0;

			//ROW 0 IS THE SOUTH ROW in the data and screen Y grows DOWNWARD, so the row
			//index is flipped. Without this the map is mirrored north-to-south, which
			//looks plausible enough to ship and makes every corridor lead the wrong way.
			%lx = %gx - %off;
			%ly = %gy - %off;

			//ONE PIXEL OF INSET. Cells read as tiles instead of a smear, which is a
			//surprising amount of the "too voxel" complaint for zero controls - the gap
			//lets the dark backdrop through as an implicit grid.
			%sw.position = (3 + %lx * %px + 1) SPC (3 + ((%n - 1) - %ly) * %px + 1);
			%sw.extent   = (%px - 1) SPC (%px - 1);
		}
	}

	//Compass, pinned to the panel edges. N is what a player actually looks for; the other
	//three are there so the first glance does not have to be a guess.
	%mid = mFloor((%side + 6) / 2) - 7;
	MinimapN.position = %mid SPC 1;             MinimapN.setText("<just:center><font:verdana bold:11><color:d8dcf0>N");
	MinimapS.position = %mid SPC (%side - 8);   MinimapS.setText("<just:center><font:verdana bold:11><color:8a8fa8>S");
	MinimapW.position = "2" SPC %mid;           MinimapW.setText("<just:center><font:verdana bold:11><color:8a8fa8>W");
	MinimapE.position = (%side - 8) SPC %mid;   MinimapE.setText("<just:center><font:verdana bold:11><color:8a8fa8>E");

	//The marker scales with the cell so it stays readable at both sizes.
	MinimapMarker.extent = %px SPC %px;
	MinimapNose.extent   = mFloor(%px * 0.55) SPC mFloor(%px * 0.55);

	MinimapLabel.extent = (%side - 12) SPC 16;
	MinimapLegend.position = "6" SPC (%side - 14);
	MinimapLegend.extent   = (%side - 12) SPC 16;
	MinimapLegend.setVisible(%large);

	if(%large)
		MinimapLegend.setText("<font:verdana:10><color:c8be96>artery"
			@ "  <color:96846a>room  <color:685e50>hall  <color:ffce46>stairs"
			@ "  <color:5adcff>you");

	Minimap_Repaint();
}


//////////////////////////////////////////////////////////////////////////////
// THE TOGGLE
//
// M cycles LARGE -> CORNER -> HIDDEN -> LARGE, so one press from the default opens the
// big map, a second drops it back to the corner, a third clears the screen.
//////////////////////////////////////////////////////////////////////////////
function Minimap_Cycle()
{
	if(!MRPG_isActive())
		return;

	if(!$Minimap::Built && !Minimap_Build())
		return;

	if($Minimap::Mode == $Minimap::MODE_CORNER)
		$Minimap::Mode = $Minimap::MODE_LARGE;
	else if($Minimap::Mode == $Minimap::MODE_LARGE)
		$Minimap::Mode = $Minimap::MODE_HIDDEN;
	else
		$Minimap::Mode = $Minimap::MODE_CORNER;

	$Pref::Client::MRPG::MinimapMode = $Minimap::Mode;
	Minimap_ApplyMode();
}

//The bindable action. MRPG_gateKey is the key-DOWN edge test and the server test
//in one: without the edge it cycles twice per press and appears to skip a state.
//
//NO FORWARD TO ToggleCursor HERE. An earlier pass had this fall through to stock
//when off-server, because the bind was permanent and the alternative was a dead
//key. The broker in Keybinds.cs hands "m" back to whatever held it instead, so
//this command is simply not bound off-server and stock owns the key outright -
//which is the honest version of what that forward was imitating.
function MRPG_ToggleMap(%val)
{
	if(MRPG_gateKey(%val))
		Minimap_Cycle();
}


//////////////////////////////////////////////////////////////////////////////
// HEADING
//
// COMPUTED CLIENT-SIDE, and that is not an optimisation - it is the only way this can be
// correct. The server does send a yaw with MRPG_MapPos, but that message is only sent when
// the player CHANGES CELL, so a heading drawn from it would freeze for eight units of
// travel and lie about which way you are looking for most of the time.
//
// The client already has the answer locally: its own control object's forward vector,
// available every frame for nothing. No bandwidth, no staleness.
//////////////////////////////////////////////////////////////////////////////
//The repeating half. Split from the work so Minimap_PlaceMarker can refresh the nose
//immediately without cancelling and re-arming the schedule on every cell change.
function Minimap_HeadingTick()
{
	cancel($Minimap::HeadSch);

	//THE GATE, AND IT IS THE FIRST THING IN THE FUNCTION.
	//
	//This tick is where "Unable to find object: 'ServerConnection'" came from,
	//sixteen times a second forever, for every player who ever left the server.
	//It re-armed itself unconditionally, so once started it outlived the
	//connection it was started for and there was nothing left that could stop it.
	//
	//Returning WITHOUT rescheduling is the whole fix: the loop now ends itself at
	//the first tick after the connection goes, whether or not Minimap_Shutdown
	//was reached. See ServerGate.cs rule 3.
	if(!MRPG_isActive() || !$Minimap::Built)
		return;

	$Minimap::HeadSch = schedule($Minimap::HeadingMs, 0, Minimap_HeadingTick);

	//RESOLUTION WATCH, piggybacked here rather than given its own schedule.
	//
	//Positions are real screen pixels now, so a resolution or window change leaves the
	//panel somewhere arbitrary. Blockland has no reliable client-side resize callback to
	//hook, and this tick is already running - a string compare per 60ms is free.
	if($Minimap::Built && getRes() !$= $Minimap::LastRes)
		Minimap_ApplyMode();

	Minimap_HeadingNow();
}

function Minimap_HeadingNow()
{
	if(!$Minimap::Built || $Minimap::Mode == $Minimap::MODE_HIDDEN)
		return;
	if(!isObject(MinimapNose) || !MinimapMarker.isVisible())
	{
		if(isObject(MinimapNose))
			MinimapNose.setVisible(0);
		return;
	}

	//isObject BEFORE THE CALL, not just the flag. Minimap_PlaceMarker calls this
	//directly (not through the tick) so it cannot rely on the tick's gate, and a
	//method call on a dead object name is precisely the console error this whole
	//pass exists to remove.
	if(!MRPG_isActive())
	{
		MinimapNose.setVisible(0);
		return;
	}

	%obj = ServerConnection.getControlObject();
	if(!isObject(%obj))
	{
		MinimapNose.setVisible(0);
		return;
	}

	%fwd = %obj.getForwardVector();
	if(%fwd $= "")
	{
		MinimapNose.setVisible(0);
		return;
	}

	%fx = getWord(%fwd, 0);
	%fy = getWord(%fwd, 1);

	%len = mSqrt(%fx * %fx + %fy * %fy);
	if(%len < 0.001)
	{
		//Looking straight up or down - there is no horizontal heading to draw, and
		//normalising would divide by ~zero.
		MinimapNose.setVisible(0);
		return;
	}
	%fx = %fx / %len;
	%fy = %fy / %len;

	%px = Minimap_CellPx();

	//Marker centre, then step out along the facing vector. Screen Y is inverted against
	//world +Y (north), same flip as the grid.
	%mp = MinimapMarker.getPosition();
	%cx = getWord(%mp, 0) + %px / 2;
	%cy = getWord(%mp, 1) + %px / 2;

	%reach = %px * 0.75;
	%ns = mFloor(%px * 0.55);

	MinimapNose.position = mFloor(%cx + %fx * %reach - %ns / 2)
	                  SPC mFloor(%cy - %fy * %reach - %ns / 2);
	MinimapNose.setVisible(1);
}


//////////////////////////////////////////////////////////////////////////////
// THE EXPLORED STORE
//
// $Minimap::Seen[level, cx, cy] holds the map byte for every cell the server has told us
// about. Kept for the whole session so moving the window is a repaint from memory rather
// than a request to the server.
//////////////////////////////////////////////////////////////////////////////
function clientCmdMRPG_MapReset()
{
	//Wipe by generation counter rather than by walking every key: TorqueScript cannot
	//enumerate an associative array, and tracking a parallel key list on the client would
	//cost more than the stale entries ever do. Bumping the generation makes every old key
	//unreachable, and the reset is rare (a join, or an admin /mapClear).
	$Minimap::Gen++;
	$Minimap::HaveView = 0;

	Minimap_Repaint();
}


function clientCmdMRPG_MapCells(%level, %data)
{
	if(!$Minimap::Enabled)
		return;

	//"cx cy hh cx cy hh ..." - three words per cell.
	%wc = getWordCount(%data);
	%g  = $Minimap::Gen;

	for(%i = 0; %i + 2 < %wc; %i += 3)
	{
		%cx = getWord(%data, %i);
		%cy = getWord(%data, %i + 1);
		%hh = getWord(%data, %i + 2);

		$Minimap::Seen[%g, %level, %cx, %cy] = Minimap_HexToDec(%hh);
	}

	//Only repaint if some of what arrived is actually on screen. During the initial sync
	//most messages describe territory far from the player, and repainting per message
	//would be hundreds of full repaints in a row.
	if($Minimap::HaveView && %level == $Minimap::ViewLevel)
		Minimap_Repaint();
}


function clientCmdMRPG_MapSyncDone(%total)
{
	$Minimap::SyncedCells = %total;
	Minimap_Repaint();
}


//Where the player is, pushed every time they change cell.
function clientCmdMRPG_MapPos(%level, %cx, %cy, %yaw)
{
	if(!$Minimap::Enabled || !MRPG_isActive())
		return;

	if(!$Minimap::Built && !Minimap_Build())
		return;

	$Minimap::PlayerLevel = %level;
	$Minimap::PlayerCX    = %cx;
	$Minimap::PlayerCY    = %cy;
	$Minimap::PlayerYaw   = %yaw;

	%n = Minimap_Cells();
	%half = mFloor(%n / 2);

	//RE-CENTRE ONLY NEAR THE EDGE, or on a level change. See the header: a full repaint is
	//625 field writes and doing it every cell is a hitch every eight units of walking.
	%need = 0;

	if(!$Minimap::HaveView)                       %need = 1;
	else if(%level != $Minimap::ViewLevel)        %need = 1;
	else
	{
		%dx = %cx - $Minimap::ViewCX;
		%dy = %cy - $Minimap::ViewCY;
		%m  = %half - $Minimap::RecenterMargin;

		if(mAbs(%dx) > %m || mAbs(%dy) > %m)      %need = 1;
	}

	if(%need)
	{
		$Minimap::ViewCX    = %cx;
		$Minimap::ViewCY    = %cy;
		$Minimap::ViewLevel = %level;
		$Minimap::HaveView  = 1;
		Minimap_Repaint();
	}

	Minimap_PlaceMarker();
}


//////////////////////////////////////////////////////////////////////////////
// REPAINT
//////////////////////////////////////////////////////////////////////////////
function Minimap_Repaint()
{
	if(!$Minimap::Built || !$Minimap::HaveView)
		return;

	if($Minimap::Mode == $Minimap::MODE_HIDDEN)
		return;

	%n    = Minimap_Cells();
	%half = mFloor(%n / 2);
	%g    = $Minimap::Gen;
	%lv   = $Minimap::ViewLevel;
	%vx   = $Minimap::ViewCX;
	%vy   = $Minimap::ViewCY;

	//Cell indices live in LARGE-grid space for the control's whole life; CORNER draws the
	//centred sub-square. See Minimap_ApplyMode.
	%max = $Minimap::CellsLarge;
	%off = mFloor((%max - %n) / 2);

	for(%ly = 0; %ly < %n; %ly++)
	{
		for(%lx = 0; %lx < %n; %lx++)
		{
			%sw = $Minimap::Cell[(%ly + %off) * %max + (%lx + %off)];
			if(!isObject(%sw))
				continue;

			%byte = $Minimap::Seen[%g, %lv, %vx - %half + %lx, %vy - %half + %ly];

			if(%byte $= "")
			{
				//Unexplored. HIDDEN rather than painted black, so the backdrop shows
				//through - one control's colour instead of N^2 writes of the same value.
				%sw.setVisible(0);
				continue;
			}

			%sw.setVisible(1);

			//WRITE THE FIELD, do not call setColor. GuiSwatchCtrl exposes no setColor
			//console method - the `color` field IS mColor - and the same finding is
			//already recorded at CharacterScreen.cs:1236. (GUIFunctions.cs:698 appears to
			//call setColor, but on a GuiBitmapCtrl-derived control, not a swatch.)
			%sw.color = Minimap_ColorFor(%byte);
		}
	}

	if(isObject(MinimapLabel))
	{
		//LARGE has room for the cell coordinates as well, which is what makes it the
		//"where exactly am I" view rather than just a bigger version of the corner.
		if($Minimap::Mode == $Minimap::MODE_LARGE)
			MinimapLabel.setText("<font:verdana bold:12><color:c8b48c>Floor "
				@ ($Minimap::ViewLevel + 1)
				@ "<font:verdana:10><color:7a7268>    cell "
				@ $Minimap::PlayerCX @ ", " @ $Minimap::PlayerCY);
		else
			MinimapLabel.setText("<font:verdana bold:11><color:c8b48c>Floor "
				@ ($Minimap::ViewLevel + 1));
	}

	Minimap_PlaceMarker();
}


function Minimap_PlaceMarker()
{
	if(!$Minimap::Built || !$Minimap::HaveView || !isObject(MinimapMarker))
		return;

	if($Minimap::Mode == $Minimap::MODE_HIDDEN)
	{
		MinimapMarker.setVisible(0);
		if(isObject(MinimapNose)) MinimapNose.setVisible(0);
		return;
	}

	//A marker for a level the map is not showing would be a lie about where you are.
	if($Minimap::PlayerLevel != $Minimap::ViewLevel)
	{
		MinimapMarker.setVisible(0);
		if(isObject(MinimapNose)) MinimapNose.setVisible(0);
		return;
	}

	%n    = Minimap_Cells();
	%px   = Minimap_CellPx();
	%half = mFloor(%n / 2);

	%gx = $Minimap::PlayerCX - $Minimap::ViewCX + %half;
	%gy = $Minimap::PlayerCY - $Minimap::ViewCY + %half;

	if(%gx < 0 || %gx >= %n || %gy < 0 || %gy >= %n)
	{
		MinimapMarker.setVisible(0);
		if(isObject(MinimapNose)) MinimapNose.setVisible(0);
		return;
	}

	//Same north-up flip as the grid layout.
	MinimapMarker.position = (3 + %gx * %px) SPC (3 + ((%n - 1) - %gy) * %px);
	MinimapMarker.setVisible(1);

	//The nose follows immediately rather than waiting for its own tick, so the heading is
	//correct on the frame the marker moves instead of up to HeadingMs later.
	Minimap_HeadingNow();
}


//////////////////////////////////////////////////////////////////////////////
// HELPERS
//////////////////////////////////////////////////////////////////////////////
function Minimap_HexToDec(%hh)
{
	%digits = "0123456789ABCDEF";
	%hi = strPos(%digits, getSubStr(%hh, 0, 1));
	%lo = strPos(%digits, getSubStr(%hh, 1, 1));

	if(%hi < 0 || %lo < 0)
		return 0;

	return %hi * 16 + %lo;
}


//////////////////////////////////////////////////////////////////////////////
// KILL SWITCH
//
// Separate from the MODE cycle. $Minimap::Enabled turns the whole feature off - no
// building, no repainting, no heading tick - whereas MODE_HIDDEN is a player looking at a
// clean screen with the map still live behind it.
//////////////////////////////////////////////////////////////////////////////
function Minimap_SetVisible(%vis)
{
	$Minimap::Enabled = %vis ? 1 : 0;

	if(isObject(MinimapPanel))
		MinimapPanel.setVisible($Minimap::Enabled && $Minimap::Mode != $Minimap::MODE_HIDDEN);
}


//Kept because older config may call it. Cycling is what M does - see Minimap_Cycle.
function Minimap_Toggle()
{
	Minimap_Cycle();
}


//////////////////////////////////////////////////////////////////////////////
// THE KEYBIND
//
// M IS SAFE TO CLAIM even though Incantation.cs binds every letter including "m". Those
// live on MRPG_IncTypeMap, a separate ActionMap that is PUSHED only for the spell-typing
// QTE and popped after (Incantation.cs:332/399). A pushed map wins, so "m" types a letter
// while casting and toggles the map at every other moment - which is the behaviour you
// want from both.
//
// Registered in $remap as well so it appears in Options > Keyboard and can be moved.
//////////////////////////////////////////////////////////////////////////////
//"m" IS NOT BOUND HERE ANY MORE. Minimap_BindKeys used to call
//MRPG_bindDefault("m", "MRPG_ToggleMap") from the boot-time build fallback, which
//claimed the key on every client at launch and could never give it back. Both the
//bind and the Options > Keyboard row are owned by the broker in Keybinds.cs now:
//borrowed on join, returned on leave, and "m" goes back to stock ToggleCursor.


//Built lazily: MAIN_INTERFACE only exists once the HUD .gui has been exec'd and the
//server has sent addMonsterRPGGUI, so building at file-exec time would have nothing to
//parent to.
function Minimap_EnsureBuilt()
{
	//Left a MonsterRPG server (or never joined one) while this was pending. Stop -
	//do NOT re-arm, or this becomes another loop that outlives its connection.
	if(!MRPG_isActive())
		return;

	if($Minimap::Built)
		return;

	//PlayGui, not MAIN_INTERFACE - the map is a screen-space panel now. Waiting for the HUD
	//would also mean never building for a player whose HUD failed to arrive.
	if(isObject(PlayGui))
		Minimap_Build();
	else
		$MRPG::Gate::MinimapSch = schedule(2000, 0, Minimap_EnsureBuilt);
}


//////////////////////////////////////////////////////////////////////////////
// TEARDOWN
//
// DELETED, NOT HIDDEN. The grid is 31x31 swatches plus a frame, a backdrop, four
// compass labels, a marker, a nose and two text lines - 967 controls - and they
// live in PlayGui, which belongs to whatever server the player is on next. Hiding
// them would leave that tree parked in a stranger's HUD for the rest of the
// session; the rebuild is one loop of `new GuiSwatchCtrl` on the next join, which
// is a cost this add-on already paid at boot every launch.
//
// Deleting MinimapPanel takes its children with it, so the per-cell handles in
// $Minimap::Cell[] are dangling afterwards. $Minimap::Built = 0 is what makes that
// safe: every repaint, marker and mode function tests it before touching the array,
// and Minimap_Build reassigns the whole thing.
//////////////////////////////////////////////////////////////////////////////
function Minimap_Shutdown()
{
	cancel($Minimap::HeadSch);
	$Minimap::HeadSch = "";

	if(isObject(MinimapPanel))
		MinimapPanel.delete();

	$Minimap::Built    = 0;
	$Minimap::HaveView = 0;

	//Bumped so nothing from the last server's dungeon can be repainted onto the
	//next one's map. clientCmdMRPG_MapReset does the same thing for the same
	//reason - TorqueScript cannot enumerate an associative array, so the
	//generation counter IS the wipe.
	$Minimap::Gen++;
}


//////////////////////////////////////////////////////////////////////////////
// BUILD AFTER THE HUD, SO IT DRAWS OVER IT
//
// clientCmdaddMonsterRPGGUI is where the HUD is reparented into PlayGui:
//
//     PlayGui.add("MonsterRPGx_MAIN_INTERFACE");
//
// Sibling draw order inside a GuiControl follows child order, so a panel added to PlayGui
// BEFORE that call ends up underneath MAIN_INTERFACE's full-screen swatch - present,
// correctly coloured, and completely hidden.
//
// (The map used to be built before the parent call, back when it was a child of
// MAIN_INTERFACE and had to exist before UIS_setOriginalValues measured the tree. It is
// not part of that tree any more and positions itself from getRes(), so the ordering
// requirement inverted.)
//
// Building in the parent call, before those two lines run, means the map is just another
// authored HUD control as far as the scaler is concerned.
//
// Packaged rather than edited into GUIFunctions.cs so the map stays removable and does not
// have to be re-applied every time that file is touched.
//////////////////////////////////////////////////////////////////////////////
package MRPGMinimap
{
	function clientCmdaddMonsterRPGGUI()
	{
		//AFTER the parent, and the order is load-bearing now that the panel lives in
		//PlayGui rather than inside the HUD.
		//
		//The parent call is what does PlayGui.add("MonsterRPGx_MAIN_INTERFACE"). Sibling
		//draw order in a GuiControl follows child order, so building FIRST would put the
		//map underneath the whole HUD - present, correct, and invisible behind
		//MAIN_INTERFACE's full-screen swatch.
		//
		//It no longer needs to exist before UIS_setOriginalValues either: the panel is not
		//part of the scaled tree and positions itself from getRes().
		Parent::clientCmdaddMonsterRPGGUI();

		if($Minimap::Enabled)
		{
			//RE-ADD IF IT ALREADY EXISTS. Minimap_Build early-outs on an existing
			//panel, so on the path where the 6s fallback won the race the map was
			//built BEFORE this command - which is the ordering the comment above
			//says must never happen, and the early-out made it silent. Re-adding a
			//control that is already a child moves it to the end of the child list
			//(SimGroup::addObject drops it from its old group first), so this puts
			//the map back on top of MAIN_INTERFACE either way.
			if(isObject(MinimapPanel))
				PlayGui.add(MinimapPanel);
			else
				Minimap_Build();
		}
	}
};

//activatePackage IS the guard. Blockland has no isActivePackage - the exe exports
//isPackage and activatePackage only - so the usual `if(!isActivePackage(X))` wrapper
//logs "Unable to find function" on every exec. It is also redundant: TGE's
//Namespace::activatePackage walks its active list and returns early if the package is
//already on (consoleInternal.cc:975), so calling it twice is a no-op.
activatePackage(MRPGMinimap);


//NOTHING IS ARMED HERE ANY MORE.
//
//This file used to end with:
//
//    schedule(6000, 0, Minimap_EnsureBuilt);
//
//which built the map six seconds after BLOCKLAND started - on every client, on
//every server, in the main menu - and started a 60ms heading tick that nothing
//could stop. That is where the "Unable to find object: 'ServerConnection'" spam
//came from, and it also meant a black map panel sat in the corner of every
//non-MonsterRPG server the player joined.
//
//The fallback still exists and is still needed for the same reason as before -
//addMonsterRPGGUI is sent once per client and guarded by %client.setMonsterRPGGUI
//(Core_OLDpackage.cs:2720), so a client that reconnects without the server
//re-sending it would otherwise never get a map. It is armed from
//MRPG_ClientEnter() now: same six seconds, measured from the join instead of from
//the launch. See ServerGate.cs.
