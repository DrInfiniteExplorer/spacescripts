

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    IGC.UnicastListener.SetMessageCallback("yolo?");
    IGC.SendBroadcastMessage("YoWhereAreYouMan", "I am lost");    
}

string cockpitName = null;
IMyCockpit cachedCockpit;
List<IMyCockpit> cockpitList = new List<IMyCockpit>();
IMyCockpit GetCockPit() {    
    if(cachedCockpit == null || GridTerminalSystem.GetBlockWithId(cachedCockpit.EntityId) == null) {
        GridTerminalSystem.GetBlocksOfType(cockpitList, x => x.IsSameConstructAs(Me));
        cachedCockpit = cockpitList.FirstOrDefault(x => cockpitName == null || x.CustomName == cockpitName);
    }
    return cachedCockpit;    
}

Vector3D yawReferenceWorld = new Vector3D(0,0,1); 
List<IMyTerminalBlock> gyros = new List<IMyTerminalBlock>();

string prevCustomData;
Vector3D dockingOffset;
void LoadConf() {
    if(Me.CustomData == prevCustomData) return;
    var conf = Me.CustomData.LoadDict();
    Vector3D.TryParse(conf.GetValue("DockingAdjustment", "X: 0 Y:0 Z:0"), out dockingOffset);

    cockpitName = conf.GetValue("Cockpit", null);
    cachedCockpit = null;

    prevCustomData = Me.CustomData;
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

// "Natural fwd" is 0,0,-1 according to Quaternion.CreateFromForwardUp as that gives identity rotation
//Vector3D savedFwd = new Vector3D(-1, 0, 0); // "90deg right"
Vector3D savedFwd = new Vector3D(0, 0, -1); // identity rotation
Vector3D savedUp = new Vector3D(0, 1, 0);
Vector3D savedGridAlignPos = new Vector3D(0, 0, 0); // start w. world-aligned

Vector3D? targetPos;
void Main(string argument) {
    while(IGC.UnicastListener.HasPendingMessage)
    {
        var msg = IGC.UnicastListener.AcceptMessage();
        if(msg.Tag == "We here") {
            var lines = (msg.Data as string).Split('\n');
            Echo(lines.ToString());
            var alignGridName = lines[0];
            var alignFwd = lines[1];
            var alignUp = lines[2];
            var alignGridPos = lines[3];
            Vector3D temp;
            if(!Vector3D.TryParse(alignFwd, out temp)) return;
            savedFwd = temp;
            if(!Vector3D.TryParse(alignUp, out temp)) return;
            savedUp = temp;
            if(!Vector3D.TryParse(alignGridPos, out temp)) return;
            savedGridAlignPos = temp;
        }
        if(msg.Tag == "These fellas") {
            var lines = (msg.Data as string).Split('\n');
            foreach(var line in lines) {
                Vector3D temp;
                if(Vector3D.TryParse(line, out temp))
                {
                    targetPos = temp;                    
                }
            }
        }
    }
    
    LoadConf();

    StringBuilder displayString = new StringBuilder();
    Action<String> write = delegate(string s) {
        Echo(s);
        displayString.Append(s + "\n");
    };

    //write($"FWD: {str(savedFwd)}");
    //write($"UP: {str(savedUp)}");
    IMyCockpit cockpit = null;
    try {
        cockpit = GetCockPit();
        if(cockpit == null) return;
        //write($"F,U: {cockpit.Orientation.Forward} {cockpit.Orientation.Up}");

        if(argument == "save") {
            savedFwd = cockpit.WorldMatrix.Forward;
            savedUp = cockpit.WorldMatrix.Up;
            return;
        }
        if(argument == "restore") {            
        }
        if(argument == "front")
        {
            savedFwd = new Vector3D(0, 0, -1);
        }
        if(argument == "right")
        {
            savedFwd = new Vector3D(1, 0, 0);
        }
        if(argument == "left")
        {
            savedFwd = new Vector3D(-1, 0, 0);
        }
        if(argument == "back")
        {
            savedFwd = new Vector3D(0, 0, 1);
        }
        if(argument == "askGrid")
        {
            IGC.SendBroadcastMessage("YoWhereAreYouMan", "I am lost");
        }
        if(argument == "askDocking")
        {
            IGC.SendBroadcastMessage("YoWhereAreYouMan", "I need this:DockingConnector:Above");
        }
        
        if(targetPos.HasValue)
        {
            var docker = GridTerminalSystem.GetBlockWithName("Connector");
            var autoPilot = GridTerminalSystem.GetBlockWithName("Remote Control");
            
            var offset = autoPilot.WorldMatrix.Translation - docker.WorldMatrix.Translation;
            offset += Vector3D.TransformNormal(dockingOffset, docker.WorldMatrix);
            
            targetPos = targetPos.Value + offset;
            cockpit.writeLeft($"Target:\n{targetPos.Value.X:F2}\n{targetPos.Value.Y:F2}\n{targetPos.Value.Z:F2}");
            IMyRemoteControl control = GridTerminalSystem.GetBlockWithName("Remote Control") as IMyRemoteControl;            
            control.ClearWaypoints();
            control.AddWaypoint(targetPos.Value, "Docking point");
            control.FlightMode = FlightMode.OneWay;
            control.SetAutoPilotEnabled(true);
            targetPos = null;
        }
        
        //var hereFromOrigo = cockpit.WorldMatrix.Translation;
        //write(str(hereFromOrigo));
        
        
        
        //write(str(Vector3D.TransformNormal(new Vector3D(1, 0, 0), cockpit.WorldMatrix)));
        //write(str(cockpit.WorldMatrix));
        
        //write("WF " + str(cockpit.WorldMatrix.Forward)); // actual forward vector in world
        //write("WU " + str(cockpit.WorldMatrix.Up)); // actual up vector in world

        var saveToWorld = Quaternion.CreateFromForwardUp(savedFwd, savedUp);
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
        
        write("rot: " + rot);
        write("axi: " + str(axis));
        //write("ayy");
        write(targetPos?.ToString());
        
        //Vector3D rotation = new Vector3D(axis.X, axis.Y, axis.Z);
        Vector3D rotation = new Vector3D(fullRot.X, fullRot.Y, fullRot.Z) * (fullRot.W >= 0 ? 1: -1);
        //write("Q: " + str(fullRot));
        //write("rot: " + str(rotation));
        Matrix cockToShipMat;
        cockpit.Orientation.GetMatrix(out cockToShipMat);
        GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros, x => ((IMyGyro)x).GyroOverride);
        foreach(var block in gyros) {
            //write($"F,U: {block.Orientation.Forward} {block.Orientation.Up}");

            var gyro = block as IMyGyro;
            gyro.Orientation.GetMatrix(out gyroToShipMat);
            Matrix shipToGyroMat = Matrix.Transpose(gyroToShipMat);
            
            Matrix fullMat = Matrix.Multiply(cockToShipMat, shipToGyroMat);
            var rotated = Vector3D.TransformNormal(rotation, fullMat);
            //var rotated = Vector3D.TransformNormal(rotation, cockToShipMat);
            //rotated = Vector3D.TransformNormal(rotation, shipToGyroMat);
            //write(str(rotation));
            //write(str(rotated));

            gyro.Pitch = (float)rotated.X;
            gyro.Yaw = (float)rotated.Y;
            gyro.Roll = (float)rotated.Z;
        }
    }
    finally {
        
        //var displayBlock = GridTerminalSystem.GetBlockWithName("Display") as IMyTextPanel;
        var displayBlock = cockpit.GetSurface(0);
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

public static void writeLeft(this IMyCockpit cockpit, string what, bool flush=true) {
    var displayBlock = cockpit.GetSurface(1);
    var displayString = flush ? what : displayBlock.GetText() + "\n" + what;
    if(displayBlock == null) {
        return;
    } else {
        displayBlock.ContentType = ContentType.TEXT_AND_IMAGE;
        displayBlock.Alignment = TextAlignment.CENTER;
        var pixelsAsFont1 = displayBlock.MeasureStringInPixels(new StringBuilder(displayString), "Debug", 1.0f);
        var pixelsOnSurface = displayBlock.SurfaceSize;
        var ayy = pixelsOnSurface / pixelsAsFont1;
        displayBlock.FontSize = Math.Min(ayy.X, ayy.Y) * 2.0f;
        displayBlock.WriteText(displayString, false);
    }
    
}

