using Bestia.Web.Configuracao;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AdicionarBestia(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UsarBestia();
app.Run();

public partial class Program;
