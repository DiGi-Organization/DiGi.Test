using DiGi.Core.xUnit;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Measures how many of a county's buildings the building data update can never reach, and why.
        /// <para>The update walks subdivision references and asks for the buildings of <c>(reference.CountyId, reference.Id)</c>. A building is therefore reached only when its own <c>subdivision_id</c> belongs to a subdivision whose reference carries that same county identifier. Anything else is invisible to every run, however many times it is repeated.</para>
        /// <para>Three separate ways that can fail, and this separates them: the building names no subdivision at all, the subdivision it names has no parent county on record, or the subdivision it names is filed under a different polygon part of the county than the building is. The last one matters because a county with disconnected territory is one row per part, each with its own identifier.</para>
        /// <para>Writes its findings to the reports folder rather than asserting a threshold - it is a measurement of stored data, not a property of the code.</para>
        /// <para><b>It measures the local database only.</b> The conf points at a partial, not-current test copy, so these figures do not describe the deployed estate and must never be read as if they did - the two hosts disagree in ways that invert conclusions. For production, ask the API: <c>gis/buildingdata/coveragebycountyid</c>.</para>
        /// <para>Skipped by default: it reads the whole administrative table and every building of the counties named.</para>
        /// </summary>
        [Fact(Skip = "Diagnostic. Reads whole counties. Point GIS_PostgreSQL_Main.conf at a database before running.")]
        public async Task BuildingData_UnreachableBuildings_Diagnostic()
        {
            GISPostgreSQLConverterManager? gISPostgreSQLConverterManager = Create.GISPostgreSQLConverterManager();
            Assert.NotNull(gISPostgreSQLConverterManager);

            AdministrativeAreal2DPostgreSQLConverter? administrativeAreal2DPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<AdministrativeAreal2DPostgreSQLConverter>();
            Assert.NotNull(administrativeAreal2DPostgreSQLConverter);

            Building2DPostgreSQLConverter? building2DPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<Building2DPostgreSQLConverter>();
            Assert.NotNull(building2DPostgreSQLConverter);

            List<AdministrativeAreal2DReference>? administrativeAreal2DReferences = await administrativeAreal2DPostgreSQLConverter.GetAdministrativeAreal2DReferencesByAdministrativeArealTypeAsync(AdministrativeArealType.Subdivision, commandTimeout: 600);
            Assert.NotNull(administrativeAreal2DReferences);

            StringBuilder stringBuilder = new();

            int subdivisionCount_WithoutCounty = administrativeAreal2DReferences.Count(x => x.CountyId is null);
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"Subdivision references: {administrativeAreal2DReferences.Count}");
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"  without a parent county: {subdivisionCount_WithoutCounty}");
            stringBuilder.AppendLine();

            // The set the update actually visits, keyed the way it keys it.
            HashSet<(int, int)> visited = [];
            foreach (AdministrativeAreal2DReference administrativeAreal2DReference in administrativeAreal2DReferences)
            {
                if (administrativeAreal2DReference.CountyId is int countyId_Subdivision)
                {
                    visited.Add((countyId_Subdivision, administrativeAreal2DReference.Id));
                }
            }

            // Every subdivision identifier that exists, whichever county it is filed under.
            HashSet<int> subdivisionIds_All = [.. administrativeAreal2DReferences.Select(x => x.Id)];

            HashSet<int> subdivisionIds_WithoutCounty = [.. administrativeAreal2DReferences.Where(x => x.CountyId is null).Select(x => x.Id)];

            stringBuilder.AppendLine("county    buildings  unreachable   no_subdiv  subdiv_no_county  subdiv_other_county  subdiv_unknown");

            int[] countyIds = [55417, 22138, 80295, 5, 4816, 91005];
            foreach (int countyId in countyIds)
            {
                List<Building2DReference>? building2DReferences = await building2DPostgreSQLConverter.GetBuilding2DReferencesByCountyIdAsync(countyId, commandTimeout: 600);
                if (building2DReferences is null)
                {
                    stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"{countyId,-9}  (not read)");
                    continue;
                }

                long unreachable = 0;
                long noSubdivision = 0;
                long subdivisionWithoutCounty = 0;
                long subdivisionOtherCounty = 0;
                long subdivisionUnknown = 0;

                foreach (Building2DReference building2DReference in building2DReferences)
                {
                    if (building2DReference.SubdivisionId is not int subdivisionId)
                    {
                        unreachable++;
                        noSubdivision++;
                        continue;
                    }

                    if (visited.Contains((countyId, subdivisionId)))
                    {
                        continue;
                    }

                    unreachable++;

                    if (!subdivisionIds_All.Contains(subdivisionId))
                    {
                        subdivisionUnknown++;
                    }
                    else if (subdivisionIds_WithoutCounty.Contains(subdivisionId))
                    {
                        subdivisionWithoutCounty++;
                    }
                    else
                    {
                        subdivisionOtherCounty++;
                    }
                }

                // Cross-check: the same figure taken straight from the database rather than from the references.
                // The two disagreeing would mean the reference reader is dropping a column.
                long count_WithoutSubdivision = await building2DPostgreSQLConverter.GetCountWithoutSubdivisionAsync(countyId, 600);
                stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"{countyId,-9} {building2DReferences.Count,10} {unreachable,12} {noSubdivision,11} {subdivisionWithoutCounty,17} {subdivisionOtherCounty,20} {subdivisionUnknown,15}   GetCountWithoutSubdivisionAsync={count_WithoutSubdivision}");
            }

            string? directory = Assembly.GetExecutingAssembly().ReportsDirectory();
            Assert.False(string.IsNullOrWhiteSpace(directory));

            string path = Path.Combine(directory!, "BuildingData_UnreachableBuildings.txt");
            File.WriteAllText(path, stringBuilder.ToString());

            Assert.True(File.Exists(path));
        }
    }
}
