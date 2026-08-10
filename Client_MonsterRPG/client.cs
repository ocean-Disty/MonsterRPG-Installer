// THE SERVER GATE - FIRST, before anything that might want to ask.
//
// This add-on is loaded on EVERY server the player joins, so every module in it
// has to be able to answer "am I on a MonsterRPG server?" before it does
// anything. ServerGate.cs owns that question (MRPG_isActive), owns the two
// functions that start and stop the whole add-on (MRPG_ClientEnter /
// MRPG_ClientLeave), and documents the four rules the rest of these files follow.
//
// Read it before adding a schedule, a build, or a commandToServer to any of them.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/ServerGate.cs");

//Preloading profiles cause why not
exec("Add-Ons/Client_MonsterRPG/GUIs/profiles.cs");
//

// Copy the body skins next to m.dts. Must run BEFORE the character screen, which
// hands a skin root to GuiObjectView::setObject the moment it opens.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/SkinDeploy.cs");


//exec("add-ons/Client_MonsterRPG/client.cs");

// KEYBIND LAYER - before every screen that binds a key, because they all call
// MRPG_bindDefault from it now. It also moves Super Shift off Left Alt so free
// mouse can have it. See the header of Keybinds.cs: add-ons load AFTER
// config/client/config.cs, so a plain moveMap.bind erases the player's remap on
// every launch, which is what made all of this unremappable in practice.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Keybinds.cs");

// LOADING COVER - first, so it can be raised before any other panel could open.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/LoadingScreen.cs");

exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/ClientCommands.cs");
// The clipboard half of /voice. setClipboard() only works on the machine that runs
// it and the server is a dedicated one, so the copy has to happen here. Order-free:
// one leaf clientCmd handler, no package, no GUI, no schedule.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/VoiceChat.cs");
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/InvCellFunctions.cs");
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/GUIFunctions.cs");

exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Party.cs");
//exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Stats.cs");

exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Quests.cs");
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/DamageStreak.cs");
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/NamePlates.cs");
//exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Inspect.cs"); //file does not exist - was causing "Missing file" error
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Package.cs");
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Support.cs");
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/RPGPanels.cs");
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Equipment.cs");
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Spells.cs");
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/SpellBar.cs");
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/TreeClient.cs"); // ability-tree screen (press K)
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Incantation.cs"); // spell incantation QTE (cast gate)
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/CharacterScreen.cs"); // medieval character screen (press N)
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/AttributeScreen.cs"); // spend attribute points (press J)
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/FormFX.cs"); // transformation screen FX + HUD icon
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Minimap.cs"); // dungeon minimap + fog of war; needs the HUD
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/CampPanel.cs"); // village/bandit-camp pool bar; lives INSIDE
                                                              // MAIN_INTERFACE, so it builds before Parent::
                                                              // clientCmdaddMonsterRPGGUI (Minimap builds after)
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Support_TMLParser.cs");
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Support_LibStr.cs");
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Support_LibAltStr.cs");
//exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Support_InputDetection/Client_KeyInputRelay.cs");
//exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Support_InputDetection/Client_RelayEyeVector.cs");
//exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Support_InputDetection/Client_HataInputCamSync.cs");
//exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Support_InputDetection/Client_HataLookCtrl.cs");
//exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Support_InputDetection/Client_HataCrosshair.cs");
//exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Support_Themes/Themes.cs");

//Hook in Server-side Items Control
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Support_InputDetection/Client_ItemsOverride.cs"); //fixed path - "./" resolved to the add-on root where this file does not exist

exec("Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGx_Main.gui");
// Container-split step 1: load the (currently marker-only) container file, then
// reparent its children into MonsterRPGx_Main so runtime topology is unchanged.
exec("Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGx_Containers.gui");
MonsterRPGx_MergeContainers();
// The corpse loot window is built in script rather than living in the 126k-line container
// file. Must run AFTER the merge: it parents itself into MonsterRPGx_Main, and the merge
// is what guarantees Main is the live container tree.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/CorpseWindow.cs");
MRPG_initCorpseWindow();
// Weapon/armour stat tooltip on inventory hover. Same reason for the ordering: it parents
// its panel into MonsterRPGx_Main.
//
// NO MRPG_initItemTip() HERE ANY MORE. That call built the panel and started a 60ms
// cursor poll at BOOT, which then ran for the whole session on every server. It is
// called from MRPG_ClientEnter() now - see ServerGate.cs.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/ItemTip.cs");
// Villager / quest dialogue window. Parents to Canvas rather than MonsterRPGx_Main - a
// conversation is not an inventory screen and must not require that dialog to be open.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Dialogue.cs");
MRPG_initDialogue();
// Equip / Quest / Spells tabs are now editable .gui files (were fully script-built).
// Their MRPG_build*() functions slot these next to the other tab swatches instead of
// building from scratch. (Party / Stats / Leaderboard frames are already static in
// MonsterRPGx_Main.gui; only their data-fed scroll rows stay script-built.)
exec("Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGx_Equip.gui");
exec("Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGx_Quest.gui");
exec("Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGx_Spells.gui");
exec("Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGx_Status.gui");
exec("Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGx_RenameGui.gui");
exec("Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGx_Transfer.gui");
exec("Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGx_LockPick.gui");
exec("Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGx_RecipeMngmt.gui");
//exec("Add-Ons/Client_MonsterRPG/GUIs/MonsterRPGx_Party.gui"); //file does not exist (only MonsterRPGx_OldParty.gui is present) - was causing "Missing file" error
exec("Add-Ons/Client_MonsterRPG/GUIs/Gui_HataCrosshair.gui");

//MSG BOX

// Bottom-left chat window (Global / Local / Events). LAST of the scripts on
// purpose: its package wraps clientCmdaddMRPGClientToServer, onServerMessage,
// newMessageHud::* and PlayGui::loadPaint, and a package only wraps what is
// already defined when it is activated - loading this before Package.cs or the
// stock huds would leave those chains truncated. It also needs
// GuiControl::getCanvasPosition from Support.cs.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/ChatPanel.cs");
// Click-a-name menu and the player card. AFTER ChatPanel.cs: it defines
// MRPGChatText::onURL for a control ChatPanel builds, and reuses its control
// helpers and colour palette.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/ChatProfile.cs");

// Native ray-traced audio. Defines one clientCmd handler and nothing else - no
// package, no schedule, no socket until a MonsterRPG server actually invites us.
// A player without MonsterRPGAudio.dll never has MRPGAudio_Connect defined and
// every function in it returns on its first line.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/AudioNative.cs");

// The green microphone indicator. AFTER AudioNative.cs, whose MRPGAudio_VoiceStat
// it polls, and it needs MonsterRPGx_MAIN_INTERFACE - so it builds on demand
// rather than at exec time.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/VoiceIcon.cs");

// The settings screen and the gear that opens it: audio devices, four volumes and
// the microphone switch. AFTER AudioNative.cs (it drives the same DLL) and after
// Keybinds.cs, whose table owns the Ctrl+O row and whose MRPG_keyOfBinding it uses
// to print the key under the gear. Nothing is built at exec time - the gear needs
// NewChatHud, so MRPGSettings_Start builds it on join.
exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Settings.cs");

exec("Add-Ons/Client_MonsterRPG/GUIs/MessageBoxYesNoDlgBG.gui");
exec("Add-Ons/Client_MonsterRPG/GUIs/MessageBoxOKCancelDlgBG.gui");
exec("Add-Ons/Client_MonsterRPG/GUIs/MessageBoxOKDlgBG.gui");
exec("Add-Ons/Client_MonsterRPG/GUIs/MessagePopupDlgBG.gui");


// ARM THE GATE - LAST, AND THE ORDER IS THE POINT.
//
// activatePackage pushes onto a stack and the most recently activated package is
// the OUTERMOST wrapper, so arming MRPGServerGate here - after every other package
// in this add-on - is what makes MRPG_ClientLeave() run FIRST on the way out, before
// any other module's disconnect handler. The flag is down before anything
// downstream can re-enter a tick.
//
// It hooks disconnectedCleanup, which is the complete set on its own: stock's
// disconnect(), onConnectionDropped, onConnectionTimedOut and onConnectionError all
// funnel into it. onExit covers the one path that does not - quitting straight from
// a server.
MRPG_ArmServerGate();

