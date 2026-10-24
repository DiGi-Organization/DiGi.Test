using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using DiGi.GIS.YOLO.UI.Classes;
using DiGi.UI.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the county list of the Year Built prediction options window shows the coverage marker per part id, that coverage arriving after the list is filled redraws the labels in place without losing the selection, and that a failed read marks nothing and says so.
        /// <para>The coverage is read in the background and normally lands while the operator is already picking counties, so a redraw that rebuilt the list would throw that selection away. The two pieces of a multi-part county are given different coverage to show the marker follows the part id, not the code they share. A read that fails must leave every county unmarked - no marker read as "not run" is the mistake the status line exists to prevent.</para>
        /// </summary>
        [Fact]
        public void YearBuiltPredictionsOptionsWindow_Coverage()
        {
            Exception? exception = null;
            List<string?> texts_Before = [];
            List<string?> texts_After = [];
            List<string?> texts_Failed = [];
            List<string?> texts_Started = [];
            int selectedCount_After = -1;
            string? status_After = null;
            string? status_Failed = null;
            string? status_Started = null;

            List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
            [
                new() { Id = 4816, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 22139, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
            ];

            Dictionary<int, double> coverageFactors = new()
            {
                [4816] = 0.05,
                [22138] = 0.97
            };

            Thread thread = new(() =>
            {
                try
                {
                    YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new() { CountyIds = [22138, 22139] };

                    // Read later: the list is filled, two counties are selected, then the coverage arrives.
                    YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow = new(yearBuiltPredictionPipelineOptions, administrativeAreal2DReferences);
                    texts_Before = Texts(yearBuiltPredictionsOptionsWindow);

                    yearBuiltPredictionsOptionsWindow.SetCoverageFactors(coverageFactors);
                    texts_After = Texts(yearBuiltPredictionsOptionsWindow);
                    selectedCount_After = ListBoxControl(yearBuiltPredictionsOptionsWindow).GetItems<AdministrativeAreal2DReference>()?.Count ?? 0;
                    status_After = Status(yearBuiltPredictionsOptionsWindow);

                    yearBuiltPredictionsOptionsWindow.SetCoverageFactors(null);
                    texts_Failed = Texts(yearBuiltPredictionsOptionsWindow);
                    status_Failed = Status(yearBuiltPredictionsOptionsWindow);

                    // Through the constructor: a read that completes at once is applied as the window is built.
                    YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow_Started = new(yearBuiltPredictionPipelineOptions, administrativeAreal2DReferences, _ => Task.FromResult<Dictionary<int, double>?>(coverageFactors));
                    texts_Started = Texts(yearBuiltPredictionsOptionsWindow_Started);
                    status_Started = Status(yearBuiltPredictionsOptionsWindow_Started);
                }
                catch (Exception exception_Thread)
                {
                    exception = exception_Thread;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(exception);

            Assert.Equal(["0201 boleslawiecki (id 4816)", "2412 rybnicki (id 22138)", "2412 rybnicki (id 22139)"], texts_Before);
            Assert.Equal(["0201 boleslawiecki (id 4816)", "2412 rybnicki (id 22138) (>10%)", "2412 rybnicki (id 22139)"], texts_After);
            Assert.Equal(2, selectedCount_After);
            Assert.StartsWith("(>10%)", status_After);

            Assert.Equal(texts_Before, texts_Failed);
            Assert.Equal("Prediction coverage could not be read - no county is marked", status_Failed);

            Assert.Equal(texts_After, texts_Started);
            Assert.StartsWith("(>10%)", status_Started);

            static ListBoxControl ListBoxControl(YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow)
            {
                return (ListBoxControl)yearBuiltPredictionsOptionsWindow.FindName("ListBoxControl_Counties")!;
            }

            static List<string?> Texts(YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow)
            {
                List<string?> result = [];
                foreach (object @object in ((ListBox)ListBoxControl(yearBuiltPredictionsOptionsWindow).FindName("ListBox_Main")!).Items)
                {
                    result.Add((@object as ListBoxItem)?.Content as string);
                }

                return result;
            }

            static string? Status(YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow)
            {
                return ((Label)yearBuiltPredictionsOptionsWindow.FindName("Label_Coverage")!).Content as string;
            }
        }
    }
}
