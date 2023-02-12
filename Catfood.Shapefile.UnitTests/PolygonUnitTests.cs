using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Catfood.Shapefile;
using System.IO;
using System.Reflection;

namespace Catfood.Shapefile.UnitTests
{
    [TestClass]
    public class PolygonUnitTests
    {
        [TestMethod]
        public void ParseFirstPointSuccess()
        {
            string shapefilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "..\\..\\..\\TestData\\PAN_water_areas_dcw.shp");

            using (Shapefile shapefile = new Shapefile(shapefilePath))
            {
                Assert.AreEqual(ShapeType.Polygon, shapefile.Type);
                Assert.AreEqual(30, shapefile.Count);
                Assert.AreEqual(-82.6906126889013, shapefile.BoundingBox.Left, 0.001);
                Assert.AreEqual(8.12419598204565, shapefile.BoundingBox.Top, 0.001);
                Assert.AreEqual(-78.0280536845589, shapefile.BoundingBox.Right, 0.001);
                Assert.AreEqual(9.51797198334384, shapefile.BoundingBox.Bottom, 0.001);

                foreach(Shape shape in shapefile)
                {
                    Assert.AreEqual(ShapeType.Polygon, shape.Type);
                    ShapePolygon shapePolygon = shape as ShapePolygon;
                    Assert.IsNotNull(shapePolygon);
                    Assert.AreEqual(5, shapePolygon.GetMetadataNames().Length);
                    Assert.AreEqual("Panama", shapePolygon.GetMetadata("country"));
                    Assert.AreEqual(1, shapePolygon.Parts.Count);
                    Assert.AreEqual(29, shapePolygon.Parts[0].Length);
                    Assert.AreEqual(-78.0503086845797, shapePolygon.Parts[0][0].X, 0.001);
                    Assert.AreEqual(8.42391698232495, shapePolygon.Parts[0][0].Y, 0.001);
                    break;
                }
            }
        }
    }
}
