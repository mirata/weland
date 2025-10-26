using System;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cairo;
using Gdk;
using Gtk;
using weland;
using static Weland.Side;

namespace Weland;

public class UDBExporter
{
    const double detachedOffset = 4096.0;
    static readonly List<int> transferModeMapping = [0, 4, 5, 6, 9, 15, 16, 17, 18, 19, 20];

    private static string[] transferModes = [
        "Normal",
        "Pulsate",
        "Wobble",
        "FastWobble",
        "Landscape",
        "HorizontalSlide",
        "FastHorizontalSlide",
        "VerticalSlide",
        "FastVerticalSlide",
        "Wander",
        "FastWander",
    ];

    string[] itemNames = {
        "Knife",
        "Magnum Pistol",
        "Magnum Magazine",
        "Plasma Pistol",
        "Plasma Energy Cell",
        "Assault Rifle",
        "AR Magazine",
        "AR Grenade Magazine",
        "Missile Launcher",
        "Missile 2-Pack",
        "Invisibility Powerup",
        "Invincibility Powerup",
        "Infravision Powerup",
        "Alien Weapon",
        "Alien Weapon Ammo",
        "Flamethrower",
        "Flamethrower Canister",
        "Extravision Powerup",
        "Oxygen Powerup",
        "Energy Powerup x1",
        "Energy Powerup x2",
        "Energy Powerup x3",
        "Shotgun",
        "Shotgun Cartridges",
        "S'pht Door Key",
        "Uplink Chip",
        "Light Blue Ball",
        "The Ball",
        "Violet Ball",
        "Yellow Ball",
        "Brown Ball",
        "Orange Ball",
        "Blue Ball",
        "Green Ball",
        "Submachine Gun",
        "Submachine Gun Clip"
        };

    string[] monsterNames = {
            "Marine",
            "Tick Energy",
            "Tick Oxygen",
            "Tick Kamakazi",
            "Compiler Minor",
            "Compiler Major",
            "Compiler Minor Invisible",
            "Compiler Major Invisible",
            "Fighter Minor",
            "Fighter Major",
            "Fighter Minor Projectile",
            "Fighter Major Projectile",
            "Civilian Crew",
            "Civilian Science",
            "Civilian Security",
            "Civilian Assimilated",
            "Hummer Minor",
            "Hummer Major",
            "Hummer Big Minor",
            "Hummer Big Major",
            "Hummer Possessed",
            "Cyborg Minor",
            "Cyborg Major",
            "Cyborg Flame Minor",
            "Cyborg Flame Major",
            "Enforcer Minor",
            "Enforcer Major",
            "Hunter Minor",
            "Hunter Major",
            "Trooper Minor",
            "Trooper Major",
            "Mother of all Cyborgs",
            "Mother of all Hunters",
            "Sewage Yeti",
            "Water Yeti",
            "Lava Yeti",
            "Defender Minor",
            "Defender Major",
            "Juggernaut Minor",
            "Juggernaut Major",
            "Tiny Figher",
            "Tiny Bob",
            "Tiny Yeti",
            "Civilian Fusion Crew",
            "Civilian Fusion Science",
            "Civilian Fusion Security",
            "Civilian Fusion Assimilated"
        };


    string[] sceneryNames = [
        "(L) Light Dirt",
        "(L) Dark Dirt",
        "(L) Bones",
        "(L) Bone",
        "(L) Ribs",
        "(L) Skull",
        "(L) Hanging Light #1",
        "(L) Hanging Light #2",
        "(L) Large Cylinder",
        "(L) Small Cylinder",
        "(L) Block #1",
        "(L) Block #2",
        "(L) Block #3",
        "(W) Pistol Clip",
        "(W) Short Light",
        "(W) Long Light",
        "(W) Siren",
        "(W) Rocks",
        "(W) Blood Drops",
        "(W) Filtration Device",
        "(W) Gun",
        "(W) Bob Remains",
        "(W) Puddles",
        "(W) Big Puddles",
        "(W) Security Monitor",
        "(W) Alien Supply Can",
        "(W) Machine",
        "(W) Fighter's Staff",
        "(S) Stubby Green Light",
        "(S) Long Green Light",
        "(S) Junk",
        "(S) Big Antenna #1",
        "(S) Big Antenna #2",
        "(S) Alien Supply Can",
        "(S) Bones",
        "(S) Big Bones",
        "(S) Pfhor Pieces",
        "(S) Bob Pieces",
        "(S) Bob Blood",
        "(P) Green Light",
        "(P) Small Alien Light",
        "(P) Alien Ceiling Rod Light",
        "(P) Bulbous Yellow Alien Object",
        "(P) Square Grey Organic Object",
        "(P) Pfhor Skeleton",
        "(P) Pfhor Mask",
        "(P) Green Stuff",
        "(P) Hunter Shield",
        "(P) Bones",
        "(P) Alien Sludge",
        "(J) Short Ceiling Light",
        "(J) Long Light",
        "(J) Weird Rod",
        "(J) Pfhor Ship",
        "(J) Sun",
        "(J) Large Glass Container",
        "(J) Nub #1",
        "(J) Nub #2",
        "(J) Lh'owon",
        "(J) Floor Whip Antenna",
        "(J) Ceiling Whip Antenna",
    ];

    Level level;
    int levelIndex;

    public UDBExporter(Level level, int levelIndex)
    {
        this.level = level;
        this.levelIndex = levelIndex;
    }

    private double GetLayerXOffset(int layer)
    {
        if (layer == 2 || layer == 3)
        {
            return -detachedOffset;
        }
        return 0;
    }

    private double GetLayerYOffset(int layer)
    {
        if (layer == 3 || layer == 4)
        {
            return detachedOffset;
        }
        return 0;
    }

    private List<int> GetLayersForPoly(int i)
    {
        return Level.Attributes.PolygonLayers.ContainsKey((short)i) ? Level.Attributes.PolygonLayers[(short)i] : [1];
    }

    public void Export(string path)
    {
        var scriptPath = System.IO.Path.ChangeExtension(path, ".acs");

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var map = new UdbLevel();

        HashSet<short> repairPlatforms = [];

        HashSet<short> initialisedLights = [];

        Dictionary<short, HashSet<int>> adjacentPlatforms = [];
        short? startingPolygonIndex = null;

        for (short i = 0; i < level.Lights.Count; i++)
        {
            var light = level.Lights[i];
            if (!IsUnchangingState(light))
            {
                initialisedLights.Add(i);
            }

            map.Lights.Add(GetLight(light, i));
        }


        HashSet<short> activatedLights = [];

        for (short index = 0; index < level.Platforms.Count; index++)
        {
            var platform = level.Platforms[index];
            if (platform.ActivatesLight)
            {
                var polygon = level.Polygons[platform.PolygonIndex];
                var light = level.Lights[polygon.FloorLight];
                if (!LightStatesAreEqual(light.PrimaryActive, light.SecondaryActive)
                   || !LightStatesAreEqual(light.PrimaryActive, light.PrimaryInactive)
                   || !LightStatesAreEqual(light.PrimaryActive, light.SecondaryInactive)
                   || !LightStatesAreEqual(light.PrimaryActive, light.BecomingActive)
                   || !LightStatesAreEqual(light.PrimaryActive, light.BecomingInactive))
                {
                    activatedLights.Add(polygon.FloorLight);
                }
            }
        }

        for (short index = 0; index < level.Sides.Count; index++)
        {
            var side = level.Sides[index];
            if (side.IsControlPanel)
            {
                if (side.IsLightSwitch())
                {
                    activatedLights.Add(side.ControlPanelPermutation);
                }
                else if (side.IsPlatformSwitch() && side.IsRepairSwitch())
                {
                    repairPlatforms.Add(side.ControlPanelPermutation);
                }
            }
        }

        var controlledLightIndexes = initialisedLights.Concat(activatedLights).ToHashSet();
        var scriptCalls = new HashSet<string>();
        var scripts = new HashSet<string>();
        var taggedPolygons = new HashSet<short>();

        for (short index = 0; index < level.Polygons.Count; index++)
        {
            var p = level.Polygons[index];
            switch (p.Type)
            {
                case PolygonType.LightOnTrigger:
                case PolygonType.LightOffTrigger:
                    taggedPolygons.Add(index);
                    controlledLightIndexes.Add(p.Permutation);
                    break;
                case PolygonType.PlatformOnTrigger:
                case PolygonType.PlatformOffTrigger:
                case PolygonType.Teleporter:
                    taggedPolygons.Add(index);
                    taggedPolygons.Add(p.Permutation);
                    break;
                case PolygonType.VisibleMonsterTrigger:
                case PolygonType.InvisibleMonsterTrigger:
                case PolygonType.DualMonsterTrigger:
                case PolygonType.ItemTrigger:
                case PolygonType.MustBeExplored:
                case PolygonType.AutomaticExit:
                case PolygonType.MinorOuch:
                case PolygonType.MajorOuch:
                case PolygonType.Glue:
                case PolygonType.GlueTrigger:
                case PolygonType.Superglue:
                    taggedPolygons.Add(index);
                    break;
            }
        }

        for (short index = 0; index < level.Polygons.Count; index++)
        {
            var p = level.Polygons[index];
            switch (p.Type)
            {
                case PolygonType.LightOnTrigger:
                case PolygonType.LightOffTrigger:
                case PolygonType.PlatformOnTrigger:
                case PolygonType.PlatformOffTrigger:
                case PolygonType.Teleporter:
                case PolygonType.ItemImpassable:
                case PolygonType.MonsterImpassable:
                case PolygonType.ZoneBorder:
                case PolygonType.Goal:
                case PolygonType.VisibleMonsterTrigger:
                case PolygonType.InvisibleMonsterTrigger:
                case PolygonType.DualMonsterTrigger:
                case PolygonType.ItemTrigger:
                case PolygonType.MustBeExplored:
                case PolygonType.AutomaticExit:
                case PolygonType.MinorOuch:
                case PolygonType.MajorOuch:
                case PolygonType.Glue:
                case PolygonType.GlueTrigger:
                case PolygonType.Superglue:
                    break;
            }
            var platform = level.Platforms.FirstOrDefault(e => e.PolygonIndex == index);
            var floorHeight = p.FloorHeight;
            var ceilingHeight = p.CeilingHeight;

            if (p.MediaIndex > -1 && p.MediaIndex < level.Medias.Count)
            {
                var media = level.Medias[p.MediaIndex];
                floorHeight = media.High;
            }

            if (platform != null)
            {
                var minFloor = platform.MinimumHeight;
                var maxFloor = platform.MaximumHeight;
                var minCeiling = platform.MinimumHeight;
                var maxCeiling = platform.MaximumHeight;

                if (platform.ComesFromFloor && platform.ComesFromCeiling)
                {
                    maxFloor = (short)(platform.MinimumHeight + ((platform.MaximumHeight - platform.MinimumHeight) / 2));
                    minCeiling = (short)(platform.MinimumHeight + ((platform.MaximumHeight - platform.MinimumHeight) / 2));
                }

                if (platform.ComesFromCeiling)
                {
                    if (platform.InitiallyExtended)
                    {
                        ceilingHeight = minCeiling;
                        //if (platform.MinimumHeight < p.CeilingHeight)
                        //{
                        //    ceilingHeight = Math.Max(p.FloorHeight, platform.MinimumHeight);
                        //}
                    }
                    else if (platform.MaximumHeight < p.CeilingHeight)
                    {
                        ceilingHeight = Math.Max(p.FloorHeight, platform.MinimumHeight);
                    }
                }

                if (platform.ComesFromFloor)
                {
                    if (platform.InitiallyExtended)
                    {
                        floorHeight = maxFloor;
                    }
                    //else if (platform.MaximumHeight > p.FloorHeight)
                    //{
                    //    floorHeight = platform.MaximumHeight;
                    //}
                }
                if (platform.ActivatesLight)
                {
                    activatedLights.Add(p.FloorLight);
                }
            }

            List<UdbLine> lines = [];

            for (var i = 0; i < p.VertexCount; ++i)
            {
                var lineIndex = p.LineIndexes[i];
                var pointStart = level.Endpoints[p.EndpointIndexes[i]];
                var pointEnd = level.Endpoints[p.EndpointIndexes[i == p.VertexCount - 1 ? 0 : i + 1]];
                var line = level.Lines[lineIndex];
                var side = level.Sides.FirstOrDefault(e => e.LineIndex == lineIndex && e.PolygonIndex == index);// p.SideIndexes[i] > -1 && p.SideIndexes[i] < level.Sides.Count ? level.Sides[p.SideIndexes[i]] : null;
                var adjacentPolyIndex = p.AdjacentPolygonIndexes[i];
                //fallback as sometimes polyindexes are invalid. get the line and associates owners
                if (adjacentPolyIndex > -1 && adjacentPolyIndex >= level.Polygons.Count)
                {
                    adjacentPolyIndex = line.ClockwisePolygonOwner == index ? line.CounterclockwisePolygonOwner : line.ClockwisePolygonOwner;
                }

                var adjacentPolygon = adjacentPolyIndex > -1 && adjacentPolyIndex < level.Polygons.Count ? level.Polygons[adjacentPolyIndex] : null;
                var adjacentPlatform = level.Platforms.FirstOrDefault(e => e.PolygonIndex == adjacentPolyIndex);
                if (adjacentPlatform != null)
                {
                    if (!adjacentPlatforms.ContainsKey(index))
                    {
                        adjacentPlatforms[index] = [];
                    }
                    adjacentPlatforms[index].Add(adjacentPolyIndex);
                }

                UdbTexture? upper = null;
                UdbTexture? middle = null;
                UdbTexture? lower = null;

                int? triggerLightIndex = null;
                int? triggerPlatformIndex = null;
                string? controlPanelClassValue = null;

                var dontPegTop = false;
                var dontPegBottom = platform != null && platform.ComesFromCeiling && adjacentPolygon == null;
                var blockSound = adjacentPolygon != null && ((p.Type != PolygonType.ZoneBorder && adjacentPolygon?.Type == PolygonType.ZoneBorder) || (p.Type == PolygonType.ZoneBorder && adjacentPolygon?.Type != PolygonType.ZoneBorder));

                if (side != null)
                {
                    if (adjacentPolygon != null)
                    {
                        var floorRoundingOffset = CalculateFloorRoundingOffset(p, adjacentPolygon);

                        //doom low textures are anchored to the bottom left of the side - ie if the lower height changes, the texture follows it
                        //marathon low textures are anchored to the bottom left - if the lower height changes, the texture grows
                        //however for marathon floor platforms, the offset is relative to the min and max height, and then anchors top left
                        if (side.Type == SideType.Split)
                        {
                            var upperPlatformOffset = 0;
                            var lowerPlatformOffset = 0;

                            if (adjacentPlatform != null)
                            {
                                var minFloor = adjacentPlatform.MinimumHeight;
                                var maxFloor = adjacentPlatform.MaximumHeight;
                                var minCeiling = adjacentPlatform.MinimumHeight;
                                var maxCeiling = adjacentPlatform.MaximumHeight;

                                if (adjacentPlatform.ComesFromFloor && adjacentPlatform.ComesFromCeiling)
                                {
                                    maxFloor = (short)(adjacentPlatform.MinimumHeight + ((adjacentPlatform.MaximumHeight - adjacentPlatform.MinimumHeight) / 2));
                                    minCeiling = (short)(adjacentPlatform.MinimumHeight + ((adjacentPlatform.MaximumHeight - adjacentPlatform.MinimumHeight) / 2));
                                }

                                upperPlatformOffset = adjacentPlatform.ComesFromCeiling ? maxCeiling - minCeiling : 0;
                                lowerPlatformOffset = adjacentPlatform.ComesFromFloor ? minFloor - p.FloorHeight : 0;
                            }

                            var fromFloor = adjacentPlatform?.ComesFromFloor ?? false;
                            var fromCeiling = adjacentPlatform?.ComesFromCeiling ?? false;

                            var yOffset = p.CeilingHeight - adjacentPolygon.CeilingHeight + upperPlatformOffset;
                            upper = GetUdbTexture(side.Primary, side.PrimaryLightsourceIndex, side.PrimaryTransferMode, ConvertUnit((short)yOffset));
                            lower = GetUdbTexture(side.Secondary, side.SecondaryLightsourceIndex, side.SecondaryTransferMode, fromFloor ? ConvertUnit((short)lowerPlatformOffset) + floorRoundingOffset : floorRoundingOffset);
                        }
                        else if (side.Type == SideType.High)
                        {
                            var platformOffset = adjacentPlatform != null ? adjacentPlatform.MaximumHeight - adjacentPlatform.MinimumHeight : 0;
                            var yOffset = p.CeilingHeight - adjacentPolygon.CeilingHeight + platformOffset;
                            upper = GetUdbTexture(side.Primary, side.PrimaryLightsourceIndex, side.PrimaryTransferMode, ConvertUnit((short)yOffset));
                        }
                        else if (side.Type == SideType.Low)
                        {
                            double platformOffset = 0;
                            if (adjacentPlatform != null)
                            {
                                if (adjacentPlatform.MaximumHeight > p.CeilingHeight)
                                {
                                    platformOffset += adjacentPlatform.MaximumHeight - adjacentPolygon.CeilingHeight;
                                }
                            }

                            var floor = platformOffset + floorRoundingOffset;

                            lower = GetUdbTexture(side.Primary, side.PrimaryLightsourceIndex, side.PrimaryTransferMode, ConvertUnit((short)floor));
                        }
                        else if (side.Type == SideType.Full)
                        {
                            middle = GetUdbTexture(side.Primary, side.PrimaryLightsourceIndex, side.PrimaryTransferMode);
                        }
                    }
                    else
                    {
                        middle = GetUdbTexture(side.Primary, side.PrimaryLightsourceIndex, side.PrimaryTransferMode);
                    }

                    if (side.IsControlPanel)
                    {
                        controlPanelClassValue = side.ControlPanelClassValue().ToString();
                        var active = false;
                        if (side.IsLightSwitch())
                        {
                            triggerLightIndex = side.ControlPanelPermutation;
                            activatedLights.Add(side.ControlPanelPermutation);
                            active = level.Lights[(short)triggerLightIndex].InitiallyActive;
                        }
                        else if (side.IsPlatformSwitch())
                        {
                            triggerPlatformIndex = side.ControlPanelPermutation;
                            active = level.Platforms.Find(x => x.PolygonIndex == triggerPlatformIndex)?.InitiallyActive ?? false;
                        }

                        if (side.IsRepairSwitch())
                        {

                        }

                        if (middle != null)
                        {
                            SetSwitchActiveTextureState(middle, side, active);
                        }

                        var safeLineIndex = lineIndex + 2000;

                        if (controlPanelClassValue == "PatternBuffer")
                        {
                            scriptCalls.Add($@"ScriptCall(""HealTerminal"", ""Init"", {safeLineIndex}, ""Save"", 0);");
                            scripts.Add($@"script ""Save{lineIndex}Interact"" (void)
{{
	ScriptCall(""HealTerminal"", ""Interact"", {safeLineIndex});
}}
");
                        }
                        else if (controlPanelClassValue == "Oxygen")
                        {
                            scriptCalls.Add($@"ScriptCall(""HealTerminal"", ""Init"", {safeLineIndex}, ""Oxygen"", 0);");
                            scripts.Add($@"script ""Oxygen{lineIndex}Interact"" (void)
{{
	ScriptCall(""HealTerminal"", ""Interact"", {safeLineIndex});
}}
");
                        }
                        else if (controlPanelClassValue == "Shield" || controlPanelClassValue == "DoubleShield" || controlPanelClassValue == "TripleShield")
                        {
                            var factor = controlPanelClassValue == "TripleShield" ? 3 : controlPanelClassValue == "DoubleShield" ? 2 : 1;
                            scriptCalls.Add($@"ScriptCall(""HealTerminal"", ""Init"", {safeLineIndex}, ""Health"", {factor});");
                            scripts.Add($@"script ""Shield{lineIndex}Interact"" (void)
{{
	ScriptCall(""HealTerminal"", ""Interact"", {safeLineIndex});
}}
");
                        }
                        else if (controlPanelClassValue == "Terminal")
                        {
                            scriptCalls.Add($@"ScriptCall(""Terminal"", ""Init"", {safeLineIndex}, {levelIndex}, {side.ControlPanelPermutation});");
                            scripts.Add($@"script ""Terminal{lineIndex}Interact"" (void)
{{
	ScriptCall(""Terminal"", ""Interact"", {safeLineIndex});
}}
");
                        }
                        else if (controlPanelClassValue == "Terminal")
                        {
                            scriptCalls.Add($@"ScriptCall(""Terminal"", ""Init"", {safeLineIndex}, {levelIndex}, {side.ControlPanelPermutation});");
                            scripts.Add($@"script ""Terminal{lineIndex}Interact"" (void)
{{
	ScriptCall(""Terminal"", ""Interact"", {safeLineIndex});
}}
");
                        }
                        else if (controlPanelClassValue == "TagSwitch")
                        {
                            scriptCalls.Add($@"ScriptCall(""TagSwitch"", ""Init"", {safeLineIndex}, {levelIndex}, {side.ControlPanelPermutation});");
                            scripts.Add($@"script ""TagSwitch{lineIndex}Interact"" (void)
{{
	ScriptCall(""TagSwitch"", ""Interact"", {safeLineIndex});
}}
");
                        }
                    }
                }
                else
                {
                    middle = new UdbTexture { Name = "F_SKY1", X = 0, Y = 0, Sky = true };
                }

                var start = level.Endpoints[line.EndpointIndexes[0]];
                var end = level.Endpoints[line.EndpointIndexes[1]];

                var udbLine = new UdbLine
                {
                    Start = new UdbVector
                    {
                        X = ConvertUnit(pointStart.X),
                        Y = -ConvertUnit(pointStart.Y)
                    },
                    End = new UdbVector
                    {
                        X = ConvertUnit(pointEnd.X),
                        Y = -ConvertUnit(pointEnd.Y)
                    },
                    Upper = upper,
                    Middle = middle,
                    Lower = lower,
                    IsSolid = adjacentPolygon != null && line.Solid,
                    DontPegBottom = dontPegBottom,
                    DontPegTop = dontPegTop,
                    BlockSound = blockSound,
                    TriggerLightIndex = triggerLightIndex,
                    TriggerPlatformIndex = triggerPlatformIndex,
                    ControlPanelClassValue = controlPanelClassValue,
                    LineIndex = lineIndex,
                    Portal = Level.Attributes.PortalLines.ContainsKey(lineIndex) ? Level.Attributes.PortalLines[lineIndex] : null
                };

                lines.Add(udbLine);
            }

            var layers = GetLayersForPoly(index);

            foreach (var layer in layers)
            {
                var layerSector = new UdbSector
                {
                    Index = index,
                    Layer = layer,
                    FloorHeight = Math.Round(ConvertUnit(floorHeight)),
                    CeilingHeight = Math.Round(ConvertUnit(ceilingHeight)),
                    FloorTexture = new UdbTexture { Name = FormatTextureName(p.FloorTexture), X = ConvertUnit(p.FloorOrigin.X), Y = ConvertUnit(p.FloorOrigin.Y), Sky = GetSky(p.FloorTransferMode), LightIndex = p.FloorLight, TransferMode = GetTransferMode(p.FloorTransferMode) },
                    CeilingTexture = new UdbTexture { Name = FormatTextureName(p.CeilingTexture), X = ConvertUnit(p.CeilingOrigin.X), Y = ConvertUnit(p.CeilingOrigin.Y), Sky = GetSky(p.CeilingTransferMode), LightIndex = p.CeilingLight, TransferMode = GetTransferMode(p.CeilingTransferMode) },
                    Platform = GetUdbPlatform(platform),
                    Lines = lines.JsonClone(),
                    LightSectorTagId = controlledLightIndexes.Contains(p.FloorLight) ? 500 + p.FloorLight : null,
                    AdditionalTagIds = taggedPolygons.Contains(index) ? [index] : []
                };

                layerSector.Lines.ForEach(l =>
                {
                    l.Start.X += GetLayerXOffset(layer);
                    l.End.X += GetLayerXOffset(layer);
                    l.Start.Y += GetLayerYOffset(layer);
                    l.End.Y += GetLayerYOffset(layer);
                });

                map.Sectors.Add(layerSector);

                var center = GetCenter(layerSector.Lines);

                if (p.Type == PolygonType.MustBeExplored)
                {
                    map.Things.Add(new UdbThing
                    {
                        X = center.X,
                        Y = center.Y,
                        Angle = 0,
                        Type = 30040
                    });
                }
            }
        }

        foreach (var obj in level.Objects.Where(e => !e.NetworkOnly))
        {
            if (obj.Type == ObjectType.Player)
            {
                var sectors = map.Sectors.Where(x => x.Index == (int)obj.PolygonIndex);
                foreach (var sector in sectors)
                {
                    sector.AdditionalTagIds.Add(obj.PolygonIndex);
                }
                startingPolygonIndex = obj.PolygonIndex;
            }

            var layers = GetLayersForPoly(obj.PolygonIndex);

            foreach (var layer in layers)
            {
                var thing = GetUdbThing(obj, layer, levelIndex);
                if (thing != null)
                {
                    map.Things.Add(thing);
                }
            }
        }

        var transferDefinitions = new List<TransferDefinition>();
        foreach (var sector in map.Sectors)
        {
            if (IsCustomTransferMode(sector.FloorTexture.TransferMode))
            {
                transferDefinitions.Add(new TransferDefinition { Sector = sector, Position = TransferPosition.Floor, TransferMode = sector.FloorTexture.TransferMode! });
            }
            if (IsCustomTransferMode(sector.CeilingTexture.TransferMode))
            {
                transferDefinitions.Add(new TransferDefinition { Sector = sector, Position = TransferPosition.Ceiling, TransferMode = sector.CeilingTexture.TransferMode! });
            }
            foreach (var line in sector.Lines)
            {
                if (line.Upper != null && IsCustomTransferMode(line.Upper.TransferMode))
                {
                    transferDefinitions.Add(new TransferDefinition { Sector = sector, Line = line, Position = TransferPosition.Upper, TransferMode = line.Upper.TransferMode! });
                }
                if (line.Middle != null && IsCustomTransferMode(line.Middle.TransferMode))
                {
                    transferDefinitions.Add(new TransferDefinition { Sector = sector, Line = line, Position = TransferPosition.Middle, TransferMode = line.Middle.TransferMode! });
                }
                if (line.Lower != null && IsCustomTransferMode(line.Lower.TransferMode))
                {
                    transferDefinitions.Add(new TransferDefinition { Sector = sector, Line = line, Position = TransferPosition.Lower, TransferMode = line.Lower.TransferMode! });
                }
            }
        }

        var groupedDefs = transferDefinitions.GroupBy(e => new { e.TransferMode, e.Position }).ToList();
        var transferScripts = new List<string>();
        var transferGroupIndex = 3000;
        foreach (var groupedDef in groupedDefs)
        {
            foreach (var definition in groupedDef)
            {
                if (definition.Line != null)
                {
                    definition.Line.AdditionalTagIds.Add(transferGroupIndex);
                }
                else
                {
                    definition.Sector.AdditionalTagIds.Add(transferGroupIndex);
                }
            }
            transferScripts.Add($@"ScriptCall(""Transfer"", ""Init"", {transferGroupIndex}, ""{groupedDef.Key.Position}"", ""{groupedDef.Key.TransferMode}"");");
            transferGroupIndex++;
        }

        //lightabsolute_bottom, lightabsolute_mid, lightabsolute_top, lightabsolute, light, light_top, light_mid, light_bottom
        using var writer = new StreamWriter(path);
        {
            writer.WriteLine($@"/// <reference path=""../udbscript.d.ts"" />
`#version 4`;

`#name {level.Name}`;

let count = 0;

const lineMatch = (line1, line2, tolerance = 0.001) => {{
  const isCollinear = (p1, p2, p3) => {{
    return Math.abs((p3.y - p1.y) * (p2.x - p1.x) - (p2.y - p1.y) * (p3.x - p1.x)) < tolerance;
  }};

  const isWithinBounds = (p, p1, p2) => {{
    return Math.min(p1.x, p2.x) - tolerance <= p.x && p.x <= Math.max(p1.x, p2.x) + tolerance && Math.min(p1.y, p2.y) - tolerance <= p.y && p.y <= Math.max(p1.y, p2.y) + tolerance;
  }};

  return isCollinear(line1[0], line1[1], line2[0]) && isCollinear(line1[0], line1[1], line2[1]) && isWithinBounds(line2[0], line1[0], line1[1]) && isWithinBounds(line2[1], line1[0], line1[1]);
}};

const allSectorLines = [];
const allSectors = [];

const drawSector = async (polygon, debug = false) => {{
  UDB.Map.drawLines([new UDB.Vector2D(polygon.lines[0].start.x, polygon.lines[0].start.y), ...polygon.lines.map((l) => new UDB.Vector2D(l.end.x, l.end.y))]);

  let sectors = UDB.Map.getMarkedSectors();
  for (let sector of sectors) {{
    sector.tag = null;
    allSectors.push({{ sector, polygon }});
    sector.fields.rotationfloor = 90;
    sector.fields.rotationceiling = 90;
    // lightceilingabsolute = true;
    // lightceiling = 150;
    // lightfloor = 32;
    sector.floorHeight = polygon.floorHeight;
    sector.ceilingHeight = polygon.ceilingHeight;
    sector.floorTexture = polygon.floorTexture.sky ? ""F_SKY1"" : polygon.floorTexture.name;
    sector.ceilingTexture = polygon.ceilingTexture.sky ? ""F_SKY1"" : polygon.ceilingTexture.name;
    sector.brightness = getBrightness(polygon.floorTexture.lightIndex);
    if (polygon.ceilingTexture.lightIndex !== polygon.floorTexture.lightIndex) {{
      sector.fields.lightceilingabsolute = true;
      sector.fields.lightceiling = getBrightness(polygon.ceilingTexture.lightIndex);
    }} else {{
      sector.fields.lightceilingabsolute = false;
      sector.fields.lightceiling = 0;
    }}

    for (let l of sector.getSidedefs()) {{
      const sectorLine = polygon.lines.find((x) => {{
        // UDB.showMessage(`[${{x.start.x}}, ${{x.start.y}}] - [${{x.end.x}}, ${{x.end.y}}], [${{l.line.line.v1.x}}, ${{l.line.line.v1.y}}] - [${{l.line.line.v2.x}}, ${{l.line.line.v2.y}}]`);
        return lineMatch([x.start, x.end], [l.line.line.v1, l.line.line.v2]);
      }});
      if (!sectorLine) {{
        continue;
      }}

      if (sectorLine.triggerLightIndex) {{
        l.line.action = 80;
        l.line.flags.repeatspecial = true;
        l.line.flags.playeruse = true;
        l.line.flags.impact = true;
        l.line.fields.arg0str = `Light${{sectorLine.triggerLightIndex}}Toggle`;
        l.line.tag = 1500 + sectorLine.triggerLightIndex;
      }}

      if (sectorLine.triggerPlatformIndex) {{
        l.line.action = 80;
        l.line.flags.repeatspecial = true;
        l.line.flags.playeruse = true;
        l.line.flags.impact = true;
        l.line.fields.arg0str = `Platform${{sectorLine.triggerPlatformIndex}}Toggle`;
        l.line.tag = 1000 + sectorLine.triggerPlatformIndex;
      }}


      if(sectorLine.controlPanelClassValue === ""PatternBuffer"") {{
        l.line.action = 80;
        l.line.flags.repeatspecial = true;
        l.line.flags.playeruse = true;
        l.line.fields.arg0str = `Save${{sectorLine.lineIndex}}Interact`;
        l.line.tag = 2000 + sectorLine.lineIndex;
      }} 

      if(sectorLine.controlPanelClassValue === ""Oxygen"") {{
        l.line.action = 80;
        l.line.flags.repeatspecial = true;
        l.line.flags.playeruse = true;
        l.line.fields.arg0str = `Oxygen${{sectorLine.lineIndex}}Interact`;
        l.line.tag = 2000 + sectorLine.lineIndex;
      }}

      if(sectorLine.controlPanelClassValue === ""Shield"" || sectorLine.controlPanelClassValue === ""DoubleShield"" || sectorLine.controlPanelClassValue === ""TripleShield"") {{
        l.line.action = 80;
        l.line.flags.repeatspecial = true;
        l.line.flags.playeruse = true;
        l.line.fields.arg0str = `Shield${{sectorLine.lineIndex}}Interact`;
        l.line.tag = 2000 + sectorLine.lineIndex;
      }}

      if(sectorLine.controlPanelClassValue === ""Terminal"") {{
        l.line.action = 80;
        l.line.flags.repeatspecial = true;
        l.line.flags.playeruse = true;
        l.line.fields.arg0str = `Terminal${{sectorLine.lineIndex}}Interact`;
        l.line.tag = 2000 + sectorLine.lineIndex;
      }}

      if(sectorLine.controlPanelClassValue === ""TagSwitch"") {{
        l.line.action = 80;
        l.line.flags.repeatspecial = true;
        l.line.flags.playeruse = true;
        l.line.fields.arg0str = `TagSwitch${{sectorLine.lineIndex}}Interact`;
        l.line.tag = 2000 + sectorLine.lineIndex;
      }}
      allSectorLines.push({{ sideIndex: l.index, sectorLine: sectorLine, sector: polygon }});
    }}
  }}
  count++;

  if (count % 5 === 0) {{
    for(let i = 0; i < 100_000; i++){{
      // Simulate work
    }}
  }}
}};

const addThing = (thing) => {{
  const t = UDB.Map.createThing(new UDB.Vector3D(thing.x, thing.y, thing.z), thing.type);
  t.angle = thing.angle;
}};

const getBrightness = (lightIndex) => {{
  const light = level.lights[lightIndex];
  return light.initiallyActive ? light.primaryActive.intensity : light.primaryInactive.intensity;
}};

const applySideChanges = () => {{
  for (const sl of allSectorLines) {{
    const {{ sideIndex, sectorLine, sector }} = sl;

    const line = UDB.Map.getSidedefs()[sideIndex];
    line.line.flags[""blockeverything""] = sectorLine.isSolid;
    //line.line.flags[""dontpegbottom""] = sectorLine.dontPegBottom;
    //line.line.flags[""dontpegtop""] = sectorLine.dontPegTop;
    line.line.flags[""blocksound""] = sectorLine.blockSound;

    for(let tagId of sectorLine.additionalTagIds){{
      line.line.addTag(tagId);
    }}

    if (sectorLine.upper) {{
      if (sectorLine.upper.sky) {{
        line.upperTexture = ""-"";
      }} else {{
        line.upperTexture = sectorLine.upper.name;
        line.fields.offsetx_top = sectorLine.upper.x;
        line.fields.offsety_top = sectorLine.upper.y;
      }}
      if (sectorLine.upper.lightIndex !== sector.floorTexture.lightIndex) {{
        line.fields.lightabsolute_top = true;
        line.fields.light_top = getBrightness(sectorLine.upper.lightIndex);
      }} else {{
        line.fields.lightabsolute_top = false;
        line.fields.light_top = 0;
      }}
    }}
    if (sectorLine.middle) {{
      if (sectorLine.middle.sky) {{
        line.middleTexture = ""-"";
      }} else {{
        line.middleTexture = sectorLine.middle.name;
        line.fields.offsetx_mid = sectorLine.middle.x;
        line.fields.offsety_mid = sectorLine.middle.y;
      }}
      if (sectorLine.middle.lightIndex !== sector.floorTexture.lightIndex) {{
        line.fields.lightabsolute_mid = true;
        line.fields.light_mid = getBrightness(sectorLine.middle.lightIndex);
      }} else {{
        line.fields.lightabsolute_mid = false;
        line.fields.light_mid = 0;
      }}
    }}
    if (sectorLine.lower) {{
      if (sectorLine.lower.sky) {{
        line.lowerTexture = ""-"";
      }} else {{
        line.lowerTexture = sectorLine.lower.name;
        line.fields.offsetx_bottom = sectorLine.lower.x;
        line.fields.offsety_bottom = sectorLine.lower.y;
      }}
      if (sectorLine.lower.lightIndex !== sector.floorTexture.lightIndex) {{
        line.fields.lightabsolute_bottom = true;
        line.fields.light_bottom = getBrightness(sectorLine.lower.lightIndex);
      }} else {{
        line.fields.lightabsolute_bottom = false;
        line.fields.light_bottom = 0;
      }}
    }}
  }}
}};

const applySectorChanges = () => {{
  for (const st of allSectors) {{
    const {{ sector, polygon }} = st;
    if (polygon.platform) {{
      sector.addTag(polygon.index);
    }}
    if (polygon.lightSectorTagId) {{
      sector.addTag(polygon.lightSectorTagId);
    }}
    for(let tagId of polygon.additionalTagIds){{
      sector.addTag(tagId);
    }}

    if (polygon.platform?.isDoor) {{
      sector.getSidedefs().forEach((sidedef) => {{
        const line = sidedef.line;
        if(line.front?.sector && line.back?.sector?.index !== sector.index){{
          line.flip();
        }} 

        if (line.front && sidedef.line.back) {{
          line.action = 80;
          line.flags.repeatspecial = true;
          line.flags.playeruse = true;
          line.flags.monsteruse = polygon.platform?.isMonsterControl ?? false;
          line.fields.arg0str = polygon.platform.touchScript;
        }}
      }});
    }}
  }}
}};

const createPortal = (start, end, flipped) => {{
  if (flipped) {{
    end.flip();
  }} else {{
    start.flip();
  }}

  if (start.tag === 0) {{
    start.tag = UDB.Map.getNewTag();
  }}

  if (end.tag === 0) {{
    end.tag = UDB.Map.getNewTag();
  }}

  start.action = 156;
  start.args[0] = end.tag;
  start.args[2] = 3;

  end.action = 156;
  end.args[0] = start.tag;
  end.args[2] = 3;
}};

const addPortals = () => {{
  const portals = {{}};
  for (const sl of allSectorLines) {{
    const {{ sideIndex, sectorLine, sector }} = sl;
    if (sectorLine.portal !== null) {{
      if (!portals[sectorLine.lineIndex]) {{
        portals[sectorLine.lineIndex] = [];
      }}
      portals[sectorLine.lineIndex].push({{ sideIndex, sectorLine, sector }});
    }}
  }}

  for(const [lineIndex, portalLines] of Object.entries(portals)) {{
    if(portalLines.length >= 2) {{
      const start =  UDB.Map.getSidedefs()[portalLines[0].sideIndex];
      const end =  UDB.Map.getSidedefs()[portalLines[1].sideIndex];
      createPortal(start.line, end.line, portalLines[0].sectorLine.portal);
    }}
  }}
}};

const level = {JsonSerializer.Serialize(map, options)};

const create = async () => {{
  for (const sector of level.sectors) {{
    await drawSector(sector);
  }}

  for (const thing of level.things) {{
    {{
      addThing(thing);
    }}
  }}

  applySideChanges();
  applySectorChanges();
  addPortals();
}};

create().then();
");
        }

        using var acsWriter = new StreamWriter(scriptPath);

        acsWriter.WriteLine($@"#include ""zcommon.acs""

Script ""InitialiseLighting"" ENTER
{{");
        for (var i = 0; i < level.Lights.Count; i++)
        {
            var light = level.Lights[i];
            acsWriter.WriteLine($@"     ScriptCall(""Light"", ""Init"", {i + 500}, {i + 1500}, {light.InitiallyActive.ToString().ToLower()},
        {FormatLightFn(light.PrimaryActive)},
        {FormatLightFn(light.SecondaryActive)},
        {FormatLightFn(light.PrimaryInactive)},
        {FormatLightFn(light.SecondaryInactive)},
        {FormatLightFn(light.BecomingActive)},
        {FormatLightFn(light.BecomingInactive)});
");
        }
        acsWriter.WriteLine($@"}}
");
        foreach (var i in controlledLightIndexes.OrderBy(x => x))
        {
            var light = level.Lights[i];
            var tagId = 500 + i;
            acsWriter.WriteLine($@"script ""Light{i}Toggle"" (void)
{{
	ScriptCall(""Light"", ""Toggle"", {i + 500});
}}
");
        }

        acsWriter.WriteLine($@"Script ""InitialisePlatforms"" ENTER
{{");

        var adjacentPlatformRules = "";

        for (short i = 0; i < level.Platforms.Count; i++)
        {
            var platform = level.Platforms[i];
            var platformId = platform.PolygonIndex;
            var polygon = level.Polygons[platformId];
            if (platform == null) continue;

            var hasAdjacentPlatforms = adjacentPlatforms.TryGetValue(platformId, out var adjacentPlatformIndexes);
            var triggerAdjacent = platform.ActivatesAdjacantPlatformsAtEachLevel
                || platform.ActivatesAdjacentPlatformsWhenActivating
                || platform.ActivatesAdjacentPlatformsWhenDeactivating
                || platform.DeactivatesAdjacentPlatformsWhenActivating
                || platform.DeactivatesAdjacentPlatformsWhenDeactivating;

            acsWriter.WriteLine($@"    ScriptCall(""Platform"", ""Init"", {platformId}, {platformId + 1000}, ""{platform.Type}"", {ConvertUnit(platform.MinimumHeight, 1):F1}, {ConvertUnit(platform.MaximumHeight, 1):F1}, {FormatPlatformSpeed(platform.Speed):F2},  {FormatTicks(platform.Delay)}, {platform.IsDoor.ToString().ToLower()},
	 {platform.ComesFromFloor.ToString().ToLower()},  {platform.ComesFromCeiling.ToString().ToLower()},  {platform.ExtendsFloorToCeiling.ToString().ToLower()},  {platform.InitiallyExtended.ToString().ToLower()},  {platform.InitiallyActive.ToString().ToLower()}, {platform.ContractsSlower.ToString().ToLower()}, {platform.CannotBeExternallyDeactivated.ToString().ToLower()}, {platform.CausesDamage.ToString().ToLower()}, {platform.ReversesDirectionWhenObstructed.ToString().ToLower()},
	 {platform.ActivatesOnlyOnce.ToString().ToLower()},  {platform.DeactivatesAtEachLevel.ToString().ToLower()}, {platform.DeactivatesAtInitialLevel.ToString().ToLower()}, {platform.DelaysBeforeActivation.ToString().ToLower()}, {(platform.ActivatesLight ? polygon.FloorLight + 500 : -1)}, {(platform.DeactivatesLight ? polygon.FloorLight + 500 : -1)}, {platform.IsPlayerControllable.ToString().ToLower()}, {platform.IsMonsterControllable.ToString().ToLower()});
");

            if (hasAdjacentPlatforms && triggerAdjacent)
            {
                adjacentPlatformRules += $@"    ScriptCall(""Platform"", ""SetAdjacentPlatformRules"", {platformId}, {platform.ActivatesAdjacentPlatformsWhenActivating.ToString().ToLower()}, {platform.ActivatesAdjacentPlatformsWhenDeactivating.ToString().ToLower()}, {platform.ActivatesAdjacantPlatformsAtEachLevel.ToString().ToLower()}, {platform.DeactivatesAdjacentPlatformsWhenActivating.ToString().ToLower()}, {platform.DeactivatesAdjacentPlatformsWhenDeactivating.ToString().ToLower()}, {platform.DoesNotActivateParent.ToString().ToLower()});
";
            }
            if (repairPlatforms.Contains(platform.PolygonIndex))
            {
                adjacentPlatformRules += $@"    ScriptCall(""Platform"", ""SetRepairPlatform"", {platformId});
";
            }
        }
        acsWriter.WriteLine(adjacentPlatformRules);
        acsWriter.WriteLine($@"}}
");


        for (short i = 0; i < level.Platforms.Count; i++)
        {
            var platform = level.Platforms[i];
            var platformId = platform.PolygonIndex;
            acsWriter.WriteLine($@"script ""Platform{platformId}Toggle"" (void)
{{
	ScriptCall(""Platform"", ""Toggle"", {platformId});
}}
");
            if (platform.IsDoor)
            {
                acsWriter.WriteLine($@"script ""Door{platformId}Touch"" (void)
{{
	ScriptCall(""Platform"", ""ToggleTouch"", {platformId}, GetActorClass(ActivatorTID()));
}}
");
            }
        }


        acsWriter.WriteLine($@"Script ""InitialisePolygonTypes"" ENTER
{{");
        for (short i = 0; i < level.Polygons.Count; i++)
        {
            var p = level.Polygons[i];
            switch (p.Type)
            {
                case PolygonType.LightOnTrigger:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""LightActivate"", {p.Permutation + 500});");
                    break;
                case PolygonType.LightOffTrigger:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""LightDeactivate"", {p.Permutation + 500});");
                    break;
                case PolygonType.PlatformOnTrigger:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""PlatformActivate"", {p.Permutation});");
                    break;
                case PolygonType.PlatformOffTrigger:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""PlatformDeactivate"", {p.Permutation});");
                    break;
                case PolygonType.Teleporter:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""Teleport"", {p.Permutation});");
                    break;
                case PolygonType.VisibleMonsterTrigger:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""VisibleMonsterTrigger"", {p.Permutation});");
                    break;
                case PolygonType.InvisibleMonsterTrigger:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""InvisibleMonsterTrigger"", {p.Permutation});");
                    break;
                case PolygonType.DualMonsterTrigger:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""DualMonsterTrigger"", {p.Permutation});");
                    break;
                case PolygonType.ItemTrigger:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""ItemTrigger"", {p.Permutation});");
                    break;
                case PolygonType.MustBeExplored:
                    //                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""MustBeExplored"", {p.Permutation});");
                    //handled with ExploreMarker
                    break;
                case PolygonType.AutomaticExit:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""AutomaticExit"", {p.Permutation});");
                    break;
                case PolygonType.MinorOuch:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""MinorOuch"", {p.Permutation});");
                    break;
                case PolygonType.MajorOuch:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""MajorOuch"", {p.Permutation});");
                    break;
                case PolygonType.Glue:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""Glue"", {p.Permutation});");
                    break;
                case PolygonType.GlueTrigger:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""GlueTrigger"", {p.Permutation});");
                    break;
                case PolygonType.Superglue:
                    acsWriter.WriteLine($@"    ScriptCall(""Polygon"", ""Init"", {i}, ""Superglue"", {p.Permutation});");
                    break;
                case PolygonType.ItemImpassable:
                case PolygonType.MonsterImpassable:
                case PolygonType.ZoneBorder:
                case PolygonType.Goal:
                    break;
            }
        }

        acsWriter.WriteLine($@"}}
");


        acsWriter.WriteLine($@"Script ""InitialiseTerminals"" ENTER
{{
   ScriptCall(""Terminal"", ""InitDefinitions"");");
        foreach (var scriptCall in scriptCalls)
        {
            acsWriter.WriteLine($"   {scriptCall}");
        }

        acsWriter.WriteLine($@"}}
");

        acsWriter.WriteLine($@"Script ""InitialiseTransferModes"" ENTER
{{");

        foreach (var scriptCall in transferScripts)
        {
            acsWriter.WriteLine($"   {scriptCall}");
        }

        acsWriter.WriteLine($@"}}
");


        acsWriter.WriteLine($@"Script ""InitialiseLevel"" ENTER
{{");

        acsWriter.WriteLine($@"    ScriptCall(""LevelManager"", ""SetLevelindex"", {levelIndex});
    ScriptCall(""LevelManager"", ""SetStart"", {startingPolygonIndex});
");

        acsWriter.WriteLine($@"}}
");


        foreach (var script in scripts)
        {
            acsWriter.WriteLine(script);
        }
    }

    private bool IsCustomTransferMode(string? transferMode)
    {
        return !string.IsNullOrEmpty(transferMode) && transferMode != "Normal" && transferMode != "Landscape";
    }

    private double CalculateFloorRoundingOffset(Polygon p, Polygon adjacentPolygon)
    {
        var a = ConvertUnit(adjacentPolygon.FloorHeight, null);
        var b = ConvertUnit(p.FloorHeight, null);

        return a - b - (Math.Round(a) - Math.Round(b));
    }

    private static Regex regex = new Regex(@"^(?<num1>\d*)SET(?<num2>\d*)$", RegexOptions.Compiled);
    private void SetSwitchActiveTextureState(UdbTexture middle, Side side, bool active)
    {
        var match = regex.Match(middle.Name);
        var num1 = match.Groups["num1"].Value;
        var num2 = match.Groups["num2"].Value;


        if (side.IsPlatformSwitch() || side.IsLightSwitch())
        {
            middle.Name = active ? $"{num1}SET00" : $"{num1}SET01";
        }

        if (side.IsTagSwitch())
        {
            if (num1 == "1" || num1 == "2")
            {
                middle.Name = active ? $"{num1}SET36" : $"{num1}SET37";
            }
            else if (num1 == "3")
            {
                middle.Name = active ? $"{num1}SET35" : $"{num1}SET36";
            }
        }
    }

    private (double X, double Y) GetCenter(List<UdbLine> vertices)
    {
        var n = vertices.Count;
        double area = 0, cx = 0, cy = 0;

        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n; // Next vertex (wraps around)
            var crossProduct = (vertices[i].Start.X * vertices[j].Start.Y) - (vertices[j].Start.X * vertices[i].Start.Y);

            area += crossProduct;
            cx += (vertices[i].Start.X + vertices[j].Start.X) * crossProduct;
            cy += (vertices[i].Start.Y + vertices[j].Start.Y) * crossProduct;
        }

        area *= 0.5;
        cx /= 6 * area;
        cy /= 6 * area;

        return (cx, cy);
    }

    private string FormatLightFn(Light.Function fn)
    {
        return $@"""{fn.LightingFunction.ToString()}"", {FormatTicks(fn.Period)}, {FormatTicks(fn.DeltaPeriod)}, {FormatIntensity(fn.Intensity)}, {FormatIntensity(fn.DeltaIntensity, false)}";
    }

    private int GetLightFunction(Light.Function fn)
    {
        switch (fn.LightingFunction)
        {
            case LightingFunction.Linear:
                return 1;
            case LightingFunction.Smooth:
                return 2;
            case LightingFunction.Flicker:
                return 3;
            default:
                return 0;
        }
    }

    private UdbThing? GetUdbThing(MapObject obj, int layer, int levelIndex)
    {
        var type = GetThingType(obj, levelIndex);
        if (type == null)
        {
            return null;
        }

        var t = new UdbThing
        {
            Type = type.Value,
            X = ConvertUnit(obj.X) + GetLayerXOffset(layer),
            Y = -ConvertUnit(obj.Y) + GetLayerYOffset(layer),
            Z = ConvertUnit(obj.Z),
            Angle = 360.0 - obj.Facing
        };

        return t;
    }

    private int? GetThingType(MapObject obj, int levelIndex)
    {
        if (obj.Type == ObjectType.Player)
        {
            return 1;
        }

        if (obj.Type == ObjectType.Goal)
        {
            return 9001;
        }

        var isRevolution = levelIndex >= 24;

        var name = GetThingName(obj);
        switch (name)
        {
            //monsters
            case "Fighter Minor":
                return 16000;
            case "Fighter Major":
                return 16001;
            case "Fighter Minor Projectile":
                return 16002;
            case "Fighter Major Projectile":
                return 16003;
            case "Compiler Minor":
                return isRevolution ? 16014 : 16010;
            case "Compiler Major":
                return isRevolution ? 16015 : 16011;
            case "Compiler Minor Invisible":
                return isRevolution ? 16016 : 16012;
            case "Compiler Major Invisible":
                return isRevolution ? 16017 : 16013;
            case "Trooper Minor":
                return 16020;
            case "Trooper Major":
                return 16021;
            case "Hunter Minor":
                return 16051;
            case "Hunter Major":
                return 16052;
            case "Enforcer Minor":
                return 16040;
            case "Enforcer Major":
                return 16041;
            case "Juggernaut Minor":
                return 17000;
            case "Juggernaut Major":
                return 17000;
            case "Defender Minor":
                return 16081;
            case "Hummer Possessed":
                return 16082;
            case "Sewage Yeti":
                return 16071;
            case "Water Yeti":
                return 16071;
            case "Hummer Minor":
                return 16061;
            case "Hummer Major":
                return 16062;

            case "Civilian Crew":
                return 16030;
            case "Civilian Science":
                return 16031;
            case "Civilian Security":
                return 16032;
            case "Civilian Engineering": //TODO: doesnt exist?
                return 16033;
            case "Civilian Assimilated":
                return 16034;
            //items
            case "Magnum Pistol":
                return 5010;
            case "Magnum Magazine":
                return 2007;
            case "Plasma Pistol":
                return 2004;
            case "Plasma Energy Cell":
                return 2047;
            case "Assault Rifle":
                return 2002;
            case "AR Magazine":
                return 2048;
            case "AR Grenade Magazine":
                return 2010;
            case "Missile Launcher":
                return 2003;
            case "Missile 2-Pack":
                return 2046;
            case "Flamethrower":
                return 82;
            case "Flamethrower Canister":
                return 17;
            case "Alien Weapon":
                return 2006;
            case "Uplink Chip":
                return 30030;
            //"Invisibility Powerup",
            //"Invincibility Powerup",
            //"Infravision Powerup",




            //scenery
            case "(S) Big Bones":
            case "(W) Alien Supply Can":
                return 30000;
            case "(S) Pfhor Pieces":
                return 30001;
            case "(S) Bob Pieces":
                return 30008;
            case "(S) Bob Blood":
                return 30010;
            case "(S) Big Antenna #1":
            case "(W) Security Monitor":
                return 30002;
            case "(S) Big Antenna #2":
            case "(W) Rocks":
                return 30004;

            default:
                return 80000;


                //16000 = "Fighter1"
                //16001 = "Fighter2"
                //16002 = "Fighter3"
                //16003 = "Fighter4"
                //16010 = "Compiler1"
                //16011 = "Compiler2"
                //16012 = "Compiler3"
                //16013 = "Compiler4"
                //16020 = "Trooper1"
                //16021 = "Trooper2"
                //16030 = "Bob1"
                //16031 = "Bob2"
                //16032 = "Bob3"
                //16033 = "Bob4"
                //16040 = "Enforcer1"
                //16041 = "Enforcer2"
                //16051 = "Hunter1"
                //16052 = "Hunter2"
                //16061 = "Wasp1"
                //16062 = "Wasp2"
                //16063 = "Wasp3"
                //16071 = "Hulk1"
                //16072 = "Hulk2"
                //17000 = "Juggernaut"


                //              "Tick Energy",
                //"Tick Oxygen",
                //"Tick Kamakazi",
                //"Compiler Minor",
                //"Compiler Major",
                //"Compiler Minor Invisible",
                //"Compiler Major Invisible",
                //"Fighter Minor",
                //"Fighter Major",
                //"Fighter Minor Projectile",
                //"Fighter Major Projectile",
                //"Civilian Crew",
                //"Civilian Science",
                //"Civilian Security",
                //"Civilian Assimilated",
                //"Hummer Minor",
                //"Hummer Major",
                //"Hummer Big Minor",
                //"Hummer Big Major",
                //"Hummer Possessed",
                //"Cyborg Minor",
                //"Cyborg Major",
                //"Cyborg Flame Minor",
                //"Cyborg Flame Major",
                //"Enforcer Minor",
                //"Enforcer Major",
                //"Hunter Minor",
                //"Hunter Major",
                //"Trooper Minor",
                //"Trooper Major",
                //"Mother of all Cyborgs",
                //"Mother of all Hunters",
                //"Sewage Yeti",
                //"Water Yeti",
                //"Lava Yeti",
                //"Defender Minor",
                //"Defender Major",
                //"Tiny Figher",
                //"Tiny Bob",
                //"Tiny Yeti",
                //"Civilian Fusion Crew",
                //"Civilian Fusion Science",
                //"Civilian Fusion Security",
                //"Civilian Fusion Assimilated"

        }
    }

    private string? GetThingName(MapObject obj)
    {
        switch (obj.Type)
        {
            case ObjectType.Monster:
                return monsterNames[obj.Index];
            case ObjectType.Item:
                return itemNames[obj.Index];
            case ObjectType.Scenery:
                return sceneryNames[obj.Index];
        }
        return null;
    }

    private UdbTexture? GetUdbTexture(TextureDefinition? texture, short lightSourceIndex, short transferMode, double? yOffset = null)
    {
        if (texture == null)
        {
            return null;
        }
        var x = ConvertUnit(texture.Value.X, null);
        var y = ConvertUnit(texture.Value.Y, null) + (yOffset ?? 0);

        //if (bump && y % 1 != 0)
        //{
        //    y -= 1;
        //}


        //y = Math.Floor(y);
        var mode = GetTransferMode(transferMode);
        return new UdbTexture { Name = FormatTextureName(texture.Value.Texture), X = x, Y = y, Sky = GetSky(transferMode), LightIndex = lightSourceIndex, TransferMode = mode };
    }

    public UdbLight GetLight(Light light, short index)
    {
        return new UdbLight
        {
            Index = index,
            InitiallyActive = light.InitiallyActive,
            PrimaryActive = GetLightState(light.PrimaryActive),
            SecondaryActive = GetLightState(light.SecondaryActive),
            PrimaryInactive = GetLightState(light.PrimaryInactive),
            SecondaryInactive = GetLightState(light.SecondaryInactive),
            BecomingActive = GetLightState(light.BecomingActive),
            BecomingInactive = GetLightState(light.BecomingInactive)
        };
    }

    public bool IsUnchangingState(Light light, bool? active = null)
    {
        var primary = light.InitiallyActive ? light.PrimaryActive : light.PrimaryInactive;
        var secondary = light.InitiallyActive ? light.SecondaryActive : light.SecondaryInactive;
        if (active.HasValue)
        {
            primary = active.Value ? light.PrimaryActive : light.PrimaryInactive;
            secondary = active.Value ? light.SecondaryActive : light.SecondaryInactive;
        }
        return LightStatesAreEqual(primary, secondary);
    }

    public bool LightStatesAreEqual(Light.Function primary, Light.Function secondary)
    {
        return primary.LightingFunction == LightingFunction.Constant && secondary.LightingFunction == LightingFunction.Constant && primary.Intensity == secondary.Intensity;
    }

    public UdbLightState GetLightState(Light.Function lightState)
    {
        return new UdbLightState
        {
            Function = lightState.LightingFunction.ToString(),
            Intensity = FormatIntensity(lightState.Intensity),
            DeltaIntensity = FormatIntensity(lightState.DeltaIntensity),
            Period = FormatTicks(lightState.Period),
            DeltaPeriod = FormatTicks(lightState.DeltaPeriod)
        };
    }

    private bool GetSky(short transferMode)
    {
        return GetTransferMode(transferMode) == "Landscape";
    }

    private UdbPlatform? GetUdbPlatform(Platform? platform)
    {
        if (platform == null)
        {
            return null;
        }
        return new UdbPlatform
        {
            MaxHeight = ConvertUnit(platform.MaximumHeight),
            MinHeight = ConvertUnit(platform.MinimumHeight),
            Speed = platform.Speed,
            Delay = platform.Delay,
            IsDoor = platform.IsDoor,
            IsCeiling = platform.ComesFromCeiling,
            IsFloor = platform.ComesFromFloor,
            IsExtended = platform.InitiallyExtended,
            IsLocked = platform.IsLocked,
            IsPlayerControl = platform.IsPlayerControllable,
            IsMonsterControl = platform.IsMonsterControllable,
            TouchScript = platform.IsDoor ? $"Door{platform.PolygonIndex}Touch" : null
        };
    }

    private string GetTransferMode(short transferMode)
    {
        return transferModes[transferModeMapping.IndexOf(transferMode)];
    }

    private string FormatTextureName(ShapeDescriptor shapeDescriptor)
    {
        var env = level.Environment + 1;
        if (env == 5)
        {
            env = 4;
        }

        var texId = shapeDescriptor.Bitmap;
        if (env == 3 && texId > 27)
        {
            texId -= 2;
        }
        else if (env == 3 && texId > 13)
        {
            texId--;
        }
        else if (env == 1 && texId > 30)
        {
            texId--;
        }
        else if (env == 4 && texId > 10)
        {
            texId--;
        }

        return $"{env}SET{texId.ToString("00")}";
    }

    private short FormatIntensity(double intensity, bool includeMin = true)
    {
        return (short)((intensity * 155) + (includeMin ? 100 : 0));
    }

    private double FormatPlatformSpeed(short speed, int decimalPlaces = 2)
    {
        //ok so the theory is
        // - marathon speed = world units per second (30 ticks)
        //doom speed = world units 64 per sec (35 ticks)
        // its almost x/2 
        return Math.Round((double)speed / 30 * 64 / 35, decimalPlaces);
    }

    private short FormatTicks(double marathonTicks)
    {
        return (short)((double)marathonTicks / 30 * 35);
    }

    private double ConvertUnit(short val, int? decimalPlaces = 3)
    {
        var converted = World.ToDoom(val);
        if (decimalPlaces.HasValue)
        {
            return Math.Round(converted, decimalPlaces.Value);
        }
        return converted;
    }
}

public record UdbLevel
{
    public List<UdbSector> Sectors { get; set; } = [];
    public List<UdbLight> Lights { get; set; } = [];
    public List<UdbThing> Things { get; set; } = [];
}

public record UdbLight
{
    public int Index { get; set; }
    public bool InitiallyActive { get; set; }
    public required UdbLightState PrimaryActive { get; set; }
    public required UdbLightState SecondaryActive { get; set; }
    public required UdbLightState PrimaryInactive { get; set; }
    public required UdbLightState SecondaryInactive { get; set; }
    public required UdbLightState BecomingActive { get; set; }
    public required UdbLightState BecomingInactive { get; set; }
}

public record UdbLightState
{
    public required string Function { get; set; }
    public short Intensity { get; set; }
    public short DeltaIntensity { get; set; }
    public short Period { get; set; }
    public short DeltaPeriod { get; set; }
}

public record UdbSector
{
    public int Index { get; set; }
    public int Layer { get; set; }
    public double FloorHeight { get; set; }
    public double CeilingHeight { get; set; }
    public required UdbTexture FloorTexture { get; set; }
    public required UdbTexture CeilingTexture { get; set; }
    public UdbPlatform? Platform { get; set; }
    public int? LightSectorTagId { get; set; }
    public HashSet<int> AdditionalTagIds { get; set; } = [];
    public List<UdbLine> Lines { get; set; } = [];
}

public record UdbPlatform
{
    public double MaxHeight { get; set; }
    public double MinHeight { get; set; }
    public double Speed { get; set; }
    public double Delay { get; set; }
    public bool IsCeiling { get; set; }
    public bool IsFloor { get; set; }
    public bool IsDoor { get; set; }
    public bool IsExtended { get; set; }
    public bool IsLocked { get; set; }
    public bool IsPlayerControl { get; set; }
    public bool IsMonsterControl { get; set; }
    public string? TouchScript { get; set; }
}

public record UdbLine
{
    public required UdbVector Start { get; set; }
    public required UdbVector End { get; set; }
    public UdbTexture? Upper { get; set; }
    public UdbTexture? Middle { get; set; }
    public UdbTexture? Lower { get; set; }
    public bool IsSolid { get; set; }
    public bool DontPegTop { get; set; }
    public bool DontPegBottom { get; set; }
    public bool BlockSound { get; set; }
    public int? TriggerLightIndex { get; set; }
    public int? TriggerPlatformIndex { get; set; }
    public string? ControlPanelClassValue { get; set; }
    public int? LineIndex { get; set; }
    public bool? Portal { get; set; } // true = normal, false = flipped, false = none
    public HashSet<int> AdditionalTagIds { get; set; } = [];
}

public record UdbVector
{
    public double X { get; set; }
    public double Y { get; set; }
}

public record UdbTexture
{
    public required string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool Sky { get; set; }
    public int LightIndex { get; set; }
    public string? TransferMode { get; set; }
}

public record UdbThing
{
    public int Type { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double Angle { get; set; }
    public int? TagId { get; set; }
}

public enum TransferPosition
{
    Ceiling,
    Floor,
    Upper,
    Middle,
    Lower
};

public record TransferDefinition
{
    public UdbSector Sector { get; set; }
    public UdbLine? Line { get; set; }
    public required string TransferMode { get; set; }
    public TransferPosition Position { get; set; }
}