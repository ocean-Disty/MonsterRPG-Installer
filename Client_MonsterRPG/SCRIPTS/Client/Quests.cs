if(!isObject(MonsterRPGx_LocalQuestSO))
{
    %this = new ScriptObject(MonsterRPGx_LocalQuestSO);
    
    %this.maxCategories = 2;
    
	%this.category[0] = "DailyQuests";
    %this.category[1] = "MainQuests";
    %this.category[2] = "Information";
}


function clientCmdReceiveMainQuestData(%data)
{
    if(!$MonsterRPG::Client::QuestsCounted){$MonsterRPG::Client::QuestCount = 0; $MonsterRPG::Client::QuestsCounted = 1;}
	$MonsterRPG::Client::Quest[$MonsterRPG::Client::QuestCount, "Name"] = getField(%data,0);
    $MonsterRPG::Client::Quest[$MonsterRPG::Client::QuestCount, "questName"] = getField(%data,1);
	$MonsterRPG::Client::Quest[$MonsterRPG::Client::QuestCount, "Location"] = getField(%data,2);
	$MonsterRPG::Client::Quest[$MonsterRPG::Client::QuestCount, "Description"] = getField(%data,3);
	$MonsterRPG::Client::Quest[$MonsterRPG::Client::QuestCount, "goldReward"] = getField(%data,4);
	$MonsterRPG::Client::Quest[$MonsterRPG::Client::QuestCount, "expReward"] = getField(%data,5);
	$MonsterRPG::Client::Quest[$MonsterRPG::Client::QuestCount, "karmaReward"] = getField(%data,6);
	$MonsterRPG::Client::Quest[$MonsterRPG::Client::QuestCount, "fameReward"] = getField(%data,7);
	$MonsterRPG::Client::Quest[$MonsterRPG::Client::QuestCount, "Stage"] = getField(%data,8);
	$MonsterRPG::Client::QuestCount++;

	//Debounced re-render of the scroll-list quest panel once the burst ends (RPGPanels.cs).
	cancel($MRPG_QuestRenderSch);
	$MRPG_QuestRenderSch = schedule(120, 0, MRPG_renderQuests);
}

function MonsterRPGx_LocalQuestSO::list(%this)
{
    MonsterRPGx_MainQuests_List.clear();
    MonsterRPGx_DailyQuests_List.clear();
	MonsterRPGx_Information_List.clear();
    
	commandToServer('ServerCmdGetMainQuestData');
	$MonsterRPG::Client::QuestsCounted = 0;
	
	echo("Count: " @ $MonsterRPG::Client::QuestCount);
    %rowid = -1;

    for(%i = 0; %i < $MonsterRPG::Client::QuestCount; %i++)
    {
        %rowid++;
        %name = $MonsterRPG::Client::Quest[%i, "Name"];
		%questName = $MonsterRPG::Client::Quest[%i, "questName"];
		%loc = $MonsterRPG::Client::Quest[%i, "Location"];
		%desc = $MonsterRPG::Client::Quest[%i, "Description"];
		%goldReward = $MonsterRPG::Client::Quest[%i, "goldReward"];
		%expReward = $MonsterRPG::Client::Quest[%i, "expReward"];
		%karmaReward = $MonsterRPG::Client::Quest[%i, "karmaReward"];
		%fameReward = $MonsterRPG::Client::Quest[%i, "fameReward"];
		%stage = $MonsterRPG::Client::Quest[%i, "Stage"];
		
		switch(%stage)
		{
		    case 1:
		        %stage = "I";
			case 2:
		        %stage = "II";
			case 3:
		        %stage = "III";
			case 4:
		        %stage = "IV";
			case 5:
		        %stage = "V";
			case 6:
		        %stage = "VI";
		}
        
        MonsterRPGx_MainQuests_List.addRow(%i, %name SPC %stage, %rowid);
    }
}

function clientcmdClearMonsterRPGx_QuestsList()
{
    %this = MonsterRPGx_LocalQuestSO;

    %this.playersInCat["MainQuests"]  = 0;
    %this.playersInCat["DailyQuests"]  = 0;
    %this.playersInCat["Information"] = 0;
    
    %this.tPlayersList = ""; // No trusted players
	%this.playersList["MainQuests"]  = "";
    %this.playersList["DailyQuests"]  = "";
    %this.playersList["Information"] = "";
    
    %this.list();
}


function MonsterRPGx_LocalQuestSO::onListActed(%this,%list)
{
    %id = %list.getSelectedId ();
	%row = %list.getRowTextById (%id);
    MonsterRPGx_LocalQuestSO.list();
	MonsterRPGx_LocalQuestSO.lastSelectedRow = %row;
	
	%name = getWord(%row, 0);
	%stage = getWord(%row,1);
	
	switch$(%stage)
	{
		case "I":
		    %stage = 1;
		case "II":
		    %stage = 2;
		case "III":
		    %stage = 3;
		case "IV":
		    %stage = 4;
		case "V":
		    %stage = 5;
		case "VI":
		    %stage = 6;
	}

    if(%list $= MonsterRPGx_MainQuests_List)
	{
	    commandToServer('QuestView', %name, %stage);
	}
}

function MonsterRPGx_Quest::onWake(%this)
{
	MonsterRPGx_LocalQuestSO.list();
}

function MonsterRPGx_Quest::onSleep(%this)
{
    //test
}
