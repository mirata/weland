using System.Text.Json;
using static Weland.Side;

namespace Weland;

public class UDBExporter
{
    const double Scale = 64.0;

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

    public UDBExporter(Level level)
    {
        this.level = level;
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

        Dictionary<short, int> lightSwitchMapInfos = [];
        Dictionary<short, int> platformSwitchMapInfos = [];

        HashSet<short> initialisedLights = [];

        Dictionary<short, HashSet<int>> adjacentPlatforms = [];

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
                    lightSwitchMapInfos[side.PolygonIndex] = side.ControlPanelPermutation + 2000;
                    activatedLights.Add(side.ControlPanelPermutation);
                }
                else
                {
                    platformSwitchMapInfos[side.PolygonIndex] = side.ControlPanelPermutation;
                }
            }
        }

        var controlledLightIndexes = initialisedLights.Concat(activatedLights).ToHashSet();

        for (short index = 0; index < level.Polygons.Count; index++)
        {
            var p = level.Polygons[index];
            var platform = level.Platforms.FirstOrDefault(e => e.PolygonIndex == index);
            var floorHeight = p.FloorHeight;
            var ceilingHeight = p.CeilingHeight;
            if (platform != null)
            {
                if (platform.ComesFromCeiling)
                {
                    if (platform.InitiallyExtended)
                    {
                        ceilingHeight = Math.Max(p.FloorHeight, platform.MinimumHeight);
                    }
                    else if (platform.MinimumHeight < p.CeilingHeight)
                    {
                        ceilingHeight = Math.Max(p.FloorHeight, platform.MinimumHeight);
                    }
                }
                if (platform.ComesFromFloor)
                {
                    if (platform.InitiallyExtended)
                    {
                        floorHeight = Math.Min(p.CeilingHeight, platform.MaximumHeight);
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
                var pointStart = level.Endpoints[p.EndpointIndexes[i]];
                var pointEnd = level.Endpoints[p.EndpointIndexes[i == p.VertexCount - 1 ? 0 : i + 1]];
                var line = level.Lines[p.LineIndexes[i]];
                var side = level.Sides.FirstOrDefault(e => e.LineIndex == p.LineIndexes[i] && e.PolygonIndex == index);// p.SideIndexes[i] > -1 && p.SideIndexes[i] < level.Sides.Count ? level.Sides[p.SideIndexes[i]] : null;
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

                if (side != null)
                {
                    if (adjacentPolygon != null)
                    {
                        if (side.Type == SideType.Split)
                        {
                            var yOffset = ConvertUnit(p.CeilingHeight) - ConvertUnit(adjacentPolygon.CeilingHeight);
                            upper = GetUdbTexture(side.Primary, side.PrimaryLightsourceIndex, side.PrimaryTransferMode, yOffset);
                            lower = GetUdbTexture(side.Secondary, side.SecondaryLightsourceIndex, side.SecondaryTransferMode);
                        }
                        else if (side.Type == SideType.High)
                        {
                            var platformOffset = adjacentPlatform != null ? ConvertUnit(Math.Min(adjacentPolygon.CeilingHeight, adjacentPlatform.MaximumHeight)) - ConvertUnit(Math.Max(adjacentPolygon.FloorHeight, adjacentPlatform.MinimumHeight)) : 0;
                            var yOffset = ConvertUnit(p.CeilingHeight) - ConvertUnit(adjacentPolygon.CeilingHeight) + platformOffset;
                            upper = GetUdbTexture(side.Primary, side.PrimaryLightsourceIndex, side.PrimaryTransferMode, yOffset);
                        }
                        else if (side.Type == SideType.Low)
                        {
                            var platformOffset = adjacentPlatform != null ? ConvertUnit(Math.Min(adjacentPolygon.CeilingHeight, adjacentPlatform.MaximumHeight)) - ConvertUnit(Math.Max(adjacentPolygon.FloorHeight, adjacentPlatform.MinimumHeight)) : 0;
                            var yOffset = ConvertUnit(p.CeilingHeight) - ConvertUnit(adjacentPolygon.CeilingHeight) - platformOffset;
                            lower = GetUdbTexture(side.Primary, side.PrimaryLightsourceIndex, side.PrimaryTransferMode);
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

                        if (middle != null)
                        {
                            SetSwitchTexture(middle, active);
                        }
                    }
                }
                else
                {
                    middle = new UdbTexture { Name = "F_SKY1", X = 0, Y = 0, Sky = true };
                }

                var start = level.Endpoints[line.EndpointIndexes[0]];
                var end = level.Endpoints[line.EndpointIndexes[1]];

                lines.Add(new UdbLine
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
                    TriggerLightIndex = triggerLightIndex,
                    TriggerPlatformIndex = triggerPlatformIndex
                });
            }

            if (Level.DetachedPolygonIndexes.Contains(index))
            {
                lines.ForEach(l =>
                {
                    l.Start.X -= 2048;
                    l.End.X -= 2048;
                    l.Start.Y += 2048;
                    l.End.Y += 2048;
                });
            }

            var sector = new UdbSector
            {
                Index = index,
                FloorHeight = ConvertUnit(floorHeight),
                CeilingHeight = ConvertUnit(ceilingHeight),
                FloorTexture = new UdbTexture { Name = FormatTextureName(p.FloorTexture), X = ConvertUnit(p.FloorOrigin.X), Y = ConvertUnit(p.FloorOrigin.Y), Sky = GetSky(p.FloorTransferMode), LightIndex = p.FloorLight },
                CeilingTexture = new UdbTexture { Name = FormatTextureName(p.CeilingTexture), X = ConvertUnit(p.CeilingOrigin.X), Y = ConvertUnit(p.CeilingOrigin.Y), Sky = GetSky(p.CeilingTransferMode), LightIndex = p.CeilingLight },
                Platform = GetUdbPlatform(platform),
                Lines = lines,
                LightSectorTagId = controlledLightIndexes.Contains(p.FloorLight) ? 500 + p.FloorLight : null
            };

            map.Sectors.Add(sector);

            var center = GetCenter(lines);

            if (lightSwitchMapInfos.ContainsKey(index))
            {
                map.Things.Add(new UdbThing
                {
                    X = center.X,
                    Y = center.Y,
                    Angle = 0,
                    Type = 9001,
                    TagId = lightSwitchMapInfos[index]
                });
            }
        }

        foreach (var obj in level.Objects.Where(e => !e.NetworkOnly))
        {
            var thing = GetUdbThing(obj);
            if (thing != null)
            {
                map.Things.Add(thing);
            }
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
      allSectorLines.push({{ sideIndex: l.index, sectorLine: sectorLine, sector: polygon }});
    }}
  }}
  count++;
  //if (count % 25 === 0) {{
    //await new Promise((resolve) => setTimeout(resolve, 10));
  //}}
}};

const addThing = (thing) => {{
  const t = UDB.Map.createThing(new UDB.Vector3D(thing.x, thing.y, thing.z), thing.type);
  t.angle = thing.angle;
}};

const getBrightness = (lightIndex) => {{
  const light = level.lights[lightIndex];
  return light.initiallyActive ? light.primaryActive.intensity : light.primaryInactive.intensity;
}};

const applySideTextures = () => {{
  for (const sl of allSectorLines) {{
    const {{ sideIndex, sectorLine, sector }} = sl;

    const line = UDB.Map.getSidedefs()[sideIndex];
    line.line.flags[""blockeverything""] = sectorLine.isSolid;

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
      // ensure all linedefs are pointed out
      sector.getSidedefs().forEach((sidedef) => {{
        const line = sidedef.line;
        if(line.front?.sector !== null && line.back?.sector !== null && line.front.sector.index === sector.index){{
          line.flip();
        }}
      }});
      sector.addTag(polygon.index);
    }}
    if (polygon.lightSectorTagId) {{
      sector.addTag(polygon.lightSectorTagId);
    }}

    if (polygon.platform?.isDoor) {{
      sector.getSidedefs().forEach((sidedef) => {{
        if (sidedef.line.front && sidedef.line.back) {{
          sidedef.line.action = 80;
          sidedef.line.flags.repeatspecial = true;
          sidedef.line.flags.playeruse = true;
          sidedef.line.fields.arg0str = polygon.platform.touchScript;
        }}
      }});
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

  applySideTextures();
  applySectorChanges();
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

            acsWriter.WriteLine($@"    ScriptCall(""Platform"", ""Init"", {platformId}, {platformId + 1000}, {ConvertUnit(platform.MinimumHeight, 1):F1}, {ConvertUnit(platform.MaximumHeight, 1):F1}, {FormatPlatformSpeed(platform.Speed):F2},  {FormatTicks(platform.Delay)}, {platform.IsDoor.ToString().ToLower()},
	 {platform.ComesFromFloor.ToString().ToLower()},  {platform.ComesFromCeiling.ToString().ToLower()},  {platform.ExtendsFloorToCeiling.ToString().ToLower()},  {platform.InitiallyExtended.ToString().ToLower()},  {platform.InitiallyActive.ToString().ToLower()},
	 {platform.ActivatesOnlyOnce.ToString().ToLower()},  {platform.DeactivatesAtEachLevel.ToString().ToLower()}, {platform.DeactivatesAtInitialLevel.ToString().ToLower()}, {platform.DelaysBeforeActivation.ToString().ToLower()}, {(platform.ActivatesLight ? polygon.FloorLight + 500 : -1)}, {(platform.DeactivatesLight ? polygon.FloorLight + 500 : -1)}, {platform.IsPlayerControllable.ToString().ToLower()}, {platform.IsMonsterControllable.ToString().ToLower()});
");

            if (hasAdjacentPlatforms && triggerAdjacent)
            {
                adjacentPlatformRules += $@"    ScriptCall(""Platform"", ""SetAdjacentPlatformRules"", {platformId}, {platform.ActivatesAdjacentPlatformsWhenActivating.ToString().ToLower()}, {platform.ActivatesAdjacentPlatformsWhenDeactivating.ToString().ToLower()}, {platform.ActivatesAdjacantPlatformsAtEachLevel.ToString().ToLower()}, {platform.DeactivatesAdjacentPlatformsWhenActivating.ToString().ToLower()}, {platform.DeactivatesAdjacentPlatformsWhenDeactivating.ToString().ToLower()});
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
	ScriptCall(""Platform"", ""Toggle"", {platformId}, GetActorClass(ActivatorTID()));
}}
");
            if (platform.IsDoor)
            {
                acsWriter.WriteLine($@"script ""Door{platformId}Touch"" (void)
{{
	ScriptCall(""Platform"", ""Toggle"", {platformId}, GetActorClass(ActivatorTID()));
}}
");
            }
        }
    }

    private void SetSwitchTexture(UdbTexture middle, bool active)
    {
        if (!middle.Name.Contains("SET00") && !middle.Name.Contains("SET01"))
        {
            return;
        }
        if (active)
        {
            middle.Name = middle.Name.Replace("SET01", "SET00");
        }
        else
        {
            middle.Name = middle.Name.Replace("SET00", "SET01");
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

    private UdbThing? GetUdbThing(MapObject obj)
    {
        var type = GetThingType(obj);
        if (type == null)
        {
            return null;
        }

        var t = new UdbThing
        {
            Type = type.Value,
            X = ConvertUnit(obj.X),
            Y = -ConvertUnit(obj.Y),
            Z = ConvertUnit(obj.Z),
            Angle = 360.0 - obj.Facing
        };

        if (Level.DetachedPolygonIndexes.Contains(obj.PolygonIndex))
        {
            t.X -= 2048;
            t.Y += 2048;
        }

        return t;
    }

    private int? GetThingType(MapObject obj)
    {
        if (obj.Type == ObjectType.Player)
        {
            return 1;
        }

        if (obj.Type == ObjectType.Goal)
        {
            return 9001;
        }

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
                return 16010;
            case "Compiler Major":
                return 16011;
            case "Compiler Minor Invisible":
                return 16012;
            case "Compiler Major Invisible":
                return 16013;

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
            //"Invisibility Powerup",
            //"Invincibility Powerup",
            //"Infravision Powerup",


            //scenery
            case "(S) Big Bones":
                return 30000;
            case "(S) Pfhor Pieces":
                return 30001;
            case "(S) Bob Pieces":
                return 30008;
            case "(S) Bob Blood":
                return 30010;
            case "(S) Big Antenna #1":
                return 30006;
            case "(S) Big Antenna #2":
                return 30004;
            default:
                return null;


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
        return new UdbTexture { Name = FormatTextureName(texture.Value.Texture), X = Math.Round(ConvertUnit(texture.Value.X)), Y = Math.Round(ConvertUnit(texture.Value.Y) + (yOffset ?? 0)), Sky = GetSky(transferMode), LightIndex = lightSourceIndex };
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
        return transferMode == 9;
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

    private double ConvertUnit(short val, int decimalPlaces = 3)
    {
        return Math.Round(World.ToDouble(val) * Scale, decimalPlaces);
    }
}


public class UdbLevel
{
    public List<UdbSector> Sectors { get; set; } = [];
    public List<UdbLight> Lights { get; set; } = [];
    public List<UdbThing> Things { get; set; } = [];
}

public class UdbLight
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

public class UdbLightState
{
    public required string Function { get; set; }
    public short Intensity { get; set; }
    public short DeltaIntensity { get; set; }
    public short Period { get; set; }
    public short DeltaPeriod { get; set; }
}

public class UdbSector
{
    public int Index { get; set; }
    public double FloorHeight { get; set; }
    public double CeilingHeight { get; set; }
    public required UdbTexture FloorTexture { get; set; }
    public required UdbTexture CeilingTexture { get; set; }
    public UdbPlatform? Platform { get; set; }
    public int? LightSectorTagId { get; set; }
    public List<UdbLine> Lines { get; set; } = [];
}

public class UdbPlatform
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

public class UdbLine
{
    public required UdbVector Start { get; set; }
    public required UdbVector End { get; set; }
    public UdbTexture? Upper { get; set; }
    public UdbTexture? Middle { get; set; }
    public UdbTexture? Lower { get; set; }
    public bool IsSolid { get; set; }
    public int? TriggerLightIndex { get; set; }
    public int? TriggerPlatformIndex { get; set; }
}

public class UdbVector
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class UdbTexture
{
    public required string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool Sky { get; set; }
    public int LightIndex { get; set; }
}

public class UdbThing
{
    public int Type { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double Angle { get; set; }
    public int? TagId { get; set; }
}