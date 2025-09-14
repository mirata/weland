using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Weland;

namespace weland.level;
public class LevelAndAttributes
{
    public Wadfile.DirectoryEntry Wad { get; set; }
    public LevelAttributes Attributes { get; set; }

    public LevelAndAttributes Clone()
    {
        return new LevelAndAttributes
        {
            Wad = Wad.Clone(),
            Attributes = Attributes with { }
        };
    }
}

public record LevelAttributes
{
    public Dictionary<short, List<int>> PolygonLayers { get; set; } = [];
    public Dictionary<short, bool> PortalLines { get; set; } = [];
}
