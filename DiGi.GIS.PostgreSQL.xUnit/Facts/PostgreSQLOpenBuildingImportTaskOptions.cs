using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using System;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the defaults of the open building import that DiGi.GIS.PostgreSQL#170 added: files that cannot reach the scope are left unread, and the conversion uses half the processors (zero).
        /// </summary>
        [Fact]
        public void PostgreSQLOpenBuildingImportTaskOptions_Defaults()
        {
            PostgreSQLOpenBuildingImportTaskOptions postgreSQLOpenBuildingImportTaskOptions = new();

            Assert.True(postgreSQLOpenBuildingImportTaskOptions.SkipSourcesOutOfScope);
            Assert.Equal(0, postgreSQLOpenBuildingImportTaskOptions.MaxDegreeOfParallelism);
            Assert.Null(postgreSQLOpenBuildingImportTaskOptions.CountyIds);
            Assert.Null(postgreSQLOpenBuildingImportTaskOptions.BoundingBox2D);
        }

        /// <summary>
        /// Verifies that a populated instance - every member set away from its default - survives a JSON round trip and the copy constructor, member by member, with its own collections and box.
        /// <para>A member the instance leaves at its default is invisible to <c>SerializationCheck</c>, and a copy that forgot a line would pass on a member never set; both new members of DiGi.GIS.PostgreSQL#170 are set against their defaults for that reason.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLOpenBuildingImportTaskOptions_Serialization()
        {
            PostgreSQLOpenBuildingImportTaskOptions postgreSQLOpenBuildingImportTaskOptions = new()
            {
                BoundingBox2D = new BoundingBox2D(new Point2D(629000, 519000), new Point2D(631000, 521000)),
                CountyIds = [55417, 29866],
                DatasetKey = "microsoft",
                DownloadDate = new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Unspecified),
                FileName = "dataset-links.csv",
                Licence = "ODbL-1.0",
                MaxDegreeOfParallelism = 6,
                Quadkey = "120213101",
                Sha256 = "abc",
                SkipSourcesOutOfScope = false,
                SourceObservationDate = "2026",
                SourcePaths = [@"D:\Input\Microsoft\quadkey=120213101\part-0.csv.gz", @"D:\Input\Microsoft\quadkey=120213032\part-1.csv.gz"],
                SourceUrl = "https://example.invalid/dataset-links.csv",
                Version = "2026-02-03",
            };

            Core.xUnit.Query.SerializationCheck(postgreSQLOpenBuildingImportTaskOptions);

            string? text = Core.Convert.ToSystem_String(postgreSQLOpenBuildingImportTaskOptions);
            PostgreSQLOpenBuildingImportTaskOptions? postgreSQLOpenBuildingImportTaskOptions_Parsed = Core.Convert.ToDiGi<PostgreSQLOpenBuildingImportTaskOptions>(text)?.FirstOrDefault();
            Assert.NotNull(postgreSQLOpenBuildingImportTaskOptions_Parsed);
            Assert.False(postgreSQLOpenBuildingImportTaskOptions_Parsed.SkipSourcesOutOfScope);
            Assert.Equal(6, postgreSQLOpenBuildingImportTaskOptions_Parsed.MaxDegreeOfParallelism);

            PostgreSQLOpenBuildingImportTaskOptions postgreSQLOpenBuildingImportTaskOptions_Copy = new(postgreSQLOpenBuildingImportTaskOptions);

            Assert.NotNull(postgreSQLOpenBuildingImportTaskOptions_Copy.BoundingBox2D);
            Assert.NotSame(postgreSQLOpenBuildingImportTaskOptions.BoundingBox2D, postgreSQLOpenBuildingImportTaskOptions_Copy.BoundingBox2D);
            Assert.Equal(629000, postgreSQLOpenBuildingImportTaskOptions_Copy.BoundingBox2D.Min.X);
            Assert.Equal(521000, postgreSQLOpenBuildingImportTaskOptions_Copy.BoundingBox2D.Max.Y);
            Assert.NotSame(postgreSQLOpenBuildingImportTaskOptions.CountyIds, postgreSQLOpenBuildingImportTaskOptions_Copy.CountyIds);
            Assert.True(postgreSQLOpenBuildingImportTaskOptions.CountyIds.SetEquals(postgreSQLOpenBuildingImportTaskOptions_Copy.CountyIds!));
            Assert.Equal(postgreSQLOpenBuildingImportTaskOptions.DatasetKey, postgreSQLOpenBuildingImportTaskOptions_Copy.DatasetKey);
            Assert.Equal(postgreSQLOpenBuildingImportTaskOptions.DownloadDate, postgreSQLOpenBuildingImportTaskOptions_Copy.DownloadDate);
            Assert.Equal(postgreSQLOpenBuildingImportTaskOptions.FileName, postgreSQLOpenBuildingImportTaskOptions_Copy.FileName);
            Assert.Equal(postgreSQLOpenBuildingImportTaskOptions.Licence, postgreSQLOpenBuildingImportTaskOptions_Copy.Licence);
            Assert.Equal(6, postgreSQLOpenBuildingImportTaskOptions_Copy.MaxDegreeOfParallelism);
            Assert.Equal(postgreSQLOpenBuildingImportTaskOptions.Quadkey, postgreSQLOpenBuildingImportTaskOptions_Copy.Quadkey);
            Assert.Equal(postgreSQLOpenBuildingImportTaskOptions.Sha256, postgreSQLOpenBuildingImportTaskOptions_Copy.Sha256);
            Assert.False(postgreSQLOpenBuildingImportTaskOptions_Copy.SkipSourcesOutOfScope);
            Assert.Equal(postgreSQLOpenBuildingImportTaskOptions.SourceObservationDate, postgreSQLOpenBuildingImportTaskOptions_Copy.SourceObservationDate);
            Assert.NotSame(postgreSQLOpenBuildingImportTaskOptions.SourcePaths, postgreSQLOpenBuildingImportTaskOptions_Copy.SourcePaths);
            Assert.True(postgreSQLOpenBuildingImportTaskOptions.SourcePaths.SetEquals(postgreSQLOpenBuildingImportTaskOptions_Copy.SourcePaths!));
            Assert.Equal(postgreSQLOpenBuildingImportTaskOptions.SourceUrl, postgreSQLOpenBuildingImportTaskOptions_Copy.SourceUrl);
            Assert.Equal(postgreSQLOpenBuildingImportTaskOptions.Version, postgreSQLOpenBuildingImportTaskOptions_Copy.Version);
        }
    }
}
