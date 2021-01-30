#region Prelude
using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using VRageMath;
using VRage.Game;
using VRage.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame.Utilities;

// Change this namespace for each script you create.
namespace SpaceEngineers.UWBlockPrograms.BatteryMonitor {
    public sealed class Program : MyGridProgram {
    // Your code goes between the next #endregion and #region
#endregion

private MyIni storageIni = new MyIni();
private MyIni configIni = new MyIni();
private IMyTextSurface textOutputSurface;
private IMyShipController controller;

private string permanentlySavedInitString = null;
private StringBuilder textOutputStringBuilder = new StringBuilder();

private string state;
private int currentWaypointIndex;


private struct OnArriveAction {
    public string Action { get; set; }
    public string ComputerName { get; set; }
    public string Argument { get; set; }

    public static List<OnArriveAction> FromString(string s) {
        return s.Split('+')
            .Select(a => {
                if (a == "wait") {
                    return (OnArriveAction?) new OnArriveAction {
                        Action = "wait",
                    };
                }
                if (a.StartsWith("notify")) {
                    var b = a.Split('/');
                    return new OnArriveAction {
                        Action = "notify",
                        ComputerName = b[1],
                        Argument = b[2],
                    };
                }
                return null;
            })
            .Where(a => a.HasValue)
            .Select(a => a.Value)
            .ToList();
    }
}

private struct Waypoint {
    public string Name { get; set; }
    public Vector3D WorldPos { get; set; }
    public double Radius { get; set; }
    public double ApproachSpeed { get; set; }
    public List<OnArriveAction> OnArriveActions { get; set; }

    public static Waypoint FromConfigLine(string name, string configLine) {

        var a = configLine.Split(',').ToList();
        return new Waypoint() {
            Name = name,
            WorldPos = new Vector3D(DoubleFromString(a[0], 0), DoubleFromString(a[1], 0), DoubleFromString(a[2], 0)),
            Radius = DoubleFromString(a[3], 0),
            ApproachSpeed = DoubleFromString(a[4], 0),
            OnArriveActions = OnArriveAction.FromString(a[5]),
        };
    }
    private static double DoubleFromString(string s, double defaultValue) {
        double d;
        if (double.TryParse(s.Trim(), out d)) {
            return d;
        } else {
            return defaultValue;
        }
    }
}

private List<Waypoint> waypoints;

Dictionary<Base6Directions.Direction, List<IMyThrust>> thrusters = new Dictionary<Base6Directions.Direction, List<IMyThrust>>();

public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    
    var storageParsedOk = storageIni.TryParse(Storage);
    state = storageIni.Get("state", "state").ToString("init");

    var configParsedOk = configIni.TryParse(Me.CustomData);

    ScreenWriteln($"storage {storageParsedOk} config {configParsedOk}");

    var textOutputBlock = GridTerminalSystem.GetBlockWithName(configIni.Get("blocks", "textOutput").ToString());

    textOutputSurface = textOutputBlock as IMyTextSurface ?? (textOutputBlock as IMyTextSurfaceProvider)?.GetSurface(0);
    if (textOutputSurface != null) {
        textOutputSurface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
        textOutputSurface.FontSize = 1;
        textOutputSurface.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
    }

    var controllerBlock = GridTerminalSystem.GetBlockWithName(configIni.Get("blocks", "controller").ToString());
    controller = controllerBlock as IMyShipController;


    var keys = new List<MyIniKey>();
    configIni.GetKeys("waypoints", keys);
    waypoints = keys.Select(key => Waypoint.FromConfigLine(key.Name, configIni.Get(key).ToString())).ToList();

    var savedWaypointName = storageIni.Get("state", "currentWaypointName").ToString();
    currentWaypointIndex = waypoints.FindIndex(w => w.Name == savedWaypointName);

    if (currentWaypointIndex == -1) {
        currentWaypointIndex = 0;
        state = "init";
    }

    var allThrustersOnShip = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType<IMyThrust>(allThrustersOnShip);
    thrusters[Base6Directions.Direction.Right] = allThrustersOnShip.Where(t => t.GridThrustDirection == new Vector3I(1, 0, 0)).ToList();
    thrusters[Base6Directions.Direction.Left] = allThrustersOnShip.Where(t => t.GridThrustDirection == new Vector3I(-1, 0, 0)).ToList();
    thrusters[Base6Directions.Direction.Up] = allThrustersOnShip.Where(t => t.GridThrustDirection == new Vector3I(0, 1, 0)).ToList();
    thrusters[Base6Directions.Direction.Down] = allThrustersOnShip.Where(t => t.GridThrustDirection == new Vector3I(0, -1, 0)).ToList();
    thrusters[Base6Directions.Direction.Backward] = allThrustersOnShip.Where(t => t.GridThrustDirection == new Vector3I(0, 0, 1)).ToList();
    thrusters[Base6Directions.Direction.Forward] = allThrustersOnShip.Where(t => t.GridThrustDirection == new Vector3I(0, 0, -1)).ToList();
}

public void Save() {
    storageIni.Set("state", "state", state);

    Storage = storageIni.ToString();
}

long frameCounter = 0;

public void Main(string args) {
    StartFrame();
    try {
        if (args == "createWaypoint") {
            var newWp = new Waypoint {
                Name = $"autoCreated{waypoints.Count+1}",
                WorldPos = Me.GetPosition(),
                ApproachSpeed = 2,
                Radius = 15
            };
            waypoints.Add(newWp);
            configIni.Set("waypoints", newWp.Name, $"{newWp.WorldPos.X:0.00},{newWp.WorldPos.Y:0.00},{newWp.WorldPos.Z:0.00},{newWp.Radius},{newWp.ApproachSpeed},");
            Me.CustomData = configIni.ToString();
        } else {
            Frame(args);
        }
    } catch (Exception e) {
        ScreenWriteln($"Error: {e}");
    }

    EndFrame();
}

private void Frame(string args) {

    var currentPos = Me.GetPosition();

    if (state == "init") {
        if (waypoints.Count > 0) {
            state = "followingWaypoints";
            currentWaypointIndex = 0;
        } else {
            ScreenWriteln("No waypoints :(");
        }
    }

    ScreenWriteln($"State = {state} ({frameCounter++})");

    if (state == "followingWaypoints") {
        var wp = FigureOutWhereWeHeaded(currentPos, args);

        var worldToShipCoords = Matrix.Transpose(Me.WorldMatrix);

        //ScreenWriteln($"At {currentPos.X:0.00} {currentPos.Y:0.00} {currentPos.Z:0.00}");
        ScreenWriteln($"Heading to {wp.Name} {(wp.OnArriveActions.Count > 0 ? wp.OnArriveActions[0].Action : "none")}");
        //ScreenWriteln($"Heading to {wp.WorldPos.X:0.00} {wp.WorldPos.Y:0.00} {wp.WorldPos.Z:0.00}");

        var delta = Vector3D.TransformNormal(wp.WorldPos - currentPos, worldToShipCoords);
        var currentVelocity = Vector3D.TransformNormal(controller.GetShipVelocities().LinearVelocity, worldToShipCoords);

        //ScreenWriteln($"delta {delta.X:0.00} {delta.Y:0.00} {delta.Z:0.00}");
        //ScreenWriteln($"currentVelocity {currentVelocity.X:0.00} {currentVelocity.Y:0.00} {currentVelocity.Z:0.00}");

        var approachVelocity = Vector3D.Normalize(delta) * wp.ApproachSpeed;

        var velocityDelta = approachVelocity - currentVelocity;

        //ScreenWriteln($"approachVelocity {approachVelocity.X:0.00} {approachVelocity.Y:0.00} {approachVelocity.Z:0.00}");
        //ScreenWriteln($"velocityDelta {velocityDelta.X:0.00} {velocityDelta.Y:0.00} {velocityDelta.Z:0.00}");
        
        if (Me.ShowOnHUD) {
            ScreenWriteln("ShowOnHUD is set, autopilot not thrusting");
            ResetThrusters();
        } else {
            FuckWithThrusters(velocityDelta.X, thrusters[Base6Directions.Direction.Left], thrusters[Base6Directions.Direction.Right], wp.ApproachSpeed);
            FuckWithThrusters(velocityDelta.Y, thrusters[Base6Directions.Direction.Down], thrusters[Base6Directions.Direction.Up], wp.ApproachSpeed);
            FuckWithThrusters(velocityDelta.Z, thrusters[Base6Directions.Direction.Forward], thrusters[Base6Directions.Direction.Backward], wp.ApproachSpeed);
        }
    }
}

private void ResetThrusters() {
    foreach (var p in thrusters) {
        foreach (var t in p.Value) {
            t.ThrustOverridePercentage = 0;
        }
    }
}

private void FuckWithThrusters(double velocityDeltaComponent, List<IMyThrust> positiveThrusters, List<IMyThrust> negativeThrusters, double approachSpeed) {
    var thrustSetting = (float) (Math.Abs(velocityDeltaComponent) / approachSpeed);
    foreach (var t in positiveThrusters) {
        t.ThrustOverridePercentage = velocityDeltaComponent > 0 ? thrustSetting : 0;
    }
    foreach (var t in negativeThrusters) {
        t.ThrustOverridePercentage = velocityDeltaComponent < 0 ? thrustSetting : 0;
    }
}

private string fuck = "";

private Waypoint FigureOutWhereWeHeaded(Vector3D currentPos, string args) {

    var idx = args != null && args != "" ? waypoints.FindIndex(w => w.Name == args) : -1;
    if (idx != -1) {
        currentWaypointIndex = idx;
    } else {
        var wp = waypoints[currentWaypointIndex];

        if ((wp.WorldPos - currentPos).Length() < wp.Radius) {
            currentWaypointIndex += 1;


            foreach (var action in wp.OnArriveActions) {
                if (action.Action == "wait") {
                    // set state to waiting
                } else if (action.Action == "notify") {
                    var block = (GridTerminalSystem.GetBlockWithName(action.ComputerName) as IMyProgrammableBlock);
                    fuck = block == null ? "fuck" : "fuck???";
                    fuck += action.ComputerName + "(" + action.Argument + ")";
                    block?.TryRun(action.Argument);
                }
            }

        }

        if (currentWaypointIndex >= waypoints.Count) {
            currentWaypointIndex = 0;
        }
    }

    return waypoints[currentWaypointIndex];
}

private void StartFrame() {
    if (permanentlySavedInitString == null) {
        permanentlySavedInitString = textOutputStringBuilder.ToString();
    }
    textOutputStringBuilder.Clear();
}
private void EndFrame() {
    if (textOutputSurface != null) {
        textOutputSurface.WriteText(permanentlySavedInitString);
        textOutputSurface.WriteText(textOutputStringBuilder.ToString(), true);
        textOutputSurface.WriteText(fuck, true);
    } else {
        Echo(permanentlySavedInitString);
        Echo("textOutput not found :(\n" + textOutputStringBuilder.ToString());
    }
}

private void ScreenWriteln(string s) {
    textOutputStringBuilder.Append(s + "\n");
}

#region PreludeFooter
    }
}
#endregion