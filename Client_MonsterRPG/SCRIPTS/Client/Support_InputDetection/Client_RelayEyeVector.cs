//Client-side Hook for Relaying Player's Eye Vector
function clientCmdRelayEyeVector(%eyeVec,%fwdVec)
{
//	echo("Eye Vector =" SPC %eyeVec);
//	echo("Forward Vector =" SPC %fwdVec);
	$Player_EyeVector = %eyeVec; //store the relayed player eye vector, to the client
	$Player_ForwardVector = %fwdVec; //store the relayed player forward vector, to the client
}