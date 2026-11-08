using System;
using System.Linq;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the functionality of the <see cref="Core.Classes.IndexedDoubles"/> class, verifying its ability to handle random data population, copy construction, string serialization via <c>ToSystem_String</c>, and restoration through conversion.
        /// </summary>
        [Fact]
        public void IndexDoubles()
        {
            Core.Classes.IndexedDoubles indexDoubles_1 = [];

            Random random = new();

            while (random.NextDouble() < 0.4)
            {
                indexDoubles_1.Add(random.Next(0, 100), random.NextDouble());
            }

            Core.Classes.IndexedDoubles indexDoubles_2 = new(indexDoubles_1);

            string? @string_1 = indexDoubles_1.ToSystem_String();
            string? @string_2 = indexDoubles_2.ToSystem_String();

            Assert.Equal(string_1, string_2);

            Core.Classes.IndexedDoubles? indexDoubles_3 = Convert.ToDiGi<Core.Classes.IndexedDoubles>(string_1)?.FirstOrDefault();
            Assert.NotNull(indexDoubles_3);

            Core.Classes.IndexedDoubles? indexDoubles_4 = Convert.ToDiGi<Core.Classes.IndexedDoubles>(string_2)?.FirstOrDefault();
            Assert.NotNull(indexDoubles_4);

            Assert.Equal(indexDoubles_3.ToSystem_String(), string_1);

            Assert.Equal(indexDoubles_4.ToSystem_String(), string_2);

            Query.SerializationCheck(indexDoubles_1);
        }
    }
}