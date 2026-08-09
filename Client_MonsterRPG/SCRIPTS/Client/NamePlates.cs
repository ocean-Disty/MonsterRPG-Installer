////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////
///////////////// PSEUDO-3D NAMEPLATES (2D emulating world text) ///////////
////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////

// Replaces the mounted/repositioned label objects, which could never stay in
// sync - they were world objects updated on a 200ms server tick while the camera
// moved every frame.
//
// These are GUI controls projected from the world each client frame, so position
// is computed from the SAME frame's camera. There is no lag to desync.
//
// DATA FLOW - split by how often each piece changes:
//   * Static (bot id, name, level) rides the bot's SHAPE NAME. The client reads
//     it straight off its own ghost via getShapeName(), so this costs zero extra
//     network. The server sets it once through the cached setter.
//     Format:  "<serverObjectId>\t<name>\t<level>"
//   * Dynamic (HP, line-of-sight) comes from a periodic per-client message,
//     keyed by that same server object id. LOS is inherently per-client - two
//     players see different things - so it cannot ride the shared shape name.
//
// The id embedded in the shape name is what makes the two halves line up.
// Blockland exposes no ghost-id API, so without it the client would have to
// guess which ghost the server meant by comparing positions.

$MRPG::Plate::MaxPlates  = 12;    // pool size - hard cap on how many draw at once
$MRPG::Plate::MaxDist    = 45;    // stop drawing past this many units

// TWO-RATE DESIGN - this is what makes a per-frame update affordable.
//
// ServerConnection holds EVERY ghosted object, which on a populated map is a
// large list. Walking it each frame to find AIPlayers would cost far more than
// the projection itself. So finding bots happens on a slow pass, and the
// per-frame pass only touches the handful already found.
//
// Positions are still read live from the ghosts every frame, so movement stays
// perfectly smooth - only list MEMBERSHIP lags, and a bot coming into view
// appearing up to RescanMS late is imperceptible.
$MRPG::Plate::TickMS   = 1;    // via scheduleNoQuota - effectively next frame
$MRPG::Plate::RescanMS = 250;  // how often to re-find which bots are around
$MRPG::Plate::HeadOffset = 2.6;   // units above bot origin, scaled by bot size

$MRPG::Plate::NameSize   = 21;
$MRPG::Plate::LevelSize  = 15;

// Fail OPEN when the server has not reported on a bot yet: showing a plate a
// fraction early is far less jarring than every plate blinking out whenever a
// message is late.
$MRPG::Plate::DefaultVisible = 1;

function MRPGPlates_Build()
{
    if(isObject(MRPG_PlateSet))
        return;

    if(!isObject(MRPG_PlateProfile))
    {
        new GuiControlProfile(MRPG_PlateProfile)
        {
            fontType  = $Pref::Client::MonsterRPGx::StreakFont;
            fontSize  = $MRPG::Plate::NameSize;
            fontColor = "255 255 255 255";

            // World text sits against arbitrary terrain - without an outline it
            // is unreadable half the time.
            fontOutline       = true;
            fontOutlineColor  = "0 0 0 255";
            fontOutlineOffset = "2 2";

            allowColorChars = 1;
            maxLength       = 255;
            justify         = "Center";
        };
    }

    new GuiControl(MRPG_PlateSet)
    {
        profile     = "GuiDefaultProfile";
        horizSizing = "width";
        vertSizing  = "height";
        position    = "0 0";
        extent      = getWord(Canvas.getExtent(), 0) SPC getWord(Canvas.getExtent(), 1);
        minExtent   = "8 2";
        enabled     = "1";
        visible     = "1";
    };

    // Pre-allocated pool. Creating and deleting controls every frame would churn
    // hard at 30Hz; these are just repositioned and hidden instead.
    for(%i = 0; %i < $MRPG::Plate::MaxPlates; %i++)
    {
        %plate = new GuiMLTextCtrl()
        {
            profile     = "MRPG_PlateProfile";
            horizSizing = "right";
            vertSizing  = "bottom";
            position    = "0 0";
            extent      = "260 52";
            minExtent   = "8 2";
            enabled     = "1";
            visible     = "0";
        };

        MRPG_PlateSet.add(%plate);
        $MRPG::Plate::Ctrl[%i] = %plate;
    }

    PlayGui.add(MRPG_PlateSet);
}

// Matches the server's HP bucket colours so the plate reads the same as the
// old 3D name did.
function MRPGPlates_HPColor(%pct)
{
    if(%pct >= 80) return "6BE06B";
    if(%pct >= 60) return "AEE06B";
    if(%pct >= 40) return "E0D66B";
    if(%pct >= 20) return "E0A56B";
    return "E06B6B";
}

// Server pushes: "id hp vis id hp vis ..." for every bot near this client.
function clientCmdMRPGPlateData(%data)
{
    $MRPG::Plate::DataPass++;
    %pass = $MRPG::Plate::DataPass;

    %count = getWordCount(%data);

    // Triples of "id hp vis". Highest index touched is %i+2, and valid indices
    // run 0..%count-1, so the guard is %i+2 < %count.
    for(%i = 0; %i + 2 < %count; %i += 3)
    {
        %id = getWord(%data, %i);

        if(%id $= "")
            continue;

        $MRPG::Plate::HP[%id]   = getWord(%data, %i + 1);
        $MRPG::Plate::Vis[%id]  = getWord(%data, %i + 2);
        $MRPG::Plate::Pass[%id] = %pass;
    }
}

// SLOW PASS - find which ghosts are MRPG bots and cache their static data.
function MRPGPlates_Rescan()
{
    cancel($MRPG::Plate::ScanSch);

    //THE BODY WAS GATED BUT THE RE-ARM AT THE BOTTOM WAS NOT, so this kept ticking
    //every 250ms after the connection went - finding nothing, forever.
    //MRPGPlates_Stop cancels it on a clean leave, but a tick that can only be
    //stopped from outside is the exact shape of fault this pass exists to remove.
    //See ServerGate.cs rule 3.
    if(!MRPG_isActive())
    {
        MRPGPlates_HideAll();
        $MRPG::Plate::BotCount = 0;
        return;
    }

    %found = 0;

    if(isObject(ServerConnection) && $MonsterRPG::Client::inMonsterRPGServer)
    {
        %ghosts = ServerConnection.getCount();

        for(%i = 0; %i < %ghosts && %found < $MRPG::Plate::MaxPlates; %i++)
        {
            %obj = ServerConnection.getObject(%i);

            if(%obj.getClassName() !$= "AIPlayer")
                continue;

            // "<id>\t<name>\t<level>" - anything else is an AIPlayer this system
            // does not own (another add-on's bot), so leave it alone.
            %raw = %obj.getShapeName();

            if(getFieldCount(%raw) < 3)
                continue;

            %id = getField(%raw, 0);

            if(%id $= "")
                continue;

            $MRPG::Plate::BotObj[%found]   = %obj;
            $MRPG::Plate::BotId[%found]    = %id;
            $MRPG::Plate::BotName[%found]  = getField(%raw, 1);
            $MRPG::Plate::BotLevel[%found] = getField(%raw, 2);
            %found++;
        }
    }

    $MRPG::Plate::BotCount = %found;
    $MRPG::Plate::ScanSch = schedule($MRPG::Plate::RescanMS, 0, MRPGPlates_Rescan);
}

// FAST PASS - runs every frame. Only touches the cached list, never the full
// ghost list, so the per-frame cost is bounded by MaxPlates.
function MRPGPlates_Loop()
{
    cancel($MRPG::Plate::LoopSch);

    if(!isObject(ServerConnection) || !$MonsterRPG::Client::inMonsterRPGServer)
    {
        MRPGPlates_HideAll();
        return;
    }

    %cam = MRPG_GetCameraTransform();

    if(%cam $= "")
    {
        MRPGPlates_HideAll();
        $MRPG::Plate::LoopSch = scheduleNoQuota($MRPG::Plate::TickMS, 0, MRPGPlates_Loop);
        return;
    }

    %used = 0;
    %count = $MRPG::Plate::BotCount;

    for(%i = 0; %i < %count && %used < $MRPG::Plate::MaxPlates; %i++)
    {
        %obj = $MRPG::Plate::BotObj[%i];

        // Ghost can vanish between rescans - bot died, or moved out of range.
        if(!isObject(%obj))
            continue;

        %id    = $MRPG::Plate::BotId[%i];
        %name  = $MRPG::Plate::BotName[%i];
        %level = $MRPG::Plate::BotLevel[%i];

        // Occluded, per the server's line-of-sight test. Compare against the
        // CURRENT pass - a stale entry from before the bot left range must not
        // keep suppressing it, so anything not freshly reported falls back to
        // the default rather than reusing an old result.
        if($MRPG::Plate::Pass[%id] == $MRPG::Plate::DataPass)
            %vis = $MRPG::Plate::Vis[%id];
        else
            %vis = $MRPG::Plate::DefaultVisible;

        if(!%vis)
            continue;

        %pos = %obj.getPosition();

        // getScale() returns a vector - take the Z component. Multiplying by the
        // whole "x y z" string would concatenate rather than scale.
        %sz = getWord(%obj.getScale(), 2);

        if(%sz <= 0)
            %sz = 1;

        %head = vectorAdd(%pos, "0 0" SPC ($MRPG::Plate::HeadOffset * %sz));

        %screen = worldToScreen(%cam, %head);

        if(%screen $= "")
            continue;   // behind the camera

        %depth = getWord(%screen, 2);

        if(%depth > $MRPG::Plate::MaxDist)
            continue;

        %hp = $MRPG::Plate::HP[%id];

        if(%hp $= "")
            %hp = 100;

        // Shrink with distance so the plates read as sitting in the world
        // rather than pasted flat on the screen.
        %scale = 1 - ((%depth / $MRPG::Plate::MaxDist) * 0.45);

        %nameSize  = mFloor($MRPG::Plate::NameSize  * %scale);
        %levelSize = mFloor($MRPG::Plate::LevelSize * %scale);

        %font = $Pref::Client::MonsterRPGx::StreakFont;

        // Level ABOVE the name, as its own row - the thing the world-object
        // version was trying and failing to do.
        %text = "<just:center>" @
                "<font:" @ %font @ ":" @ %levelSize @ "><color:FFD166>Lv." SPC %level @ "\n" @
                "<font:" @ %font @ ":" @ %nameSize  @ "><color:" @ MRPGPlates_HPColor(%hp) @ ">" @ %name;

        %plate = $MRPG::Plate::Ctrl[%used];
        %plate.setText(%text);

        %pw = getWord(%plate.getExtent(), 0);
        %ph = getWord(%plate.getExtent(), 1);

        // Anchor the BOTTOM of the plate at the projected point so the text
        // grows upward off the bot's head instead of straddling it.
        %plate.resize(mFloor(getWord(%screen, 0) - (%pw / 2)),
                      mFloor(getWord(%screen, 1) - %ph),
                      %pw, %ph);

        %plate.setVisible(1);
        %used++;
    }

    for(%i = %used; %i < $MRPG::Plate::MaxPlates; %i++)
        $MRPG::Plate::Ctrl[%i].setVisible(0);

    // scheduleNoQuota, not schedule - a 1ms self-rescheduling loop on the normal
    // scheduler burns through Blockland's per-tick schedule quota. This is the
    // same mechanism Client_HataInputCamSync uses for its per-frame camera loop.
    $MRPG::Plate::LoopSch = scheduleNoQuota($MRPG::Plate::TickMS, 0, MRPGPlates_Loop);
}

function MRPGPlates_HideAll()
{
    for(%i = 0; %i < $MRPG::Plate::MaxPlates; %i++)
    {
        if(isObject($MRPG::Plate::Ctrl[%i]))
            $MRPG::Plate::Ctrl[%i].setVisible(0);
    }
}

// Walks the whole pipeline and reports where it breaks. Run from the console:
//   MRPGPlates_Debug();
// Each stage feeds the next, so the FIRST line that looks wrong is the culprit.
function MRPGPlates_Debug()
{
    echo("---------- MRPG NAMEPLATE DIAGNOSTIC ----------");
    echo("1. inMonsterRPGServer flag :" SPC ($MonsterRPG::Client::inMonsterRPGServer ? "1 (ok)" : "0  <-- MRPGPlates_Start never ran"));
    echo("2. ServerConnection        :" SPC (isObject(ServerConnection) ? "ok," SPC ServerConnection.getCount() SPC "ghosts" : "MISSING"));
    echo("3. Plate controls built    :" SPC (isObject(MRPG_PlateSet) ? "ok" : "MISSING  <-- Build never ran"));

    %obj = ServerConnection.getControlObject();
    echo("4. Control object          :" SPC (isObject(%obj) ? %obj SPC %obj.getClassName() : "MISSING"));

    // Raw candidate probe. Any that this build does not expose will print an
    // "Unable to find function" line just above its result - that is expected
    // and is itself the answer for that candidate.
    if(isObject(%obj))
    {
        echo("5. Camera source candidates:");
        echo("     a) conn.getControlCameraTransform :" SPC (ServerConnection.getControlCameraTransform() $= "" ? "empty" : ServerConnection.getControlCameraTransform()));
        echo("     b) obj.getEyeTransform            :" SPC (%obj.getEyeTransform() $= "" ? "empty" : %obj.getEyeTransform()));
        echo("     c) obj.getTransform               :" SPC (%obj.getTransform() $= "" ? "empty" : %obj.getTransform()));
        echo("     d) obj.getEyePoint                :" SPC (%obj.getEyePoint() $= "" ? "empty" : %obj.getEyePoint()));
        echo("     e) obj.getEyeVector               :" SPC (%obj.getEyeVector() $= "" ? "empty" : %obj.getEyeVector()));
        echo("     f) obj.getForwardVector           :" SPC (%obj.getForwardVector() $= "" ? "empty" : %obj.getForwardVector()));
        echo("     g) obj.getPosition                :" SPC (%obj.getPosition() $= "" ? "empty" : %obj.getPosition()));
        echo("     h) $Player_EyeVector (relayed)    :" SPC ($Player_EyeVector $= "" ? "empty" : $Player_EyeVector));
    }

    MRPG_DetectCameraSource();
    echo("   -> chosen source        :" SPC $MRPG::Cam::Source SPC ($MRPG::Cam::Source $= "none" ? " <-- no usable camera" : ($MRPG::Cam::Source $= "xform" ? " (yaw only, no pitch)" : "")));

    %cam = MRPG_GetCameraTransform();
    echo("6. Camera transform        :" SPC (%cam $= "" ? "EMPTY  <-- projection cannot run" : %cam));
    echo("7. FOV / third person      :" SPC MRPG_GetCameraFov() SPC "/ TPOn=" @ $TPOn);

    // Walk the raw ghost list rather than the cache, so this still reports
    // something useful when the rescan is what is failing.
    %seen = 0;
    %parsed = 0;

    for(%i = 0; %i < ServerConnection.getCount(); %i++)
    {
        %g = ServerConnection.getObject(%i);

        if(%g.getClassName() !$= "AIPlayer")
            continue;

        %seen++;

        if(%seen > 3)
            continue;

        %raw = %g.getShapeName();
        echo("   bot" SPC %g SPC "shapeName =" SPC (%raw $= "" ? "<EMPTY>" : "\"" @ %raw @ "\""));
        echo("        fields =" SPC getFieldCount(%raw) SPC "(need 3)");

        if(getFieldCount(%raw) >= 3)
        {
            %parsed++;
            %sz = getWord(%g.getScale(), 2);
            if(%sz <= 0) %sz = 1;
            %head = vectorAdd(%g.getPosition(), "0 0" SPC ($MRPG::Plate::HeadOffset * %sz));
            %scr = worldToScreen(%cam, %head);
            echo("        projected =" SPC (%scr $= "" ? "<behind camera / no cam>" : %scr) SPC " canvas =" SPC Canvas.getExtent());
        }
    }

    echo("8. AIPlayer ghosts seen    :" SPC %seen);
    echo("9. Parsed as MRPG bots     :" SPC %parsed SPC (%parsed == 0 && %seen > 0 ? " <-- shape name payload not arriving" : ""));
    echo("10. Cached BotCount        :" SPC $MRPG::Plate::BotCount);
    echo("11. Loop running           :" SPC ($MRPG::Plate::LoopSch !$= "" ? "sch=" @ $MRPG::Plate::LoopSch : "NOT SCHEDULED"));
    echo("-----------------------------------------------");
}

function MRPGPlates_Start()
{
    // Bail unless a real camera source exists. Without one the projection
    // produces a near-constant and plates sit frozen in a corner, which is
    // worse than not drawing them - the server falls back to engine-rendered
    // shape names, which track correctly.
    MRPG_DetectCameraSource();

    if($MRPG::Cam::Source $= "none" || $MRPG::Cam::Source $= "")
    {
        warn("MRPG nameplates: no client-side camera source available - " @
             "falling back to engine shape names. Run MRPGPlates_Debug() for detail.");
        return;
    }

    MRPGPlates_Build();
    MRPGPlates_Rescan();
    MRPGPlates_Loop();
}

function MRPGPlates_Stop()
{
    cancel($MRPG::Plate::LoopSch);
    cancel($MRPG::Plate::ScanSch);

    $MRPG::Plate::BotCount = 0;

    MRPGPlates_HideAll();
}
