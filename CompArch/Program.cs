
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Numerics;
using static System.Net.Mime.MediaTypeNames;

var app = App.GetApp();

//var branchPredictor = app.Services.GetBranchPredictor();
//branchPredictor.Predict(BranchPredictionScenario.S2008_2Ai);

//var test = app.Services.GetRequiredService<IConfiguration>();
//Console.WriteLine(test["Test"]);

var scenarios = app.Services.GetTomasuloScenarios();

//scenarios.RunManualScenario();
scenarios.RunScenario("../../../Scenarios/2008_3A.txt");