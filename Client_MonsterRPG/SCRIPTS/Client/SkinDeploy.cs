//------------------------------------------------------------------------------
// Skin index  -  band order, texture deployment, and the portrait's shape list
//
// THE BODY SKIN IS THE DATABLOCK / THE SHAPE FILE, NOT AN IFL FRAME.
//
// This used to write a skin.ifl and select a frame with setIflFrame. That could
// never work in world, and the reason is worth keeping so it is not rebuilt:
//
//   * ShapeBase::setIflFrame is SERVER-SIDE ONLY. Blockland's binary carries the
//     error string "ShapeBase::setIflFrame - Got called on a client, wtf!", and
//     every use of it in Blockland's own scripts is on Avatar_Preview - a
//     client-side GuiObjectView, not a networked player. Called on a real player
//     it changed the SERVER's shape instance, which nothing draws, and no part of
//     the frame is networked - so every client kept rendering frame 0 and every
//     character looked identical. It was never a texture problem.
//   * a networked frame would not have survived regardless. animateIfls
//     (tsAnimate.cc:567) starts with `frame = 0;` and only overrides that from a
//     thread whose sequence declares iflMatters for that ifl. No player .dsq
//     mentions a skin ifl, so the frame is cleared on every animation tick.
//
// NOT setSkinName either: TSShapeInstance::reSkin reloads EVERY material from the
// shape's own directory (tsShapeInstance.cc:516), but this shape's textures do
// not live there - so calling it wipes the face, the decals and much of the body.
//
// What IS networked per object is the datablock. So each race x age band has its
// own player shape carrying a differently NAMED skin material, and the server
// puts the character on the matching PlayerData. See
// Server/Datablocks/Datablocks_SkinShapes.cs and the SKIN TYPE section of
// Server/Core/Core_Character.cs.
//
//
// WHERE THE TEXTURE LIVES - base/data/shapes/, NAMED BARE. Unchanged, and still
// established from failures in game:
//
//      Every working material on the player shape - brickSIDE, black, black25,
//      black50, megaPhoneRidge, brickRAMP, brickBOTTOMLOOP, brickBOTTOMEDGE - is
//      a bare name whose PNG sits in base/data/shapes/, which is the PARENT of
//      base/data/shapes/player/. The renamed skin material resolves by that same
//      rule.
//
//      "Next to the .dts" is the usual Blockland advice for a standalone model
//      and it is WRONG for this one. Both alternatives were tried and logged
//      failures:
//
//        bare name, png in base/data/shapes/player/
//            -> "Could not locate texture: blockhead_plastic"
//        "Add-Ons/Client_MonsterRPG/Skins/name.png" (root-relative)
//            -> resolved against the shape dir, never found
//
//      When a texture fails, Blockland does not leave it blank - it guesses a
//      stock texture from the NAME. human_sca(rred) loaded red.png; others picked
//      up letter prints, which is why a skin once appeared as text on a head.
//      That is why the deploy check below is an error and not a warning, and why
//      orphaned old roots must be deleted rather than left lying about.
//------------------------------------------------------------------------------

$MRPGSkin::Src = "Add-Ons/Client_MonsterRPG/Skins/";   // masters (ship with the add-on)
$MRPGSkin::Tex = "base/data/shapes/";                  // where the engine resolves them

// KEEP IN STEP WITH $MRPG::Skin::Order in Core_Character.cs and SKIN_ORDER in
// Tools/install_player_skin.py.
//
// AGE BANDS, race-major then ascending age. The band a character wears is
//     raceIndex * $MRPGSkin::Bands + band(age)
// so this list is indexed arithmetically, not searched - reordering it hands
// everyone another age's skin without any error being raised.
// GENDER-MAJOR over race-major over age, 76 entries:
//     frame = (genderIndex * 2 + raceIndex) * $MRPGSkin::Bands + band(age)
//   0..18  male   blockhead      38..56  female blockhead
//  19..37  male   human          57..75  female human
//
// Male first so the 38 pre-gender entries keep their indices; the male PNGs are
// byte for byte the ones that already shipped.
//
// READ THIS BEFORE USING IT AS A TEXTURE INDEX: only entry 0..37 have ever been
// selectable and RIGHT NOW NONE OF THEM ARE. There is no skin.ifl and no frame
// switching - see $MRPGSkin::Single below. This list is the authority for which
// skin root a character NOTIONALLY wears (what /charinfo reports and what the
// look packet carries), not for what is on screen. Everybody renders
// base/data/shapes/skin.png.
$MRPGSkin::Order =
	"blockhead_a18 blockhead_a20 blockhead_a22 blockhead_a24 blockhead_a26" SPC
	"blockhead_a28 blockhead_a29 blockhead_a30 blockhead_a31 blockhead_a34" SPC
	"blockhead_a37 blockhead_a41 blockhead_a45 blockhead_a50 blockhead_a55" SPC
	"blockhead_a61 blockhead_a68 blockhead_a76 blockhead_a87" SPC
	"human_a18 human_a20 human_a22 human_a24 human_a26" SPC
	"human_a28 human_a29 human_a30 human_a31 human_a34" SPC
	"human_a37 human_a41 human_a45 human_a50 human_a55" SPC
	"human_a61 human_a68 human_a76 human_a87" SPC
	"blockhead_f_a18 blockhead_f_a20 blockhead_f_a22 blockhead_f_a24" SPC
	"blockhead_f_a26 blockhead_f_a28 blockhead_f_a29 blockhead_f_a30" SPC
	"blockhead_f_a31 blockhead_f_a34 blockhead_f_a37 blockhead_f_a41" SPC
	"blockhead_f_a45 blockhead_f_a50 blockhead_f_a55 blockhead_f_a61" SPC
	"blockhead_f_a68 blockhead_f_a76 blockhead_f_a87" SPC
	"human_f_a18 human_f_a20 human_f_a22 human_f_a24 human_f_a26" SPC
	"human_f_a28 human_f_a29 human_f_a30 human_f_a31 human_f_a34" SPC
	"human_f_a37 human_f_a41 human_f_a45 human_f_a50 human_f_a55" SPC
	"human_f_a61 human_f_a68 human_f_a76 human_f_a87";
$MRPGSkin::Bands = 19;

// PER-BAND SHAPES ARE GONE TOO. They were the second attempt after setIflFrame
// and they broke sixteen add-ons: the player's shapeFile is how Bot_Zombie,
// Bot_Shark, Support_HitLocation, Server_PhysicsDeath, Weapon_ActionMelee,
// Gamemode_Slayer_AAA and others decide "is this a blockhead?", eleven of them by
// FULL PATH, and stock's own applyBodyParts / applyDefaultCharacterPrefs bail
// before hideAllNodes for any other shape - which rendered every hat and every
// body variant at once. The player stays on base/data/shapes/player/m.dts.
//
// So there is currently ONE body sheet for everybody: base/data/shapes/skin.png.
// Age drives height, width and skin TONE (setNodeColor), not body complexion.
$MRPGSkin::Single = "skin";

// Repair path for when a Blockland update wipes base/. The installer
// (Tools/install_player_skin.py --apply) normally puts this in place before the
// shape is ever loaded, which matters because a shape resource is parsed once per
// session and cached.
//
// One sheet to check now, not ten. The body material on m.dts is named "skin", so
// it resolves to base/data/shapes/skin.png by exactly the same rule as
// brickSIDE.png - see the placement note at the top of this file.
function MRPGSkin_deploy()
{
	%tex = $MRPGSkin::Tex @ $MRPGSkin::Single @ ".png";

	// Which master to fall back on. The MIDDLE age band, because that is what
	// make_skin_faces.py bakes the head's face plate against - a mismatch between
	// the body sheet and the plate shows as a seam at the neck.
	%src = $MRPGSkin::Src @ getWord($MRPGSkin::Order, 7) @ ".png";   // human_a45

	// fileCopy may be refused by the engine's write sandbox for this directory -
	// if so the error below says so rather than leaving it a mystery.
	if (!isFile(%tex) && isFile(%src))
		fileCopy(%src, %tex);

	if (!isFile(%tex))
	{
		error("MRPGSkin: " @ %tex @ " is MISSING - the body's skin material has no "
			@ "texture and hands/head render untextured. Re-run: "
			@ "py -3 Tools/install_player_skin.py --apply");
		$MRPGSkin::Frames = 0;
		return 0;
	}

	$MRPGSkin::Frames = 1;
	echo("MRPGSkin: body sheet present (" @ %tex @ ")");
	return 1;
}

// Band index for a skin root, or -1 if it is not in the order list.
function MRPGSkin_frame(%root)
{
	if (%root $= "")
		return -1;
	%n = getWordCount($MRPGSkin::Order);
	for (%i = 0; %i < %n; %i++)
		if (getWord($MRPGSkin::Order, %i) $= %root)
			return %i;
	return -1;
}

// THE ONE THING IN THIS ADD-ON THAT LEGITIMATELY RUNS AT FILE SCOPE, and it is
// exempt from ServerGate.cs rule 1 for a reason that cannot be worked around: a
// TGE shape resource is parsed ONCE PER SESSION and cached, so the body sheet has
// to be on disk before anything loads m.dts. Deferring this to the join would mean
// a player who first connects to a non-MonsterRPG server has already cached an
// untextured shape, and no later copy could fix it without a restart.
//
// It is also not "running" in any meaningful sense - one isFile, and a fileCopy
// only when the texture is genuinely missing (i.e. after a Blockland update wiped
// base/). No schedule, no GUI, no server traffic.
MRPGSkin_deploy();
