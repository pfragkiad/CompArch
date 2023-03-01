//TODO: Put full settings to file.
//TODO: Load with dependencies.
//TODO: CDB priority with maximum concurrent WBs (writeBacksPerCycle too)
//TODO: ROB capacity


var app = App.GetApp();

//var branchPredictor = app.Services.GetBranchPredictor();
//branchPredictor.Predict(BranchPredictionScenario.S2008_2Ai);

//var test = app.Services.GetRequiredService<IConfiguration>();
//Console.WriteLine(test["Test"]);

var scenarios = app.Services.GetTomasuloScenarios();

//scenarios.RunManualScenario();
scenarios.RunScenario("../../../Scenarios/2008_3A.txt");
//scenarios.RunScenario("../../../Scenarios/2008_3B.txt");