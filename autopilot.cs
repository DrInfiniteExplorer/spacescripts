
private MyIni configIni = new MyIni();

UI ui = new UI();

Display alignDisplay = new Display(false);
Display comDisplay = new Display(false);

CachedBlock<IMyCockpit> cachedCockpit;
CachedDisplay cachedAlignDisplay;
CachedDisplay communicationDisplay;
CachedDisplay cachedUiDisplay;
public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    IGC.UnicastListener.SetMessageCallback("yolo?");
    IGC.SendBroadcastMessage("YoWhereAreYouMan", "I am lost");
    cachedCockpit = new CachedBlock<IMyCockpit>(this);
    cachedAlignDisplay = new CachedDisplay(this);
    communicationDisplay = new CachedDisplay(this);
    cachedUiDisplay = new CachedDisplay(this);
}


List<IMyTerminalBlock> gyros = new List<IMyTerminalBlock>();

string prevCustomData;
void LoadConf() {
    if(Me.CustomData == prevCustomData) return;
    prevCustomData = Me.CustomData;

    var configParsedOk = configIni.TryParse(Me.CustomData);
    var textOutputBlockName = configIni.Get("blocks", "AlignDisplay");
    
    cachedCockpit.Name = configIni.Get("blocks", "Cockpit").ToString();
    cachedAlignDisplay.Name = configIni.Get("blocks", "AlignDisplay").ToString();
    communicationDisplay.Name = configIni.Get("blocks", "ComDisplay").ToString();
    cachedUiDisplay.Name = configIni.Get("blocks", "UiDisplay").ToString();
}

// "Natural fwd" is 0,0,-1 according to Quaternion.CreateFromForwardUp as that gives identity rotation
//Vector3D alignedFwd = new Vector3D(-1, 0, 0); // "90deg right"
Vector3D alignedFwd = new Vector3D(0, 0, -1); // identity rotation
Vector3D alignedUp = new Vector3D(0, 1, 0);
Vector3D savedGridAlignPos = new Vector3D(0, 0, 0); // start w. world-aligned

bool shouldAlignToGrid = false;

Vector3D? targetPos;
void Main(string argument) {
    
    LoadConf();
    alignDisplay.AttachToSurface(cachedAlignDisplay.Surface);
    comDisplay.AttachToSurface(communicationDisplay.Surface);
    ui.display.AttachToSurface(cachedUiDisplay.Surface);
    
    ui.input = cachedCockpit.Block as IMyShipController;
    ui.Update(Runtime.TimeSinceLastRun);

    if(IGC.UnicastListener.HasPendingMessage) {
        comDisplay.Clear();
        while(IGC.UnicastListener.HasPendingMessage)
        {
            DealWithMessage(IGC.UnicastListener.AcceptMessage());
        }
    }
    
    alignDisplay.Clear();
    alignDisplay.Writeln("yolo");

    IMyCockpit cockpit = null;
    try {
        cockpit = cachedCockpit.Block;
        if(cockpit == null){
            alignDisplay.Writeln("No cockpit?");
            return;
        }
        if(argument == "askAlign")
        {
            IGC.SendBroadcastMessage("YoWhereAreYouMan", "I am lost");
        }
        if(argument == "askDocking")
        {
            IGC.SendBroadcastMessage("YoWhereAreYouMan", "I need this:DockingConnector:Above");
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

        if(shouldAlignToGrid) {
            AlignToGrid(cockpit);
        }
    }
    finally {
        ui.display.Complete();
        alignDisplay.Complete();
        comDisplay.Complete();
    }
}

void DealWithMessage(MyIGCMessage msg) {
    if(msg.Tag == "We here") {
        var lines = (msg.Data as string).Split('\n');
        
        var alignGridName = lines[0];
        var alignFwd = lines[1];
        var alignUp = lines[2];
        var alignGridPos = lines[3];
        comDisplay.Writeln($"Station: {alignGridName}");
        Vector3D temp;
        ui.QueueYesNo($"Align with {alignGridName}?", (bool yes) => {
            if(!yes) {
                ui.Message("Will not align", 2);
                shouldAlignToGrid = true;
                return;
            }
            ui.Message("Will align", 2);
            if(!Vector3D.TryParse(alignFwd, out temp)) return;
            alignedFwd = temp;
            comDisplay.Writeln($" GridFwd: {str(temp)}");
            if(!Vector3D.TryParse(alignUp, out temp)) return;
            alignedUp = temp;
            comDisplay.Writeln($" GridUp : {str(temp)}");
            if(!Vector3D.TryParse(alignGridPos, out temp)) return;
            comDisplay.Writeln($" GridPos: {str(temp)}");
            savedGridAlignPos = temp;
            shouldAlignToGrid = true;
        });
    }
    if(msg.Tag == "These fellas") {
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
    if(msg.Tag == "DockingRoute") {
        
        comDisplay.Writeln("Docking route received");
        var lines = (msg.Data as string).Split('\n');
        var stationName = lines[0];        
        var routeName = $"Station-{stationName}-DockingWaypoints";
        foreach(string line in lines.Skip(1)) {
            var keyValue = line.Split(new char[] {'='}, 2, StringSplitOptions.RemoveEmptyEntries);
            configIni.Set(routeName, keyValue[0], keyValue[1]);
            Me.CustomData = configIni.ToString();
        }
        
        ui.QueueYesNo($"Dock with {stationName}?", (bool yes) => EngageAutopilot($"{routeName}", "Dock"));
        
        EngageAutopilot(routeName, "Dock");
    }
}

void EngageAutopilot(string route, string finalAction)
{
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

    public void Message(string s, int timeSeconds) {
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
            display.Clear();
            var query = yesNoQueries.Peek();
            display.Writeln(query.prompt);
            display.Writeln("  Yes: Q");
            display.Writeln("   No: E");
            if(input == null) return;
            var rolly = Math.Sign(input.RollIndicator);
            if(rolly == 0 || rolly == prevRollSign) return;
            prevRollSign = rolly;
            yesNoQueries.Dequeue();
            query.action(rolly < 0);
            
        }
    }
}


void AlignToGrid(IMyCockpit cockpit) {
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
    GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros, x => ((IMyGyro)x).GyroOverride);
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
        surface.ContentType = ContentType.TEXT_AND_IMAGE;
        surface.Alignment = TextAlignment.CENTER;
        var pixelsAsFont1 = surface.MeasureStringInPixels(builder, "Debug", 1.0f);
        var pixelsOnSurface = surface.SurfaceSize;
        var ayy = pixelsOnSurface / pixelsAsFont1;
        surface.FontSize = Math.Min(ayy.X * 2, ayy.Y);
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
