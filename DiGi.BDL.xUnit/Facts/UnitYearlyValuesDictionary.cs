using DiGi.BDL.Classes;
using DiGi.BDL.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace DiGi.BDL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The pages of the BDL <c>data/by-variable</c> endpoint convert into one <see cref="UnitYearlyValues"/> per unit, holding the one variable with its measure unit (DiGi-Organization/DiGi.GIS.PostgreSQL#149). The pages are in the shape BDL answered on 2026-11-19; a unit spread over two pages is joined, a result without an id or values is skipped, and null or empty input gives an empty dictionary.
        /// </summary>
        [Fact]
        public void UnitYearlyValuesDictionary_Pages()
        {
            List<JsonObject> jsonObjects =
            [
                (JsonObject)JsonNode.Parse("""
                    {"totalRecords":3,"page":0,"pageSize":2,"variableId":1723518,"measureUnitId":1,
                     "links":{"next":"https://bdl.stat.gov.pl/api/v1/data/by-variable/1723518?page=1"},
                     "results":[
                       {"id":"071412865011","name":"M.st.Warszawa od 2002","values":[{"year":"2021","val":829426,"attrId":1}]},
                       {"id":"071412865028","name":"Bemowo - dzielnica","values":[{"year":"2021","val":53248,"attrId":1}]}]}
                    """)!,
                (JsonObject)JsonNode.Parse("""
                    {"totalRecords":3,"page":1,"pageSize":2,"variableId":1723518,"measureUnitId":1,
                     "results":[
                       {"id":"071412865028","name":"Bemowo - dzielnica","values":[{"year":"2020","val":1,"attrId":1}]},
                       {"id":null,"name":"no id","values":[{"year":"2021","val":5,"attrId":1}]},
                       {"id":"071412865048","name":"Mokotów - dzielnica","values":null}]}
                    """)!
            ];

            Dictionary<string, UnitYearlyValues> unitYearlyValuesDictionary = Create.UnitYearlyValuesDictionary(Variable.inhabited_dwellings_total_census_2021, jsonObjects);

            Assert.Equal(["071412865011", "071412865028"], unitYearlyValuesDictionary.Keys.OrderBy(x => x));

            UnitYearlyValues unitYearlyValues_Warsaw = unitYearlyValuesDictionary["071412865011"];
            Assert.Equal("071412865011", unitYearlyValues_Warsaw.unitId);
            Assert.Equal("M.st.Warszawa od 2002", unitYearlyValues_Warsaw.unitName);
            YearlyValues yearlyValues_Warsaw = Assert.Single(unitYearlyValues_Warsaw.results!);
            Assert.Equal((int)Variable.inhabited_dwellings_total_census_2021, yearlyValues_Warsaw.id);
            Assert.Equal(1, yearlyValues_Warsaw.measureUnitId);
            YearlyValue yearlyValue_Warsaw = Assert.Single(yearlyValues_Warsaw.values!);
            Assert.Equal("2021", yearlyValue_Warsaw.year);
            Assert.Equal(829426, yearlyValue_Warsaw.val);

            YearlyValues yearlyValues_Bemowo = Assert.Single(unitYearlyValuesDictionary["071412865028"].results!);
            Assert.Equal(["2020", "2021"], yearlyValues_Bemowo.values!.Select(x => x.year).OrderBy(x => x));

            Assert.Empty(Create.UnitYearlyValuesDictionary(Variable.inhabited_dwellings_total_census_2021, null));
            Assert.Empty(Create.UnitYearlyValuesDictionary(Variable.inhabited_dwellings_total_census_2021, []));
        }
    }
}
