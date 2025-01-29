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

allSectorLines = [];

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
        continue;
      }}

      allSectorLines.push({{sideIndex: l.index, sectorLine: sectorLine }});
    }}
  }}
}};

");

                var index = 0;
                foreach (Polygon p in level.Polygons)
                {
                    var lines = new List<UDBLine>();

                    for (var i = 0; i < p.VertexCount; ++i)
                    {
                        var pointStart = level.Endpoints[p.EndpointIndexes[i]];
                        var pointEnd = level.Endpoints[p.EndpointIndexes[i == p.VertexCount - 1 ? 0 : i + 1]];
                        var line = level.Lines[p.LineIndexes[i]];
                        var side = p.SideIndexes[i] > -1 && p.SideIndexes[i] < level.Sides.Count ? level.Sides[p.SideIndexes[i]] : null;
                        var pIndex = p.AdjacentPolygonIndexes[i];

                        var adjacentPolygon = pIndex > -1 && pIndex < level.Polygons.Count ? level.Polygons[pIndex] : null;

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
                                X = ConvertPoint(pointStart.X),
                                Y = -ConvertPoint(pointStart.Y)
                            },
                            End = new UDBVector
                            {
                                X = ConvertPoint(pointEnd.X),
                                Y = -ConvertPoint(pointEnd.Y)
                            },
                            Upper = upper,
                            Middle = middle,
                            Lower = lower
                        });
                    }

                    w.WriteLine($@"drawSector({{
  index: {index},
  floorHeight: {ConvertPoint(p.FloorHeight)},
  ceilingHeight: {ConvertPoint(p.CeilingHeight)},
  floorTexture: '{FormatTexture(p.FloorTexture)}',
  ceilingTexture: '{FormatTexture(p.CeilingTexture)}',
  brightness: {FormatBrightness(p.FloorLight)},
  lines: [
{string.Join(string.Empty, lines.Select(e => $@"    {{
      start: new UDB.Vector2D({e.Start.X}, {e.Start.Y}),
      end: new UDB.Vector2D({e.End.X}, {e.End.Y}),
      upperTexture: {ConvertTexture(e.Upper)},
      middleTexture: {ConvertTexture(e.Middle)},
      lowerTexture: {ConvertTexture(e.Lower)},
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
    line.upperTexture = sl.sectorLine.upperTexture.name;
  }}
  if (sl.sectorLine.middleTexture) {{
    line.middleTexture = sl.sectorLine.middleTexture.name;
  }}
  if (sl.sectorLine.lowerTexture) {{
    line.lowerTexture = sl.sectorLine.lowerTexture.name;
  }}
}}");
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
            return Math.Round(World.ToDouble(val) * Scale, 3);
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