if (isPackage("MonsterRPGxPackage_Client"))
{
	deactivatePackage("MonsterRPGxPackage_Client");
}

package MonsterRPGxPackage_Client
{
	
	function disconnect(%bool)
	{
		//ONLY IF WE WERE ON A MonsterRPG SERVER. Everything below pokes named
		//controls in MonsterRPGx_Containers.gui and calls MonsterRPGx_ helpers, and
		//it used to run on EVERY disconnect from EVERY server - so a player who
		//never touched MonsterRPG paid for a full crafting-window reset each time
		//they left a game, with a console error for anything that had not been
		//built yet.
		if(!$MonsterRPG::Client::inMonsterRPGServer)
			return parent::disconnect(%bool);

		MonsterRPGx_DraftBtnFuncs("Reset","ItemCraft");
		MonsterRPGx_DraftBtnFuncs("Reset","GenCraft");
		MonsterRPGx_DraftBtnFuncs("Reset","GridForm");
		MonsterRPGx_DraftBtnFuncs("Reset","Crucible");
		
		CLIENTCMDMonsterRPGx_ReceiveDraftData("ItemCraft","Reset","");
		CLIENTCMDMonsterRPGx_ReceiveDraftData("GenCraft","Reset","");
		CLIENTCMDMonsterRPGx_ReceiveDraftData("GridForm","Reset","");
		CLIENTCMDMonsterRPGx_ReceiveDraftData("Crucible","Reset","");
		CLIENTCMDMonsterRPGx_ReceiveDraftData("MenuData","","Reset");
		
		for(%c = 0; %c < 4; %c++)
		{
			%menuA = "MonsterRPGx_DraftWndw_AlSel" @ %c;
			%menuR = "MonsterRPGx_DraftWndw_RtSel" @ %c;
			%sw = "MonsterRPGx_DraftWndw_AlSw" @ %c;
			
			%menuA.clear();
			%menuR.clear();
			%sw.setColor(getField("27 57 130 220" TAB "47 77 150 220" TAB "67 87 170 220" TAB "87 107 180 220",%c));
			%sw.resize(getWord("0 32 64 96",%c),127,32,1);
		}
		
		MonsterRPGx_DraftWndw_ItemSelWndw.rtnBtn = "";
		MonsterRPGx_DraftWndw_ItemSelWndw.menuType = "";
		MonsterRPGx_DraftWndw_ItemSelWndw.setVisible(0);
		
		MonsterRPGx_RecipeMngmt.initSetup = false;
		
		return parent::disconnect(%bool);
	}
	
	//function PlayGui::onWake(%this)
	//{
	//	Parent::onWake(%this);
	//}
	
	
	//function onChatMessage (%message, %voice, %pitch)
    //{
	//    %parent = parent::onChatMessage(%message, %voice, %pitch);
	//	
	//	if($MonsterRPG::Client::Server == 1)
	//	{
	//        if (strlen (%message) > 0)
	//        {
	//	        newChatHud_AddLine ("<font:arial bold:14>" @ %message);
	//        }
	//	}
	//	else
	//	{
	//	  return %parent;
	//	}
    //}
	
	//Lets just package and use this function to send all the player data for efficiency
	
	function clientCmdBottomPrint(%message, %time, %showBox, %data)
	{
		%firstWord = getWord(%message,0);
		if(%firstWord $= "MonsterRPGPlayerData")
		{		    
			////////////////////
			///MAIN HUD STUFF///
			////////////////////
			
			
			$MonsterRPG::Client::Level = getField(%data,9);
			$MonsterRPG::Client::Exp = getField(%data,7);
			$MonsterRPG::Client::MaxExp = getField(%data,8);
			$MonsterRPG::Client::Gold = getField(%data,6);
			$MonsterRPG::Client::MaxHP = getField(%data,16);
			$MonsterRPG::Client::MaxMana = getField(%data,18);
			$MonsterRPG::Client::Damage = getField(%data,17);
			
			//MonsterRPGx_Inventory_Gold.setText("<font:verdana bold:14><color:FFFF00>Gold: " @ $MonsterRPG::Client::Gold);
			
			
			
			
			///////////////////
			///RPG HUD STUFF///
			///////////////////
			
			MonsterRPGx_MAIN_INTERFACE.setVisible(true);
			
			//HP
			%HPPercent = getField(%data,0);
			%health = getField(%data,1);
			%maxhealth = getField(%data,2);
			%hptxt = %health @ "/" @ %maxhealth;
			
			
			///Mana
			%MPPercent = getField(%data,3);
			%mana = getField(%data,4);
			%maxmana = getField(%data,5);
			%mptxt = %mana @ "/" @ %maxmana;
			
			
			//ExP
			%EXPPercent = getField(%data,7);
			%exp = getField(%data,8);
			%maxexp = getField(%data,9);
			%exptxt = %exp @ "/" @ %maxexp;
			
			
			//Level
			%lvltxt = getField(%data,10);
			
			%col = "<color:FF3300>";
			
			%time = 1;
			
			
			
			//MonsterRPGx_PlayerName1.setText("<just:center><font:" @ $Pref::Client::MonsterRPGx::HUD_Font @ ":25>\c1" @ strUpr(%boss) @ " - " @ %name);
			//MonsterRPGx_PlayerHealth1.setPercent(%HPPercent, MonsterRPGx_PlayerHealthDmg1);
			////MonsterRPGx_PlayerMana1.setPercent(%MPPercent, MonsterRPGx_PlayerManaDmg1);
			///MonsterRPGx_PlayerEXP1.setPercent(%EXPPercent, MonsterRPGx_PlayerEXPInc1);
			//MonsterRPGx_PlayerArmor1.setPercent(0, MonsterRPGx_PlayerArmorDmg1);
			
			
			///ExP TEXT
			
			//MP TEXT
			//MonsterRPGx_PlayerEXPText1.setText("<just:center><font:verdana bold:14>" @ %col @ %exptxt);
			//cancel(MonsterRPGx_PlayerEXPText1.textBlinkSch);
			//MonsterRPGx_PlayerEXPText1.textBlinkSch = MonsterRPGx_PlayerEXPText1.schedule(200, setText, "<just:center><font:verdana bold:14>" @ %exptxt);
			
			//cancel(MonsterRPGx_PlayerEXPText1.textSch);
			//MonsterRPGx_PlayerEXPText1.textSch = MonsterRPGx_PlayerEXPText1.schedule(1000 * %time, setText, "<just:center><font:verdana bold:14><color:B3B3B3>" @ %exptxt);
			
			
			///LEVEL TEXT
			//MonsterRPGx_PlayerLevelText1.setText("<just:center><font:verdana bold:23>\c5Lvl. " @ %lvltxt);
			//cancel(MonsterRPGx_PlayerLevelText1.textBlinkSch);
			//MonsterRPGx_PlayerLevelText1.textBlinkSch = MonsterRPGx_PlayerLevelText1.schedule(200, setText, "<just:center><font:verdana bold:14>" @ %exptxt);
			
			//cancel(MonsterRPGx_PlayerLevelText1.textSch);
			//MonsterRPGx_PlayerLevelText1.textSch = MonsterRPGx_PlayerLevelText1.schedule(1000 * %time, setText, "<just:center><font:verdana bold:23><color:B3B3B3>\c5Lvl. " @ %lvltxt);
			
			///
			
			
			///Stat display texts TEXT
			//MonsterRPGx_PlayerLevelText1.setText("<just:center><font:verdana bold:23>\c5Lvl. " @ %lvltxt);
			//cancel(MonsterRPGx_PlayerLevelText1.textBlinkSch);
			//MonsterRPGx_PlayerLevelText1.textBlinkSch = MonsterRPGx_PlayerLevelText1.schedule(200, setText, "<just:center><font:verdana bold:14>" @ %exptxt);
			
			//cancel(MonsterRPGx_PlayerLevelText1.textSch);
			//MonsterRPGx_PlayerLevelText1.textSch = MonsterRPGx_PlayerLevelText1.schedule(1000 * %time, setText, "<just:center><font:verdana bold:23><color:B3B3B3>\c5Lvl. " @ %lvltxt);

			// Usage
			setFormattedText(MonsterRPGx_StatValue1, "Level: " @ $MonsterRPG::Client::Level);
			setFormattedText(MonsterRPGx_StatValue2, "Damage: " @ $MonsterRPG::Client::Damage);
			setFormattedText(MonsterRPGx_StatValue3, "Mana: " @ $MonsterRPG::Client::MaxMana);
			setFormattedText(MonsterRPGx_StatValue4, "Health: " @ $MonsterRPG::Client::MaxHP);
			setFormattedText(MonsterRPGx_StatValue5, "Gold: " @ $MonsterRPG::Client::Gold);
			
			// RESIZES
			%resYScale = getRPGuiScale();
			
			if(%resYScale $= "4k")
			{
				Canvas.setCursor("MonsterRPGCursor4k");
			}
			else if(%resYScale $= "Standard")
			{
				Canvas.setCursor("MonsterRPGCursorStandard");
			}
			else if(%resYScale $= "Small")
			{
				Canvas.setCursor("MonsterRPGCursorSmall");
			}
			
			%res = getRes();
	    	%screenWidth = getField (%res, 0);
	    	%screenHeight = getField (%res, 1);
			%bottom = 0;
			%ExtAdjust = %screenHeight / 768;
			
			updateHPBitmap(mCeil(%HPPercent));
			updateManaBitmap(mCeil(%MPPercent));
			updateExpBitmap(mFloor(%EXPPercent));
			
			if(!$MonsterRPG::Client::GotHit && $MonsterRPG::Client::lastHitAngle >= 15 && $MonsterRPG::Client::lastHitAngle <= 360)
			{
				$MonsterRPG::Client::GotHit = 1;
				Damage_Indicator.setBitmap("Add-ons/Client_MonsterRPG/GUIs/Damage_Indicators/HitIndicator" @ $MonsterRPG::Client::lastHitAngle @ ".png");
				Damage_Indicator.UIS_applyScaling(%ExtAdjust + 2, getField(getRes(), 0) / 2 SPC getField(getRes(), 1));
				
				// Get the extent of the damage indicator bitmap
				%DIExt = Damage_Indicator.getExtent();
				%DIWidth = getField(%DIExt, 0);
				%DIHeight = getField(%DIExt, 1);
				
				%CPosX = getField(Crosshair.getPosition(),0);
				%CPosY = getField(Crosshair.getPosition(),1);
				
				Damage_Indicator.center();
				Damage_Indicator.setVisible(1);
				
				cancel(Damage_Indicator.resetHitsch);
				Damage_Indicator.resetHitsch = Damage_Indicator.schedule(2000,resetHits);
			}
			return;
		}
		
		return Parent::clientCmdBottomPrint(%message, %time, %showBox, %data);
	}
	
	function ToggleZoom(%toggle)
	{
		parent::ToggleZoom(%toggle);
		$DynamicFOV::Zooming = %toggle;
	}
	
	function resetCanvas(%a0, %a1, %a2, %a3, %a4, %a5, %a6)
 	{
	    if($MonsterRPG::Client::inMonsterRPGServer == 1)
		{
			cancel(MonsterRPGx_MAIN_INTERFACE.canvasRescale);
			MonsterRPGx_MAIN_INTERFACE.canvasRescale = schedule(100,0,scaleNewCanvas,MonsterRPGx_MAIN_INTERFACE);

			//The character screen is laid out against a fixed 1024x768 design box and
			//re-scaled to fit, so a resolution change has to re-run that or it stays
			//sized for the OLD screen - which at a big jump means a panel hanging off
			//the edge. Same 100ms delay as the HUD: getRes() does not report the new
			//size until the canvas has actually finished resetting.
			//
			//Unconditional on the dialog existing rather than on it being OPEN: the
			//screen is built once and kept, so a resize while it is closed would
			//otherwise be missed entirely and only show up the next time it opened.
			if(isObject(CS_Frame))
			{
				cancel($CS_LayoutSch);
				$CS_LayoutSch = schedule(100,0,CS_layout);
			}
		}

		return parent::resetCanvas(%a0, %a1, %a2, %a3, %a4, %a5, %a6);
	}
	
	function PlayGui::createToolHUD(%this)
    {
	    if($MonsterRPG::Client::inMonsterRPGServer == 1)
		{
			%this.createNewToolHUD();
		}
		else
		{
			return parent::createToolHUD(%this);
		}
	}
	
	function useTools(%val)
    {
		if($MonsterRPG::Client::inMonsterRPGServer == 1)
		{
			useNewTools(%val);
		}
		else
		{
			return parent::useTools(%val);
		}
	}
	
	function clientCmdSetActiveTool(%slot)
    {
		if($MonsterRPG::Client::inMonsterRPGServer == 1)
		{
			clientCmdNewSetActiveTool(%slot);
		}
		else
		{
			return parent::clientCmdSetActiveTool(%slot);
		}
	}
	
	function setActiveTool(%index)
	{
		if($MonsterRPG::Client::inMonsterRPGServer == 1)
		{
			setNewActiveTool(%index);
		}
		else
		{
			return parent::setActiveTool(%index);
		}
	}
	
	function clientCmdSetVignette(%multiply,%color,%key)
	{
	    if($MonsterRPG::Client::inMonsterRPGServer == 1)
		{
		    if(%key == 1)
			{
				if (%color $= "")
				{
					error ("ERROR: clientCmdSetVignette(" @ %multiply @ ", " @ %color @ ") - null color");
					return;
				}
				%multiply = mClamp (%multiply, 0, 1);
				%color = getColorF (%color);
				PlayGui_Vignette.mMultiply = %multiply;
		     	PlayGui_Vignette.setColor (%color);
				NoHudGui_Vignette.mMultiply = %multiply;
				NoHudGui_Vignette.setColor (%color);
			}
		}
		else
		{
		    return parent::clientCmdSetVignette(%multiply,%color);
		}
	}
};
activatePackage(MonsterRPGxPackage_Client);

package xRPGAbilities
{
    function OptionsDlg::onSleep(%a)
    {
        Parent::onSleep(%a);
	}
    
    function PlayGui::onRender(%t)
    {
        Parent::onRender(%t);
	}
    
    function resetCanvas()
    {
        Parent::resetCanvas();
		
	}
    
    function useSprayCan(%a)
    {
        if($MonsterRPG::Client::Server == 1 && $Pref::Client::MonsterRPGx::AllowBuilding != 1)
        {
            //%ability = HUD_AbsActive.ability;
            
			// commandToServer('UseAbility', %ability, %a);
            
            //moveRun(%a);
            
            return;
		}
        
        Parent::useSprayCan(%a);
	}
    
    function useBricks(%x)
    {
        if($MonsterRPG::Client::Server == 1 && $Pref::Client::MonsterRPGx::AllowBuilding != 1 && %x == 1)
        {
            //commandToServer('UseAbility', 0, %x);
            
            return;
		}
        
        Parent::useBricks(%x);
	}
    
    function useFirstSlot(%x)
    {
        if($MonsterRPG::Client::Server == 1 && $Pref::Client::MonsterRPGx::AllowBuilding != 1)
        {
            //commandToServer('UseAbility', 0, %x);
            
            return;
		}
        
        Parent::useFirstSlot(%x);
	}
    
    function useSecondSlot(%x)
    {
        if($MonsterRPG::Client::Server == 1 && $Pref::Client::MonsterRPGx::AllowBuilding != 1)
        {
			// commandToServer('UseAbility', 1, %x);
            
            return;
		}
        
        Parent::useSecondSlot(%x);
	}
    
    function useThirdSlot(%x)
    {
        if($MonsterRPG::Client::Server == 1 && $Pref::Client::MonsterRPGx::AllowBuilding != 1)
        {
            //commandToServer('UseAbility', 2, %x);
            
            return;
		}
        
        Parent::useThirdSlot(%x);
	}
    
    function useFourthSlot(%x)
    {
        if($MonsterRPG::Client::Server == 1 && $Pref::Client::MonsterRPGx::AllowBuilding != 1)
        {
			// commandToServer('UseAbility', 3, %x);
            
            return;
		}
        
        Parent::useFourthSlot(%x);
	}
    
    function useFifthSlot(%x)
    {
        if($MonsterRPG::Client::Server == 1 && $Pref::Client::MonsterRPGx::AllowBuilding != 1)
        {
            //commandToServer('UseAbility', 4, %x);
            
            return;
		}
        
        Parent::useFifthSlot(%x);
	}
    
    function useSixthSlot(%x)
    {
        if($MonsterRPG::Client::Server == 1 && $Pref::Client::MonsterRPGx::AllowBuilding != 1)
        {
            //commandToServer('UseAbility', 5, %x);
            
            return;
		}
        
        Parent::useSixthSlot(%x);
	}
    
    function useSeventhSlot(%x)
    {
        if($MonsterRPG::Client::Server == 1 && $Pref::Client::MonsterRPGx::AllowBuilding != 1)
        {
            //commandToServer('UseAbility', 6, %x);
            
            return;
		}
        
        Parent::useSeventhSlot(%x);
	}
    
    function useEighthSlot(%x)
    {
        if($MonsterRPG::Client::Server == 1 && $Pref::Client::MonsterRPGx::AllowBuilding != 1)
        {
            //commandToServer('UseAbility', 7, %x);
            
            return;
		}
        
        Parent::useEighthSlot(%x);
	}
    
    function useNinthSlot(%x)
    {
        if($MonsterRPG::Client::Server == 1 && $Pref::Client::MonsterRPGx::AllowBuilding != 1)
        {
            //commandToServer('UseAbility', 8, %x);
            
            return;
		}
        
        Parent::useNinthSlot(%x);
	}
    
    function useTenthSlot(%x)
    {
        if($MonsterRPG::Client::Server == 1 && $Pref::Client::MonsterRPGx::AllowBuilding != 1)
        {
            //commandToServer('UseAbility', 9, %x);
            
            return;
		}
        
        Parent::useTenthSlot(%x);
	}
};

activatepackage(MonsterRPGxAbilities);

//////////////////////////////////////////////////////////////////////////////
// THE VISIBLE HALF OF LEAVING
//
// ONE BODY, THREE HOOKS. This used to be the same forty lines copy-pasted into
// disconnect, disconnectedCleanup and onExit - which is how it drifted: all three
// wrote $MonsterRPG::Client::inMonsterRPGServer = 0 by hand, none of them stopped
// the minimap, the camp panel or the item-tip poll, and every one of them ran on
// servers that had never sent a MonsterRPG command.
//
// The flag and the schedules belong to MRPG_ClientLeave (ServerGate.cs) now. What
// is left here is exactly what that function should not own: putting the stock
// HUD's own appearance back.
//////////////////////////////////////////////////////////////////////////////
function MonsterRPGx_RestoreStockHud()
{
	//Guarded, because these are OUR controls and OUR profile swap - on a client
	//that never joined a MonsterRPG server there is nothing to undo, and calling
	//setVisible on a control the .gui failed to create is a console error per
	//disconnect.
	if(isObject(Canvas))
		Canvas.setCursor("DefaultCursor");

	if(isObject(MonsterRPGx_HUD))
		MonsterRPGx_HUD.setVisible(0);
	if(isObject(MonsterRPGx_MAIN_INTERFACE))
		MonsterRPGx_MAIN_INTERFACE.setVisible(0);
	if(isObject(Damage_Indicator))
		Damage_Indicator.setVisible(0);

	if(isObject(PlayGui_Vignette))
		PlayGui_Vignette.setBitmap("base/client/ui/vignette.png");
	if(isObject(PlayGui_ShapeNameHud))
		PlayGui_ShapeNameHud.profile = "BlockChatTextProfile";

	$MonsterRPG::Client::SetHPBitmap   = 0;
	$MonsterRPG::Client::SetManaBitmap = 0;
	$MonsterRPG::Client::SetExpBitmap  = 0;

	//The exp bar's animation state is a FRACTION now, and it is what the next server's
	//first update animates FROM. Left set, joining a second server would sweep the bar
	//down from the previous character's progress instead of starting at the new one's.
	//"" is the "no value seen yet" case updateExpBitmap tests for, so clear rather than
	//zero - zero would sweep up from empty, which is the same bug wearing a hat.
	$MonsterRPG::Client::ExpFrac     = "";
	$MonsterRPG::Client::ExpFadeFrac = "";
	$MonsterRPG::Client::ExpTarget   = "";
}

// ONE CALL, AND IT MUST NOT TEST THE FLAGS ITSELF.
//
// MRPG_ClientLeave is what clears $MonsterRPG::Client::inMonsterRPGServer, and
// MRPGServerGate is activated LAST (client.cs) so it is the outermost
// disconnectedCleanup wrapper - on a dropped connection it has already run by the
// time Parent:: reaches this package. A `if(!$MonsterRPG::Client::inMonsterRPGServer)
// return;` here would therefore skip the stock-HUD restore on exactly the paths
// that need it most.
//
// So the "did any of this ever run?" test lives inside MRPG_ClientLeave, once, and
// MonsterRPGx_ClearData / MonsterRPGx_RestoreStockHud are called from there. This
// is just the entry point for the three hooks below; it is idempotent because
// MRPG_ClientLeave is.
function MonsterRPGx_LeaveServer()
{
	MRPG_ClientLeave();
}

package MonsterRPGx_TempClient
{
	function disconnect(%a)
	{
		MonsterRPGx_LeaveServer();
		return Parent::disconnect(%a);
	}

	function disconnectedCleanup(%this)
	{
		%r = Parent::disconnectedCleanup(%this);
		MonsterRPGx_LeaveServer();
		return %r;
	}

	function onExit()
	{
		MonsterRPGx_LeaveServer();
		Parent::onExit();
	}
};

activatePackage(MonsterRPGx_TempClient);