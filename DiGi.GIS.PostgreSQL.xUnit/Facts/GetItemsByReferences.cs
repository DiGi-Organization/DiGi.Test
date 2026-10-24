using DiGi.Analytical.Building.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Building2DReferencedObjectPostgreSQLConverter{TBuilding2DReferencedObject, TUniqueObject}.GetItemByReferenceAsync(string, int?, bool, int, System.Threading.CancellationToken)"/>
        /// returns null when given a null or whitespace reference.
        /// </summary>
        [Fact]
        public async Task GetItemByReferenceAsync_NullOrWhiteSpace_ReturnsNull()
        {
            Building2DOccupancyDataPostgreSQLConverter building2DOccupancyDataPostgreSQLConverter = new(null);
            Building2DOccupancyData? result_Null = await building2DOccupancyDataPostgreSQLConverter.GetItemByReferenceAsync(null!, 5, fallbackByReference: true);
            Assert.Null(result_Null);

            Building2DOccupancyData? result_WhiteSpace = await building2DOccupancyDataPostgreSQLConverter.GetItemByReferenceAsync("   ", 5, fallbackByReference: true);
            Assert.Null(result_WhiteSpace);
        }

        /// <summary>
        /// Verifies that <see cref="Building2DReferencedObjectPostgreSQLConverter{TBuilding2DReferencedObject, TUniqueObject}.GetItemsByReferenceAsync(string, int?, long?, bool, int, System.Threading.CancellationToken)"/>
        /// returns null when given a null or whitespace reference.
        /// </summary>
        [Fact]
        public async Task GetItemsByReferenceAsync_NullOrWhiteSpace_ReturnsNull()
        {
            Building2DOccupancyDataPostgreSQLConverter building2DOccupancyDataPostgreSQLConverter = new(null);
            List<Building2DOccupancyData>? result_Null = await building2DOccupancyDataPostgreSQLConverter.GetItemsByReferenceAsync(null!, 5, fallbackByReference: true);
            Assert.Null(result_Null);

            List<Building2DOccupancyData>? result_WhiteSpace = await building2DOccupancyDataPostgreSQLConverter.GetItemsByReferenceAsync("   ", 5, fallbackByReference: true);
            Assert.Null(result_WhiteSpace);
        }

        /// <summary>
        /// Verifies that <see cref="Building2DReferencedObjectPostgreSQLConverter{TBuilding2DReferencedObject, TUniqueObject}.GetItemsByReferencesAsync(IEnumerable{string}?, int?, long?, bool, IEnumerable{int}?, int, System.Threading.CancellationToken)"/>
        /// returns null when given null references.
        /// </summary>
        [Fact]
        public async Task GetItemsByReferencesAsync_NullReferences_ReturnsNull()
        {
            Building2DOccupancyDataPostgreSQLConverter building2DOccupancyDataPostgreSQLConverter = new(null);
            List<Building2DOccupancyData>? result = await building2DOccupancyDataPostgreSQLConverter.GetItemsByReferencesAsync((IEnumerable<string>?)null, 5, fallbackByReference: true);
            Assert.Null(result);
        }

        /// <summary>
        /// Verifies that <see cref="Building2DReferencedObjectPostgreSQLConverter{TBuilding2DReferencedObject, TUniqueObject}.GetItemsByReferencesAsync(Npgsql.NpgsqlConnection?, IEnumerable{string}?, int?, long?, bool, IEnumerable{int}?, int, System.Threading.CancellationToken)"/>
        /// returns null when connection is null.
        /// </summary>
        [Fact]
        public async Task GetItemsByReferencesAsync_NullConnection_ReturnsNull()
        {
            Building2DOccupancyDataPostgreSQLConverter building2DOccupancyDataPostgreSQLConverter = new(null);
            List<Building2DOccupancyData>? result = await building2DOccupancyDataPostgreSQLConverter.GetItemsByReferencesAsync(null, [], 5, fallbackByReference: true);
            Assert.Null(result);
        }

        /// <summary>
        /// Verifies that <see cref="Building2DReferencedObjectPostgreSQLConverter{TBuilding2DReferencedObject, TUniqueObject}.GetUniqueIdsByReferencesAsync(IEnumerable{string}?, int?, bool, int, System.Threading.CancellationToken)"/>
        /// returns null when given null references.
        /// </summary>
        [Fact]
        public async Task GetUniqueIdsByReferencesAsync_NullReferences_ReturnsNull()
        {
            Building2DOccupancyDataPostgreSQLConverter building2DOccupancyDataPostgreSQLConverter = new(null);
            HashSet<string>? result = await building2DOccupancyDataPostgreSQLConverter.GetUniqueIdsByReferencesAsync((IEnumerable<string>?)null, 5, fallbackByReference: true);
            Assert.Null(result);
        }

        /// <summary>
        /// Verifies that <see cref="Building2DReferencedObjectPostgreSQLConverter{TBuilding2DReferencedObject, TUniqueObject}.GetUniqueIdsByReferencesAsync(Npgsql.NpgsqlConnection?, IEnumerable{string}?, int?, bool, int, System.Threading.CancellationToken)"/>
        /// returns null when connection is null.
        /// </summary>
        [Fact]
        public async Task GetUniqueIdsByReferencesAsync_NullConnection_ReturnsNull()
        {
            Building2DOccupancyDataPostgreSQLConverter building2DOccupancyDataPostgreSQLConverter = new(null);
            HashSet<string>? result = await building2DOccupancyDataPostgreSQLConverter.GetUniqueIdsByReferencesAsync(null, [], 5, fallbackByReference: true);
            Assert.Null(result);
        }

        /// <summary>
        /// Verifies that the read half of replacing what a building holds answers an empty set, not an exception, when the table it reads has never been created.
        /// <para>This is the regression guard for a dead end rather than a slow path. <c>BuildingModelController.UpdateAsync</c> takes the identifiers it is about to supersede before writing and refuses the request when that read fails, while the write it guards is the only thing that creates the table - so a storage database whose table was dropped to regenerate the data from scratch answered <c>42P01: relation "building_model_component" does not exist</c> to every upload, permanently.</para>
        /// <para>Skipped by default: it opens a connection, so it needs <c>GIS_PostgreSQL_Storage.conf</c> pointing at a database. That conf resolves to a development database, so nothing measured here describes the deployed estate - only that the guard answers rather than throws.</para>
        /// </summary>
        [Fact(Skip = "Executes an integration query. Point GIS_PostgreSQL_Storage.conf at a database before running.")]
        public async Task GetUniqueIdsByReferencesAsync_MissingTable_ReturnsEmpty()
        {
            GISPostgreSQLConverterManager? gISPostgreSQLConverterManager = Create.GISPostgreSQLConverterManager();
            Assert.NotNull(gISPostgreSQLConverterManager);

            BuildingModelPostgreSQLConverter? buildingModelPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<BuildingModelPostgreSQLConverter>();
            Assert.NotNull(buildingModelPostgreSQLConverter);

            // The registered converter is the Component one, so the Envelope detail level names a table
            // of the same shape that nothing has ever written to - a missing table without dropping one.
            BuildingModelPostgreSQLConverter buildingModelPostgreSQLConverter_Envelope = new(buildingModelPostgreSQLConverter.ConnectionData, BuildingModelDetailLevel.Envelope);
            Assert.False(await DiGi.PostgreSQL.Query.TableExistsAsync(buildingModelPostgreSQLConverter_Envelope.ConnectionData, buildingModelPostgreSQLConverter_Envelope.TableName));

            HashSet<string>? uniqueIds = await buildingModelPostgreSQLConverter_Envelope.GetUniqueIdsByReferencesAsync(["272D6AAF-9E4B-9B0E-E053-CC2BA8C0B5EA"], 5);
            Assert.NotNull(uniqueIds);
            Assert.Empty(uniqueIds);
        }
    }
}
