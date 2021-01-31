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
private IMyShipController controller;
private string state;
private List<Waypoint> waypoints;
private ThrustController thrustController;
private FrameText frameText;
private WaypointFollower waypointFollower;
long frameCounter = 0;

public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    var storageParsedOk = storageIni.TryParse(Storage);
    state = storageIni.Get("state", "state").ToString("init");

    var configParsedOk = configIni.TryParse(Me.CustomData);
    var textOutputBlockName = configIni.Get("blocks", "textOutput").ToString().Split(':');
    var textOutputBlock = GridTerminalSystem.GetBlockWithName(textOutputBlockName[0]);

    frameText = new FrameText(textOutputBlock as IMyTextSurface ?? (textOutputBlock as IMyTextSurfaceProvider)
        ?.GetSurface(textOutputBlockName.Length > 1 ? int.Parse(textOutputBlockName[1]) : 0));
    frameText.PreludeWriteln($"storage {storageParsedOk} config {configParsedOk}");

    var controllerBlock = GridTerminalSystem.GetBlockWithName(configIni.Get("blocks", "controller").ToString());
    controller = controllerBlock as IMyShipController;

    var keys = new List<MyIniKey>();
    configIni.GetKeys("waypoints", keys);
    waypoints = keys.Select(key => Waypoint.FromString(key.Name, configIni.Get(key).ToString())).ToList();

    var allThrustersOnShip = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType<IMyThrust>(allThrustersOnShip);
    thrustController = new ThrustController(allThrustersOnShip);
}
public void Save() {
    storageIni.Set("state", "state", state);
    Storage = storageIni.ToString();
}
public void Main(string args) {
    frameText.StartFrame();
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
        frameText.Writeln($"Error: {e}");
    }
    frameText.EndFrame();
}
private void Frame(string args) {
    var currentWorldPos = Me.GetPosition();
    if (state == "init") {
        if (waypoints.Count > 0) {
            state = "followingWaypoints";
            waypointFollower = new WaypointFollower(waypoints, controller, thrustController);
        } else {
            frameText.Writeln("No waypoints :(");
        }
    }
    frameText.Writeln($"State = {state} ({frameCounter++})");
    if (state == "waiting") {
        if (args == "continue") {
            state = "followingWaypoints";
        }
    }
    if (state == "follwingWaypoints") {
        if (args == "wait") {
            thrustController.ResetThrusters();
            state = "waiting";
        }
    }
    if (state == "followingWaypoints") {
        frameText.Writeln($"Heading to {waypointFollower.Current.Name}");
        waypointFollower.Go(Me.GetPosition(), Me.WorldMatrix, (s, wp, actions) => {
            foreach (var action in actions) {
                if (action.Action == "wait") {
                    state = "waiting";
                    thrustController.ResetThrusters();
                } else if (action.Action == "notify") {
                    var block = (GridTerminalSystem.GetBlockWithName(action.ComputerName) as IMyProgrammableBlock);
                    block?.TryRun(action.Argument);
                }
            }
            if (s == "finished") {
                waypointFollower = new WaypointFollower(waypoints, controller, thrustController);
            }
        });
    }
}
private class WaypointFollower {
    List<Waypoint> waypoints;
    private IMyShipController controller;
    ThrustController thrustController;
    int currentWaypointIndex;
    public Waypoint Current { get { return waypoints[currentWaypointIndex]; } }
    public WaypointFollower(List<Waypoint> waypointsToFollow, IMyShipController shipController, ThrustController oof) {
        waypoints = waypointsToFollow;
        controller = shipController;
        thrustController = oof;
        currentWaypointIndex = 0;
    }
    public void Go(Vector3D currentWorldPos, MatrixD worldMatrix, Action<string, Waypoint, List<OnArriveAction>> f) {
        if ((Current.WorldPos - currentWorldPos).Length() < Current.Radius) {
            if (currentWaypointIndex + 1 >= waypoints.Count) {
                f("finished", Current, Current.OnArriveActions);
                return;
            }
            f("passedWaypoint", Current, Current.OnArriveActions);
            currentWaypointIndex += 1;
        }
        var worldToShipCoords = Matrix.Transpose(worldMatrix);
        var delta = Vector3D.TransformNormal(Current.WorldPos - currentWorldPos, worldToShipCoords);
        var currentVelocity = Vector3D.TransformNormal(controller.GetShipVelocities().LinearVelocity, worldToShipCoords);
        thrustController.ApproachVelocity(currentVelocity, Vector3D.Normalize(delta) * Current.ApproachSpeed);
    }
}
private class ThrustController {
    public Dictionary<Base6Directions.Direction, List<IMyThrust>> ThrustersByDirection { get; set; }
    public ThrustController(List<IMyThrust> allowedThrusters) {
        ThrustersByDirection = new Dictionary<Base6Directions.Direction, List<IMyThrust>>();
        ThrustersByDirection[Base6Directions.Direction.Right] = allowedThrusters.Where(t => t.GridThrustDirection == new Vector3I(1, 0, 0)).ToList();
        ThrustersByDirection[Base6Directions.Direction.Left] = allowedThrusters.Where(t => t.GridThrustDirection == new Vector3I(-1, 0, 0)).ToList();
        ThrustersByDirection[Base6Directions.Direction.Up] = allowedThrusters.Where(t => t.GridThrustDirection == new Vector3I(0, 1, 0)).ToList();
        ThrustersByDirection[Base6Directions.Direction.Down] = allowedThrusters.Where(t => t.GridThrustDirection == new Vector3I(0, -1, 0)).ToList();
        ThrustersByDirection[Base6Directions.Direction.Backward] = allowedThrusters.Where(t => t.GridThrustDirection == new Vector3I(0, 0, 1)).ToList();
        ThrustersByDirection[Base6Directions.Direction.Forward] = allowedThrusters.Where(t => t.GridThrustDirection == new Vector3I(0, 0, -1)).ToList();
    }
    public void ResetThrusters() {
        foreach (var p in ThrustersByDirection) {
            foreach (var t in p.Value) {
                t.ThrustOverridePercentage = 0;
            }
        }
    }
    public void ApproachVelocity(Vector3D currentVelocity, Vector3D desiredVelocity) {
        var velocityDelta = desiredVelocity - currentVelocity;
        ControlThrusters(velocityDelta.X, ThrustersByDirection[Base6Directions.Direction.Left], ThrustersByDirection[Base6Directions.Direction.Right], desiredVelocity.Length());
        ControlThrusters(velocityDelta.Y, ThrustersByDirection[Base6Directions.Direction.Down], ThrustersByDirection[Base6Directions.Direction.Up], desiredVelocity.Length());
        ControlThrusters(velocityDelta.Z, ThrustersByDirection[Base6Directions.Direction.Forward], ThrustersByDirection[Base6Directions.Direction.Backward], desiredVelocity.Length());
    }
    private void ControlThrusters(double velocityDeltaComponent, List<IMyThrust> positiveThrusters, List<IMyThrust> negativeThrusters, double approachSpeed) {
        var thrustSetting = (float) (Math.Abs(velocityDeltaComponent) / approachSpeed);
        foreach (var t in positiveThrusters) {
            t.ThrustOverridePercentage = velocityDeltaComponent > 0 ? thrustSetting : 0;
        }
        foreach (var t in negativeThrusters) {
            t.ThrustOverridePercentage = velocityDeltaComponent < 0 ? thrustSetting : 0;
        }
    }
}
private class FrameText {
    private StringBuilder preludeStringBuilder;
    private StringBuilder frameStringBuilder;
    private IMyTextSurface textOutputSurface;
    public FrameText(IMyTextSurface surface) {
        textOutputSurface = surface;
        preludeStringBuilder = new StringBuilder();
        frameStringBuilder = new StringBuilder();
    }
    public void StartFrame() {
        frameStringBuilder.Clear();
    }
    public void EndFrame() {
        textOutputSurface?.WriteText(preludeStringBuilder.ToString());
        textOutputSurface?.WriteText(frameStringBuilder.ToString(), true);
    }
    public void Writeln(string s) {
        frameStringBuilder.Append(s + "\n");
    }
    public void PreludeWriteln(string s) {
        preludeStringBuilder.Append(s + "\n");
    }
}
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
    public static Waypoint FromString(string name, string configLine) {
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

#region PreludeFooter
    }
}
#endregion