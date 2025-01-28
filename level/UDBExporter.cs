using System;
using System.IO;
using System.Collections.Generic;
using Pango;

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
");
                var polygonIndex = 0;
                foreach (Polygon p in level.Polygons)
                {
                    if(polygonIndex == 56)
                    {

                    }
                    w.WriteLine("UDB.Map.drawLines([");
                    for (int i = 0; i < p.VertexCount; ++i)
                    {
                        var point = level.Endpoints[p.EndpointIndexes[i]];
                        w.WriteLine($"\tnew UDB.Vector2D({ConvertPoint(point.X)}, {-ConvertPoint(point.Y)}),");
                    }
                    var point0 = level.Endpoints[p.EndpointIndexes[0]];
                    w.WriteLine($"\tnew UDB.Vector2D({ConvertPoint(point0.X)}, {-ConvertPoint(point0.Y)}),");
                    w.WriteLine("]);\n");

                    w.WriteLine($@"var sectors = UDB.Map.getMarkedSectors();
for (let s of sectors) {{
    s.floorHeight = {ConvertPoint(p.FloorHeight)};
    s.ceilingHeight = {ConvertPoint(p.CeilingHeight)};
    s.floorTexture = '{FormatTexture(p.FloorTexture)}';
    s.ceilingTexture = '{FormatTexture(p.CeilingTexture)}';
    s.brightness = {FormatBrightness(p.FloorLight)};
}}
");
                    polygonIndex++;
                }
            }
        }

        private string FormatTexture(ShapeDescriptor shapeDescriptor)
        {
            var env = level.Environment + 1;
            var texId = shapeDescriptor.Bitmap;
            if (env == 3 && texId > 27)
            {
                texId-= 2;
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