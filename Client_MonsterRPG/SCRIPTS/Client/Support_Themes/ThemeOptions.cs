function ThemeList::onWake(%this)
{
	%this.loadingList = true;
	if(!%this.generatedList)
	{
		%index = 1;
		%mask = "Add-Ons/System_RPGsExtended/Client/Support_Themes/THEMES/Theme_*/description.txt";
		for(%file = findFirstFile(%mask); %file !$= ""; %file = findNextFile(%mask))
		{
			%path = filePath(%file);
			%name = strReplace(getSubStr(%path, 14, strLen(%path)), "_", " ");
			%this.addFront(%name, %index);
			%index ++;
		}
		%this.generatedList = true;
	}
	if(Themes.themePath $= "")
		%this.setSelected(0);
	else
	{
		%path = Themes.themePath;
		%themeName = strReplace(getSubStr(%path, 14, strLen(%path)), "_", " ");
		%count = %this.size();
		for(%i = 1; %i < %count; %i ++)
		{
			%item = %this.getTextByID(%i);
			if(%item $= %themeName)
			{
				%this.setSelected(%i);
				break;
			}
		}
	}
	%this.loadingList = false;
}

function ThemeLayerList::onWake(%this)
{
	%this.update();
}

function ThemeLayerList::update(%this)
{
	%this.clear();
	for(%i = 0; %i < $Pref::Themes::ThemeLayerCount; %i ++)
	{
		%this.addRow(%i, $Pref::Themes::ThemeLayer[%i]);
	}
}

function ThemeOptions::clickApply(%this)
{
	Themes.restoreTheme();
	Themes.applyTheme();
	canvas.popDialog(%this);
}

function ThemeOptions::clickAddTheme(%this)
{
	if(ThemeList.loadingList)
		return;
	%index = ThemeList.getSelected();
	%path = ThemeList.getTextByID(%index);
	if(%path $= "Default")
		%path = "";
	else
		%path = "Add-Ons/System_RPGsExtended/Client/Support_Themes/THEMES/Theme_" @ strReplace(%path, " ", "_");
	for(%i = 0; %i < $Pref::Themes::ThemeLayerCount; %i ++)
	{
		%layer = $Pref::Themes::ThemeLayer[%i];
		if(%layer $= %path)
		{
			MessageBoxOK("Error", "That theme is already in use!");
			return;
		}
	}
	$Pref::Themes::ThemeLayer[$Pref::Themes::ThemeLayerCount] = %path;
	$Pref::Themes::ThemeLayerCount ++;
	ThemeLayerList.update();
}

function ThemeOptions::clickRemoveTheme(%this)
{
	%index = ThemeLayerList.getRowNumByID(ThemeLayerList.getSelectedID());
	if(%index < 0)
		return;
	for(%i = %index + 1; %i < $Pref::Themes::ThemeLayerCount; %i ++)
		$Pref::Themes::ThemeLayer[%i - 1] = $Pref::Themes::ThemeLayer[%i];
	$Pref::Themes::ThemeLayerCount --;
	$Pref::Themes::ThemeLayer[$Pref::Themes::ThemeLayerCount] = "";
	ThemeLayerList.update();
}

function ThemeOptions::clickUp(%this)
{
	%index = ThemeLayerList.getRowNumByID(ThemeLayerList.getSelectedID());
	if(%index <= 0)
		return;
	%layer = $Pref::Themes::ThemeLayer[%index];
	$Pref::Themes::ThemeLayer[%index] = $Pref::Themes::ThemeLayer[%index - 1];
	$Pref::Themes::ThemeLayer[%index - 1] = %layer;
	ThemeLayerList.update();
	ThemeLayerList.setSelectedRow(%index - 1);
}

function ThemeOptions::clickDown(%this)
{
	%index = ThemeLayerList.getRowNumByID(ThemeLayerList.getSelectedID());
	if(%index < 0 || %index >= $Pref::Themes::ThemeLayerCount - 1)
		return;
	%layer = $Pref::Themes::ThemeLayer[%index];
	$Pref::Themes::ThemeLayer[%index] = $Pref::Themes::ThemeLayer[%index + 1];
	$Pref::Themes::ThemeLayer[%index + 1] = %layer;
	ThemeLayerList.update();
	ThemeLayerList.setSelectedRow(%index + 1);
}