using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DiGi.Laya.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Modify.PredictAsync(IEnumerable{Classes.LayaRequest}?, Classes.LayaPredictionOptions?, System.Threading.CancellationToken)"/> answers a two-item request with the real interpreter and checkpoint of DiGi.Laya_Preflight.conf: exit code 0, no messages, one response per request in request order, an answer of the right type to every question, a choice label among the criteria with probabilities summing to 1, a score level within the levels and a noul probability between 0 and 1, and the SHA-256 of the checkpoint directory the run used.
        /// <para>The requests are those of Laya_Request.jsonl, the input the fixture Laya_Response.jsonl was captured from. The interpreter and checkpoint are machine specific, so the fact returns without asserting when the conf, its interpreter or its checkpoint is absent.</para>
        /// <para>Medium test (7 s on an RTX 5090, 2026-10-15): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public async Task Modify_PredictAsync()
        {
            string directory = Path.Combine(Path.GetTempPath(), string.Concat("Laya_Predict_", Guid.NewGuid().ToString("N")));

            try
            {
                Classes.LayaPredictionOptions? layaPredictionOptions = PreflightOptions(directory);
                if (layaPredictionOptions == null)
                {
                    return;
                }

                Classes.LayaChoiceQuestion layaChoiceQuestion = new("department", "Which team should handle this?", [new Classes.LayaCriterion("billing", "invoices, refunds"), new Classes.LayaCriterion("technical", null)]);
                Classes.LayaScoreQuestion layaScoreQuestion = new("urgency", "How urgent is this request?", ["low", "mid", "high"]);
                Classes.LayaNoulQuestion layaNoulQuestion = new("refund", "The customer asks for a refund.");

                List<Classes.LayaRequest> layaRequests =
                [
                    new Classes.LayaRequest("item-1", "{\"subject\":\"Double charge\",\"body\":\"We were billed twice for March.\"}", true, [layaChoiceQuestion, layaScoreQuestion, layaNoulQuestion], 1024, 256),
                    new Classes.LayaRequest("item-2", "Proszę o zwrot pieniędzy za podwójną opłatę.", false, [layaNoulQuestion], null, null)
                ];

                Classes.LayaPredictionResult? layaPredictionResult = await layaRequests.PredictAsync(layaPredictionOptions);
                Assert.NotNull(layaPredictionResult);
                Assert.True(layaPredictionResult!.ExitCode == 0, string.Join(Environment.NewLine, layaPredictionResult.Messages ?? []));
                Assert.Empty(layaPredictionResult.Messages!);
                Assert.Equal(Python.Query.DirectorySHA256(layaPredictionOptions.ModelPath), layaPredictionResult.ModelSHA256);
                Assert.NotNull(layaPredictionResult.Duration);
                Assert.Equal(["item-1", "item-2"], layaPredictionResult.Responses!.Select(x => x.RequestId));

                Classes.LayaResponse layaResponse = layaPredictionResult.Responses![0];
                Assert.Equal(["department", "urgency", "refund"], layaResponse.Answers!.Select(x => x.Name));
                Assert.True(layaResponse.InputTokenCount > 0);
                Assert.False(layaResponse.Truncated);

                Classes.LayaChoiceAnswer layaChoiceAnswer = Assert.IsType<Classes.LayaChoiceAnswer>(layaResponse.Answers![0]);
#pragma warning disable CA1861 // a one-off assertion; hoisting the array buys nothing
                Assert.Contains(layaChoiceAnswer.Label, new string[] { "billing", "technical" });
#pragma warning restore CA1861
                Assert.Equal(["billing", "technical"], layaChoiceAnswer.Probabilities!.Select(x => x.Label));
                Assert.InRange(layaChoiceAnswer.Probabilities!.Sum(x => x.Value), 0.999, 1.001);

                Classes.LayaScoreAnswer layaScoreAnswer = Assert.IsType<Classes.LayaScoreAnswer>(layaResponse.Answers![1]);
                Assert.InRange(layaScoreAnswer.Level!.Value, 0.0, 2.0);
                Assert.Equal(3, layaScoreAnswer.Distribution!.Count);

                Classes.LayaNoulAnswer layaNoulAnswer = Assert.IsType<Classes.LayaNoulAnswer>(layaResponse.Answers![2]);
                Assert.InRange(layaNoulAnswer.Probability!.Value, 0.0, 1.0);

                Classes.LayaNoulAnswer layaNoulAnswer_Second = Assert.IsType<Classes.LayaNoulAnswer>(Assert.Single(layaPredictionResult.Responses![1].Answers!));
                Assert.InRange(layaNoulAnswer_Second.Probability!.Value, 0.0, 1.0);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
