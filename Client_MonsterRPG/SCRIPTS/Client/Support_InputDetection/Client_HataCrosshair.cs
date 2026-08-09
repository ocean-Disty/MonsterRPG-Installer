//Third Person Crosshair
function clientCmdHataCrosshair(%enable) //Relay Gui from server to client
{
	if(%enable && !PlayGui.HasNewCrosshair) //if enabling, and Crosshair is off, and not overriding
	{
		PlayGui.add(Hata_Crosshair); //show Aim Reticle
		PlayGui.hasNewCrosshair = true; //note Crosshair on
	}
	else if(!%enable && PlayGui.HasNewCrosshair) //if disabling, and Crosshair is on
	{
		PlayGui.remove(Hata_Crosshair); //hide Aim Reticle
		PlayGui.hasNewCrosshair = false; //note Crosshair off
	}
}

//TAB and the free-look key are stock binds that exist on every server. Both relays
//are gated so pressing them elsewhere does not send MonsterRPG commands to a server
//that has no handler for them - see ServerGate.cs rule 4.
package HataCrosshair
{
	//Relay First Person Toggle to Server
	function toggleFirstPerson(%val)
	{
		//Basically: when you hit TAB it calls this function twice. So I made it only flip the "$TPOn" bool on the second call.
		if($FPToggled) //if just toggled
		{
			$FPToggled = false; //set "just toggled" bool off
			$TPOn = !$TPOn; //toggle third person bool
			if(MRPG_isActive())
				commandToServer('relayFirstPerson',$TPOn); //relay client "is third person?" to the server
		}
		else //otherwise
			$FPToggled = true; //set "just toggled" bool on
		Parent::toggleFirstPerson(%val); //Parent default function
	}
	//Relay First Person Toggle to Server
	function toggleFreeLook(%val)
	{
		$FLOn = %val; //define Free Look bool
		if(MRPG_isActive())
			commandToServer('relayFreeLook',$FLOn); //relay client "is Free Looking?" to the server
		Parent::toggleFreeLook(%val); //Parent default function
	}
};activatePackage(HataCrosshair);