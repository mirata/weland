using System;
using System.Collections.Generic;
using System.IO;
using weland;
using weland.level;

namespace Weland;

interface ISerializableBE
{
    void Load(BinaryReaderBE reader);
    void Save(BinaryWriterBE writer);
}

public static class World
{
    public const short One = 1024;
    public const short ZDoom = 16;
    public static short FromDouble(double d)
    {
        return (short)Math.Round(d * World.One);
    }

    public static double ToDouble(short w)
    {
        return (double)w / World.One;
    }

    public static double ToDoom(short w)
    {
        return (double)w / World.ZDoom;
    }

    public static double ToDouble(int i)
    {
        return (double)i / World.One;
    }
}

public static class Angle
{
    const short AngularPrecision = 512;
    public static short FromDouble(double d)
    {
        return (short)Math.Round(d * AngularPrecision / 360);
    }
    public static double ToDouble(short a)
    {
        return (double)a * 360 / AngularPrecision;
    }
}

public partial class Level
{
    public List<Point> Endpoints = [];
    public List<Line> Lines = [];
    public List<Polygon> Polygons = [];
    public List<MapObject> Objects = [];
    public List<Side> Sides = [];
    public List<Platform> Platforms = [];
    public List<Light> Lights = [];
    public Dictionary<uint, byte[]> Chunks = [];
    public List<Placement> ItemPlacement = [];
    public List<Placement> MonsterPlacement = [];
    public List<Annotation> Annotations = [];
    public List<Media> Medias = [];
    public List<AmbientSound> AmbientSounds = [];
    public List<RandomSound> RandomSounds = [];

    MapInfo mapInfo = new MapInfo();

    // stuff for the editor, not saved to file
    public short TemporaryLineStartIndex = -1;
    public Point TemporaryLineEnd;

    public short VisualModePolygonIndex = -1;
    public Point VisualModePoint;

    // for hiding points
    public List<HashSet<Polygon>> EndpointPolygons = [];
    public List<HashSet<Line>> EndpointLines = [];

    List<uint> ChunkFilter = [
	    // saved game / optimized map tags
	    Endpoint.Tag,
    Wadfile.Chunk("plyr"),
    Wadfile.Chunk("dwol"),
    Wadfile.Chunk("mobj"),
    Wadfile.Chunk("door"),
    Wadfile.Chunk("iidx"),
    Wadfile.Chunk("alin"),
    Wadfile.Chunk("apol"),
    Wadfile.Chunk("mOns"),
    Wadfile.Chunk("fx  "),
    Wadfile.Chunk("bang"),
    Platform.DynamicTag,
    Wadfile.Chunk("weap"),
    Wadfile.Chunk("cint"),
    Wadfile.Chunk("slua"),

	    // embedded physics
	    Wadfile.Chunk("MNpx"),
    Wadfile.Chunk("FXpx"),
    Wadfile.Chunk("PRpx"),
    Wadfile.Chunk("RXpx"),
    Wadfile.Chunk("WPpx"),
];

    void LoadChunk(ISerializableBE chunk, byte[] data)
    {
        chunk.Load(new BinaryReaderBE(new MemoryStream(data)));
    }

    byte[] SaveChunk(ISerializableBE chunk)
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriterBE(stream);
        chunk.Save(writer);
        return stream.ToArray();
    }

    public Level()
    {
        // build a Forge-style light list
        for (var i = 0; i <= 20; ++i)
        {
            var light = new Light((double)(20 - i) / 20);
            Lights.Add(light);
        }

        for (var i = 0; i < Placement.Count; ++i)
        {
            ItemPlacement.Add(new Placement());
            MonsterPlacement.Add(new Placement());
        }
    }

    void LoadChunkList<T>(List<T> list, byte[] data) where T : ISerializableBE, new()
    {
        var reader = new BinaryReaderBE(new MemoryStream(data));
        list.Clear();
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var t = new T();
            t.Load(reader);
            list.Add(t);
        }
    }

    byte[] SaveChunk<T>(List<T> list) where T : ISerializableBE, new()
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriterBE(stream);
        foreach (var t in list)
        {
            t.Save(writer);
        }

        return stream.ToArray();
    }

    public void Load(Wadfile.DirectoryEntry wad)
    {
        Chunks = wad.Chunks;

        if (wad.Chunks.ContainsKey(MapInfo.Tag))
        {
            LoadChunk(mapInfo, wad.Chunks[MapInfo.Tag]);
        }
        else
        {
            throw new Wadfile.BadMapException("Incomplete level: missing map info chunk");
        }

        if (wad.Chunks.ContainsKey(Point.Tag))
        {
            LoadChunkList<Point>(Endpoints, wad.Chunks[Point.Tag]);
        }
        else if (wad.Chunks.ContainsKey(Endpoint.Tag))
        {
            Endpoints.Clear();
            List<Endpoint> endpointList = [];
            LoadChunkList<Endpoint>(endpointList, wad.Chunks[Endpoint.Tag]);
            foreach (var e in endpointList)
            {
                Endpoints.Add(e.Vertex);
            }
        }
        else
        {
            throw new Wadfile.BadMapException("Incomplete level: missing points chunk");
        }

        if (wad.Chunks.ContainsKey(Line.Tag))
        {
            LoadChunkList<Line>(Lines, wad.Chunks[Line.Tag]);
        }
        else
        {
            throw new Wadfile.BadMapException("Incomplete level: missing lines chunk");
        }

        if (wad.Chunks.ContainsKey(Polygon.Tag))
        {
            LoadChunkList<Polygon>(Polygons, wad.Chunks[Polygon.Tag]);
        }
        else
        {
            throw new Wadfile.BadMapException("Incomplete level: missing polygons chunk");
        }

        if (wad.Chunks.ContainsKey(Side.Tag))
        {
            LoadChunkList<Side>(Sides, wad.Chunks[Side.Tag]);
        }

        if (wad.Chunks.ContainsKey(Platform.StaticTag))
        {
            LoadChunkList<Platform>(Platforms, wad.Chunks[Platform.StaticTag]);
        }
        else if (wad.Chunks.ContainsKey(Platform.DynamicTag))
        {
            var reader = new BinaryReaderBE(new MemoryStream(wad.Chunks[Platform.DynamicTag]));
            Platforms.Clear();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var platform = new Platform();
                platform.LoadDynamic(reader);
                Platforms.Add(platform);

                // open up the polygon
                if (platform.PolygonIndex >= 0 && platform.PolygonIndex < Polygons.Count)
                {
                    var polygon = Polygons[platform.PolygonIndex];
                    if (platform.ComesFromFloor)
                    {
                        polygon.FloorHeight = platform.MinimumHeight;
                    }
                    if (platform.ComesFromCeiling)
                    {
                        polygon.CeilingHeight = platform.MaximumHeight;
                    }
                }
            }
        }

        if (wad.Chunks.ContainsKey(Light.Tag))
        {
            LoadChunkList<Light>(Lights, wad.Chunks[Light.Tag]);
        }

        if (wad.Chunks.ContainsKey(Placement.Tag))
        {
            var reader = new BinaryReaderBE(new MemoryStream(wad.Chunks[Placement.Tag]));
            ItemPlacement.Clear();
            for (var i = 0; i < Placement.Count; ++i)
            {
                var placement = new Placement();
                placement.Load(reader);
                ItemPlacement.Add(placement);
            }

            MonsterPlacement.Clear();
            for (var i = 0; i < Placement.Count; ++i)
            {
                var placement = new Placement();
                placement.Load(reader);
                MonsterPlacement.Add(placement);
            }
        }

        if (wad.Chunks.ContainsKey(Annotation.Tag))
        {
            LoadChunkList<Annotation>(Annotations, wad.Chunks[Annotation.Tag]);
        }

        Attributes.PolygonLayers.Clear();
        Attributes.PortalLines.Clear();
        EndpointPolygons.Clear();
        EndpointLines.Clear();
        for (var i = 0; i < Endpoints.Count; ++i)
        {
            EndpointPolygons.Add([]);
            EndpointLines.Add([]);
        }

        foreach (var polygon in Polygons)
        {
            UpdatePolygonConcavity(polygon);
            for (var i = 0; i < polygon.VertexCount; ++i)
            {
                var line = Lines[polygon.LineIndexes[i]];
                EndpointPolygons[line.EndpointIndexes[0]].Add(polygon);
                EndpointPolygons[line.EndpointIndexes[1]].Add(polygon);
            }
        }

        foreach (var line in Lines)
        {
            EndpointLines[line.EndpointIndexes[0]].Add(line);
            EndpointLines[line.EndpointIndexes[1]].Add(line);
        }

        for (var i = 0; i < Polygons.Count; ++i)
        {
            var polygon = Polygons[i];
            if (polygon.Type == PolygonType.Platform)
            {
                var found = false;
                for (var j = 0; j < Platforms.Count; ++j)
                {
                    var platform = Platforms[j];
                    if (platform.PolygonIndex == i)
                    {
                        polygon.Permutation = (short)j;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    var platform = new Platform();
                    platform.SetTypeWithDefaults(PlatformType.SphtDoor);
                    platform.PolygonIndex = (short)i;
                    polygon.Permutation = (short)Platforms.Count;
                    Platforms.Add(platform);
                }
            }
        }

        foreach (var side in Sides)
        {
            side.PolygonIndex = -1;
            side.LineIndex = -1;
        }

        for (short index = 0; index < Lines.Count; ++index)
        {
            var line = Lines[index];
            Polygon p1 = null;
            Polygon p2 = null;
            if (line.ClockwisePolygonOwner != -1)
            {
                p1 = Polygons[line.ClockwisePolygonOwner];
            }
            if (line.CounterclockwisePolygonOwner != -1)
            {
                p2 = Polygons[line.CounterclockwisePolygonOwner];
            }

            if (p1 != null && p2 != null)
            {
                line.HighestAdjacentFloor = Math.Max(p1.FloorHeight, p2.FloorHeight);
                line.LowestAdjacentCeiling = Math.Min(p1.CeilingHeight, p2.CeilingHeight);
            }
            else if (p1 != null)
            {
                line.HighestAdjacentFloor = p1.FloorHeight;
                line.LowestAdjacentCeiling = p1.CeilingHeight;
            }
            else if (p2 != null)
            {
                line.HighestAdjacentFloor = p2.FloorHeight;
                line.LowestAdjacentCeiling = p2.CeilingHeight;
            }
            else
            {
                line.HighestAdjacentFloor = 0;
                line.LowestAdjacentCeiling = 0;
            }

            if (line.VariableElevation)
            {
                line.Solid = line.HighestAdjacentFloor >= line.LowestAdjacentCeiling;
            }

            if (line.ClockwisePolygonSideIndex != -1)
            {
                var side = Sides[line.ClockwisePolygonSideIndex];
                side.LineIndex = index;
                side.PolygonIndex = line.ClockwisePolygonOwner;
            }

            if (line.CounterclockwisePolygonSideIndex != -1)
            {
                var side = Sides[line.CounterclockwisePolygonSideIndex];
                side.LineIndex = index;
                side.PolygonIndex = line.CounterclockwisePolygonOwner;
            }
        }

        if (wad.Chunks.ContainsKey(MapObject.Tag))
        {
            LoadChunkList<MapObject>(Objects, wad.Chunks[MapObject.Tag]);
        }
        else
        {
            throw new Wadfile.BadMapException("Incomplete level: missing map objects chunk");
        }

        if (wad.Chunks.ContainsKey(Media.Tag))
        {
            LoadChunkList<Media>(Medias, wad.Chunks[Media.Tag]);
        }

        if (wad.Chunks.ContainsKey(AmbientSound.Tag))
        {
            LoadChunkList<AmbientSound>(AmbientSounds, wad.Chunks[AmbientSound.Tag]);
        }

        if (wad.Chunks.ContainsKey(RandomSound.Tag))
        {
            LoadChunkList<RandomSound>(RandomSounds, wad.Chunks[RandomSound.Tag]);
        }
    }

    public void AssurePlayerStart()
    {
        var found_start = false;
        foreach (var obj in Objects)
        {
            if (obj.Type == ObjectType.Player)
            {
                found_start = true;
                break;
            }
        }

        if (Polygons.Count > 0 && !found_start)
        {
            var obj = new MapObject
            {
                Type = ObjectType.Player
            };
            var center = PolygonCenter(Polygons[0]);
            obj.X = center.X;
            obj.Y = center.Y;
            obj.PolygonIndex = 0;
            Objects.Add(obj);
        }
    }

    public LevelAndAttributes Save()
    {
        var wad = new Wadfile.DirectoryEntry
        {
            Chunks = Chunks
        };
        wad.Chunks[MapInfo.Tag] = SaveChunk(mapInfo);
        wad.Chunks[Point.Tag] = SaveChunk(Endpoints);
        wad.Chunks[Line.Tag] = SaveChunk(Lines);
        wad.Chunks[Polygon.Tag] = SaveChunk(Polygons);
        wad.Chunks[Side.Tag] = SaveChunk(Sides);
        wad.Chunks[MapObject.Tag] = SaveChunk(Objects);
        wad.Chunks[Platform.StaticTag] = SaveChunk(Platforms);
        wad.Chunks[Light.Tag] = SaveChunk(Lights);
        wad.Chunks[Annotation.Tag] = SaveChunk(Annotations);
        wad.Chunks[Media.Tag] = SaveChunk(Medias);
        wad.Chunks[AmbientSound.Tag] = SaveChunk(AmbientSounds);
        wad.Chunks[RandomSound.Tag] = SaveChunk(RandomSounds);

        {
            var stream = new MemoryStream();
            var writer = new BinaryWriterBE(stream);
            foreach (var placement in ItemPlacement)
            {
                placement.Save(writer);
            }
            foreach (var placement in MonsterPlacement)
            {
                placement.Save(writer);
            }

            wad.Chunks[Placement.Tag] = stream.ToArray();
        }


        // remove merge-type chunks
        foreach (var tag in ChunkFilter)
        {
            wad.Chunks.Remove(tag);
        }

        return new LevelAndAttributes { Wad = wad, Attributes = Attributes.JsonClone() };
    }

    public string Name
    {
        get
        {
            return mapInfo.Name;
        }
        set
        {
            mapInfo.Name = value;
        }
    }

    public short Environment
    {
        get
        {
            return mapInfo.Environment;
        }
        set
        {
            mapInfo.Environment = value;
        }
    }

    public short Landscape
    {
        get
        {
            return mapInfo.Landscape;
        }
        set
        {
            mapInfo.Landscape = value;
        }
    }

    public bool Vacuum
    {
        get
        {
            return GetEnvironmentFlag(EnvironmentFlags.Vacuum);
        }
        set
        {
            SetEnvironmentFlag(EnvironmentFlags.Vacuum, value);
        }
    }

    public bool Magnetic
    {
        get
        {
            return GetEnvironmentFlag(EnvironmentFlags.Magnetic);
        }
        set
        {
            SetEnvironmentFlag(EnvironmentFlags.Magnetic, value);
        }
    }

    public bool Rebellion
    {
        get
        {
            return GetEnvironmentFlag(EnvironmentFlags.Rebellion);
        }
        set
        {
            SetEnvironmentFlag(EnvironmentFlags.Rebellion, value);
        }
    }

    public bool LowGravity
    {
        get
        {
            return GetEnvironmentFlag(EnvironmentFlags.LowGravity);
        }
        set
        {
            SetEnvironmentFlag(EnvironmentFlags.LowGravity, value);
        }
    }

    public bool RebellionM1
    {
        get
        {
            return GetEnvironmentFlag(EnvironmentFlags.RebellionM1);
        }
        set
        {
            SetEnvironmentFlag(EnvironmentFlags.RebellionM1, value);
        }
    }

    public bool GlueM1
    {
        get
        {
            return GetEnvironmentFlag(EnvironmentFlags.GlueM1);
        }
        set
        {
            SetEnvironmentFlag(EnvironmentFlags.GlueM1, value);
        }
    }

    public bool OuchM1
    {
        get
        {
            return GetEnvironmentFlag(EnvironmentFlags.OuchM1);
        }
        set
        {
            SetEnvironmentFlag(EnvironmentFlags.OuchM1, value);
        }
    }

    public bool SongIndexM1
    {
        get
        {
            return GetEnvironmentFlag(EnvironmentFlags.SongIndexM1);
        }
        set
        {
            SetEnvironmentFlag(EnvironmentFlags.SongIndexM1, value);
        }
    }

    public bool TerminalsStopTime
    {
        get
        {
            return GetEnvironmentFlag(EnvironmentFlags.TerminalsStopTime);
        }
        set
        {
            SetEnvironmentFlag(EnvironmentFlags.TerminalsStopTime, value);
        }
    }

    public bool M1ActivationRange
    {
        get
        {
            return GetEnvironmentFlag(EnvironmentFlags.M1ActivationRange);
        }
        set
        {
            SetEnvironmentFlag(EnvironmentFlags.M1ActivationRange, value);
        }
    }

    public bool M1Weapons
    {
        get
        {
            return GetEnvironmentFlag(EnvironmentFlags.M1Weapons);
        }
        set
        {
            SetEnvironmentFlag(EnvironmentFlags.M1Weapons, value);
        }
    }

    public bool Extermination
    {
        get
        {
            return GetMissionFlag(MissionFlags.Extermination);
        }
        set
        {
            SetMissionFlag(MissionFlags.Extermination, value);
        }
    }

    public bool Exploration
    {
        get
        {
            return GetMissionFlag(MissionFlags.Exploration);
        }
        set
        {
            SetMissionFlag(MissionFlags.Exploration, value);
        }
    }

    public bool Retrieval
    {
        get
        {
            return GetMissionFlag(MissionFlags.Retrieval);
        }
        set
        {
            SetMissionFlag(MissionFlags.Retrieval, value);
        }
    }

    public bool Repair
    {
        get
        {
            return GetMissionFlag(MissionFlags.Repair);
        }
        set
        {
            SetMissionFlag(MissionFlags.Repair, value);
        }
    }

    public bool Rescue
    {
        get
        {
            return GetMissionFlag(MissionFlags.Rescue);
        }
        set
        {
            SetMissionFlag(MissionFlags.Rescue, value);
        }
    }

    public bool ExplorationM1
    {
        get
        {
            return GetMissionFlag(MissionFlags.ExplorationM1);
        }
        set
        {
            SetMissionFlag(MissionFlags.ExplorationM1, value);
        }
    }

    public bool RescueM1
    {
        get
        {
            return GetMissionFlag(MissionFlags.RescueM1);
        }
        set
        {
            SetMissionFlag(MissionFlags.RescueM1, value);
        }
    }

    public bool RepairM1
    {
        get
        {
            return GetMissionFlag(MissionFlags.RepairM1);
        }
        set
        {
            SetMissionFlag(MissionFlags.RepairM1, value);
        }
    }

    public bool SinglePlayer
    {
        get
        {
            return GetEntryPointFlag(EntryPointFlags.SinglePlayer);
        }
        set
        {
            SetEntryPointFlag(EntryPointFlags.SinglePlayer, value);
        }
    }

    public bool MultiplayerCooperative
    {
        get
        {
            return GetEntryPointFlag(EntryPointFlags.MultiplayerCooperative);
        }
        set
        {
            SetEntryPointFlag(EntryPointFlags.MultiplayerCooperative, value);
        }
    }

    public bool MultiplayerCarnage
    {
        get
        {
            return GetEntryPointFlag(EntryPointFlags.MultiplayerCarnage);
        }
        set
        {
            SetEntryPointFlag(EntryPointFlags.MultiplayerCarnage, value);
        }
    }

    public bool KillTheManWithTheBall
    {
        get
        {
            return GetEntryPointFlag(EntryPointFlags.KillTheManWithTheBall);
        }
        set
        {
            SetEntryPointFlag(EntryPointFlags.KillTheManWithTheBall, value);
        }
    }

    public bool KingOfTheHill
    {
        get
        {
            return GetEntryPointFlag(EntryPointFlags.KingOfTheHill);
        }
        set
        {
            SetEntryPointFlag(EntryPointFlags.KingOfTheHill, value);
        }
    }

    public bool Defense
    {
        get
        {
            return GetEntryPointFlag(EntryPointFlags.Defense);
        }
        set
        {
            SetEntryPointFlag(EntryPointFlags.Defense, value);
        }
    }

    public bool Rugby
    {
        get
        {
            return GetEntryPointFlag(EntryPointFlags.Rugby);
        }
        set
        {
            SetEntryPointFlag(EntryPointFlags.Rugby, value);
        }
    }

    public bool CaptureTheFlag
    {
        get
        {
            return GetEntryPointFlag(EntryPointFlags.CaptureTheFlag);
        }
        set
        {
            SetEntryPointFlag(EntryPointFlags.CaptureTheFlag, value);
        }
    }

    void SetEnvironmentFlag(EnvironmentFlags flag, bool value)
    {
        if (value)
        {
            mapInfo.EnvironmentFlags |= flag;
        }
        else
        {
            mapInfo.EnvironmentFlags &= ~flag;
        }
    }

    bool GetEnvironmentFlag(EnvironmentFlags flag)
    {
        return (mapInfo.EnvironmentFlags & flag) != 0;
    }

    void SetMissionFlag(MissionFlags flag, bool value)
    {
        if (value)
        {
            mapInfo.MissionFlags |= flag;
        }
        else
        {
            mapInfo.MissionFlags &= ~flag;
        }
    }

    bool GetMissionFlag(MissionFlags flag)
    {
        return (mapInfo.MissionFlags & flag) != 0;
    }

    void SetEntryPointFlag(EntryPointFlags flag, bool value)
    {
        if (value)
        {
            mapInfo.EntryPointFlags |= flag;
        }
        else
        {
            mapInfo.EntryPointFlags &= ~flag;
        }
    }

    bool GetEntryPointFlag(EntryPointFlags flag)
    {
        return (mapInfo.EntryPointFlags & flag) != 0;
    }

    //static public void Main(string[] args) {
    //    if (args.Length == 1) {
    //	Wadfile wadfile = new Wadfile();
    //	wadfile.Load(args[0]);

    //	Level level = new Level();
    //	level.Load(wadfile.Directory[0]);
    //	Console.WriteLine("\"{0}\"", level.mapInfo.Name);
    //	Console.WriteLine("{0} Points", level.Endpoints.Count);
    //	Console.WriteLine("{0} Lines", level.Lines.Count);
    //	Console.WriteLine("{0} Polygons", level.Polygons.Count);
    //    } else {
    //	Console.WriteLine("Test usage: wadfile.exe <wadfile>");
    //    }
    //}
}

