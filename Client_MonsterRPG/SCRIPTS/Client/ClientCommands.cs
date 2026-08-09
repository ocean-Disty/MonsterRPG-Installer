function CLIENTCMDMonsterRPGx_VerSetup()
{
	commandToServer('MonsterRPGx_ReceiveVer',"0.4.0Beta");
}

function clientCmdMRPGShowInspectData(%data)
{
	%Type = getField(%data,0);
	%Name = getField(%data,1);
	%Level = getField(%data,2);
	%Rarity = getField(%data,3);
	
	if(%Type $= "Armor")
	{
		%BaseArmor = getField(%data,4);
		%SpeedMulti = getField(%data,5);
		%BonusArmor = getField(%data,6);
		%TotalArmor = getField(%data,7);
		%Specials = getField(%data,8);
		%InspectCount = 8;
	}
	else if(%Type $= "Weapon")
	{
		%WeakRdsDmg = getField(%data,4);
		%StrongRdsDmg = getField(%data,5);
		%WeakDirDmg = getField(%data,6);
		%StrongDirDmg = getField(%data,7);
		%ShortAttRng = getField(%data,8);
		%FarAttRng = getField(%data,9);
		%WeakDmgScale = getField(%data,10);
		%StrongDmgScale = getField(%data,11);
		%BonusDmg = getField(%data,12);
		%hasRadius = getField(%data,13);
		%isRanged = getField(%data,14);
		%Special = getField(%data,15);
		%SpecialLevel = getField(%data,16);
		%InspectCount = 16;
	}
	
	%ERROR = -1;
	if(%Level > 0)
	{
	    %LevelDis = 0;
		%RarityDis = 0;
		
		switch(%Rarity)
		{
			case "Common": %rColor = "<color:444444>";
			case "UnCommon": %rColor = "<color:32CD32>";
			case "Rare": %rColor = "<color:1644FC>";
			case "Epic": %rColor = "<color:FF00FF>";
			case "Legendary": %rColor = "<color:FFBE00>";
			case "Mythic": %rColor = "<color:FF5349>";
		}
		
		%LevelDis = "<color:0047AB>Level: <color:008081>" @ %Level;
		%RarityDis = "<color:0047AB>Rarity: " @ %rColor @ %Rarity @ "<color:0047AB>";
		
		if(%Type $= "Armor")
		{
		}
		else if(%Type $= "Weapon")
		{
			%RadiusDis = 0;
			%DirectDmgDis = 0;
			%AttackRngDis = 0;
			%BaseDmgDis = 0;
			%BonusDmgDis = 0;
		
			if(%hasRadius)
			{
				%RadiusDis = "<color:0047AB>Radius Damage: <color:008081>" @  %WeakRdsDmg @ "-" @ %StrongRdsDmg;
			}
			else
			{
				%RadiusDis = "";
			}
			
			if(%BonusDmg > 0) 
			{
				%BonusDmgDis =  "<color:0047AB>Bonus Damage: <color:008081>" @ %BonusDmg;
				%StrongTotalDamage += %BonusDmg;
				%WeakTotalDamage += %BonusDmg;
			}
			else
			{
				%BonusDmgDis = "";
			}
			
			if(!%isRanged)
			{
				%DirectDmgDis = "<color:0047AB>Direct Damage: <color:008081>" @ %WeakDirDmg @ "-" @ %StrongDirDmg ;
				%AttackRngDis = "<color:0047AB>Attack Range: <color:008081>" @ %ShortAttRng @ "-" @ %FarAttRng;
				
				%StrongTotalDamage += %StrongDirDmg;
				%WeakTotalDamage += %WeakDirDmg;
				
				%TotalDmgDIs = "<color:0047AB>Total Damage: <color:008081>" @ %WeakTotalDamage @ "-" @ %StrongTotalDamage ;
			}
			else
			{
				%DirectDmgDis = "<color:0047AB>Direct Damage: <color:008081>" @  %WeakDirDmg;
				%AttackRngDis = "<color:0047AB>Attack Range: <color:008081>" @ %FarAttRng;
				
				%WeakTotalDamage += %WeakDirDmg;
				
				%TotalDmgDIs = "<color:0047AB>Total Damage: <color:008081>" @ %WeakTotalDamage;
			}
		}
		else
		{
		}
		
		//%specialCount = 0;
		//%count = getSpecialCount(%player);
		//for(%i=0; %i < %count; %i++)
		//{
		//	if(%profile.var["Special",%i,%slot] !$= "" && isString(%profile.var["Special",%i,%slot]))
		//	{
		//	    %specialCount++;
		//	    %SpecialDis = %SpecialDis @ "<br>(" @ %profile.var["Special",%i,%slot] @ " Level): " @ %profile.var["SpecialLvl",%i,%slot];
		//	}
		//}
		
		if(%Type $= "Armor")
		{
		    %data = %LevelDis TAB %RarityDis TAB %BaseArmrDis TAB %SpeedMultiDis TAB %BonusArmrDis TAB %TotalArmrDis TAB %SpecialDis;
			SplitDisplayData(%Name,%data);
		}
		else if(%Type $= "Weapon")
		{
			%data = %LevelDis TAB %RarityDis TAB %RadiusDis TAB %TotalDmgDIs TAB %DirectDmgDis TAB %AttackRngDis TAB %BaseDmgDis TAB %BonusDmgDis TAB %SpecialDis;
			SplitDisplayData(%name,%data);
		}
	}
	else
	{
	    %ERROR = 1;
		msgClient(%client, '', "Item data can't be fetched");
	}
}


function SplitDisplayData(%name,%data)
{
	%LevelDis = getField(%data,0);
	%RarityDis = getField(%data,1);
	%RadiusDis = getField(%data,2);
	%TotalDmgDIs = getField(%data,3);
	%DirectDmgDis = getField(%data,4);
	%AttackRngDis = getField(%data,5);
	%BaseDmgDis = getField(%data,6);
	%BonusDmgDis = getField(%data,7);
	%SpecialDis = getField(%data,8);
	
	%string = "<font:verdana bold:22><just:left>" @ %LevelDis @ "\n" @ %RarityDis @ "\n" @ %RadiusDis @ "\n" @ %TotalDmgDis @ "\n" @ %DirectDmgDis @ "\n" @ %BonusDmgDis @ "\n" @ %AttackRngDis @ "\n" @ %SpecialDis;
	
	MessageBoxOKBG(%name,%string);
}



function CLIENTCMDMonsterRPGxUpdateRPGLeaderboard(%data)
{
    // Make sure the store object exists (it is written/read by field below).
    if(!isObject($MonsterRPG::Client::RPGData))
        $MonsterRPG::Client::RPGData = new ScriptObject();

    // Extract each leaderboard variable using getField
    $MonsterRPG::Client::RPGData.var["TopRebirthName"] = getField(%data, 0);
    $MonsterRPG::Client::RPGData.var["TopLevelName"] = getField(%data, 1);
    $MonsterRPG::Client::RPGData.var["TopHarvestName"] = getField(%data, 2);
    $MonsterRPG::Client::RPGData.var["TopFishermanName"] = getField(%data, 3);
    $MonsterRPG::Client::RPGData.var["TopMKName"] = getField(%data, 4);
    $MonsterRPG::Client::RPGData.var["TopPKName"] = getField(%data, 5);
    $MonsterRPG::Client::RPGData.var["LowKarmaName"] = getField(%data, 6);
    $MonsterRPG::Client::RPGData.var["HighKarmaName"] = getField(%data, 7);
    $MonsterRPG::Client::RPGData.var["LowKarmaTitle"] = getField(%data, 8);
    $MonsterRPG::Client::RPGData.var["HighKarmaTitle"] = getField(%data, 9);
    $MonsterRPG::Client::RPGData.var["MostDeathsName"] = getField(%data, 10);
    $MonsterRPG::Client::RPGData.var["MostPlayedTimeName"] = getField(%data, 11);
    $MonsterRPG::Client::RPGData.var["MostQuestsCompletedName"] = getField(%data, 12);
    $MonsterRPG::Client::RPGData.var["MostBountiesCompletedName"] = getField(%data, 13);
    $MonsterRPG::Client::RPGData.var["MostFamousName"] = getField(%data, 14);

    // Array of leaderboard categories and corresponding names
    %leaderboardCategories[0] = "Top Rebirth";
    %leaderboardCategories[1] = "Top Level";
    %leaderboardCategories[2] = "Top Harvest";
    %leaderboardCategories[3] = "Top Fisherman";
    %leaderboardCategories[4] = "Monster Kills";
    %leaderboardCategories[5] = "Player Kills";
    %leaderboardCategories[6] = "Lowest Karma";
    %leaderboardCategories[7] = "Highest Karma";
    %leaderboardCategories[8] = "Most Deaths";
    %leaderboardCategories[9] = "Most Playtime";
    %leaderboardCategories[10] = "Most Quests";
    %leaderboardCategories[11] = "Most Bounties";
    %leaderboardCategories[12] = "Most Famous";
    
    // Corresponding variables for leaderboard names
    %leaderboardNames[0] = $MonsterRPG::Client::RPGData.var["TopRebirthName"];
    %leaderboardNames[1] = $MonsterRPG::Client::RPGData.var["TopLevelName"];
    %leaderboardNames[2] = $MonsterRPG::Client::RPGData.var["TopHarvestName"];
    %leaderboardNames[3] = $MonsterRPG::Client::RPGData.var["TopFishermanName"];
    %leaderboardNames[4] = $MonsterRPG::Client::RPGData.var["TopMKName"];
    %leaderboardNames[5] = $MonsterRPG::Client::RPGData.var["TopPKName"];
    %leaderboardNames[6] = $MonsterRPG::Client::RPGData.var["LowKarmaName"];
    %leaderboardNames[7] = $MonsterRPG::Client::RPGData.var["HighKarmaName"];
    %leaderboardNames[8] = $MonsterRPG::Client::RPGData.var["MostDeathsName"];
    %leaderboardNames[9] = $MonsterRPG::Client::RPGData.var["MostPlayedTimeName"];
    %leaderboardNames[10] = $MonsterRPG::Client::RPGData.var["MostQuestsCompletedName"];
    %leaderboardNames[11] = $MonsterRPG::Client::RPGData.var["MostBountiesCompletedName"];
    %leaderboardNames[12] = $MonsterRPG::Client::RPGData.var["MostFamousName"];

    // Assign the text to MonsterRPGx_Leaderboard1 to MonsterRPGx_Leaderboard12, skipping "N/A" or empty names
	%displayCount = 0;
	for (%i = 0; %i < 13 && %displayCount < 12; %i++) 
	{
    	%selectedIndex = %i;
    	%name = %leaderboardNames[%selectedIndex];
    
     // Only display if the name is not "N/A" and not empty
     if (%name !$= "N/A" && %name !$= "") 
	 {
        %leaderboardText = %leaderboardCategories[%selectedIndex] @ ": " @ %name;
        
        // Set the text for each leaderboard GUI element
        %controlName = "MonsterRPGx_Leaderboard" @ (%displayCount + 1);
        %control = eval("return " @ %controlName @ ";");
        if (isObject(%control))
        {
            %control.setText("<color:FFFFFF>" @ %leaderboardText);
        }
        else
        {
            echo("Control not found: " @ %controlName);
        }
        
        	// Increment the display count
       	 %displayCount++;
     }
	}	

	// Clear remaining leaderboard elements if less than 12 valid names
	for (%i = %displayCount; %i < 12; %i++) 
	{
    	("MonsterRPGx_Leaderboard" @ (%i + 1)).setText("");
	}


    // Optionally store or display the leaderboard type text for UI or debugging
    $MonsterRPG::Client::CurrentLeaderboardType = "Filtered Leaderboard Display";

    // Keep the new styled Leaderboard tab (RPGPanels.cs, on the Stats swatch)
    // live while it is open.
    if(isObject(MonsterRPGx_Stats) && MonsterRPGx_Stats.isVisible())
        MRPG_renderLeaderboard();
}

// The leaderboard's numeric values arrive in a separate message (see Core_HUD.cs).
// Stored into the same RPGData.var the hover tooltip reads live (MRPG_buildLeaderDetail).
function CLIENTCMDMonsterRPGxUpdateRPGLeaderboardVals(%data)
{
    if(!isObject($MonsterRPG::Client::RPGData))
        $MonsterRPG::Client::RPGData = new ScriptObject();

    $MonsterRPG::Client::RPGData.var["TopRebirth"] = getField(%data, 0);
    $MonsterRPG::Client::RPGData.var["TopLevel"] = getField(%data, 1);
    $MonsterRPG::Client::RPGData.var["TopHarvest"] = getField(%data, 2);
    $MonsterRPG::Client::RPGData.var["TopFishcaught"] = getField(%data, 3);
    $MonsterRPG::Client::RPGData.var["TopMK"] = getField(%data, 4);
    $MonsterRPG::Client::RPGData.var["TopPK"] = getField(%data, 5);
    $MonsterRPG::Client::RPGData.var["LowKarma"] = getField(%data, 6);
    $MonsterRPG::Client::RPGData.var["HighKarma"] = getField(%data, 7);
    $MonsterRPG::Client::RPGData.var["MostDeaths"] = getField(%data, 8);
    $MonsterRPG::Client::RPGData.var["MostPlayedTime"] = getField(%data, 9);
    $MonsterRPG::Client::RPGData.var["MostQuestsCompleted"] = getField(%data, 10);
    $MonsterRPG::Client::RPGData.var["MostBountiesCompleted"] = getField(%data, 11);
    $MonsterRPG::Client::RPGData.var["TopFame"] = getField(%data, 12);
}

function formatSkillPathForDisplay(%path)
{
    %parts = strReplace(%path, "{}", " ");
    %reversedParts = "";
    for (%i = getWordCount(%parts) - 1; %i >= 0; %i--)
    {
        %reversedParts = %reversedParts SPC getWord(%parts, %i);
    }
    return trim(%reversedParts);
}

function clientCmdMonsterRPGxUpdateRPGSkills(%data)
{
    if($MRPGDEBUG) echo("DEBUG: Received skill data: " @ %data);

    %pipePos = strpos(%data, "|");
    if (%pipePos == -1)
    {
        // gatherClientSkillData returns "<count><|entry><|entry>...", so a
        // profile with NO skills yields a bare "0" with no pipe at all. That is
        // a normal state, not malformed data - and since MonsterRPGSkillsUpdate
        // re-sends every 341ms it was spamming this error several times a
        // second for any player who had not unlocked a skill yet.
        // Clear the list and leave quietly; only genuinely odd payloads error.
        if (%data $= "" || %data == 0)
        {
            for (%i = 1; %i <= 12; %i++)
            {
                %skillNameObj = "MonsterRPGx_Skills" @ %i;
                %skillBarObj = "MonsterRPGx_SkillBar" @ %i;
                if (isObject(%skillNameObj))
                    %skillNameObj.setText("");
                if (isObject(%skillBarObj))
                    %skillBarObj.setVisible(false);
            }
            return;
        }

        error("ERROR: Invalid skill data format received: " @ %data);
        return;
    }

    %skillCount = getSubStr(%data, 0, %pipePos);
    %skillData = getSubStr(%data, %pipePos + 1, strlen(%data) - %pipePos - 1);

    if($MRPGDEBUG) echo("DEBUG: Parsed skill count: " @ %skillCount);
    if($MRPGDEBUG) echo("DEBUG: Parsed skill data: " @ %skillData);

    // Clear existing skill list
    for (%i = 1; %i <= 12; %i++)
    {
        %skillNameObj = "MonsterRPGx_Skills" @ %i;
        %skillBarObj = "MonsterRPGx_SkillBar" @ %i;
        if (isObject(%skillNameObj))
            %skillNameObj.setText("");
        if (isObject(%skillBarObj))
            %skillBarObj.setVisible(false);
    }

    %skillDataArray = strReplace(%skillData, "|", "\t");
    for (%i = 0; %i < %skillCount; %i++)
    {
        %skillInfo = getField(%skillDataArray, %i);
        if($MRPGDEBUG) echo("DEBUG: Processing skill info: " @ %skillInfo);

        %skillPath = getWordd(%skillInfo, 0, ",");
        %skillName = getWordd(%skillInfo, 1, ",");
        %level = getWordd(%skillInfo, 2, ",");
        %exp = getWordd(%skillInfo, 3, ",");
        %maxExp = getWordd(%skillInfo, 4, ",");
        %depth = getWordd(%skillInfo, 5, ",");

        if (%skillName !$= "")
        {
            $MonsterRPG::Client::Skill[%skillPath, "Name"] = %skillName;
            $MonsterRPG::Client::Skill[%skillPath, "Level"] = %level;
            $MonsterRPG::Client::Skill[%skillPath, "Exp"] = %exp;
            $MonsterRPG::Client::Skill[%skillPath, "MaxExp"] = %maxExp;
            $MonsterRPG::Client::Skill[%skillPath, "Depth"] = %depth;

            %expPercent = 0;
            if (%maxExp > 0)
            {
                %expPercent = mFloor((%exp / %maxExp) * 100);
            }
            updateSkillInfo(%skillPath, %skillName, %level, %exp, %maxExp, %expPercent, %depth, %i + 1);

            if($MRPGDEBUG) echo("DEBUG: Processed skill - Path: " @ %skillPath @ ", Name: " @ %skillName @ ", Level: " @ %level @ ", Exp: " @ %exp @ "/" @ %maxExp @ ", Depth: " @ %depth);
        }
    }
}

function getWordd(%string, %index, %delimiter)
{
    %count = 0;
    %startPos = 0;
    %length = strlen(%string);
    
    for (%i = 0; %i < %length; %i++)
    {
        %char = getSubStr(%string, %i, 1);
        if (%char $= %delimiter || %i == %length - 1)
        {
            if (%count == %index)
            {
                if (%i == %length - 1 && %char !$= %delimiter)
                    %i++;
                return getSubStr(%string, %startPos, %i - %startPos);
            }
            %count++;
            %startPos = %i + 1;
        }
    }
    return "";
}

function updateSkillInfo(%skillPath, %skillName, %level, %exp, %maxExp, %expPercent, %depth, %skillIndex)
{
    %indent = strRepeat("  ", %depth);
    
    // Update skill name
    %skillNameObj = "MonsterRPGx_Skills" @ %skillIndex;
    if (isObject(%skillNameObj))
    {
        %displayText = %indent @ %skillName @ " Lv." @ %level;
        %skillNameObj.setText(%displayText);
        %skillNameObj.setVisible(true);

        if($MRPGDEBUG) echo("DEBUG: Setting skill text - Object: " @ %skillNameObj @ ", Text: " @ %displayText);
    }
    else
    {
        if($MRPGDEBUG) error("ERROR: Skill name object not found - Index: " @ %skillIndex);
    }

    // Update experience bar
    %skillBarName = "MonsterRPGx_SkillBar" @ %skillIndex;
    if (isObject(%skillBarName))
    {
        %expPercent = mClamp(%expPercent, 0, 100);
        %x = getWord(%skillBarName.getPosition(), 0);
        %y = getWord(%skillBarName.getPosition(), 1);
        %w = getWord(%skillBarName.getExtent(), 0);
        %h = getWord(%skillBarName.getExtent(), 1);

        %newWidth = (%w * %expPercent) / 100;
        %skillBarName.resize(%x, %y, %newWidth, %h);
        %skillBarName.setVisible(true);

        if($MRPGDEBUG) echo("DEBUG: Updating skill bar - Name: " @ %skillBarName @ ", Percent: " @ %expPercent);
    }
    else
    {
        if($MRPGDEBUG) error("ERROR: Skill bar object not found - Index: " @ %skillIndex);
    }
}

function safeStrCmp(%str1, %str2)
{
    if (%str1 $= "" && %str2 $= "")
        return true;
    return strcmp(%str1, %str2) == 0;
}


function strRepeat(%str, %count)
{
    %result = "";
    for (%i = 0; %i < %count; %i++)
    {
        %result = %result @ %str;
    }
    return %result;
}

// Function to convert level to Roman numerals
function ConvertToRoman(%level)
{
	%romanNumerals = "I II III IV V";
	%levels = getWord(%romanNumerals, %level - 1);
	return %levels;
}

function CLIENTCMDMonsterRPGxUpdateRPGTraits(%data)
{
    // Retrieve traits from the data
    $MonsterRPG::Client::Trait_Knight = getField(%data, 0);
    $MonsterRPG::Client::Trait_Hunter = getField(%data, 1);
    $MonsterRPG::Client::Trait_Tank = getField(%data, 2);
    $MonsterRPG::Client::Trait_Mage = getField(%data, 3);
    $MonsterRPG::Client::Trait_Ninja = getField(%data, 4);
    $MonsterRPG::Client::Trait_Merchant = getField(%data, 5);
    $MonsterRPG::Client::Trait_Lord = getField(%data, 6);
    $MonsterRPG::Client::Trait_Brute = getField(%data, 7);
    $MonsterRPG::Client::Trait_Nomad = getField(%data, 8);

    // Trait names in order
    %traitNames[0] = "Knight";
    %traitNames[1] = "Hunter";
    %traitNames[2] = "Tank";
    %traitNames[3] = "Mage";
    %traitNames[4] = "Ninja";
    %traitNames[5] = "Merchant";
    %traitNames[6] = "Lord";
    %traitNames[7] = "Brute";
    %traitNames[8] = "Nomad";

    // Loop through traits and set text
    for (%i = 0; %i < 9; %i++)
    {
        %traitName = %traitNames[%i];
        %traitValue = $MonsterRPG::Client::Trait_[%traitName];

        if (%traitValue !$= "" && %traitValue > 0) // Check if trait is valid
        {
            %traitLevel = ConvertToRoman(%traitValue);
            %text = %traitName @ " " @ %traitLevel;
        }
        else
        {
            %text = ""; // Set empty text if trait is invalid
        }

        // Set the text for each MonsterRPGx_Traits control
        %controlName = "MonsterRPGx_Traits" @ (%i + 1);
        %control = eval("return " @ %controlName @ ";");
        if (isObject(%control))
        {
            %control.setText("<color:FFFFFF>" @ %text);
        }
        else
        {
            echo("Control not found: " @ %controlName);
        }
    }
}


//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function CLIENTCMDMonsterRPGx_ResetInvs(%invName,%invSize,%clearInv)
{
	switch$(%invName)
	{
		case "Initialize":
		
			%wndwCount = MonsterRPGx_SwGUIPar.getCount();
			
			for(%c = 0; %c < %wndwCount; %c++)
				MonsterRPGx_SwGUIPar.getObject(%c).setVisible(false);
			
			if(isObject(%relCell_infoSw = "MonsterRPGx_" @ getWord(MonsterRPGx_Main.prevCell,0) @ "_InfoParent_" @ getWord(MonsterRPGx_Main.prevCell,1)))
				%relCell_infoSw.setVisible(false);
		
			MonsterRPGx_Main.prevCell = "";
			MonsterRPGx_Main.currCell = "";
			MonsterRPGx_Main.prevSelTime = "";
			
			canvas.popDialog(MonsterRPGx_Transfer);
			return;
			
		case "PlyrInv":
		
			%cellRange = 39;
			%mxTls = %invSize; //getField(%invSizes,%c);
			MonsterRPGx_PlyrInv.setVisible(true);
			
			for(%cellNum = 0; %cellNum < %cellRange; %cellNum++)
			{
				%relCel_bgCol = "MonsterRPGx_PlyrInv_ItemBGColor_" @ %cellNum;
					
				if(%cellNum >= %mxTls)
					%relCel_bgCol.setVisible(false);
				else
					%relCel_bgCol.setVisible(true);
			}
			
		default:
		
			if(%clearInv)
			{
				for(%c = 0; %c < %invSize; %c++)
				{
					//Remove Item Icon
					if(isObject(%relCell_icon = "MonsterRPGx_" @ %invName @ "_ItemIcon_" @ %c))
					{
						%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/" @ %relCell_icon.MonsterRPGx_BtnImg);
						%relCell_icon.mColor = "255 255 255 255";
					}
					
					//Hide Info Window
					if(isObject(%relCell_info = "MonsterRPGx_" @ %invName @ "_InfoParent_" @ %c)) //? (isObject check encase of Grid-Forming GUI)
						%relCell_info.setVisible(false); //???
					
					//Hide Item Stack Amount
					if(isObject(%relCell_stackAmSw = "MonsterRPGx_" @ %invName @ "_InfoSwStackAm_" @ %c))
						%relCell_stackAmSw.setVisible(false);
				}
			}
			else
			{
				%cellRange = %invSize; //getField(%invSizes,%c);
				%relInv = "MonsterRPGx_" @ %invName;
				%relInv.setVisible(true);
			}
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function CLIENTCMDMonsterRPGx_ResetVehicleInv(%vehName,%cellType,%cellRange,%mxRows,%mxColms)
{
	//%wndwCount = MonsterRPGx_SwGUIPar.getCount();
			
	//for(%c = 0; %c < %wndwCount; %c++)
	//	MonsterRPGx_SwGUIPar.getObject(%c).setVisible(false);
	
	%relInv = "MonsterRPGx_" @ %cellType;
	%relInv.setVisible(true);
	MonsterRPGx_VehSpwnInvBG.clear();
	MonsterRPGx_VehSpwnInv.setText(%vehName SPC "Inventory");
	
	//resize wndw
	%extX = (68 * %mxColms) + 12;
	%extY = 74 + (68 * %mxRows) + 30;
	%posXY = MonsterRPGx_VehSpwnInv.position;
	MonsterRPGx_VehSpwnInv.resize(getWord(%posXY,0),getWord(%posXY,1),%extX,%extY);

	%cntrX = (%extX / 2) - 32;
	MonsterRPGx_VehSpwnInvBG.resize(4,26,%extX - 8,%extY - 30);
	MonsterRPGx_NewInvBtn(%cellType,0,%cntrX,4,true);
	
	for(%posY = 0; %posY < %mxRows; %posY++)
	{
		for(%posX = 0; %posX < %mxColms; %posX++)
		{
			MonsterRPGx_NewInvBtn(%cellType,%num++,4 + (68 * %posX),72 + (68 * %posY),false);
		}
	}
	
	//aux cell resetting ???
		
	MonsterRPGx_Main.prevCell = "";
	MonsterRPGx_Main.currCell = "";
	MonsterRPGx_Main.prevSelTime = "";
	
	canvas.popDialog(MonsterRPGx_Transfer);
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function CLIENTCMDMonsterRPGx_ReceiveInvItems(%invName,%cellNum,%itemData)
{
	%itemDB = getField(%itemData,0);
	%relCell_icon = "MonsterRPGx_" @ %invName @ "_ItemIcon_" @ %cellNum;
	%relCell_icon.tool = %itemDB;
	
	if(isObject(%itemDB))
	{
		//Set Item Icon
		if(%itemDB.iconName $= "")
		{
			%lRef = getSubStr(%itemDB.uiName,0,1);
			%relCell_icon.setBitmap("Add-Ons/Print_Letters_Default/icons/" @ %lRef);
		}
		else
			%relCell_icon.setBitmap(%itemDB.iconName);
		
		if(%itemDB.doColorShift)
			%relCell_icon.mColor = getColorI(%itemDB.colorShiftColor);
		else
			%relCell_icon.mColor = "255 255 255 255";
		
		
		// Set Custom Icon for swords
		%itemLevel = getField(%itemData,5);
	    setRPGItemIcon(%itemDB,%itemLevel,%relCell_icon);
		
		
		//Set Item Stack Amount and Limit
		%relCell_stackAm = "MonsterRPGx_" @ %invName @ "_InfoTxtStackAm_" @ %cellNum;
		%relCell_stackAmSw = "MonsterRPGx_" @ %invName @ "_InfoSwStackAm_" @ %cellNum;
		%relCell_stackLm = "MonsterRPGx_" @ %invName @ "_InfoTxtStackLm_" @ %cellNum;
		
		if(MonsterRPGx_SwGUIPar.stackEnab)
		{
			%relCell_stackAmSw.setVisible(true);
		
			%itemAm = getField(%itemData,1);
			%relCell_stackAm.setText("<font:impact:16><just:right><color:ffffff>" @ %itemAm);
		
			%itemLm = getField(%itemData,2);
			%relCell_stackLm.setText("<font:impact:16><color:00dd00>" @ %itemLm);
		}
		else
		{
			%relCell_stackAmSw.setVisible(false);
			%relCell_stackAm.setText("<font:impact:16><just:right><color:ffffff>-");
			%relCell_stackLm.setText("<font:impact:16><color:00dd00>-");
		}	
		
		
		//Set Item Weight (in info window)
		%relCell_wght = "MonsterRPGx_" @ %invName @ "_InfoTxtWght_" @ %cellNum;
		%itemWght = getField(%itemData,3);

		if(MonsterRPGx_SwGUIPar.encEnab)
			%relCell_wght.setText("<font:impact:16><color:0000ff>" @ %itemWght SPC MonsterRPGx_SwGUIPar.wghtSymb);
		else
			%relCell_wght.setText("<font:impact:16><color:0000ff>-");
		
		
		//Set Item Durability (in info window)
		%relCell_durab = "MonsterRPGx_" @ %invName @ "_InfoTxtHealth_" @ %cellNum;
		%itemDurab = getField(%itemData,4);

		if(MonsterRPGx_SwGUIPar.durabEnab)
			%relCell_durab.setText("<font:impact:16><color:ff0000>" @ %itemDurab @ "%");
		else
			%relCell_durab.setText("<font:impact:16><color:ff0000>-");
			
	}
	else
	{
		//Remove Item Icon
		%relCell_icon = "MonsterRPGx_" @ %invName @ "_ItemIcon_" @ %cellNum;
		%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/" @ %relCell_icon.MonsterRPGx_BtnImg);
		%relCell_icon.mColor = "255 255 255 255";
		
		//Hide Info Window
		%relCell_info = "MonsterRPGx_" @ %invName @ "_InfoParent_" @ %cellNum; //???
		%relCell_info.setVisible(false); //???
		
		//Hide Item Stack Amount
		%relCell_stackAmSw = "MonsterRPGx_" @ %invName @ "_InfoSwStackAm_" @ %cellNum;
		%relCell_stackAmSw.setVisible(false);
	}
	
	//Disable selected cell if updated (i.e. if an item is moved by another player or via events(?))
	if((%invName SPC %cellNum) $= MonsterRPGx_Main.prevCell) //TEST!!!
	{
		MonsterRPGx_Main.prevCell = "";
		MonsterRPGx_Main.currCell = "";
		MonsterRPGx_Main.prevSelTime = "";
		
		canvas.popDialog(MonsterRPGx_Transfer);

		%relCell_info = "MonsterRPGx_" @ %invName @ "_InfoParent_" @ %cellNum;
		%relCell_info.setVisible(false); //TEST!!!
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function CLIENTCMDMonsterRPGx_MassUpdateInv(%invName,%itemData)
{
	%wCount = getWordCount(%itemData);

	for(%c = 1; %c < %wCount; %c += 6)
	{
		%itemDB = getWord(%itemData,0 + %c);
		%itemAm = getWord(%itemData,1 + %c);
		%itemLm = getWord(%itemData,2 + %c);
		%itemWght = getWord(%itemData,3 + %c);
		%itemDurab = getWord(%itemData,4 + %c);
		%cellNum = getWord(%itemData,5 + %c);
		//The mass tuple is 6 words and carries NO level - the old read at word
		//6+%c grabbed the NEXT item's datablock id as the "level". Leveled slots
		//are re-sent individually (with the level in field 5) right after the
		//mass update, so leave it empty here.
		%itemLevel = "";
		
		%relCell_icon = "MonsterRPGx_" @ %invName @ "_ItemIcon_" @ %cellNum;
		%relCell_icon.tool = %itemDB;
		
		if(isObject(%itemDB))
		{
			//Set Item Icon
			if(%itemDB.iconName $= "")
			{
				%lRef = getSubStr(%itemDB.uiName,0,1);
				%relCell_icon.setBitmap("Add-Ons/Print_Letters_Default/icons/" @ %lRef);
				
			}
			else
				%relCell_icon.setBitmap(%itemDB.iconName);
			
			if(%itemDB.doColorShift)
				%relCell_icon.mColor = getColorI(%itemDB.colorShiftColor);
			else
				%relCell_icon.mColor = "255 255 255 255";
				
			setRPGItemIcon(%itemDB,%itemLevel,%relCell_icon);
			
			//Set Item Stack Amount and Limit
			%relCell_stackAm = "MonsterRPGx_" @ %invName @ "_InfoTxtStackAm_" @ %cellNum;
			%relCell_stackAmSw = "MonsterRPGx_" @ %invName @ "_InfoSwStackAm_" @ %cellNum;
			%relCell_stackLm = "MonsterRPGx_" @ %invName @ "_InfoTxtStackLm_" @ %cellNum;

			if(MonsterRPGx_SwGUIPar.stackEnab)
			{
				%relCell_stackAmSw.setVisible(true);
				%relCell_stackAm.setText("<font:impact:16><just:right><color:ffffff>" @ %itemAm);
				%relCell_stackLm.setText("<font:impact:16><color:00dd00>" @ %itemLm);
			}
			else
			{
				%relCell_stackAmSw.setVisible(false);
				%relCell_stackAm.setText("<font:impact:16><just:right><color:ffffff>-");
				%relCell_stackLm.setText("<font:impact:16><color:00dd00>-");
			}				
			
			
			//Set Item Weight (in info window)
			%relCell_wght = "MonsterRPGx_" @ %invName @ "_InfoTxtWght_" @ %cellNum;
			
			if(MonsterRPGx_SwGUIPar.encEnab)
				%relCell_wght.setText("<font:impact:16><color:0000ff>" @ %itemWght SPC MonsterRPGx_SwGUIPar.wghtSymb);
			else
				%relCell_wght.setText("<font:impact:16><color:0000ff>-");
			
			
			//Set Item Durability (in info window)
			%relCell_durab = "MonsterRPGx_" @ %invName @ "_InfoTxtHealth_" @ %cellNum;
			
			if(MonsterRPGx_SwGUIPar.durabEnab)
				%relCell_durab.setText("<font:impact:16><color:ff0000>" @ %itemDurab @ "%");
			else
				%relCell_durab.setText("<font:impact:16><color:ff0000>-");
		}
		else
		{			
			//Remove Item Icon
			%relCell_icon = "MonsterRPGx_" @ %invName @ "_ItemIcon_" @ %cellNum;
			%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/" @ %relCell_icon.MonsterRPGx_BtnImg);
			%relCell_icon.mColor = "255 255 255 255";
			
			//Hide Info Window
			%relCell_info = "MonsterRPGx_" @ %invName @ "_InfoParent_" @ %cellNum; //???
			%relCell_info.setVisible(false); //???
			
			//Hide Item Stack Amount
			%relCell_stackAmSw = "MonsterRPGx_" @ %invName @ "_InfoSwStackAm_" @ %cellNum;
			%relCell_stackAmSw.setVisible(false);
		}
	}
	
	//Disable selected cell if updated (i.e. if an item is moved by another player or via events(?))
	if((%invName SPC %cellNum) $= MonsterRPGx_Main.prevCell) //TEST!!!
	{
		MonsterRPGx_Main.prevCell = "";
		MonsterRPGx_Main.currCell = "";
		MonsterRPGx_Main.prevSelTime = "";
		
		canvas.popDialog(MonsterRPGx_Transfer);

		%relCell_info = "MonsterRPGx_" @ %invName @ "_InfoParent_" @ %cellNum;
		%relCell_info.setVisible(false); //TEST!!!
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function CLIENTCMDMonsterRPGx_MassUpdateInv_GridForming(%invName,%itemData,%mxGridSlots,%btnIcon)
{
	%wCount = getWordCount(%itemData);
	
	for(%c = 1; %c < %wCount; %c += 6)
	{
		%cellNum = getWord(%itemData,5 + %c);
		%relCell_icon = "MonsterRPGx_" @ %invName @ "_ItemIcon_" @ %cellNum;
			
		if(%cellNum < %mxGridSlots) //"<=" because %c = 1
		{
			%bool = getWord(%itemData,0 + %c);
			
			if(%bool == 0)
			{
				%relCell_icon.tool = "";
				%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/boolOff"); // @ %relCell_icon.MonsterRPGx_BtnImg);
			}
			else
			{
				%relCell_icon.tool = 1;
				%relCell_icon.setBitmap(%btnIcon); // @ %relCell_icon.MonsterRPGx_BtnImg);
				%relCell_icon.MonsterRPGx_BtnImg = %btnIcon;
			}
		}
		else
		{
			%itemDB = getWord(%itemData,0 + %c);
			%itemAm = getWord(%itemData,1 + %c);
			%itemLm = getWord(%itemData,2 + %c);
			%itemWght = getWord(%itemData,3 + %c);
			%itemDurab = getWord(%itemData,4 + %c);
			%cellNum = getWord(%itemData,5 + %c);
			%itemLevel = getWord(%itemData,6 + %c);
			
			%relCell_icon = "MonsterRPGx_" @ %invName @ "_ItemIcon_" @ %cellNum;
			%relCell_icon.tool = %itemDB;
			
			if(isObject(%itemDB))
			{
				//Set Item Icon
				if(%itemDB.iconName $= "")
				{
					%lRef = getSubStr(%itemDB.uiName,0,1);
					%relCell_icon.setBitmap("Add-Ons/Print_Letters_Default/icons/" @ %lRef);
				}
				else
					%relCell_icon.setBitmap(%itemDB.iconName);
				
				if(%itemDB.doColorShift)
					%relCell_icon.mColor = getColorI(%itemDB.colorShiftColor);
				else
					%relCell_icon.mColor = "255 255 255 255";
					
				setRPGItemIcon(%itemDB,%itemLevel,%relCell_icon);
				
				
				//Set Item Stack Amount and Limit
				%relCell_stackAm = "MonsterRPGx_" @ %invName @ "_InfoTxtStackAm_" @ %cellNum;
				%relCell_stackAmSw = "MonsterRPGx_" @ %invName @ "_InfoSwStackAm_" @ %cellNum;
				%relCell_stackLm = "MonsterRPGx_" @ %invName @ "_InfoTxtStackLm_" @ %cellNum;			
				
				if(MonsterRPGx_SwGUIPar.stackEnab)
				{
					%relCell_stackAmSw.setVisible(true);
					%relCell_stackAm.setText("<font:impact:16><just:right><color:ffffff>" @ %itemAm);
					%relCell_stackLm.setText("<font:impact:16><color:00dd00>" @ %itemLm);
				}
				else
				{
					%relCell_stackAmSw.setVisible(false);
					%relCell_stackAm.setText("<font:impact:16><just:right><color:ffffff>-");
					%relCell_stackLm.setText("<font:impact:16><color:00dd00>-");
				}

				
				//Set Item Weight (in info window)
				%relCell_wght = "MonsterRPGx_" @ %invName @ "_InfoTxtWght_" @ %cellNum;
				
				if(MonsterRPGx_SwGUIPar.encEnab)
					%relCell_wght.setText("<font:impact:16><color:0000ff>" @ %itemWght SPC MonsterRPGx_SwGUIPar.wghtSymb);
				else
					%relCell_wght.setText("<font:impact:16><color:0000ff>-");
				
				
				//Set Item Durability (in info window)
				%relCell_durab = "MonsterRPGx_" @ %invName @ "_InfoTxtHealth_" @ %cellNum;
				
				if(MonsterRPGx_SwGUIPar.durabEnab)
					%relCell_durab.setText("<font:impact:16><color:ff0000>" @ %itemDurab @ "%");
				else
					%relCell_durab.setText("<font:impact:16><color:ff0000>-");
			}
			else
			{			
				//Remove Item Icon
				%relCell_icon = "MonsterRPGx_" @ %invName @ "_ItemIcon_" @ %cellNum;
				%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/" @ %relCell_icon.MonsterRPGx_BtnImg);
				%relCell_icon.mColor = "255 255 255 255";
				
				//Hide Info Window
				%relCell_info = "MonsterRPGx_" @ %invName @ "_InfoParent_" @ %cellNum; //???
				%relCell_info.setVisible(false); //???
				
				//Hide Item Stack Amount
				%relCell_stackAmSw = "MonsterRPGx_" @ %invName @ "_InfoSwStackAm_" @ %cellNum;
				%relCell_stackAmSw.setVisible(false);
			}
		}
	}
	
	//Disable selected cell if updated (i.e. if an item is moved by another player or via events(?))
	if((%invName SPC %cellNum) $= MonsterRPGx_Main.prevCell) //TEST!!!
	{
		MonsterRPGx_Main.prevCell = "";
		MonsterRPGx_Main.currCell = "";
		MonsterRPGx_Main.prevSelTime = "";
		
		canvas.popDialog(MonsterRPGx_Transfer);

		%relCell_info = "MonsterRPGx_" @ %invName @ "_InfoParent_" @ %cellNum;
		%relCell_info.setVisible(false); //TEST!!!
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function CLIENTCMDMonsterRPGx_ToggleInvWndw(%action,%wndw)
{
	switch$(%action)
	{
		case "Open":
		
			switch$(%wndw)
			{
				case "All":
					canvas.pushDialog(MonsterRPGx_Main);
					canvas.popDialog(MonsterRPGx_Transfer);
					canvas.popDialog(MonsterRPGx_LockPick);
				case "DTblInv":
					canvas.pushDialog(MonsterRPGx_RecipeMngmt);
				default:
					%invWndw = "MonsterRPGx_" @ %wndw;
					%invWndw.setVisible(true);
			}
			
		case "Close":
		
			switch$(%wndw)
			{
				case "All":
					canvas.popDialog(MonsterRPGx_Main);
					canvas.popDialog(MonsterRPGx_Transfer);
					canvas.popDialog(MonsterRPGx_LockPick);
				case "DTblInv":
					canvas.popDialog(MonsterRPGx_RecipeMngmt);
				default:
					%invWndw = "MonsterRPGx_" @ %wndw;
					%invWndw.setVisible(false);
					
					if(%wndw $= getWord(MonsterRPGx_Main.prevCell,0))
					{
						if(isObject(%relCell_infoSw = "MonsterRPGx_" @ getWord(MonsterRPGx_Main.prevCell,0) @ "_InfoParent_" @ getWord(MonsterRPGx_Main.prevCell,1)))
							%relCell_infoSw.setVisible(false);
					
						MonsterRPGx_Main.prevCell = "";
						MonsterRPGx_Main.currCell = "";
						MonsterRPGx_Main.prevSelTime = "";
					}
			}
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function CLIENTCMDMonsterRPGx_UpdateGUI(%type,%data,%btnIcon)
{
	switch$(%type)
	{
		case "EncUpdate":
			
			if(%data $= "")
				MonsterRPGx_SwTotEnc.setVisible(false);
			else
			{
				%encRel = getWord(%data,0);
				%encStart = getWord(%data,1);
				%encMax = getWord(%data,2);
				%encMod = getWord(%data,3);
				
				if(%encMod !$= "")
				{
					if(%encMod >= 0)
						%encModTxt = "( +" @ %encMod @ ")";
					else
						%encModTxt = "( -" @ %encMod @ ")";
				}
				
				MonsterRPGx_SwTotEnc.setVisible(true);
				MonsterRPGx_TxtTotEnc.setText("<font:impact:24><color:ffffff>Total Encumbrance: " @ %encRel @ " of " @ %encStart @ " (" @ %encMax @ " Max) " @ %encModTxt);
			}
		
		case "BrViewers": //Inventory Viewers

			if(%data $= "")
				MonsterRPGx_SwTotViewers.setVisible(false);
			else
			{
				MonsterRPGx_TxtTotViewers.setText("<font:impact:24><color:ffffff>Total Inventory Viewers: " @ %data);
				MonsterRPGx_SwTotViewers.setVisible(true);
			}
		
		case "HungerThirst":
		
			if(getWord(%data,0))
			{
				MonsterRPGx_SwHungerLvl.setVisible(true);
				%am = mFloatLength(getWord(%data,1),2);
				MonsterRPGx_TxtHungerLvlRel.resize(147,3,mClamp(360 * (%am / 100),0,360),26); //211,3 //176,
				MonsterRPGx_TxtHungerLvl.setText("<font:impact:28><color:ffffff>" @ (getWord(%data,1)) @ "%");
			}
			else
				MonsterRPGx_SwHungerLvl.setVisible(false);
			
			if(getWord(%data,2))
			{
				MonsterRPGx_SwThirstLvl.setVisible(true);
				%am = mFloatLength(getWord(%data,3),2);
				MonsterRPGx_TxtThirstLvlRel.resize(147,3,mClamp(360 * (%am / 100),0,360),26); //211,3 //176,
				MonsterRPGx_TxtThirstLvl.setText("<font:impact:28><color:ffffff>" @ (getWord(%data,3)) @ "%");
			}
			else
				MonsterRPGx_SwThirstLvl.setVisible(false);
				
	   case "HealthMana":
		
		    MonsterRPGx_HUDBase.setVisible(true);
			MonsterRPGx_HUD.setVisible(true);
			MonsterRPGx_HUD_Resize();
			
			//if(getWord(%data,0))
			//{
				//MonsterRPGx_HUDBase.setVisible(true);
				//MonsterRPGx_HUD.setVisible(true);
				//MonsterRPGx_SwRPGHealthLvl.setVisible(true);
				//%am = mFloatLength(getWord(%data,0),0);
				//MonsterRPGx_TxtRPGHealthLvlRel.resize(2,3,mClamp(300 * (%am / 100),0,300),20); //211,3 //176,
				//MonsterRPGx_TxtRPGHealthLvl.setText("<just:center><font:arial bold:20><color:ffffff>" @ (getWord(%data,1)) @ "/" @ (getWord(%data,2)));
				//MonsterRPGx_HUD_Resize();
			//}
			//else
			//{	
				//MonsterRPGx_SwRPGHealthLvl.setVisible(false);
			//	MonsterRPGx_TxtRPGHealthLvl_Resize();
			//}
			
			//if(getWord(%data,3))
			//{
			//	//MonsterRPGx_SwRPGManaLvl.setVisible(true);
				//%am = mFloatLength(getWord(%data,3),0);
				//MonsterRPGx_TxtRPGManaLvlRel.resize(2,3,mClamp(250 * (%am / 100),0,250),20); //211,3 //176,
			//	MonsterRPGx_TxtRPGManaLvl.setText("<just:center><font:arial bold:20><color:ffffff>" @ (getWord(%data,4)) @ "/" @ (getWord(%data,5)));
			//}
			//else
			//{
			//	MonsterRPGx_SwRPGManaLvl.setVisible(false);
			//	MonsterRPGx_SwRPGManaLvl_Resize();
			//}
		
		case "WaterLevel":

			%invName = getField(%data,0);
			%amMax = getField(%data,1);
			%amLeft = getField(%data,2);
			
			%sw = "MonsterRPGx_" @ %invName @ "_SwAm";
			%txt = "MonsterRPGx_" @ %invName @ "_TxtAm";
			%hExt = %sw.maxHExt;
			%hPos = %sw.hPos;
			%res = mClamp(%hExt * (%amLeft / %amMax),0,%hExt);
			
			%sw.resize(0,%hPos + (%hExt - %res),184,%res);
			%txt.setText("<font:impact:20>Uses Left:" @ %amLeft);
			
		
		case "CraftPreview":
		
			%relCell_icon = "MonsterRPGx_" @ getField(%data,0) @ "_ItemIcon_" @ getField(%data,1);
			%relCell_icon.tool = "";
			
			if(isObject(%itemDB = getField(%data,2)))
			{
				//Set Item Icon
				if(%itemDB.iconName $= "")
				{
					%lRef = getSubStr(%itemDB.uiName,0,1);
					%relCell_icon.setBitmap("Add-Ons/Print_Letters_Default/icons/" @ %lRef);
				}
				else
					%relCell_icon.setBitmap(%itemDB.iconName);
				
				if(%itemDB.doColorShift)
					%relCell_icon.mColor = getWords(getColorI(%itemDB.colorShiftColor),0,2) SPC 100;
				else
					%relCell_icon.mColor = "255 255 255 100"; //"255 255 255 255";
			}
			else
			{
				//Remove Item Icon
				%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/" @ %relCell_icon.MonsterRPGx_BtnImg);
				%relCell_icon.mColor = "255 255 255 255";
			}
		
		case "GridFormingToggle":
		
			%cellType = getField(%data,0);
			%cellNum = getField(%data,1);
			%bool = getField(%data,2);
			%relCell_icon = "MonsterRPGx_" @ %cellType @ "_ItemIcon_" @ %cellNum; //object check?
			
			if(%bool)
			{
				%relCell_icon.setBitmap(%btnIcon);
				%relCell_icon.tool = "Bool";
			}
			else
			{
				%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/boolOff");
				%relCell_icon.tool = "";
			}
		
		case "GridFormingMassUpdate":
		
			%cellType = getField(%data,0);
			%mxGridSlots = getField(%data,1);
			%bool = getField(%data,2);
			
			if(%bool)
			{
				for(%c = 0; %c < %mxGridSlots; %c++)
				{
					%relCell_icon = "MonsterRPGx_" @ %cellType @ "_ItemIcon_" @ %c; //object check?
					%relCell_icon.setBitmap(%btnIcon);
					%relCell_icon.tool = "Bool";
					//%relCell_icon.MonsterRPGx_BtnImg = %btnIcon;
				}
			}
			else
			{
				for(%c = 0; %c < %mxGridSlots; %c++)
				{
					%relCell_icon = "MonsterRPGx_" @ %cellType @ "_ItemIcon_" @ %c; //object check?
					%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/boolOff");
					%relCell_icon.tool = "";
				}
			}
		
		case "FurnaceProgUpdate":
		
			%cellType = getField(%data,0);
			%fieldCount = getFieldCount(%data);
			
			for(%c = 1; %c < %fieldCount; %c += 2)
			{
				%cellNum = getField(%data,%c);
				%perc = getField(%data,%c + 1);
				%relCell_sw = "MonsterRPGx_" @ %cellType @ "_ProgressSw_" @ %cellNum; //object check?
				
				%scale = mClamp(%perc * 64,0,64);
				%remScale = 64 - %scale;
				%relCell_sw.resize(0,%remScale,64,%scale);
			}
		
		case "ToggleLocked":
		
			%cellType = getField(%data,0);
			%bool = getField(%data,1);
			
			%overlayWndw = "MonsterRPGx_" @ %cellType @ "_lockedOverlay"; //object check?
			if(isObject(%overlayWndw)) %overlayWndw.setVisible(%bool);
		
		case "ResetLockSmTbl":
		
			%codeLen = mClamp(MonsterRPGx_LockSmInv.securityLevel = %data,0,7); //"0" allows all digits to default to "X" when necessary
			
			for(%c = 0; %c < 7; %c++)
			{
				%ref = "MonsterRPGx_LockSmInv_Txt" @ %c;
				%btnUp = "MonsterRPGx_LockSmInv_BtnUp" @ %c;
				%btnDw = "MonsterRPGx_LockSmInv_BtnDw" @ %c;
				
				if(%c < %codeLen)
				{
					%ref.setValue("<font:impact:20>" @ 0);
					%ref.setActive(true);
					%ref.enabled = true;
					%btnUp.setVisible(true);
					%btnDw.setVisible(true);
				}
				else
				{
					%ref.setValue("<font:impact:20><color:999999>X");
					%ref.setActive(false);
					%ref.enabled = false;
					%btnUp.setVisible(false);
					%btnDw.setVisible(false);
				}
			}
		
		case "DataSync":
		
			MonsterRPGx_ToggleAutoSort.setValue(%data);
			MonsterRPGx_Main.isHoldingShift = false; //reset value on plyr respawn
		
		case "ServerData":
			
			MonsterRPGx_SwGUIPar.encEnab = getField(%data,0);
			MonsterRPGx_SwGUIPar.durabEnab = getField(%data,1);
			MonsterRPGx_SwGUIPar.stackEnab = getField(%data,2);
			MonsterRPGx_SwGUIPar.wghtSymb = getField(%data,3);
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function CLIENTCMDMonsterRPGx_LockPickWndw(%action,%data)
{
	switch$(%action)
	{
		case "Setup":
		
			%pos = MonsterRPGx_LockPickWndw.getPosition();
			%posX = getWord(%pos,0);
			%posY = getWord(%pos,1);
			
			%securityLevel = MonsterRPGx_LockPick.securityLevel = mClamp(getField(%data,0),2,7);
			MonsterRPGx_LockPick.storGroup = getField(%data,1);
			MonsterRPGx_LockPick.storType = getField(%data,2);
			MonsterRPGx_LockPick_Txtcc.setText("<font:impact:18>Characters Correct: ?");
			
			switch(%securityLevel)
			{
				case 2:
					MonsterRPGx_LockPickWndw.resize(%posX,%posY,148,292);
				case 3:
					MonsterRPGx_LockPickWndw.resize(%posX,%posY,216,292);
				case 4:
					MonsterRPGx_LockPickWndw.resize(%posX,%posY,284,292);
				case 5:
					MonsterRPGx_LockPickWndw.resize(%posX,%posY,352,292);
				case 6:
					MonsterRPGx_LockPickWndw.resize(%posX,%posY,420,292);
				case 7:
					MonsterRPGx_LockPickWndw.resize(%posX,%posY,488,292);
			}
			
			for(%c = 0; %c < %securityLevel; %c++)
			{
				%ref_TE = "MonsterRPGx_LockPick_TE" @ %c + 1;
				%ref_Sdr = "MonsterRPGx_LockPick_Sdr" @ %c + 1;
				%ref_TE.setValue(4);
				%ref_Sdr.setValue(5);
			}
			
			canvas.pushDialog(MonsterRPGx_LockPick);
		
		case "Close":
		
			MonsterRPGx_LockPick.securityLevel = "";
			MonsterRPGx_LockPick.storGroup = "";
			MonsterRPGx_LockPick.storType = "";
			
			canvas.popDialog(MonsterRPGx_LockPick);
		
		case "Update":
		
			MonsterRPGx_LockPick_Txtcc.setText("<font:impact:18>Characters Correct:" SPC mFloor(%data));
		
		case "Send":
		
			%strLen = %securityLevel = mClamp(MonsterRPGx_LockPick.securityLevel,2,7);
			%storGroup = MonsterRPGx_LockPick.storGroup;
			%storType = MonsterRPGx_LockPick.storType;
			
			for(%c = 0; %c < %strLen; %c++)
			{
				%ref = "MonsterRPGx_LockPick_TE" @ %c + 1;
				%str = setWord(%str,%c,%ref.getValue());
			}
			commandToServer('MonsterRPGx_PickLock',%storGroup,%storType,strReplace(%str," ",""));
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function CLIENTCMDMonsterRPGx_ReceiveDraftData(%type,%data,%aux,%auxB,%auxC,%auxD)
{
	switch$(%type)
	{
		//Crafting / Forming / Crucible Recipe Data
		case "ItemCraft" or "GenCraft" or "GridForm" or "Crucible":
		
			switch$(%type)
			{
				case "ItemCraft":
					%list = MonsterRPGx_DraftRcpList_ItemCraft;
					%outputNum = 8;
				case "GenCraft":
					%list = MonsterRPGx_DraftRcpList_GenCraft;
					%isShapeless = %aux;
					%outputNum = 18;
				case "GridForm":
					%list = MonsterRPGx_DraftRcpList_GridForm;
					%outputNum = 31;
				case "Crucible":
					%list = MonsterRPGx_DraftRcpList_Crucible;
					%outputNum = 10;
				default:
					return;
			}
			%rCount = mFloor(%list.rowCount);
			%recipe = %data;
			
			if(%data $= "Reset")
			{
				%list.clear();
				%list.rowCount = 0;
				
				for(%c = 0; %c < %rCount; %c++)
					%list.recipe[%c] = "";
			}
			else
			{
				if(%type $= "GenCraft" && %isShapeless)
					%txtMod = " (Shapeless)";
				
				if((%uiName = getWord(%recipe,%outputNum).uiName) $= "")
					%uiName = "(MISSING ITEM)";
				
				%list.addRow(%rCount,%uiName @ %txtMod,%rCount);
				%list.recipe[%rCount] = %recipe;
				%list.rowCount++;
			}
			
		//////////////////////////////////////////////////
		//Popup Menu Item Data
		
		case "MenuData":
		
			%wCount = getWordCount(%data);
		
			switch$(%aux)
			{
				case "Reset":
				
					MonsterRPGx_DraftWndw_ItemSelItemsMenu.clear();
					MonsterRPGx_DraftWndw_ItemSelMoldsMenu.clear();
					MonsterRPGx_DraftWndw_ItemSelMatsMenu.clear();
					MonsterRPGx_Dtbl_MISfrncRslt.clear();
					MonsterRPGx_Dtbl_MIScrblRslt.clear();
					MonsterRPGx_DraftWndw_AlSel0.clear();
					MonsterRPGx_DraftWndw_AlSel1.clear();
					MonsterRPGx_DraftWndw_AlSel2.clear();
					MonsterRPGx_DraftWndw_AlSel3.clear();
					//MonsterRPGx_DraftWndw_AlSelOutput.clear();
					
					%mCount = MonsterRPGx_DraftWndw_Crucible.metalCount;
						for(%c = 0; %c < %mCount; %c++)
							MonsterRPGx_DraftWndw_Crucible.metal[%c] = "";
					
					MonsterRPGx_DraftWndw_ItemSelItemsMenu.setSelected(0);
					MonsterRPGx_DraftWndw_ItemSelMoldsMenu.setSelected(0);
					MonsterRPGx_DraftWndw_ItemSelMatsMenu.setSelected(0);
					MonsterRPGx_Dtbl_MISfrncRslt.setSelected(0);
					MonsterRPGx_Dtbl_MIScrblRslt.setSelected(0);
					MonsterRPGx_DraftWndw_Crucible.lqdMoldMax = "";
					
					MonsterRPGx_DraftWndw_ItemSelWndw.setVisible(0);
					MonsterRPGx_DtblConfirm.setVisible(0);
					MonsterRPGx_DtblConfirm.type = "";
					MonsterRPGx_DtblItemSetMod.setVisible(0);
					MonsterRPGx_DtblItemSetMod.itemID = "";
					
					MonsterRPGx_DraftWndw_ItemSelIcon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/noItem");
					MonsterRPGx_DraftWndw_ItemSelIcon.mColor = "255 255 255 255";
					MonsterRPGx_DraftWndw_ItemSelAmEdit.setValue(1);
					MonsterRPGx_DraftWndw_ItemSelItemsMenu.setSelected(0);
					MonsterRPGx_DraftWndw_ItemSelMoldsMenu.setSelected(0);
					MonsterRPGx_DraftWndw_ItemSelMatsMenu.setSelected(0);
					
				case "Items":
				
					%menu = MonsterRPGx_DraftWndw_ItemSelItemsMenu;
					%menuB = MonsterRPGx_Dtbl_MISfrncRslt;
					
				case "Molds":
				
					%menu = MonsterRPGx_DraftWndw_ItemSelMoldsMenu;
					
				case "Materials":
				
					%menu = MonsterRPGx_DraftWndw_ItemSelMatsMenu;
					
				case "CrucibleMax":
				
					for(%c = 0; %c < 4; %c++)
					{
						%menuR = "MonsterRPGx_DraftWndw_RtSel" @ %c;
						
						for(%d = 0; %d <= %data; %d++) //%lMax = %data
							%menuR.add(%d @ "m", %d);
					}
					return;
					
				case "Metals":
					
					for(%d = 0; %d < %wCount; %d += 2)
					{
						MonsterRPGx_DraftWndw_Crucible.metal[getWord(%data,%d)] = getWord(%data,%d + 1);
						MonsterRPGx_DraftWndw_Crucible.metalCount++;
					}
					for(%c = 0; %c < 4; %c++)
					{
						%menuM = "MonsterRPGx_DraftWndw_AlSel" @ %c;
						
						for(%d = 0; %d < %wCount; %d += 2)
							%menuM.add(getWord(%data,%d),getWord(%data,%d + 1));
					}
					for(%d = 0; %d < %wCount; %d += 2)
						MonsterRPGx_Dtbl_MIScrblRslt.add(getWord(%data,%d),getWord(%data,%d + 1)); //item crucible result menu
					
					return; //MonsterRPGx_DraftWndw_AlSelOutput.add(getWord(%data,4),4);
					
				case "Complete":
				
					MonsterRPGx_RecipeMngmt.initSetup = true;
					MonsterRPGx_DraftWndw_ItemSelItemsMenu.sort();
					MonsterRPGx_DraftWndw_ItemSelMoldsMenu.sort();
					MonsterRPGx_DraftWndw_ItemSelMatsMenu.sort();
					MonsterRPGx_Dtbl_MISfrncRslt.sort();
					
					echo("> MonsterRPGx: Initial client drafting GUI setup complete!");
			}
			
			for(%c = 0; %c < %wCount; %c++)
			{
				if((%itemID = getWord(%data,%c)) $= "-" || strReplace(%itemID," ","") $= "")
				{
					%menu.add(" ",%itemID);
					if(%menuB !$= "") %menuB.add(" ",%itemID);
				}
				else
				{
					%menu.add(%itemID.uiName,%itemID);
					if(%menuB !$= "") %menuB.add(%itemID.uiName,%itemID); //item furnace result menu
				}
			}
			
		//////////////////////////////////////////////////
		
		case "RmvRecipe":
		
			switch$(%data)
			{
				case "ItemCraft":
					%list = MonsterRPGx_DraftRcpList_ItemCraft;
				case "GenCraft":
					%list = MonsterRPGx_DraftRcpList_GenCraft;
				case "GridForm":
					%list = MonsterRPGx_DraftRcpList_GridForm;
				case "Crucible":
					%list = MonsterRPGx_DraftRcpList_Crucible;
				default:
					return;
			}		
			%list.removeRowByID(%aux);
		
		//////////////////////////////////////////////////
		
		case "ItemSettings":
		
			switch$(%data)
			{
				case "Reset":
					MonsterRPGx_DraftRcpList_ItemSettings.clear();
				case "Complete":
					MonsterRPGx_DraftRcpList_ItemSettings.sort(0,1);
				default:
					if(isObject(getField(%aux,8)))
					{
						%aux = setField(%aux,11,getField(%aux,8));
						%aux = setField(%aux,8,getField(%aux,8).uiName);
					}
					//if(getField(%aux,9))
					//{
						%aux = setField(%aux,12,getField(%aux,9));
						%aux = setField(%aux,9,MonsterRPGx_Dtbl_MIScrblRslt.getTextByID(getField(%aux,9))); //find metal name from popup menu data and metal id
					//}

					MonsterRPGx_DraftRcpList_ItemSettings.addRow(%auxB,%auxB.uiName TAB %aux,%auxB);
			}
			
		case "ItemSettings-ByID":
			
			if(isObject(getField(%aux,8)))
			{
				%aux = setField(%aux,11,getField(%aux,8));
				%aux = setField(%aux,8,getField(%aux,8).uiName);
			}
			//if(isObject(getField(%aux,9)))
			//{
				%aux = setField(%aux,12,getField(%aux,9));
				%aux = setField(%aux,9,MonsterRPGx_Dtbl_MIScrblRslt.getTextByID(getField(%aux,9))); //find metal name from popup menu data and metal id
			//}
				
			MonsterRPGx_DraftRcpList_ItemSettings.setRowByID(%auxB,%auxB.uiName TAB %aux);
		
		//////////////////////////////////////////////////
		
		case "ServerSettings":
			
			MonsterRPGx_DTbl_EnabEnc.setValue(getField(%aux,0));
			MonsterRPGx_DTbl_EnabDurab.setValue(getField(%aux,1));
			MonsterRPGx_DTbl_DgrdUse.setValue(getField(%aux,2));
			MonsterRPGx_DTbl_DgrdTime.setValue(getField(%aux,3));
			MonsterRPGx_DTbl_EnabStack.setValue(getField(%aux,4));
			MonsterRPGx_DTbl_EnabItemStor.setValue(getField(%aux,5));
			MonsterRPGx_DTbl_EnabBrStor.setValue(getField(%aux,6));
			MonsterRPGx_DTbl_EnabVehStor.setValue(getField(%aux,7));
			MonsterRPGx_DTbl_EnabHunger.setValue(getField(%aux,8));
			MonsterRPGx_DTbl_EnabThirst.setValue(getField(%aux,9));
			MonsterRPGx_DTbl_EnabModding.setValue(getField(%aux,10));
			MonsterRPGx_DTbl_EnabAutoSave.setValue(getField(%aux,11));
			MonsterRPGx_DTbl_MenuPerCraft.setSelected(getField(%aux,12));
			
			MonsterRPGx_DTbl_EncStart.setValue(getField(%auxB,0));
			MonsterRPGx_DTbl_EncMax.setValue(getField(%auxB,1));
			MonsterRPGx_DTbl_WghtSymb.setValue(getField(%auxB,2));
			MonsterRPGx_DTbl_MaxDist.setValue(getField(%auxB,3));
			MonsterRPGx_DTbl_dropCntrs.setValue(getField(%auxB,4));
			MonsterRPGx_DTbl_invQPlyr.setValue(getField(%auxB,5));
			MonsterRPGx_DTbl_invQSrvr.setValue(getField(%auxB,6));
			MonsterRPGx_DTbl_hmrDest.setValue(getField(%auxB,7));
			MonsterRPGx_DTbl_wndDest.setValue(getField(%auxB,8));
			MonsterRPGx_DTbl_redHungerAm.setValue(getField(%auxB,9));
			MonsterRPGx_DTbl_redThirstAm.setValue(getField(%auxB,10));
			MonsterRPGx_DTbl_oreFreq.setValue(getField(%auxB,11));
			MonsterRPGx_DTbl_gemFreq.setValue(getField(%auxB,12));
			MonsterRPGx_DTbl_bowSpearAmmo.setValue(getField(%auxB,13));
			
			MonsterRPGx_DTbl_invSaveSched.setValue(getField(%auxC,0));
			MonsterRPGx_DTbl_ShopUpdateShed.setValue(getField(%auxC,1));
			//MonsterRPGx_DTbl_moneyUpdateSched.setValue(getField(%auxC,2));
			MonsterRPGx_DTbl_LootUpdateSched.setValue(getField(%auxC,2));
			//MonsterRPGx_DTbl_PlantUpdateSched.setValue(getField(%auxC,3));
			MonsterRPGx_DTbl_HungerUpdateSched.setValue(getField(%auxC,3));
			MonsterRPGx_DTbl_ThirstUpdateSched.setValue(getField(%auxC,4));
			MonsterRPGx_DTbl_DgrdSched.setValue(getField(%auxC,5));

			MonsterRPGx_DTbl_defStckLmt.setValue(getField(%auxD,0));
			MonsterRPGx_DTbl_defWght.setValue(getField(%auxD,1));
			MonsterRPGx_DTbl_defDurab.setValue(getField(%auxD,2));
			MonsterRPGx_DTbl_defDgrdUse.setValue(getField(%auxD,3));
			MonsterRPGx_DTbl_defDgrdTime.setValue(getField(%auxD,4));
			MonsterRPGx_DTbl_defEff.setValue(getField(%auxD,5));
			MonsterRPGx_DTbl_brnRate.setValue(getField(%auxD,6));
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function CLIENTCMDMonsterRPGx_ReceiveShopItems(%type,%shopData,%itemID)
{
	switch$(%type)
	{
		case "Reset":
		
			MonsterRPGx_Shop_listBuy.clear();
			MonsterRPGx_Shop_listSell.clear();
			MonsterRPGx_ShopInv.setVisible(true);
			MonsterRPGx_ShopInv.setText(getField(%shopData,0));
			MonsterRPGx_Shop_amBuy.setSelected(1);
			MonsterRPGx_Shop_amSell.setSelected(1);
			MonsterRPGx_Shop_nameBuy.setText("<font:impact:20>(Item Name)");
			MonsterRPGx_Shop_nameSell.setText("<font:impact:20>(Item Name)");
			MonsterRPGx_Shop_iconBuy.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/noItem");
			MonsterRPGx_Shop_iconSell.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/noItem");
			
		case "UpdateMoney":
		
			MonsterRPGx_Shop_moneyBuy.setText("<font:impact:18>Shopkeeper's Money:" SPC %shopData);
			MonsterRPGx_Shop_moneySell.setText("<font:impact:18>Shopkeeper's Money:" SPC %shopData);
			
		case "Buy":
		
			%shopData = setField(%shopData,0,%itemID.uiName);
			MonsterRPGx_Shop_listBuy.addRow(%itemID,%shopData,0);
			
		case "Sell":
		
			%shopData = setField(%shopData,0,%itemID.uiName);
			MonsterRPGx_Shop_listSell.addRow(%itemID,%shopData,0);
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function CLIENTCMDMonsterRPGx_ClearPlyrCraftGUI()
{
	%cellTypes = "CTblPlyr2by2 CTblPlyr1by3 CTblPlyr3by3";
	%mxSlotsStr = "10 10 10";
	
	for(%c = 0; %c < 3; %c++)
	{
		%cellType = getWord(%cellTypes,%c);
		%mxSlots = getWord(%mxSlotsStr,%c);
		
		for(%cellNum = 0; %cellNum < %mxSlots; %cellNum++)
		{
			%relCell_icon = "MonsterRPGx_" @ %cellType @ "_ItemIcon_" @ %cellNum;
			%relCell_icon.tool = %itemDB;

			if(isObject(%relCell_icon))
			{
				//Remove Item Icon
				%relCell_icon.setBitmap("Add-Ons/Client_MonsterRPG/GUIs/" @ %relCell_icon.MonsterRPGx_BtnImg);
				%relCell_icon.mColor = "255 255 255 255";
				
				//Hide Info Window
				%relCell_info = "MonsterRPGx_" @ %cellType @ "_InfoParent_" @ %cellNum; //???
				%relCell_info.setVisible(false); //???
				
				//Hide Item Stack Amount
				%relCell_stackAmSw = "MonsterRPGx_" @ %cellType @ "_InfoSwStackAm_" @ %cellNum;
				%relCell_stackAmSw.setVisible(false);
			}
		}
	}
}