//TODO: Load with dependencies.
//TODO: CDB priority with maximum concurrent WBs (multiple writeBacksPerCycle are pending)
//TODO: ROB capacity

var app = App.GetApp();

//var branchPredictor = app.Services.GetBranchPredictor();
//branchPredictor.Predict(BranchPredictionScenario.S2008_2Ai);

//var configuration = app.Services.GetRequiredService<IConfiguration>();
//Console.WriteLine(configuration["Test"]);

//ScenarioPaths options2 = new ScenarioPaths();
//configuration.GetSection("ScenarioPaths").Bind(options2);
// or 
//var settings = app.Services.GetRequiredService<IOptions<ScenarioPaths>>().Value;

var loader = app.Services.GetTomasuloScenarioReader();
var paths = app.Services.GetTomasuloScenarioPaths();

if(!string.IsNullOrWhiteSpace(paths.SelectedPath))
    loader.RunScenario(paths.SelectedPath);
else
    loader.RunManualScenario();
