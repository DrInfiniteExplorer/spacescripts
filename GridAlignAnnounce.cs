
IMyBroadcastListener queryListener;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Once;
    
    //IGC.SendBroadcastMessage("YoWhereAreYouMan", "I am lost");
    
    queryListener = IGC.RegisterBroadcastListener("YoWhereAreYouMan");
    queryListener.SetMessageCallback("ayy?");
    
    Me.GetSurface(1).ContentType = ContentType.TEXT_AND_IMAGE;
    Me.GetSurface(1).WriteText("Grid Announce\nComputer");
    Me.GetSurface(1).FontSize = 3.8f;
    Me.GetSurface(1).Alignment = TextAlignment.CENTER;    
}


void Main(string argument, UpdateType updateSource)
{
    StringBuilder displayString = new StringBuilder();
    Action<String> write = delegate(string s) {
        Echo(s);
        displayString.Append(s + "\n");
    };
    try {
        if((updateSource & UpdateType.IGC) != 0) {
            write(argument);
            write(updateSource.ToString());
            //IGC.SendBroadcastMessage(YoWeAreHereMan, toSay);
            while(queryListener.HasPendingMessage) {
                var msg = queryListener.AcceptMessage();                
                write(msg.Tag);
                write(msg.Source.ToString());
                write(msg.Data.ToString());
                if(msg.Tag == "YoWhereAreYouMan") {
                    var query = msg.Data as string;
                    if(query == "I am lost") {
                        var toSay = $"{Me.CubeGrid.CustomName}\n{Me.WorldMatrix.Forward}\n{Me.WorldMatrix.Up}\n{Me.WorldMatrix.Translation}\n";
                        IGC.SendUnicastMessage(msg.Source, "We here", toSay);
                        write(toSay);
                    }
                    if(query.StartsWith("I need this:")) {
                        var words = query.Split(':');
                        if(words.Count() < 2){
                            write("Not enough instructions from I need this");
                            return;
                        };
                        string who = words[1];
                        string verby = null;
                        if(words.Count() == 3) {
                            verby = words[2];
                        }
                        List<IMyTerminalBlock> blockList = new List<IMyTerminalBlock>();
                        GridTerminalSystem.GetBlocksOfType<IMyEntity>(blockList, x => x.CustomName == who);
                        
                        Func<IMyTerminalBlock, String> getty = x => x.WorldMatrix.Translation.ToString();
                        
                        if(verby == "Above") {
                            getty = x => Vector3.Transform(Base6Directions.GetVector(x.Orientation.Up)*-2.5f, x.WorldMatrix).ToString();
                        }
                        Echo(verby);
                        
                        var toSay = String.Join("\n", blockList.Select(getty));
                        IGC.SendUnicastMessage(msg.Source, "These fellas", toSay);
                        write(toSay);
                    }
                }
            }
            
        }
    }
    finally {
        
        //var displayBlock = GridTerminalSystem.GetBlockWithName("Display") as IMyTextPanel;
        var displayBlock = Me.GetSurface(0);
        if(displayBlock == null) {
            Echo("No item display :(");
        } else {
            displayBlock.ContentType = ContentType.TEXT_AND_IMAGE;
            displayBlock.Alignment = TextAlignment.CENTER;
            var pixelsAsFont1 = displayBlock.MeasureStringInPixels(displayString, "Debug", 1.0f);
            var pixelsOnSurface = displayBlock.SurfaceSize;
            var ayy = pixelsOnSurface / pixelsAsFont1;
            displayBlock.FontSize = Math.Min(ayy.X, ayy.Y);
            displayBlock.FontSize = 1.8f;
            displayBlock.WriteText(displayString, false);
        }
    }

}
