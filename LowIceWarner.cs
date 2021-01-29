List<IMyInventory> GetInventories_inventories = new List<IMyInventory>();
List<IMyInventory> GetInventories(List<IMyTerminalBlock> cargosList) {
    var inventories = GetInventories_inventories;
    inventories.Clear();
    foreach(IMyTerminalBlock ayy in cargosList) {
        IMyEntity cargo = ayy as IMyEntity;
        if(cargo == null) continue;
        for(int inventoryIdx = 0; inventoryIdx < cargo.InventoryCount; ++inventoryIdx) {
            IMyInventory inv = cargo.GetInventory(inventoryIdx);
            inventories.Add(inv);
        }        
    }
    return inventories;
}


List<IMyTerminalBlock> blockList = new List<IMyTerminalBlock>();
List<MyProductionItem> currentQueue = new List<MyProductionItem>();
List<MyProductionItem> currentQueueTmp = new List<MyProductionItem>();
Dictionary<MyDefinitionId, MyFixedPoint > queueStuff = new Dictionary<MyDefinitionId, MyFixedPoint >();
List<MyInventoryItem> items = new List<MyInventoryItem>();
Dictionary<IMyAssembler, MyFixedPoint> queueSizes = new Dictionary<IMyAssembler, MyFixedPoint>();
MyFixedPoint maxStackPerQueue = (MyFixedPoint)50;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}


Dictionary<string, string> conf = null;
List<string> alertImages = new List<string> { "Cross", "Danger"};

void StartAlert() {
    var soundBlock = GridTerminalSystem.GetBlockWithName(conf["IceAlertSoundBlock"]) as IMySoundBlock;
    if(soundBlock == null) {
        Echo("No sound block??");
    } else {
        soundBlock.Play();
    }
    var displayBlock = GridTerminalSystem.GetBlockWithName(conf["VisualIndicator"]) as IMyTextPanel;
    if(displayBlock == null) {
        Echo("No display :(");
    } else {
        var color = displayBlock.BackgroundColor;
        if(color.R == 0 && color.G == 0 && color.B == 0) {
            color.B=221;
            displayBlock.BackgroundColor = color;
            displayBlock.AddImagesToSelection(alertImages, false);
        }
    }
}

void AckAlert() {
    Echo("Acking alert");
    var soundBlock = GridTerminalSystem.GetBlockWithName(conf["IceAlertSoundBlock"]) as IMySoundBlock;
    if(soundBlock == null) {
        Echo("No sound block??");
    } else {
        soundBlock.Stop();
    }
    var displayBlock = GridTerminalSystem.GetBlockWithName(conf["VisualIndicator"]) as IMyTextPanel;
    if(displayBlock == null) {
        Echo("No display :(");
    } else {
        Echo("Removing cross?");
        displayBlock.RemoveImageFromSelection("Cross");
    }
}

void StopAlert() {
    var displayBlock = GridTerminalSystem.GetBlockWithName(conf["VisualIndicator"]) as IMyTextPanel;
    if(displayBlock == null) {
        Echo("No display :(");
    } else {
        var color = displayBlock.BackgroundColor;
        color.R=0;
        color.G=0;
        color.B=0;
        displayBlock.BackgroundColor = color;
        List<string> images = new List<string>();
        displayBlock.GetSelectedImages(images);
        displayBlock.RemoveImagesFromSelection(images);
    }
}

int timeToUpdate = 0;
void Main(string argument)
{
    if(argument == "ack") {
        conf = Me.CustomData.LoadDict();
        conf["Acknowledged"] = "true";
        Me.CustomData = conf.ToDebugString();
        AckAlert();
    }
    timeToUpdate = (timeToUpdate + 1) % 10;
    if(timeToUpdate != 0) return;
    
    blockList.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyEntity>(blockList);
    List<IMyInventory> inventories = GetInventories(blockList);
    
    Dictionary<string, MyFixedPoint > inventory = new Dictionary<string, MyFixedPoint >();
    
    Me.GetSurface(1).ContentType = ContentType.TEXT_AND_IMAGE;
    Me.GetSurface(1).WriteText("Ice Warner\nComputer");
    Me.GetSurface(1).FontSize = 4.5f;
    Me.GetSurface(1).Alignment = TextAlignment.CENTER;
    
    MyFixedPoint iceLevel = MyFixedPoint.Zero;
    
    foreach(var inv in inventories) {
        items.Clear();
        inv.GetItems(items, delegate(MyInventoryItem item) {
            if(item.Type.SubtypeId == "Ice") {
                iceLevel += item.Amount;
            }
            return false;
        });
    }
    
    var bigScreen = Me.GetSurface(0);
    bigScreen.ContentType = ContentType.TEXT_AND_IMAGE;
    Me.GetSurface(0).WriteText($"Ice level: {iceLevel}");
    Me.GetSurface(0).FontSize = 1.3f;
    Me.GetSurface(0).Alignment = TextAlignment.CENTER;
    
    conf = Me.CustomData.LoadDict();
    
    MyFixedPoint threshhold = MyFixedPoint.DeserializeString(conf["WarningLevel"]);
    bool belowThreshold = iceLevel < threshhold;
    bool alertAcked = conf.GetValue("Acknowledged", "false") == "true";
    Echo($"{belowThreshold} = {iceLevel} < {threshhold}");
    Echo($"{alertAcked}");
    if(!belowThreshold)
    {
        if(alertAcked) {
            conf.Remove("Acknowledged");
            Me.CustomData = conf.ToDebugString();
            Echo("Removed ack");
            return;
        }
        StopAlert();
    }    
    if(belowThreshold && !alertAcked) {
        Echo("Should play sound");
        StartAlert();
        return;        
    }
}

}

static class Testy {
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
