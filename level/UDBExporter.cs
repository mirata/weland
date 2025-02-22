using System;
using System.IO;
using System.Collections.Generic;
using Pango;
using static Weland.Side;
using System.Xml.Linq;
using Gtk;
using Weland;
using System.Drawing.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Net.WebSockets;
using Cairo;
using Gdk;
using static GLib.Signal;
using System.Diagnostics.Metrics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.JavaScript;
using System.Reflection;

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

        var initialisedLights = new HashSet<short>();

        for (short i = 0; i < level.Lights.Count; i++)
        {
            var light = level.Lights[i];
            if (!IsUnchangingState(light))
            {
                initialisedLights.Add(i);
            }

            map.Lights.Add(GetLight(light, i));
        }


        var activatedLights = new HashSet<short>();

        for (short index = 0; index < level.Platforms.Count; index++)
        {
            var platform = level.Platforms[index];
            if (platform.ActivatesLight)
            {
                var polygon = level.Polygons[platform.PolygonIndex];
                activatedLights.Add(polygon.FloorLight);
            }
        }

        for (short index = 0; index < level.Sides.Count; index++)
        {
            var side = level.Sides[index];
            if (side.IsControlPanel && side.IsLightSwitch())
            {
                activatedLights.Add(side.ControlPanelPermutation);
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
                        ceilingHeight = platform.MinimumHeight;
                    }
                    else if (platform.MinimumHeight < p.CeilingHeight)
                    {
                        ceilingHeight = platform.MinimumHeight;
                    }
                }
                if (platform.ComesFromFloor)
                {
                    if (platform.InitiallyExtended)
                    {
                        floorHeight = platform.MaximumHeight;
                    }
                    else if (platform.MaximumHeight > p.FloorHeight)
                    {
                        floorHeight = platform.MaximumHeight;
                    }
                }
            }

            var lines = new List<UdbLine>();

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

                UdbTexture? upper = null;
                UdbTexture? middle = null;
                UdbTexture? lower = null;

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
                            var platformOffset = adjacentPlatform != null ? ConvertUnit(adjacentPlatform.MaximumHeight) - ConvertUnit(adjacentPlatform.MinimumHeight) : 0;
                            var yOffset = ConvertUnit(p.CeilingHeight) - ConvertUnit(adjacentPolygon.CeilingHeight) + platformOffset;
                            upper = GetUdbTexture(side.Primary, side.PrimaryLightsourceIndex, side.PrimaryTransferMode, yOffset);
                        }
                        else if (side.Type == SideType.Low)
                        {
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
                    IsSolid = adjacentPolygon != null && line.Solid
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

allSectorLines = [];

const drawSector = async (sector, debug = false) => {{
  UDB.Map.drawLines([new UDB.Vector2D(sector.lines[0].start.x, sector.lines[0].start.y), ...sector.lines.map((l) => new UDB.Vector2D(l.end.x, l.end.y))]);

  let sectors = UDB.Map.getMarkedSectors();
  for (let s of sectors) {{
    if (sector.platform) {{
      s.addTag(s.index);
    }} else {{
      s.tag = null;
    }}
    if (sector.lightSectorTagId) {{
      s.addTag(sector.lightSectorTagId);
    }}
    s.fields.rotationfloor = 90;
    s.fields.rotationceiling = 90;
    // lightceilingabsolute = true;
    // lightceiling = 150;
    // lightfloor = 32;
    s.floorHeight = sector.floorHeight;
    s.ceilingHeight = sector.ceilingHeight;
    s.floorTexture = sector.floorTexture.sky ? ""F_SKY1"" : sector.floorTexture.name;
    s.ceilingTexture = sector.ceilingTexture.sky ? ""F_SKY1"" : sector.ceilingTexture.name;
    s.brightness = getBrightness(sector.floorTexture.lightIndex);
    if (sector.ceilingTexture.lightIndex !== sector.floorTexture.lightIndex) {{
      s.fields.lightceilingabsolute = true;
      s.fields.lightceiling = getBrightness(sector.ceilingTexture.lightIndex);
    }} else {{
      s.fields.lightceilingabsolute = false;
      s.fields.lightceiling = 0;
    }}

    for (let l of s.getSidedefs()) {{
      const sectorLine = sector.lines.find((x) => {{
        // UDB.showMessage(`[${{x.start.x}}, ${{x.start.y}}] - [${{x.end.x}}, ${{x.end.y}}], [${{l.line.line.v1.x}}, ${{l.line.line.v1.y}}] - [${{l.line.line.v2.x}}, ${{l.line.line.v2.y}}]`);
        return lineMatch([x.start, x.end], [l.line.line.v1, l.line.line.v2]);
      }});
      if (!sectorLine) {{
        continue;
      }}

      allSectorLines.push({{ sideIndex: l.index, sectorLine: sectorLine, sector }});
    }}
  }}
  count++;
  if (count % 50 === 0) {{
    await new Promise((resolve) => setTimeout(resolve, 1000));
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

const level = {JsonSerializer.Serialize(map, options)};

const create = async () => {{
  for (const sector of level.sectors) {{
    drawSector(sector);
  }}

  for (const thing of level.things) {{
    {{
      addThing(thing);
    }}
  }}

  applySideTextures();
}};

create().then();
");
        }

        using var acsWriter = new StreamWriter(scriptPath);

        acsWriter.WriteLine($@"#include ""zcommon.acs""

Script ""InitialiseLighting"" ENTER
{{");
        foreach (var i in initialisedLights)
        {
            var light = level.Lights[i];
            if(light.InitiallyActive)
            {
                acsWriter.WriteLine($@" ACS_NamedExecute(""Light{i}Active"", 0);");
            }
            else
            {
                acsWriter.WriteLine($@" ACS_NamedExecute(""Light{i}Inactive"", 0);");
            }
        }
        acsWriter.WriteLine($@"}}
");
        for (var i = 0; i < level.Lights.Count; i++)
        {
            var light = level.Lights[i];
            var tagId = 500 + i;
            acsWriter.WriteLine($@"

//----------- Light {i} -------------
bool light{i}On = {light.InitiallyActive.ToString().ToLower()};

script ""Light{i}Switch"" (int tagId, int soundTagId)
{{
	ACS_NamedTerminate(""Light{i}Active"", 0);
	ACS_NamedTerminate(""Light{i}Inactive"", 0);
	ACS_NamedTerminate(""Light{i}Change"", 0);
	Delay(1);
	light{i}On = !light{i}On;
	toggleSwitch(tagId, soundTagId, light{i}On);

	if(light{i}On)
	{{
		ACS_NamedExecute(""Light{i}Change"", 0, 1);
	}}
	else
	{{
		ACS_NamedExecute(""Light{i}Change"", 0, 0);
	}}
}}

script ""Light{i}Active"" (void)
{{
	int period;
	int lightVal;

	int ticks = 0;
	int initial = GetSectorLightLevel({tagId});
	while(true)
	{{
		{FormatLightFn(tagId, light.PrimaryActive)}
		{FormatLightFn(tagId, light.SecondaryActive)}
	}}
}}

script ""Light{i}Inactive"" (void)
{{
	int period;
	int lightVal;

	int ticks = 0;
	int initial = GetSectorLightLevel({tagId});
	while(true)
	{{
		{FormatLightFn(tagId, light.PrimaryInactive)}
		{FormatLightFn(tagId, light.SecondaryInactive)}
	}}
}}

script ""Light{i}Change"" (int active)
{{
	int period;
	int lightVal;

	int ticks = 0;
	int initial = GetSectorLightLevel({tagId});
	if(active == 1)
	{{
		{FormatLightFn(tagId, light.BecomingActive)}
		ACS_NamedExecute(""Light{i}Active"", 0, 0);
	}}
	else
	{{
		{FormatLightFn(tagId, light.BecomingInactive)}
		ACS_NamedExecute(""Light{i}Inactive"", 0, 0);
	}}
}}

");
        }

        acsWriter.WriteLine($@"
function void toggleSwitch(int tagId, int soundTagId, bool active) {{
	if(active){{
		SetLineTexture(tagId, SIDE_FRONT, TEXTURE_MIDDLE, ""SWON"");
		PlaySound(soundTagId, ""MSWON"");
	}}
	else {{
		SetLineTexture(tagId, SIDE_FRONT, TEXTURE_MIDDLE, ""SWOFF"");
		PlaySound(soundTagId, ""MSWOFF"");
	}}
}}

function int lightFn(int fn, int phase, int intensity, int intensityDelta, int currentIntensity, int ticks)
{{
	int v;
	switch(fn)
	{{
		case 0:
			v =  intensity * 100;
			//Print(s:""Constant"", d:v);
			break;
		case 1:
			int linearFrac = (intensity - currentIntensity) * 100 / phase;
			v = (currentIntensity * 100) + (linearFrac * ticks);
			if(ticks > phase){{
				v = intensity * 100;
			}}
			//Print(s:""Fade"", d:currentIntensity, s:"" -> "", d:intensity, s:"" - "", d:frac);
			break;
		case 2:
			int smoothFrac = (intensity - currentIntensity) * 100 / phase;
			v = (currentIntensity * 100) + (smoothFrac * ticks);
			if(ticks > phase){{
				v = intensity * 100;
			}}
			//Print(s:""Smooth"", d:currentIntensity, s:"" -> "", d:intensity, s:"" - "", d:frac);
			break;
		case 3:
			int d = Random(intensityDelta * -100.0, intensityDelta * 100.0);
			Print(f:d);
			v = (intensity * 100) + d;
			if(v > 25500)
			{{
				v = 25500;
			}}
			else if(v < 15000)
			{{
				v = 15000;
			}}
			//Print(s:""Smooth"", d:currentIntensity, s:"" -> "", d:intensity, s:"" - "", d:frac);
			break;
		default:
			break;
	}}
	return v / 100;
}}

function int abs (int x)
{{
    if (x < 0)
        return -x;

    return x;
}}");
    }

    private string FormatLightFn(int tagId, Light.Function fn)
    {
        return $@"ticks = 0;
        period = {FormatTicks(fn.Period)}{(fn.DeltaPeriod > 0 ? $" + Random(-{FormatTicks(fn.DeltaPeriod)}, {FormatTicks(fn.DeltaPeriod)})" : string.Empty)};
		while(ticks <= period)
		{{
			lightVal = lightFn({GetLightFunction(fn)}, period, {FormatIntensity(fn.Intensity)}, {FormatIntensity(fn.DeltaIntensity, false)}, initial, ticks);
			Light_ChangeToValue({tagId}, lightVal);
			Delay(1);
			ticks++;
		}}
";
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
        return new UdbTexture { Name = FormatTextureName(texture.Value.Texture), X = ConvertUnit(texture.Value.X), Y = ConvertUnit(texture.Value.Y) + (yOffset ?? 0), Sky = GetSky(transferMode), LightIndex = lightSourceIndex };
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
        return (transferMode == 9);
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
            IsMonsterControl = (platform.IsMonsterControllable),
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

        return $"{(env)}SET{texId.ToString("00")}";
    }

    private short FormatIntensity(double intensity, bool includeMin = true)
    {
        return (short)((intensity * 155) + (includeMin ? 100 : 0));
    }

    private short FormatTicks(double marathonTicks)
    {
        return (short)((double)marathonTicks / 30 * 35);
    }


    private double ConvertUnit(short val)
    {
        return Math.Round(World.ToDouble(val) * Scale, 3);
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
}

public class UdbLine
{
    public required UdbVector Start { get; set; }
    public required UdbVector End { get; set; }
    public UdbTexture? Upper { get; set; }
    public UdbTexture? Middle { get; set; }
    public UdbTexture? Lower { get; set; }
    public bool IsSolid { get; set; }
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
}