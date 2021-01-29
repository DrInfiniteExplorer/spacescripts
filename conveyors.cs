List<IMyInventory> GetInventories(List<IMyTerminalBlock> cargosList) {
    List<IMyInventory> inventories = new List<IMyInventory>();
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

List<List<IMyInventory>> getConveyorSystems(List<IMyInventory> inventories) {
    List<List<IMyInventory>> conveyorSystems = new List<List<IMyInventory>>();
    while(inventories.Count != 0) {
        IMyInventory first = inventories[0];
        List<IMyInventory> thisSystem = new List<IMyInventory>();
        foreach(IMyInventory inv in inventories) {
            if(first.IsConnectedTo(inv)) {
                thisSystem.Add(inv);
            }
        }
        foreach(IMyInventory inv in thisSystem) {
            inventories.Remove(inv);
        }
        conveyorSystems.Add(thisSystem);        
    }
    return conveyorSystems;
}

void moveStuffTo(IMyInventory to, List<IMyInventory> fromList) {
    IMyTerminalBlock terminal = to.Owner as IMyTerminalBlock;
    Echo($"{terminal == null}");
    string[] targets = terminal.CustomName.Replace(" Storage", "").Split(',');
    
    foreach(var inv in fromList) {
        if(inv == to) continue;
        IMyTerminalBlock invTerminal = inv.Owner as IMyTerminalBlock;
        if(invTerminal != null && invTerminal.CustomData.Contains("No touching!")) continue;
        List<MyInventoryItem> items = new List<MyInventoryItem>();
        inv.GetItems(items, delegate(MyInventoryItem item) { return true; });
        foreach(var item in items) {
            string category = item.Type.TypeId.Split('_')[1].ToLower();
            string type = item.Type.SubtypeId.ToLower();
            Echo($"{category}:{type}".Trim());
            foreach(var What in targets) {
                string what = What.ToLower().Trim();
                if(type == what || category == what) {
                    Echo("moving");
                    to.TransferItemFrom(inv, item, null);
                }
            }
        }
    }
}

void Main(string argument)
{
    List<IMyTerminalBlock> blockList = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyEntity>(blockList);

    List<IMyInventory> inventories = GetInventories(blockList);
    
    List<List<IMyInventory>> conveyorSystems = getConveyorSystems(inventories);
    
    foreach(var invList in conveyorSystems) {
        foreach(var inv in invList) {
            IMyTerminalBlock terminal = inv.Owner as IMyTerminalBlock;
            if(terminal == null) continue;
            if(!terminal.CustomName.EndsWith(" Storage")) continue;
            Echo($"Will work with {terminal.CustomName}");
            moveStuffTo(inv, invList);
        }
    }    
}