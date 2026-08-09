exec("./ThemeOptions.cs");
exec("./ThemeOptions.gui");

if($Pref::Themes::DefaultPrefVersion !$= "1.0.0")
{
	if($Pref::Themes::DefaultPrefVersion $= "0.1.0")
	{
		if($Pref::Themes::CurrentTheme $= "")
		{
			$Pref::Themes::ThemeLayerCount = 0;
		}
		else
		{
			$Pref::Themes::ThemeLayer[0] = $Pref::Themes::CurrentTheme;
			$Pref::Themes::ThemeLayerCount = 1;
			deleteVariables("$Pref::Themes::CurrentTheme");
		}
	}	
	$Pref::Themes::DefaultPrefVersion = "1.0.0";
}

$Themes::DefaultThemePath = "base/client/ui";

//Lists all themes currently affecting the GUI.
function dumpActiveThemes()
{
	echo("Active theme layers:");
	for(%i = 0; %i < $Pref::Themes::ThemeLayerCount; %i ++)
	{
		echo(" (" @ %i + 1 @ ")" SPC $Pref::Themes::ThemeLayer[%i]);
	}
}

//Applies a theme to all GUIs.
//@param	string themePath	Optional - Use theme files from a specific location.
function Themes::applyTheme(%this, %themePath)
{
	%restore = (%themePath $= "&DEFAULT");
	if(%restore)
	{
		echo("Restoring default theme.");
	}
	else if($Pref::Themes::ThemeLayerCount > 0)
	{
		dumpActiveThemes();
	}
	else
	{
		echo("No theme selected!");
		return;
	}
	
	%startTime = getRealTime();

	%this.setCanvasReady(true);
	%this.themeApplying = true;

	if(%restore)
	{
		%this.restoreGuiTree(GuiGroup);
		%this.restoreCursor();
	}
	else
	{
		for(%i = 0; %i < $Pref::Themes::ThemeLayerCount; %i ++)
		{
			%layer = $Pref::Themes::ThemeLayer[%i];
			%profiles = %layer @ "/profiles.cs";
			if(isFile(%profiles) && !%this.loadedProfiles[%profiles])
			{
				%this.loadedProfiles[%profiles] = true;
				exec(%profiles);
			}
		}

		%this.themeGuiTree(GuiGroup, %themePath);
		%this.themeCursor(%themePath);

		for(%i = 0; %i < $Pref::Themes::ThemeLayerCount; %i ++)
		{
			%layer = $Pref::Themes::ThemeLayer[%i];
			%tweaks = %layer @ "/tweaks.cs";
			if(isFile(%tweaks))
			{
				exec(%tweaks);
				%restartNext = true;
			}
		}
	}

	%this.themeApplying = false;
	%this.setCanvasReady(false);
	
	Themes.showThemeMessage = true;

	if(%this.restartRequired)
	{
		messageBoxYesNo(
			"Restart Required",
			"The theme has been applied, but a restart may be required before all elements display properly." @
				"\n\n<font:Arial Bold:14>Close Blockland now?",
			"quit();");
	}
	if(%restartNext)
	{
		%this.restartRequired = true;
	}
	
	%elapsed = getRealTime() - %startTime;
	if(%elapsed < 0)
		%elapsed = 0;
	echo("Took" SPC %elapsed @ "ms to" SPC (%restore ? "restore default theme." : "apply all theme layers."));
}

//Restores to the default theme.
function Themes::restoreTheme(%this)
{
	%this.applyTheme("&DEFAULT");
}

//Sets whether the canvas is ready to have a theme applied to it.
//@param	bool canvasReady
function Themes::setCanvasReady(%this, %canvasReady)
{
	if(%canvasReady && !%this.canvasReady)
	{
		%this.canvasReady = true;
		ThemeBlankMessage.setVisible(%this.showThemeMessage);
		%this.restoreContent = canvas.getObject(0);
		canvas.setContent(ThemeBlank);
		canvas.repaint();
	}
	else if(!%canvasReady && %this.canvasReady)
	{
		%this.canvasReady = false;
		if(!isObject(%this.restoreContent))
			canvas.setContent(MainMenuGui);
		canvas.setContent(%this.restoreContent);
	}
}

//Applies a theme to the entire tree of %gui's child objects.
//@param	SimSet gui
//@param	string themePath
function Themes::themeGuiTree(%this, %gui, %themePath)
{
	for(%i = 0; %i < %gui.getCount(); %i ++)
	{
		%child = %gui.getObject(%i);
		if(!%child.themeIgnore)
		{
			%this.themeGui_profile(%child, %themePath);
			%this.themeGui_bitmap(%child, %themePath);
			if(%child.getCount() > 0)
			{
				%this.themeGuiTree(%child, %themePath);
			}
		}
	}
}

//Applies a theme (including profile and bitmap) to a specific GUI object.
function Themes::themeGui(%this, %gui, %themePath)
{
	if(isObject(%gui))
	{
		%this.themeGui_profile(%gui, %themePath);
		%this.themeGui_bitmap(%gui, %themePath);
	}
}

//Applies a theme (profile only) to a specific GUI object.
//@param	GuiControl gui
//@param	string themePath
function Themes::themeGui_profile(%this, %gui, %themePath)
{
	%profile = %gui.profile;
	if(!isObject(%profile))
	{
		return;
	}
	
	%profileName = %profile.getName();
	%defaultBitmap = %profile.bitmap;
	
	if(%themePath $= "")
	{
		for(%i = 0; %i < $Pref::Themes::ThemeLayerCount; %i ++)
		{
			%tLayer = $Pref::Themes::ThemeLayer[%i];
			%tBitmap = %this.getThemeBitmap(%tLayer, %defaultBitmap);
			%tName = %this.getThemeName(%tLayer);
			%tProfileName = %tName @ "__" @ %profileName;
			%tProfile = nameToID(%tProfileName);
			if(%tBitmap !$= "" || isObject(%tProfile))
			{
				%themePath = %tLayer;
				%themeBitmap = %tBitmap;
				%themeName = %tName;
				%themeProfileName = %tProfileName;
				%themeProfile = %tProfile;
			}
		}
	}
	else
	{
		%themeBitmap = %this.getThemeBitmap(%themePath, %defaultBitmap);
		%themeName = %this.getThemeName(%themePath);
		%themeProfileName = %themeName @ "__" @ %profileName;
		%themeProfile = nameToID(%themeProfileName);
	}
	
	if(%profile.themePath $= %themePath)
	{
		%themeProfileName = %profileName;
		%themeProfile = %profile;
	}
	
	if(!isObject(%themeProfile))
	{
		if(%themeBitmap $= "")
		{
			return;
		}
	
		%profile.setName("ThemeParentProfile");
		%themeProfile = new GuiControlProfile(ThemeNewProfile : ThemeParentProfile)
		{
			bitmap = %themeBitmap;
			parent = %profile;
			themePath = %themePath;
		};
		if(!isObject(%themeProfile))
		{
			error("ERROR: Unable to create theme profile" SPC %themeProfileName);
			return;
		}
		ThemeData.add(ThemeNewProfile);
		ThemeNewProfile.setName(%themeProfileName);
		ThemeParentProfile.setName(%profileName);
	}
	
	if(%themeProfile != %profile)
	{
		%gui.themeApplying = true;
		
		%gui.setProfile(%themeProfile);
		if(!%this.themeApplying)
		{
			cancel(canvas.repaintSchedule);
			canvas.repaintSchedule = canvas.schedule(0, "repaint");
		}
		if(!%gui.themed)
		{
			%gui.defaultProfile = %profile;
		}
		
		%gui.themeApplying = false;
	}
	
	%gui.themed = true;
}

//Applies a theme (bitmap only) to a specific GUI object.
//@param	GuiControl gui
//@param	string themePath
function Themes::themeGui_bitmap(%this, %gui, %themePath)
{
	if(!isFunction(%gui.getClassName(), "setBitmap"))
	{
		return;
	}
	
	%bitmap = %gui.bitmap;
	
	if(%themePath $= "")
	{
		for(%i = 0; %i < $Pref::Themes::ThemeLayerCount; %i ++)
		{
			%tLayer = $Pref::Themes::ThemeLayer[%i];
			%tBitmap = %this.getThemeBitmap(%tLayer, %bitmap);
			if(%tBitmap !$= "" || isObject(%tProfile))
			{
				%themePath = %tLayer;
				%themeBitmap = %tBitmap;
			}
		}
	}
	else
	{
		%themeBitmap = %this.getThemeBitmap(%themePath, %bitmap);
	}
	
	if(%themeBitmap !$= "")
	{
		%gui.themeApplying = true;
		
		if(!%gui.themedBitmap)
		{
			%gui.defaultBitmap = %gui.bitmap;
		}
		%gui.setBitmap(%themeBitmap);
		%gui.themedBitmap = true;
		
		%gui.themeApplying = false;
	}

	%gui.themed = true;
}

//Sets the cursor to the theme's custom cursor.
//@param	string themePath
function Themes::themeCursor(%this, %themePath)
{
	if(%themePath $= "")
	{
		for(%i = 0; %i < $Pref::Themes::ThemeLayerCount; %i ++)
		{
			%layer = $Pref::Themes::ThemeLayer[%i];
			if(isFile(%layer @ "/CUR_3darrow.png"))
			{
				%themePath = %layer;
			}
		}
	}

	%cursorFile = %themePath @ "/CUR_3darrow.png";
	if(strLen(%themePath) && isFile(%cursorFile))
	{
		%name = %this.getThemeName(%themePath) @ "_Cursor";
		%cursor = nameToID(%name);
		if(!isObject(%cursor))
		{
			%cursor = new GuiCursor(%name)
			{
				bitmapName = %cursorFile;
				hotspot = "0 0";
			};
		}
		canvas.setCursor(%cursor);
	}
}

//Restores the cursor to its default state.
function Themes::restoreCursor(%this)
{
	if(isObject(DefaultCursor))
		canvas.setCursor(DefaultCursor);
}

//Restores an entire tree of GUI objects to its default state.
//@param	SimSet gui
function Themes::restoreGuiTree(%this, %gui)
{
	for(%i = 0; %i < %gui.getCount(); %i ++)
	{
		%child = %gui.getObject(%i);
		if(!%child.themeIgnore)
		{
			%this.restoreGui(%child);
			if(%child.getCount() > 0)
			{
				%this.restoreGuiTree(%child);
			}
		}
	}
}

//Restores a specific GUI to its default state.
//@param	GuiControl gui
function Themes::restoreGui(%this, %gui)
{
	if(!%gui.themed)
		return;
	
	%gui.themeApplying = true;
	
	if(isObject(%gui.defaultProfile))
	{
		%gui.setProfile(%gui.defaultProfile);
		if(!%this.themeApplying)
		{
			cancel(canvas.repaintSchedule);
			canvas.repaintSchedule = canvas.schedule(0, "repaint");
		}
	}
	%gui.defaultProfile = "";
	
	if(%gui.themedBitmap && isFunction(%gui.getClassName(), "setBitmap") && %gui.defaultBitmap !$= "")
	{
		%gui.setBitmap(%gui.defaultBitmap);
	}
	%gui.defaultBitmap = "";
	%gui.themedBitmap = "";
	
	%gui.themeApplying = "";
	%gui.themed = "";
}

//Given a default bitmap, returns the theme's replacement image.
//@param	string themePath
//@param	string bitmap
//@return	string	Returns the image path if found, "" if no image found.
function Themes::getThemeBitmap(%this, %themePath, %bitmap)
{
	if(!strLen(%bitmap))
		return "";

	//replace current path with theme path
	%themeBitmap = setSubStr(%bitmap, 0, strLen($Themes::DefaultThemePath), %themePath);
	
	//check if file exists
	if(%this.isThemeBitmap[%themeBitmap] $= "")
	{
		%this.isThemeBitmap[%themeBitmap] =
			isFile(%themeBitmap) ||
			isFile(%themeBitmap @ ".png") ||
			isFile(%themeBitmap @ ".jpg") ||
			isFile(findFirstFile(%themeBitmap @ "_*"));
	}
	if(!%this.isThemeBitmap[%themeBitmap])
		return "";
	return %themeBitmap;
}

//Finds the theme name for a theme path.
//@param	string themePath
//@return	string
function Themes::getThemeName(%this, %themePath)
{
	%themeName = stripChars(%themePath, "~`!@#$%^&*()-=+[{]}\\|;:\'\",<.>? ");
	%themeName = getField(strReplace(%themeName, "/", "\t"), 1);
	return %themeName;
}

//Checks whether a GUI should not be themed.
//@param	GuiControl gui
//@return	bool
function Themes_themeIgnore(%gui)
{
	if(%gui.themeIgnore)
		return true;
	else if(isObject(%parent = %gui.getGroup()))
		return Themes_themeIgnore(%parent);
	else
		return false;
}

//Called when a bitmap is changed.
//@param	GuiControl gui
//@param	string bitmap
function Themes_onSetBitmap(%gui, %bitmap)
{
	if(%gui.themed && !%gui.themeApplying && $Pref::Themes::ThemeLayerCount > 0 && !Themes_themeIgnore(%gui))
	{
		Themes.themeGui_bitmap(%gui);
	}
}

//create a dummy function so that I can parent it properly
if(!isFunction("GuiControl", "onAdd"))
	eval("function GuiControl::onAdd(%this){}");

package Themes
{
	//@private
	function cursorOn()
	{
		parent::cursorOn();
		if($Pref::Themes::ThemeLayerCount > 0)
			Themes.themeCursor();
	}

	//@private
	function GuiControl::onAdd(%this)
	{
		parent::onAdd(%this);
		if($Pref::Themes::ThemeLayerCount > 0 && !Themes_themeIgnore(%this))
		{
			Themes.schedule(0, "themeGui", %this);
		}
	}

	//@private
	function GuiAnimatedBitmapCtrl::setBitmap(%this, %bitmap)
	{
		parent::setBitmap(%this, %bitmap);
		Themes_onSetBitmap(%this, %bitmap);
	}

	//@private
	function GuiBitmapButtonCtrl::setBitmap(%this, %bitmap)
	{
		parent::setBitmap(%this, %bitmap);
		Themes_onSetBitmap(%this, %bitmap);
	}

	//@private
	function GuiBitmapCtrl::setBitmap(%this, %bitmap)
	{
		parent::setBitmap(%this, %bitmap);
		Themes_onSetBitmap(%this, %bitmap);
	}

	//@private
	function GuiControlProfile::updateFont(%this)
	{
		parent::updateFont(%this);
		for(%i = 0; %i < $Pref::Themes::ThemeLayerCount; %i ++)
		{
			%themePath = $Pref::Themes::ThemeLayer[%i];
			%name = Themes.getThemeName(%themePath) @ "__" @ %this.getName();
			%profile = nameToID(%name);
			if(isObject(%profile))
			{
				%profile.fontSize = %this.fontSize;
				%profile.updateFont();
			}
		}
	}
	
	//Restore the default theme when working with the GUI editor.
	//@private
	function GuiEdit(%state)
	{
		if(%state)
		{
			parent::GuiEdit(%state);
		}
		else if(canvas.getObject(0) == nameToID(GuiEditorGUI) && $guiEditRestoreTheme)
		{
			parent::GuiEdit(%state);
			Themes.applyTheme();
			$guiEditRestoreTheme = false;
		}
		else if($Pref::Themes::ThemeLayerCount > 0)
		{
			$guiEditRestoreTheme = true;
			Themes.restoreTheme();
			parent::GuiEdit(%state);
		}
		else
		{
			parent::GuiEdit(%state);
		}
	}
};
activatePackage(Themes);

if(!isObject(Themes))
{
	new ScriptGroup(Themes)
	{
		showThemeMessage = false;
		
		new SimSet(ThemeData);
		new GuiControl(ThemeBlank)
		{
			profile = GuiDefaultProfile;
			position = "0 0";
			extent = "800 600";
			enabled = "1";
			visible = "1";
			themeIgnore = true;

			new GuiSwatchCtrl()
			{
				profile = "GuiDefaultProfile";
				horizSizing = "width";
				vertSizing = "height";
				position = "0 0";
				extent = "800 600";
				minExtent = "8 2";
				enabled = "1";
				visible = "1";
				clipToParent = "1";
				color = "0 0 0 255";

				new GuiMLTextCtrl(ThemeBlankMessage) {
					profile = "GuiMLTextProfile";
					horizSizing = "center";
					vertSizing = "center";
					position = "206 228";
					extent = "228 24";
					minExtent = "8 2";
					enabled = "1";
					visible = "1";
					clipToParent = "1";
					lineSpacing = "2";
					allowColorChars = "0";
					maxChars = "-1";
					text = "<just:center><color:ffffff><font:Arial Bold:24>Applying theme...";
					maxBitmapHeight = "-1";
					selectable = "0";
					autoResize = "1";
				};
			};
		};
	};
	if($Pref::Themes::ThemeLayerCount > 0)
	{
		Themes.schedule(0, "applyTheme");
	}
}