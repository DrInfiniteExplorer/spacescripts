
MyFixedPoint ammoPerTurret = (MyFixedPoint)10;

IEnumerable<IMyInventory> FirstAsIE(IMyInventory inv) {
    if(inv == null) yield break;
    yield return inv;
}

IMyInventory stealAmmo(IMyInventory turret, List<IMyInventory> storages, IMyInventory lastStorageWithAmmo) {
    foreach(var inv in FirstAsIE(lastStorageWithAmmo).Concat<IMyInventory>(storages)) {
        List<MyInventoryItem> items = new List<MyInventoryItem>();
        inv.GetItems(items, delegate(MyInventoryItem item) { return true; });
        foreach(var item in items) {
            string type = item.Type.SubtypeId.ToLower();
            if(type == "nato_25x184mm") {
                turret.TransferItemFrom(inv, item, ammoPerTurret);
                return inv;
            }
        }
    }
    return null;    
}

List<IMyTerminalBlock> blockList = new List<IMyTerminalBlock>();
void Main(string argument)
{
    try {
        Me.GetSurface(1).ContentType = ContentType.TEXT_AND_IMAGE;
        Me.GetSurface(1).WriteText("Load Ship\nComputer");
        Me.GetSurface(1).FontSize = 1.2f;
        Me.GetSurface(1).Alignment = TextAlignment.CENTER;

        blockList.Clear();
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blockList, b => b.CubeGrid != Me.CubeGrid && b.CustomName.EndsWith(" Storage"));
        var storageInventories = blockList.SelectMany(x => x.getInventories()).ToList();

        blockList.Clear();
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blockList, b => b.CubeGrid == Me.CubeGrid && b.DefinitionDisplayNameText.Contains("Blister Turret"));
        var turretInventories = blockList.SelectMany(x => x.getInventories()).ToList();
        IMyInventory lastStorageWithAmmo = null;
        foreach(var turretInv in turretInventories) {
            lastStorageWithAmmo = stealAmmo(turretInv, storageInventories, lastStorageWithAmmo);
        }
        
    }
    finally {
        blockList.Clear();
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
