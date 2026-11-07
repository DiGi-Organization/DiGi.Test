using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using DiGi.GIS.YOLO.UI.Classes;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the progress bar in the bottom-left corner of the Year Built prediction options window is shown only while the prediction coverage is being read (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#40).
        /// <para>Four windows: one given no read, which never shows the bar; one whose read is still pending, which shows it until the coverage is set; one whose read completes at once, and one whose read throws at once - both of which have hidden it again by the time the constructor returns. The pending read is never completed: its continuation would be posted to a dispatcher this thread does not pump, so the coverage is set directly instead, which is the call the read ends in.</para>
        /// </summary>
        [Fact]
        public void YearBuiltPredictionsOptionsWindow_CoverageProgress()
        {
            Exception? exception = null;
            Visibility visibility_NoRead = Visibility.Visible;
            Visibility visibility_Pending = Visibility.Collapsed;
            Visibility visibility_PendingSet = Visibility.Visible;
            Visibility visibility_Completed = Visibility.Visible;
            Visibility visibility_Failed = Visibility.Visible;
            string? status_Failed = null;

            List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
            [
                new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
            ];

            Dictionary<int, double> coverageFactors = new()
            {
                [22138] = 0.97
            };

            Thread thread = new(() =>
            {
                try
                {
                    YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new() { CountyIds = [22138] };

                    YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow_NoRead = new(yearBuiltPredictionPipelineOptions, administrativeAreal2DReferences);
                    visibility_NoRead = ProgressBar(yearBuiltPredictionsOptionsWindow_NoRead).Visibility;

                    TaskCompletionSource<Dictionary<int, double>?> taskCompletionSource = new();
                    YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow_Pending = new(yearBuiltPredictionPipelineOptions, administrativeAreal2DReferences, _ => taskCompletionSource.Task);
                    visibility_Pending = ProgressBar(yearBuiltPredictionsOptionsWindow_Pending).Visibility;
                    yearBuiltPredictionsOptionsWindow_Pending.SetCoverageFactors(coverageFactors);
                    visibility_PendingSet = ProgressBar(yearBuiltPredictionsOptionsWindow_Pending).Visibility;

                    YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow_Completed = new(yearBuiltPredictionPipelineOptions, administrativeAreal2DReferences, _ => Task.FromResult<Dictionary<int, double>?>(coverageFactors));
                    visibility_Completed = ProgressBar(yearBuiltPredictionsOptionsWindow_Completed).Visibility;

                    YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow_Failed = new(yearBuiltPredictionPipelineOptions, administrativeAreal2DReferences, _ => Task.FromException<Dictionary<int, double>?>(new InvalidOperationException("unreachable")));
                    visibility_Failed = ProgressBar(yearBuiltPredictionsOptionsWindow_Failed).Visibility;
                    status_Failed = ((Label)yearBuiltPredictionsOptionsWindow_Failed.FindName("Label_Coverage")!).Content as string;
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

            Assert.Equal(Visibility.Collapsed, visibility_NoRead);
            Assert.Equal(Visibility.Visible, visibility_Pending);
            Assert.Equal(Visibility.Collapsed, visibility_PendingSet);
            Assert.Equal(Visibility.Collapsed, visibility_Completed);
            Assert.Equal(Visibility.Collapsed, visibility_Failed);
            Assert.Equal("Prediction coverage could not be read - no county is marked", status_Failed);

            static ProgressBar ProgressBar(YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow)
            {
                return (ProgressBar)yearBuiltPredictionsOptionsWindow.FindName("ProgressBar_Coverage")!;
            }
        }
    }
}
