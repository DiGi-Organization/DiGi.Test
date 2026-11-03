using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Enums;
using System;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that an orthophoto WMS probe survives a JSON round trip and a copy with every member populated, and with the nullable members null.
        /// <para>The copy is asserted directly because SerializationCheck never runs the copy constructor.</para>
        /// </summary>
        [Fact]
        public void OrtoImageProbe()
        {
            DateTimeOffset probeDate = new(2026, 11, 3, 13, 15, 0, TimeSpan.FromHours(1));
            BoundingBox2D boundingBox2D = new(new Point2D(500000, 300000), new Point2D(501500, 301000));

            Classes.OrtoImageProbe ortoImageProbe = new(boundingBox2D, Constants.WebService.StandardResolutionTime, Constants.WebService.StandardResolutionTimeLayer, "2020", OrtoImageProbeStatus.Image, new string('a', 64), probeDate);

            Assert.Equal(500000, ortoImageProbe.BoundingBox2D?.Min.X);
            Assert.Equal(301000, ortoImageProbe.BoundingBox2D?.Max.Y);
            Assert.Equal(Constants.WebService.StandardResolutionTime, ortoImageProbe.ServiceUrl);
            Assert.Equal("Raster", ortoImageProbe.Layer);
            Assert.Equal("2020", ortoImageProbe.Time);
            Assert.Equal(OrtoImageProbeStatus.Image, ortoImageProbe.OrtoImageProbeStatus);
            Assert.Equal(new string('a', 64), ortoImageProbe.ContentHash);
            Assert.Equal(probeDate, ortoImageProbe.ProbeDate);

            Classes.OrtoImageProbe? ortoImageProbe_Json = Core.Convert.ToDiGi<Classes.OrtoImageProbe>(Core.Convert.ToSystem_String(ortoImageProbe))?.FirstOrDefault();
            Assert.NotNull(ortoImageProbe_Json);
            Assert.Equal(ortoImageProbe.OrtoImageProbeStatus, ortoImageProbe_Json.OrtoImageProbeStatus);
            Assert.Equal(ortoImageProbe.ContentHash, ortoImageProbe_Json.ContentHash);
            Assert.Equal(ortoImageProbe.ProbeDate, ortoImageProbe_Json.ProbeDate);
            Assert.Equal(ortoImageProbe.BoundingBox2D?.Max.X, ortoImageProbe_Json.BoundingBox2D?.Max.X);

            Core.xUnit.Query.SerializationCheck(ortoImageProbe);

            Classes.OrtoImageProbe ortoImageProbe_Copy = new(ortoImageProbe);
            Assert.Equal(ortoImageProbe.BoundingBox2D?.Min.Y, ortoImageProbe_Copy.BoundingBox2D?.Min.Y);
            Assert.Equal(ortoImageProbe.ServiceUrl, ortoImageProbe_Copy.ServiceUrl);
            Assert.Equal(ortoImageProbe.Layer, ortoImageProbe_Copy.Layer);
            Assert.Equal(ortoImageProbe.Time, ortoImageProbe_Copy.Time);
            Assert.Equal(ortoImageProbe.OrtoImageProbeStatus, ortoImageProbe_Copy.OrtoImageProbeStatus);
            Assert.Equal(ortoImageProbe.ContentHash, ortoImageProbe_Copy.ContentHash);
            Assert.Equal(ortoImageProbe.ProbeDate, ortoImageProbe_Copy.ProbeDate);

            Classes.OrtoImageProbe ortoImageProbe_Null = new(null, null, null, null, OrtoImageProbeStatus.Blank, null, null);

            Core.xUnit.Query.SerializationCheck(ortoImageProbe_Null);

            Classes.OrtoImageProbe ortoImageProbe_NullCopy = new(ortoImageProbe_Null);
            Assert.Null(ortoImageProbe_NullCopy.BoundingBox2D);
            Assert.Null(ortoImageProbe_NullCopy.ContentHash);
            Assert.Equal(OrtoImageProbeStatus.Blank, ortoImageProbe_NullCopy.OrtoImageProbeStatus);
        }
    }
}
