using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.Laya.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies what <see cref="Modify.PredictAsync(IEnumerable{Classes.LayaRequest}?, Classes.LayaPredictionOptions?, System.Threading.CancellationToken)"/> does around the process, with fake interpreters (.cmd files) standing in for CPython so no Python or laya is needed: the scripts and requests.jsonl are written (the options' token budget filled in only where a request sets none), a responses.jsonl left by an earlier run is deleted before the process starts, and a failed process is reported with its exit code and no responses.
        /// <para>With a fake interpreter that copies a prepared responses file into place, it verifies the reading: responses are matched to requests by id and returned in request order; a request that cannot be written is left out and named while the rest still run; an error line names the refused request and is not also reported as missing; a request without a line, a line for an unknown id and a line that is not JSON are each named; a response missing an answer or truncated by Laya is kept and named; and every finding about one request is also in <see cref="Classes.LayaPredictionResult.RequestMessages"/> under that request's id, while the findings naming no request of the run are not.</para>
        /// <para>Also verifies the refusals that start no process - ids used twice, a checkpoint directory that does not resolve, a calibration file that does not exist - and that no requests give an empty result while <c>null</c> requests or options give <c>null</c>.</para>
        /// </summary>
        [Fact]
        public async Task Modify_PredictAsync_Responses()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            string directory = Path.Combine(Path.GetTempPath(), string.Concat("Laya_PredictAsync_", Guid.NewGuid().ToString("N")));

            try
            {
                string directory_Checkpoint = Path.Combine(directory, "checkpoint");
                Directory.CreateDirectory(directory_Checkpoint);
                File.WriteAllText(Path.Combine(directory_Checkpoint, "rl_agent_config.json"), "{}");

                Classes.LayaChoiceQuestion layaChoiceQuestion = new("department", "Which team should handle this?", [new Classes.LayaCriterion("billing", "invoices, refunds"), new Classes.LayaCriterion("technical", null)]);
                Classes.LayaScoreQuestion layaScoreQuestion = new("urgency", "How urgent is this request?", ["low", "mid", "high"]);
                Classes.LayaNoulQuestion layaNoulQuestion = new("refund", "The customer asks for a refund.");

                Classes.LayaRequest layaRequest_1 = new("item-1", "{\"subject\":\"Double charge\",\"body\":\"We were billed twice for March.\"}", true, [layaChoiceQuestion, layaScoreQuestion, layaNoulQuestion], 1024, 256);
                Classes.LayaRequest layaRequest_2 = new("item-2", "Please refund me.", false, [layaNoulQuestion], null, null);
                Classes.LayaRequest layaRequest_3 = new("item-3", "Where is my parcel?", false, [layaNoulQuestion], null, null);
                Classes.LayaRequest layaRequest_4 = new("item-4", " ", false, [layaNoulQuestion], null, null);
                Classes.LayaRequest layaRequest_5 = new("item-5", "Refund and fix the app.", false, [layaNoulQuestion, layaChoiceQuestion], null, null);

                //A failing interpreter: the stale responses file is gone afterwards and nothing is read back
                string path_Exit = Path.Combine(directory, "exit3.cmd");
                File.WriteAllText(path_Exit, "@exit /b 3\r\n");

                string directory_Failed = Path.Combine(directory, "failed");
                Directory.CreateDirectory(directory_Failed);
                File.WriteAllText(Path.Combine(directory_Failed, "responses.jsonl"), File.ReadAllText(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "Laya_Response.jsonl")!));

                Classes.LayaPredictionResult? layaPredictionResult_Failed = await new Classes.LayaRequest[] { layaRequest_1 }.PredictAsync(new Classes.LayaPredictionOptions() { PythonPath = path_Exit, ModelPath = directory_Checkpoint, WorkingDirectory = directory_Failed });
                Assert.NotNull(layaPredictionResult_Failed);
                Assert.Equal(3, layaPredictionResult_Failed!.ExitCode);
                Assert.Empty(layaPredictionResult_Failed.Responses!);
                Assert.Contains(layaPredictionResult_Failed.Messages!, x => x.Contains("exited with code 3"));
                Assert.False(File.Exists(Path.Combine(directory_Failed, "responses.jsonl")));
                Assert.True(File.Exists(Path.Combine(directory_Failed, "predict.py")));
                Assert.True(File.Exists(Path.Combine(directory_Failed, "utils.py")));
                Assert.Single(File.ReadAllLines(Path.Combine(directory_Failed, "requests.jsonl")));
                Assert.NotNull(layaPredictionResult_Failed.Duration);
                Assert.Equal(Python.Query.DirectorySHA256(directory_Checkpoint), layaPredictionResult_Failed.ModelSHA256);
                Assert.Null(layaPredictionResult_Failed.CalibrationSHA256);
                Assert.False(layaPredictionResult_Failed.Stalled || layaPredictionResult_Failed.TimedOut || layaPredictionResult_Failed.Cancelled);

                //An interpreter that "answers" by copying a prepared responses file into place
                string directory_Answered = Path.Combine(directory, "answered");
                string path_Prepared = Path.Combine(directory, "prepared.jsonl");
                File.WriteAllLines(path_Prepared,
                [
                    File.ReadAllLines(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "Laya_Response.jsonl")!)[0],
                    "{\"id\": \"item-2\", \"error\": \"ValueError: question 'refund': refused\"}",
                    "{\"id\": \"item-9\", \"answers\": {}}",
                    "not json",
                    "{\"id\": \"item-5\", \"answers\": {\"refund\": {\"type\": \"noul\", \"noul\": 0.5}}, \"usage\": {\"input_tokens\": 1024, \"truncated\": true}}"
                ]);

                string path_Copy = Path.Combine(directory, "copy.cmd");
                File.WriteAllText(path_Copy, string.Concat("@copy /y \"", path_Prepared, "\" \"", Path.Combine(directory_Answered, "responses.jsonl"), "\" >nul\r\n@exit /b 0\r\n"));

                Classes.LayaPredictionResult? layaPredictionResult = await new Classes.LayaRequest[] { layaRequest_1, layaRequest_2, layaRequest_3, layaRequest_4, layaRequest_5 }.PredictAsync(new Classes.LayaPredictionOptions() { PythonPath = path_Copy, ModelPath = directory_Checkpoint, WorkingDirectory = directory_Answered, MaxLength = 512 });
                Assert.NotNull(layaPredictionResult);
                Assert.Equal(0, layaPredictionResult!.ExitCode);
                Assert.Equal(["item-1", "item-5"], layaPredictionResult.Responses!.Select(x => x.RequestId));
                Assert.Equal("billing", Assert.IsType<Classes.LayaChoiceAnswer>(layaPredictionResult.Responses![0].Answers![0]).Label);
                Assert.True(layaPredictionResult.Responses![1].Truncated);

                List<string> messages = layaPredictionResult.Messages!;
                Assert.Contains(messages, x => x.Contains("'item-4' was left out"));
                Assert.Contains(messages, x => x.Contains("'item-2' was refused by Laya: ValueError: question 'refund': refused"));
                Assert.DoesNotContain(messages, x => x.Contains("'item-2' got no response"));
                Assert.Contains(messages, x => x.Contains("'item-3' got no response"));
                Assert.Contains(messages, x => x.Contains("id 'item-9'"));
                Assert.Contains(messages, x => x.StartsWith("Line 4 of responses.jsonl is not a JSON object", StringComparison.CurrentCulture));
                Assert.Contains(messages, x => x.Contains("'item-5' got no answer to: department"));
                Assert.Contains(messages, x => x.Contains("'item-5' was truncated"));
                Assert.DoesNotContain(messages, x => x.Contains("'item-1'"));

                //Every finding about one request is also filed under its id; the line answering no request and the malformed line are not
                List<Classes.LayaRequestMessage> layaRequestMessages = layaPredictionResult.RequestMessages!;
                Assert.Equal(["item-4", "item-2", "item-3", "item-5", "item-5"], layaRequestMessages.Select(x => x.RequestId));
                Assert.All(layaRequestMessages, x => Assert.Contains(x.Message, messages));
                Assert.All(layaRequestMessages, x => Assert.Contains(string.Concat("'", x.RequestId, "'"), x.Message));
                Assert.Equal(messages.Count - 2, layaRequestMessages.Count);

                //item-4 was left out; the options' budget fills only the requests that set none
                List<JsonObject> jsonObjects_Requests = [.. File.ReadAllLines(Path.Combine(directory_Answered, "requests.jsonl")).Select(x => (JsonObject)JsonNode.Parse(x)!)];
                Assert.Equal(["item-1", "item-2", "item-3", "item-5"], jsonObjects_Requests.Select(x => x["id"]!.GetValue<string>()));
                Assert.Equal(1024, jsonObjects_Requests[0]["max_len"]!.GetValue<int>());
                Assert.Equal(512, jsonObjects_Requests[1]["max_len"]!.GetValue<int>());
                Assert.False(jsonObjects_Requests[1].ContainsKey("head_max_len"));

                //Refusals that start no process: the working directory is never even created
                string directory_Refused = Path.Combine(directory, "refused");

                Classes.LayaPredictionResult? layaPredictionResult_Duplicate = await new Classes.LayaRequest[] { layaRequest_2, layaRequest_2 }.PredictAsync(new Classes.LayaPredictionOptions() { PythonPath = path_Copy, ModelPath = directory_Checkpoint, WorkingDirectory = directory_Refused });
                Assert.Equal(-1, layaPredictionResult_Duplicate!.ExitCode);
                Assert.Contains(layaPredictionResult_Duplicate.Messages!, x => x.Contains("'item-2' is used 2 times"));
                Assert.Null(layaPredictionResult_Duplicate.Duration);
                Assert.Empty(layaPredictionResult_Duplicate.RequestMessages!);

                Classes.LayaPredictionResult? layaPredictionResult_Model = await new Classes.LayaRequest[] { layaRequest_2 }.PredictAsync(new Classes.LayaPredictionOptions() { PythonPath = path_Copy, ModelPath = "multilingual", WorkingDirectory = directory_Refused });
                Assert.Equal(-1, layaPredictionResult_Model!.ExitCode);
                Assert.Contains(layaPredictionResult_Model.Messages!, x => x.Contains("Hub id is not accepted"));

                Classes.LayaPredictionResult? layaPredictionResult_Calibration = await new Classes.LayaRequest[] { layaRequest_2 }.PredictAsync(new Classes.LayaPredictionOptions() { PythonPath = path_Copy, ModelPath = directory_Checkpoint, CalibrationPath = Path.Combine(directory, "missing.json"), WorkingDirectory = directory_Refused });
                Assert.Equal(-1, layaPredictionResult_Calibration!.ExitCode);
                Assert.Contains(layaPredictionResult_Calibration.Messages!, x => x.Contains("calibration file"));

                Assert.False(Directory.Exists(directory_Refused));

                Classes.LayaPredictionResult? layaPredictionResult_Empty = await Array.Empty<Classes.LayaRequest>().PredictAsync(new Classes.LayaPredictionOptions());
                Assert.Equal(0, layaPredictionResult_Empty!.ExitCode);
                Assert.Empty(layaPredictionResult_Empty.Responses!);
                Assert.Empty(layaPredictionResult_Empty.Messages!);

                Assert.Null(await ((IEnumerable<Classes.LayaRequest>?)null).PredictAsync(new Classes.LayaPredictionOptions()));
                Assert.Null(await new Classes.LayaRequest[] { layaRequest_2 }.PredictAsync(null));
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
