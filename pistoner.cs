
const string MULTIPLIERS = ".kMGTPEZY";

bool filterThis(IMyTerminalBlock block) {
  return block.CubeGrid == Me.CubeGrid || true;
}

float getExtraFieldFloat(IMyTerminalBlock block, string regexString) {
  System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(regexString, System.Text.RegularExpressions.RegexOptions.Singleline);
  float result = 0.0f;
  double parsedDouble;
  System.Text.RegularExpressions.Match match = regex.Match(block.DetailedInfo);
  if (match.Success) {
    if (Double.TryParse(match.Groups[1].Value, out parsedDouble)) {
      result = (float) parsedDouble;
    }
    if(MULTIPLIERS.IndexOf(match.Groups[2].Value) > -1) {
      result = result * (float) Math.Pow(1000.0, MULTIPLIERS.IndexOf(match.Groups[2].Value));
    }
  }
  return result;
}







public Program()
{
}

IMyPistonBase getPiston(string name) {
    IMyPistonBase currentPiston = null;
  List<IMyTerminalBlock> pistonBlockList = new List<IMyTerminalBlock>();
  GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(pistonBlockList, filterThis);
  if(pistonBlockList.Count == 0) {
    return null;
  }
  else {
    for(int i = 0; i < pistonBlockList.Count; i++) {
      if(pistonBlockList[i].CustomName == name) {
        currentPiston = (IMyPistonBase)pistonBlockList[i];
        break;
      }
    }
  }
  return currentPiston;
}

List<IMyPistonBase> getPistonsStartingWith(string prefix)
{
  List<IMyTerminalBlock> pistonBlockList = new List<IMyTerminalBlock>();
  List<IMyPistonBase> filtered = new List<IMyPistonBase>();
  GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(pistonBlockList, filterThis);
  for(int i = 0; i < pistonBlockList.Count; i++) {
    if(pistonBlockList[i].CustomName.StartsWith(prefix)) {
      filtered.Add((IMyPistonBase)pistonBlockList[i]);
    }
  }
  return filtered;
}

float getPistonPos(IMyPistonBase piston) {
    //return getExtraFieldFloat(piston, "Current position: (\\d+\\.?\\d*)");
    return piston.CurrentPosition;
}

bool fullyExtended(IMyPistonBase piston)
{
  float currentPos = getPistonPos(piston);
  float max = piston.MaxLimit;
  
  return currentPos >= max;
}

bool fullyRetracted(IMyPistonBase piston)
{
  float currentPos = getPistonPos(piston);
  float min = piston.MinLimit;
  
  return currentPos <= min;
}

bool atLimit(IMyPistonBase piston, bool extending) {
      float pos = getPistonPos(piston);
      //Echo($"{piston.CustomName} at {pos} of [{piston.MinLimit}, {piston.MaxLimit}]");
      if(extending && pos >= piston.MaxLimit) {
          return true;
      }
      if(!extending && pos <= piston.MinLimit)
      {
          return true;
      }
      return false;
}

void stopLimitReachedPistons(List<IMyPistonBase> pistons)
{
  foreach(IMyPistonBase piston in pistons) {
      if(piston.Velocity == 0) continue;
      bool extending = piston.Velocity > 0;
      if(atLimit(piston, extending)) {
          piston.Velocity = 0;
      }
  }
}



void Main(string argument, UpdateType updateType)
{
  
  Runtime.UpdateFrequency = UpdateFrequency.Update100;
  
  Dictionary<string, string> conf = new Dictionary<string, string>();
  string[] stuff = (Me.CustomData + "\n" + argument).Split('\n');
  foreach(string str in stuff) {
      string[] ayy = str.Split('=');
      if(ayy.Count() < 2) continue;
      conf[ayy[0].Trim()] = ayy[1].Trim();
  }
  if(conf.GetValue("prefix", null) == null)
  {
      Echo("Need conf(key=value per line) in customdata");
      Echo("Need 'prefix=<name-prefix>' to figure out pistons");
      return;
  }
  if(conf.GetValue("speed", null) == null)
  {
      Echo("Need conf(key=value per line) in customdata");
      Echo("Need 'speed=<speed>' for.. speed conf.");
      return;
  }
  if(conf.GetValue("mode", null) == null)
  {
      Echo("Need conf(key=value per line) in customdata");
      Echo("Need 'mode=retract' or 'mode=extend'");
      return;
  }
  if(conf.GetValue("swap", null) == null)
  {
      Echo("Need conf(key=value per line) in customdata");
      Echo("Need 'swap=true' or 'swap=false'");
      Echo("If true will swap mode when reached end.");
      return;
  }

  Echo($"Conf is {conf.ToDebugString()}");
  
  string namePrefix = conf["prefix"];
  float velocity = float.Parse(conf["speed"]);
  bool extend = conf["mode"] != "retract";
  bool swapAtEnd = conf["swap"] == "true";
  
  conf["mode"] = extend ? "extend" : "retract";
  Me.CustomData = conf.ToDebugString();
  
  Echo($"Will extend({extend}) {namePrefix} at speed {velocity}");
  
  List<IMyPistonBase> pistons = getPistonsStartingWith(namePrefix);
  stopLimitReachedPistons(pistons);
  pistons.Sort(delegate(IMyPistonBase x, IMyPistonBase y) {
      return x.CustomName.CompareTo(y.CustomName); 
  });
  
  string all="";
  
  Echo($"{pistons.Count} {namePrefix}'s to work!");
  
  foreach(IMyPistonBase piston in pistons)
  {
      float limit = extend ? piston.MaxLimit : piston.MinLimit;
      all += piston.CustomName;
      if(atLimit(piston, extend)) {
          Echo($"Piston {piston.CustomName} at its limit");
          continue;
      }
      all += " Starting this boi ";
      piston.Velocity = velocity * (extend ? 1f : -1f);
      return;
  }
  all = $"{pistons.Count}";
  Echo($"Done? {all}");
  
  if(swapAtEnd) {
      conf["mode"] = extend ? "retract" : "extend";
      Me.CustomData = conf.ToDebugString();
  }
  
  
  Runtime.UpdateFrequency = 0;
  
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
