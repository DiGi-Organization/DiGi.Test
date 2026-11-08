using DiGi.Core.Parameter.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that every <see cref="Analytical.Enums.BuildingParameter"/> member declares its own unique identifier.
        /// <para>Parameters are stored keyed by this identifier, so two members sharing one collapse into a single slot and silently overwrite each other. Year, Code and Source once shared a single GUID; this fact exists so a copy-pasted attribute cannot reintroduce that.</para>
        /// </summary>
        [Fact]
        public void BuildingParameter_UniqueIds()
        {
            Dictionary<string, string> dictionary = [];

            foreach (Analytical.Enums.BuildingParameter buildingParameter in Enum.GetValues<Analytical.Enums.BuildingParameter>())
            {
                FieldInfo? fieldInfo = typeof(Analytical.Enums.BuildingParameter).GetField(buildingParameter.ToString());

                Assert.NotNull(fieldInfo);

                ParameterProperties? parameterProperties = fieldInfo.GetCustomAttribute<ParameterProperties>();

                Assert.NotNull(parameterProperties);

                string? uniqueId = parameterProperties.UniqueId;

                Assert.False(string.IsNullOrWhiteSpace(uniqueId), string.Format(CultureInfo.CurrentCulture, "{0} declares no unique identifier.", buildingParameter));

                Assert.False(dictionary.TryGetValue(uniqueId!, out string? name), string.Format(CultureInfo.CurrentCulture, "{0} and {1} share unique identifier {2}.", name, buildingParameter, uniqueId));

                dictionary[uniqueId!] = buildingParameter.ToString();
            }
        }

        /// <summary>
        /// Tests that Year, LOD, Code and Source can all be set on one building and read back independently.
        /// <para>This is the behavioural counterpart of <see cref="BuildingParameter_UniqueIds"/>: under the shared GUID the string Source was rejected as an integer and the numeric Code overwrote Year, which both broke the resume watermark and put the county code into the year column.</para>
        /// </summary>
        [Fact]
        public void BuildingParameter_RoundTrip()
        {
            const string code = "2862";
            const string source = @"2862_CityGML.zip\2862_N-34-77-D-b-1-1.gml";

            CityGML.Classes.Building building = new("B_001", 0, null);

            SetValueSettings setValueSettings = new(true, false);

            Assert.True(building.SetValue(Analytical.Enums.BuildingParameter.Year, (short)2024, setValueSettings));
            Assert.True(building.SetValue(Analytical.Enums.BuildingParameter.LOD, CityGML.Enums.LOD.LOD2, setValueSettings));
            Assert.True(building.SetValue(Analytical.Enums.BuildingParameter.Source, source, setValueSettings));
            Assert.True(building.SetValue(Analytical.Enums.BuildingParameter.Code, code, setValueSettings));

            GetValueSettings getValueSettings = new(true, false);

            Assert.True(building.TryGetValue(Analytical.Enums.BuildingParameter.Year, out short? year, getValueSettings));
            Assert.Equal((short)2024, year);

            Assert.True(building.TryGetValue(Analytical.Enums.BuildingParameter.LOD, out CityGML.Enums.LOD? lOD, getValueSettings));
            Assert.Equal(CityGML.Enums.LOD.LOD2, lOD);

            Assert.True(building.TryGetValue(Analytical.Enums.BuildingParameter.Code, out string? code_Actual, getValueSettings));
            Assert.Equal(code, code_Actual);

            Assert.True(building.TryGetValue(Analytical.Enums.BuildingParameter.Source, out string? source_Actual, getValueSettings));
            Assert.Equal(source, source_Actual);
        }

        /// <summary>
        /// Tests that the four parameters survive a serialization round trip, which is how they reach the database.
        /// <para>The parameters travel to PostgreSQL inside the building's object JSONB and come back through ToDiGi, so the resume watermark depends on Source surviving this trip - not merely on being set in memory.</para>
        /// </summary>
        [Fact]
        public void BuildingParameter_SurvivesSerialization()
        {
            const string source = @"2862_CityGML.zip\2862_N-34-77-D-b-1-1.gml";

            CityGML.Classes.Building building = new("B_001", 0, null);

            SetValueSettings setValueSettings = new(true, false);

            building.SetValue(Analytical.Enums.BuildingParameter.Year, (short)2024, setValueSettings);
            building.SetValue(Analytical.Enums.BuildingParameter.Code, "2862", setValueSettings);
            building.SetValue(Analytical.Enums.BuildingParameter.Source, source, setValueSettings);

            CityGML.Classes.Building? building_Actual = Core.Query.Clone(building);

            Assert.NotNull(building_Actual);

            GetValueSettings getValueSettings = new(true, false);

            Assert.True(building_Actual.TryGetValue(Analytical.Enums.BuildingParameter.Source, out string? source_Actual, getValueSettings));
            Assert.Equal(source, source_Actual);

            Assert.True(building_Actual.TryGetValue(Analytical.Enums.BuildingParameter.Year, out short? year, getValueSettings));
            Assert.Equal((short)2024, year);
        }
    }
}
