

void dumpDefaultConf(Dictionary<string, string> conf) {
    conf["TargetBlock"] = conf.GetValue("TargetBlock","");
    Me.CustomData = conf.ToDebugString();
}

Dictionary<string, string> getCheckConf(string argument) {
    var conf = Me.CustomData.ParseDict();
    if(!conf.ContainsKey("TargetBlock")) {
        Echo("Config key 'TargetBlock' missing!");
        dumpDefaultConf(conf);
        return null;
    }

    return conf;
}

public void Notify(string Who, string What) {
    var who = GridTerminalSystem.GetBlockWithName(Who);
    if(who == null) {
        return;
    }
    var pb = who as IMyProgrammableBlock;
    var timer = who as IMyTimerBlock;
    if(pb != null) {
        pb.TryRun(What);
    }
    else if(timer != null) {
        if(What == "Trigger") {
            timer.Trigger();
        }else if(What == "Start") {
            timer.StartCountdown();            
        }else if(What == "Stop") {
            timer.StopCountdown();
        }else{
            Echo("Ayy lmao cant do that with timers");
        }
    }
    else {
        who.ApplyAction(What);
    }
}

void EchoEcho(string s) { Echo(s); }

void Main(string argument)
{
    StringBuilder s = new StringBuilder();
    try {
        Action<string> Echo = delegate(string ss) {
            s.Append(ss + "\n");
        };
        
        var conf = getCheckConf(argument);
        
        //var rootBlock = GridTerminalSystem.GetBlockWithName(conf["TargetBlock"]);
        
        List<IMyTerminalBlock> blockList = new List<IMyTerminalBlock>();
        GridTerminalSystem.GetBlocksOfType<IMySlimBlock>(blockList);
        
        //var damage = blockList.Cast<IMySlimBlock>().Aggregate(0, delegate(float total, IMySlimBlock block) { return total + block.AccumulatedDamage; });
        var damage = "";
        Echo($"{blockList.Count} with {damage} damage");
        
        //ShowOnHUD_On

        
    }
    finally {
        Me.GetSurface(0).ContentType = ContentType.TEXT_AND_IMAGE;
        Me.GetSurface(0).WriteText(s.ToString());
        EchoEcho(s.ToString());
    }
    
    
    
  /*  
    // HACK: tip-toeing around whitelist to get string version of MyDefinitionBase which gives "TypeId/SubtypeId" format.
foreach(var kv in projector.RemainingBlocksPerType)
{
    string idStr = kv.Key.ToString();

    // can also be turned into MyDefinitionId/MyItemType
    //var id = MyDefinitionId.Parse(idStr);
}
*/

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

public static Dictionary<string, string> ParseDict(this string str) {
    return str
        .Split('\n')
        .Select (part  => part.Split('='))
        .Where (part => part.Length == 2 && part[0].Trim() != "" && part[1].Trim() != "")
        .ToDictionary (sp => sp[0].Trim(), sp => sp[1].Trim());
}


