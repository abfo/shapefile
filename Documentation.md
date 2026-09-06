# Using Catfood.Shapefile

Catfood.Shapefile is not thread safe. Open, enumerate, and dispose a shapefile on the same thread.

Install the Catfood.Shapefile NuGet package and import the `Catfood.Shapefile` namespace. NuGet also installs the DbfDataReader dependency.

A shapefile consists of three files with the same base filename:

* `filename.shp` contains shapes.
* `filename.shx` indexes the shapes.
* `filename.dbf` contains metadata for each shape.

Pass the path to any of these files to the constructor, then enumerate shapes:

```csharp
using (Shapefile shapefile = new Shapefile("my.shp"))
{
    foreach (Shape shape in shapefile)
    {
        Console.WriteLine("ShapeType: {0}", shape.Type);
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
* The connection-string constructor overload, `ConnectionStringTemplate`, `ConnectionStringTemplateJet`, and `ConnectionStringTemplateAce` remain for source compatibility but are obsolete and ignored. Replace their usage with `new Shapefile(path)`.
* `DataRecord` is backed by DbfDataReader rather than OleDb. Use `IsDBNull()`, `GetFieldType()`, and the appropriate typed getters or `GetValue()`; do not cast it to `OleDbDataReader` or assume Jet-specific numeric types or type names. DbfDataReader does not implement every optional `IDataRecord` operation, such as `GetBytes()`, `GetChars()`, or `GetData()`.

See the `ShapefileDemo` project for a command-line application that dumps each shape, and the [ESRI Shapefile Technical Description](https://www.esri.com/library/whitepapers/pdfs/shapefile.pdf) for the file format.
