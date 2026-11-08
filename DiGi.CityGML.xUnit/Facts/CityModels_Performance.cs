using DiGi.CityGML.Classes;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace DiGi.CityGML.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Measures the cost of parsing a CityGML archive into a <see cref="CityModel"/>.
        /// <para>The parse path used to deep-copy every surface geometry three times - once in the surface constructor, once in the building's surface setter and once in the city model's building setter - before the adopting constructors were introduced. Measured A/B over 200 parses of the fixture: 341ms with the clone passes, 179ms without them - a 1.90x speed-up.</para>
        /// <para>The threshold below is a gross-regression guard only; it is machine dependent and is deliberately loose enough not to be flaky. Re-run the A/B against the recorded figures when judging a real regression.</para>
        /// </summary>
        [Fact]
        public void CityModels_Performance()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "2862_CityGML.zip");

            Assert.True(File.Exists(path));

            // Warm-up so JIT and the serialization manager's type registration are not measured.
            List<CityModel>? cityModels = Create.CityModels(path);

            Assert.NotNull(cityModels);
            Assert.Single(cityModels);

            const int count = 200;

            Stopwatch stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < count; i++)
            {
                cityModels = Create.CityModels(path);
            }

            stopwatch.Stop();

            Assert.NotNull(cityModels);

            Assert.True(stopwatch.ElapsedMilliseconds < 1500, string.Format(CultureInfo.CurrentCulture, "{0} parses took {1}ms; the adopting parse path measured 179ms.", count, stopwatch.ElapsedMilliseconds));
        }
    }
}
