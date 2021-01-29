
public static void Swap<T> (ref T lhs, ref T rhs) {
    T temp = lhs;
    lhs = rhs;
    rhs = temp;
}

void dumpDefaultConf(Dictionary<string, string> conf) {
    conf["Auto"] = conf.GetValue("Auto","");
    conf["FrontMerge"] = conf.GetValue("FrontMerge","");
    conf["BackMerge"] = conf.GetValue("BackMerge","");
    conf["FrontConnector"] = conf.GetValue("FrontConnector","");
    conf["BackConnector"] = conf.GetValue("BackConnector","");
    conf["ExtenderPiston"] = conf.GetValue("ExtenderPiston","");
    conf["AdvanceVelocityPush"] = conf.GetValue("AdvanceVelocityPush","");
    conf["AdvanceVelocityPull"] = conf.GetValue("AdvanceVelocityPull","");
    conf["Reverse"] = conf.GetValue("Reverse","false");
    conf["RailProjector"] = conf.GetValue("RailProjector","");
    conf["Notify"] = conf.GetValue("Notify","");
    Me.CustomData = conf.ToDebugString();
}

Dictionary<string, string> getCheckConf(string argument) {
    var conf = Me.CustomData.ParseDict();
    if(!conf.ContainsKey("Auto")) {
        Echo("Config key 'Auto=true|false|(number of cycles)' missing!");
        dumpDefaultConf(conf);
        return null;
    }
    if(argument == "stop") {
        conf["Auto"] = conf["Auto"] + " Paused";
        Me.CustomData = conf.ToDebugString();
    }
    if(argument == "resume") {
        conf["Auto"] = conf["Auto"].Replace(" Paused", "");
        Me.CustomData = conf.ToDebugString();
    }
    if(!conf.ContainsKey("FrontMerge")) {
        Echo("Config key 'FrontMerge' that names the front rotor is missing!");
        dumpDefaultConf(conf);
        return null;
    }
    if(!conf.ContainsKey("BackMerge")) {
        Echo("Config key 'BackMerge' that names the back rotor is missing!");
        dumpDefaultConf(conf);
        return null;
    }
    if(!conf.ContainsKey("FrontConnector")) {
        Echo("Config key 'FrontConnector' that names the front rotor is missing!");
        dumpDefaultConf(conf);
        return null;
    }
    if(!conf.ContainsKey("BackConnector")) {
        Echo("Config key 'BackConnector' that names the back rotor is missing!");
        dumpDefaultConf(conf);
        return null;
    }
    if(!conf.ContainsKey("ExtenderPiston")) {
        Echo("Config key 'ExtenderPiston' that names the piston is missing!");
        dumpDefaultConf(conf);
        return null;
    }
    if(!conf.ContainsKey("AdvanceVelocityPush")) {
        Echo("Config key 'AdvanceVelocityPush' for pushing speed is missing!");
        dumpDefaultConf(conf);
        return null;
    }
    if(!conf.ContainsKey("AdvanceVelocityPull")) {
        Echo("Config key 'AdvanceVelocityPull' for pulling speed is missing!");
        dumpDefaultConf(conf);
        return null;
    }
    if(!conf.ContainsKey("Reverse")) {
        Echo("Config key 'Reverse=true|false' for moving backwards is missing!");
        dumpDefaultConf(conf);
        return null;
    }
    return conf;
}

public Program()
{
    Runtime.UpdateFrequency = (UpdateFrequency)Enum.Parse(typeof(UpdateFrequency), Storage == "" ? "None" : Storage);
}

public void Save()
{
    Storage = Runtime.UpdateFrequency.ToString();
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
        if(conf == null) {
            Runtime.UpdateFrequency = 0;
        };

        string autoConf = conf["Auto"];
        int autoCycles; // set to 0 if TryParse fails
        Int32.TryParse(autoConf, out autoCycles);
        bool shouldUpdate = autoConf == "true" || autoCycles > 0;
        
        if(shouldUpdate) {
            if(autoCycles > 0) {
                Echo($"Auto mode, {autoCycles} steps left");
            }
            else {
                Echo($"Auto mode, infinite");
            }
        } else {
            Echo("Manual step mode");
        }
        
        Runtime.UpdateFrequency = shouldUpdate ? UpdateFrequency.Update100 : 0;

        // https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyShipConnector
        // https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyShipMergeBlock
        // https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyExtendedPistonBase
        // https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyProjector
        
        
        var FrontMerge = GridTerminalSystem.GetBlockWithName(conf["FrontMerge"]) as IMyShipMergeBlock;
        var BackMerge = GridTerminalSystem.GetBlockWithName(conf["BackMerge"]) as IMyShipMergeBlock;
        var FrontConnector = GridTerminalSystem.GetBlockWithName(conf["FrontConnector"]) as IMyShipConnector;
        var BackConnector = GridTerminalSystem.GetBlockWithName(conf["BackConnector"]) as IMyShipConnector;
        var piston = GridTerminalSystem.GetBlockWithName(conf["ExtenderPiston"]) as IMyExtendedPistonBase;
        
        IMyProjector projector = null;
        string projectorName = conf["RailProjector"];
        if(projectorName != ""){
            projector = GridTerminalSystem.GetBlockWithName(projectorName) as IMyProjector;
        }
        
        bool reverse = conf["Reverse"] == "true";
        
        if(reverse) {
            Swap(ref FrontMerge, ref BackMerge);
            Swap(ref FrontConnector, ref BackConnector);
        }
        
        
        // Safety checks:
        //  Before unlocking back, make sure locked in front, else alert!
        // If piston should extend but cant, alert!
        
        bool mFrontMerged = FrontMerge.IsConnected;
        bool mFrontEnabled = FrontMerge.Enabled;
        bool cFrontConnected = FrontConnector.Status == MyShipConnectorStatus.Connected; // Connected, Connectable, Unconnected
        bool cFrontCan = FrontConnector.Status == MyShipConnectorStatus.Connectable; // Connected, Connectable, Unconnected
        bool cFrontEnabled = FrontConnector.Enabled;
        
        bool mBackMerged =  BackMerge.IsConnected;
        bool mBackEnabled = BackMerge.Enabled;
        bool cBackConnected = BackConnector.Status == MyShipConnectorStatus.Connected; // Connected, Connectable, Unconnected
        bool cBackCan = BackConnector.Status == MyShipConnectorStatus.Connectable; // Connected, Connectable, Unconnected
        bool cBackEnabled =   BackConnector.Enabled;
        
        
        bool retracting = piston.Velocity < 0;
        bool extending = piston.Velocity > 0;
        bool stopped = piston.Velocity == 0;
        
        bool atMax = piston.CurrentPosition == piston.MaxLimit;
        bool atMin = piston.CurrentPosition == piston.MinLimit;
        
        int remainingProjectorBlocks = projector?.RemainingBlocks ?? 0;
        
        bool projectionDone = remainingProjectorBlocks == 0;

        if(mBackMerged && cBackConnected && atMin && cFrontConnected) {
            Echo("Disconnecting front");
            FrontConnector.Disconnect();
            return;
        }    
        if(mBackMerged && cBackConnected && atMin) {
            Echo("Unmergeing front, pushing forward forward");
            FrontMerge.Enabled = false;
            piston.Velocity = float.Parse(conf["AdvanceVelocityPush"]);        
            return;
        }    
        
        if(mBackMerged && cBackConnected && atMax && cFrontCan && !mFrontMerged) {
            Echo("Merging front");
            piston.Velocity = 0;
            FrontMerge.Enabled = true;
            return;
        }
        if(mFrontMerged && mBackMerged && cBackConnected && atMax && cFrontCan) {
            Echo("Connecting front");
            FrontConnector.Connect();
            return;
        }
        if(mFrontMerged && cFrontConnected && atMax && !projectionDone) {
            Echo($"Remaining projector blocks\n  before advance: {remainingProjectorBlocks}");
            return;
        }        
        if(mFrontMerged && cFrontConnected && atMax && cBackConnected && projectionDone) {
            Echo("Disconnecting back");
            BackConnector.Disconnect();
            return;
        }
        if(mFrontMerged && cFrontConnected && atMax && projectionDone) {
            Echo("Unmergeing back, pulling forward");
            // Make sure front and back are part of same thing! https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyTerminalBlock.IsSameConstructAs
            BackMerge.Enabled = false;
            piston.Velocity = -float.Parse(conf["AdvanceVelocityPull"]);
            return;
        }
        if(mFrontMerged && cFrontConnected && atMin && cBackCan && !mBackMerged) {
            Echo("Merging back");
            piston.Velocity = 0;
            BackMerge.Enabled = true;
            return;
        }
        if(mBackMerged && cFrontConnected && atMin) {
            Echo("Connecting back");
            BackConnector.Connect();
            if(autoCycles > 0) {
                autoCycles -= 1;
                conf["Auto"] = $"{autoCycles}";
                dumpDefaultConf(conf);
            }
            else if(autoConf != "true") {
                Runtime.UpdateFrequency = 0;
            }
            if(conf.ContainsKey("Notify")) {
                var notifyStr = conf["Notify"];
                if(notifyStr != "") {
                    var parts = notifyStr.Split(',');
                    Notify(parts[0],parts[1]);
                }
            }
            return;
        }
        
        Echo("Didn't do nuthin!");
        
        Echo($"mFrontMerged={mFrontMerged}");
        Echo($"mFrontEnabled={mFrontEnabled}");
        Echo($"cFrontConnected={cFrontConnected}");
        Echo($"cFrontCan={cFrontCan}");
        Echo($"cFrontEnabled={cFrontEnabled}");

        Echo($"mBackMerged={mBackMerged}");
        Echo($"mBackEnabled={mBackEnabled}");
        Echo($"cBackConnected={cBackConnected}");
        Echo($"cBackCan={cBackCan}");
        Echo($"cBackEnabled={cBackEnabled}");

        Echo($"retracting={retracting}");
        Echo($"extending={extending}");
        Echo($"stopped={stopped}");

        Echo($"atMax={atMax}");
        Echo($"atMin={atMin}");
        Echo($"projectionDone={projectionDone}");
        
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


