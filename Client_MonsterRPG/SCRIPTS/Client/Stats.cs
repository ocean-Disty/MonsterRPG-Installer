if(!isObject(MonsterRPGx_LocalStatsSO))
{
    %this = new ScriptObject(MonsterRPGx_LocalStatsSO);
    
    %this.maxCategories = 2;
    
	%this.category[0] = "Stats";
    %this.category[1] = "Traits";
    %this.category[2] = "Achievements";
}


function clientCmdReceiveTraitData(%data)
{
    if(!$MonsterRPG::Client::TraitsCounted){$MonsterRPG::Client::TraitCount = 0; $MonsterRPG::Client::TraitsCounted = 1;}
    $MonsterRPG::Client::Trait[$MonsterRPG::Client::TraitCount, "Name"] = getField(%data,0);
	$MonsterRPG::Client::Trait[$MonsterRPG::Client::TraitCount, "Description"] = getField(%data,1);
	$MonsterRPG::Client::Trait[$MonsterRPG::Client::TraitCount, "levelReq"] = getField(%data,2);
	$MonsterRPG::Client::Trait[$MonsterRPG::Client::TraitCount, "goldCost"] = getField(%data,3);
	$MonsterRPG::Client::Trait[$MonsterRPG::Client::TraitCount, "rank"] = getField(%data,4);
	$MonsterRPG::Client::TraitCount++;
}

function MonsterRPGx_LocalStatsSO::list(%this)
{
    MonsterRPGx_Stats_List.clear();
    MonsterRPGx_Traits_List.clear();
	MonsterRPGx_Achievements_List.clear();
    
	commandToServer('GetTraitData');
	$MonsterRPG::Client::TraitsCounted = 0;
	
    %rowid = -1;

    for(%i = 0; %i < $MonsterRPG::Client::TraitCount; %i++)
    {
        %rowid++;
        %name = strReplace($MonsterRPG::Client::Trait[%i, "Name"],"Trait_","");
		%desc = $MonsterRPG::Client::Trait[%i, "Description"];
		%reqLvl = $MonsterRPG::Client::Trait[%i, "levelReq"];
		%goldCost = $MonsterRPG::Client::Trait[%i, "goldCost"];
		%rank = $MonsterRPG::Client::Trait[%i, "rank"];
		
		switch(%rank)
		{
		    case 1:
		        %rank = "I";
			case 2:
		        %rank = "II";
			case 3:
		        %rank = "III";
			case 4:
		        %rank = "IV";
			case 5:
		        %rank = "V";
			case 6:
		        %rank = "VI";
		}
        
        MonsterRPGx_Traits_List.addRow(%i, %name SPC %rank, %rowid);
    }

	MonsterRPGx_Stats_List.addRow(0, "Mana: " SPC $MonsterRPG::Client::MaxMana, 0);
	MonsterRPGx_Stats_List.addRow(1, "Damage: " SPC $MonsterRPG::Client::Damage, 1);
	MonsterRPGx_Stats_List.addRow(2, "Health: " SPC $MonsterRPG::Client::MaxHP, 2);
}

function clientcmdClearMonsterRPGx_TraitsList()
{
    %this = MonsterRPGx_LocalStatsSO;

    %this.playersInCat["Stats"]  = 0;
    %this.playersInCat["Traits"]  = 0;
    %this.playersInCat["Achievements"] = 0;
    
    %this.tPlayersList = ""; // No trusted players
	%this.playersList["Stats"]  = "";
    %this.playersList["Traits"]  = "";
    %this.playersList["Achievements"] = "";
    
    %this.list();
}


function MonsterRPGx_LocalStatsSO::onListActed(%this,%list)
{
    %id = %list.getSelectedId ();
	%row = %list.getRowTextById (%id);
    MonsterRPGx_LocalStatsSO.list();
	MonsterRPGx_LocalStatsSO.lastSelectedRow = %row;
	
	%name = getField(%row, 0);
	%name = getWord(%name,0);
	%val = getWord(%name,1);
	
	if(%list $= MonsterRPGx_Traits_List)
	{
	    %name = "Trait_" @ %name;
        commandToServer('TraitView', %name);
	}
		
    if(%list $= MonsterRPGx_Stats_List)
	{
	    %name = strreplace(%name,":","");
	    commandToServer('StatView', %name);
	}
}

function MonsterRPGx_Stats::onWake(%this)
{
	MonsterRPGx_LocalStatsSO.list();
}

function MonsterRPGx_Stats::onSleep(%this)
{
    //test
}
