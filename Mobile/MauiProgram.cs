using Microsoft.Extensions.Logging;

namespace Stealth.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		// Register pages
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<LogsPage>();

#if DEBUG
		builder.Services.AddLogging(logging =>
		{
			logging.AddDebug();
		});
#endif

		return builder.Build();
	}
}
