
void Main(string argument)
{
    var args = argument.Split(',');

    var a = GridTerminalSystem.GetBlockWithName(args[0]);
    var b = GridTerminalSystem.GetBlockWithName(args[1]);
    var c = GridTerminalSystem.GetBlockWithName(args[2]);
    var d = GridTerminalSystem.GetBlockWithName(args[3]);
    
    var distanceAB = a.GetPosition() - b.GetPosition();
    var distanceCD = c.GetPosition() - d.GetPosition();
    
    Echo($"a-b: {distanceAB}\nLen:{distanceAB.Length()}\n");
    Echo($"c-d: {distanceCD}\nLen:{distanceCD.Length()}\n");

    Echo($"(c-d)-(a-b): {distanceCD.Length()-distanceAB.Length()}\n");
}
