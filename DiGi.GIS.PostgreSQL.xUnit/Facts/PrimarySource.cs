using System.Text.Json.Nodes;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the normalisation of a footprint's per-object provenance into the closed <c>primary_source</c> vocabulary, dataset by dataset, against the shapes the committed readers and fixtures produce (DiGi.GIS.PostgreSQL#125).
        /// <para>The vocabulary feeds figure R1 (the provenance diagram), so each mapping is pinned: an EUBUCCO <c>gov-*</c> source is the register and beats a weaker OpenStreetMap value in the same row; a DBSM <c>OSM</c> or <c>OSM+ESA</c> value is OpenStreetMap while <c>ESA</c> alone classifies to <c>other</c>; an Overture <c>sources</c> array answers from its first recognised entry while an unrecognised one is stepped over; the OSM reader's dataset key and the Microsoft reader's quadkey answer directly; a GHS-OBAT row, whose only evidence is the <c>overture_id</c> link, and a row with no provenance at all are both <c>other</c> - so the column is populated for every stored row. The fact needs no database.</para>
        /// </summary>
        [Fact]
        public void PrimarySource_FromProvenance()
        {
            // No provenance at all still answers a value, so the column can never be null.
            Assert.Equal(Constants.PrimarySource.Other, Query.PrimarySource(null));

            // DiGi.OSM: the reader stamps its dataset key on every footprint.
            JsonObject provenance_Osm = [];
            provenance_Osm["dataset"] = "osm";
            provenance_Osm["osm_type"] = "way";
            provenance_Osm["osm_id"] = 123456;
            Assert.Equal(Constants.PrimarySource.Osm, Query.PrimarySource(provenance_Osm));

            // Microsoft: no origin of its own - the quadkey the file was fetched under is the identity.
            JsonObject provenance_Microsoft = [];
            provenance_Microsoft["quadkey"] = "03210";
            provenance_Microsoft["capture"] = "2019-07";
            Assert.Equal(Constants.PrimarySource.Microsoft, Query.PrimarySource(provenance_Microsoft));

            // EUBUCCO: the geometry_source column of the committed sample (Prague, a licence exception region).
            JsonObject provenance_Eubucco = [];
            provenance_Eubucco["geometry_source"] = "gov-czechia-prague";
            Assert.Equal(Constants.PrimarySource.Register, Query.PrimarySource(provenance_Eubucco));

            // The strongest evidence wins among several source columns: the register produced the geometry.
            JsonObject provenance_Eubucco_Mixed = [];
            provenance_Eubucco_Mixed["geometry_source"] = "gov-germany-generic";
            provenance_Eubucco_Mixed["height_source"] = "osm";
            Assert.Equal(Constants.PrimarySource.Register, Query.PrimarySource(provenance_Eubucco_Mixed));

            // DBSM: the three values of the committed sample.
            JsonObject provenance_Dbsm_Osm = [];
            provenance_Dbsm_Osm["source"] = "OSM";
            Assert.Equal(Constants.PrimarySource.Osm, Query.PrimarySource(provenance_Dbsm_Osm));

            JsonObject provenance_Dbsm_Mixed = [];
            provenance_Dbsm_Mixed["source"] = "OSM+ESA";
            Assert.Equal(Constants.PrimarySource.Osm, Query.PrimarySource(provenance_Dbsm_Mixed));

            // ESA alone is not Esri imagery and names none of the categories.
            JsonObject provenance_Dbsm_Esa = [];
            provenance_Dbsm_Esa["source"] = "ESA";
            Assert.Equal(Constants.PrimarySource.Other, Query.PrimarySource(provenance_Dbsm_Esa));

            // Overture: the published sources array, first recognised entry in order; an entry of an
            // unrecognised dataset is stepped over rather than ending the search.
            JsonObject provenance_Overture_Osm = [];
            provenance_Overture_Osm["id"] = "08f3";
            JsonArray sources_Osm = [];
            sources_Osm.Add(new JsonObject { ["dataset"] = "osm", ["record"] = "r1" });
            sources_Osm.Add(new JsonObject { ["dataset"] = "esri", ["record"] = "r2" });
            provenance_Overture_Osm["sources"] = sources_Osm;
            Assert.Equal(Constants.PrimarySource.Osm, Query.PrimarySource(provenance_Overture_Osm));

            JsonObject provenance_Overture_Esri = [];
            provenance_Overture_Esri["id"] = "08f4";
            JsonArray sources_Esri = [];
            sources_Esri.Add(new JsonObject { ["dataset"] = "gsa" });
            sources_Esri.Add(new JsonObject { ["dataset"] = "esri", ["record"] = "r1" });
            provenance_Overture_Esri["sources"] = sources_Esri;
            Assert.Equal(Constants.PrimarySource.Esri, Query.PrimarySource(provenance_Overture_Esri));

            JsonObject provenance_Overture_Microsoft = [];
            provenance_Overture_Microsoft["id"] = "08f5";
            JsonArray sources_Microsoft = [];
            sources_Microsoft.Add(new JsonObject { ["dataset"] = "microsoft", ["record"] = "r1" });
            provenance_Overture_Microsoft["sources"] = sources_Microsoft;
            Assert.Equal(Constants.PrimarySource.Microsoft, Query.PrimarySource(provenance_Overture_Microsoft));

            // GHS-OBAT: the link key is no origin at all.
            JsonObject provenance_GhsObat = [];
            provenance_GhsObat["overture_id"] = "08f6";
            Assert.Equal(Constants.PrimarySource.Other, Query.PrimarySource(provenance_GhsObat));

            // An empty object behaves like no provenance.
            Assert.Equal(Constants.PrimarySource.Other, Query.PrimarySource(new JsonObject()));

            // The vocabularies of the real releases (DiGi.GIS.PostgreSQL#133), as read from the files downloaded
            // for #126. Overture 2026-10-21.0 names OpenStreetMap in full and Microsoft as "Microsoft ML Buildings".
            JsonObject provenance_Overture_OpenStreetMap = [];
            provenance_Overture_OpenStreetMap["sources"] = new JsonArray(new JsonObject { ["dataset"] = "OpenStreetMap" });
            Assert.Equal(Constants.PrimarySource.Osm, Query.PrimarySource(provenance_Overture_OpenStreetMap));

            JsonObject provenance_Overture_MicrosoftMl = [];
            provenance_Overture_MicrosoftMl["sources"] = new JsonArray(new JsonObject { ["dataset"] = "Microsoft ML Buildings" });
            Assert.Equal(Constants.PrimarySource.Microsoft, Query.PrimarySource(provenance_Overture_MicrosoftMl));

            // EUBUCCO v0.2: gov-poland, msft and osm; geometry_source decides alone, even when an attribute column
            // carries stronger evidence.
            JsonObject provenance_Eubucco_Msft = [];
            provenance_Eubucco_Msft["geometry_source"] = "msft";
            provenance_Eubucco_Msft["type_source"] = "gov-poland";
            provenance_Eubucco_Msft["height_source"] = "estimated";
            Assert.Equal(Constants.PrimarySource.Microsoft, Query.PrimarySource(provenance_Eubucco_Msft));

            JsonObject provenance_Eubucco_Osm = [];
            provenance_Eubucco_Osm["geometry_source"] = "osm";
            provenance_Eubucco_Osm["type_source"] = "osm";
            provenance_Eubucco_Osm["height_source"] = "estimated";
            Assert.Equal(Constants.PrimarySource.Osm, Query.PrimarySource(provenance_Eubucco_Osm));

            // An unclassifiable geometry_source falls back to weighing the other columns.
            JsonObject provenance_Eubucco_Unknown = [];
            provenance_Eubucco_Unknown["geometry_source"] = "nan";
            provenance_Eubucco_Unknown["height_source"] = "osm";
            Assert.Equal(Constants.PrimarySource.Osm, Query.PrimarySource(provenance_Eubucco_Unknown));

            // DBSM R2025: eub (EUBUCCO v0.1's authoritative part), msb (Microsoft Buildings), osm.
            JsonObject provenance_Dbsm_Eub = [];
            provenance_Dbsm_Eub["source"] = "eub";
            Assert.Equal(Constants.PrimarySource.Register, Query.PrimarySource(provenance_Dbsm_Eub));

            JsonObject provenance_Dbsm_Msb = [];
            provenance_Dbsm_Msb["source"] = "msb";
            Assert.Equal(Constants.PrimarySource.Microsoft, Query.PrimarySource(provenance_Dbsm_Msb));

            // The short codes match exactly, never as substrings.
            JsonObject provenance_Substring = [];
            provenance_Substring["source"] = "eubmsbx";
            Assert.Equal(Constants.PrimarySource.Other, Query.PrimarySource(provenance_Substring));
        }
    }
}
