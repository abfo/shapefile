# Catfood.Shapefile

A .NET library for read-only enumeration of ESRI shapefiles and their metadata. Supported 2D shapes include Point, MultiPoint, PolyLine and Polygon.

Version 3 targets **.NET Standard 2.1** and reads DBF metadata directly using the [DbfDataReader 2.2.0 NuGet package](https://www.nuget.org/packages/DbfDataReader/2.2.0). No Jet/ACE installation, OLE DB provider, connection string, or x86 process is required. **.NET Framework is no longer supported**; applications must use a runtime that supports .NET Standard 2.1.

```csharp
using Catfood.Shapefile;

using (var shapefile = new Shapefile("my.shp"))
{
    foreach (Shape shape in shapefile)
    {
        Console.WriteLine($"{shape.RecordNumber}: {shape.Type}");
        foreach (string name in shape.GetMetadataNames() ?? Array.Empty<string>())
            Console.WriteLine($"{name}: {shape.GetMetadata(name)}");
    }
}
```

Keep the `.shp`, `.shx`, and `.dbf` files together with the same base filename. See [Documentation.md](Documentation.md) for metadata behavior and migration notes.

Build and test with the .NET 10 SDK (the demo and tests target .NET 10):

```shell
dotnet build Catfood.Shapefile.sln --configuration Release
dotnet test Catfood.Shapefile.sln --configuration Release --settings test.runsettings
dotnet run --project ShapefileDemo --configuration Release -- TestData/PAN_water_areas_dcw.shp
```

The supplied test settings run tests in a 64-bit process. Omit `--settings test.runsettings` to use the test runner's default architecture.
