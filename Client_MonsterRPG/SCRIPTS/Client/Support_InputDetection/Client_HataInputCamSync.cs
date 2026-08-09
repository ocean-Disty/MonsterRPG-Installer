$yzFlip = true;

//Client-side Hook for Movement Input Overrides
function clientCmdMoveInputOverride(%enable)
{
	$Hata_MoveInputOverride = %enable;
}

package KeyInputCamSync
{
	function KeyInputCamSync(%testDir,%onParent)
	{
		//NOT REACHABLE OFF A MonsterRPG SERVER, and gated anyway. The only callers
		//are the movement relays' camera-relative branch, which needs
		//$Hata_MoveInputOverride - a flag only clientCmdMoveInputOverride sets and
		//MRPG_ClientLeave clears. The gate is here because this arms a 1ms
		//scheduleNoQuota loop that re-enters itself while a key is held: if it ever
		//did start off-server it would spam RelayEyeVector at frame rate.
		if(!MRPG_isActive())
		{
			cancel($KeyInputCamSyncSched);
			return;
		}

		//Update Eye Vector and Forward Vector (from Server)
		commandToServer('RelayEyeVector'); //relay client "is third person?" to the server
		%Evec = $Player_EyeVector; //client player's eye vector
		%Fvec = $Player_ForwardVector; //client player's muzzle vector
		%Hvec = vectorNormalize(getWords(%Evec,0,1) SPC 0); //build vector from horizontal component of input vector
		%Rvec = vectorCross(%Hvec,"0 0 1"); //relative rightward vector of player's eye vector
		%Uvec = vectorCross(%Rvec,%Evec); //relative upward vector of player's eye vector

		//Make Movement Vectors (Forward / Back)
		if(%testDir $= "forward")
		{
			%moveVec = %Evec;
			%hold = $Hata_MoveForward;
		}
		if(%testDir $= "backward")
		{
			%moveVec = vectorScale(%Evec,-1);
			%hold = $Hata_MoveBackward;
		}
		
		//Make Movement Vectors (Right / Left)
		if(%testDir $= "right")
		{
			%moveVec = %Rvec;
			%hold = $Hata_MoveRight;
		}
		if(%testDir $= "left")
		{
			%moveVec = vectorScale(%Rvec,-1);
			%hold = $Hata_MoveLeft;
		}
		
		//Make Movement Vectors (Up / Down)
		if(%testDir $= "up")
		{
			%moveVec = %Uvec;
			%hold = $Hata_MoveUp;
		}
		if(%testDir $= "down")
		{
			%moveVec = vectorScale(%Uvec,-1);
			%hold = $Hata_MoveDown;
		}

		if(%hold) //if input arg. 'val' = true
		{
			cancel($KeyInputCamSyncSched); //cancel any previous schedules
			$KeyInputCamSyncSched = scheduleNoQuota(1,0,KeyInputCamSync,%testDir); //start / continue the loop
		}

		//Make Player Vectors
		%vec0 = vectorCross(%Fvec,"0 0 1");			//player rightward vector
		%vec1 = %Fvec; 								//player forward vector

		//Compare Move-Input & Ledge-facing Vectors
		%dotx = vectorDot(%moveVec,%vec0);			//relative x vec
		%doty = vectorDot(%moveVec,%vec1);			//relative y vec
		%dotz = vectorDot(%moveVec,"0 0" SPC 1);	//global z vec (same as player's relative z vector)

		//For Free Look (Right / Left)
		%rightMin = 0.5;
		%leftMin = 0.5;
		//For General (Forward / Back)
		%fwdMin = 0.5;
		%bckMin = 0.5;
		
		//For General (Up / Down)
		%upMin = %testDir $= "Up" ? 0.5 : 0.9; //0.98;
		%downMin = %testDir $= "Down" ? 0.5 : 0.9; //0.98;
		
		//Move Right
		if(%dotx > %rightMin)
			%newDir = "moveRight";
		//Move Left
		if(%dotx < -%leftMin)
			%newDir = "moveLeft";
		
		//Move Forward
		if(%doty > %fwdMin)
			%newDir = "moveForward";
		
		//Move Backward
		if(%doty < -%bckMin)
			%newDir = "moveBackward";
		
		//Move Up (Jump)
		if(%dotz > %upMin)
			%newDir = "jump";
		
		//Move Down (Crouch)
		if(%dotz < -%downMin)
			%newDir = "crouch";

		if(%newDir $= "") //if 'new Direction' undefined
			return; //stop here
		if($moveDir[%testDir] !$= %newDir) //if the 'movement Direction' for the 'test Direction' does not match the 'new Direction'
		{
			if($moveDir[%testDir] !$= "") //if the 'movement Direction' for the 'test Direction' is defined
				KeyInputReset($moveDir[%testDir]); //reset the previous input
			$moveDir[%testDir] = %newDir; //set the new 'movement Direction' for the 'test Direction' to 'new Direction'
			%wrongDir = true; //set 'wrong Direction' bool to true (a shortcut for the following if() statement)
		}
		if(%onParent || %wrongDir) //if called via the input Parent, or the 'wrong Direction' bool = true
		{
			$camSyncOverride = true; //override camSync, and Parent the movement fcn normally
			call(%newDir,%hold); //Call 'new Direction' fcn (e.g. moveLeft, moveRight, moveForward, moveBackward, jump, or crouch)
		}
	}
	function KeyInputReset(%lastDir)
	{
		$camSyncOverride = true; //override camSync, and Parent the movement fcn normally
		call(%lastDir,0); //Call 'new Direction' fcn (e.g. moveLeft, moveRight, moveForward, moveBackward, jump, or crouch)
	}
};activatePackage(KeyInputCamSync);