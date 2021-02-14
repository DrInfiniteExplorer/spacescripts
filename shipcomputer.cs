
private MyIni configIni = new MyIni();

UI mainUi = new UI();
UI auxUi = new UI();

Display alignDisplay = new Display(false);
Display comDisplay = new Display(false);

CachedBlock<IMyCockpit> cachedCockpit;
CachedBlock<IMyProgrammableBlock> cachedAutopilot;
CachedBlock<IMyShipConnector> cachedConnector;
CachedDisplay communicationDisplay;
CachedDisplay mainUiDisplay;
CachedDisplay auxUiDisplay;
public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    IGC.UnicastListener.SetMessageCallback("<Unicast Received>");

    //IGC.SendBroadcastMessage("YoWhereAreYouMan", "QueryGridInfo");

    cachedCockpit = new CachedBlock<IMyCockpit>(this);
    cachedAutopilot = new CachedBlock<IMyProgrammableBlock>(this);
    cachedConnector = new CachedBlock<IMyShipConnector>(this);
    communicationDisplay = new CachedDisplay(this);
    mainUiDisplay = new CachedDisplay(this);
    auxUiDisplay = new CachedDisplay(this);
    
    
    me = this;
}

static MyGridProgram me;

string shipClass;

string prevCustomData;
void LoadConf() {
    if(Me.CustomData == prevCustomData) return;
    prevCustomData = Me.CustomData;

    var configParsedOk = configIni.TryParse(Me.CustomData);
    var textOutputBlockName = configIni.Get("blocks", "AlignDisplay");
    
    cachedCockpit.Name = configIni.Get("blocks", "Cockpit").ToString();
    cachedAutopilot.Name = configIni.Get("blocks", "Autopilot").ToString();
    cachedConnector.Name = configIni.Get("blocks", "Connector").ToString();
    communicationDisplay.Name = configIni.Get("blocks", "ComDisplay").ToString();
    mainUiDisplay.Name = configIni.Get("blocks", "UiDisplay").ToString();
    auxUiDisplay.Name = configIni.Get("blocks", "AuxDisplay").ToString();
    
    shipClass = configIni.Get("ship", "class").ToString();
}

// "Natural fwd" is 0,0,-1 according to Quaternion.CreateFromForwardUp as that gives identity rotation
//Vector3D alignedFwd = new Vector3D(-1, 0, 0); // "90deg right"
Vector3D alignedFwd = new Vector3D(0, 0, -1); // identity rotation
Vector3D alignedUp = new Vector3D(0, 1, 0);
Vector3D savedGridAlignPos = new Vector3D(0, 0, 0); // start w. world-aligned

bool _shouldAlignToGrid = false;

List<IMyTerminalBlock> gyros = new List<IMyTerminalBlock>();
void SetGridAlignment(Vector3D? fwd, Vector3D? up) {
    _shouldAlignToGrid = fwd.HasValue && up.HasValue;
    
    
    GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros, x => ((IMyGyro)x).CustomData.Contains("AutoGyro=True"));
    foreach(var block in gyros) {
        (block as IMyGyro).GyroOverride = _shouldAlignToGrid;
    }
    if(!_shouldAlignToGrid) return;
    alignedFwd = fwd.Value;
    alignedUp = up.Value;
}

Vector3D? targetPos;
void Main(string argument) {
    
    LoadConf();
    comDisplay.AttachToSurface(communicationDisplay.Surface);
    mainUi.display.AttachToSurface(mainUiDisplay.Surface);
    auxUi.display.AttachToSurface(auxUiDisplay.Surface);
    
    mainUi.input = cachedCockpit.Block as IMyShipController;
    mainUi.Update(Runtime.TimeSinceLastRun);
    auxUi.Update(Runtime.TimeSinceLastRun);

    try {
        if(IGC.UnicastListener.HasPendingMessage) {
            //comDisplay.Clear();
            while(IGC.UnicastListener.HasPendingMessage)
            {
                DealWithMessage(IGC.UnicastListener.AcceptMessage());
            }
        }
        
        IMyCockpit cockpit = null;
        cockpit = cachedCockpit.Block;
        if(cockpit == null){
            alignDisplay.Writeln("No cockpit?");
            return;
        }
        
        if(argument != "") auxUi.Message($"Arg:\n{argument}", 2, 16);
        
        if(argument == "askAlign")
        {
            IGC.SendBroadcastMessage("YoWhereAreYouMan", "QueryGridInfo");
        }
        if(argument == "askDocking")
        {
            //IGC.SendBroadcastMessage("YoWhereAreYouMan", "I need this:DockingConnector:Above");
            IGC.SendBroadcastMessage("YoWhereAreYouMan", $"QueryDocking:{shipClass}");
            mainUi.Message("Asking for\ndocking", 2);
        }
        if(argument == "createWaypoint") {
            var keys = new List<MyIniKey>();
            configIni.GetKeys("waypoints", keys);
            var waypointCount = keys.Count();
            var newWp = new {
                Name = $"autoCreated{waypointCount+1}",
                WorldPos = Me.GetPosition(),
                ApproachSpeed = 2,
                Radius = 15
            };
            var up = Me.WorldMatrix.Up;
            var fwd = Me.WorldMatrix.Forward;
            configIni.Set("waypoints", newWp.Name, $"{newWp.WorldPos.X:0.00},{newWp.WorldPos.Y:0.00},{newWp.WorldPos.Z:0.00},{newWp.Radius},{newWp.ApproachSpeed},,{str(up)},{str(fwd)}");
            Me.CustomData = configIni.ToString();
        }
        
        if(targetPos.HasValue)
        {
            return;
            var docker = GridTerminalSystem.GetBlockWithName("Connector");
            var autoPilot = GridTerminalSystem.GetBlockWithName("Remote Control");
            
            var offset = autoPilot.WorldMatrix.Translation - docker.WorldMatrix.Translation;
            //offset += Vector3D.TransformNormal(dockingOffset, docker.WorldMatrix);
            
            targetPos = targetPos.Value + offset;
            IMyRemoteControl control = GridTerminalSystem.GetBlockWithName("Remote Control") as IMyRemoteControl;            
            control.ClearWaypoints();
            control.AddWaypoint(targetPos.Value, "Docking point");
            control.FlightMode = FlightMode.OneWay;
            control.SetAutoPilotEnabled(true);
            targetPos = null;
        }

        AlignToGrid(cockpit);
    }
    catch(Exception e) {
        mainUi.Message(e.ToString(), 10);
        configIni.Set("Exception", "Error", e.ToString());
        Me.CustomData = configIni.ToString();
    }
    finally {
        mainUi.display.Complete();
        auxUi.display.Complete();
        comDisplay.Complete();
    }
}

void DealWithMessage(MyIGCMessage msg) {
    if(msg.Tag == "GridInfo") {
        var lines = (msg.Data as string).Split('\n');
        
        var alignGridName = lines[0];
        var alignFwd = lines[1];
        var alignUp = lines[2];
        var alignGridPos = lines[3];
        comDisplay.Clear();
        comDisplay.Writeln($"Station: {alignGridName}");
        Vector3D temp;
        mainUi.QueueYesNo($"Align with {alignGridName}?", (bool yes) => {
            if(!yes) {
                
                mainUi.Message("Will not align", 2);
                SetGridAlignment(null, null);
                return;
            }
            mainUi.Message("Will align", 2);
            if(!Vector3D.TryParse(alignFwd, out temp)) return;
            alignedFwd = temp;
            comDisplay.Writeln($" GridFwd: {str(temp)}");
            if(!Vector3D.TryParse(alignUp, out temp)) return;
            alignedUp = temp;
            comDisplay.Writeln($" GridUp : {str(temp)}");
            if(!Vector3D.TryParse(alignGridPos, out temp)) return;
            comDisplay.Writeln($" GridPos: {str(temp)}");
            savedGridAlignPos = temp;
            SetGridAlignment(alignedFwd, alignedUp);
        });
    }
    else if(msg.Tag == "BlockPosition") {
        comDisplay.Clear();
        comDisplay.Writeln("Got requested positions");
        var lines = (msg.Data as string).Split('\n');
        foreach(var line in lines) {
            comDisplay.Writeln("  " + line);
            Vector3D temp;
            if(Vector3D.TryParse(line, out temp))
            {
                targetPos = temp;                    
            }
        }
    }
    else if(msg.Tag == "DockingRoute") {
        
        mainUi.Message("Docking route received", 2);
        var lines = (msg.Data as string).Split('\n');
        var stationName = lines[0];        
        var routeName = $"Station-{stationName}-DockingWaypoints";
        mainUi.Message($"Route to\n{stationName}\n{lines.Count()-1} waypoints", 2);
        
        configIni.YeetSection(routeName);
        mainUi.Message($"Storing route as\n{routeName}", 2);
        Me.CustomData = configIni.ToString();
        var configParsedOk = configIni.TryParse(Me.CustomData);
        
        for(int idx = 0; idx < lines.Count() -2; idx++)
        {
            var line = lines[idx+1];
            mainUi.Message($"Waypoint {idx} at {line}", 1);
            configIni.Set(routeName, idx.ToString(), line);
        }
        var connector = lines.Last().Split(';');
        var pos = Vec3DFromString(connector[0]);
        
        var stationConnectorQuat = QuatFromString(connector[1]);
        
        Quaternion cockpitQuat;
        Quaternion connectorQuat;
        cachedCockpit.Block.Orientation.GetQuaternion(out cockpitQuat);
        cachedConnector.Block.Orientation.GetQuaternion(out connectorQuat);

        var shipInternalRot = Quaternion.Concatenate(cockpitQuat, Quaternion.Inverse(connectorQuat));

        Quaternion quat;
        quat = Quaternion.Concatenate(shipInternalRot, stationConnectorQuat);
        
        
        var offsetConnectorToCockpitGridSpace = (cachedCockpit.Block.Position - cachedConnector.Block.Position) * Me.CubeGrid.GridSize;
        
        var offsetConnectorToCockpitConnectorSpace = Vector3D.Transform(offsetConnectorToCockpitGridSpace, Quaternion.Conjugate(connectorQuat));
        
        offsetConnectorToCockpitConnectorSpace += Me.CubeGrid.GridSizeEnum == MyCubeSize.Small ? new Vector3D(0, 0, -0.45) : new Vector3D(0, 0, 0);
        
        comDisplay.Clear();
        comDisplay.Writeln(str(offsetConnectorToCockpitGridSpace));
        comDisplay.Writeln(str(offsetConnectorToCockpitConnectorSpace));
        
        var offsetConnectorToCockpitWorldSpace = Vector3D.Transform(offsetConnectorToCockpitConnectorSpace, stationConnectorQuat);;
        
        comDisplay.Writeln(str(offsetConnectorToCockpitWorldSpace));

        pos += offsetConnectorToCockpitWorldSpace;
        
        configIni.Set(routeName, (lines.Count()-2).ToString(), $"{Vec3DToString(pos)},0.1,2,,{QuatToString(quat)}");
        mainUi.Message($"Waypoint {lines.Count()-2} at yolo", 1);
        
        AlignToNextWaypoint(routeName,"0");


        
        Me.CustomData = configIni.ToString();
        
        mainUi.QueueYesNo($"Dock with {stationName}?", (bool yes) => {
            if(yes) EngageAutopilot(routeName, "Dock");
        });
        
        EngageAutopilot(routeName, "Dock");
    }
    if(msg.Tag == "AutoPilotAdvance")
    {
        var info = (msg.Data as string).Split(':');
        var route = info[0];
        var waypoint = info[1];
        auxUi.Message($"Reached waypoint {waypoint}", 1, 16);
        
        AlignToNextWaypoint(route, waypoint);
    }
}

void AlignToNextWaypoint(string route, string waypoint)
{
    var nextWaypoint = int.Parse(waypoint)+1;
    var waypointInfo = configIni.Get(route, nextWaypoint.ToString()).ToString();
    var entries = waypointInfo.Split(',');
    if(entries.Count() > 6) {
        var orientation = QuatFromStrings(entries[6], entries[7], entries[8], entries[9]);
        
        var fwd = Vector3D.Transform(new Vector3D(0, 0, -1), orientation);
        var up = Vector3D.Transform(new Vector3D(0, 1, 0), orientation);
        SetGridAlignment(fwd, up);
    } else {
        // No next waypoint! Should we dock? Probably!
        auxUi.Message("We are probably done?", 3);
        SetGridAlignment(null, null);
        cachedConnector.Block.Connect();
    }
}

void EngageAutopilot(string route, string finalAction)
{
    mainUi.Message("AutoPilot:\n" + finalAction, 1);
    
    MyIni autopilotIni = new MyIni();    
    var configParsedOk = autopilotIni.TryParse(cachedAutopilot.Block?.CustomData);
    if(!configParsedOk) {
        mainUi.Message($"Failed to obtain\nconf from block\n{cachedAutopilot.Name}", 5);
    }
    
    autopilotIni.YeetSection("waypoints");
    
    var keys = new List<MyIniKey>();
    configIni.GetKeys(route, keys);
    keys = keys.OrderBy(x => x.Name).ToList();
    for(int idx = 0; idx < keys.Count(); idx++)
    {
        var key = keys[idx];
        var entries = configIni.Get(key).ToString().Split(',');
        
        // $"{newWp.WorldPos.X:0.00},{newWp.WorldPos.Y:0.00},{newWp.WorldPos.Z:0.00},{newWp.Radius},{newWp.ApproachSpeed},,{str(up)},{str(fwd)}");
        var pre = idx == keys.Count() ? "wait+" : "";
        var rewritten = string.Join(",", entries.Take(5)) + $",{pre}notify/{IGC.Me}/AutoPilotAdvance/{route}:{key.Name}";
        
        autopilotIni.Set("waypoints", idx.ToString(), rewritten);
    }
    
    if(cachedAutopilot.Block == null)
    {
        mainUi.Message("No autopilot block!!", 10);        
        return;
    }
    
    cachedAutopilot.Block.CustomData = autopilotIni.ToString();
    cachedAutopilot.Block?.TryRun("begin");
    
}

class UI {
    struct YesNoQuery {
        public string prompt;
        public Action<bool> action;
        public YesNoQuery(string s, Action<bool> a) 
        {
            prompt = s;
            action = a;
        }
    }
    struct MessageType {
        public string msg { get; set; }
        public TimeSpan lifetime { get; set; }
        public MessageType(string s, TimeSpan t) {
            msg = s;
            lifetime = t;
        }
    }
    Queue<MessageType> messages = new Queue<MessageType>();
    Queue<YesNoQuery> yesNoQueries = new Queue<YesNoQuery>();
    private int prevRollSign = 0;
    private TimeSpan lastTime = TimeSpan.Zero;
    private TimeSpan msgDisplayTime = TimeSpan.Zero;
    public Display display = new Display(false);
    public IMyShipController input;

    private static char[] delimiterChars = { ',', ':', '-', '/', ' ' };
    private static string[] delimiterStrs;

    public void Message(string s, int timeSeconds, int splitLength=0) {
        if(splitLength != 0)
        {
            if(delimiterStrs == null) delimiterStrs = delimiterChars.Select(x => x.ToString()).ToArray();
            for(int delimiterIdx = 0; delimiterIdx < delimiterChars.Length; delimiterIdx++)
            {
                var delimiter = delimiterChars[delimiterIdx];
                if(!s.Contains(delimiter)) continue;
                var splits = s.Split(delimiter).ToList();
                int count = 0;
                
                for(int splitIdx=0;splitIdx < splits.Count(); splitIdx++) {
                    var ss = splits[splitIdx];
                    if(count + ss.Length > splitLength) {
                        if(splitIdx+1 < splits.Count) {
                            splits[splitIdx+1] = "\n" + splits[splitIdx+1];
                        }
                        count = 0;
                    }
                    count += ss.Length;
                }
                s = string.Join(delimiterStrs[delimiterIdx], splits);
            }
        }
        var msgTime = new TimeSpan(0, 0, timeSeconds);
        var msg = new MessageType(s, msgTime);
        if(messages.Count() == 0) {
            msgDisplayTime = lastTime;
        }
        messages.Enqueue(msg);
    }
    
    public void QueueYesNo(string question, Action<bool> action) {
        yesNoQueries.Enqueue(new YesNoQuery(question, action));
    }
    
    public void Update(TimeSpan timeSinceLastUpdate) {
        lastTime += timeSinceLastUpdate;
        display.Clear();        
        int rolly = Math.Sign(input?.RollIndicator ?? 0);
        int rollDiff = Math.Sign(rolly - prevRollSign);
        int pressRoll = prevRollSign == 0 ? rollDiff : 0;
        prevRollSign = rolly;
        if(messages.Count() > 0) 
        {
            var msg = messages.Peek();
            display.Writeln(msg.msg);
            var elapsed = lastTime - msgDisplayTime;
            if(elapsed > msg.lifetime) {
                messages.Dequeue();
                msgDisplayTime = lastTime;
            }
            
            return;
        }
        
        if(yesNoQueries.Count() > 0) {
            var query = yesNoQueries.Peek();
            display.Writeln(query.prompt);
            display.Writeln("  Yes: Q");
            display.Writeln("   No: E");
            if(pressRoll == 0) return;
            yesNoQueries.Dequeue();
            query.action(pressRoll < 0);
            
        }
    }
}


void AlignToGrid(IMyCockpit cockpit) {
    if(!_shouldAlignToGrid) return;
    if(cockpit == null) return;
    
    var saveToWorld = Quaternion.CreateFromForwardUp(alignedFwd, alignedUp);
    var worldToSaved = Quaternion.Conjugate(saveToWorld);
    
    var rawCockMatrix = cockpit.WorldMatrix;
    var cockToWorld = Quaternion.CreateFromForwardUp(rawCockMatrix.Forward, rawCockMatrix.Up);
    var worldToCock = Quaternion.Conjugate(cockToWorld);
    var fullRot = Quaternion.Concatenate(cockToWorld, worldToSaved);


    Vector3 axis;
    float rot;
    fullRot.GetAxisAngle(out axis, out rot);
    axis *= rot;
    Matrix gyroToShipMat;
    
    Vector3D rotation = new Vector3D(fullRot.X, fullRot.Y, fullRot.Z) * (fullRot.W >= 0 ? 1: -1);

    Matrix cockToShipMat;
    cockpit.Orientation.GetMatrix(out cockToShipMat);
    foreach(var block in gyros) {
        var gyro = block as IMyGyro;
        gyro.Orientation.GetMatrix(out gyroToShipMat);
        Matrix fullMat = Matrix.Multiply(cockToShipMat, Matrix.Transpose(gyroToShipMat));
        var rotated = Vector3D.TransformNormal(rotation, fullMat);
        gyro.Pitch = (float)rotated.X;
        gyro.Yaw = (float)rotated.Y;
        gyro.Roll = (float)rotated.Z;
    }
}


struct CachedBlock<Type> where Type : class, IMyTerminalBlock {
    public string Name { get; set; }
    private Type cached;
    private List<Type> list;
    private MyGridProgram program;
    
    public CachedBlock(MyGridProgram program) {
        Name = null;
        cached = default(Type);
        this.program = program;
        list = new List<Type>();
    }
    
    public Type Block {
        get {
            if(program == null) return cached;
            if(cached == null || program.GridTerminalSystem.GetBlockWithId(cached.EntityId) == null || cached.CustomName != Name) {
                var proggy = program;
                program.GridTerminalSystem.GetBlocksOfType(list, x => x.IsSameConstructAs(proggy.Me));
                var name = Name;
                cached = list.FirstOrDefault(x => name == null || x.CustomName == name);
            }
            return cached;
        }
    }
}

struct CachedDisplay {
    private string displayName;
    private int displayIndex;
    public string Name {
        get{ return displayName + displayIndex.ToString(); }
        set {
            var splitty = value.Split(':');
            displayName = splitty[0];
            if(splitty.Count() == 1 || !int.TryParse(splitty[1], out displayIndex)) displayIndex = 0;
        }
    }
    private IMyTerminalBlock cachedBlock;
    private List<IMyTerminalBlock> list;
    private MyGridProgram program;
    
    public CachedDisplay(MyGridProgram program) {
        displayName = null;
        displayIndex = 0;
        cachedBlock = null;
        this.program = program;
        list = new List<IMyTerminalBlock>();
    }
    
    public IMyTextSurface Surface {
        get {
            if(program == null) return null;
            if(cachedBlock == null || program.GridTerminalSystem.GetBlockWithId(cachedBlock.EntityId) == null || cachedBlock.CustomName != displayName) {
                var proggy = program;
                program.GridTerminalSystem.GetBlocksOfType(list, x => x.IsSameConstructAs(proggy.Me));
                var name = displayName;
                cachedBlock = list.FirstOrDefault(x => name == null || name == "" || x.CustomName == name);
            }
            if(cachedBlock == null) return null;
            var surface = cachedBlock as IMyTextSurface ?? (cachedBlock as IMyTextSurfaceProvider)?.GetSurface(displayIndex);

            return surface;
        }
    }
}


struct Display {
    StringBuilder builder;
    IMyTextSurface surface;

    public Display(bool yolo = false) {
        builder = new StringBuilder();
        surface = null;
    }
    public Display(IMyTerminalBlock block, int display=0) {
        builder = new StringBuilder();
        surface = null;
        AttachToSurface(block, display);
    }
    public Display(IMyGridTerminalSystem gridTerminalSystem, string blockNameAndDisplay) {
        builder = new StringBuilder();
        surface = null;
        AttachToSurface(gridTerminalSystem, blockNameAndDisplay);
    }
    
    public void AttachToSurface(IMyTerminalBlock block, int display=0) {
        surface = block as IMyTextSurface ?? (block as IMyTextSurfaceProvider)?.GetSurface(display);
    }
    public void AttachToSurface(IMyGridTerminalSystem gridTerminalSystem, string blockNameAndDisplay){
        var splitty = blockNameAndDisplay.Split(':');
        var block = gridTerminalSystem.GetBlockWithName(splitty[0]);
        surface = block as IMyTextSurface ?? (block as IMyTextSurfaceProvider)?.GetSurface(int.Parse(splitty.ElementAtOrDefault(1) ?? "0"));
    }
    public void AttachToSurface(IMyTextSurface surface){
        this.surface = surface;
    }
    
    public void Writeln(string s) {
        builder.Append(s + "\n");
    }
    
    public Action<string> Writerln() {
        var yolo = this;
        return x => yolo.Writeln(x);
    }
    
    public void Clear() {
        builder.Clear();
    }
    
    public void Complete() {
        if(surface == null) return;
        if(builder.Length == 0) {
            builder.Append(" ");
            surface.WriteText(builder, false);
            return;
        }
        if(surface.ContentType != ContentType.TEXT_AND_IMAGE) surface.ContentType = ContentType.TEXT_AND_IMAGE;
        if(surface.Alignment != TextAlignment.CENTER) surface.Alignment = TextAlignment.CENTER;
        var pixelsAsFont1 = surface.MeasureStringInPixels(builder, "Debug", 1.0f);
        var pixelsOnSurface = surface.SurfaceSize;
        var ayy = pixelsOnSurface / pixelsAsFont1;
        var yya = Math.Min(ayy.X, ayy.Y) * 2;
        if(surface.FontSize != yya) surface.FontSize = yya;
        surface.WriteText(builder, false);
    }
}


string str(Vector3D v) {
    return $"{v.X:F2}, {v.Y:F2}, {v.Z:F2}";
}
string str(Vector3 v) {
    return $"{v.X:F2}, {v.Y:F2}, {v.Z:F2}";
}
string str(Quaternion v) {
    return $"{v.X:F2}, {v.Y:F2}, {v.Z:F2} {v.W:F2}";
}
string str(MatrixD m) {
    return str(m.Col0) + "\n" +
        str(m.Col1) + "\n" +
        str(m.Col2) + "\n";
}

public static Quaternion QuatFromStrings(string x, string y, string z, string w)
{
    return new Quaternion(float.Parse(x), float.Parse(y), float.Parse(z), float.Parse(w));
}
public static Quaternion QuatFromString(string commaSeparated)
{
    var splits = commaSeparated.Split(',');
    return QuatFromStrings(splits[0],splits[1],splits[2],splits[3]);
}

public static void QuatToStrings(Quaternion q, out string x, out string y, out string z, out string w)
{
    x = q.X.ToString();
    y = q.Y.ToString();
    z = q.Z.ToString();
    w = q.W.ToString();
}

public static string QuatToString(Quaternion q) {
    return $"{q.X:F2},{q.Y:F2},{q.Z:F2},{q.W:F2}";
}


public static Vector3D Vec3DFromStrings(string x, string y, string z)
{
    return new Vector3D(float.Parse(x), float.Parse(y), float.Parse(z));
}

public static Vector3D Vec3DFromString(string commaSeparated)
{
    var splits = commaSeparated.Split(',');
    return Vec3DFromStrings(splits[0],splits[1],splits[2]);
}

public static string Vec3DToString(Vector3D v) {
    return $"{v.X:F2},{v.Y:F2},{v.Z:F2}";
}


}

static class Testy {
    public static IEnumerable<IMyInventory> getInventories(this IMyTerminalBlock block) {
        for(int inventoryIdx = 0; inventoryIdx < block.InventoryCount; ++inventoryIdx) {
            yield return block.GetInventory(inventoryIdx);
        }
    }
    
public static TV GetValue<TK, TV>(this IDictionary<TK, TV> dict, TK key, TV defaultValue = default(TV))
{
    TV value;
    return dict.TryGetValue(key, out value) ? value : defaultValue;
}

public static string ToDebugString<TKey, TValue> (this IDictionary<TKey, TValue> dictionary)
{
    return string.Join("\n", dictionary.Select(kv => kv.Key + "=" + kv.Value).ToArray()) + "\n";
}

public static Dictionary<string, string> LoadDict(this string str) {
    return str
        .Split('\n')
        .Select (part  => part.Split('='))
        .Where (part => part.Length == 2 && part[0].Trim() != "" && part[1].Trim() != "")
        .ToDictionary (sp => sp[0].Trim(), sp => sp[1].Trim());
}

public static void YeetSection(this MyIni ini, string sectionName)
{
    var keys = new List<MyIniKey>();
    ini.GetKeys(sectionName, keys);
    foreach(var key in keys) {
        ini.Delete(key);
    }
}

