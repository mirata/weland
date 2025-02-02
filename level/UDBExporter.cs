using System;
using System.IO;
using System.Collections.Generic;
using Pango;
using static Weland.Side;
using System.Xml.Linq;
using Gtk;
using Weland;

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
  UDB.Map.drawLines([sector.lines[0].start, ...sector.lines.map((l) => l.end)]);

  let sectors = UDB.Map.getMarkedSectors();
  for (let s of sectors) {{
    s.fields.rotationfloor = 90;
    s.fields.rotationceiling = 90;
    // lightceilingabsolute = true;
    // lightceiling = 150;
    // lightfloor = 32;
    s.floorHeight = sector.floorHeight;
    s.ceilingHeight = sector.ceilingHeight;
    s.floorTexture = sector.floorTexture.sky ? 'F_SKY1' : sector.floorTexture.name;
    s.ceilingTexture = sector.ceilingTexture.sky ? 'F_SKY1' : sector.ceilingTexture.name;
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

      allSectorLines.push({{sideIndex: l.index, sectorLine: sectorLine }});
    }}
  }}
  count++;
  if (count % 50 === 0) {{
    UDB.showMessageYesNo(`${{count}}`);
  }}
}};

");

                var index = 0;
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

                    var lines = new List<UDBLine>();

                    for (var i = 0; i < p.VertexCount; ++i)
                    {
                        var pointStart = level.Endpoints[p.EndpointIndexes[i]];
                        var pointEnd = level.Endpoints[p.EndpointIndexes[i == p.VertexCount - 1 ? 0 : i + 1]];
                        var line = level.Lines[p.LineIndexes[i]];
                        var side = p.SideIndexes[i] > -1 && p.SideIndexes[i] < level.Sides.Count ? level.Sides[p.SideIndexes[i]] : null;
                        var pIndex = p.AdjacentPolygonIndexes[i];

                        var adjacentPolygon = pIndex > -1 && pIndex < level.Polygons.Count ? level.Polygons[pIndex] : null;
                        var adjacentPlatform = level.Platforms.FirstOrDefault(e => e.PolygonIndex == pIndex);

                        UDBTexture? upper = null;
                        UDBTexture? middle = null;
                        UDBTexture? lower = null;

                        if (side != null)
                        {
                            if (adjacentPolygon != null)
                            {
                                if (adjacentPolygon.CeilingHeight < p.CeilingHeight && adjacentPolygon.FloorHeight > p.FloorHeight)
                                {
                                    var yOffset = ConvertUnit(p.CeilingHeight) - ConvertUnit(adjacentPolygon.CeilingHeight);
                                    upper = ConvertTexture(side.Primary, side.PrimaryTransferMode, yOffset);
                                    lower = ConvertTexture(side.Secondary, side.SecondaryTransferMode);
                                }
                                else if (adjacentPolygon.CeilingHeight < p.CeilingHeight || (adjacentPlatform?.ComesFromCeiling ?? false))
                                {
                                    var yOffset = ConvertUnit(p.CeilingHeight) - ConvertUnit(adjacentPolygon.CeilingHeight);
                                    upper = ConvertTexture(side.Primary, side.PrimaryTransferMode, yOffset);
                                }
                                else if (adjacentPolygon.FloorHeight > p.FloorHeight || (adjacentPlatform?.ComesFromFloor ?? false))
                                {
                                    lower = ConvertTexture(side.Primary, side.PrimaryTransferMode);
                                }
                            }
                            else
                            {
                                middle = ConvertTexture(side.Primary, side.PrimaryTransferMode);
                            }
                        }
                        else
                        {
                            middle = new UDBTexture { Name = "F_SKY1", X = 0, Y = 0, Sky = true };
                        }

                        var start = level.Endpoints[line.EndpointIndexes[0]];
                        var end = level.Endpoints[line.EndpointIndexes[1]];

                        lines.Add(new UDBLine
                        {
                            Start = new UDBVector
                            {
                                X = ConvertUnit(pointStart.X),
                                Y = -ConvertUnit(pointStart.Y)
                            },
                            End = new UDBVector
                            {
                                X = ConvertUnit(pointEnd.X),
                                Y = -ConvertUnit(pointEnd.Y)
                            },
                            Upper = upper,
                            Middle = middle,
                            Lower = lower,
                            AdjacentPlatform = FormatAdjacentPlatform(adjacentPlatform),
                        });
                    }

                    w.WriteLine($@"drawSector({{
  index: {index},
  floorHeight: {ConvertUnit(floorHeight)},
  ceilingHeight: {ConvertUnit(ceilingHeight)},
  floorTexture: {{ name: '{FormatTextureName(p.FloorTexture)}', offset: [{ConvertUnit(p.FloorOrigin.X)}, {ConvertUnit(p.FloorOrigin.Y)}], sky: {FormatBool(GetSky(p.FloorTransferMode))} }},
  ceilingTexture: {{ name: '{FormatTextureName(p.CeilingTexture)}', offset: [{ConvertUnit(p.CeilingOrigin.X)}, {ConvertUnit(p.CeilingOrigin.Y)}], sky: {FormatBool(GetSky(p.CeilingTransferMode))} }},
  floorBrightness: {FormatBrightness(p.FloorLight)},
  ceilingBrightness: {FormatBrightness(p.CeilingLight)},
  polygon: {FormatPlatform(platform)},
  lines: [
{string.Join(string.Empty, lines.Select(e => $@"    {{
      start: new UDB.Vector2D({e.Start.X}, {e.Start.Y}),
      end: new UDB.Vector2D({e.End.X}, {e.End.Y}),
      upperTexture: {FormatTexture(e.Upper)},
      middleTexture: {FormatTexture(e.Middle)},
      lowerTexture: {FormatTexture(e.Lower)},
    }},
"))}
  ],
}});

");
                    index++;
                }

                w.WriteLine($@"
for (const sl of allSectorLines) {{
  const line = UDB.Map.getSidedefs()[sl.sideIndex];
  if (sl.sectorLine.upperTexture) {{
    if(sl.sectorLine.upperTexture.sky){{
      line.upperTexture = '-';
    }} else {{
      line.upperTexture = sl.sectorLine.upperTexture.name;
      line.fields.offsetx_top = sl.sectorLine.upperTexture.offset[0];
      line.fields.offsety_top = sl.sectorLine.upperTexture.offset[1];
    }}
  }}
  if (sl.sectorLine.middleTexture) {{
    if(sl.sectorLine.middleTexture.sky){{
      line.middleTexture = '-';
    }} else {{
      line.middleTexture = sl.sectorLine.middleTexture.name;
      line.fields.offsetx_mid = sl.sectorLine.middleTexture.offset[0];
      line.fields.offsety_mid = sl.sectorLine.middleTexture.offset[1];
    }}
  }}
  if (sl.sectorLine.lowerTexture) {{
    if(sl.sectorLine.lowerTexture.sky){{
      line.lowerTexture = '-';
    }} else {{
      line.lowerTexture = sl.sectorLine.lowerTexture.name;
      line.fields.offsetx_bottom = sl.sectorLine.lowerTexture.offset[0];
      line.fields.offsety_bottom = sl.sectorLine.lowerTexture.offset[1];
    }}
  }}
}}

");
            }
        }

        public UDBTexture? ConvertTexture(TextureDefinition? texture, short transferMode, double? yOffset = null)
        {
            if (texture == null)
            {
                return null;
            }
            return new UDBTexture { Name = FormatTextureName(texture.Value.Texture), X = ConvertUnit(texture.Value.X), Y = ConvertUnit(texture.Value.Y) + (yOffset ?? 0), Sky = GetSky(transferMode) };
        }

        private bool GetSky(short transferMode)
        {
            return (transferMode == 9);
        }

        public string FormatPlatform(Platform? platform)
        {
            if (platform == null)
            {
                return "null";
            }
            return $@"{{ maxHeight: {ConvertUnit(platform.MaximumHeight)}, minHeight: '{ConvertUnit(platform.MinimumHeight)}', speed: {platform.Speed}, delay: {platform.Delay}, isDoor: {FormatBool(platform.IsDoor)}, ceiling: {FormatBool(platform.ComesFromCeiling)}, floor: {FormatBool(platform.ComesFromFloor)}, isExtended: {FormatBool(platform.InitiallyExtended)}, isLocked: {FormatBool(platform.IsLocked)}, isDoor: {FormatBool(platform.IsDoor)} isPlayerControl: {FormatBool(platform.IsPlayerControllable)}, isMonsterControl: {FormatBool(platform.IsMonsterControllable)} }}";
        }

        public string FormatBool(bool value)
        {
            return value.ToString().ToLower();
        }

        public UDBAdjacentPlatform? FormatAdjacentPlatform(Platform? platform)
        {
            if (platform == null)
            {
                return null;
            }
            return new UDBAdjacentPlatform { Ceiling = platform.ComesFromCeiling, Floor = platform.ComesFromFloor };
        }

        public string FormatTexture(UDBTexture? texture)
        {
            if (texture == null)
            {
                return "null";
            }
            return $@"{{ name: '{texture.Name}', offset: [{texture.X}, {texture.Y}], sky: {FormatBool(texture.Sky)} }}";
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

        public short FormatBrightness(short lightId)
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

public class UDBLine
{
    public required UDBVector Start { get; set; }
    public required UDBVector End { get; set; }
    public UDBTexture? Upper { get; set; }
    public UDBTexture? Middle { get; set; }
    public UDBTexture? Lower { get; set; }
    public UDBAdjacentPlatform? AdjacentPlatform { get; set; }
}

public class UDBAdjacentPlatform
{
    public bool Ceiling { get; set; }
    public bool Floor { get; set; }
}

public class UDBVector
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class UDBTexture
{
    public required string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool Sky { get; set; }
}