using System;
using System.IO;
using System.Collections.Generic;
using Pango;
using static Weland.Side;

namespace Weland
{
    public class UDBExporter
    {
        const double Scale = 64.0;

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

const drawSector = (sector, debug = false) => {{
  UDB.Map.drawLines([sector.lines[0].start, ...sector.lines.map((l) => l.end)]);

  let sectors = UDB.Map.getMarkedSectors();
  for (let s of sectors) {{
    s.floorHeight = sector.floorHeight;
    s.ceilingHeight = sector.ceilingHeight;
    s.floorTexture = sector.floorTexture;
    s.ceilingTexture = sector.ceilingTexture;
    s.brightness = sector.brightness;

    for (let l of s.getSidedefs()) {{
      const sectorLine = sector.lines.find((x) => lineMatch([x.start, x.end], [l.line.line.v1, l.line.line.v2]));
      if (!sectorLine) {{
        l.middleTexture = ""-"";
        continue;
      }}

      l.upperTexture = sectorLine.upperTexture?.name ?? ""-"";
      l.middleTexture = sectorLine.middleTexture?.name ?? ""-"";
      l.lowerTexture = sectorLine.lowerTexture?.name ?? ""-"";
    }}
  }}
}};

");
                foreach (Polygon p in level.Polygons)
                {
                    w.WriteLine("UDB.Map.drawLines([");
                    for (int i = 0; i < p.VertexCount; ++i)
                    {
                        var point = level.Endpoints[p.EndpointIndexes[i]];
                        w.WriteLine($"\tnew UDB.Vector2D({ConvertPoint(point.X)}, {-ConvertPoint(point.Y)}),");
                    }
                    var point0 = level.Endpoints[p.EndpointIndexes[0]];
                    w.WriteLine($"\tnew UDB.Vector2D({ConvertPoint(point0.X)}, {-ConvertPoint(point0.Y)}),");
                    w.WriteLine("]);\n");
                    var lines = new List<UDBLine>();

                    for (var i = 0; i < p.VertexCount; ++i)
                    {
                        var line = level.Lines[p.LineIndexes[i]];
                        var side = p.SideIndexes[i] > -1 ? level.Sides[p.SideIndexes[i]] : null;
                        Polygon? adjacentPolygon = null;
                        try
                        {
                            adjacentPolygon = p.AdjacentPolygonIndexes[i] > -1 ? level.Polygons[p.AdjacentPolygonIndexes[i]] : null;
                        }
                        catch (Exception) { }

                        TextureDefinition? upper = null;
                        TextureDefinition? middle = null;
                        TextureDefinition? lower = null;

                        if (side != null)
                        {
                            if (adjacentPolygon != null)
                            {
                                if (adjacentPolygon.CeilingHeight < p.CeilingHeight && adjacentPolygon.FloorHeight > p.FloorHeight)
                                {
                                    upper = side.Primary;
                                    lower = side.Secondary;
                                }
                                else if (adjacentPolygon.CeilingHeight < p.CeilingHeight)
                                {
                                    upper = side.Primary;
                                }
                                else if (adjacentPolygon.FloorHeight > p.FloorHeight)
                                {
                                    lower = side.Primary;
                                }
                            }
                            else
                            {
                                middle = side.Primary;
                            }
                        }

                        var start = level.Endpoints[line.EndpointIndexes[0]];
                        var end = level.Endpoints[line.EndpointIndexes[1]];

                        lines.Add(new UDBLine
                        {
                            Start = new UDBVector
                            {
                                X = ConvertPoint(start.X),
                                Y = -ConvertPoint(start.Y)
                            },
                            End = new UDBVector
                            {
                                X = ConvertPoint(end.X),
                                Y = -ConvertPoint(end.Y)
                            },
                            Upper = upper,
                            Middle = middle,
                            Lower = lower
                        });
                    }

                    w.WriteLine($@"drawSector({{
  floorHeight: {ConvertPoint(p.FloorHeight)},
  ceilingHeight: {ConvertPoint(p.CeilingHeight)},
  floorTexture: '{FormatTexture(p.FloorTexture)}',
  ceilingTexture: '{FormatTexture(p.CeilingTexture)}',
  brightness: {FormatBrightness(p.FloorLight)},
  lines: [
{string.Join(string.Empty, lines.Select(e => $@"    {{
      start: new UDB.Vector2D({e.Start.X}, {e.Start.Y}),
      end: new UDB.Vector2D(${e.End.X}, {e.End.Y}),
      upperTexture: {ConvertTexture(e.Upper)},
      middleTexture: {ConvertTexture(e.Middle)},
      lowerTexture: {ConvertTexture(e.Lower)},
    }},
"))}
  ],
}});

");
                }
            }
        }

        public string ConvertTexture(TextureDefinition? texture)
        {
            if (texture == null)
            {
                return "null";
            }
            return $@"{{ name: '{FormatTexture(texture.Value.Texture)}', offset: [0, 0] }}";
        }

        private string FormatTexture(ShapeDescriptor shapeDescriptor)
        {
            var env = level.Environment + 1;
            var texId = shapeDescriptor.Bitmap;
            if (env == 3 && texId > 27)
            {
                texId -= 2;
            }
            else if (env == 3 && texId > 13)
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

        private double ConvertPoint(short val)
        {
            return World.ToDouble(val) * Scale;
        }
    }
}

class UDBLine
{
    public required UDBVector Start { get; set; }
    public required UDBVector End { get; set; }
    public TextureDefinition? Upper { get; set; }
    public TextureDefinition? Middle { get; set; }
    public TextureDefinition? Lower { get; set; }
}

class UDBVector
{
    public double X { get; set; }
    public double Y { get; set; }
}