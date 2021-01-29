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

Dictionary<string, string > stupidTranslation = new Dictionary<string, string >{
    {"RadioCommunication","RadioCommunicationComponent"},
    {"Medical","MedicalComponent"},
    {"Girder","GirderComponent"},
    {"Reactor","ReactorComponent"},
    {"Detector","DetectorComponent"},
    {"Computer","ComputerComponent"},
    {"Construction","ConstructionComponent"},
};

List<string > stupidTranslationSuffixList = new List<string >{"","Component","Magazine"};


List<IMyTerminalBlock> blockList = new List<IMyTerminalBlock>();
List<MyProductionItem> currentQueue = new List<MyProductionItem>();
List<MyProductionItem> currentQueueTmp = new List<MyProductionItem>();
Dictionary<MyDefinitionId, MyFixedPoint > queueStuff = new Dictionary<MyDefinitionId, MyFixedPoint >();
List<MyInventoryItem> items = new List<MyInventoryItem>();
Dictionary<IMyAssembler, MyFixedPoint> queueSizes = new Dictionary<IMyAssembler, MyFixedPoint>();
MyFixedPoint maxStackPerQueue = (MyFixedPoint)50;
void Main(string argument)
{
    blockList.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyEntity>(blockList);
    List<IMyInventory> inventories = GetInventories(blockList);
    

    Dictionary<string, string> expectedLevelsConf = Me.CustomData.LoadDict();
    Dictionary<string, MyFixedPoint > inventory = new Dictionary<string, MyFixedPoint >();
    
    
    foreach(var inv in inventories) {
        items.Clear();
        inv.GetItems(items, delegate(MyInventoryItem item) { return true; });
        foreach(var item in items) {
            inventory[item.Type.SubtypeId] = inventory.GetValue(item.Type.SubtypeId, MyFixedPoint.Zero) + item.Amount;
        }
    }

    string assemblerName = "AssemblerAutomatic";
    blockList.Clear();
    GridTerminalSystem.SearchBlocksOfName(assemblerName, blockList, delegate(IMyTerminalBlock b) { return true; });
    List<IMyAssembler> assemblers = blockList.Cast<IMyAssembler>().Where(x => x != null).ToList();
    if(assemblers.Count == 0) {
        Echo($"No assembler of name {assemblerName}, stopping");
        return;
    }
    
    currentQueue.Clear();
    queueSizes.Clear();
    foreach(var assembler in assemblers) {        
        currentQueueTmp.Clear();
        assembler.GetQueue(currentQueueTmp);
        currentQueue.AddRange(currentQueueTmp);
        queueSizes[assembler] = currentQueueTmp.Aggregate(MyFixedPoint.Zero, (tot, entry) => tot + entry.Amount);
        Echo($"Assembler {assembler.CustomName} has {queueSizes[assembler]} queued");
    }

    // MyDefinitionId id = currentQueue[0].BlueprintId;
    // MyFixedPoint amount = currentQueue[0].Amount;

    queueStuff.Clear();
    foreach(var entry in currentQueue) {
        queueStuff[entry.BlueprintId] = queueStuff.GetValue(entry.BlueprintId, MyFixedPoint.Zero) + entry.Amount;
        //Echo($"Queue has {entry.BlueprintId}:{queueStuff[entry.BlueprintId]}");
    }
    
    foreach(var entry in expectedLevelsConf) {
        if(!inventory.ContainsKey(entry.Key)) inventory[entry.Key] = 0;
    }
    
    Dictionary<string, string> expectedLevels = expectedLevelsConf.ToDictionary(entry => entry.Key, entry => entry.Value);
    
    Action<Dictionary<string, MyFixedPoint >> QueueThingsFunc = null;
    QueueThingsFunc = delegate(Dictionary<string, MyFixedPoint> inventoryList) {
        Dictionary<string, MyFixedPoint> overFlowQueue = new Dictionary<string, MyFixedPoint>();
        foreach(var entry in inventoryList) {
            string keyy = entry.Key;
            Echo($"{keyy}:{entry.Value}");
            if(expectedLevels.ContainsKey(keyy)) {
                var expectedLevel = MyFixedPoint.DeserializeString(expectedLevels[keyy]);
                var missing = expectedLevel - entry.Value;
                
                Func<string, bool> tryQueue = delegate(string id) {
                    MyDefinitionId definitionId = MyDefinitionId.Parse(id);
                    var toBuild = missing - queueStuff.GetValue(definitionId, MyFixedPoint.Zero);
                    if(toBuild <= MyFixedPoint.Zero) {
                        return true;
                    }
                    
                    var assembler = queueSizes.ToList().OrderBy(a => (float)a.Value).FirstOrDefault().Key;
                    
                    try {
                        var queueThisMany = toBuild > maxStackPerQueue ? maxStackPerQueue : toBuild;
                        assembler.AddQueueItem(definitionId, queueThisMany);
                        Echo($"** Queued {queueThisMany} at assembler with {queueSizes[assembler]} queued");
                        if(queueThisMany != toBuild) {
                            overFlowQueue[keyy] = entry.Value;
                        }
                        queueSizes[assembler] = queueSizes[assembler] + queueThisMany;
                        queueStuff[definitionId] = queueStuff.GetValue(definitionId, MyFixedPoint.Zero) + queueThisMany;
                        return true;
                    } catch(Exception e) {
                        return false;
                    }
                };
                
                bool suffixDidIt = stupidTranslationSuffixList.Any(delegate(string suffix) {
                    return tryQueue("MyObjectBuilder_BlueprintDefinition/" + keyy + suffix);                
                });
                if(suffixDidIt) continue;
                if(!tryQueue("MyObjectBuilder_BlueprintDefinition/" + stupidTranslation.GetValue(keyy, keyy)))
                {
                    Echo($"---Ayy lmao could not queue {keyy} yo");
                }
            } else {
                expectedLevelsConf[keyy] = "";
            }
        }
        if(overFlowQueue.Count != 0) {
            QueueThingsFunc(overFlowQueue);
        }
    };
    QueueThingsFunc(inventory);
    
    Me.CustomData = expectedLevelsConf.ToDebugString();

    currentQueue.Clear();
    queueSizes.Clear();
    foreach(var assembler in assemblers) {        
        currentQueueTmp.Clear();
        assembler.GetQueue(currentQueueTmp);
        currentQueue.AddRange(currentQueueTmp);        
    }
    var totalQueuedItems = currentQueue.Aggregate(MyFixedPoint.Zero, (tot, entry) => tot + entry.Amount);
    StringBuilder displayString = new StringBuilder();
    Action<String> write = delegate(string s) {
        displayString.Append(s + "\n");
    };
    write($"{totalQueuedItems} queued");
    
    var displayBlock = GridTerminalSystem.GetBlockWithName("QueueDisplay") as IMyTextPanel;
    if(displayBlock == null) {
        return;
    }

    queueStuff.Clear();
    foreach(var entry in currentQueue) {
        queueStuff[entry.BlueprintId] = queueStuff.GetValue(entry.BlueprintId, MyFixedPoint.Zero) + entry.Amount;
    }
    foreach(var entry in queueStuff) {
        string name = entry.Key.ToString().Split('/')[1].Replace("Component",""); 
        write($"{name} : {entry.Value}");
    }


    displayBlock.WriteText(displayString, false);
    
    //Echo(dictionary.ToDebugString());
    
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
