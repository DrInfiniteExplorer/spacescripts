
void Main(string argument)
{
    
    
    //Runtime.UpdateFrequency = conf["Auto"] == "true" ? UpdateFrequency.Update100 : 0;

    //https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyProjector
    var projector = GridTerminalSystem.GetBlockWithName("Projector") as IMyProjector;
    
    Echo($"{projector.GetType().Name}");
    Echo($"{projector.LoadBlueprint()}");
    

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
