
void dumpDefaultConf(Dictionary<string, string> conf) {
    conf["Auto"] = conf.GetValue("Auto","");
    conf["FrontRotor"] = conf.GetValue("FrontRotor","");
    conf["BackRotor"] = conf.GetValue("BackRotor","");
    conf["ExtenderPiston"] = conf.GetValue("ExtenderPiston","");
    conf["AdvanceVelocityPush"] = conf.GetValue("AdvanceVelocityPush","");
    conf["AdvanceVelocityPull"] = conf.GetValue("AdvanceVelocityPull","");
    Me.CustomData = conf.ToDebugString();
}

Dictionary<string, string> getCheckConf() {
    var conf = Me.CustomData.ParseDict();
    if(!conf.ContainsKey("Auto")) {
        Echo("Config key 'Auto=true|false' missing!");
        dumpDefaultConf(conf);
        return null;
    }
    if(!conf.ContainsKey("FrontRotor")) {
        Echo("Config key 'FrontRotor' that names the front rotor is missing!");
        dumpDefaultConf(conf);
        return null;
    }
    if(!conf.ContainsKey("BackRotor")) {
        Echo("Config key 'BackRotor' that names the back rotor is missing!");
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
    return conf;
}


void Main(string argument)
{
    
    var conf = getCheckConf();
    if(conf == null) return;

    
    Runtime.UpdateFrequency = conf["Auto"] == "true" ? UpdateFrequency.Update100 : 0;

    // https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyMotorAdvancedStator
    // https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyExtendedPistonBase
    
    var frontRotor = GridTerminalSystem.GetBlockWithName(conf["FrontRotor"]) as IMyMotorAdvancedStator;
    var backRotor = GridTerminalSystem.GetBlockWithName(conf["BackRotor"]) as IMyMotorAdvancedStator;
    var piston = GridTerminalSystem.GetBlockWithName(conf["ExtenderPiston"]) as IMyExtendedPistonBase;
    
    // Determine state
    // State is determined by matrix on right, action determined by which row matches, or error if none.
    /*
                                        | FA    FL    FE      BA     BL    PR   RET   PE   EXT  |
      A01 front attach                  |             X       X      X                X         |
      A02 front lock                    | X           ?       X      X                X         |
      A03 back unlock                   | X     X     ?       X      X                X         |
      A04 back detach                   | X     X     ?       X                       X         |
      A05 piston retract                | X     X     ?                               X         |
      A06 piston retracting             | X     X     ?                         X               |
      A07 back attach                   | X     X     ?                    X                    |
      A08 back lock                     | X     X     ?       X            X                    |
      A09 front unlock                  | X     X     X       X      X     X                    |
      A10 front detach                  | X           X       X      X     X                    |
      A11 piston extend                 |             ?       X      X                          |
      A12 piston extending              |                     X      X                     X    |
                                                            
                                                            
      A90 Alert, missing front          |                     X      X                X         |
      
    */
    
    
    // Safety checks:
    //  Before unlocking back, make sure locked in front, else alert!
    // If piston should extend but cant, alert!
    
    bool frontAttached = frontRotor.IsAttached;
    bool frontLocked = frontRotor.RotorLock;
    bool frontCanLock = frontRotor.PendingAttachment;
    bool backAttached = backRotor.IsAttached;
    bool backLocked = backRotor.RotorLock;
    bool backCanLock = backRotor.PendingAttachment;
    
    bool retracting = piston.Velocity < 0;
    bool extending = piston.Velocity > 0;
    bool stopped = piston.Velocity == 0;
    
    bool atMax = piston.CurrentPosition == piston.MaxLimit;
    bool atMin = piston.CurrentPosition == piston.MinLimit;
    
    if(backAttached && backLocked && frontAttached && frontLocked && atMax) {
        Echo("Pulling forward");
        // Make sure front and back are part of same thing! https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyTerminalBlock.IsSameConstructAs
        backRotor.Detach();
        backRotor.RotorLock = false;
        piston.Velocity = -float.Parse(conf["AdvanceVelocityPull"]);
        return;        
    }
    
    if(frontAttached && frontLocked && atMin && !backAttached && !backLocked)
    {
        Echo("Attaching back");
        piston.Velocity = 0;
        backRotor.Attach();
        return;
    }

    if(frontAttached && frontLocked && atMin && backAttached && !backLocked)
    {
        Echo("Locking back");
        backRotor.RotorLock = true;
        return;
    }

    if(frontAttached && frontLocked && atMin && backAttached && backLocked)
    {
        Echo("Pushing forward");
        frontRotor.Detach();
        frontRotor.RotorLock = false;
        piston.Velocity = float.Parse(conf["AdvanceVelocityPush"]);        
        return;
    }
    if(!frontAttached && !frontLocked && atMax && backAttached && backLocked)
    {
        Echo("Attaching front");
        frontRotor.Attach();
        piston.Velocity = 0;
        return;
    }
    if(frontAttached && !frontLocked && atMax && backAttached && backLocked)
    {
        Echo("Locking front");
        frontRotor.RotorLock = true;
        return;
    }

    
    Echo("Didn't do nuthin!");
    
    Echo($"frontAttached: {frontAttached}");
    Echo($"frontLocked: {frontLocked}");
    Echo($"frontCanLock: {frontCanLock}");
    Echo($"backAttached: {backAttached}");
    Echo($"backLocked: {backLocked}");
    Echo($"backCanLock: {backCanLock}");
    Echo($"retracting: {retracting}");
    Echo($"extending: {extending}");
    Echo($"stopped: {stopped}");
    Echo($"atMax: {atMax}");
    Echo($"atMin: {atMin}");
    

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
