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

int timeToUpdate = 0;
void Main(string argument)
{
    timeToUpdate = (timeToUpdate + 1) % 10;
    if(timeToUpdate != 0) return;
    
    blockList.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyEntity>(blockList);
    List<IMyInventory> inventories = GetInventories(blockList);
    
    Dictionary<string, MyFixedPoint > inventory = new Dictionary<string, MyFixedPoint >();
    
    Me.GetSurface(1).ContentType = ContentType.TEXT_AND_IMAGE;
    Me.GetSurface(1).WriteText("Inventory Display\nComputer");
    Me.GetSurface(1).FontSize = 4.5f;
    Me.GetSurface(1).Alignment = TextAlignment.CENTER;
    
    foreach(var inv in inventories) {
        items.Clear();
        inv.GetItems(items, delegate(MyInventoryItem item) { return true; });
        foreach(var item in items) {
            inventory[item.Type.SubtypeId] = inventory.GetValue(item.Type.SubtypeId, MyFixedPoint.Zero) + item.Amount;
        }
    }


    StringBuilder displayString = new StringBuilder();
    Action<String> write = delegate(string s) {
        displayString.Append(s + "\n");
    };
    
    var displayBlock = GridTerminalSystem.GetBlockWithName("ItemDisplay") as IMyTextPanel;
    if(displayBlock == null) {
        Echo("No item display :(");
        return;
    }
    foreach(var entry in inventory.OrderByDescending(x => (float)x.Value)) {
        write($"{entry.Key}: {entry.Value}");
    }
    displayBlock.WriteText(displayString, false);
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
