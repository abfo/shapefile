# Catfood.Shapefile

A .NET library for read-only enumeration of ESRI shapefiles and their metadata. Supported 2D shapes include Point, MultiPoint, PolyLine and Polygon.

Version 3 targets **.NET Standard 2.1** and reads DBF metadata directly using the [DbfDataReader 2.2.0 NuGet package](https://www.nuget.org/packages/DbfDataReader/2.2.0). No Jet/ACE installation, OLE DB provider, connection string, or x86 process is required. **.NET Framework is no longer supported**; applications must use a runtime that supports .NET Standard 2.1.

Install the Catfood.Shapefile NuGet package and import the `Catfood.Shapefile` namespace. NuGet also installs the DbfDataReader dependency.

A shapefile consists of three files with the same base filename:

* `filename.shp` contains shapes.
* `filename.shx` indexes the shapes.
* `filename.dbf` contains metadata for each shape.

Pass the path to any of these files to the constructor, then enumerate shapes:

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

Alternatively, create a parameterless `Shapefile` and call `Open(path)` once. Dispose it before opening another dataset with a new instance. Failed opens release files and can be retried.

`Shape` is the base class for `ShapePoint`, `ShapeMultiPoint`, `ShapePolyLine`, and `ShapePolygon`. Cast based on the `Type` property:

```csharp
switch (shape.Type)
{
    case ShapeType.Point:
        ShapePoint point = (ShapePoint)shape;
        Console.WriteLine("Point={0},{1}", point.Point.X, point.Point.Y);
        break;
}
```

## Bounding boxes

Bounding boxes default to `BoundingBoxConvention.Legacy`:
`Left = XMin`, `Top = YMin`, `Right = XMax`, `Bottom = YMax`.
For Y-up coordinates, opt in to `Top = YMax` and `Bottom = YMin`:

```csharp
using (var shapefile = new Shapefile("my.shp", BoundingBoxConvention.YUp))
{
    RectangleD bounds = shapefile.BoundingBox;
}
```

The convention applies to the file and all enumerated MultiPoint, PolyLine,
PolyLineM and Polygon bounding boxes. Point coordinates and metadata are unchanged.
Constructors without an explicit convention retain the legacy mapping.
When using the parameterless constructor, set `BoundingBoxConvention` before
calling `Open`; changing it after opening throws `InvalidOperationException`.
`RectangleD` itself continues to store its constructor arguments as supplied.

## Metadata

Use `GetMetadataNames()` to list field names and `GetMetadata(name)` to read string values. Name lookup in this dictionary is case-insensitive. Missing field names return `null`; DBF null values become empty strings. Trailing padding is removed from character fields, while leading spaces are preserved. The DBF header's language driver determines character encoding; `.cpg` overrides are not currently read.

For typed values, use `Shape.DataRecord`, which exposes DbfDataReader through `IDataRecord`. Set `RawMetadataOnly = true` before creating an enumerator to skip building the string dictionary:

```csharp
using (var shapefile = new Shapefile("my.shp") { RawMetadataOnly = true })
{
    foreach (Shape shape in shapefile)
    {
        var record = shape.DataRecord;
        for (int i = 0; i < record.FieldCount; i++)
            Console.WriteLine("{0}: {1}", record.GetName(i),
                record.IsDBNull(i) ? "" : record.GetValue(i));
    }
}
```

The data record belongs to the live enumerator: read or copy its values before advancing, resetting, or disposing the enumerator, or disposing the shapefile. The string dictionary remains available on a retained shape. With `RawMetadataOnly`, `GetMetadataNames()` and `GetMetadata()` return `null`.

DBF records match shapes by physical position. Records marked deleted are included so subsequent shapes retain the correct metadata. If a shape has no corresponding DBF row, enumeration throws `InvalidOperationException`. Enumeration can be repeated, and `Reset()` returns an enumerator to the start. Disposing the shapefile also closes its active enumerators.

## Migrating from version 2

Version 3 replaces `System.Data.OleDb` with [DbfDataReader 2.2.0](https://www.nuget.org/packages/DbfDataReader/2.2.0), resolving the missing Jet provider reported in [issue #3](https://github.com/abfo/shapefile/issues/3).

* The library now targets **.NET Standard 2.1**. .NET Framework applications must migrate to a compatible runtime before upgrading. The sample and test projects target .NET 10.
* Jet and ACE drivers, connection strings, and x86 targeting are no longer needed. DBF files are opened directly, including filenames longer than eight characters. Companion memo files (`.fpt`/`.dbt`, including uppercase extensions) are opened alongside the DBF when present.
* The connection-string constructor overloads, `ConnectionStringTemplate`, `ConnectionStringTemplateJet`, and `ConnectionStringTemplateAce` remain for source compatibility but are obsolete; connection string templates are ignored. Replace their usage with `new Shapefile(path)` or `new Shapefile(path, BoundingBoxConvention.YUp)` to retain an explicit bounding-box convention.
* `DataRecord` is backed by DbfDataReader rather than OleDb. Use `IsDBNull()`, `GetFieldType()`, and the appropriate typed getters or `GetValue()`; do not cast it to `OleDbDataReader` or assume Jet-specific numeric types or type names. DbfDataReader does not implement every optional `IDataRecord` operation, such as `GetBytes()`, `GetChars()`, or `GetData()`.

See the `ShapefileDemo` project for a command-line application that dumps each shape, and the [ESRI Shapefile Technical Description](https://www.esri.com/library/whitepapers/pdfs/shapefile.pdf) for the file format.

## Build and Test

Build and test with the .NET 10 SDK (the demo and tests target .NET 10):

```shell
dotnet build Catfood.Shapefile.sln --configuration Release
dotnet test Catfood.Shapefile.sln --configuration Release --settings test.runsettings
dotnet run --project ShapefileDemo --configuration Release -- TestData/PAN_water_areas_dcw.shp
```

The supplied test settings run tests in a 64-bit process. Omit `--settings test.runsettings` to use the test runner's default architecture.
