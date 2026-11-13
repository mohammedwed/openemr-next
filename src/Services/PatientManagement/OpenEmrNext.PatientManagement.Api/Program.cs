using OpenEmrNext.PatientManagement.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddPatientManagementHttpDependencies();
var app = builder.Build();
app.Run();
