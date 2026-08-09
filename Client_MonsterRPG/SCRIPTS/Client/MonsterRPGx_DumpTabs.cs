//////////////////////////////////////////////////////////////////////////////
// MonsterRPGx_DumpTabs.cs  -  DEV/TOOLING ONLY (not exec'd by client.cs)
//////////////////////////////////////////////////////////////////////////////
//
// Offline ".gui" exporter for the script-built RPG tabs. Lets us regenerate an
// accurate engine dump of a tab WITHOUT joining a server: at the Blockland main
// menu the GUI subsystem is fully up, so we can exec the client scripts, run the
// tab builder (which creates the control tree from purely local data), and call
// SimObject::save() to serialize it. The row CONTENT (quests/party/spells lists)
// is server-fed and intentionally NOT part of the structure dump - the builders
// only lay out the frame + scroll containers, which is all we want to make static.
//
// USAGE (main menu, console ~, NO server join):
//     exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/MonsterRPGx_DumpTabs.cs");
//     MRPG_DumpAllTabs();
//
// Output: Add-Ons/Client_MonsterRPG/GUIs/dump_<Tab>.gui  (one per tab)
// Each is a raw engine dump; run the matching transform_*.py to turn it into an
// editable, named MonsterRPGx_<Tab>.gui (same flow used for the Equip tab).

$MRPG::Dump::OutDir = "Add-Ons/Client_MonsterRPG/GUIs/";

// exec the client exactly as the game would, so the object tree we dump is
// identical to the live one ("emulation of loading them"). Menu-time warnings
// from the input-detection files are harmless - they define funcs/packages and
// do not block GUI object creation.
function MRPG_DumpLoadClient()
{
	if(isObject(MonsterRPGx_Main) && isObject(MonsterRPGx_Stats))
	{
		echo("MRPG_DUMP: client already loaded (MonsterRPGx_Main present).");
		return true;
	}
	echo("MRPG_DUMP: loading client.cs ...");
	exec("Add-Ons/Client_MonsterRPG/client.cs");
	if(!isObject(MonsterRPGx_Main) || !isObject(MonsterRPGx_Stats))
	{
		error("MRPG_DUMP: FAILED - MonsterRPGx_Main / MonsterRPGx_Stats not created after exec.");
		return false;
	}
	echo("MRPG_DUMP: client loaded OK.");
	return true;
}

// Build one tab via its builder, then save its root control to dump_<label>.gui.
// %builder is called to (re)create the tree; %root is the object we serialize.
function MRPG_DumpOne(%label, %builder, %root)
{
	if(!isFunction(%builder))
	{
		error("MRPG_DUMP[" @ %label @ "]: builder " @ %builder @ "() not defined - skipped.");
		return;
	}
	call(%builder);
	if(!isObject(%root))
	{
		error("MRPG_DUMP[" @ %label @ "]: root " @ %root @ " not created by " @ %builder @ "() - skipped.");
		return;
	}
	%out = $MRPG::Dump::OutDir @ "dump_" @ %label @ ".gui";
	%root.save(%out);
	echo("MRPG_DUMP[" @ %label @ "]: saved " @ %root.getName() @ " -> " @ %out);
}

// Dump every genuinely script-built tab root. (Equip is already an editable .gui;
// Stats/Leaderboard frames are already static in Main.gui, only their scroll rows
// are script-fed, so there is nothing to dump for them.)
function MRPG_DumpAllTabs()
{
	if(!MRPG_DumpLoadClient())
		return;

	echo("======== MRPG_DUMP: begin ========");
	MRPG_DumpOne("Quest",  "MRPG_buildQuest",  MonsterRPGx_Quest);
	MRPG_DumpOne("Party",  "MRPG_buildParty",  MonsterRPGx_Party);
	MRPG_DumpOne("Spells", "MRPG_buildSpells", MonsterRPGx_SpellsPanel);
	echo("======== MRPG_DUMP: done. Transform dump_*.gui into editable MonsterRPGx_*.gui next. ========");
}
