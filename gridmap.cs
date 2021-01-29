

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}


bool useSelfDisplay = false;

string prevCustomData;
void LoadConf() {
    if(Me.CustomData == prevCustomData) return;
    prevCustomData = Me.CustomData;
    
    var conf = Me.CustomData.LoadDict();
    useSelfDisplay = conf.GetValue("SelfDisplay", "false") == "true";
}

IEnumerable<Vector3I> AllBetween(Vector3I min, Vector3I max) {
    for(int x = min.X; x <= max.X; x++) {
      for(int y = min.Y; y <= max.Y; y++) {
        for(int z = min.Z; z <= max.Z; z++) {
            yield return new Vector3I(x,y,z);
        }
      }
    }
}

class PlaneMap {
    Vector3I wide;
    public int wideLow = int.MaxValue;
    public int wideHigh = int.MinValue;
    public int wideExtent { get { return wideHigh - wideLow; }}

    Vector3I high;
    public int highLow = int.MaxValue;
    public int highHigh = int.MinValue;
    public int highExtent { get { return highHigh - highLow; }}
    
    Vector3I planeIsh;
    
    Dictionary<Vector3I, char> map = new Dictionary<Vector3I, char>();

    public PlaneMap(Vector3I wide, Vector3I high) {        
        this.wide = wide;
        this.high = high;
        planeIsh = wide + high;
    }
    
    public Vector3I Mark(Vector3I pos, IMySlimBlock block, char mark = '▓') {
        var projected = pos * planeIsh;
        map[projected] = mark;
        int alongWide = wide.Dot(ref pos);
        int alongHigh = high.Dot(ref pos);
        if(alongWide < wideLow) wideLow = alongWide;
        if(alongWide > wideHigh) wideHigh = alongWide;
        if(alongHigh < highLow) highLow = alongHigh;
        if(alongHigh > highHigh) highHigh = alongHigh;        
        return projected;
    }
    
    public char Get(int wide, int high) {
        var pos = this.wide * (wide + wideLow) + this.high * (high + highLow);
        return map.GetValue(pos, ' ');
    }
}

List<IMyTerminalBlock> listy = new List<IMyTerminalBlock>();

IEnumerable<UpdateFrequency> Run(Vector3I wide, Vector3I high, string displayName) {
    StringBuilder displayString = new StringBuilder();
    Action<String> write = delegate(string s) {
        Echo(s);
        displayString.Append(s + "\n");
    };

    try {
        var grid = Me.CubeGrid;
        PlaneMap map = new PlaneMap(wide, high);
        int count = 0;
        foreach(var gridPos in AllBetween(grid.Min, grid.Max)) {
            if(count == 5000) {
                Echo("Pausing execution...");
                count = 0;
                yield return UpdateFrequency.Update1;
            }
            count++;
            if(!grid.CubeExists(gridPos)) continue;
            var slim = grid.GetCubeBlock(gridPos);
            var projected = map.Mark(gridPos, slim);
        }
        
        GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(listy);
        foreach(var remote in listy) {
            map.Mark(remote.Position, null, 'R');
        }
        
        map.Mark(Me.Position, null, 'M');
        
        write($"yolo {map.wideExtent}x{map.highExtent} {map.wideLow}-{map.wideHigh} {map.highLow}-{map.highHigh}");
        
        StringBuilder s = new StringBuilder();
        for(int y = 0; y <= map.highExtent; ++y) {
            s.Clear();
            for(int x=0; x <= map.wideExtent; ++x) {
                s.Append(map.Get(x,y));
            }
            write(s.ToString());
            yield return UpdateFrequency.Update1;
        }
        
    }
    finally {
        var displayBlock = GridTerminalSystem.GetBlockWithName(displayName) as IMyTextSurface;
        if(useSelfDisplay) displayBlock = Me.GetSurface(0);
        //var displayBlock = Me.GetSurface(0);
        if(displayBlock == null) {
            Echo("No item display :(");
        } else {
            displayBlock.ContentType = ContentType.TEXT_AND_IMAGE;
            displayBlock.FontSize = 1.2f;
            displayBlock.Font = "Monospace";
            displayBlock.Alignment = TextAlignment.CENTER;
            
            var pixelsAsFont1 = displayBlock.MeasureStringInPixels(displayString, "Monospace", 1.0f);
            var pixelsOnSurface = displayBlock.SurfaceSize;
            var ayy = pixelsOnSurface / pixelsAsFont1;
            displayBlock.FontSize = Math.Min(ayy.X, ayy.Y);
            
            displayBlock.WriteText(displayString, false);
        }
    }    
    if(useSelfDisplay) {
        yield return 0;
    }
}

IEnumerator<UpdateFrequency> state;
void Main(string argument) {
    LoadConf();

    Me.GetSurface(1).ContentType = ContentType.TEXT_AND_IMAGE;
    Me.GetSurface(1).WriteText("Map\nComputer");
    Me.GetSurface(1).FontSize = 4.5f;
    Me.GetSurface(1).Alignment = TextAlignment.CENTER;
    
    if(state == null) {
        var enumerable = new[] { 
            Run(new Vector3I(1, 0, 0), new Vector3I(0, 0, 1), "MapDisplayXZ"),
            Run(new Vector3I(1, 0, 0), new Vector3I(0, 1, 0), "MapDisplayXY"),
            Run(new Vector3I(0, 0, 1), new Vector3I(0, 1, 0), "MapDisplayZY"),
        }.SelectMany(x => x);
        
        state = enumerable.GetEnumerator();
    }
    if(!state.MoveNext())
    {
        state.Dispose();
        state = null;
        Runtime.UpdateFrequency = 0;
    } else {
        Runtime.UpdateFrequency = state.Current;
        //Runtime.UpdateFrequency = 0;
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
