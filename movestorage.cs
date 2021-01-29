
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

List<List<IMyInventory>> getConveyorSystems_conveyorSystemsCache = new List<List<IMyInventory>>();
List<List<IMyInventory>> getConveyorSystems_conveyorSystems = new List<List<IMyInventory>>();
List<List<IMyInventory>> getConveyorSystems(List<IMyInventory> inventories) {
    var conveyorSystems = getConveyorSystems_conveyorSystems;
    conveyorSystems.Clear();
    int idx=0;
    while(inventories.Count != 0) {
        IMyInventory first = inventories[0];
        List<IMyInventory> thisSystem;
        if(idx < getConveyorSystems_conveyorSystemsCache.Count) {
            thisSystem = getConveyorSystems_conveyorSystemsCache[idx];
            thisSystem.Clear();
        } else {            
            thisSystem= new List<IMyInventory>();
            getConveyorSystems_conveyorSystemsCache.Add(thisSystem);
        }
        foreach(IMyInventory inv in inventories) {
            if(first.IsConnectedTo(inv)) {
                thisSystem.Add(inv);
            }
        }
        foreach(IMyInventory inv in thisSystem) {
            inventories.Remove(inv);
        }
        conveyorSystems.Add(thisSystem);     
        idx++;
    }
    return conveyorSystems;
}

List<MyInventoryItem> moveStuffTo_items = new List<MyInventoryItem>();
void moveStuffTo(IMyInventory to, List<IMyInventory> fromList) {
    IMyTerminalBlock terminal = to.Owner as IMyTerminalBlock;
    Echo($"{terminal == null}");
    string[] targets = terminal.CustomName.Replace(" Storage", "").Split(',');
    
    foreach(var inv in fromList) {
        if(inv == to) continue;
        IMyTerminalBlock invTerminal = inv.Owner as IMyTerminalBlock;
        if(invTerminal != null && invTerminal.CustomData.Contains("No touching!")) continue;
        bool isDrill = (inv.Owner as IMyShipDrill) != null;
        if(isDrill) 
        {
            Echo("Yolo ayy");
            return;
        }
        bool isExtractDrillName = invTerminal?.CustomName == "PlsExtractDrill";
        if(isDrill && !isExtractDrillName)  continue;
        var items = moveStuffTo_items;
        items.Clear();
        inv.GetItems(items, delegate(MyInventoryItem item) { return true; });
        foreach(var item in items) {
            string category = item.Type.TypeId.Split('_')[1].ToLower();
            string type = item.Type.SubtypeId.ToLower();
            //Echo($"{category}:{type}".Trim());
            string[] sources = invTerminal.CustomName.Replace(" Storage", "").Split(',');
            if(sources.Contains(type) || sources.Contains(category)) continue;
            foreach(var What in targets) {
                string what = What.ToLower().Trim();
                if(type == what || category == what) {
                    //Echo("moving");
                    to.TransferItemFrom(inv, item, null);
                }
            }
        }
    }
}

List<IMyTerminalBlock> blockList = new List<IMyTerminalBlock>();
void Main(string argument)
{
    Me.GetSurface(1).ContentType = ContentType.TEXT_AND_IMAGE;
    Me.GetSurface(1).WriteText("Move Storage\nComputer");
    Me.GetSurface(1).FontSize = 4.5f;
    Me.GetSurface(1).Alignment = TextAlignment.CENTER;

    blockList.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyEntity>(blockList);

    List<IMyInventory> inventories = GetInventories(blockList);
    Echo($"{inventories.Count} inventories");
    
    List<List<IMyInventory>> conveyorSystems = getConveyorSystems(inventories);
    Echo($"{conveyorSystems.Count} systems");
    
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