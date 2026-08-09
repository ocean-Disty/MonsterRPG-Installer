//Client-side Hook for Server Look Control
function clientCmdHataForceAim(%newVec,%lookVec)
{
	%nVecZ = getWord(%newVec,2);
	%mVecZ = getWord(%lookVec,2);
	%mSubZ = %mVecZ - %nVecZ;
//	talk("New Vector Z =" SPC %nVecZ);
//	talk("Muz. Vector Z =" SPC %mVecZ);
//	talk("New Pitch =" SPC %mSubZ);
	$mvPitch = (%mSubZ*$pi/2)/2;
//	talk("Move Pitch =" SPC $mvPitch);
}