// Temporary, opt-in movement diagnostic.
//
// Load at the client console with:
//   exec("Add-Ons/Client_MonsterRPG/SCRIPTS/Client/Testing_MovementProbe.cs");
// Then run MRPG_moveProbeStatus(); and reproduce the problem.  A key that is
// captured by a pushed ActionMap never reaches these wrappers, which is exactly
// the signal we need.  Remove the probe with MRPG_moveProbeStop().

function MRPG_moveProbeStatus()
{
	echo("[MRPG move probe] binds:"
		@ " w=" @ moveMap.getCommand(keyboard, "w")
		@ " a=" @ moveMap.getCommand(keyboard, "a")
		@ " s=" @ moveMap.getCommand(keyboard, "s")
		@ " d=" @ moveMap.getCommand(keyboard, "d")
		@ " shift=" @ moveMap.getCommand(keyboard, "lshift"));
	echo("[MRPG move probe] state:"
		@ " incActive=" @ $Inc_Active
		@ " incTier=" @ $Inc_Tier
		@ " incMap=" @ isObject(MRPG_IncantMap)
		@ " typeMap=" @ isObject(MRPG_IncTypeMap)
		@ " inputOverride=" @ $Hata_MoveInputOverride
		@ " camOverride=" @ $camSyncOverride
		@ " mrpgActive=" @ MRPG_isActive());

	%control = isObject(ServerConnection) ? ServerConnection.getControlObject() : 0;
	if(isObject(%control))
	{
		%db = %control.getDatablock();
		echo("[MRPG move probe] control=" @ %control
			@ " datablock=" @ (isObject(%db) ? %db.getName() : "-")
			@ " state=" @ %control.getState());
		echo("[MRPG move probe] speeds:"
			@ " normal=" @ %control.getMaxForwardSpeed()
			@ "/" @ %control.getMaxBackwardSpeed()
			@ "/" @ %control.getMaxSideSpeed()
			@ " crouch=" @ %control.getMaxCrouchForwardSpeed()
			@ "/" @ %control.getMaxCrouchBackwardSpeed()
			@ "/" @ %control.getMaxCrouchSideSpeed());
	}
	else
		echo("[MRPG move probe] no Player control object");
}

// Recovery-only command.  ActionMap::pop is harmless when the map is not on
// the stack.  This deliberately does not modify the player's saved moveMap.
function MRPG_moveProbeReleaseMaps()
{
	if(isObject(MRPG_IncantMap))
		MRPG_IncantMap.pop();
	if(isObject(MRPG_IncTypeMap))
		MRPG_IncTypeMap.pop();

	$Inc_Active = 0;
	$Inc_Tier = 0;
	echo("[MRPG move probe] released incantation ActionMaps");
}

function MRPG_moveProbeStop()
{
	deactivatePackage(MRPGMovementProbe);
	echo("[MRPG move probe] stopped");
}

if(isPackage(MRPGMovementProbe))
	deactivatePackage(MRPGMovementProbe);

package MRPGMovementProbe
{
	function moveForward(%value)
	{
		echo("[MRPG move probe] moveForward=" @ %value);
		Parent::moveForward(%value);
	}

	function moveBackward(%value)
	{
		echo("[MRPG move probe] moveBackward=" @ %value);
		Parent::moveBackward(%value);
	}

	function moveLeft(%value)
	{
		echo("[MRPG move probe] moveLeft=" @ %value);
		Parent::moveLeft(%value);
	}

	function moveRight(%value)
	{
		echo("[MRPG move probe] moveRight=" @ %value);
		Parent::moveRight(%value);
	}

	function Crouch(%value)
	{
		echo("[MRPG move probe] Crouch=" @ %value);
		Parent::Crouch(%value);
	}
};
activatePackage(MRPGMovementProbe);
echo("[MRPG move probe] loaded; run MRPG_moveProbeStatus()");
