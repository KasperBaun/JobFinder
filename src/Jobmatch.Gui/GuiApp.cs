using Jobmatch.Gui.Server;

namespace Jobmatch.Gui;

public static class GuiApp
{
    public static async Task Run()
    {
        var ctx = Jobmatch.UserContext.Resolve();
        await GuiServer.RunAsync(ctx);
    }
}
