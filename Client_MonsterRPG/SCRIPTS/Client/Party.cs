if(!isObject(MonsterRPGx_LocalPartySO))
{
    %this = new ScriptObject(MonsterRPGx_LocalPartySO);
    
    %this.maxCategories = 2;
    
    %this.category[0] = "Server";
    %this.category[1] = "Party";
	%this.category[2] = "PartyBonus";
}


function clientCmdMonsterRPGx_CreateParty(%command)
{
    commandToServer('CreateParty');
	commandToServer('UpdateParty');
}

function clientCmdMonsterRPGx_LeaveParty(%command)
{
    commandToServer('LeaveParty');
	commandToServer('UpdateParty');
}

function MonsterRPGx_LeaveParty()
{
    commandToServer('LeaveParty');
	commandToServer('UpdateParty');
}

// The Leave button bitmap and MonsterRPGx_PartyLeaveMouse::onMouseDown both call
// MonsterRPGx_PartyLeave() (which never existed), so leaving only worked when the
// sibling mouse control's command fired. Alias it to the real function.
function MonsterRPGx_PartyLeave()
{
    MonsterRPGx_LeaveParty();
}

function MonsterRPGx_CreateParty()
{
    commandToServer('CreateParty');
	commandToServer('UpdateParty');
}

function MonsterRPGx_KickMember()
{
    // Selection is by hover: whichever member row you last pointed at (RPGPanels.cs).
    %member = $MRPG_Party_SelMember;

    if(%member $= "")
    {
        messageBoxOK("Kick member", "Hover the party member you want to kick, then click Kick.");

        return;
    }

    commandToServer('PartyKick', %member);
}

function MonsterRPGx_InviteMember()
{
    %member = $MRPG_Party_SelPlayer;

    if(%member $= "")
    {
        messageBoxOK("Invite member", "Hover the player you want to invite, then click Invite.");

        return;
    }

    commandToServer('PartyInvite', %member);
}

function clientCmdMonsterRPGxUpdatePartyMembers(%data)
{
    %memberCount = getField(%data, 0);
    %memberData = getFields(%data, 1);
    
    $MonsterRPG::Party::MembersCount = %memberCount;
    
    // Split the member data
    %memberEntries = strreplace(%memberData, "^", "\t");
    
    for(%i = 0; %i < %memberCount; %i++)
    {
        %memberIndex = %i * 2;
        $MonsterRPG::Party::Member[%i] = getField(%memberEntries, %memberIndex);
        $MonsterRPG::Party::Member::Level[%i] = getField(%memberEntries, %memberIndex + 1);
    }

    generateScrollableText(2);
    MRPG_partyMaybeRender("members"); // scroll-list party panel (RPGPanels.cs)
}

function clientCmdMonsterRPGxGetServerPlayers(%data)
{
    %playerCount = getField(%data, 0);
    %playerData = getFields(%data, 1);
    
    $MonsterRPG::PlayerCount = %playerCount;
    
    // Split the player data by "^"
    %playerEntries = strreplace(%playerData, "^", "\t");
    
    for(%i = 0; %i < %playerCount; %i++)
    {
        %playerIndex = %i * 2;
        $MonsterRPG::Player[%i] = getField(%playerEntries, %playerIndex);
        $MonsterRPG::Player::Level[%i] = getField(%playerEntries, %playerIndex + 1);
    }

    generateScrollableText(1);
    MRPG_partyMaybeRender("players"); // scroll-list party panel (RPGPanels.cs)
}

function clientCmdMonsterRPGxUpdatePartyHandshake(%data)
{
    $MonsterRPG::Party::InParty = getWord(%data,0);
    $MonsterRPG::Party::MaxMembers = getWord(%data,1);	
	$MonsterRPG::Party::DropRateBonus = getWord(%data,2);
	$MonsterRPG::Party::YieldBonus = getWord(%data,3);
	$MonsterRPG::Party::FameBonus = getWord(%data,4);
	$MonsterRPG::Party::MembersCount = getWord(%data,5);
	$MonsterRPG::Party::Leader = getWord(%data,6);
	$MonsterRPG::Party::PartyName = getWord(%data,6) @ "'s Party";

    generateScrollableText(3);
}