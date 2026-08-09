function MonsterRPGx_SelectCell(%cellInv,%cellNum)
{
	if(MonsterRPGx_Main.isHoldingShift)
	{
		commandToServer('MonsterRPGx_QuickTransfer',%cellInv,%cellNum);
		canvas.popDialog(MonsterRPGx_Transfer);
		
		//Hide Info Window
		if(isObject(%relCell_infoSw = "MonsterRPGx_" @ getWord(MonsterRPGx_Main.prevCell,0) @ "_InfoParent_" @ getWord(MonsterRPGx_Main.prevCell,1)))
			%relCell_infoSw.setVisible(false);
		
		MonsterRPGx_Main.prevCell = "";
		MonsterRPGx_Main.currCell = "";
		MonsterRPGx_Main.prevSelTime = "";
		
		return;
	}
	
	if(MonsterRPGx_Main.isHoldingInspect)
	{
		commandToServer('GetInspectData',%cellNum);
		
		return;
	}
	
	//////////////////////////////////////////////////
			
	if(MonsterRPGx_Main.prevCell $= "")
	{
		%relCell_icon = "MonsterRPGx_" @ %cellInv @ "_ItemIcon_" @ %cellNum;
		
		if(%relCell_icon.tool > 0) //if(%relCell_icon.tool !$= "") //if(strStr(%relCell_icon.bitmap,"noItem") == -1)
		{
			MonsterRPGx_Main.prevCell = %cellInv SPC %cellNum;
			MonsterRPGx_Main.prevSelTime = getSimTime();
			
			%relCell_infoSw = "MonsterRPGx_" @ %cellInv @ "_InfoParent_" @ %cellNum;
			%relCell_infoSw.setVisible(true);
		}
	}
	else
	{
		MonsterRPGx_Main.currCell = %cellInv SPC %cellNum;
		
		if(MonsterRPGx_Main.currCell !$= MonsterRPGx_Main.prevCell)
		{
			%cellInvA = getWord(MonsterRPGx_Main.prevCell,0);
			%cellNumA = getWord(MonsterRPGx_Main.prevCell,1);
			%cellInvB = getWord(MonsterRPGx_Main.currCell,0);
			%cellNumB = getWord(MonsterRPGx_Main.currCell,1);
			
			commandToServer('MonsterRPGx_SwapCells',%cellInvA,%cellNumA,%cellInvB,%cellNumB);
		}
		else
		{
			if((getSimTime() - MonsterRPGx_Main.prevSelTime) < 250)
			{
				%cellInv = getWord(MonsterRPGx_Main.prevCell,0);
				%cellNum = getWord(MonsterRPGx_Main.prevCell,1);
				
				commandToServer('MonsterRPGx_AutoStack',%cellInv,%cellNum);
			}
		}
		
		canvas.popDialog(MonsterRPGx_Transfer);
		
		//Hide Info Window
		if(isObject(%relCell_infoSw = "MonsterRPGx_" @ getWord(MonsterRPGx_Main.prevCell,0) @ "_InfoParent_" @ getWord(MonsterRPGx_Main.prevCell,1)))
			%relCell_infoSw.setVisible(false);
		
		MonsterRPGx_Main.prevCell = "";
		MonsterRPGx_Main.currCell = "";
		MonsterRPGx_Main.prevSelTime = "";
	}
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

function MonsterRPGx_AuxSelect(%cellInvB,%cellNumB)
{
	if(MonsterRPGx_Main.prevCell $= "")	
		return;
	if(MonsterRPGx_Main.prevCell $= (%cellInvB SPC %cellNumB))
	{
		MonsterRPGx_SelectCell(%cellInvB,%cellNumB);
		return;
	}
	%cellInvA = getWord(MonsterRPGx_Main.prevCell,0);
	%cellNumA = getWord(MonsterRPGx_Main.prevCell,1);
	
	//Item Stack Amount
	%relCell_stackAm = "MonsterRPGx_" @ %cellInvA @ "_InfoTxtStackAm_" @ %cellNumA;
	%stackAm = %relCell_stackAm.getText();
	%stackAm = strReplace(%stackAm,"<font:impact:16><just:right><color:ffffff>","");
	
	//Item Stack Limit (in info window)
	%relCell_stackLm = "MonsterRPGx_" @ %cellInvA @ "_InfoTxtStackLm_" @ %cellNumA;
	%stackLm = %relCell_stackLm.getText();
	%stackLm = strReplace(%stackLm,"<font:impact:16><color:00dd00>","");
	
	%stackLm = mClamp(%stackLm,1,999);
	%stackAm = mClamp(%stackAm,1,%stackLm);
	
	if(%stackAm == 1)
	{
		commandToServer('MonsterRPGx_TransAmount',%cellInvA,%cellNumA,%cellInvB,%cellNumB,1);
		
		//Hide Info and Transfer Windows
		%relCell_info = "MonsterRPGx_" @ %cellInvA @ "_InfoParent_" @ %cellNumA;
		%relCell_info.setVisible(false);
		
		MonsterRPGx_Main.prevCell = "";
		MonsterRPGx_Main.currCell = "";
		MonsterRPGx_Main.prevSelTime = "";
		
		return;
	}

	if(%stackAm > 2)
		MonsterRPGx_TransAm_Sldr.ticks = getMax(%stackAm - 2,1);
	else
		MonsterRPGx_TransAm_Sldr.ticks = 0;
	MonsterRPGx_TransAm_Sldr.range = 1 SPC %stackAm;
	MonsterRPGx_TransAm_Sldr.setValue(1);
	MonsterRPGx_TransAm_Edt.setValue(1);
	
	MonsterRPGx_Main.currCell = %cellInvB SPC %cellNumB;
	canvas.pushDialog(MonsterRPGx_Transfer);
}