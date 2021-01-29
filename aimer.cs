

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
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

string prevCustomData;
string rotorYawName;
string rotorPitchName;
void LoadConf() {
    if(Me.CustomData == prevCustomData) return;
    var conf = Me.CustomData.LoadDict();
    cockpitName = conf.GetValue("Cockpit", null);
    cachedCockpit = null;
    rotorYawName = conf.GetValue("RotorYaw", null);
    rotorPitchName = conf.GetValue("RotorPitch", null);
    prevCustomData = Me.CustomData;
}


float targetYaw = 0;
float targetPitch = 0;

string str(Vector2 v) {
    return $"{v.X:F2}, {v.Y:F2}";
}

float normalize(float angle) {
    while(angle >= Math.PI) {
        angle -= (float)(2*Math.PI);
    }
    while(angle < -Math.PI) {
        angle += (float)(2*Math.PI);
    }
    return angle;
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
        if(rotorYawName == null || rotorPitchName == null) return;
        
        var yawy = GridTerminalSystem.GetBlockWithName(rotorYawName) as IMyMotorAdvancedStator;
        var pitchy = GridTerminalSystem.GetBlockWithName(rotorPitchName) as IMyMotorAdvancedStator;
        if(yawy == null || pitchy == null) return;
        
        var rot = cockpit.RotationIndicator;

        targetYaw = normalize(targetYaw + rot.Y * 0.01f);
        float angleDiff = normalize(targetYaw - yawy.Angle);
        yawy.TargetVelocityRad = angleDiff*2.0f;
        
        targetPitch = normalize(targetPitch - rot.X * 0.01f);
        angleDiff = normalize(targetPitch - pitchy.Angle);
        pitchy.TargetVelocityRad = angleDiff * 2.0f;
        
        write($"{targetYaw:F2}");
        write($"{targetPitch:F2}");
        
        

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
