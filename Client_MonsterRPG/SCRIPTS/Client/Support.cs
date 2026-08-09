// ============================================================================
// CONTAINER SPLIT
// ----------------------------------------------------------------------------
// The 73 storage-container windows live in MonsterRPGx_Containers.gui rather than
// MonsterRPGx_Main.gui, so the main-menu file stays editable. That file is NOT
// pushed as its own dialog - a second full-screen dialog stacked on top of
// MonsterRPGx_Main would swallow every mouse click meant for the inventory.
// Instead every child of the holder (MonsterRPGx_ContainersHolder) is REPARENTED
// into the live tree at load, which is what MonsterRPGx_MergeContainers does.
//
// client.cs calls it once, right after both .gui files are exec'd. The holder,
// MonsterRPGx_Main and MonsterRPGx_SwGUIPar are all "0 0" / "1024 768", so child
// positions carry over unchanged.
// ============================================================================
// THE CONTAINER WINDOWS MUST LAND IN MonsterRPGx_SwGUIPar, NOT IN MonsterRPGx_Main.
//
// This is not tidiness - the sweep that closes stale container windows is written as
// "hide every child of SwGUIPar":
//
//     %wndwCount = MonsterRPGx_SwGUIPar.getCount();
//     for(%c = 0; %c < %wndwCount; %c++)
//         MonsterRPGx_SwGUIPar.getObject(%c).setVisible(false);
//     (CLIENTCMDMonsterRPGx_ResetInvs "Initialize", ClientCommands.cs:526)
//
// and the server fires that on every single inventory open (ServerCommands.cs:24). It is
// the ONLY thing that hides a container window from a previous session.
//
// The un-split ancestor of this GUI puts all 75 of its container windows inside
// RPGx_SwGUIPar (System_RPGsExtended/GUIs/RPGx_Main.gui) - verified, all 75, including
// RPGx_CJugInv and RPGx_RTopBkpkBrInv. When the containers were cut out into
// MonsterRPGx_Containers.gui this merge put them back one level too high, into
// MonsterRPGx_Main. Geometry was unaffected, because SwGUIPar is itself "0 0" / "1024 768"
// inside Main - which is exactly why it looked correct and stayed broken: every window
// still drew in the right place, but the sweep could no longer see any of them, so a
// container stayed on screen after the inventory that owned it was closed.
function MonsterRPGx_MergeContainers()
{
	if(!isObject(MonsterRPGx_Main))
	{
		error("MonsterRPGx_MergeContainers: MonsterRPGx_Main not loaded - skipping merge.");
		return;
	}
	if(!isObject(MonsterRPGx_ContainersHolder))
		return; // container file absent/not exec'd - nothing to merge, harmless

	%host = MonsterRPGx_SwGUIPar;
	if(!isObject(%host))
	{
		// Falling back to Main keeps the windows visible and usable, but they will not be
		// swept - so say so rather than silently reintroducing the bug above.
		error("MonsterRPGx_MergeContainers: MonsterRPGx_SwGUIPar missing - merging into"
			SPC "MonsterRPGx_Main instead. Container windows will NOT auto-close when the"
			SPC "inventory is reopened.");
		%host = MonsterRPGx_Main;
	}

	%moved = 0;
	// add() reparents; pulling index 0 repeatedly drains the holder in order.
	while(MonsterRPGx_ContainersHolder.getCount() > 0)
	{
		%host.add(MonsterRPGx_ContainersHolder.getObject(0));
		%moved++;
	}
	echo("MonsterRPGx_MergeContainers: reparented " @ %moved @ " object(s) into" SPC %host.getName() @ ".");
}

function MonsterRPGx_NewInvBtn(%cellType,%num,%posX,%posY,%isLockSlot)
{
	if(%isLockSlot)
		%lockMod = "_lock";
	
	%btn = new GuiSwatchCtrl("MonsterRPGx_" @ %cellType @ "_ItemBGColor_" @ %num) {
	   profile = "GuiDefaultProfile";
	   position = %posX SPC %posY;
	   extent = "64 64";
	   color = "244 244 244 255";

	   new GuiBitmapCtrl("MonsterRPGx_" @ %cellType @ "_ItemIcon_" @ %num) {
		  profile = "GuiDefaultProfile";
		  position = "0 0";
		  extent = "64 64";
		  bitmap = "Add-Ons/Client_MonsterRPG/GUIs/noItem" @ %lockMod;
		  mColor = "255 255 255 255";
			 MonsterRPGx_BtnImg = "noItem" @ %lockMod;
	   };
	   new GuiSwatchCtrl("MonsterRPGx_" @ %cellType @ "_InfoSwStackAm_" @ %num) {
		  profile = "GuiDefaultProfile";
		  position = "38 48";
		  extent = "28 16";
		  visible = false;
		  color = "0 0 0 255";

		  new GuiMLTextCtrl("MonsterRPGx_" @ %cellType @ "_InfoTxtStackAm_" @ %num) {
			 profile = "GuiMLTextProfile";
			 position = "0 0";
			 extent = "24 16";
			 text = "<font:impact:16><just:right><color:ffffff>999";
			 selectable = "0";
		  };
	   };
	   new GuiSwatchCtrl("MonsterRPGx_" @ %cellType @ "_InfoParent_" @ %num) {
		  profile = "GuiDefaultProfile";
		  position = "0 0";
		  extent = "64 64";
		  visible = false;
		  color = "255 255 255 200";

		  new GuiMLTextCtrl("MonsterRPGx_" @ %cellType @ "_InfoTxtHealth_" @ %num) {
			 profile = "GuiMLTextProfile";
			 position = "22 4";
			 extent = "40 16";
			 text = "<font:impact:16><color:ff0000>100%";
			 selectable = "0";
		  };
		  new GuiBitmapCtrl("MonsterRPGx_" @ %cellType @ "_InfoBmpHealth_" @ %num) {
			 profile = "GuiDefaultProfile";
			 position = "4 4";
			 extent = "16 16";
			 bitmap = "Add-Ons/Client_MonsterRPG/GUIs/icon_durab";
			 mColor = "255 255 255 255";
		  };
		  new GuiBitmapCtrl("MonsterRPGx_" @ %cellType @ "_InfoBmpWght_" @ %num) {
			 profile = "GuiDefaultProfile";
			 position = "4 24";
			 extent = "16 16";
			 bitmap = "Add-Ons/Client_MonsterRPG/GUIs/icon_wght";
			 mColor = "255 255 255 255";
		  };
		  new GuiBitmapCtrl("MonsterRPGx_" @ %cellType @ "_InfoBmpStackLm_" @ %num) {
			 profile = "GuiDefaultProfile";
			 position = "4 44";
			 extent = "16 16";
			 bitmap = "Add-Ons/Client_MonsterRPG/GUIs/icon_stacklm";
			 mColor = "255 255 255 255";
		  };
		  new GuiMLTextCtrl("MonsterRPGx_" @ %cellType @ "_InfoTxtWght_" @ %num) {
			 profile = "GuiMLTextProfile";
			 position = "22 24";
			 extent = "40 16";
			 text = "<font:impact:16><color:0000ff>999 lb";
			 selectable = "0";
		  };
		  new GuiMLTextCtrl("MonsterRPGx_" @ %cellType @ "_InfoTxtStackLm_" @ %num) {
			 profile = "GuiMLTextProfile";
			 position = "22 44";
			 extent = "40 16";
			 text = "<font:impact:16><color:00dd00>999";
			 selectable = "0";
		  };
	   };
	   new GuiBitmapButtonCtrl("MonsterRPGx_" @ %cellType @ "_ItemButton_" @ %num) {
		  profile = "GuiDefaultProfile";
		  position = "0 0";
		  extent = "64 64";
		  command = "MonsterRPGx_SelectCell(\"" @ %cellType @ "\"," @ %num @ ");";
		  altCommand = "MonsterRPGx_AuxSelect(\"" @ %cellType @ "\"," @ %num @ ");";
		  text = " ";
		  //Was "base/client/ui/btnColor" - stock Blockland's slot highlight, not the
		  //theme's. Every cell authored in MonsterRPGx_Containers.gui uses the themed one,
		  //so script-built cells (the corpse window) were the only slots in the game
		  //hovering a different colour from the rest of the inventory.
		  bitmap = $Pref::Client::CurrentTheme @ "btnColor";
		  mColor = "0 0 0 255";
	   };
	};
	MonsterRPGx_VehSpwnInvBG.add(%btn);
}

///PIRATE RPG ADDITIONS I sorry

function clientCmdRPGRename(%command, %text)
{
    $MonsterRPG::RenameCommand = %command;
    
    xRPG_ReNameText.setText(%text);
    xRPG_ReNameGui.getObject(0).setText(%text);
    
    Canvas.pushDialog(xRPG_ReNameGui);
}

function MonsterRPGx_FormParty()
{
    %name = MonsterRPGx_Party_Name.getValue();
    
    if(strLen(%name) < 2)
    {
        messageBoxOKBG("Naming", "Your name is too short. (2-24)");
        
        return;
    }
    
    if(strLen(%name) > 24)
    {
        messageBoxOKBG("Naming", "Your name is too long. (2-24)");
        
        return;
    }
    
    %name = strReplace(%name, " ", "_");
    
    if(getSubStr(%name, 0, 1) $= "_" || getSubStr(%name, strLen(%name) - 1, 1) $= "_")
    {
        messageBoxOKBG("Naming", "Your name cannot begin or end with _.");
        
        return;
    }
    
    %list = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890_";
    
    for(%x = 0; %x < strLen(%name); %x++)
    {
        if(strPos(%list, getSubStr(%name, %x, 1)) == -1)
        {
            MessageBoxOKBG("Naming", "You cannot use symbols in your name.");
            
            return;
        }
    }
    
    %name = strReplace(%name, "_", " ");
    commandToServer(addTaggedString($MonsterRPG::RenameCommand), %name);
    
    Canvas.popDialog(xRPG_ReNameGui);
}

//////////////////////////////////////////////////////////////////////////////
// TAB SWITCHBOARD  --  READ THIS IF MonsterRPGx_Main.gui DOESN'T MATCH THE GAME
//////////////////////////////////////////////////////////////////////////////
//
// The main menu in-game is NOT what you see in MonsterRPGx_Main.gui. Three things
// rewrite it at runtime, which is why the .gui is confusing / has "no Equip menu":
//
//  1. EQUIP, QUEST, SPELLS have NO .gui - they are built in script and add()-ed to
//     MonsterRPGx_PlyrInv the first time their tab opens:
//        Equip  -> Equipment.cs  MRPG_buildEquip    (swatch MonsterRPGx_Equipment)
//        Quest  -> RPGPanels.cs  MRPG_buildQuest    (swatch MonsterRPGx_Quest)
//        Spells -> Spells.cs     MRPG_buildSpells   (MonsterRPGx_SpellsPanel; opens WITH Equip)
//
//  2. STATS and LEADERBOARD are CROSS-WIRED. The .gui swatches keep their old names;
//     the tabs show the opposite one, and the old fixed slots are hidden and
//     replaced by script scroll lists (RPGPanels.cs MRPG_buildPanels):
//        "Stats" tab       (button MainStatsButton,  key "Stats") -> swatch MonsterRPGx_Skills (Attributes/Skills/Traits)
//        "Leaderboard" tab (button MainSkillsButton, key "Skills")-> swatch MonsterRPGx_Stats  (the board)
//
//  3. Tab buttons are plain GuiBitmapCtrls, so their `command=` NEVER fires - only
//     the child GuiMouseEventCtrl does (GUIFunctions.cs). That's why MainQuestButton's
//     "useRPGTab(Quests)" typo is harmless: the real trigger is
//     MonsterRPGx_MainQuestMouse -> useRPGTab("Quest").
//
// FULL MAP  (in-game label -> key below -> swatch shown -> where the content lives):
//    Inventory   -> Inv    -> MonsterRPGx_BigInventory   (.gui)
//    Party       -> Party  -> MonsterRPGx_Party          (.gui, re-skinned in script)
//    Leaderboard -> Skills -> MonsterRPGx_Stats          (.gui swatch + script list)
//    Stats       -> Stats  -> MonsterRPGx_Skills         (.gui swatch + script list)
//    Equip       -> Equip  -> MonsterRPGx_Equipment      (script: Equipment.cs)
//    Quest       -> Quest  -> MonsterRPGx_Quest          (script: RPGPanels.cs)
//
// The swatches were deliberately NOT renamed to match: MonsterRPGx_Skills / _Stats
// are ALSO used as prefixes for dynamically-built slots (e.g. MonsterRPGx_Skills0),
// so a blind rename would corrupt those. THIS COMMENT is the source of truth.
function useRpgTab(%tab)
{
    //The Equipment panel is built at runtime (Equipment.cs); hide it up front so
    //every other tab covers it, then the Equip case re-shows it. The Spells panel
    //(Spells.cs) is a companion that only opens alongside Equip - hide it too.
    if(isObject(MonsterRPGx_Equipment))
        MonsterRPGx_Equipment.setVisible(false);
    if(isObject(MonsterRPGx_SpellsPanel))
        MonsterRPGx_SpellsPanel.setVisible(false);
    if(isObject(MRPG_SpellsChain))
        MRPG_SpellsChain.setVisible(false);

    if(%tab $= "Inv")
    {
        MonsterRPGx_BigInventory.setVisible(true);
		MonsterRPGx_PlyrInvBitmap.setVisible(true);
		MonsterRPGx_PlyrInvBar.setVisible(true);
        MonsterRPGx_Stats.setVisible(false);
        MonsterRPGx_Party.setVisible(false);
		MonsterRPGx_Quest.setVisible(false);
		MonsterRPGx_Skills.setVisible(false);
    }
    else if(%tab $= "Party")
    {
        MonsterRPGx_BigInventory.setVisible(false);
		MonsterRPGx_PlyrInvBitmap.setVisible(false);
		MonsterRPGx_PlyrInvBar.setVisible(false);
        MonsterRPGx_Stats.setVisible(false);
        MonsterRPGx_Party.setVisible(true);
		MonsterRPGx_Quest.setVisible(false);
		MonsterRPGx_Skills.setVisible(false);

		//Build the scroll-list party panels + start hover polling (RPGPanels.cs)
		MRPG_buildParty();
		$MRPG_Party_PlayerSig = ""; $MRPG_Party_MemberSig = ""; // force a fresh render
		MRPG_partyMaybeRender("players");
		MRPG_partyMaybeRender("members");
		MRPG_tipStart();
    }
	else if(%tab $= "Skills")
    {
        // "Skills" tab button is relabelled "Leaderboard" and shows the board,
        // which lives on the MonsterRPGx_Stats swatch (RPGPanels.cs swap).
        MonsterRPGx_BigInventory.setVisible(false);
		MonsterRPGx_PlyrInvBitmap.setVisible(false);
		MonsterRPGx_PlyrInvBar.setVisible(false);
        MonsterRPGx_Stats.setVisible(true);
        MonsterRPGx_Party.setVisible(false);
		MonsterRPGx_Quest.setVisible(false);
		MonsterRPGx_Skills.setVisible(false);

		MRPG_refreshLeaderboard();
    }
    else if(%tab $= "Stats")
    {
        // "Stats" tab shows Attributes + Combat + Specialty + Traits, which live
        // on the MonsterRPGx_Skills swatch (RPGPanels.cs swap).
        MonsterRPGx_BigInventory.setVisible(false);
		MonsterRPGx_PlyrInvBitmap.setVisible(false);
		MonsterRPGx_PlyrInvBar.setVisible(false);
        MonsterRPGx_Stats.setVisible(false);
        MonsterRPGx_Party.setVisible(false);
		MonsterRPGx_Quest.setVisible(false);
		MonsterRPGx_Skills.setVisible(true);

		MRPG_refreshStats();
    }
	else if(%tab $= "Quest")
    {
		//Build (once) the quest panel first, then show + refresh it (RPGPanels.cs)
		MRPG_buildQuest();

        MonsterRPGx_BigInventory.setVisible(false);
		MonsterRPGx_PlyrInvBitmap.setVisible(false);
		MonsterRPGx_PlyrInvBar.setVisible(false);
        MonsterRPGx_Stats.setVisible(false);
        MonsterRPGx_Party.setVisible(false);
		MonsterRPGx_Quest.setVisible(true);
		MonsterRPGx_Skills.setVisible(false);

		MRPG_refreshQuests();
    }
	else if(%tab $= "Equip")
    {
        MonsterRPGx_BigInventory.setVisible(false);
		MonsterRPGx_PlyrInvBitmap.setVisible(false);
		MonsterRPGx_PlyrInvBar.setVisible(false);
        MonsterRPGx_Stats.setVisible(false);
        MonsterRPGx_Party.setVisible(false);
		MonsterRPGx_Quest.setVisible(false);
		MonsterRPGx_Skills.setVisible(false);

		//Build (once) + show the equipment panel and (re)draw the avatar (Equipment.cs)
		MRPG_buildEquip();
		MonsterRPGx_Equipment.setVisible(true);
		MRPG_refreshEquip();

		//Open the Spells companion panel to the right (Spells.cs) and start the
		//shared hover-tooltip loop so its rows show details on hover.
		MRPG_showSpells(true);
		MRPG_tipStart();
    }
}

function upgradeTrait(%trait,%amount)
{
    if(!%amount){%amount = 1;}

    commandToServer('Pshop',%trait,%amount);
}



////////////////////////
///////////////////////
////////////resize

//- GuiControl::shift (moves a gui in the X or Y)
function GuiControl::shift(%this,%x,%y)
{
	%this.position = vectorAdd(%this.position,%x SPC %y);
}

//- GuiControl::center (Centers %this inside %parent)
function GuiControl::center(%this,%parent)
{
	%this.centerX(%parent);
	%this.centerY(%parent);
}

//- GuiControl::getCanvasPosition (returns absolute position of a control on the canvas)
function GuiControl::getCanvasPosition(%this)
{
	%targ = %this;
	%x = getWord(%this.position,0);
	%y = getWord(%this.position,1);
	while(isObject(%targ.getGroup()))
	{
		%parent = %targ.getGroup();
		if(%parent.getName() $= "Canvas")
			return %x SPC %y;
			
		%x += getWord(%parent.position,0);
		%y += getWord(%parent.position,1);
		%targ = %parent;
	}
}

//- GuiControl::centerX (Centers %this inside %parent horizontally)
function GuiControl::centerX(%this,%parent)
{
	if(isObject(%parent))
	{
		%maxArea = getWord(%parent.extent,0);
		%width = getWord(%this.extent,0);
		
		%xPosition = (%maxArea/2)-(%width/2);
		if(%parent $= %this.getGroup())
			%this.position = (%xPosition+getWord(%parent.position,0)) SPC getWord(%this.position,1);
		else
			%this.position = (%xPosition+getWord(%parent.getCanvasPosition(),0)) SPC getWord(%this.getCanvasPosition(),1);
	}
	else
	{
		%parent = %this.getGroup();
		%maxArea = getWord(%parent.extent,0);
		%width = getWord(%this.extent,0);
		
		%xPosition = (%maxArea/2)-(%width/2);
		%this.position = %xPosition SPC getWord(%this.position,1);
	}
}

//- GuiControl::centerY (Centers %this inside %parent vertically)
function GuiControl::centerY(%this,%parent)
{
	if(isObject(%parent))
	{
		%maxArea = getWord(%parent.extent,1);
		%height = getWord(%this.extent,1);

		%yPosition = (%maxArea/2)-(%height/2);
		if(%parent $= %this.getGroup())
			%this.position = getWord(%this.position,0) SPC (%yPosition+getWord(%parent.position,1));
		else
			%this.position = getWord(%this.getCanvasPosition(),0) SPC (%yPosition+getWord(%parent.getCanvasPosition(),1));
	}
	else
	{
		%parent = %this.getGroup();
		%maxArea = getWord(%parent.extent,1);
		%height = getWord(%this.extent,1);
		
		%yPosition = (%maxArea/2)-(%height/2);
		%this.position = getWord(%this.position,0) SPC %yPosition;
	}
}

/////
/////


//wordcenter support


// Blockland's FOV pref is HORIZONTAL. $cameraFoV (used by the old version of
// this function) is never assigned anywhere, and the mod actively drives FOV off
// player velocity in clientCmdDynamicFOVTick, so the live value has to be read
// or nameplates drift outward whenever you sprint.
function MRPG_GetCameraFov()
{
    if($DynamicFOV::CurrentFOV > 0)
        return $DynamicFOV::CurrentFOV;

    if($pref::Player::defaultFov > 0)
        return $pref::Player::defaultFov;

    return 90;
}

// In third person the eye transform is the PLAYER'S eye, not the camera, which
// sits pulled back behind them - projecting from the eye puts labels off by that
// distance. $TPOn is already tracked client-side by the HataCrosshair package.
// Blockland's camera also collides, so this is an approximation: tune if labels
// sit slightly off in third person.
if($Pref::Client::MonsterRPGx::ThirdPersonCamDist $= "")
    $Pref::Client::MonsterRPGx::ThirdPersonCamDist = 4.5;

// getEyeTransform() comes back EMPTY on a client-side Player ghost in this
// build - confirmed by diagnostic. Different Blockland versions expose
// different pieces client-side, so probe once for a source that actually
// returns something and remember which one won. Probing every frame would
// spam "Unable to find function" for whichever candidates do not exist.
//
// Ordered best-first:
//   conn  - the real camera. Handles pitch AND the third-person pullback.
//   eye   - player's eye. Correct orientation, needs manual TP offset.
//   xform - player body transform. Has YAW but no PITCH, so vertical accuracy
//           degrades when looking up or down. Last resort.
function MRPG_DetectCameraSource()
{
    $MRPG::Cam::Source = "none";

    if(!isObject(ServerConnection))
        return;

    %obj = ServerConnection.getControlObject();

    if(!isObject(%obj))
        return;

    if(ServerConnection.getControlCameraTransform() !$= "")
    {
        $MRPG::Cam::Source = "conn";
        return;
    }

    if(%obj.getEyeTransform() !$= "")
    {
        $MRPG::Cam::Source = "eye";
        return;
    }

    if(%obj.getTransform() !$= "")
    {
        $MRPG::Cam::Source = "xform";
        return;
    }
}

function MRPG_GetCameraTransform()
{
    if(!isObject(ServerConnection))
        return "";

    %obj = ServerConnection.getControlObject();

    if(!isObject(%obj))
        return "";

    if($MRPG::Cam::Source $= "" || $MRPG::Cam::Source $= "none")
        MRPG_DetectCameraSource();

    switch$($MRPG::Cam::Source)
    {
        // Already the camera - third-person offset is baked in, do not add it.
        case "conn":
            return ServerConnection.getControlCameraTransform();

        case "eye":
            %eyeT = %obj.getEyeTransform();

        case "xform":
            %eyeT = %obj.getTransform();

        default:
            return "";
    }

    if(!$TPOn)
        return %eyeT;

    %fwd = %obj.getForwardVector();
    %pos = VectorSub(getWords(%eyeT, 0, 2),
                     VectorScale(%fwd, $Pref::Client::MonsterRPGx::ThirdPersonCamDist));

    return %pos SPC getWords(%eyeT, 3, 6);
}

// Projects a world point to screen pixels.
// Returns "screenX screenY depth", or "" when the point is at/behind the camera.
//
// Rewritten - the previous version had three defects:
//   1. A "%distanceFactor = 1 - (%distance / 100)" term multiplied into the
//      projection. Perspective already accounts for distance via the depth
//      component; that term hit 0 at 100 units (divide by zero) and went
//      negative past it, mirroring everything.
//   2. It read $cameraFoV, which is never set - mTan("" * $pi/360) is 0, so
//      %fovFactor was 0 and every result was a divide by zero.
//   3. The vertical term mixed screenWidth into a height calculation and then
//      inverted the result twice on the way out.
//
// The core was right and is kept: rotating the world delta by the eye
// transform's INVERSE rotation (same axis, negated angle) yields camera space,
// where +X is right, +Y is forward/depth and +Z is up.
function worldToScreen(%eyeTransform, %worldPosition)
{
    if(%eyeTransform $= "" || %worldPosition $= "")
        return "";

    %delta = VectorSub(%worldPosition, getWords(%eyeTransform, 0, 2));

    %offset = MatrixMulVector("0 0 0" SPC getWords(%eyeTransform, 3, 5) SPC (-1 * getWord(%eyeTransform, 6)), %delta);

    %x = getWord(%offset, 0);   // right
    %y = getWord(%offset, 1);   // forward - this is depth
    %z = getWord(%offset, 2);   // up

    // Behind the camera, or so close the divide blows up.
    if(%y <= 0.1)
        return "";

    %tanHalf = mTan(MRPG_GetCameraFov() * $pi / 360);

    if(%tanHalf <= 0)
        return "";

    %res = getRes();
    %sw = getWord(%res, 0);
    %sh = getWord(%res, 1);

    if(%sw <= 0 || %sh <= 0)
        return "";

    // Horizontal from the FOV directly; vertical derived via aspect ratio.
    %ndcX = %x / (%y * %tanHalf);
    %ndcY = %z / (%y * %tanHalf / (%sw / %sh));

    %screenX = (0.5 + (%ndcX * 0.5)) * %sw;
    %screenY = (0.5 - (%ndcY * 0.5)) * %sh;

    return %screenX SPC %screenY SPC %y;
}



function MonsterRPGx_HUD_resize()
{
	     //x5 pixel size from the original pic of medieval ui for scale
        //RESIZE HP AND MANA BAR
			
		%res = getRes();
	    %screenWidth = getWord (%res, 0);
	    %screenHeight = getWord (%res, 1);
		%bottom = 0;
		
		%pos = "0 0";
	    %extent = "1024 768";
		
		%ResAdjust = %screenHeight - 768;
		%ExtAdjust = %screenHeight / 768;
		
		talk("Adjust: " @ %ExtAdjust);
		
		%w = getWord (%extent, 0);
		%h = getWord (%extent, 1);
		%x = %w / 2 - %w / 2;
	    %y = (%h - %w * 1) - %bottom;
	    MonsterRPGx_MAIN_INTERFACE.resize (%x, %y + (256 + %ResAdjust), %w, %h);
		MonsterRPGx_MAIN_INTERFACE.CenterX();
		
		talk("Ext 1: " @ Bottom_UI.getExtent());
		talk("Pos 1: " @ Bottom_UI.getPosition());
		
		//%bPos = "110 660";
		//%bExt = "810 128";
		//%w = getWord (%bExt, 0);
		//%h = getWord (%bExt, 1);
		//%x = getWord (%bPos, 0);
	    //%y = getWord (%bPos, 1);
		//Bottom_UI.resize (%x, %y, %w * %ExtAdjust, %h * %ExtAdjust);
		//UIS_applyScaling(Bottom_UI);
		
		talk("Ext 2: " @ Bottom_UI.getExtent());
		talk("Pos 2: " @ Bottom_UI.getPosition());
			
		///RESIZE TOP PRINT
		
		%res = getRes();
	    %screenWidth = getWord (%res, 0);
	    %screenHeight = getWord (%res, 1);
		%bottom = 0;
		
		%pos = TopPrintDlg.getPosition();
	    %extent = TopPrintDlg.getExtent();
		
		%w = getWord (%extent, 0);
		%h = getWord (%extent, 1);
		%x = %w / 2 - %w / 2;
	    %y = (%h - %w * 1) - %bottom;
	    TopPrintDlg.resize (%x, %y, %w, %h);
		
	    %CorrectY = getWord(centerPrintDlg.getPosition(), 1) + 150;
		%CorrectX = getWord(centerPrintDlg.getPosition(), 0);
		
		TopPrintDlg.resize(%CorrectX - getWord(%extent, 0), %CorrectY - getWord(%extent, 1), getWord(%extent, 0), getWord(%extent, 1));
		TopPrintDlg.CenterX();

	    if(getWord(TopPrintDlg.getPosition(), 1) < 0)
		    TopPrintDlg.shift(0, mAbs(getWord(TopPrintDlg.getPosition(), 1)) * 1);
		
		
		///RESIZE MIDDLE PRINT
		
		%res = getRes();
	    %screenWidth = getWord (%res, 0);
	    %screenHeight = getWord (%res, 1);
		%bottom = 0;
		
		%pos = MiddlePrintDlg.getPosition();
	    %extent = MiddlePrintDlg.getExtent();
		
		%w = getWord (%extent, 0);
		%h = getWord (%extent, 1);
		%x = %w / 2 - %w / 2;
	    %y = (%h - %w * 1) - %bottom;
	    MiddlePrintDlg.resize (%x, %y, %w, %h);
		
	    %CorrectY = getWord(centerPrintDlg.getPosition(), 1) + 450;
		%CorrectX = getWord(centerPrintDlg.getPosition(), 0);
		
		MiddlePrintDlg.resize(%CorrectX - getWord(%extent, 0), %CorrectY - getWord(%extent, 1), getWord(%extent, 0), getWord(%extent, 1));
		MiddlePrintDlg.CenterX();

	    if(getWord(MiddlePrintDlg.getPosition(), 1) < 0)
		    MiddlePrintDlg.shift(0, mAbs(getWord(MiddlePrintDlg.getPosition(), 1)) * 1);
}

/////
/////


function MBSetTextBG(%text, %frame, %msg)
{
    // Calculate the difference between actual text length and true text length
    %realDivider = (strLen(stripmlcontrolchars(%msg)) - strLen(%msg)) / 2;
	%ext = %text.getExtent();
    
    // Store the initial text height to prevent cumulative expansion
    if (%text.initialHeight $= ""){ %text.initialHeight = getWord(%ext, 1); }
	
    %newHeight = %text.initialHeight + %realDivider;
	%newWidth = %text.initialWidth + %realDivider;
    %text.setText("<just:center>" @ %msg);
    %text.forceReflow();
	
    %newExtent = getWord(%ext,0) SPC %newHeight;
    %deltaY = getWord(%newExtent, 1) - getWord(%ext, 1);

    %windowPos = %frame.getPosition();
    %windowExt = %frame.getExtent();
	
	%TextPos = %text.getPosition();
    %TextExt = %text.getExtent();

    %BKDPos = MBOKBackDrop.getPosition();
    %BKDExt = MBOKBackDrop.getExtent();
    %BKDExtNewY = getWord(%BKDExt, 1) + %deltaY;
	
	%xRed = 300;

    %frame.resize(getWord(%windowPos, 0), getWord(%windowPos, 1) - %deltaY / 2, getWord(%windowExt, 0) - %xRed, getWord(%windowExt, 1) + %deltaY);
    MBOKBackDrop.resize(getWord(%BKDPos, 0), getWord(%BKDPos, 1), getWord(%BKDExt, 0) - %xRed, %BKDExtNewY);
	%text.resize(getWord(%TextPos, 0), getWord(%TextPos, 1), getWord(%TextExt, 0) - %xRed, getWord(%TextExt, 1) + %deltaY);
}

function MessageBoxOKBG (%title, %message, %callback)
{
	MBOKFrameBG.setText (%title);
	
	Canvas.pushDialog(MessageBoxOKDlgBG);
	MBOKFrameBG.resize(getWord(Canvas.getCursorPos(),0) - 275,getWord(Canvas.getCursorPos(),1) - 360,551,362);
    MBOKBackDrop.resize(5,23,541,335);
	MBOKTextBG.resize(0,0,538,14);
	
	MBSetTextBG(MBOKTextBG, MBOKFrameBG, %message);	
	MessageBoxOKDlgBG.callBack = %callback;
}

function MessageBoxOKDlgBG::onSleep (%this)
{
	%this.callBack = "";
}

function clientCmdMessageBoxOKBG (%title, %message)
{
	MessageBoxOKBG (detag (%title), detag (%message), "");
}


function MessageBoxOKCancelBG(%title, %message, %callback, %cancelCallback)
{
    MBOKCancelFrameBG.setText(%title);
    Canvas.pushDialog(MessageBoxOKCancelDlgBG);
    MBSetTextBG(%textBoxName, MBOKCancelFrameBG, %chunk);
    MessageBoxOKCancelDlgBG.callBack = %callback;
    MessageBoxOKCancelDlgBG.cancelCallback = %cancelCallback;
}

function clientCmdMessageBoxOKCancelBG (%title, %message, %okServerCmd, %cancelServerCmd)
{
	%okTag = getTag (%okServerCmd);
	%okTag = mFloor (%okTag);
	%okString = getTaggedString (%okTag);
	%okString = getSafeVariableName (%okString);
	if (%okString $= "")
	{
		%okCallBack = "";
	}
	else 
	{
		%okCallBack = "commandToServer(\'" @ %okString @ "\');";
	}
	%cancelCallback = "commandToServer(\'MessageBoxCancelBG\');";
	MessageBoxOKCancelBG (detag (%title), detag (%message), %okCallBack, %cancelCallback);
}

function MessageBoxOKCancelDlgBG::onSleep (%this)
{
	%this.callBack = "";
}

function messageBoxYesNoBG (%title, %message, %yesCallback, %noCallback)
{
	MBYesNoFrameBG.setText (%title);
	Canvas.pushDialog (MessageBoxYesNoDlgBG);
	MBSetTextBG(MBYesNoTextBG, MBYesNoFrameBG, %chunk);
	MessageBoxYesNoDlgBG.yesCallBack = %yesCallback;
	MessageBoxYesNoDlgBG.noCallback = %noCallback;
}

function clientCmdMessageBoxYesNoBG (%title, %message, %okServerCmd, %cancelServerCmd)
{
	%okTag = getTag (%okServerCmd);
	%okTag = mFloor (%okTag);
	%okString = getTaggedString (%okTag);
	%okString = getSafeVariableName (%okString);
	if (%okString $= "")
	{
		%okCallBack = "";
	}
	else 
	{
		%okCallBack = "commandToServer(\'" @ %okString @ "\');";
	}
	%cancelCallback = "commandToServer(\'MessageBoxNoBG\');";
	messageBoxYesNoBG (detag (%title), detag (%message), %okCallBack, %cancelCallback);
}

function MessageBoxYesNoDlgBG::onSleep (%this)
{
	%this.yesCallBack = "";
	%this.noCallback = "";
}


///TopPrint

$TopPrintSizes[1] = 20;
$TopPrintSizes[2] = 36;
$TopPrintSizes[3] = 56;

function TopPrintText::onResize (%this, %width, %height)
{
	%this.position = "0 0";
}

function clientCmdClearTopPrint ()
{
	$TopPrintActive = 0;
	TopPrintDlg.visible = 0;
	if (isEventPending ($TopPrintDlg::removePrintEvent))
	{
		cancel ($TopPrintDlg::removePrintEvent);
	}
	$TopPrintDlg::removePrintEvent = 0;
}

function clientCmdTopPrint (%message, %time, %size)
{
	if ($TopPrintActive)
	{
		if ($TopPrintDlg::removePrintEvent != 0)
		{
			cancel ($TopPrintDlg::removePrintEvent);
			$TopPrintDlg::removePrintEvent = 0;
		}
	}
	else 
	{
		TopPrintDlg.visible = 1;
		$TopPrintActive = 1;
	}
	TopPrintText.setText ("<just:center>" @ %message @ "\n");
	if (%time > 0)
	{
		$TopPrintDlg::removePrintEvent = schedule (%time * 1000, 0, "clientCmdClearTopPrint");
	}
}


///MiddlePrint

$MiddlePrintSizes[1] = 20;
$MiddlePrintSizes[2] = 36;
$MiddlePrintSizes[3] = 56;

function MiddlePrintText::onResize (%this, %width, %height)
{
	%this.position = "0 0";
}

function clientCmdClearMiddlePrint ()
{
	$MiddlePrintActive = 0;
	MiddlePrintDlg.visible = 0;
	if (isEventPending ($MiddlePrintDlg::removePrintEvent))
	{
		cancel ($MiddlePrintDlg::removePrintEvent);
	}
	$MiddlePrintDlg::removePrintEvent = 0;
}

function clientCmdMiddlePrint (%message, %time, %size)
{
	if ($MiddlePrintActive)
	{
		if ($MiddlePrintDlg::removePrintEvent != 0)
		{
			cancel ($MiddlePrintDlg::removePrintEvent);
			$MiddlePrintDlg::removePrintEvent = 0;
		}
	}
	else 
	{
		MiddlePrintDlg.visible = 1;
		$MiddlePrintActive = 1;
	}
	MiddlePrintText.setText ("<just:center>" @ %message @ "\n");
	if (%time > 0)
	{
		$MiddlePrintDlg::removePrintEvent = schedule (%time * 1000, 0, "clientCmdClearMiddlePrint");
	}
}


////SUPPORT FOR HIT MARKER

function clientCmdSendHitmarkerData(%eyeTransform, %enemyPos)
{
    //Was WorldToScreen(%enemyPos) - a single argument to a two-parameter
    //function, so the enemy position landed in %eyeTransform and the world
    //position was empty. Prefer the live local camera over the relayed
    //transform; the relayed one is a round-trip stale by the time it arrives.
    %cam = MRPG_GetCameraTransform();

    if(%cam $= "")
        %cam = %eyeTransform;

    %screenPos = WorldToScreen(%cam, %enemyPos);

    if (%screenPos !$= "")
    {
        //worldToScreen returns "x y depth" - position takes only x and y.
        MonsterRPGx_HitMarker.position = getWords(%screenPos, 0, 1);
        
        cancel(MonsterRPGx_HitMarker.hideSch);
        MonsterRPGx_HitMarker.setVisible(true);
        MonsterRPGx_HitMarker.hideSch = MonsterRPGx_HitMarker.schedule(100, setVisible, false);
    }
}

function clientCmdResetChunkedFields()
{
	$MonsterRPG::Client::NumberOfChunks = 0;
}

function clientCmdSendChunkedFields(%chunk,%number)
{
	$MonsterRPG::Client::RecentChunk[%number] = %chunk;
	$MonsterRPG::Client::NumberOfChunks++;
}

