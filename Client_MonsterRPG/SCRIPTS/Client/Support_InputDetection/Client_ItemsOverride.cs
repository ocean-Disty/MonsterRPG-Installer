//Item Inputs
//
// THE MRPG_isActive() TERM IN EVERY TEST BELOW IS A BUG FIX, NOT A TIDY-UP.
//
// $Player_BricksActive / ToolsActive / PaintsActive are set ONLY by
// clientCmdRelayItemState - i.e. only by a MonsterRPG server - and a set flag makes
// these wrappers SWALLOW the input instead of parenting it. So a player who left a
// MonsterRPG server with bricks active could not plant a brick, use a tool or spray
// paint on the next server they joined: no error, no message, the click simply did
// nothing for the rest of the session.
//
// MRPG_ClientLeave() clears all three on the way out, so the term below is the
// second line of defence rather than the only one. It is worth having both because
// the failure is silent and the player has no way to diagnose it.
package ItemInputRelays
{
	//use Bricks Override
	function useBricks(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_BricksActive)
			Parent::useBricks(%val); //Parent default 'useBricks' fcn
	}
	function useFirstSlot(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_BricksActive)
			Parent::useFirstSlot(%val); //Parent default 'useFirstSlot' fcn
	}
	function useSecondSlot(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_BricksActive)
			Parent::useSecondSlot(%val); //Parent default 'useSecondSlot' fcn
	}
	function useThirdSlot(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_BricksActive)
			Parent::useThirdSlot(%val); //Parent default 'useThirdSlot' fcn
	}
	function useFourthSlot(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_BricksActive)
			Parent::useFourthSlot(%val); //Parent default 'useFourthSlot' fcn
	}
	function useFifthSlot(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_BricksActive)
			Parent::useFifthSlot(%val); //Parent default 'useFifthSlot' fcn
	}
	function useSixthSlot(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_BricksActive)
			Parent::useSixthSlot(%val); //Parent default 'useSixthSlot' fcn
	}
	function useSeventhSlot(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_BricksActive)
			Parent::useSeventhSlot(%val); //Parent default 'useSeventhSlot' fcn
	}
	function useEighthSlot(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_BricksActive)
			Parent::useEighthSlot(%val); //Parent default 'useEighthSlot' fcn
	}
	function useNinthSlot(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_BricksActive)
			Parent::useNinthSlot(%val); //Parent default 'useNinthSlot' fcn
	}
	function useTenthSlot(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_BricksActive)
			Parent::useTenthSlot(%val); //Parent default 'useTenthSlot' fcn
	}
	//use Tools Override
	function useTools(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_ToolsActive)
			Parent::useTools(%val); //Parent default 'useTools' fcn
	}
	//use Paints Override
	function useSprayCan(%val,%override)
	{
		if(%override || !MRPG_isActive() || !$Player_PaintsActive)
			Parent::useSprayCan(%val); //Parent default 'usePaint' fcn
	}
};activatePackage(ItemInputRelays);

//Item Detection Hook
function clientCmdRelayItemState(%type,%bool)
{
	if(%type == 0)
		$Player_BricksActive = %bool ? true : false; //type 0 = Bricks
	if(%type == 1)
		$Player_ToolsActive = %bool ? true : false; //type 1 = Tools
	if(%type == 2)
		$Player_PaintsActive = %bool ? true : false; //type 2 = Paints
}
//Force Use Bricks (from server)
function clientCmdHataUseBricks()
{
	useBricks(true,true);
}
//Force Use Tools (from server)
function clientCmdHataUseTools()
{
	useTools(true,true);
}
//Force Use Paints (from server)
function clientCmdHataUsePaints()
{
	useSprayCan(true,true);
}
