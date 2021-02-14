
private MyIni configIni = new MyIni();

IMyBroadcastListener queryListener;

string prevCustomData;
void LoadConf() {
    if(Me.CustomData == prevCustomData) return;
    prevCustomData = Me.CustomData;

    var configParsedOk = configIni.TryParse(Me.CustomData);
    Echo($"Parse: {configParsedOk}");
}


public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Once;
    
    //IGC.SendBroadcastMessage("YoWhereAreYouMan", "I am lost");
    
    queryListener = IGC.RegisterBroadcastListener("YoWhereAreYouMan");
    queryListener.SetMessageCallback("ayy?");
    
    Me.GetSurface(1).ContentType = ContentType.TEXT_AND_IMAGE;
    Me.GetSurface(1).WriteText("Grid Announce\nComputer");
    Me.GetSurface(1).FontSize = 3.8f;
    Me.GetSurface(1).Alignment = TextAlignment.CENTER;    
}

Action<String> write;
void Main(string argument, UpdateType updateSource)
{
    LoadConf();
    
    StringBuilder displayString = new StringBuilder();
    write = delegate(string s) {
        Echo(s);
        displayString.Append(s + "\n");
    };
    try {
        if((updateSource & UpdateType.IGC) != 0) {
            write(argument);
            write(updateSource.ToString());
            //IGC.SendBroadcastMessage(YoWeAreHereMan, toSay);
            while(queryListener.HasPendingMessage) {
                var msg = queryListener.AcceptMessage();                
                DealWithMessage(msg);
            }
            
        }
        
        if(argument.StartsWith("Fake:"))
        {
            MyIGCMessage fake = new MyIGCMessage(argument.Split(':')[1], "YoWhereAreYouMan", 0);
            DealWithMessage(fake);
        }
    }
    finally {
        
        //var displayBlock = GridTerminalSystem.GetBlockWithName("Display") as IMyTextPanel;
        var displayBlock = Me.GetSurface(0);
        if(displayBlock == null) {
            Echo("No item display :(");
        } else {
            displayBlock.ContentType = ContentType.TEXT_AND_IMAGE;
            displayBlock.Alignment = TextAlignment.CENTER;
            var pixelsAsFont1 = displayBlock.MeasureStringInPixels(displayString, "Debug", 1.0f);
            var pixelsOnSurface = displayBlock.SurfaceSize;
            var ayy = pixelsOnSurface / pixelsAsFont1;
            displayBlock.FontSize = Math.Min(ayy.X, ayy.Y);
            displayBlock.FontSize = 1.8f;
            displayBlock.WriteText(displayString, false);
        }
    }
}

bool getBlock(string name, string verb, out Vector3D worldPos, out Quaternion orientation)
{
    List<IMyTerminalBlock> blockList = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyEntity>(blockList, x => x.CustomName == name);
    worldPos = Vector3D.Zero;
    orientation = Quaternion.Identity;
    if(blockList.Count() == 0) return false;
    var block = blockList[0];
    worldPos = block.WorldMatrix.Translation;
    orientation = Quaternion.CreateFromRotationMatrix(block.WorldMatrix);
    if(verb == "Forward") {
        worldPos += 2.5 * Vector3D.Transform(Base6Directions.GetVector(Base6Directions.Direction.Forward), orientation);
    }
    else if(verb == "Backward") {
        worldPos += 2.5 * Vector3D.Transform(Base6Directions.GetVector(Base6Directions.Direction.Backward), orientation);
    }
    else if(verb == "Above") {
        worldPos += 2.5 * Vector3D.Transform(Base6Directions.GetVector(Base6Directions.Direction.Up), orientation);
    }
    else if(verb == "Below") {
        worldPos += 2.5 * Vector3D.Transform(Base6Directions.GetVector(Base6Directions.Direction.Down), orientation);
    }
    else if(verb == "Left") {
        worldPos += 2.5 * Vector3D.Transform(Base6Directions.GetVector(Base6Directions.Direction.Left), orientation);
    }
    else if(verb == "Right") {
        worldPos += 2.5 * Vector3D.Transform(Base6Directions.GetVector(Base6Directions.Direction.Right), orientation);
    }
    return true;
}

void DealWithMessage(MyIGCMessage msg)
{
    write(msg.Tag);
    write(msg.Source.ToString());
    write(msg.Data.ToString());
    if(msg.Tag != "YoWhereAreYouMan") {
        return;
    }
    var query = msg.Data as string;
    if(query == "QueryGridInfo") {
        var toSay = $"{Me.CubeGrid.CustomName}\n{Me.WorldMatrix.Forward}\n{Me.WorldMatrix.Up}\n{Me.WorldMatrix.Translation}\n";
        IGC.SendUnicastMessage(msg.Source, "GridInfo", toSay);
        write(toSay);
    }
    if(query.StartsWith("QueryBlockPosition:")) {
        var words = query.Split(':');
        if(words.Count() < 2){
            write("Not enough instructions from QueryBlockPosition");
            return;
        };
        string who = words[1];
        string verby = null;
        if(words.Count() == 3) {
            verby = words[2];
        }
        
        Vector3D pos;
        Quaternion quat;
        if(!getBlock(who, verby, out pos, out quat))
        {
            write($"Couldn't get block {who}");
            return;
        }
        var toSay = $"{pos};{quat}";
        IGC.SendUnicastMessage(msg.Source, "BlockPosition", toSay);
        write(toSay);
    }
    if(query.StartsWith("QueryDocking:"))
    {
        var words = query.Split(':');
        
        var shipClass = words[1];
        
        var keys = new List<MyIniKey>();
        var route = $"DockingWaypoints-{shipClass}";
        configIni.GetKeys(route, keys);
        if(keys.Count() == 0) 
        {
            route = "DockingWaypoints";
            configIni.GetKeys(route, keys);
        }
        Echo($"{keys.Count()} waypoints");
        var waypoints = string.Join("\n", keys.Where(x => x.Name != "Connector").Select( key => $"{configIni.Get(key)}"));
        Vector3D connectorPos;
        Quaternion connectorQuat;
        if(!getBlock(configIni.Get(route, "Connector").ToString(), "Forward", out connectorPos, out connectorQuat))
        {
            write("Ayy couldn't get connector :(");
            return;
        }
        
        Quaternion flipFwd = new Quaternion(1, 0, 0, 0);
        flipFwd.Normalize();
        connectorQuat = Quaternion.Concatenate(flipFwd, connectorQuat);
        
        var fullMsg = Me.CubeGrid.CustomName + "\n" + waypoints + "\n" + $"{connectorPos.X:F2},{connectorPos.Y:F2},{connectorPos.Z:F2};{connectorQuat.X:F2},{connectorQuat.Y:F2},{connectorQuat.Z:F2},{connectorQuat.W:F2}";
        
        configIni.Set("LastMsg", "Msg", "!" + fullMsg + "!");
        Me.CustomData = configIni.ToString();

        IGC.SendUnicastMessage(msg.Source, "DockingRoute", fullMsg);
        Echo(fullMsg);
        
    }
}
