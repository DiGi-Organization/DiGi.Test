using DiGi.Geometry.Spatial.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The first scratch county part of the building solar radiation facts. Only a partition key in the throwaway storage test database.
        /// </summary>
        private const int countyId_SolarRadiation_A = 990301;

        /// <summary>
        /// The second scratch county part of the building solar radiation facts, standing for a sibling polygon part of the same county.
        /// </summary>
        private const int countyId_SolarRadiation_B = 990302;

        /// <summary>
        /// The scratch county part of the building solar radiation size report.
        /// </summary>
        private const int countyId_SolarRadiation_Size = 990303;

        /// <summary>
        /// The scratch county part of the building solar radiation summary fact.
        /// </summary>
        private const int countyId_SolarRadiation_Summary = 990304;

        /// <summary>
        /// The number of hours of the solar reference year, the length of every surface's shading factors.
        /// </summary>
        private const int hourCount_SolarReferenceYear = 8760;

        /// <summary>
        /// Drops the scratch partitions of the building solar radiation facts on the storage test database. Called before writing and again in <c>finally</c>; the parent table is left in place, empty of scratch rows.
        /// </summary>
        /// <param name="connectionData_Storage">The storage test database.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task CleanupBuildingSolarRadiationScratchAsync(ConnectionData connectionData_Storage)
        {
            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            foreach (int countyId in new int[] { countyId_SolarRadiation_A, countyId_SolarRadiation_B, countyId_SolarRadiation_Size, countyId_SolarRadiation_Summary })
            {
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.BuildingSolarRadiation}_{countyId};");
            }
        }

        /// <summary>
        /// Executes one query and returns the first column of its first row.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to execute through.</param>
        /// <param name="commandText">The query to execute.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the value, or null for no row or a database null.</returns>
        private static async Task<object?> ScalarAsync(NpgsqlConnection npgsqlConnection, string commandText)
        {
            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);

            object? result = await npgsqlCommand.ExecuteScalarAsync();
            return result is DBNull ? null : result;
        }

        /// <summary>
        /// Builds synthetic quantized shading factors for one surface over the solar reference year, shaped like solved ones: 0 while the sun is down (a daylength model for 52° N), fully shaded while the sun is behind the surface, a ramp from shaded to open over the low-sun hours after sunrise and before sunset, and open around noon.
        /// <para>They are not solved factors. They stand in for them where only the run-length structure matters, as in the size report.</para>
        /// </summary>
        /// <param name="azimuth">The azimuth the surface faces, in degrees clockwise from north, or null for a roof.</param>
        /// <param name="shadedHours">How many hours after sunrise and before sunset the neighbours shade the surface.</param>
        /// <returns>The 8 760 quantized shading factors.</returns>
        private static byte[] ScratchShadingFactors(double? azimuth, double shadedHours)
        {
            byte[] result = new byte[hourCount_SolarReferenceYear];
            double latitude = 52.0 * Math.PI / 180;
            for (int hour = 0; hour < result.Length; hour++)
            {
                int day = hour / 24;
                double hourOfDay = (hour % 24) + 0.5;

                double declination = 23.44 * Math.PI / 180 * Math.Sin(2 * Math.PI * (284 + day + 1) / 365.0);
                double daylength = 2 * Math.Acos(Math.Clamp(-Math.Tan(latitude) * Math.Tan(declination), -1, 1)) * 12 / Math.PI;
                double sunrise = 12 - (daylength / 2);
                double sunset = 12 + (daylength / 2);
                if (hourOfDay <= sunrise || hourOfDay >= sunset)
                {
                    continue;
                }

                double sunAzimuth = 180 + (15 * (hourOfDay - 12));
                if (azimuth is not null && Math.Abs(((sunAzimuth - azimuth.Value + 540) % 360) - 180) > 90)
                {
                    result[hour] = byte.MaxValue;
                    continue;
                }

                double shadingFactor = Math.Max(0, 1 - (Math.Min(hourOfDay - sunrise, sunset - hourOfDay) / shadedHours));
                result[hour] = (byte)Math.Round(shadingFactor * byte.MaxValue, MidpointRounding.AwayFromZero);
            }

            return result;
        }

        /// <summary>
        /// Builds a scratch building solar radiation result with one wall per given azimuth and one roof, each carrying synthetic factors.
        /// </summary>
        /// <param name="reference">The reference of the scratch building.</param>
        /// <param name="buildingModelId">The id of the scratch building model.</param>
        /// <param name="weatherSourceId">The weather source id of the summary.</param>
        /// <param name="userEmail">The user email.</param>
        /// <param name="irradiationScale">A factor applied to every summary value, so that two writes for one key store different summaries.</param>
        /// <param name="azimuths">The azimuths of the walls.</param>
        /// <returns>The result.</returns>
        private static GIS.Classes.BuildingSolarRadiationResult ScratchBuildingSolarRadiationResult(string reference, long buildingModelId, string weatherSourceId, string? userEmail, double irradiationScale, params double[] azimuths)
        {
            List<GIS.Classes.SurfaceSolarRadiation> surfaceSolarRadiations = [];
            for (int i = 0; i <= azimuths.Length; i++)
            {
                double? azimuth = i < azimuths.Length ? azimuths[i] : null;
                Vector3D normal = azimuth is null ? new Vector3D(0, 0, 1) : new Vector3D(Math.Sin(azimuth.Value * Math.PI / 180), Math.Cos(azimuth.Value * Math.PI / 180), 0);
                double irradiation = (azimuth is null ? 1000 : 600) * irradiationScale;

                surfaceSolarRadiations.Add(new GIS.Classes.SurfaceSolarRadiation($"b1a5c1de-0000-4000-8000-{i:D12}", 10 + i, normal, 0.8, 0.6, irradiation, irradiation * 0.6, irradiation * 0.3, irradiation * 0.1, irradiation * 1.2, irradiation * (10 + i), ScratchShadingFactors(azimuth, 2 + i)));
            }

            return new GIS.Classes.BuildingSolarRadiationResult(reference, countyId_SolarRadiation_A, buildingModelId, 50.0, 1, GIS.Enums.WeatherSourceType.EPW, weatherSourceId, null, null, userEmail, new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero), surfaceSolarRadiations);
        }
    }
}
