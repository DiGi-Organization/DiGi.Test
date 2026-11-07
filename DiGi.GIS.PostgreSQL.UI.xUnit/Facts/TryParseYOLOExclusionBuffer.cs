using DiGi.GIS.YOLO.UI.Classes;
using System.Globalization;
using System.Threading;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the exclusion-buffer rule of the training dialog (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#39): empty means the class default, a finite non-negative invariant-culture number is accepted, and a negative, non-finite, comma-decimal or non-numeric text is refused with a reason.
        /// <para>Asserted under pl-PL as well, where <c>250,5</c> would otherwise read as a number.</para>
        /// </summary>
        [Fact]
        public void TryParseYOLOExclusionBuffer()
        {
            CultureInfo cultureInfo = Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach (string culture in new[] { "en-US", "pl-PL" })
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);

                    double exclusionBuffer_Default = new YOLOTrainingDatasetOptions().ExclusionBuffer;

                    Assert.True(Query.TryParseYOLOExclusionBuffer(null, out double exclusionBuffer, out string? reason));
                    Assert.Equal(exclusionBuffer_Default, exclusionBuffer);
                    Assert.Null(reason);

                    Assert.True(Query.TryParseYOLOExclusionBuffer("   ", out exclusionBuffer, out _));
                    Assert.Equal(exclusionBuffer_Default, exclusionBuffer);

                    Assert.True(Query.TryParseYOLOExclusionBuffer(" 250 ", out exclusionBuffer, out _));
                    Assert.Equal(250, exclusionBuffer);

                    Assert.True(Query.TryParseYOLOExclusionBuffer("250.5", out exclusionBuffer, out _));
                    Assert.Equal(250.5, exclusionBuffer);

                    Assert.True(Query.TryParseYOLOExclusionBuffer("0", out exclusionBuffer, out _));
                    Assert.Equal(0, exclusionBuffer);

                    foreach (string text in new[] { "-1", "250,5", "NaN", "Infinity", "metres" })
                    {
                        Assert.False(Query.TryParseYOLOExclusionBuffer(text, out _, out reason));
                        Assert.NotNull(reason);
                    }
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = cultureInfo;
            }
        }
    }
}
