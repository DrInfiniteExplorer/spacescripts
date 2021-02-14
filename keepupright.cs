

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

IMyCockpit cachedCockpit;
List<IMyCockpit> cockpitList = new List<IMyCockpit>();
IMyCockpit GetCockPit() {    
    if(cachedCockpit == null || GridTerminalSystem.GetBlockWithId(cachedCockpit.EntityId) == null) {
        GridTerminalSystem.GetBlocksOfType(cockpitList, x => x.IsSameConstructAs(Me));
        cachedCockpit = cockpitList.FirstOrDefault();
    }
    return cachedCockpit;    
}

Vector3D yawReferenceWorld = new Vector3D(0,0,1); 
List<IMyTerminalBlock> gyros = new List<IMyTerminalBlock>();

string prevCustomData;
double desiredYaw = 0;
double desiredPitch = 0;
double desiredRoll = 0;
void LoadConf() {
    if(Me.CustomData == prevCustomData) return;
    var conf = Me.CustomData.LoadDict();
    desiredYaw = float.Parse(conf.GetValue("DesiredYaw", "0"));
    desiredPitch = float.Parse(conf.GetValue("DesiredPitch", "0"));
    desiredRoll = float.Parse(conf.GetValue("DesiredRoll", "0"));
    prevCustomData = Me.CustomData;
}


void Main(string argument) {
    
    LoadConf();

    StringBuilder displayString = new StringBuilder();
    Action<String> write = delegate(string s) {
        Echo(s);
        displayString.Append(s + "\n");
    };

    IMyCockpit cockpit = null;
    try {
        cockpit = GetCockPit();
        if(cockpit == null) return;
        
        Matrix cockToShipMat;
        cockpit.Orientation.GetMatrix(out cockToShipMat);
        Matrix shipToCockMat = Matrix.Transpose(cockToShipMat);
        Matrix worldToCockMat = MatrixD.Transpose(cockpit.WorldMatrix);

        Vector3D reversedGravity = Vector3D.Normalize(-cockpit.GetTotalGravity());
        if(reversedGravity != reversedGravity) return; // getting NaN in space lol
        Vector3D reversedGravityCockSpace = Vector3D.TransformNormal(reversedGravity, worldToCockMat);
        Vector3D reversedGravityShipSpace = Vector3D.TransformNormal(reversedGravity, shipToCockMat);
        Vector3D yawReferenceCock = Vector3D.TransformNormal(yawReferenceWorld, worldToCockMat);
        
        var yaw = Math.Atan2(yawReferenceCock.Z, yawReferenceCock.X);
        var roll = Math.Atan2(reversedGravityCockSpace.X, reversedGravityCockSpace.Y);
        var pitch = Math.Atan2(reversedGravityCockSpace.Z, reversedGravityCockSpace.Y);        
        
        write($"Yaw: {yaw:F2} ");
        write($"Pitch: {pitch:F2}");
        write($"Roll: {roll:F2}");
        
        GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros, x => ((IMyGyro)x).GyroOverride && x.CubeGrid == Me.CubeGrid);
        shipToCockMat = cockToShipMat;
        Matrix gyroToShipMat;
        
        var freeYaw = GridTerminalSystem.GetBlockWithName("FreeYaw")?.IsWorking ?? false; 
        var freePitch = GridTerminalSystem.GetBlockWithName("FreePitch")?.IsWorking ?? false;
        var freeRoll = GridTerminalSystem.GetBlockWithName("FreeRoll")?.IsWorking ?? false;
        
        Vector3D rotation = new Vector3D(freePitch ? 0 : -pitch, 0 , freeRoll ? 0 : roll);
        foreach(var block in gyros) {
            var gyro = block as IMyGyro;
            gyro.Orientation.GetMatrix(out gyroToShipMat);
            Matrix shipToGyroMat = Matrix.Transpose(gyroToShipMat);
            
            Matrix fullMat = Matrix.Multiply(cockToShipMat, shipToGyroMat);
            var rotated = Vector3D.TransformNormal(rotation, fullMat);
            //var rotated = Vector3D.TransformNormal(rotation, cockToShipMat);
            //rotated = Vector3D.TransformNormal(rotation, shipToGyroMat);

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
            displayBlock.FontSize = 3.2f;
            displayBlock.Alignment = TextAlignment.CENTER;
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
