//Movement Inputs
//
// EVERY RELAY IN THIS FILE IS GATED ON MRPG_isActive(), and this is the file where
// that matters most. These wrappers sit on moveForward / moveBackward / moveLeft /
// moveRight / Jump / Crouch - the six functions a player triggers more than any
// others - and they used to fire commandToServer on EVERY press and EVERY release,
// on every server the player joined. A server without Server_MonsterRPG has no
// serverCmdSetMoveTrigger to receive them, so they were pure waste at best and a
// flood-kick at worst.
//
// $Hata_MoveInputOverride is only ever set by clientCmdMoveInputOverride, i.e. by a
// MonsterRPG server, and MRPG_ClientLeave() clears it on the way out - so the
// camera-relative branch below cannot capture movement keys on a server that never
// asked for it. See ServerGate.cs rule 4.
package MovementKeyInputRelays
{
	//Move Rightward (default D)
	function moveRight(%val)
	{
		if(MRPG_isActive())
			commandToServer('SetMoveTrigger',3,%val); //relay 'Move Trigger' to server
		if(!$Hata_MoveInputOverride || $camSyncOverride) //if 'Move Input Override' = false
		{
			Parent::moveRight(%val); //Parent default 'move Rightward' fcn
			$camSyncOverride = $camSyncOverride ? false : $camSyncOverride; //reset 'cam Sync Override' bool (if it's set)
		}
		else //otherwise
		{
			$Hata_MoveRight = %val; //store 'val' bool as a global variable
			KeyInputCamSync("right",true); //Do the appropriate relative action
		}
	}
	//Move Leftward (default A)
	function moveLeft(%val)
	{
		if(MRPG_isActive())
			commandToServer('SetMoveTrigger',1,%val); //relay 'Move Trigger' to server
		if(!$Hata_MoveInputOverride || $camSyncOverride) //if 'Move Input Override' = false
		{
			Parent::moveLeft(%val); //Parent default 'move Leftward' fcn
			$camSyncOverride = $camSyncOverride ? false : $camSyncOverride; //reset 'cam Sync Override' bool (if it's set)
		}
		else //otherwise
		{
			$Hata_MoveLeft = %val; //store 'val' bool as a global variable
			KeyInputCamSync("left",true); //Do the appropriate relative action
		}
	}
	//Move Forward (default W)
	function moveForward(%val)
	{
		if(MRPG_isActive())
			commandToServer('SetMoveTrigger',0,%val); //relay 'Move Trigger' to server
		if(!$Hata_MoveInputOverride || $camSyncOverride) //if 'Move Input Override' = false
		{
			Parent::moveForward(%val); //Parent default 'move Forward' fcn
			$camSyncOverride = $camSyncOverride ? false : $camSyncOverride; //reset 'cam Sync Override' bool (if it's set)
		}
		else //otherwise
		{
			if($yzFlip) //if (Forward / Backward) - (Up / Down) flip enabled
			{
				$Hata_MoveUp = %val; //store 'val' bool as a global variable
				KeyInputCamSync("up",true); //Do the appropriate relative action
			}
			else //otherwise
			{
				$Hata_MoveForward = %val; //store 'val' bool as a global variable
				KeyInputCamSync("forward",true); //Do the appropriate relative action
			}
		}
	}
	//Move Backward (default S)
	function moveBackward(%val)
	{
		if(MRPG_isActive())
			commandToServer('SetMoveTrigger',2,%val); //relay 'Move Trigger' to server
		if(!$Hata_MoveInputOverride || $camSyncOverride) //if 'Move Input Override' = false
		{
			Parent::moveBackward(%val); //Parent default 'move Backward' fcn
			$camSyncOverride = $camSyncOverride ? false : $camSyncOverride; //reset 'cam Sync Override' bool (if it's set)
		}
		else //otherwise
		{
			if($yzFlip) //if (Forward / Backward) - (Up / Down) flip enabled
			{
				$Hata_MoveDown = %val; //store 'val' bool as a global variable
				KeyInputCamSync("down",true); //Do the appropriate relative action
			}
			else //otherwise
			{
				$Hata_MoveBackward = %val; //store 'val' bool as a global variable
				KeyInputCamSync("backward",true); //Do the appropriate relative action
			}
		}
	}
	//Jump (default Spacebar)
	function Jump(%val)
	{
		if(MRPG_isActive())
			commandToServer('SetActionTrigger',2,%val); //relay 'Action Trigger' to server
		if(!$Hata_MoveInputOverride || $camSyncOverride) //if 'Move Input Override' = false
		{
			Parent::Jump(%val); //Parent default 'Jump' fcn
			$camSyncOverride = $camSyncOverride ? false : $camSyncOverride; //reset 'cam Sync Override' bool (if it's set)
		}
		else //otherwise
		{
			if($yzFlip) //if (Forward / Backward) - (Up / Down) flip enabled
			{
				$Hata_MoveBackward = %val; //store 'val' bool as a global variable
				KeyInputCamSync("backward",true); //Do the appropriate relative action
			}
			else //otherwise
			{
				$Hata_MoveUp = %val; //store 'val' bool as a global variable
				KeyInputCamSync("up",true); //Do the appropriate relative action
			}
		}
	}
	//Crouch (default Shift)
	function Crouch(%val)
	{
		if(MRPG_isActive())
			commandToServer('SetActionTrigger',3,%val); //relay 'Action Trigger' to server
		if(!$Hata_MoveInputOverride || $camSyncOverride) //if 'Move Input Override' = false
		{
			Parent::Crouch(%val); //Parent default 'Crouch' fcn
			$camSyncOverride = $camSyncOverride ? false : $camSyncOverride; //reset 'cam Sync Override' bool (if it's set)
		}
		else //otherwise
		{
			if($yzFlip) //if (Forward / Backward) - (Up / Down) flip enabled
			{
				$Hata_MoveForward = %val; //store 'val' bool as a global variable
				KeyInputCamSync("forward",true); //Do the appropriate relative action
			}
			else //otherwise
			{
				$Hata_MoveDown = %val; //store 'val' bool as a global variable
				KeyInputCamSync("down",true); //Do the appropriate relative action
			}
		}
	}
};activatePackage(MovementKeyInputRelays);