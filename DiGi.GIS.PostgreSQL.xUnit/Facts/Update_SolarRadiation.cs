using DiGi.Core.IO.Table.Classes;
using DiGi.Geometry.Spatial.Classes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The scratch county part of the yearly solar radiation aggregation facts. Only an in-memory key - the Modify never touches a database.
        /// </summary>
        private const int countyId_SolarAggregation = 990401;

        /// <summary>
        /// Builds a wall surface facing the given azimuth (degrees clockwise from north) with the given area and summary values, the beam/diffuse/ground split at the 60/30/10 ratio the shaded irradiation carries.
        /// </summary>
        private static GIS.Classes.SurfaceSolarRadiation SolarWall(double azimuth, double area, double irradiation, double energy)
        {
            Vector3D normal = new(Math.Sin(azimuth * Math.PI / 180), Math.Cos(azimuth * Math.PI / 180), 0);
            return new GIS.Classes.SurfaceSolarRadiation($"wall-{azimuth}", area, normal, 0.8, 0.6, irradiation, irradiation * 0.6, irradiation * 0.3, irradiation * 0.1, irradiation * 1.2, energy, null);
        }

        /// <summary>
        /// Builds a flat roof surface - a vertical outward normal, so it has area and energy but no classifiable facing direction.
        /// </summary>
        private static GIS.Classes.SurfaceSolarRadiation SolarRoof(double area, double irradiation, double energy)
        {
            Vector3D normal = new(0, 0, 1);
            return new GIS.Classes.SurfaceSolarRadiation("roof", area, normal, 0.8, 0.6, irradiation, irradiation * 0.6, irradiation * 0.3, irradiation * 0.1, irradiation * 1.2, energy, null);
        }

        private static GIS.Classes.BuildingSolarRadiationResult SolarResult(string reference, params GIS.Classes.SurfaceSolarRadiation[] surfaces)
        {
            return new GIS.Classes.BuildingSolarRadiationResult(reference, countyId_SolarAggregation, 42L, 50.0, 1, GIS.Enums.WeatherSourceType.EPW, "1", null, null, null, new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero), [.. surfaces]);
        }

        /// <summary>
        /// Reads the first row's cell of the given column, null for an absent value or an empty table.
        /// <para>The column is looked up by the name the method under test wrote it under, the same way the external components area facts read their cells.</para>
        /// </summary>
        private static object? SolarCell(Table table, Column column)
        {
            if (table.RowCount < 1 || !table.TryGetColumn(column.Name, out Column? column_Table) || column_Table is null)
            {
                return null;
            }

            Row? row = table.GetRow(0);
            if (row is null || !row.TryGetValue<object?>(column_Table.Index, out object? value) || value is null || value is DBNull)
            {
                return null;
            }

            return value;
        }

        private static float? SolarFloat(Table table, Column column)
        {
            object? value = SolarCell(table, column);
            if (value is null)
            {
                return null;
            }

            return Core.Query.TryConvert(value, out double d) ? (float)d : null;
        }

        /// <summary>
        /// Verifies the aggregation of <see cref="Modify.Update_SolarRadiation(Table?, IEnumerable{GIS.Classes.BuildingSolarRadiationResult}?)"/>: the building totals are the area-weighted averages and the energy sum, the per-direction values cover exactly the surfaces in each sector, and a sector with no surface is null rather than zero.
        /// <para>Reference: DiGi.GIS.PostgreSQL#101.</para>
        /// </summary>
        [Fact]
        public void Update_SolarRadiation_Aggregation()
        {
            GIS.Classes.BuildingSolarRadiationResult result = SolarResult("agg-building", SolarWall(0, 10, 200, 2000), SolarWall(180, 20, 400, 8000));

            Table table = new();
            DiGi.GIS.PostgreSQL.Classes.SolarRadiationSummaryResult modifyResult = Modify.Update_SolarRadiation(table, [result]);

            Assert.Equal(1, modifyResult.ProcessedReferenceCount);
            Assert.Equal(2, modifyResult.SurfaceCount);
            Assert.Equal(0, modifyResult.SurfaceWithoutSectorCount);
            Assert.Equal(0, modifyResult.SkippedSurfaceCount);

            List<Column> columns = DiGi.GIS.IO.Create.Columns_SolarRadiation();
            Assert.Equal(25, columns.Count);

            // Building totals (index 0-5): the area-weighted averages over the two walls, and the energy sum.
            Assert.Equal(10000.0 / 30.0, SolarFloat(table, columns[0])!.Value, 1e-2); // SolarIrradiation
            Assert.Equal(10000.0, SolarFloat(table, columns[1])!.Value, 1e-2);        // SolarEnergy
            Assert.Equal(6000.0 / 30.0, SolarFloat(table, columns[2])!.Value, 1e-2);  // SolarBeam
            Assert.Equal(3000.0 / 30.0, SolarFloat(table, columns[3])!.Value, 1e-2);  // SolarDiffuse
            Assert.Equal(1000.0 / 30.0, SolarFloat(table, columns[4])!.Value, 1e-2);  // SolarGround
            Assert.Equal(12000.0 / 30.0, SolarFloat(table, columns[5])!.Value, 1e-2); // SolarIrradiationUnshaded

            // North (index 6-7) and south (index 14-15) carry the two walls; every other sector is null.
            Assert.Equal(200.0, SolarFloat(table, columns[6])!.Value, 1e-2);   // SolarNorthIrradiation
            Assert.Equal(2000.0, SolarFloat(table, columns[7])!.Value, 1e-2);  // SolarNorthEnergy
            Assert.Equal(400.0, SolarFloat(table, columns[14])!.Value, 1e-2);  // SolarSouthIrradiation
            Assert.Equal(8000.0, SolarFloat(table, columns[15])!.Value, 1e-2); // SolarSouthEnergy

            for (int i = 8; i < 14; i++)
            {
                Assert.Null(SolarFloat(table, columns[i]));
            }

            for (int i = 16; i < 22; i++)
            {
                Assert.Null(SolarFloat(table, columns[i]));
            }
        }

        /// <summary>
        /// Verifies the null handling of <see cref="Modify.Update_SolarRadiation(Table?, IEnumerable{GIS.Classes.BuildingSolarRadiationResult}?)"/>: a building with no stored result writes no row, and a building whose surfaces all lack a usable area leaves every one of the 22 columns null rather than zero.
        /// <para>Reference: DiGi.GIS.PostgreSQL#101.</para>
        /// </summary>
        [Fact]
        public void Update_SolarRadiation_NullHandling()
        {
            // No stored result: no row at all.
            Table table_Empty = new();
            DiGi.GIS.PostgreSQL.Classes.SolarRadiationSummaryResult result_Empty = Modify.Update_SolarRadiation(table_Empty, []);
            Assert.Equal(0, result_Empty.ProcessedReferenceCount);
            Assert.Equal(0, table_Empty.RowCount);

            // A result whose single surface has no usable area: a row, but every summary column null.
            GIS.Classes.SurfaceSolarRadiation skipped = new GIS.Classes.SurfaceSolarRadiation("no-area", 0, new Vector3D(0, 1, 0), 0.8, 0.6, 200, 120, 60, 20, 240, 2000, null);
            GIS.Classes.BuildingSolarRadiationResult result_Skipped = SolarResult("skip-building", skipped);

            Table table_Skipped = new();
            DiGi.GIS.PostgreSQL.Classes.SolarRadiationSummaryResult result_Modified = Modify.Update_SolarRadiation(table_Skipped, [result_Skipped]);
            Assert.Equal(1, result_Modified.ProcessedReferenceCount);
            Assert.Equal(0, result_Modified.SurfaceCount);
            Assert.Equal(1, result_Modified.SkippedSurfaceCount);

            List<Column> columns = DiGi.GIS.IO.Create.Columns_SolarRadiation();
            for (int i = 0; i < 22; i++)
            {
                Assert.Null(SolarFloat(table_Skipped, columns[i]));
            }

            // The three metadata columns (index 22-24) carry the weather source identity and the range, independent of surface usability.
            Assert.Equal("1", SolarCell(table_Skipped, columns[22]));            // SolarWeatherSourceId
            Assert.Equal(0, SolarCell(table_Skipped, columns[23]));              // SolarWeatherSourceType (EPW = 0)
            Assert.Equal(50.0f, SolarFloat(table_Skipped, columns[24])!.Value, 1e-2); // SolarRadius
        }

        /// <summary>
        /// Verifies the sector classification of <see cref="Modify.Update_SolarRadiation(Table?, IEnumerable{GIS.Classes.BuildingSolarRadiationResult}?)"/> at the azimuth boundaries: 22.5° files under northeast, 67.5° under east, 337.5° under north - the half-open sector edges of the CardinalDirection mapping.
        /// <para>Reference: DiGi.GIS.PostgreSQL#101.</para>
        /// </summary>
        [Fact]
        public void Update_SolarRadiation_SectorBoundaries()
        {
            GIS.Classes.BuildingSolarRadiationResult result = SolarResult(
                "boundary-building",
                SolarWall(22.5, 10, 100, 1000),  // northeast edge
                SolarWall(67.5, 10, 100, 1000),  // east edge
                SolarWall(337.5, 10, 100, 1000)); // north edge

            Table table = new();
            DiGi.GIS.PostgreSQL.Classes.SolarRadiationSummaryResult modifyResult = Modify.Update_SolarRadiation(table, [result]);
            Assert.Equal(3, modifyResult.SurfaceCount);
            Assert.Equal(0, modifyResult.SurfaceWithoutSectorCount);

            List<Column> columns = DiGi.GIS.IO.Create.Columns_SolarRadiation();

            // Each boundary azimuth lands in exactly one sector, carrying the wall's irradiation and energy.
            Assert.Equal(100.0, SolarFloat(table, columns[8])!.Value, 1e-2);   // SolarNortheastIrradiation
            Assert.Equal(1000.0, SolarFloat(table, columns[9])!.Value, 1e-2); // SolarNortheastEnergy
            Assert.Equal(100.0, SolarFloat(table, columns[10])!.Value, 1e-2); // SolarEastIrradiation
            Assert.Equal(1000.0, SolarFloat(table, columns[11])!.Value, 1e-2); // SolarEastEnergy
            Assert.Equal(100.0, SolarFloat(table, columns[6])!.Value, 1e-2);   // SolarNorthIrradiation
            Assert.Equal(1000.0, SolarFloat(table, columns[7])!.Value, 1e-2);  // SolarNorthEnergy

            // The other sectors are empty.
            for (int i = 12; i < 22; i++)
            {
                Assert.Null(SolarFloat(table, columns[i]));
            }
        }

        /// <summary>
        /// Verifies that a flat roof - a usable surface with a vertical outward normal - counts toward the building totals but toward no direction sector, and that the run reports it as a surface without a sector.
        /// <para>Reference: DiGi.GIS.PostgreSQL#101.</para>
        /// </summary>
        [Fact]
        public void Update_SolarRadiation_FlatRoofCountsInTotalsOnly()
        {
            GIS.Classes.BuildingSolarRadiationResult result = SolarResult("roof-building", SolarWall(0, 10, 200, 2000), SolarRoof(50, 300, 15000));

            Table table = new();
            DiGi.GIS.PostgreSQL.Classes.SolarRadiationSummaryResult modifyResult = Modify.Update_SolarRadiation(table, [result]);
            Assert.Equal(2, modifyResult.SurfaceCount);
            Assert.Equal(1, modifyResult.SurfaceWithoutSectorCount);
            Assert.Equal(0, modifyResult.SkippedSurfaceCount);

            List<Column> columns = DiGi.GIS.IO.Create.Columns_SolarRadiation();

            // The building totals include the roof (60 m² of area in all).
            Assert.Equal((200 * 10 + 300 * 50) / 60.0, SolarFloat(table, columns[0])!.Value, 1e-2); // SolarIrradiation
            Assert.Equal(2000.0 + 15000.0, SolarFloat(table, columns[1])!.Value, 1e-2);             // SolarEnergy

            // The north wall still files under north; the roof files under no sector.
            Assert.Equal(200.0, SolarFloat(table, columns[6])!.Value, 1e-2);   // SolarNorthIrradiation
            Assert.Equal(2000.0, SolarFloat(table, columns[7])!.Value, 1e-2);  // SolarNorthEnergy

            for (int i = 8; i < 22; i++)
            {
                Assert.Null(SolarFloat(table, columns[i]));
            }
        }

        /// <summary>
        /// Verifies that <see cref="DiGi.GIS.PostgreSQL.Classes.SolarRadiationSummaryResult"/> carries its four tallies through, survives a JSON round trip and a clone, and that a populated instance is serializable.
        /// <para>Reference: DiGi.GIS.PostgreSQL#101.</para>
        /// </summary>
        [Fact]
        public void SolarRadiationSummaryResult_Serialization()
        {
            DiGi.GIS.PostgreSQL.Classes.SolarRadiationSummaryResult solarRadiationSummaryResult = new(12, 34, 5, 3);

            Assert.Equal(12, solarRadiationSummaryResult.ProcessedReferenceCount);
            Assert.Equal(34, solarRadiationSummaryResult.SurfaceCount);
            Assert.Equal(5, solarRadiationSummaryResult.SurfaceWithoutSectorCount);
            Assert.Equal(3, solarRadiationSummaryResult.SkippedSurfaceCount);

            string? json = Core.Convert.ToSystem_String(solarRadiationSummaryResult);
            Assert.NotNull(json);

            DiGi.GIS.PostgreSQL.Classes.SolarRadiationSummaryResult? solarRadiationSummaryResult_Json = Core.Convert.ToDiGi<DiGi.GIS.PostgreSQL.Classes.SolarRadiationSummaryResult>(json)?.FirstOrDefault();
            Assert.NotNull(solarRadiationSummaryResult_Json);
            Assert.Equal(12, solarRadiationSummaryResult_Json.ProcessedReferenceCount);
            Assert.Equal(34, solarRadiationSummaryResult_Json.SurfaceCount);
            Assert.Equal(5, solarRadiationSummaryResult_Json.SurfaceWithoutSectorCount);
            Assert.Equal(3, solarRadiationSummaryResult_Json.SkippedSurfaceCount);

            DiGi.GIS.PostgreSQL.Classes.SolarRadiationSummaryResult solarRadiationSummaryResult_Clone = new(solarRadiationSummaryResult);
            Assert.Equal(12, solarRadiationSummaryResult_Clone.ProcessedReferenceCount);
            Assert.Equal(34, solarRadiationSummaryResult_Clone.SurfaceCount);
            Assert.Equal(5, solarRadiationSummaryResult_Clone.SurfaceWithoutSectorCount);
            Assert.Equal(3, solarRadiationSummaryResult_Clone.SkippedSurfaceCount);

            Core.xUnit.Query.SerializationCheck(solarRadiationSummaryResult);
        }
    }
}
