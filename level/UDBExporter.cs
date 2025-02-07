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

namespace Weland
{
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

        Level level;

        public UDBExporter(Level level)
        {
            this.level = level;
        }

        public void Export(string path)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            var map = new UdbLevel();

            short index = 0;
            foreach (Polygon p in level.Polygons)
            {
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
                    var side = p.SideIndexes[i] > -1 && p.SideIndexes[i] < level.Sides.Count ? level.Sides[p.SideIndexes[i]] : null;
                    var pIndex = p.AdjacentPolygonIndexes[i];

                    var adjacentPolygon = pIndex > -1 && pIndex < level.Polygons.Count ? level.Polygons[pIndex] : null;
                    var adjacentPlatform = level.Platforms.FirstOrDefault(e => e.PolygonIndex == pIndex);

                    UdbTexture? upper = null;
                    UdbTexture? middle = null;
                    UdbTexture? lower = null;

                    if (side != null)
                    {
                        if (adjacentPolygon != null)
                        {
                            if (adjacentPolygon.CeilingHeight < p.CeilingHeight && adjacentPolygon.FloorHeight > p.FloorHeight)
                            {
                                var yOffset = ConvertUnit(p.CeilingHeight) - ConvertUnit(adjacentPolygon.CeilingHeight);
                                upper = GetUdbTexture(side.Primary, side.PrimaryTransferMode, yOffset);
                                lower = GetUdbTexture(side.Secondary, side.SecondaryTransferMode);
                            }
                            else if (adjacentPolygon.CeilingHeight < p.CeilingHeight || (adjacentPlatform?.ComesFromCeiling ?? false))
                            {
                                var yOffset = ConvertUnit(p.CeilingHeight) - ConvertUnit(adjacentPolygon.CeilingHeight);
                                upper = GetUdbTexture(side.Primary, side.PrimaryTransferMode, yOffset);
                            }
                            else if (adjacentPolygon.FloorHeight > p.FloorHeight || (adjacentPlatform?.ComesFromFloor ?? false))
                            {
                                lower = GetUdbTexture(side.Primary, side.PrimaryTransferMode);
                            }
                        }
                        else
                        {
                            middle = GetUdbTexture(side.Primary, side.PrimaryTransferMode);
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
                        AdjacentPlatform = GetUdbAdjacentPlatform(adjacentPlatform),
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
                    FloorTexture = new UdbTexture { Name = FormatTextureName(p.FloorTexture), X = ConvertUnit(p.FloorOrigin.X), Y = ConvertUnit(p.FloorOrigin.Y), Sky = GetSky(p.FloorTransferMode) },
                    CeilingTexture = new UdbTexture { Name = FormatTextureName(p.CeilingTexture), X = ConvertUnit(p.CeilingOrigin.X), Y = ConvertUnit(p.CeilingOrigin.Y), Sky = GetSky(p.CeilingTransferMode) },
                    FloorBrightness = FormatBrightness(p.FloorLight),
                    CeilingBrightness = FormatBrightness(p.CeilingLight),
                    Platform = GetUdbPlatform(platform),
                    Lines = lines
                };

                map.Sectors.Add(sector);

                index++;
            }

            foreach (var obj in level.Objects)
            {
                var thing = GetUdbThing(obj);
                if (thing != null)
                {
                    map.Things.Add(thing);
                }
            }

            using (TextWriter w = new StreamWriter(path))
            {
                w.WriteLine($@"/// <reference path=""../udbscript.d.ts"" />
`#version 4`;

`#name {level.Name}`;

let count = 0;

const lineMatch = (line1, line2) => {{
  const isCollinear = (p1, p2, p3) => {{
    // UDB.showMessage(`${{p1.x}}, ${{p1.y}}, ${{p2.x}}, ${{p2.y}}, ${{p3.x}}, ${{p3.y}}`);
    return (p3.y - p1.y) * (p2.x - p1.x) === (p2.y - p1.y) * (p3.x - p1.x);
  }};

  const isWithinBounds = (p, p1, p2) => {{
    return Math.min(p1.x, p2.x) <= p.x && p.x <= Math.max(p1.x, p2.x) && Math.min(p1.y, p2.y) <= p.y && p.y <= Math.max(p1.y, p2.y);
  }};

  const valid = isCollinear(line1[0], line1[1], line2[0]) && isCollinear(line1[0], line1[1], line2[1]) && isWithinBounds(line2[0], line1[0], line1[1]) && isWithinBounds(line2[1], line1[0], line1[1]);
  return valid;
}};

allSectorLines = [];

const drawSector = (sector, debug = false) => {{
  UDB.Map.drawLines([new UDB.Vector2D(sector.lines[0].start.x, sector.lines[0].start.y), ...sector.lines.map((l) => new UDB.Vector2D(l.end.x, l.end.y))]);

  let sectors = UDB.Map.getMarkedSectors();
  for (let s of sectors) {{
    if (sector.platform) {{
      s.addTag(s.index);
    }} else {{
      s.tag = null;
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
    s.brightness = sector.floorBrightness;
    if (sector.ceilingBrightness !== sector.floorBrightness) {{
      s.fields.lightceilingabsolute = true;
      s.fields.lightceiling = sector.ceilingBrightness;
    }} else {{
      s.fields.lightceilingabsolute = false;
      s.fields.lightceiling = 0;
    }}

    for (let l of s.getSidedefs()) {{
      const sectorLine = sector.lines.find((x) => lineMatch([x.start, x.end], [l.line.line.v1, l.line.line.v2]));
      if (!sectorLine) {{
        continue;
      }}

      allSectorLines.push({{ sideIndex: l.index, sectorLine: sectorLine }});
    }}
  }}
  count++;
  if (count % 50 === 0) {{
    UDB.showMessageYesNo(`${{count}}`);
  }}
}};

const addThing = (thing) => {{
  const t = UDB.Map.createThing(new UDB.Vector3D(thing.x, thing.y, thing.z), thing.type);
  t.angle = thing.angle;
}};

const applySideTextures = () => {{
  for (const sl of allSectorLines) {{
    const line = UDB.Map.getSidedefs()[sl.sideIndex];
    if (sl.sectorLine.upper) {{
      if (sl.sectorLine.upper.sky) {{
        line.upperTexture = ""-"";
      }} else {{
        line.upperTexture = sl.sectorLine.upper.name;
        line.fields.offsetx_top = sl.sectorLine.upper.x;
        line.fields.offsety_top = sl.sectorLine.upper.y;
      }}
    }}
    if (sl.sectorLine.middle) {{
      if (sl.sectorLine.middle.sky) {{
        line.middleTexture = ""-"";
      }} else {{
        line.middleTexture = sl.sectorLine.middle.name;
        line.fields.offsetx_mid = sl.sectorLine.middle.x;
        line.fields.offsety_mid = sl.sectorLine.middle.y;
      }}
    }}
    if (sl.sectorLine.lower) {{
      if (sl.sectorLine.lower.sky) {{
        line.lowerTexture = ""-"";
      }} else {{
        line.lowerTexture = sl.sectorLine.lower.name;
        line.fields.offsetx_bottom = sl.sectorLine.lower.x;
        line.fields.offsety_bottom = sl.sectorLine.lower.y;
      }}
    }}
  }}
}};

const level = {JsonSerializer.Serialize(map, options)};

for (const sector of level.sectors) {{
  drawSector(sector);
}}

for (const thing of level.things) {{
  {{
    addThing(thing);
  }}
}}

applySideTextures();
");
            }
        }

        private UdbThing? GetUdbThing(MapObject obj)
        {
            var type = GetThingType(obj);
            if (type == null)
            {
                return null;
            }

            return new UdbThing
            {
                Type = type.Value,
                X = ConvertUnit(obj.X),
                Y = -ConvertUnit(obj.Y),
                Z = ConvertUnit(obj.Z),
                Angle = 360.0 - obj.Facing
            };
        }

        private int? GetThingType(MapObject obj)
        {
            if(obj.Type == ObjectType.Player)
            {
                return 1;
            }

            var name = GetThingName(obj);
            switch (name)
            {
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
            }
            return null;
        }

        private UdbTexture? GetUdbTexture(TextureDefinition? texture, short transferMode, double? yOffset = null)
        {
            if (texture == null)
            {
                return null;
            }
            return new UdbTexture { Name = FormatTextureName(texture.Value.Texture), X = ConvertUnit(texture.Value.X), Y = ConvertUnit(texture.Value.Y) + (yOffset ?? 0), Sky = GetSky(transferMode) };
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

        private UdbAdjacentPlatform? GetUdbAdjacentPlatform(Platform? platform)
        {
            if (platform == null)
            {
                return null;
            }
            return new UdbAdjacentPlatform { IsCeiling = platform.ComesFromCeiling, IsFloor = platform.ComesFromFloor };
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

        private short FormatBrightness(short lightId)
        {
            var intensity = level.Lights[lightId].PrimaryInactive.Intensity;
            return (short)((intensity * 155) + 100);
        }

        private double ConvertUnit(short val)
        {
            return Math.Round(World.ToDouble(val) * Scale, 3);
        }
    }
}

public class UdbLevel
{
    public List<UdbSector> Sectors { get; set; } = [];
    public List<UdbThing> Things { get; set; } = [];
}

public class UdbSector
{
    public int Index { get; set; }
    public double FloorHeight { get; set; }
    public double CeilingHeight { get; set; }
    public required UdbTexture FloorTexture { get; set; }
    public required UdbTexture CeilingTexture { get; set; }
    public short FloorBrightness { get; set; }
    public short CeilingBrightness { get; set; }
    public UdbPlatform? Platform { get; set; }
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
    public UdbAdjacentPlatform? AdjacentPlatform { get; set; }
}

public class UdbAdjacentPlatform
{
    public bool IsCeiling { get; set; }
    public bool IsFloor { get; set; }
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
}

public class UdbThing
{
    public int Type { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double Angle { get; set; }
}