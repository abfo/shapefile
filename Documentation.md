_Note: Catfood.Shapefile.dll is not thread safe. You should open, enumerate and then close (dispose) a shapefile on the same thread. Jet drivers are used to access shapefile metadata._

To get started add a reference to Catfood.Shapefile.dll and import the {{Catfood.Shapefile}} namespace.

A shapefile consists of three files:

* filename.shp - the main file containing shapes.
* filename.shx - an index to the shapes in the main file.
* filename.dbf - database containing metadata for each shape.

To enumerate shapes pass the path to any of these three files to the {{Shapefile}} constructor and then use the {{IEnumerable<Shape>}} interface as demonstrated below:

{code:c#}
using (Shapefile shapefile = new Shapefile("my.shp")
{
    foreach (Shape shape in shapefile)
    {
        Console.WriteLine("ShapeType: {0}", shape.Type);
    }
}
{code:c#}

{{Shape}} is the base class for a set of more specific classes - {{ShapePoint}}, {{ShapeMultiPoint}}, {{ShapePolyLine}} and {{ShapePolygon}}. Cast to the appropriate class based on the {{Type}} property:

{code:c#}
switch (shape.Type)
{
    case ShapeType.Point:
        ShapePoint shapePoint = shape as ShapePoint;
        Console.WriteLine("Point={0},{1}", shapePoint.Point.X, shapePoint.Point.Y);
        break;

    // ...
}
{code:c#}

Access metadata for the shape using {{GetMetadataNames()}} to list available names (keys) and {{GetMetadata()}} to access metadata by name.

Bounding boxes default to the original `BoundingBoxConvention.Legacy` mapping:
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
Existing constructors retain the legacy mapping. A custom connection template can
be passed as the second argument, followed by the convention as the third.
When using the parameterless constructor, set `BoundingBoxConvention` before
calling `Open`; changing it after opening throws `InvalidOperationException`.
`RectangleD` itself continues to store the four constructor arguments as supplied.

See the {{ShapefileDemo}} project for a sample command line application that dumps information for each shape in a shapefile. 

Catfood.Shapefile uses the Jet driver to access shapefile metadata (stored in dBase format). The 32-bit version of this driver is almost certainly available on all systems. To use Catfood.Shapefile on 64-bit systems either target your application at x86 (it will then use the 32-bit Jet driver) or install the 64-bit Jet driver. From version 1.40 you can also change the default connection string template use to access shapefile metadata via a constructor overload. {{Shapefile.ConnectionStringTemplateJet}} is the default, {{Shapefile.ConnectionStringTemplateAce}} uses the ACE driver.

You may find it helpful to refer to the [ERSI Shapefile Technical Description](http://www.esri.com/library/whitepapers/pdfs/shapefile.pdf) (PDF) for details about the properties of each shape type.
