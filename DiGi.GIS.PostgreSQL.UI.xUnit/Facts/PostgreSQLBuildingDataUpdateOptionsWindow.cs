using DiGi.Core.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the building data options window can be built and that it works on a copy of the options it is given, without a database or a running application.
        /// <para>The window is the only way a run can be scoped, and a run left unscoped walks every subdivision in the country. A window whose markup failed to parse, or whose constructor threw on the county list, would not be found until someone opened it to scope a run and could not.</para>
        /// <para>What the constructor does is what this exercises: it parses the markup, wires the item naming of both lists, fills and sorts the counties, and restores the selection from the options. A multi-part county is included because two of its pieces share a code and a name and are told apart only by the identifier the naming callback appends.</para>
        /// <para><b>It does not check what the window looks like.</b> A window is laid out by the handle it gets when it is shown, so measuring one that is never shown reports nothing, and showing one during a test run would put a dialog on screen. The controls are private to the window, so the selection they end up holding cannot be read from here either - that the dialog behaves correctly on screen is still something a person has to look at once.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataUpdateOptionsWindow_Construction()
        {
            Exception? exception = null;
            bool countiesCarried = false;
            bool updateTypesCarried = false;
            bool commandTimeoutCarried = false;
            bool copied = false;

            PostgreSQLBuildingDataUpdateOptions postgreSQLBuildingDataUpdateOptions = new()
            {
                BuildingDataUpdateTypes = [BuildingDataUpdateType.General, BuildingDataUpdateType.Database],
                CountyIds = [4816],
                CommandTimeout = 600
            };

            Thread thread = new(() =>
            {
                try
                {
                    List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
                    [
                        new() { Id = 55417, Code = "1465", Name = "m. St. Warszawa", AdministrativeArealType = AdministrativeArealType.County },
                        new() { Id = 4816, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = AdministrativeArealType.County },
                        // Two pieces of one multi-part county: same code and name, told apart only by the identifier.
                        new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County },
                        new() { Id = 22139, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
                    ];

                    PostgreSQLBuildingDataUpdateOptionsWindow postgreSQLBuildingDataUpdateOptionsWindow = new(postgreSQLBuildingDataUpdateOptions, administrativeAreal2DReferences);

                    PostgreSQLBuildingDataUpdateOptions postgreSQLBuildingDataUpdateOptions_Held = postgreSQLBuildingDataUpdateOptionsWindow.PostgreSQLBuildingDataUpdateOptions;

                    // Until OK is pressed the window holds what it was given.
                    countiesCarried = postgreSQLBuildingDataUpdateOptions_Held.CountyIds is HashSet<int> countyIds && countyIds.Count == 1 && countyIds.Contains(4816);
                    updateTypesCarried = postgreSQLBuildingDataUpdateOptions_Held.BuildingDataUpdateTypes is HashSet<BuildingDataUpdateType> buildingDataUpdateTypes && buildingDataUpdateTypes.Count == 2 && buildingDataUpdateTypes.Contains(BuildingDataUpdateType.General);
                    commandTimeoutCarried = postgreSQLBuildingDataUpdateOptions_Held.CommandTimeout == 600;

                    // A cancelled dialog has to leave the caller's options alone, which only holds if the window
                    // took a copy rather than a reference.
                    copied = !ReferenceEquals(postgreSQLBuildingDataUpdateOptions, postgreSQLBuildingDataUpdateOptions_Held);
                    postgreSQLBuildingDataUpdateOptions_Held.CommandTimeout = 30;
                }
                catch (Exception exception_Temp)
                {
                    exception = exception_Temp;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(exception);
            Assert.True(updateTypesCarried);
            Assert.True(countiesCarried);
            Assert.True(commandTimeoutCarried);
            Assert.True(copied);

            // The caller's own instance is untouched by the edit made to the window's copy above.
            Assert.Equal(600, postgreSQLBuildingDataUpdateOptions.CommandTimeout);
        }

        /// <summary>
        /// Verifies that the settings of the building data window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#67): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>The two members no control shows - <c>Radiuses</c> and <c>Years</c> - are the point of this one: they travel in the file as the exporting copy held them, and after an import OK writes the file's values into the window, so the table's shape follows the settings. The import side runs in a window constructed with no options, so every value asserted can only have come from the file.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataUpdateOptionsWindow_SettingsRoundTrip()
        {
            PostgreSQLBuildingDataUpdateOptions postgreSQLBuildingDataUpdateOptions = new()
            {
                BuildingDataUpdateTypes = [BuildingDataUpdateType.General],
                CountyIds = [1001, 1003],
                CommandTimeout = 900,
                Radiuses = [200, 400],
                Years = new Range<int>(1990, 2020),
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            PostgreSQLBuildingDataUpdateOptions? postgreSQLBuildingDataUpdateOptions_Read = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingDataUpdateOptionsWindow postgreSQLBuildingDataUpdateOptionsWindow = new(postgreSQLBuildingDataUpdateOptions, OpenBuildingImportOptionsWindowCounties(5));

                Expect(!ReferenceEquals(postgreSQLBuildingDataUpdateOptions, postgreSQLBuildingDataUpdateOptionsWindow.PostgreSQLBuildingDataUpdateOptions), "the window holds the caller's options instead of a copy");

                if (postgreSQLBuildingDataUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingDataUpdateOptions? postgreSQLBuildingDataUpdateOptions_Exported, out string? message_Export) && postgreSQLBuildingDataUpdateOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(postgreSQLBuildingDataUpdateOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (PostgreSQLBuildingDataUpdateOptionsWindow.TryReadSettings(json, out string? message_Read) is not PostgreSQLBuildingDataUpdateOptions postgreSQLBuildingDataUpdateOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: what it ends up holding can only be the file's - including the two
                // members no control shows, which reach OK through what SetControls was given.
                PostgreSQLBuildingDataUpdateOptionsWindow postgreSQLBuildingDataUpdateOptionsWindow_Imported = new(null, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLBuildingDataUpdateOptionsWindow_Imported.SetControls(postgreSQLBuildingDataUpdateOptions_Imported);

                if (postgreSQLBuildingDataUpdateOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    postgreSQLBuildingDataUpdateOptions_Read = postgreSQLBuildingDataUpdateOptionsWindow_Imported.PostgreSQLBuildingDataUpdateOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(postgreSQLBuildingDataUpdateOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }
            });

            void Expect(bool condition, string failure)
            {
                if (!condition)
                {
                    failures.Add(failure);
                }
            }

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));

            Assert.False(string.IsNullOrWhiteSpace(json));
            Assert.NotNull(postgreSQLBuildingDataUpdateOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the update types subset, the county subset, the timeout - and the two
            // members no control shows, which the file carried over the window's own defaults.
            Assert.NotNull(postgreSQLBuildingDataUpdateOptions_Read!.BuildingDataUpdateTypes);
            Assert.True(postgreSQLBuildingDataUpdateOptions_Read!.BuildingDataUpdateTypes!.SetEquals([BuildingDataUpdateType.General]));
            Assert.NotNull(postgreSQLBuildingDataUpdateOptions_Read!.CountyIds);
            Assert.True(postgreSQLBuildingDataUpdateOptions_Read!.CountyIds!.SetEquals([1001, 1003]));
            Assert.Equal(900, postgreSQLBuildingDataUpdateOptions_Read!.CommandTimeout);
            Assert.NotNull(postgreSQLBuildingDataUpdateOptions_Read!.Radiuses);
            Assert.True(postgreSQLBuildingDataUpdateOptions_Read!.Radiuses!.SequenceEqual([200d, 400d]));
            Assert.NotNull(postgreSQLBuildingDataUpdateOptions_Read!.Years);
            Assert.Equal(1990, postgreSQLBuildingDataUpdateOptions_Read!.Years!.Min);
            Assert.Equal(2020, postgreSQLBuildingDataUpdateOptions_Read!.Years!.Max);

            // The window's own options are what the caller gave them: the export read writes nothing.
            Assert.Equal(900, postgreSQLBuildingDataUpdateOptions.CommandTimeout);
            Assert.NotNull(postgreSQLBuildingDataUpdateOptions.Radiuses);
            Assert.True(postgreSQLBuildingDataUpdateOptions.Radiuses!.SequenceEqual([200d, 400d]));
        }

        /// <summary>
        /// Verifies the edges of the settings file of the building data window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#67): an unfinished window still exports - no update type checked as an empty set, never "every type"; no county as no county list - while a timeout box that does not hold a whole number is refused with the reason OK gives, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataUpdateOptionsWindow_SettingsRefusals()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingDataUpdateOptionsWindow postgreSQLBuildingDataUpdateOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(5));

                // Nothing checked: an empty set - matching none - is what the export writes, never "every type",
                // and OK is what refuses it on read-back.
                postgreSQLBuildingDataUpdateOptionsWindow.ListBoxControl_UpdateTypes.SelectItems<BuildingDataUpdateType>(x => false);
                Expect(postgreSQLBuildingDataUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingDataUpdateOptions? postgreSQLBuildingDataUpdateOptions_None, out string? message_None) && postgreSQLBuildingDataUpdateOptions_None is not null, $"an export with nothing checked was refused: {message_None}");
                Expect(postgreSQLBuildingDataUpdateOptions_None?.BuildingDataUpdateTypes is HashSet<BuildingDataUpdateType> buildingDataUpdateTypes_Empty && buildingDataUpdateTypes_Empty.Count == 0, "nothing checked was written as every type");
                Expect(!postgreSQLBuildingDataUpdateOptionsWindow.TryGetOptions(out string? message_None_Ok) && message_None_Ok is not null && message_None_Ok.Contains("update type", StringComparison.OrdinalIgnoreCase), "a window with no update type passed OK");

                // A timeout that is not a whole number could not be written back as typed; -1 is a whole
                // number and is OK's to refuse on read-back.
                postgreSQLBuildingDataUpdateOptionsWindow.SetControls(new PostgreSQLBuildingDataUpdateOptions());
                postgreSQLBuildingDataUpdateOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => true);
                postgreSQLBuildingDataUpdateOptionsWindow.TextBoxControl_CommandTimeout.Value = "half";
                Expect(!postgreSQLBuildingDataUpdateOptionsWindow.TryReadOptions(false, out _, out string? message_Half) && message_Half is not null && message_Half.Contains("timeout", StringComparison.OrdinalIgnoreCase), "a timeout that is not a number was exported");

                postgreSQLBuildingDataUpdateOptionsWindow.SetControls(new PostgreSQLBuildingDataUpdateOptions());
                postgreSQLBuildingDataUpdateOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => true);
                postgreSQLBuildingDataUpdateOptionsWindow.TextBoxControl_CommandTimeout.Value = "-1";
                Expect(postgreSQLBuildingDataUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingDataUpdateOptions? postgreSQLBuildingDataUpdateOptions_Negative, out string? message_Negative) && postgreSQLBuildingDataUpdateOptions_Negative is not null, $"a negative timeout was refused by the export: {message_Negative}");
                Expect(!postgreSQLBuildingDataUpdateOptionsWindow.TryGetOptions(out string? message_Negative_Ok) && message_Negative_Ok is not null && message_Negative_Ok.Contains("timeout", StringComparison.OrdinalIgnoreCase), "a negative timeout passed OK");

                // No county: written as no county list, refused by OK - and the refused reads wrote nothing.
                postgreSQLBuildingDataUpdateOptionsWindow.SetControls(new PostgreSQLBuildingDataUpdateOptions());
                Expect(postgreSQLBuildingDataUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingDataUpdateOptions? postgreSQLBuildingDataUpdateOptions_NoCounty, out string? message_NoCounty) && postgreSQLBuildingDataUpdateOptions_NoCounty is not null, $"an export without a county was refused: {message_NoCounty}");
                Expect(postgreSQLBuildingDataUpdateOptions_NoCounty?.CountyIds is null, "an empty county selection was written as a scope");
                Expect(!postgreSQLBuildingDataUpdateOptionsWindow.TryGetOptions(out string? message_NoCounty_Ok) && message_NoCounty_Ok is not null && message_NoCounty_Ok.Contains("county", StringComparison.OrdinalIgnoreCase), "an empty county selection passed OK");

                Expect(postgreSQLBuildingDataUpdateOptionsWindow.PostgreSQLBuildingDataUpdateOptions.CommandTimeout == 600, "a refused read wrote the timeout");
                Expect(postgreSQLBuildingDataUpdateOptionsWindow.PostgreSQLBuildingDataUpdateOptions.CountyIds is null, "a refused read wrote the county list");
            });

            void Expect(bool condition, string failure)
            {
                if (!condition)
                {
                    failures.Add(failure);
                }
            }

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));

            string? json_Self = Core.Convert.ToSystem_String(new PostgreSQLBuildingDataUpdateOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            PostgreSQLBuildingDataUpdateOptions? postgreSQLBuildingDataUpdateOptions_Self = PostgreSQLBuildingDataUpdateOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            PostgreSQLBuildingDataUpdateOptions? postgreSQLBuildingDataUpdateOptions_Foreign = PostgreSQLBuildingDataUpdateOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            PostgreSQLBuildingDataUpdateOptions? postgreSQLBuildingDataUpdateOptions_NotJson = PostgreSQLBuildingDataUpdateOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            PostgreSQLBuildingDataUpdateOptions? postgreSQLBuildingDataUpdateOptions_Several = PostgreSQLBuildingDataUpdateOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(postgreSQLBuildingDataUpdateOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(postgreSQLBuildingDataUpdateOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(postgreSQLBuildingDataUpdateOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(postgreSQLBuildingDataUpdateOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }
    }
}
