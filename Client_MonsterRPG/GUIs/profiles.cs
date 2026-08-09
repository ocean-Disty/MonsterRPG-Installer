///BURNT GLASS GOLD PROFILES
//
// The profile NAMES still say "BurntGlassBlue" on purpose - they are referenced
// 176 times across the container and main .gui files and renaming them buys
// nothing. What changed is what they point at: $Pref::Client::CurrentTheme now
// resolves to THEMES/Theme_BurntGlass_Gold (set in SCRIPTS/Client/GUIFunctions.cs).
//
// The font colours below had to move with it. The skin's button and tab faces used
// to be near-white glass, so near-black labels sat on them at about 9:1. The gold
// faces are dark - Button1_n is #6B5527 - and the same labels drop to 2.3:1, which
// is unreadable. Warm white measures 6.7:1 on the button face, 6.0:1 on an
// inactive tab and 4.6:1 on the worst case (the lighter active-tab face), so it
// clears AA everywhere while still reading warm against the gold rather than
// glaring like pure white.
//
// fontColors[1..4] are deliberately NOT touched: those back the \c1..\c4 escape
// codes used inside message text, so they carry meaning rather than styling.


new GuiControlProfile(MonsterRPGx_BurntGlassBlue_ButtonProfile : blockButtonProfile)
{
	fontColor = "255 248 230 255";
	// Set explicitly rather than inherited - the parent's highlight/disabled
	// colours are dark, and the hover face (Button1_h) is the lightest gold in the
	// set, so an inherited dark would be the one state that fails.
	fontColorHL = "255 255 255 255";
	fontColorNA = "168 150 112 255";
	fontType = "Verdana Bold";
	fontSize = "15";
	justify = "Center";
	fontColors[1] = "100 100 100";
	fontColors[2] = "30 30 30 150";  
	fontColors[3] = "0 0 255"; 
	fontColors[4] = "255 255 0"; 
	fontColorLink = "60 60 60 255";
	fontColorLinkHL = "0 0 0 255";
};

new GuiControlProfile(MonsterRPGx_BurntGlassBlue_TabTextProfile)
{
	fontColor = "255 248 230 255";
	fontType = "Verdana Bold";
	fontSize = "16";
	justify = "Left";
	fontColors[1] = "100 100 100";
	fontColors[2] = "0 255 0";  
	fontColors[3] = "0 0 255"; 
	fontColors[4] = "255 255 0"; 
	fontColorLink = "60 60 60 255";
	fontColorLinkHL = "0 0 0 255";
};

// The "blue" in the name was literal - this was pure #0000FF text, readable only
// because the active tab used to be white. On the gold active tab no blue clears
// AA, and neither does a gold-on-gold label (the best, #FFE296, measures 3.9:1).
// Active vs inactive is already carried by the bitmap - Tab1use has a lighter face
// and a gold top accent - so the text just needs to be legible.
new GuiControlProfile(MonsterRPGx_BurntGlassBlue_TabBlueTextProfile)
{
	fontColor = "255 248 230 255";
	fontType = "Verdana Bold";
	fontSize = "16";
	justify = "Left";
	fontColors[1] = "100 100 100";
	fontColors[2] = "0 255 0";  
	fontColors[3] = "0 0 255"; 
	fontColors[4] = "255 255 0"; 
	fontColorLink = "60 60 60 255";
	fontColorLinkHL = "0 0 0 255";
};

new GuiControlProfile(MonsterRPGx_BurntGlassBlue_TextProfile)
{
	fontColor = "30 30 30 255";
	fontType = "Arial Bold";
	fontSize = "15";
	justify = "Left";
	fontColors[1] = "100 100 100";
	fontColors[2] = "0 255 0";  
	fontColors[3] = "0 0 255"; 
	fontColors[4] = "255 255 0"; 
	fontColorLink = "60 60 60 255";
	fontColorLinkHL = "0 0 0 255";
};

new GuiControlProfile(MonsterRPGx_BurntGlassBlue_TextProfileVerdana)
{
	fontColor = "30 30 30 255";
	fontType = "Verdana Bold";
	fontSize = "15";
	justify = "Left";
	fontColors[1] = "100 100 100";
	fontColors[2] = "0 255 0";  
	fontColors[3] = "0 0 255"; 
	fontColors[4] = "255 255 0"; 
	fontColorLink = "60 60 60 255";
	fontColorLinkHL = "0 0 0 255";
};

new GuiControlProfile(MonsterRPGx_BurntGlassBlue_CheckBoxProfile : GuiCheckBoxProfile)
{
	bitmap = $Pref::Client::CurrentTheme  @ "torqueCheck.png";
	fontColor = "30 30 30 255";
	fontType = "Verdana Bold";
	fontSize = "15";
};

new GuiControlProfile(MonsterRPGx_BurntGlassBlue_RadioProfile : GuiRadioProfile)
{
	bitmap = $Pref::Client::CurrentTheme  @ "torqueRadio.png";
	fontColor = "30 30 30 255";
	fontType = "Verdana Bold";
	fontSize = "15";
};

new GuiControlProfile(MonsterRPGx_BurntGlassBlue_ScrollProfile : GuiScrollProfile)
{
	bitmap = $Pref::Client::CurrentTheme  @ "blockScroll.png";
};

//Dark, textured scroll for the Stats/Skills/Traits panels (RPGPanels.cs).
//opaque=false lets the dark RTS_Bar wood texture already behind it show
//through instead of a white fill, and darkblockScroll gives the arrows/thumb
//a dark brown-grey look.
new GuiControlProfile(MRPG_PanelScrollProfile : GuiScrollProfile)
{
	opaque = false;
	border = 0;
	bitmap = "Add-Ons/Client_MonsterRPG/GUIs/darkblockScroll.png";
};

new GuiControlProfile(MonsterRPGx_BurntGlassBlue_HalfScrollProfile : colorScrollProfile)
{
	bitmap = $Pref::Client::CurrentTheme  @ "halfScroll.png";
};

new GuiControlProfile(MonsterRPGx_BurntGlassBlue_TextListProfile : GuiTextListProfile)
{
    fontColor = "30 30 30 255";
    fontType = "Verdana Bold";
	fontSize = "13";
};

new GuiControlProfile(MonsterRPGx_BurntGlassBlue_PopUpMenuProfile : GuiPopupMenuProfile)
{
	fontSize = "15";
};

new GuiControlProfile(MonsterRPGx_BurntGlassBlue_ProgressProfile : GuiProgressProfile)
{
	border = 0;
	fillColor = "90 230 70 255";
};

new GuiControlProfile(MonsterRPGx_BurntGlassBlue_WindowProfile : GuiWindowProfile)
{
	bitmap = $Pref::Client::CurrentTheme  @ "blockWindow.png";
	// GuiWindowCtrl::onRender fills the whole client rect with this before any
	// child draws, and the container panel does not quite cover it - the three
	// pixels between the bottom of the title bar and the top of the genericBG
	// bitmap stay visible. GuiWindowProfile's stock fill is "200 200 200", which
	// would put a light grey seam across the top of every gold window.
	fillColor = "38 28 14 255";
	fontType = "Verdana Bold";
	fontSize = "16";
	textOffset = "5 3";
	justify = "Center";
};

new GuiControlProfile(MonsterRPGx_BurntGlassBlue_TextEditProfile : GuiTextEditProfile)
{
	fontType = "Verdana Bold";
	fontSize = "15";
	justify = "Center";
};




//DARK PROFILES

new GuiControlProfile(MonsterRPGx_Dark_ScrollProfile : GuiScrollProfile)
{
    fillColor = "100 100 100 50";
	bitmap = "Add-Ons/Client_MonsterRPG/GUIs/darkblockScroll.png";
};

new GuiControlProfile(MonsterRPGx_Dark_TextEditProfile : GuiTextEditProfile)
{
	fontColor = "220 220 220 100";
    fontType = "Verdana Bold";
	fontSize = "15";
	justify = "Center";
	fillColor = "100 100 100 50";
};

new GuiControlProfile(MonsterRPGx_Dark_GuiMLTextCtrl : GuiMLTextCtrl)
{
    fontColor = "220 220 220 100";
    fontType = "Verdana Bold";
    fontSize = "15";
    fillColor = "100 100 100 50";
	justify = "Left";
    wordWrap = 1;
};

new GuiControlProfile(MonsterRPGx_Dark_GuiTextListCtrl : GuiTextListCtrl)
{
    fontColor = "220 220 220 100";
    fontType = "Verdana Bold";
	fontSize = "15";
    fillColor = "100 100 100 50";
	bitmap = "Add-Ons/Client_MonsterRPG/GUIs/darkblockScroll.png";
	wordWrap = 1;
};


// New Rpg PROFILES

new GuiControlProfile(MonsterRPGx_GoldWhiteProfile : GuiWindowProfile)
{
	bitmap = "Add-Ons/Client_MonsterRPG/GUIs/MonsterRPG_Windows/blockWindow.png";
	fontType = "Verdana Bold";
	fontSize = "16";
	textOffset = "5 3";
	justify = "Center";
};


// Invisible drag PROFILES

new GuiControlProfile(MonsterRPGx_InvisibleDrag : GuiWindowProfile)
{
	opaque = false;
	border = 0;
	fillColor = "0 0 0 0";
};


///LARGE TEXT PROFILES


new GuiControlProfile(MonsterRPGx_BGLarge_TextProfile)
{
	fontColor = "30 30 30 255";
	fontType = "Verdana Bold";
	fontSize = "26";
	justify = "Left";
	fontColors[1] = "100 100 100";
	fontColors[2] = "0 255 0";  
	fontColors[3] = "0 0 255"; 
	fontColors[4] = "255 255 0"; 
	fontColorLink = "60 60 60 255";
	fontColorLinkHL = "0 0 0 255";
};

new GuiCursor(MonsterRPGCursorStandard)
{
   hotSpot = "1 1";
   renderOffset = "0 0";
   bitmapName = "Add-Ons/Client_MonsterRPG/GUIs/Cursor_Sizes/CursorStandard.png";
};

new GuiCursor(MonsterRPGCursor4k)
{
   hotSpot = "1 1";
   renderOffset = "0 0";
   bitmapName = "Add-Ons/Client_MonsterRPG/GUIs/Cursor_Sizes/Cursor4k.png";
};

new GuiCursor(MonsterRPGCursorSmall)
{
   hotSpot = "1 1";
   renderOffset = "0 0";
   bitmapName = "Add-Ons/Client_MonsterRPG/GUIs/Cursor_Sizes/CursorSmall.png";
};

new GuiControlProfile (MyCustomButtonProfile)
{
   opaque = 1; // This makes the button fill with the bitmap
   border = 0; // No border if you're relying on the image itself
   fillColor = "255 255 255"; // Fallback color if the image isn't available
   fontColor = "0 0 0"; // Text color if your button has a label
   fontColorHL = "255 255 255"; // Text color when hovered
   bitmap = "Add-Ons/Client_MonsterRPG/GUIs/Button_Elements/Button_middle.png"; // The path to your custom bitmap image
   hasBitmapArray = 1; // This tells the engine to use different sections of the image for button states
};

new GuiBitmapButtonCtrl(MyCustomButton)
{
   position = "100 100"; // X, Y position of the button
   extent = "50 50"; // Size of the button (make sure this matches the size of a single button state in the bitmap)
   profile = MyCustomButtonProfile; // Use the profile we created earlier
   command = "echo('Button Pressed');"; // Command to run when the button is clicked
   text = "Click Me"; // Optional: Text to display on the button
};
